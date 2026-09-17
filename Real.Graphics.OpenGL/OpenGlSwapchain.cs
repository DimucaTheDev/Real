using System;
using Real.Graphics.OpenGL.Resources;
using Real.Graphics.Rhi;
using Real.Graphics.Rhi.Descriptors;
using Real.Graphics.Rhi.Enums;
using Real.Graphics.Rhi.Handles;
using Real.Windowing;
using Silk.NET.OpenGL;

namespace Real.Graphics.OpenGL;

internal sealed class OpenGlSwapchain : ISwapchain
{
    private readonly GL _gl;
    private readonly IWindow _window;
    private readonly OpenGlTexturePool _textures;

    private TextureHandle _backbufferTexture;

    public TextureFormat Format { get; } = TextureFormat.Rgba8Unorm;
    public uint Width { get; private set; }
    public uint Height { get; private set; }

    internal TextureHandle BackbufferHandle => _backbufferTexture;

    public OpenGlSwapchain(GL gl, IWindow window, OpenGlTexturePool textures)
    {
        _gl = gl;
        _window = window;
        _textures = textures;

        Width = Math.Max(1u, (uint)window.Size.Width);
        Height = Math.Max(1u, (uint)window.Size.Height);

        CreateBackbuffer();
    }

    private void CreateBackbuffer()
    {
        var descriptor = new TextureDescriptor(
            Width: Width,
            Height: Height,
            Format: Format,
            Usage: TextureUsage.RenderTarget | TextureUsage.Sampled,
            MipLevels: 1,
            SampleCount: 1,
            DebugName: "OpenGL_Swapchain_Backbuffer"
        );

        _backbufferTexture = _textures.Create(descriptor);
    }

    public TextureHandle AcquireNextImage()
    {
        return _backbufferTexture;
    }

    public void Present()
    {
        _window.SwapBuffers();
    }

    public void Resize(uint width, uint height)
    {
        if (width == Width && height == Height && width > 0 && height > 0)
            return;

        Width = Math.Max(1u, width);
        Height = Math.Max(1u, height);

        if (_textures.IsValid(_backbufferTexture))
        {
            _textures.Destroy(_backbufferTexture);
        }

        CreateBackbuffer();
    }

    public void Dispose()
    {
        if (_textures.IsValid(_backbufferTexture))
        {
            _textures.Destroy(_backbufferTexture);
            _backbufferTexture = default;
        }
    }
}
