namespace Real.Graphics.Rhi.Enums;

[Flags]
public enum ShaderStage
{
    None = 0,
    Vertex = 1 << 0,
    Fragment = 1 << 1,
    Compute = 1 << 2
}
