namespace Real.Graphics.Rhi.Handles;

public readonly record struct BufferHandle(uint Id, uint Generation)
{
    public static readonly BufferHandle Invalid = new(0, 0);
    public bool IsValid => Id != 0;
}
