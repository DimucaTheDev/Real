namespace Real.Graphics.Rhi.Handles;

public readonly record struct TextureHandle(uint Id, uint Generation)
{
    public static readonly TextureHandle Invalid = new(0, 0);
    public bool IsValid => Id != 0;
}
