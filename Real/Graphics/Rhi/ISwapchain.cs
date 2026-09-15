using Real.Graphics.Rhi.Enums;
using Real.Graphics.Rhi.Handles;

namespace Real.Graphics.Rhi;

public interface ISwapchain : IDisposable
{
    TextureFormat Format { get; }
    uint Width { get; }
    uint Height { get; }

    /// <summary>Acquires the next backbuffer texture to render into this frame.</summary>
    TextureHandle AcquireNextImage();

    /// <summary>Presents the previously acquired image and advances frames-in-flight.</summary>
    void Present();

    void Resize(uint width, uint height);
}
