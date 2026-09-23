using System.Buffers.Binary;
using System.Text;
using Real.Graphics.Rhi.Enums;
using Silk.NET.Shaderc;
using Silk.NET.Vulkan;
using Buffer = System.Buffer;

namespace Real.Graphics.Vulkan.Resources;

/// <summary>
/// Creates VkShaderModule from precompiled SPIR-V bytecode.
/// This backend does NOT compile HLSL/GLSL itself - the asset pipeline is
/// expected to produce SPIR-V ahead of time (e.g. via DXC / glslang / shaderc)
/// so both the Vulkan and OpenGL backends consume already-compiled shaders
/// (SPIR-V here, SPIRV-Cross-generated GLSL for the GL backend).
/// </summary>
internal sealed class VulkanShaderCompiler
{
    private enum ShaderFormat
    {
        Unknown,
        SpirV,
        Glsl
    }

    private readonly Vk _vk;
    private readonly Device _device;

    public VulkanShaderCompiler(Vk vk, Device device)
    {
        _vk = vk;
        _device = device;
    }

    public unsafe ShaderModule CreateModule(ReadOnlySpan<byte> source, ShaderStage stage, string entryPoint = "main")
    {
        var spirv = source;
        if (Detect(source) == ShaderFormat.Glsl)
        {
            spirv = CompileGlslToSpirv(Encoding.UTF8.GetString(source), "", stage switch
            {
                ShaderStage.None => throw new(),
                ShaderStage.Vertex => ShaderKind.VertexShader,
                ShaderStage.Fragment => ShaderKind.FragmentShader,
                ShaderStage.Compute => ShaderKind.ComputeShader,
                _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, null)
            });
        }

        fixed (byte* pCode = spirv)
        {
            var createInfo = new ShaderModuleCreateInfo
            {
                SType = StructureType.ShaderModuleCreateInfo,
                CodeSize = (nuint)spirv.Length,
                PCode = (uint*)pCode
            };

            if (_vk.CreateShaderModule(_device, in createInfo, null, out var module) != Result.Success)
                throw new Exception($"Failed to create shader module: {module}");
            return module;
        }
    }

    public static unsafe byte[] CompileGlslToSpirv
        (string source, string fileName, ShaderKind kind, bool optimize = true)
    {
        var shaderc = Shaderc.GetApi();
        var compiler = shaderc.CompilerInitialize();
        var options = shaderc.CompileOptionsInitialize();

        if (compiler == null || options == null)
        {
            throw new InvalidOperationException("Unable to initialize Shaderc");
        }

        try
        {
            if (optimize)
            {
                shaderc.CompileOptionsSetOptimizationLevel(
                    options,
                    OptimizationLevel.Performance);
            }

            // Ветки "#if defined(VULKAN)" в шейдерах (например main.vert)
            // полагаются на этот макрос, чтобы переключиться на push_constant
            // вместо loose-uniform, который не соответствует тому, что кладёт
            // CmdPushConstants в VulkanCommandList.SetUniform.
            //shaderc.CompileOptionsAddMacroDefinition(options, "VULKAN", (nuint)6, "1", (nuint)1);

            // ВАЖНО: source.Length - это количество UTF-16 code units в C#-строке,
            // а не байт UTF-8, которые реально уходят в нативный
            // shaderc_compile_into_spv (Silk.NET маршалит string-параметр в
            // UTF-8 сам, но nuint-длину не пересчитывает - её считает вызывающий
            // код). Для чисто ASCII-шейдеров оба числа совпадают, поэтому баг
            // был незаметен - но для любого шейдера с не-ASCII символами в
            // комментариях (например кириллицей) source.Length оказывается
            // МЕНЬШЕ настоящей длины UTF-8-буфера, shaderc получает урезанный
            // по байтам (а не по символам) буфер, и в зависимости от того, где
            // именно пришёлся обрез - ловит либо "useless application of layout
            // qualifier" на случайной строке, либо "Missing entry point", если
            // обрезало раньше void main().
            var sourceByteCount = (nuint)Encoding.UTF8.GetByteCount(source);

            var result = shaderc.CompileIntoSpv(
                compiler,
                source,
                sourceByteCount,

                kind,
                fileName,
                "main",
                options);

            try
            {
                var status = shaderc.ResultGetCompilationStatus(result);
                if (status != CompilationStatus.Success)
                {
                    var error = shaderc.ResultGetErrorMessageS(result);
                    throw new Exception($"Ошибка компиляции шейдера ({fileName}): {error}");
                }

                var length = shaderc.ResultGetLength(result);
                var bytesPtr = (byte*)shaderc.ResultGetBytes(result);

                var spirvBytes = new byte[length];
                fixed (byte* dst = spirvBytes)
                {
                    Buffer.MemoryCopy(bytesPtr, dst, length, length);
                }

                return spirvBytes;
            }
            finally
            {
                shaderc.ResultRelease(result);
            }
        }
        finally
        {
            shaderc.CompileOptionsRelease(options);
            shaderc.CompilerRelease(compiler);
        }
    }

    private const uint SpirvMagicLittleEndian = 0x07230203;
    private const uint SpirvMagicBigEndian = 0x03022307;

    private static ShaderFormat Detect(ReadOnlySpan<byte> data)
    {
        if (data.Length < 4)
            return ShaderFormat.Unknown;

        uint magic = BinaryPrimitives.ReadUInt32LittleEndian(data);

        if (magic == SpirvMagicLittleEndian || magic == SpirvMagicBigEndian)
        {
            if (data.Length % 4 == 0)
                return ShaderFormat.SpirV;
        }

        if (IsValidUtf8Text(data))
        {
            return ShaderFormat.Glsl;
        }

        return ShaderFormat.Unknown;
    }

    private static bool IsValidUtf8Text(ReadOnlySpan<byte> data)
    {
        if (data.IndexOf((byte)0) != -1)
            return false;

        try
        {
            _ = Encoding.UTF8.GetCharCount(data);
            return true;
        }
        catch
        {
            return false;
        }
    }
}