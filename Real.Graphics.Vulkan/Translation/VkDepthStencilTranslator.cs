using Real.Graphics.Rhi.Descriptors;
using Silk.NET.Vulkan;
using CompareOp = Silk.NET.Vulkan.CompareOp;

namespace Real.Graphics.Vulkan.Translation;

internal static class VkDepthStencilTranslator
{
    public static PipelineDepthStencilStateCreateInfo ToVk(in DepthStencilState state) => new()
    {
        SType = StructureType.PipelineDepthStencilStateCreateInfo,
        DepthTestEnable = state.DepthTestEnabled,
        DepthWriteEnable = state.DepthWriteEnabled,
        DepthCompareOp = ToVkCompareOp(state.DepthCompare)
    };

    private static CompareOp ToVkCompareOp(Rhi.Enums.CompareOp op) => op switch
    {
        Rhi.Enums.CompareOp.Never => CompareOp.Never,
        Rhi.Enums.CompareOp.Less => CompareOp.Less,
        Rhi.Enums.CompareOp.Equal => CompareOp.Equal,
        Rhi.Enums.CompareOp.LessOrEqual => CompareOp.LessOrEqual,
        Rhi.Enums.CompareOp.Greater => CompareOp.Greater,
        Rhi.Enums.CompareOp.NotEqual => CompareOp.NotEqual,
        Rhi.Enums.CompareOp.GreaterOrEqual => CompareOp.GreaterOrEqual,
        Rhi.Enums.CompareOp.Always => CompareOp.Always,
        _ => CompareOp.Always
    };
}
