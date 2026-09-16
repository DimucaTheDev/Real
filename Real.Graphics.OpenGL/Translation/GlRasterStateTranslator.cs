using Real.Graphics.Rhi.Descriptors;
using Real.Graphics.Rhi.Enums;
using Silk.NET.OpenGL;

namespace Real.Graphics.OpenGL.Translation;

internal static class GlRasterStateTranslator
{
    public static void Apply(GL gl, in RasterState state)
    {
        switch (state.CullMode)
        {
            case CullMode.None:
                gl.Disable(EnableCap.CullFace);
                break;
            case CullMode.Front:
                gl.Enable(EnableCap.CullFace);
                gl.CullFace(TriangleFace.Front);
                break;
            case CullMode.Back:
            default:
                gl.Enable(EnableCap.CullFace);
                gl.CullFace(TriangleFace.Back);
                break;
        }

        gl.PolygonMode(TriangleFace.FrontAndBack, state.Wireframe ? PolygonMode.Line : PolygonMode.Fill);
        gl.FrontFace(state.FrontFaceCounterClockwise ? FrontFaceDirection.Ccw : FrontFaceDirection.CW);
    }
}
