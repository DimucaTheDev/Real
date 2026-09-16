using System;
using Real.Graphics.Rhi.Enums;
using Silk.NET.OpenGL;

namespace Real.Graphics.OpenGL.Translation;

internal static class GlTopologyTranslator
{
    public static PrimitiveType ToGl(PrimitiveTopology topology) => topology switch
    {
        PrimitiveTopology.TriangleList => PrimitiveType.Triangles,
        PrimitiveTopology.TriangleStrip => PrimitiveType.TriangleStrip,
        PrimitiveTopology.LineList => PrimitiveType.Lines,
        PrimitiveTopology.PointList => PrimitiveType.Points,
        _ => PrimitiveType.Triangles
    };
}
