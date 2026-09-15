using Real.Graphics.Rhi.Enums;

namespace Real.Graphics.Rhi.Descriptors;

public record struct BufferDescriptor(
    ulong Size,
    BufferUsage Usage,
    bool CpuVisible = false,
    string? DebugName = null);
