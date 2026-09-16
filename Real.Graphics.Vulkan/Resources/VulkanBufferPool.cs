using System;
using System.Collections.Generic;
using Real.Graphics.Rhi.Descriptors;
using Real.Graphics.Rhi.Enums;
using Real.Graphics.Rhi.Handles;
using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace Real.Graphics.Vulkan.Resources;

internal readonly struct VulkanBufferEntry
{
    public readonly Buffer Handle;
    public readonly DeviceMemory Memory;
    public readonly ulong Size;
    public readonly BufferUsage Usage;

    public VulkanBufferEntry(Buffer handle, DeviceMemory memory, ulong size, BufferUsage usage)
    {
        Handle = handle;
        Memory = memory;
        Size = size;
        Usage = usage;
    }
}

/// <summary>
/// Slot-based pool mapping the opaque BufferHandle (Id + Generation) exposed by
/// Real.Graphics.Rhi to the underlying VkBuffer/VkDeviceMemory pair.
/// The generation counter catches use-after-destroy at the handle level.
/// </summary>
internal sealed class VulkanBufferPool : IDisposable
{
    private readonly Vk _vk;
    private readonly Device _device;
    private readonly VulkanMemoryAllocator _allocator;
    // Needed only to upload initialData via a staging buffer at creation time.
    // This is a simple, blocking upload (submit + wait) - fine for load-time
    // resource creation; a non-blocking batched upload queue for runtime resource
    // streaming is a separate concern we're deliberately not building yet.
    private readonly Queue _transferQueue;
    private readonly CommandPool _uploadCommandPool;

    private readonly List<VulkanBufferEntry?> _slots = new();
    private readonly List<uint> _generations = new();
    private readonly Queue<uint> _freeSlots = new();

    public unsafe VulkanBufferPool(
        Vk vk,
        Device device,
        VulkanMemoryAllocator allocator,
        Queue transferQueue,
        uint transferQueueFamilyIndex)
    {
        _vk = vk;
        _device = device;
        _allocator = allocator;
        _transferQueue = transferQueue;

        // Reserve slot 0 so valid handles always have Id > 0 (BufferHandle.Invalid is (0, 0))
        _slots.Add(null);
        _generations.Add(0);

        var poolInfo = new CommandPoolCreateInfo
        {
            SType = StructureType.CommandPoolCreateInfo,
            QueueFamilyIndex = transferQueueFamilyIndex,
            Flags = CommandPoolCreateFlags.TransientBit | CommandPoolCreateFlags.ResetCommandBufferBit
        };

        var result = _vk.CreateCommandPool(_device, in poolInfo, null, out _uploadCommandPool);
        if (result != Result.Success)
            throw new InvalidOperationException($"vkCreateCommandPool (upload) failed: {result}");
    }

