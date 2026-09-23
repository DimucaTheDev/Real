#version 450 core

layout (location = 0) in vec3 aPos;
layout (location = 1) in vec3 aColor;

layout (location = 0) out vec3 ourColor;

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
    gl_Position = uMVP * vec4(aPos, 1.0);
    ourColor = aColor;
}