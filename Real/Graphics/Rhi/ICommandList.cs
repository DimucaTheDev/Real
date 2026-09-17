using System;
using System.Numerics;
using Real.Graphics.Rhi.Handles;

namespace Real.Graphics.Rhi;

public interface ICommandList
{
    void BindPipeline(PipelineHandle pipeline);

    void BindVertexBuffer(BufferHandle buffer, uint slot = 0, ulong offset = 0);
    void BindIndexBuffer(BufferHandle buffer, ulong offset = 0);

    void SetTexture(uint slot, TextureHandle texture, SamplerHandle sampler);
    void SetUniformBuffer(uint slot, BufferHandle buffer);

    void SetViewport(float x, float y, float width, float height);
    void SetScissor(int x, int y, uint width, uint height);

    void PushConstants(ReadOnlySpan<byte> data, uint offset = 0);

    // Shader Uniform Parameters of various types
    void SetUniform(string name, int value);
    void SetUniform(string name, uint value);
    void SetUniform(string name, float value);
    void SetUniform(string name, bool value);
    void SetUniform(string name, Vector2 value);
    void SetUniform(string name, float x, float y);
    void SetUniform(string name, Vector3 value);
    void SetUniform(string name, float x, float y, float z);
    void SetUniform(string name, Vector4 value);
    void SetUniform(string name, float x, float y, float z, float w);
    void SetUniform(string name, in Matrix4x4 value, bool transpose = false);
    void SetUniform(string name, ReadOnlySpan<float> values);
    void SetUniform(string name, ReadOnlySpan<int> values);

    void Draw(uint vertexCount, uint instanceCount = 1, uint firstVertex = 0);
    void DrawIndexed(uint indexCount, uint instanceCount = 1, uint firstIndex = 0);
}
