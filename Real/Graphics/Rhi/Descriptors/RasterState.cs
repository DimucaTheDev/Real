using Real.Graphics.Rhi.Enums;

namespace Real.Graphics.Rhi.Descriptors;

public record struct RasterState(
    CullMode CullMode = CullMode.Back,
    bool Wireframe = false,
    bool FrontFaceCounterClockwise = true);
