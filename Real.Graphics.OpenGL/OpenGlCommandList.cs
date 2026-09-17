using System;
using Real.Graphics.OpenGL.Resources;
using Real.Graphics.OpenGL.Translation;
using Real.Graphics.Rhi;
using Real.Graphics.Rhi.Handles;
using Silk.NET.OpenGL;

namespace Real.Graphics.OpenGL;

internal sealed class OpenGlCommandList : ICommandList
{
    private readonly GL _gl;
    private readonly OpenGlPipelinePool _pipelines;
    private readonly OpenGlBufferPool _buffers;
    private readonly OpenGlTexturePool _textures;
    private readonly OpenGlSamplerPool _samplers;

    private OpenGlPipelineEntry _currentPipeline;
    private bool _hasPipeline;
    private ulong _indexBufferOffset;

    public OpenGlCommandList(
        GL gl,
        OpenGlPipelinePool pipelines,
        OpenGlBufferPool buffers,
        OpenGlTexturePool textures,
        OpenGlSamplerPool samplers)
    {
        _gl = gl;
        _pipelines = pipelines;
        _buffers = buffers;
        _textures = textures;
        _samplers = samplers;
    }

    public void BindPipeline(PipelineHandle pipeline)
    {
        _currentPipeline = _pipelines.Get(pipeline);
        _hasPipeline = true;

        _gl.UseProgram(_currentPipeline.Program);
        _gl.BindVertexArray(_currentPipeline.Vao);

        GlRasterStateTranslator.Apply(_gl, _currentPipeline.Descriptor.Raster);
        GlBlendStateTranslator.Apply(_gl, _currentPipeline.Descriptor.Blend);
        GlDepthStencilTranslator.Apply(_gl, _currentPipeline.Descriptor.DepthStencil);
    }

    public unsafe void BindVertexBuffer(BufferHandle buffer, uint slot = 0, ulong offset = 0)
    {
        if (!_hasPipeline) return;

        var entry = _buffers.Get(buffer);
        uint stride = _currentPipeline.Descriptor.VertexLayout.Stride;

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, entry.Handle);

        if (_currentPipeline.Descriptor.VertexLayout.Attributes != null)
        {
            foreach (var attr in _currentPipeline.Descriptor.VertexLayout.Attributes)
            {
                _gl.EnableVertexAttribArray(attr.Location);
                var (size, type, normalized) = GlFormatMap.ToVertexAttrib(attr.Format);
                _gl.VertexAttribPointer(
                    attr.Location,
                    size,
                    (GLEnum)type,
                    normalized,
                    stride,
                    (void*)(offset + attr.Offset));
            }
        }
    }

    public void BindIndexBuffer(BufferHandle buffer, ulong offset = 0)
    {
        if (!_hasPipeline) return;

        var entry = _buffers.Get(buffer);
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, entry.Handle);
        _indexBufferOffset = offset;
    }

    public void SetTexture(uint slot, TextureHandle texture, SamplerHandle sampler)
    {
        var tex = _textures.Get(texture);
        uint s = _samplers.Get(sampler);

        _gl.ActiveTexture(TextureUnit.Texture0 + (int)slot);
        _gl.BindTexture(TextureTarget.Texture2D, tex.Handle);
        _gl.BindSampler(slot, s);
    }

    public void SetUniformBuffer(uint slot, BufferHandle buffer)
    {
        var buf = _buffers.Get(buffer);
        _gl.BindBufferBase(BufferTargetARB.UniformBuffer, slot, buf.Handle);
    }

    public void SetViewport(float x, float y, float width, float height)
    {
        float w = width > 0f ? width : 1f;
        float h = height > 0f ? height : 1f;
        _gl.Viewport((int)x, (int)y, (uint)w, (uint)h);
    }

    public void SetScissor(int x, int y, uint width, uint height)
    {
        _gl.Enable(EnableCap.ScissorTest);
        _gl.Scissor(x, y, Math.Max(1u, width), Math.Max(1u, height));
    }

    public unsafe void PushConstants(ReadOnlySpan<byte> data, uint offset = 0)
    {
        // Modern OpenGL push constant emulation
        // Can be forwarded via uniform buffers or program uniforms
    }

    public void Draw(uint vertexCount, uint instanceCount = 1, uint firstVertex = 0)
    {
        if (!_hasPipeline) return;

        if (instanceCount > 1)
        {
            _gl.DrawArraysInstanced(_currentPipeline.Topology, (int)firstVertex, vertexCount, instanceCount);
        }
        else
        {
            _gl.DrawArrays(_currentPipeline.Topology, (int)firstVertex, vertexCount);
        }
    }

    public unsafe void DrawIndexed(uint indexCount, uint instanceCount = 1, uint firstIndex = 0)
    {
        if (!_hasPipeline) return;

        void* indices = (void*)(nint)(_indexBufferOffset + (ulong)(firstIndex * sizeof(uint)));

        if (instanceCount > 1)
        {
            _gl.DrawElementsInstanced(_currentPipeline.Topology, indexCount, DrawElementsType.UnsignedInt, indices, instanceCount);
        }
        else
        {
            _gl.DrawElements(_currentPipeline.Topology, indexCount, DrawElementsType.UnsignedInt, indices);
        }
    }
}
