using Real.Graphics.Rhi.Descriptors;
using Real.Graphics.Rhi.Enums;
using Silk.NET.Vulkan;

namespace Real.Graphics.Vulkan.Translation;

internal static class VkRasterStateTranslator
{
    public static PipelineRasterizationStateCreateInfo ToVk(in RasterState state) => new()
    {
        SType = StructureType.PipelineRasterizationStateCreateInfo,
        PolygonMode = state.Wireframe ? PolygonMode.Line : PolygonMode.Fill,
        CullMode = ToVkCullMode(state.CullMode),
        FrontFace = state.FrontFaceCounterClockwise ? FrontFace.CounterClockwise : FrontFace.Clockwise,
        LineWidth = 1.0f
    };

    private static CullModeFlags ToVkCullMode(CullMode mode) => mode switch
    {
        CullMode.None => CullModeFlags.None,
        CullMode.Front => CullModeFlags.FrontBit,
        CullMode.Back => CullModeFlags.BackBit,
        _ => CullModeFlags.BackBit
    };
}
