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

    void Draw(uint vertexCount, uint instanceCount = 1, uint firstVertex = 0);
    void DrawIndexed(uint indexCount, uint instanceCount = 1, uint firstIndex = 0);
}
