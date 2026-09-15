using Silk.NET.Vulkan;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace Real.Graphics.Vulkan.Sync;

/// <summary>
/// Per-frame-in-flight synchronization primitives: one set of these per
/// swapchain image slot (typically 2-3), cycled round-robin each frame.
/// image-available / render-finished semaphores gate acquire/present,
/// in-flight fence gates CPU from reusing this frame's command buffer
/// before the GPU has finished with it.
/// </summary>
internal sealed class FrameSyncContext
{
    public const int FramesInFlight = 2;

    public Semaphore ImageAvailable { get; init; }
    public Semaphore RenderFinished { get; init; }
    public Fence InFlightFence { get; init; }
    public CommandBuffer CommandBuffer { get; init; }
}
