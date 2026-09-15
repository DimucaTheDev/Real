using System;
using Silk.NET.Vulkan;

namespace Real.Graphics.Vulkan.Resources;

/// <summary>
/// Shared blocking one-shot command submission for load-time uploads
/// (buffer staging copies, texture staging copies + layout transitions).
/// Used by both VulkanBufferPool and VulkanTexturePool - not per-frame.
/// </summary>
internal sealed class VulkanUploadContext
{
    private readonly Vk _vk;
    private readonly Device _device;
    private readonly Queue _queue;
    private readonly CommandPool _commandPool;

    public unsafe VulkanUploadContext(Vk vk, Device device, Queue transferQueue, uint transferQueueFamilyIndex)
    {
        _vk = vk;
        _device = device;
        _queue = transferQueue;

        var poolInfo = new CommandPoolCreateInfo
        {
            SType = StructureType.CommandPoolCreateInfo,
            QueueFamilyIndex = transferQueueFamilyIndex,
            Flags = CommandPoolCreateFlags.TransientBit | CommandPoolCreateFlags.ResetCommandBufferBit
        };

        var result = _vk.CreateCommandPool(_device, in poolInfo, null, out _commandPool);
        if (result != Result.Success)
            throw new InvalidOperationException($"vkCreateCommandPool (upload) failed: {result}");
    }

    public unsafe CommandBuffer Begin()
    {
        var allocInfo = new CommandBufferAllocateInfo
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = _commandPool,
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

    public unsafe void EndAndSubmit(CommandBuffer cmd)
    {
        _vk.EndCommandBuffer(cmd);

        var submitInfo = new SubmitInfo
        {
            SType = StructureType.SubmitInfo,
            CommandBufferCount = 1,
            PCommandBuffers = &cmd
        };

        _vk.QueueSubmit(_queue, 1, in submitInfo, default);
        _vk.QueueWaitIdle(_queue); // blocking - fine for load-time, not per-frame

        _vk.FreeCommandBuffers(_device, _commandPool, 1, in cmd);
    }
}