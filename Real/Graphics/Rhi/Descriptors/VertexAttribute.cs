using Real.Graphics.Rhi.Enums;

namespace Real.Graphics.Rhi.Descriptors;

public record struct VertexAttribute(
    uint Location,
    TextureFormat Format,
    uint Offset);

public record struct VertexLayout(
    uint Stride,
    VertexAttribute[] Attributes);
