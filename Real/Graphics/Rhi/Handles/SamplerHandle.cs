namespace Real.Graphics.Rhi.Handles;

public readonly record struct SamplerHandle(uint Id, uint Generation)
{
    public static readonly SamplerHandle Invalid = new(0, 0);
    public bool IsValid => Id != 0;
}
