using Real.Graphics.Rhi.Enums;

namespace Real.Graphics.Rhi.Descriptors;

public record struct DepthStencilState(
    bool DepthTestEnabled,
    bool DepthWriteEnabled,
    CompareOp DepthCompare = CompareOp.LessOrEqual)
{
    public static readonly DepthStencilState Default = new(true, true);
    public static readonly DepthStencilState Disabled = new(false, false);
}
