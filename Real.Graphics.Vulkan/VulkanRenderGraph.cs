using System;
using System.Collections.Generic;
using Real.Graphics.Rhi;
using Real.Graphics.Rhi.Enums;
using Real.Graphics.Vulkan.Core;
using Real.Graphics.Vulkan.RenderGraph;
using Real.Graphics.Vulkan.Resources;
using Silk.NET.Vulkan;

namespace Real.Graphics.Vulkan;

/// <summary>
/// Vulkan implementation of IRenderGraph. Collects RenderPassBuilder
/// declarations, then on Execute():
///   1. records one primary command buffer for this frame,
///   2. for each pass, inserts barriers (via VulkanBarrierBuilder) for every
///      resource in Reads()/Writes() based on its previous tracked state,
///   3. begins a VkRenderPass/VkFramebuffer resolved from VulkanRenderPassCache,
///   4. invokes the pass's Execute callback with a VulkanCommandList,
///   5. ends the render pass, submits, and (for the swapchain image) hands off
///      to VulkanSwapchain.Present().
/// </summary>
internal sealed class VulkanRenderGraph : IRenderGraph, IDisposable
{
    private readonly Vk _vk;
    private readonly VulkanLogicalDevice _device;
    private readonly VulkanTexturePool _textures;
    private readonly VulkanPipelinePool _pipelines;
    private readonly VulkanBufferPool _buffers;
    private readonly VulkanSamplerPool _samplers;
    private readonly VulkanDescriptorAllocator _descriptorAllocator;
    private readonly VulkanRenderPassCache _renderPassCache;
    private readonly Func<VulkanSwapchain?> _swapchainProvider;
    private readonly Func<CommandBuffer> _fallbackCommandBufferProvider;
    private readonly VulkanResourceTracker _tracker;
    private readonly VulkanBarrierBuilder _barrierBuilder;

    private readonly List<RenderPassBuilder> _passes = new();

    public VulkanRenderGraph(
        Vk vk,
        VulkanLogicalDevice device,
        VulkanTexturePool textures,
        VulkanPipelinePool pipelines,
        VulkanBufferPool buffers,
        VulkanSamplerPool samplers,
        VulkanDescriptorAllocator descriptorAllocator,
        VulkanRenderPassCache renderPassCache,
        Func<VulkanSwapchain?> swapchainProvider,
        Func<CommandBuffer> fallbackCommandBufferProvider)
    {
        _vk = vk;
        _device = device;
        _textures = textures;
        _pipelines = pipelines;
        _buffers = buffers;
        _samplers = samplers;
        _descriptorAllocator = descriptorAllocator;
        _renderPassCache = renderPassCache;
        _swapchainProvider = swapchainProvider;
        _fallbackCommandBufferProvider = fallbackCommandBufferProvider;
        _tracker = new VulkanResourceTracker();
        _barrierBuilder = new VulkanBarrierBuilder(vk, _tracker);
    }

    public RenderPassBuilder AddPass(string name)
    {
        var builder = new RenderPassBuilder(name);
        _passes.Add(builder);
        return builder;
    }

