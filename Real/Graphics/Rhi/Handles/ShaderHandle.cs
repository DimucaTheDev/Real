namespace Real.Graphics.Rhi.Handles;

public readonly record struct ShaderHandle(uint Id, uint Generation)
{
    public static readonly ShaderHandle Invalid = new(0, 0);
    public bool IsValid => Id != 0;
}
