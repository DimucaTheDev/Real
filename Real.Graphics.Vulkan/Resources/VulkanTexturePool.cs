using System;
using Real.Graphics.Rhi.Descriptors;
using Real.Graphics.Rhi.Enums;
using Real.Graphics.Rhi.Handles;
using Real.Graphics.Vulkan.Translation;
using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace Real.Graphics.Vulkan.Resources;

internal readonly struct VulkanTextureEntry
{
    public readonly Image Image;
    public readonly ImageView View;
    public readonly DeviceMemory Memory;
    public readonly TextureDescriptor Descriptor;
    public readonly ImageLayout CurrentLayout;

    public VulkanTextureEntry(Image image, ImageView view, DeviceMemory memory, TextureDescriptor descriptor, ImageLayout layout)
    {
        Image = image;
        View = view;
        Memory = memory;
        Descriptor = descriptor;
        CurrentLayout = layout;
    }
}

/// <summary>
/// Same slot-pool pattern as VulkanBufferPool, but also tracks the image's
/// current VkImageLayout - this is what VulkanBarrierBuilder reads/updates
/// when the render graph transitions a texture between passes.
/// </summary>
internal sealed class VulkanTexturePool : IDisposable
{
    private readonly Vk _vk;
    private readonly Device _device;
    private readonly VulkanMemoryAllocator _allocator;

    // Same rationale as VulkanBufferPool: a dedicated pool+queue for one-shot,
    // blocking uploads at resource-creation time. See VulkanUploadContext.
    private readonly VulkanUploadContext _uploadContext;

    private readonly List<VulkanTextureEntry?> _slots = new();
    private readonly List<uint> _generations = new();
    private readonly Queue<uint> _freeSlots = new();

    public VulkanTexturePool(
        Vk vk,
        Device device,
        VulkanMemoryAllocator allocator,
        VulkanUploadContext uploadContext)
    {
        _vk = vk;
        _device = device;
        _allocator = allocator;
        _uploadContext = uploadContext;

        // Reserve slot 0 so valid handles always have Id > 0 (TextureHandle.Invalid is (0, 0))
        _slots.Add(null);
        _generations.Add(0);
    }

    public unsafe TextureHandle Create(in TextureDescriptor descriptor, ReadOnlySpan<byte> initialData)
    {
        var format = VkFormatMap.ToVkFormat(descriptor.Format);
        var aspect = VkFormatMap.IsDepthFormat(descriptor.Format)
            ? ImageAspectFlags.DepthBit
            : ImageAspectFlags.ColorBit;

        var usageFlags = ToVkUsage(descriptor.Usage);
        if (!initialData.IsEmpty)
            usageFlags |= ImageUsageFlags.TransferDstBit;

        var imageInfo = new ImageCreateInfo
        {
            SType = StructureType.ImageCreateInfo,
            ImageType = ImageType.Type2D,
            Format = format,
            Extent = new Extent3D(descriptor.Width, descriptor.Height, 1),
            MipLevels = descriptor.MipLevels,
            ArrayLayers = 1,
            Samples = ToVkSampleCount(descriptor.SampleCount),
            Tiling = ImageTiling.Optimal,
            Usage = usageFlags,
            SharingMode = SharingMode.Exclusive,
            InitialLayout = ImageLayout.Undefined
        };

        // --- vk.CreateImage ---
        var result = _vk.CreateImage(_device, in imageInfo, null, out var image);
        if (result != Result.Success)
            throw new InvalidOperationException($"vkCreateImage failed: {result}");

        // --- GetImageMemoryRequirements + allocate + bind ---
        _vk.GetImageMemoryRequirements(_device, image, out var requirements);
        var memory = _allocator.Allocate(requirements, MemoryPropertyFlags.DeviceLocalBit);

        result = _vk.BindImageMemory(_device, image, memory, 0);
        if (result != Result.Success)
            throw new InvalidOperationException($"vkBindImageMemory failed: {result}");

        // --- CreateImageView ---
        var view = CreateView(image, format, aspect, descriptor.MipLevels);

        var currentLayout = ImageLayout.Undefined;

        if (!initialData.IsEmpty)
        {
            // Sampled textures end up ready to read; anything else (a storage
            // image with initial data, say) ends up in General - there's no
            // single "correct" resting layout otherwise.
            var finalLayout = descriptor.Usage.HasFlag(TextureUsage.Sampled)
                ? ImageLayout.ShaderReadOnlyOptimal
                : ImageLayout.General;

            UploadInitialData(image, descriptor, initialData, aspect, finalLayout);
            currentLayout = finalLayout;
        }
        // else: left as Undefined. A freshly created empty render target with no
        // initial data doesn't need its garbage contents preserved - the render
        // graph's first Writes() pass will transition Undefined -> ColorAttachmentOptimal
        // via VulkanBarrierBuilder, which is a valid "discard" transition.

        var entry = new VulkanTextureEntry(image, view, memory, descriptor, currentLayout);
        return Store(entry);
    }

