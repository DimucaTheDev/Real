using System;
using System.Text;
using Real.Graphics.Rhi.Enums;
using Silk.NET.OpenGL;

namespace Real.Graphics.OpenGL.Resources;

internal sealed class OpenGlShaderCompiler
{
    private readonly GL _gl;

    public OpenGlShaderCompiler(GL gl)
    {
        _gl = gl;
    }

    public unsafe uint CreateShader(ShaderStage stage, ReadOnlySpan<byte> bytecode, string entryPoint = "main")
    {
        var glStage = ToGlShaderType(stage);

        if (IsSpirV(bytecode))
        {
            uint shader = _gl.CreateShader(glStage);
            if (shader == 0)
                throw new InvalidOperationException($"Failed to create OpenGL shader object for stage {stage}.");

            fixed (byte* pCode = bytecode)
            {
                _gl.ShaderBinary(1, &shader, GLEnum.ShaderBinaryFormatSpirV, pCode, (uint)bytecode.Length);
            }

            _gl.SpecializeShader(shader, entryPoint, 0, null, null);

            _gl.GetShader(shader, ShaderParameterName.CompileStatus, out int status);
            if (status == 0)
            {
                string infoLog = _gl.GetShaderInfoLog(shader);
                _gl.DeleteShader(shader);
                throw new InvalidOperationException($"OpenGL SPIR-V specialization failed for {stage} (entry '{entryPoint}'): {infoLog}");
            }

            return shader;
        }
        else
        {
            // GLSL source text
            string source = Encoding.UTF8.GetString(bytecode);
            uint shader = _gl.CreateShader(glStage);
            if (shader == 0)
                throw new InvalidOperationException($"Failed to create OpenGL shader object for stage {stage}.");

            _gl.ShaderSource(shader, source);
            _gl.CompileShader(shader);

            _gl.GetShader(shader, ShaderParameterName.CompileStatus, out int status);
            if (status == 0)
            {
                string infoLog = _gl.GetShaderInfoLog(shader);
                _gl.DeleteShader(shader);
                throw new InvalidOperationException($"OpenGL GLSL compilation failed for {stage}: {infoLog}");
            }

            return shader;
        }
    }

    private static bool IsSpirV(ReadOnlySpan<byte> data)
    {
        if (data.Length < 4) return false;
        // SPIR-V magic number 0x07230203
        return (data[0] == 0x03 && data[1] == 0x02 && data[2] == 0x23 && data[3] == 0x07) ||
               (data[0] == 0x07 && data[1] == 0x23 && data[2] == 0x02 && data[3] == 0x03);
    }

    private static ShaderType ToGlShaderType(ShaderStage stage) => stage switch
    {
        ShaderStage.Vertex => ShaderType.VertexShader,
        ShaderStage.Fragment => ShaderType.FragmentShader,
        ShaderStage.Compute => ShaderType.ComputeShader,
        _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, "Unsupported ShaderStage.")
    };
}
