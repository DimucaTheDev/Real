using Real.Graphics.Rhi.Descriptors;
using Real.Graphics.Rhi.Enums;
using Silk.NET.OpenGL;

namespace Real.Graphics.OpenGL.Translation;

internal static class GlDepthStencilTranslator
{
    public static void Apply(GL gl, in DepthStencilState state)
    {
        if (state.DepthTestEnabled)
        {
            gl.Enable(EnableCap.DepthTest);
            gl.DepthFunc(ToGlDepthFunc(state.DepthCompare));
        }
        else
        {
            gl.Disable(EnableCap.DepthTest);
        }

        gl.DepthMask(state.DepthWriteEnabled);
    }

    private static DepthFunction ToGlDepthFunc(CompareOp op) => op switch
    {
        CompareOp.Never => DepthFunction.Never,
        CompareOp.Less => DepthFunction.Less,
        CompareOp.Equal => DepthFunction.Equal,
        CompareOp.LessOrEqual => DepthFunction.Lequal,
        CompareOp.Greater => DepthFunction.Greater,
        CompareOp.NotEqual => DepthFunction.Notequal,
        CompareOp.GreaterOrEqual => DepthFunction.Gequal,
        CompareOp.Always => DepthFunction.Always,
        _ => DepthFunction.Always
    };
}
