using Real.Graphics.Rhi.Enums;
using Silk.NET.Vulkan;

namespace Real.Graphics.Vulkan.Translation;

internal static class VkFormatMap
{
    public static Format ToVkFormat(TextureFormat format) => format switch
    {
        TextureFormat.R8Unorm => Format.R8Unorm,
        TextureFormat.Rg8Unorm => Format.R8G8Unorm,
        TextureFormat.Rgba8Unorm => Format.R8G8B8A8Unorm,
        TextureFormat.Rgba8Srgb => Format.R8G8B8A8Srgb,
        TextureFormat.Bgra8Unorm => Format.B8G8R8A8Unorm,
        TextureFormat.Bgra8Srgb => Format.B8G8R8A8Srgb,
        TextureFormat.Rgba16Float => Format.R16G16B16A16Sfloat,
        TextureFormat.Rgba32Float => Format.R32G32B32A32Sfloat,
        TextureFormat.D32Float => Format.D32Sfloat,
        TextureFormat.D24UnormS8Uint => Format.D24UnormS8Uint,
        TextureFormat.Bc7Unorm => Format.BC7UnormBlock,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unmapped TextureFormat.")
    };

    public static Format ToVkVertexFormat(TextureFormat format) => format switch
    {
        TextureFormat.Rg8Unorm => Format.R32G32Sfloat,
        TextureFormat.Rgba8Unorm => Format.R32G32B32Sfloat,
        TextureFormat.Rgba32Float => Format.R32G32B32A32Sfloat,
        _ => ToVkFormat(format)
    };

    public static bool IsDepthFormat(TextureFormat format) =>
        format is TextureFormat.D32Float or TextureFormat.D24UnormS8Uint;
}
