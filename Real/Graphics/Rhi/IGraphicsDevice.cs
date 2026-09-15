using Real.Graphics.Rhi.Descriptors;
using Real.Graphics.Rhi.Enums;
using Real.Graphics.Rhi.Handles;
using Real.Windowing;

namespace Real.Graphics.Rhi;

public interface IGraphicsDevice : IDisposable
{
    string BackendName { get; }

    BufferHandle CreateBuffer(in BufferDescriptor descriptor, ReadOnlySpan<byte> initialData = default);
    void DestroyBuffer(BufferHandle handle);

    TextureHandle CreateTexture(in TextureDescriptor descriptor, ReadOnlySpan<byte> initialData = default);
    void DestroyTexture(TextureHandle handle);

    SamplerHandle CreateSampler(in SamplerDescriptor descriptor);
    void DestroySampler(SamplerHandle handle);

    ShaderHandle CreateShader(ShaderStage stage, ReadOnlySpan<byte> bytecode, string entryPoint = "main");
    void DestroyShader(ShaderHandle handle);

    PipelineHandle CreatePipeline(in PipelineDescriptor descriptor);
    void DestroyPipeline(PipelineHandle handle);

    ISwapchain CreateSwapchain(IWindow window);
    IRenderGraph CreateRenderGraph();

    /// <summary>Blocks until all GPU work submitted by this device has completed.</summary>
    void WaitIdle();
}