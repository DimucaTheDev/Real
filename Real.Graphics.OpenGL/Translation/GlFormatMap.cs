using System;
using Real.Graphics.Rhi.Enums;
using Silk.NET.OpenGL;

namespace Real.Graphics.OpenGL.Translation;

internal static class GlFormatMap
{
    public static SizedInternalFormat ToSizedInternalFormat(TextureFormat format) => format switch
    {
        TextureFormat.R8Unorm => SizedInternalFormat.R8,
        TextureFormat.Rg8Unorm => SizedInternalFormat.RG8,
        TextureFormat.Rgba8Unorm => SizedInternalFormat.Rgba8,
        TextureFormat.Rgba8Srgb => SizedInternalFormat.Srgb8Alpha8,
        TextureFormat.Bgra8Unorm => SizedInternalFormat.Rgba8,
        TextureFormat.Bgra8Srgb => SizedInternalFormat.Srgb8Alpha8,
        TextureFormat.Rgba16Float => SizedInternalFormat.Rgba16f,
        TextureFormat.Rgba32Float => SizedInternalFormat.Rgba32f,
        TextureFormat.D32Float => SizedInternalFormat.DepthComponent32f,
        TextureFormat.D24UnormS8Uint => SizedInternalFormat.Depth24Stencil8,
        TextureFormat.Bc7Unorm => (SizedInternalFormat)GLEnum.CompressedRgbaBptcUnorm,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unmapped TextureFormat.")
    };

    public static PixelFormat ToPixelFormat(TextureFormat format) => format switch
    {
        TextureFormat.R8Unorm => PixelFormat.Red,
        TextureFormat.Rg8Unorm => PixelFormat.RG,
        TextureFormat.Rgba8Unorm => PixelFormat.Rgba,
        TextureFormat.Rgba8Srgb => PixelFormat.Rgba,
        TextureFormat.Bgra8Unorm => PixelFormat.Bgra,
        TextureFormat.Bgra8Srgb => PixelFormat.Bgra,
        TextureFormat.Rgba16Float => PixelFormat.Rgba,
        TextureFormat.Rgba32Float => PixelFormat.Rgba,
        TextureFormat.D32Float => PixelFormat.DepthComponent,
        TextureFormat.D24UnormS8Uint => PixelFormat.DepthStencil,
        TextureFormat.Bc7Unorm => PixelFormat.Rgba,
        _ => PixelFormat.Rgba
    };

    public static PixelType ToPixelType(TextureFormat format) => format switch
    {
        TextureFormat.R8Unorm or TextureFormat.Rg8Unorm or TextureFormat.Rgba8Unorm or
        TextureFormat.Rgba8Srgb or TextureFormat.Bgra8Unorm or TextureFormat.Bgra8Srgb or
        TextureFormat.Bc7Unorm => PixelType.UnsignedByte,
        TextureFormat.Rgba16Float => PixelType.HalfFloat,
        TextureFormat.Rgba32Float or TextureFormat.D32Float => PixelType.Float,
        TextureFormat.D24UnormS8Uint => PixelType.UnsignedInt248,
        _ => PixelType.UnsignedByte
    };

    public static (int Size, VertexAttribType Type, bool Normalized) ToVertexAttrib(TextureFormat format) => format switch
    {
        TextureFormat.R8Unorm => (1, VertexAttribType.Float, false),
        TextureFormat.Rg8Unorm => (2, VertexAttribType.Float, false),
        TextureFormat.Rgba8Unorm => (3, VertexAttribType.Float, false),
        TextureFormat.Rgba32Float => (4, VertexAttribType.Float, false),
        TextureFormat.Rgba16Float => (4, VertexAttribType.HalfFloat, false),
        _ => (4, VertexAttribType.Float, false)
    };

    public static bool IsDepthFormat(TextureFormat format) =>
        format is TextureFormat.D32Float or TextureFormat.D24UnormS8Uint;
}
