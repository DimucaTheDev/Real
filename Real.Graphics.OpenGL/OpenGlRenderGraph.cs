using System;
using System.Collections.Generic;
using Real.Graphics.OpenGL.RenderGraph;
using Real.Graphics.OpenGL.Resources;
using Real.Graphics.Rhi;
using Real.Graphics.Rhi.Handles;
using Silk.NET.OpenGL;

namespace Real.Graphics.OpenGL;

internal sealed class OpenGlRenderGraph : IRenderGraph, IDisposable
{
    private readonly GL _gl;
    private readonly OpenGlTexturePool _textures;
    private readonly OpenGlPipelinePool _pipelines;
    private readonly OpenGlBufferPool _buffers;
    private readonly OpenGlSamplerPool _samplers;
    private readonly OpenGlFramebufferCache _framebufferCache;
    private readonly Func<OpenGlSwapchain?> _swapchainProvider;

    private readonly List<RenderPassBuilder> _passes = new();

    public OpenGlRenderGraph(
        GL gl,
        OpenGlTexturePool textures,
        OpenGlPipelinePool pipelines,
        OpenGlBufferPool buffers,
        OpenGlSamplerPool samplers,
        OpenGlFramebufferCache framebufferCache,
        Func<OpenGlSwapchain?> swapchainProvider)
    {
        _gl = gl;
        _textures = textures;
        _pipelines = pipelines;
        _buffers = buffers;
        _samplers = samplers;
        _framebufferCache = framebufferCache;
        _swapchainProvider = swapchainProvider;
    }

    public RenderPassBuilder AddPass(string name)
    {
        var pass = new RenderPassBuilder(name);
        _passes.Add(pass);
        return pass;
    }

    public void Execute()
    {
        if (_passes.Count == 0) return;

        var swapchain = _swapchainProvider();
        var cmd = new OpenGlCommandList(_gl, _pipelines, _buffers, _textures, _samplers);

        foreach (var pass in _passes)
        {
            uint fbo = 0;
            uint width = swapchain?.Width ?? 1280;
            uint height = swapchain?.Height ?? 720;

            if (pass.ColorWrites.Count > 0 || pass.DepthWrite != null)
            {
                fbo = _framebufferCache.GetOrCreate(pass.ColorWrites, pass.DepthWrite);
                if (pass.ColorWrites.Count > 0)
                {
                    var tex = _textures.Get(pass.ColorWrites[0]);
                    width = tex.Descriptor.Width;
                    height = tex.Descriptor.Height;
                }
                else if (pass.DepthWrite != null)
                {
                    var tex = _textures.Get(pass.DepthWrite.Value);
                    width = tex.Descriptor.Width;
                    height = tex.Descriptor.Height;
                }
            }

            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
            _gl.Viewport(0, 0, Math.Max(1u, width), Math.Max(1u, height));

            if (fbo != 0)
            {
                _gl.ClearColor(0.1f, 0.1f, 0.1f, 1.0f);
                ClearBufferMask clearMask = ClearBufferMask.None;
                if (pass.ColorWrites.Count > 0) clearMask |= ClearBufferMask.ColorBufferBit;
                if (pass.DepthWrite != null)
                {
                    clearMask |= ClearBufferMask.DepthBufferBit;
                    _gl.ClearDepth(1.0);
                }

                if (clearMask != ClearBufferMask.None)
                {
                    _gl.Clear(clearMask);
                }
            }

            pass.Execution?.Invoke(cmd);

            // If this pass rendered to the swapchain backbuffer, blit to the default framebuffer (0)
            if (swapchain != null && pass.ColorWrites.Contains(swapchain.BackbufferHandle))
            {
                _gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, fbo);
                _gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0);
                _gl.BlitFramebuffer(
                    0, 0, (int)width, (int)height,
                    0, 0, (int)swapchain.Width, (int)swapchain.Height,
                    ClearBufferMask.ColorBufferBit,
                    BlitFramebufferFilter.Nearest);
                _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            }
        }

        _passes.Clear();
    }

    public void Dispose()
    {
        _passes.Clear();
    }
}
