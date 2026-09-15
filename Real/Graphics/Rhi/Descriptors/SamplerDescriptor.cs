namespace Real.Graphics.Rhi.Descriptors;

public enum FilterMode { Nearest, Linear }
public enum AddressMode { Repeat, ClampToEdge, MirroredRepeat }

public record struct SamplerDescriptor(
    FilterMode MinFilter = FilterMode.Linear,
    FilterMode MagFilter = FilterMode.Linear,
    AddressMode AddressU = AddressMode.Repeat,
    AddressMode AddressV = AddressMode.Repeat,
    float MaxAnisotropy = 1.0f);
