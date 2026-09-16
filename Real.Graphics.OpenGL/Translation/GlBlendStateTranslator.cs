using Real.Graphics.Rhi.Descriptors;
using Real.Graphics.Rhi.Enums;
using Silk.NET.OpenGL;
using BlendFactor = Real.Graphics.Rhi.Enums.BlendFactor;

namespace Real.Graphics.OpenGL.Translation;

internal static class GlBlendStateTranslator
{
    public static void Apply(GL gl, in BlendState state)
    {
        if (state.Enabled)
        {
            gl.Enable(EnableCap.Blend);
            gl.BlendFuncSeparate(
                ToGlFactor(state.SrcColor),
                ToGlFactor(state.DstColor),
                ToGlFactor(state.SrcAlpha),
                ToGlFactor(state.DstAlpha));
            gl.BlendEquationSeparate(BlendEquationModeEXT.FuncAdd, BlendEquationModeEXT.FuncAdd);
        }
        else
        {
            gl.Disable(EnableCap.Blend);
        }

        gl.ColorMask(true, true, true, true);
    }

    private static BlendingFactor ToGlFactor(BlendFactor f) => f switch
    {
        BlendFactor.Zero => BlendingFactor.Zero,
        BlendFactor.One => BlendingFactor.One,
        BlendFactor.SrcAlpha => BlendingFactor.SrcAlpha,
        BlendFactor.OneMinusSrcAlpha => BlendingFactor.OneMinusSrcAlpha,
        BlendFactor.DstAlpha => BlendingFactor.DstAlpha,
        BlendFactor.OneMinusDstAlpha => BlendingFactor.OneMinusDstAlpha,
        _ => BlendingFactor.One
    };
}