    public TextureHandle Wrap(Image image, ImageView view, in TextureDescriptor descriptor, ImageLayout layout) =>
        Store(new VulkanTextureEntry(image, view, default, descriptor, layout));

    private unsafe ImageView CreateView(Image image, Format format, ImageAspectFlags aspect, uint mipLevels)
    {
        var viewInfo = new ImageViewCreateInfo
        {
            SType = StructureType.ImageViewCreateInfo,
            Image = image,
            ViewType = ImageViewType.Type2D,
            Format = format,
            SubresourceRange = new ImageSubresourceRange
            {
                AspectMask = aspect,
                BaseMipLevel = 0,
                LevelCount = mipLevels,
                BaseArrayLayer = 0,
                LayerCount = 1
            }
        };

        var result = _vk.CreateImageView(_device, in viewInfo, null, out var view);
        if (result != Result.Success)
            throw new InvalidOperationException($"vkCreateImageView failed: {result}");

        return view;
    }

    /// <summary>
    /// Undefined -> TransferDstOptimal -> copy -> finalLayout, via a staging
    /// buffer and a blocking one-shot command buffer (VulkanUploadContext) -
    /// same load-time-only caveat as VulkanBufferPool.UploadInitialData.
    ///
    /// NOTE: only uploads mip level 0. TextureDescriptor.MipLevels > 1 with
    /// initialData would need either per-level source data or a mipmap
    /// generation pass (vkCmdBlitImage chain) - not implemented here.
    /// </summary>
    private unsafe void UploadInitialData(
        Image image, in TextureDescriptor descriptor, ReadOnlySpan<byte> initialData,
        ImageAspectFlags aspect, ImageLayout finalLayout)
    {
        var (stagingBuffer, stagingMemory) = CreateStagingBuffer((ulong)initialData.Length);

        void* mapped;
        _vk.MapMemory(_device, stagingMemory, 0, (ulong)initialData.Length, 0, &mapped);
        initialData.CopyTo(new Span<byte>(mapped, initialData.Length));
        _vk.UnmapMemory(_device, stagingMemory);

        var cmd = _uploadContext.Begin();

        TransitionLayout(cmd, image, aspect,
            oldLayout: ImageLayout.Undefined, newLayout: ImageLayout.TransferDstOptimal,
            srcAccess: AccessFlags.None, dstAccess: AccessFlags.TransferWriteBit,
            srcStage: PipelineStageFlags.TopOfPipeBit, dstStage: PipelineStageFlags.TransferBit);

        var region = new BufferImageCopy
        {
            BufferOffset = 0,
            BufferRowLength = 0,   // 0 = tightly packed, matches descriptor.Width
            BufferImageHeight = 0,
            ImageSubresource = new ImageSubresourceLayers
            {
                AspectMask = aspect,
                MipLevel = 0,
                BaseArrayLayer = 0,
                LayerCount = 1
            },
            ImageOffset = new Offset3D(0, 0, 0),
            ImageExtent = new Extent3D(descriptor.Width, descriptor.Height, 1)
        };
        _vk.CmdCopyBufferToImage(cmd, stagingBuffer, image, ImageLayout.TransferDstOptimal, 1, in region);

        TransitionLayout(cmd, image, aspect,
            oldLayout: ImageLayout.TransferDstOptimal, newLayout: finalLayout,
            srcAccess: AccessFlags.TransferWriteBit,
            dstAccess: finalLayout == ImageLayout.ShaderReadOnlyOptimal ? AccessFlags.ShaderReadBit : AccessFlags.ShaderReadBit | AccessFlags.ShaderWriteBit,
            srcStage: PipelineStageFlags.TransferBit,
            dstStage: PipelineStageFlags.FragmentShaderBit);

        _uploadContext.EndAndSubmit(cmd);

        _vk.DestroyBuffer(_device, stagingBuffer, null);
        _allocator.Free(stagingMemory);
    }

    private unsafe (Buffer, DeviceMemory) CreateStagingBuffer(ulong size)
    {
        var bufferInfo = new BufferCreateInfo
        {
            SType = StructureType.BufferCreateInfo,
            Size = size,
            Usage = BufferUsageFlags.TransferSrcBit,
            SharingMode = SharingMode.Exclusive
        };

        var result = _vk.CreateBuffer(_device, in bufferInfo, null, out var buffer);
        if (result != Result.Success)
            throw new InvalidOperationException($"vkCreateBuffer (staging) failed: {result}");

        _vk.GetBufferMemoryRequirements(_device, buffer, out var requirements);
        var memory = _allocator.Allocate(requirements, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);
        _vk.BindBufferMemory(_device, buffer, memory, 0);

        return (buffer, memory);
    }

