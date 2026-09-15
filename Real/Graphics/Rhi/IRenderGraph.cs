namespace Real.Graphics.Rhi;

public interface IRenderGraph
{
    RenderPassBuilder AddPass(string name);

    /// <summary>
    /// Resolves resource dependencies declared via Reads/Writes into barriers /
    /// layout transitions, records all passes into command buffer(s), and submits.
    /// </summary>
    void Execute();
}
