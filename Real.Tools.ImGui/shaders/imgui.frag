#version 460 core

layout(location = 0) in vec2 vUv;
layout(location = 1) in vec4 vColor;

layout(location = 0) out vec4 FragColor;

// binding = 0 is required for Vulkan (see VulkanPipelinePool: binding 0 -
// CombinedImageSampler). On GL this layout qualifier on a sampler2D works
// from 4.2 onward (ARB_shading_language_420pack); on fallback 4.1/3.3
// contexts the driver just ignores it, but since ImGui only ever uses
// slot 0 and GL's default sampler-uniform value is already 0, that's fine.
layout(binding = 0) uniform sampler2D uTexture;

void main()
{
    FragColor = vColor * texture(uTexture, vUv);
}
