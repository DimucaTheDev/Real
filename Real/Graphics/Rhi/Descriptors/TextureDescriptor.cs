using Real.Graphics.Rhi.Enums;

namespace Real.Graphics.Rhi.Descriptors;

public record struct TextureDescriptor(
    uint Width,
    uint Height,
    TextureFormat Format,
    TextureUsage Usage,
    uint MipLevels = 1,
    uint SampleCount = 1,
    string? DebugName = null);
