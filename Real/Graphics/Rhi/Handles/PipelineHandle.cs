namespace Real.Graphics.Rhi.Handles;

public readonly record struct PipelineHandle(uint Id, uint Generation)
{
    public static readonly PipelineHandle Invalid = new(0, 0);
    public bool IsValid => Id != 0;
}
