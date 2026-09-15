using Real.Graphics.Rhi.Handles;

namespace Real.Graphics.Rhi;

/// <summary>
/// Declares a render pass: which resources it reads / writes, and what it executes.
/// The render graph uses Reads/Writes to derive barriers automatically -
/// the caller never issues a barrier directly.
/// </summary>
public sealed class RenderPassBuilder
{
    public string Name { get; }

    public readonly List<TextureHandle> ReadTextures = new();
    public readonly List<TextureHandle> ColorWrites = new();
    public TextureHandle? DepthWrite;
    public Action<ICommandList>? Execution;

    public RenderPassBuilder(string name) => Name = name;

    public RenderPassBuilder Reads(TextureHandle texture)
    {
        ReadTextures.Add(texture);
        return this;
    }

    public RenderPassBuilder Writes(TextureHandle colorAttachment)
    {
        ColorWrites.Add(colorAttachment);
        return this;
    }

    public RenderPassBuilder WritesDepth(TextureHandle depthAttachment)
    {
        DepthWrite = depthAttachment;
        return this;
    }

    public RenderPassBuilder SetExecute(Action<ICommandList> execute)
    {
        Execution = execute;
        return this;
    }
}
