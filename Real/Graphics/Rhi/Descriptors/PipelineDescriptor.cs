using Real.Graphics.Rhi.Enums;
using Real.Graphics.Rhi.Handles;

namespace Real.Graphics.Rhi.Descriptors;

public record struct PipelineDescriptor(
    ShaderHandle VertexShader,
    ShaderHandle FragmentShader,
    VertexLayout VertexLayout,
    PrimitiveTopology Topology,
    BlendState Blend,
    DepthStencilState DepthStencil,
    RasterState Raster,
    TextureFormat[] ColorAttachmentFormats,
    TextureFormat? DepthAttachmentFormat,
    string? DebugName = null);
