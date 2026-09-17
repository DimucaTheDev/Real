using System;
using System.Collections.Generic;
using System.Linq;
using Real.Graphics.OpenGL.Resources;
using Real.Graphics.OpenGL.Translation;
using Real.Graphics.Rhi.Handles;
using Silk.NET.OpenGL;

namespace Real.Graphics.OpenGL.RenderGraph;

internal sealed class OpenGlFramebufferCache : IDisposable
{
    private readonly GL _gl;
    private readonly OpenGlTexturePool _textures;
    private readonly Dictionary<string, uint> _cache = new();

    public OpenGlFramebufferCache(GL gl, OpenGlTexturePool textures)
    {
        _gl = gl;
        _textures = textures;
    }

    public unsafe uint GetOrCreate(IReadOnlyList<TextureHandle> colorWrites, TextureHandle? depthWrite)
    {
        // Build a unique key from attachment IDs
        string key = string.Join(",", colorWrites.Select(c => c.Id)) + "|" + (depthWrite?.Id ?? 0);
        if (_cache.TryGetValue(key, out uint cachedFbo))
        {
            return cachedFbo;
        }

        _gl.GenFramebuffers(1, out uint fbo);
        if (fbo == 0)
            throw new InvalidOperationException("Failed to create OpenGL framebuffer.");

        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);

        var drawBuffers = new GLEnum[colorWrites.Count];
        for (int i = 0; i < colorWrites.Count; i++)
        {
            var tex = _textures.Get(colorWrites[i]);
            _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0 + i, TextureTarget.Texture2D, tex.Handle, 0);
            drawBuffers[i] = GLEnum.ColorAttachment0 + i;
        }

        if (depthWrite is { } depthHandle)
        {
            var depthTex = _textures.Get(depthHandle);
            bool hasStencil = depthTex.Descriptor.Format == Real.Graphics.Rhi.Enums.TextureFormat.D24UnormS8Uint;
            var attachment = hasStencil
                ? FramebufferAttachment.DepthStencilAttachment
                : FramebufferAttachment.DepthAttachment;

            _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, attachment, TextureTarget.Texture2D, depthTex.Handle, 0);
        }

        if (drawBuffers.Length > 0)
        {
            fixed (GLEnum* pDrawBuffers = drawBuffers)
            {
                _gl.DrawBuffers((uint)drawBuffers.Length, pDrawBuffers);
            }
        }
        else
        {
            _gl.DrawBuffer(DrawBufferMode.None);
            _gl.ReadBuffer(ReadBufferMode.None);
        }

        var status = _gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

        if (status != GLEnum.FramebufferComplete)
        {
            _gl.DeleteFramebuffers(1, in fbo);
            throw new InvalidOperationException($"OpenGL Framebuffer is incomplete: {status}");
        }

        _cache[key] = fbo;
        return fbo;
    }

    public unsafe void Dispose()
    {
        foreach (var fbo in _cache.Values)
        {
            uint id = fbo;
            _gl.DeleteFramebuffers(1, in id);
        }
        _cache.Clear();
    }
}