    public unsafe void Execute()
    {
        if (_passes.Count == 0) return;

        var swapchain = _swapchainProvider();
        CommandBuffer cmd;

        if (swapchain != null)
        {
            cmd = swapchain.CurrentSync.CommandBuffer;
            _vk.ResetCommandBuffer(cmd, 0);
            _descriptorAllocator.SetCurrentFrame(swapchain.CurrentFrameIndex);
        }
        else
        {
            cmd = _fallbackCommandBufferProvider();
            _vk.ResetCommandBuffer(cmd, 0);
            _descriptorAllocator.SetCurrentFrame(VulkanDescriptorAllocator.HeadlessFrameIndex);
        }

        var beginInfo = new CommandBufferBeginInfo
        {
            SType = StructureType.CommandBufferBeginInfo,
            Flags = CommandBufferUsageFlags.OneTimeSubmitBit
        };
        _vk.BeginCommandBuffer(cmd, in beginInfo);

        foreach (var pass in _passes)
        {
            foreach (var read in pass.ReadTextures)
            {
                if (!_textures.IsValid(read))
                    throw new InvalidOperationException(
                        $"Read texture handle {read} in pass '{pass.Name}' is not a valid texture.");
            }

            foreach (var write in pass.ColorWrites)
            {
                if (!_textures.IsValid(write))
                    throw new InvalidOperationException(
                        $"Color write attachment handle {write} in pass '{pass.Name}' is not a valid texture.");
            }

            if (pass.DepthWrite is { } depthWrite && !_textures.IsValid(depthWrite))
            {
                throw new InvalidOperationException(
                    $"Depth write attachment handle {depthWrite} in pass '{pass.Name}' is not a valid texture.");
            }

            InsertBarriersForPass(cmd, pass);

            var colorFormats = ResolveColorFormats(pass);
            var depthFormat = ResolveDepthFormat(pass);
            var renderPass = _renderPassCache.GetOrCreate(colorFormats, depthFormat);

            var attachments = new ImageView[pass.ColorWrites.Count + (pass.DepthWrite is not null ? 1 : 0)];
            uint width = 0, height = 0;
            for (int i = 0; i < pass.ColorWrites.Count; i++)
            {
                var tex = _textures.Get(pass.ColorWrites[i]);
                attachments[i] = tex.View;
                if (width == 0)
                {
                    width = tex.Descriptor.Width;
                    height = tex.Descriptor.Height;
                }
            }

            if (pass.DepthWrite is { } depth)
            {
                var tex = _textures.Get(depth);
                attachments[^1] = tex.View;
                if (width == 0)
                {
                    width = tex.Descriptor.Width;
                    height = tex.Descriptor.Height;
                }
            }

            var framebuffer = _renderPassCache.GetOrCreateFramebuffer(renderPass, attachments, width, height);

            var clearValues = new ClearValue[attachments.Length];
            for (int i = 0; i < attachments.Length; i++)
            {
                clearValues[i] = new ClearValue
                {
                    Color = new ClearColorValue(0, 0, 0, 1f)
                };
            }

            fixed (ClearValue* pClearValues = clearValues)
            {
                var renderPassBeginInfo = new RenderPassBeginInfo
                {
                    SType = StructureType.RenderPassBeginInfo,
                    RenderPass = renderPass,
                    Framebuffer = framebuffer,
                    RenderArea = new Rect2D(new Offset2D(0, 0), new Extent2D(width, height)),
                    ClearValueCount = (uint)attachments.Length,
                    PClearValues = pClearValues
                };

                _vk.CmdBeginRenderPass(cmd, in renderPassBeginInfo, SubpassContents.Inline);

                var commandList = new VulkanCommandList(
                    _vk,
                    _device.Handle,
                    cmd,
                    _pipelines,
                    _buffers,
                    _textures,
                    _samplers,
                    _descriptorAllocator);

                pass.Execution?.Invoke(commandList);

                _vk.CmdEndRenderPass(cmd);
            }

            // Transition backbuffer to PresentSrcKhr if written
            foreach (var write in pass.ColorWrites)
            {
                if (swapchain != null && swapchain.IsBackbuffer(write))
                {
                    _barrierBuilder.TransitionForPresent(cmd, write, _textures.Get(write).Image);
                }
            }
        }

        _vk.EndCommandBuffer(cmd);

        if (swapchain != null)
        {
            var sync = swapchain.CurrentSync;
            var waitSemaphore = sync.ImageAvailable;
            var signalSemaphore = swapchain.CurrentRenderFinishedSemaphore;
            var waitStage = PipelineStageFlags.ColorAttachmentOutputBit;

            var submitInfo = new SubmitInfo
            {
                SType = StructureType.SubmitInfo,
                WaitSemaphoreCount = waitSemaphore.Handle != 0 ? 1u : 0u,
                PWaitSemaphores = waitSemaphore.Handle != 0 ? &waitSemaphore : null,
                PWaitDstStageMask = waitSemaphore.Handle != 0 ? &waitStage : null,
                CommandBufferCount = 1,
                PCommandBuffers = &cmd,
                SignalSemaphoreCount = signalSemaphore.Handle != 0 ? 1u : 0u,
                PSignalSemaphores = signalSemaphore.Handle != 0 ? &signalSemaphore : null
            };

            var fence = sync.InFlightFence;
            _vk.ResetFences(_device.Handle, 1, in fence);
            var submitRes = _vk.QueueSubmit(_device.Queues.GraphicsQueue, 1, in submitInfo, fence);
            if (submitRes != Result.Success)
                throw new InvalidOperationException($"vkQueueSubmit failed: {submitRes}");
        }
        else
        {
            var submitInfo = new SubmitInfo
            {
                SType = StructureType.SubmitInfo,
                CommandBufferCount = 1,
                PCommandBuffers = &cmd
            };

            var submitRes = _vk.QueueSubmit(_device.Queues.GraphicsQueue, 1, in submitInfo, default);
            if (submitRes != Result.Success)
                throw new InvalidOperationException($"vkQueueSubmit failed: {submitRes}");
            _vk.QueueWaitIdle(_device.Queues.GraphicsQueue);
            _descriptorAllocator.ResetFrame(VulkanDescriptorAllocator.HeadlessFrameIndex);
        }

        _passes.Clear();
        _tracker.Reset();
    }

    private void InsertBarriersForPass(CommandBuffer cmd, RenderPassBuilder pass)
    {
        foreach (var read in pass.ReadTextures)
            _barrierBuilder.TransitionForRead(cmd, read, _textures.Get(read).Image);

        foreach (var write in pass.ColorWrites)
            _barrierBuilder.TransitionForColorWrite(cmd, write, _textures.Get(write).Image);

        if (pass.DepthWrite is { } depth)
            _barrierBuilder.TransitionForDepthWrite(cmd, depth, _textures.Get(depth).Image);
    }

    private TextureFormat[] ResolveColorFormats(RenderPassBuilder pass)
    {
        var formats = new TextureFormat[pass.ColorWrites.Count];
        for (int i = 0; i < pass.ColorWrites.Count; i++)
            formats[i] = _textures.Get(pass.ColorWrites[i]).Descriptor.Format;
        return formats;
    }

    private TextureFormat? ResolveDepthFormat(RenderPassBuilder pass) =>
        pass.DepthWrite is { } depth ? _textures.Get(depth).Descriptor.Format : null;

    public void Dispose()
    {
        _passes.Clear();
        _tracker.Reset();
    }
}