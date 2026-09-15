using Real.Graphics.Rhi.Enums;

namespace Real.Graphics.Rhi.Descriptors;

public record struct BlendState(
    bool Enabled,
    BlendFactor SrcColor = BlendFactor.SrcAlpha,
    BlendFactor DstColor = BlendFactor.OneMinusSrcAlpha,
    BlendFactor SrcAlpha = BlendFactor.One,
    BlendFactor DstAlpha = BlendFactor.Zero)
{
    public static readonly BlendState Opaque = new(false);
    public static readonly BlendState AlphaBlend = new(true);
}
