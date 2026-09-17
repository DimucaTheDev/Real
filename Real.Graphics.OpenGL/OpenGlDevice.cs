using System;
using Real.Graphics.OpenGL.RenderGraph;
using Real.Graphics.OpenGL.Resources;
using Real.Graphics.Rhi;
using Real.Graphics.Rhi.Descriptors;
using Real.Graphics.Rhi.Enums;
using Real.Graphics.Rhi.Handles;
using Real.Windowing;
using Silk.NET.Core.Contexts;
using Silk.NET.GLFW;
using Silk.NET.OpenGL;

namespace Real.Graphics.OpenGL;

public sealed class OpenGlDevice : IGraphicsDevice
{
    public string BackendName => "OpenGL 4.6 Core";

    private readonly GL _gl;
    private readonly Glfw? _glfw;
    private readonly OpenGlBufferPool _bufferPool;
    private readonly OpenGlTexturePool _texturePool;
    private readonly OpenGlSamplerPool _samplerPool;
    private readonly OpenGlShaderCompiler _shaderCompiler;
    private readonly OpenGlShaderPool _shaderPool;
    private readonly OpenGlPipelinePool _pipelinePool;
    private readonly OpenGlFramebufferCache _framebufferCache;

    private OpenGlSwapchain? _currentSwapchain;

    public unsafe OpenGlDevice(IWindow window, bool enableValidation = false)
    {
        if (window.Handle != 0)
        {
            window.Show();

            try
            {
                _glfw = Glfw.GetApi();
                _glfw.MakeContextCurrent((WindowHandle*)window.Handle);
                _glfw.SwapInterval(1);
                var glfwContext = new GlfwContext(_glfw, (WindowHandle*)window.Handle);
                _gl = GL.GetApi(glfwContext);
            }
            catch
            {
                _glfw ??= Glfw.GetApi();
                _gl = GL.GetApi(new LamdaNativeContext(proc => (nint)_glfw.GetProcAddress(proc)));
            }
        }
        else
        {
            _glfw = Glfw.GetApi();
            _gl = GL.GetApi(new LamdaNativeContext(proc => (nint)_glfw.GetProcAddress(proc)));
        }

        if (enableValidation)
        {
            try
            {
                _gl.Enable(EnableCap.DebugOutput);
                _gl.Enable(EnableCap.DebugOutputSynchronous);
            }
            catch
            {
                // Ignored if debug output not supported on this platform
            }
        }

        _bufferPool = new OpenGlBufferPool(_gl);
        _texturePool = new OpenGlTexturePool(_gl);
        _samplerPool = new OpenGlSamplerPool(_gl);
        _shaderCompiler = new OpenGlShaderCompiler(_gl);
        _shaderPool = new OpenGlShaderPool(_gl);
        _pipelinePool = new OpenGlPipelinePool(_gl, _shaderPool);
        _framebufferCache = new OpenGlFramebufferCache(_gl, _texturePool);
    }

    public BufferHandle CreateBuffer(in BufferDescriptor descriptor, ReadOnlySpan<byte> initialData = default) =>
        _bufferPool.Create(descriptor, initialData);

    public void DestroyBuffer(BufferHandle handle) => _bufferPool.Destroy(handle);

    public TextureHandle CreateTexture(in TextureDescriptor descriptor, ReadOnlySpan<byte> initialData = default) =>
        _texturePool.Create(descriptor, initialData);

    public void DestroyTexture(TextureHandle handle) => _texturePool.Destroy(handle);

    public SamplerHandle CreateSampler(in SamplerDescriptor descriptor) =>
        _samplerPool.Create(descriptor);

    public void DestroySampler(SamplerHandle handle) => _samplerPool.Destroy(handle);

    public ShaderHandle CreateShader(ShaderStage stage, ReadOnlySpan<byte> bytecode, string entryPoint = "main")
    {
        uint shader = _shaderCompiler.CreateShader(stage, bytecode, entryPoint);
        return _shaderPool.Store(shader, stage, entryPoint);
    }

    public void DestroyShader(ShaderHandle handle) => _shaderPool.Destroy(handle);

    public PipelineHandle CreatePipeline(in PipelineDescriptor descriptor) =>
        _pipelinePool.Create(descriptor);

    public void DestroyPipeline(PipelineHandle handle) => _pipelinePool.Destroy(handle);

    public ISwapchain CreateSwapchain(IWindow window)
    {
        _currentSwapchain?.Dispose();
        _currentSwapchain = new OpenGlSwapchain(_gl, window, _texturePool);
        return _currentSwapchain;
    }

    public IRenderGraph CreateRenderGraph() =>
        new OpenGlRenderGraph(
            _gl,
            _texturePool,
            _pipelinePool,
            _bufferPool,
            _samplerPool,
            _framebufferCache,
            () => _currentSwapchain);

    public void WaitIdle()
    {
        _gl.Finish();
    }

    public void Dispose()
    {
        _currentSwapchain?.Dispose();
        _framebufferCache.Dispose();
        _pipelinePool.Dispose();
        _shaderPool.Dispose();
        _samplerPool.Dispose();
        _texturePool.Dispose();
        _bufferPool.Dispose();
        _gl.Dispose();
    }
}
