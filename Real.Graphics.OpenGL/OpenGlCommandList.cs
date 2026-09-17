using System;
using System.Collections.Generic;
using System.Numerics;
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

    public void Reset()
    {
        _currentPipeline = default;
        _hasPipeline = false;
        _indexBufferOffset = 0;
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

        var attrs = _currentPipeline.Descriptor.VertexLayout.Attributes;
        if (attrs != null)
        {
            for (int i = 0; i < attrs.Length; i++)
            {
                ref readonly var attr = ref attrs[i];
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

    private int GetUniformLocation(string name)
    {
        if (!_hasPipeline) return -1;
        return _pipelines.GetUniformLocation(_currentPipeline.Program, name);
    }

    public void SetUniform(string name, int value)
    {
        int loc = GetUniformLocation(name);
        if (loc != -1) _gl.Uniform1(loc, value);
    }

    public void SetUniform(string name, uint value)
    {
        int loc = GetUniformLocation(name);
        if (loc != -1) _gl.Uniform1(loc, value);
    }

    public void SetUniform(string name, float value)
    {
        int loc = GetUniformLocation(name);
        if (loc != -1) _gl.Uniform1(loc, value);
    }

    public void SetUniform(string name, bool value)
    {
        int loc = GetUniformLocation(name);
        if (loc != -1) _gl.Uniform1(loc, value ? 1 : 0);
    }

    public void SetUniform(string name, Vector2 value)
    {
        int loc = GetUniformLocation(name);
        if (loc != -1) _gl.Uniform2(loc, value.X, value.Y);
    }

    public void SetUniform(string name, float x, float y)
    {
        int loc = GetUniformLocation(name);
        if (loc != -1) _gl.Uniform2(loc, x, y);
    }

    public void SetUniform(string name, Vector3 value)
    {
        int loc = GetUniformLocation(name);
        if (loc != -1) _gl.Uniform3(loc, value.X, value.Y, value.Z);
    }

    public void SetUniform(string name, float x, float y, float z)
    {
        int loc = GetUniformLocation(name);
        if (loc != -1) _gl.Uniform3(loc, x, y, z);
    }

    public void SetUniform(string name, Vector4 value)
    {
        int loc = GetUniformLocation(name);
        if (loc != -1) _gl.Uniform4(loc, value.X, value.Y, value.Z, value.W);
    }

    public void SetUniform(string name, float x, float y, float z, float w)
    {
        int loc = GetUniformLocation(name);
        if (loc != -1) _gl.Uniform4(loc, x, y, z, w);
    }

    public unsafe void SetUniform(string name, in Matrix4x4 value, bool transpose = false)
    {
        int loc = GetUniformLocation(name);
        if (loc != -1)
        {
            fixed (float* p = &value.M11)
            {
                _gl.UniformMatrix4(loc, 1, transpose, p);
            }
        }
    }

    public unsafe void SetUniform(string name, ReadOnlySpan<float> values)
    {
        int loc = GetUniformLocation(name);
        if (loc != -1 && values.Length > 0)
        {
            fixed (float* p = values)
            {
                _gl.Uniform1(loc, (uint)values.Length, p);
            }
        }
    }

    public unsafe void SetUniform(string name, ReadOnlySpan<int> values)
    {
        int loc = GetUniformLocation(name);
        if (loc != -1 && values.Length > 0)
        {
            fixed (int* p = values)
            {
                _gl.Uniform1(loc, (uint)values.Length, p);
            }
        }
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
