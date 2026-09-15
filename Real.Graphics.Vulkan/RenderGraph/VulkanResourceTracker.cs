using Real.Graphics.Rhi.Handles;
using Silk.NET.Vulkan;

namespace Real.Graphics.Vulkan.RenderGraph;

internal enum ResourceAccess
{
    None,
    ColorAttachmentWrite,
    DepthAttachmentWrite,
    ShaderRead,
    TransferSrc,
    TransferDst,
    PresentSrc
}

/// <summary>
/// Tracks the last known (layout, access, pipeline stage) for every texture
/// touched within one IRenderGraph.Execute() call. VulkanBarrierBuilder reads
/// this to decide whether a transition is needed between two passes, and
/// updates it after each pass so the next pass sees the correct "previous state".
/// </summary>
internal sealed class VulkanResourceTracker
{
    private readonly Dictionary<TextureHandle, (ImageLayout Layout, ResourceAccess Access, PipelineStageFlags Stage)> _state = new();

    public (ImageLayout Layout, ResourceAccess Access, PipelineStageFlags Stage) GetState(TextureHandle texture) =>
        _state.TryGetValue(texture, out var s) ? s : (ImageLayout.Undefined, ResourceAccess.None, PipelineStageFlags.TopOfPipeBit);

    public void SetState(TextureHandle texture, ImageLayout layout, ResourceAccess access, PipelineStageFlags stage) =>
        _state[texture] = (layout, access, stage);

    public void Reset() => _state.Clear();
}