    private unsafe void TransitionLayout(
        CommandBuffer cmd, Image image, ImageAspectFlags aspect,
        ImageLayout oldLayout, ImageLayout newLayout,
        AccessFlags srcAccess, AccessFlags dstAccess,
        PipelineStageFlags srcStage, PipelineStageFlags dstStage)
    {
        var barrier = new ImageMemoryBarrier
        {
            SType = StructureType.ImageMemoryBarrier,
            OldLayout = oldLayout,
            NewLayout = newLayout,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Image = image,
            SubresourceRange = new ImageSubresourceRange
            {
                AspectMask = aspect,
                BaseMipLevel = 0,
                LevelCount = Vk.RemainingMipLevels,
                BaseArrayLayer = 0,
                LayerCount = Vk.RemainingArrayLayers
            },
            SrcAccessMask = srcAccess,
            DstAccessMask = dstAccess
        };

        _vk.CmdPipelineBarrier(cmd, srcStage, dstStage, DependencyFlags.None, 0, null, 0, null, 1, &barrier);
    }

    private static ImageUsageFlags ToVkUsage(TextureUsage usage)
    {
        var flags = ImageUsageFlags.None;
        if (usage.HasFlag(TextureUsage.Sampled)) flags |= ImageUsageFlags.SampledBit;
        if (usage.HasFlag(TextureUsage.RenderTarget)) flags |= ImageUsageFlags.ColorAttachmentBit;
        if (usage.HasFlag(TextureUsage.DepthStencil)) flags |= ImageUsageFlags.DepthStencilAttachmentBit;
        if (usage.HasFlag(TextureUsage.Storage)) flags |= ImageUsageFlags.StorageBit;
        if (usage.HasFlag(TextureUsage.TransferSrc)) flags |= ImageUsageFlags.TransferSrcBit;
        if (usage.HasFlag(TextureUsage.TransferDst)) flags |= ImageUsageFlags.TransferDstBit;
        return flags;
    }

    private static SampleCountFlags ToVkSampleCount(uint samples) => samples switch
    {
        1 => SampleCountFlags.Count1Bit,
        2 => SampleCountFlags.Count2Bit,
        4 => SampleCountFlags.Count4Bit,
        8 => SampleCountFlags.Count8Bit,
        _ => SampleCountFlags.Count1Bit
    };

    public unsafe void Destroy(TextureHandle handle)
    {
        if (!IsValid(handle)) return;

        var entry = _slots[(int)handle.Id]!.Value;

        // Same deferred-destruction caveat as VulkanBufferPool: destroying
        // immediately is only safe here because Destroy() is not yet wired
        // into any per-frame GPU-in-flight tracking - flagged, not solved.
        _vk.DestroyImageView(_device, entry.View, null);
        if (entry.Memory.Handle != 0) // Wrap()-ped swapchain images don't own memory
            _vk.DestroyImage(_device, entry.Image, null);
        if (entry.Memory.Handle != 0)
            _allocator.Free(entry.Memory);

        _slots[(int)handle.Id] = null;
        _generations[(int)handle.Id]++;
        _freeSlots.Enqueue(handle.Id);
    }

    public bool IsValid(TextureHandle handle) =>
        handle.Id != 0 &&
        handle.Id < _slots.Count &&
        _slots[(int)handle.Id] is not null &&
        _generations[(int)handle.Id] == handle.Generation;

    public VulkanTextureEntry Get(TextureHandle handle) => _slots[(int)handle.Id]!.Value;

    internal void UpdateLayout(TextureHandle handle, ImageLayout newLayout)
    {
        var e = _slots[(int)handle.Id]!.Value;
        _slots[(int)handle.Id] = new VulkanTextureEntry(e.Image, e.View, e.Memory, e.Descriptor, newLayout);
    }

    private TextureHandle Store(VulkanTextureEntry entry)
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

        return new TextureHandle(id, _generations[(int)id]);
    }

    public unsafe void Dispose()
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i] is { } entry)
            {
                if (entry.View.Handle != 0)
                    _vk.DestroyImageView(_device, entry.View, null);
                if (entry.Memory.Handle != 0)
                {
                    if (entry.Image.Handle != 0)
                        _vk.DestroyImage(_device, entry.Image, null);
                    _allocator.Free(entry.Memory);
                }
                _slots[i] = null;
            }
        }
        _freeSlots.Clear();
    }
}