    public unsafe void Dispose()
    {
        if (_uploadCommandPool.Handle != 0)
            _vk.DestroyCommandPool(_device, _uploadCommandPool, null);

        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i] is { } entry)
            {
                _vk.DestroyBuffer(_device, entry.Handle, null);
                _allocator.Free(entry.Memory);
                _slots[i] = null;
            }
        }
        _freeSlots.Clear();
    }

    public unsafe BufferHandle Create(in BufferDescriptor descriptor, ReadOnlySpan<byte> initialData)
    {
        var usageFlags = ToVkUsage(descriptor.Usage);
        if (!initialData.IsEmpty)
            usageFlags |= BufferUsageFlags.TransferDstBit;

        var (buffer, memory) = CreateVkBuffer(
            descriptor.Size,
            usageFlags,
            descriptor.CpuVisible
                ? MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit
                : MemoryPropertyFlags.DeviceLocalBit);

        if (!initialData.IsEmpty)
            UploadInitialData(buffer, descriptor, initialData);

        var entry = new VulkanBufferEntry(buffer, memory, descriptor.Size, descriptor.Usage);
        return Store(entry);
    }

    private unsafe (Buffer Buffer, DeviceMemory Memory) CreateVkBuffer(
        ulong size, BufferUsageFlags usage, MemoryPropertyFlags memoryProperties)
    {
        var bufferInfo = new BufferCreateInfo
        {
            SType = StructureType.BufferCreateInfo,
            Size = size,
            Usage = usage,
            SharingMode = SharingMode.Exclusive
        };

        // --- 1. vk.CreateBuffer ---
        var result = _vk.CreateBuffer(_device, in bufferInfo, null, out var buffer);
        if (result != Result.Success)
            throw new InvalidOperationException($"vkCreateBuffer failed: {result}");

        // --- 2. vk.GetBufferMemoryRequirements ---
        _vk.GetBufferMemoryRequirements(_device, buffer, out var requirements);

        // --- 3. allocate + bind ---
        var memory = _allocator.Allocate(requirements, memoryProperties);

        result = _vk.BindBufferMemory(_device, buffer, memory, 0);
        if (result != Result.Success)
            throw new InvalidOperationException($"vkBindBufferMemory failed: {result}");

        return (buffer, memory);
    }

    /// <summary>
    /// Uploads initialData into a DEVICE_LOCAL buffer via a temporary
    /// HOST_VISIBLE staging buffer + a one-shot copy command, submitted and
    /// waited on synchronously. Simple and correct; not meant to be called
    /// every frame (see the class-level upload-queue note above).
    /// </summary>
    private unsafe void UploadInitialData(Buffer destination, in BufferDescriptor descriptor, ReadOnlySpan<byte> initialData)
    {
        var (stagingBuffer, stagingMemory) = CreateVkBuffer(
            descriptor.Size,
            BufferUsageFlags.TransferSrcBit,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);

        void* mapped;
        _vk.MapMemory(_device, stagingMemory, 0, descriptor.Size, 0, &mapped);
        initialData.CopyTo(new Span<byte>(mapped, initialData.Length));
        _vk.UnmapMemory(_device, stagingMemory);

        var cmd = BeginOneShotCommandBuffer();
        var copyRegion = new BufferCopy { SrcOffset = 0, DstOffset = 0, Size = descriptor.Size };
        _vk.CmdCopyBuffer(cmd, stagingBuffer, destination, 1, in copyRegion);
        EndAndSubmitOneShotCommandBuffer(cmd);

        // Staging buffer is only needed for the duration of the copy; safe to
        // destroy immediately after QueueWaitIdle in EndAndSubmit... below.
        _vk.DestroyBuffer(_device, stagingBuffer, null);
        _allocator.Free(stagingMemory);
    }

    private unsafe CommandBuffer BeginOneShotCommandBuffer()
    {
        var allocInfo = new CommandBufferAllocateInfo
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = _uploadCommandPool,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = 1
        };

        _vk.AllocateCommandBuffers(_device, in allocInfo, out var cmd);

        var beginInfo = new CommandBufferBeginInfo
        {
            SType = StructureType.CommandBufferBeginInfo,
            Flags = CommandBufferUsageFlags.OneTimeSubmitBit
        };
        _vk.BeginCommandBuffer(cmd, in beginInfo);

        return cmd;
    }

    private unsafe void EndAndSubmitOneShotCommandBuffer(CommandBuffer cmd)
    {
        _vk.EndCommandBuffer(cmd);

        var submitInfo = new SubmitInfo
        {
            SType = StructureType.SubmitInfo,
            CommandBufferCount = 1,
            PCommandBuffers = &cmd
        };

        _vk.QueueSubmit(_transferQueue, 1, in submitInfo, default);
        // Blocking wait: acceptable for load-time uploads, not for per-frame
        // streaming - a fence-based non-blocking path belongs to the future
        // upload queue, not this method.
        _vk.QueueWaitIdle(_transferQueue);

        _vk.FreeCommandBuffers(_device, _uploadCommandPool, 1, in cmd);
    }

    private static BufferUsageFlags ToVkUsage(BufferUsage usage)
    {
        var flags = BufferUsageFlags.None;
        if (usage.HasFlag(BufferUsage.Vertex)) flags |= BufferUsageFlags.VertexBufferBit;
        if (usage.HasFlag(BufferUsage.Index)) flags |= BufferUsageFlags.IndexBufferBit;
        if (usage.HasFlag(BufferUsage.Uniform)) flags |= BufferUsageFlags.UniformBufferBit;
        if (usage.HasFlag(BufferUsage.Storage)) flags |= BufferUsageFlags.StorageBufferBit;
        if (usage.HasFlag(BufferUsage.TransferSrc)) flags |= BufferUsageFlags.TransferSrcBit;
        if (usage.HasFlag(BufferUsage.TransferDst)) flags |= BufferUsageFlags.TransferDstBit;
        return flags;
    }

    public unsafe void Destroy(BufferHandle handle)
    {
        if (!IsValid(handle)) return;

        var entry = _slots[(int)handle.Id]!.Value;

        // NOTE: as flagged before - a real implementation must defer this until
        // the GPU is done with any frame that referenced this buffer (deferred-
        // delete queue keyed by frame fence), not destroy it immediately.
        _vk.DestroyBuffer(_device, entry.Handle, null);
        _allocator.Free(entry.Memory);

        _slots[(int)handle.Id] = null;
        _generations[(int)handle.Id]++;
        _freeSlots.Enqueue(handle.Id);
    }

    public bool IsValid(BufferHandle handle) =>
        handle.Id != 0 &&
        handle.Id < _slots.Count &&
        _slots[(int)handle.Id] is not null &&
        _generations[(int)handle.Id] == handle.Generation;

    public VulkanBufferEntry Get(BufferHandle handle) => _slots[(int)handle.Id]!.Value;

    private BufferHandle Store(VulkanBufferEntry entry)
    {
        uint id;
        if (_freeSlots.Count > 0)
        {
            id = _freeSlots.Dequeue();
            _slots[(int)id] = entry;
        }
        else
        {
            id = (uint)_slots.Count;
            _slots.Add(entry);
            _generations.Add(0);
        }

        return new BufferHandle(id, _generations[(int)id]);
    }
}
