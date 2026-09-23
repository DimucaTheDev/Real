#version 460 core

/*
 * ImGui vertex is wider than the native ImDrawVert (pos: vec2, uv: vec2,
 * col: packed uint = 20 bytes). Reason: as of today, Real.Graphics.Rhi
 * (see GlFormatMap.ToVertexAttrib / VkFormatMap.ToVkVertexFormat) only
 * reliably supports TextureFormat.Rgba32Float (vec4) as a vertex
 * attribute format on both backends. There is no Rg32Float in
 * TextureFormat at all, and Rgba8Unorm as a vertex attribute is read as
 * vec3 float on both GL (GlFormatMap.ToVertexAttrib) and Vulkan
 * (VkFormatMap.ToVkVertexFormat) - i.e. NOT as 4 packed color bytes on
 * either backend. So color here is also vec4 float, not a packed uint,
 * and the vertex is inflated from 20 to 48 bytes. Once GlFormatMap/
 * VkFormatMap gain a real Unorm8x4 vertex-attribute format, this can be
 * shrunk back down.
 *
 * NOTE: keep this file ASCII-only. VulkanShaderCompiler.CompileGlslToSpirv
 * used to pass source.Length (UTF-16 code units) as the byte length to
 * shaderc, which breaks for any non-ASCII text in the source (each
 * multi-byte UTF-8 character makes the passed length too short, silently
 * truncating the buffer mid-file). That's fixed now (Encoding.UTF8.GetByteCount),
 * but there's no reason to tempt fate in a file that ships as raw text.
 */
layout(location = 0) in vec4 aPos;   // .xy = position in display space, .zw unused
layout(location = 1) in vec4 aUv;    // .xy = UV, .zw unused
layout(location = 2) in vec4 aColor; // already normalized float color 0..1

layout(location = 0) out vec2 vUv;
layout(location = 1) out vec4 vColor;

#if defined(VULKAN) || defined(GL_VULKAN_GLSL)
layout(push_constant) uniform PushConstants
{
    mat4 uMVP;
};
#else
uniform mat4 uMVP;
#endif

void main()
{
    vUv = aUv.xy;
    vColor = aColor;
    gl_Position = uMVP * vec4(aPos.xy, 0.0, 1.0);
}
