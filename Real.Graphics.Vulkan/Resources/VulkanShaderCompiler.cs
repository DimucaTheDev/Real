using Silk.NET.Vulkan;

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
    private readonly Vk _vk;
    private readonly Device _device;

    public VulkanShaderCompiler(Vk vk, Device device)
    {
        _vk = vk;
        _device = device;
    }

    public unsafe ShaderModule CreateModule(ReadOnlySpan<byte> spirv)
    {
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
}
