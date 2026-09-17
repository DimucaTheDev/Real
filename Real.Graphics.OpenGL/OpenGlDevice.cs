using System;
using System.Runtime.InteropServices;
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
    private DebugProc? _debugCallback;

    public unsafe OpenGlDevice(IWindow window, bool enableValidation = false)
    {
        window.Show();

        _glfw = Glfw.GetApi();

        if (window.Handle != 0)
        {
            try
            {
                _glfw.MakeContextCurrent((WindowHandle*)window.Handle);
                _glfw.SwapInterval(1);
            }
            catch
            {
                // Ignored
            }
        }

        if (_glfw.GetCurrentContext() == null)
        {
            _glfw.WindowHint(WindowHintBool.Visible, false);
            _glfw.WindowHint(WindowHintClientApi.ClientApi, ClientApi.OpenGL);
            _glfw.WindowHint(WindowHintInt.ContextVersionMajor, 4);
            _glfw.WindowHint(WindowHintInt.ContextVersionMinor, 6);
            _glfw.WindowHint(WindowHintOpenGlProfile.OpenGlProfile, OpenGlProfile.Core);
            _glfw.WindowHint(WindowHintBool.OpenGLForwardCompat, true);
            var ctxWindow = _glfw.CreateWindow(1, 1, "Real_OpenGl_Context", null, null);
            if (ctxWindow != null)
            {
                _glfw.MakeContextCurrent(ctxWindow);
            }
        }

        _gl = GL.GetApi(new LamdaNativeContext(proc =>
        {
            var addr = (nint)_glfw.GetProcAddress(proc);
            if (addr == 0)
            {
                try
                {
                    if (OperatingSystem.IsWindows() && NativeLibrary.TryLoad("opengl32.dll", out var libWin))
                    {
                        NativeLibrary.TryGetExport(libWin, proc, out addr);
                    }
                    else if (OperatingSystem.IsLinux())
                    {
                        if (NativeLibrary.TryLoad("libGL.so.1", out var libLin) || NativeLibrary.TryLoad("libGL.so", out libLin))
                        {
                            NativeLibrary.TryGetExport(libLin, proc, out addr);
                        }
                    }
                }
                catch
                {
                    // Ignore fallback failure
                }
            }
            return addr;
        }));

        _gl.Enable(EnableCap.FramebufferSrgb);
        try
        {
            _gl.ClipControl(ClipControlOrigin.UpperLeft, ClipControlDepth.ZeroToOne);
        }
        catch
        {
            // ClipControl requires OpenGL 4.5+ or ARB_clip_control
        }
        
        if (enableValidation)
        {
            try
            {
                _gl.Enable(EnableCap.DebugOutput);
                _gl.Enable(EnableCap.DebugOutputSynchronous);
                _debugCallback = (source, type, id, severity, length, message, userParam) =>
                {
                    string msg = Marshal.PtrToStringAnsi(message, length);
                    Console.WriteLine($"[OpenGL Debug ({severity})] {msg}");
                };
                _gl.DebugMessageCallback(_debugCallback, null);
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
