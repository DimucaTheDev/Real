using Real.Graphics.Rhi;
using Real.Windowing;

namespace Real.Graphics.OpenGL;

public sealed class OpenGlBackendFactory : IGraphicsBackendFactory
{
    public GraphicsApi Api => GraphicsApi.OpenGl;

    public IGraphicsDevice CreateDevice(IWindow window, bool enableValidation = false) =>
        new OpenGlDevice(window, enableValidation);
}
