using Real.Graphics.Rhi.Descriptors;
using Silk.NET.Vulkan;
using BlendFactor = Real.Graphics.Rhi.Enums.BlendFactor;

namespace Real.Graphics.Vulkan.Translation;

internal static class VkBlendStateTranslator
{
    public static PipelineColorBlendAttachmentState ToVk(in BlendState state) => new()
    {
        BlendEnable = state.Enabled,
        SrcColorBlendFactor = ToVkFactor(state.SrcColor),
        DstColorBlendFactor = ToVkFactor(state.DstColor),
        ColorBlendOp = BlendOp.Add,
        SrcAlphaBlendFactor = ToVkFactor(state.SrcAlpha),
        DstAlphaBlendFactor = ToVkFactor(state.DstAlpha),
        AlphaBlendOp = BlendOp.Add,
        ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit
            | ColorComponentFlags.BBit | ColorComponentFlags.ABit
    };

    private static Silk.NET.Vulkan.BlendFactor ToVkFactor(BlendFactor f) => f switch
    {
        BlendFactor.Zero => Silk.NET.Vulkan.BlendFactor.Zero,
        BlendFactor.One => Silk.NET.Vulkan.BlendFactor.One,
        BlendFactor.SrcAlpha => Silk.NET.Vulkan.BlendFactor.SrcAlpha,
        BlendFactor.OneMinusSrcAlpha => Silk.NET.Vulkan.BlendFactor.OneMinusSrcAlpha,
        BlendFactor.DstAlpha => Silk.NET.Vulkan.BlendFactor.DstAlpha,
        BlendFactor.OneMinusDstAlpha => Silk.NET.Vulkan.BlendFactor.OneMinusDstAlpha,
        _ => Silk.NET.Vulkan.BlendFactor.One
    };
}
