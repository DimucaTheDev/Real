using Real.Graphics.Rhi;
using Real.Graphics.Rhi.Enums;
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
internal sealed class VulkanRenderGraph : IRenderGraph
{
    private readonly Vk _vk;
    private readonly Device _device;
    private readonly VulkanTexturePool _textures;
    private readonly VulkanPipelinePool _pipelines;
    private readonly VulkanBufferPool _buffers;
    private readonly VulkanDescriptorAllocator _descriptorAllocator;
    private readonly VulkanRenderPassCache _renderPassCache;
    private readonly VulkanResourceTracker _tracker;
    private readonly VulkanBarrierBuilder _barrierBuilder;

    private readonly List<RenderPassBuilder> _passes = new();

    public VulkanRenderGraph(
        Vk vk,
        Device device,
        VulkanTexturePool textures,
        VulkanPipelinePool pipelines,
        VulkanBufferPool buffers,
        VulkanDescriptorAllocator descriptorAllocator,
        VulkanRenderPassCache renderPassCache)
    {
        _vk = vk;
        _device = device;
        _textures = textures;
        _pipelines = pipelines;
        _buffers = buffers;
        _descriptorAllocator = descriptorAllocator;
        _renderPassCache = renderPassCache;
        _tracker = new VulkanResourceTracker();
        _barrierBuilder = new VulkanBarrierBuilder(vk, _tracker);
    }

    public RenderPassBuilder AddPass(string name)
    {
        var builder = new RenderPassBuilder(name);
        _passes.Add(builder);
        return builder;
    }

    public void Execute()
    {
        // TODO: acquire/begin a primary CommandBuffer for this frame
        CommandBuffer cmd = default;

        foreach (var pass in _passes)
        {
            InsertBarriersForPass(cmd, pass);

            var renderPass = _renderPassCache.GetOrCreate(
                ResolveColorFormats(pass), ResolveDepthFormat(pass));

            // TODO: resolve/attach framebuffer, vk.CmdBeginRenderPass

            var commandList = new VulkanCommandList(_vk, cmd, _pipelines, _buffers, _textures, _descriptorAllocator);
            pass.Execution?.Invoke(commandList);

            // TODO: vk.CmdEndRenderPass
        }

        // TODO: vk.EndCommandBuffer, submit to graphics queue with the
        // frame's semaphores/fence (see Sync/FrameSyncContext)

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
}
