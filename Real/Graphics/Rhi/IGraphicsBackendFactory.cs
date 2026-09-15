using Real.Windowing;

namespace Real.Graphics.Rhi;

public interface IGraphicsBackendFactory
{
    GraphicsApi Api { get; }

    IGraphicsDevice CreateDevice(IWindow window, bool enableValidation = false);
}
