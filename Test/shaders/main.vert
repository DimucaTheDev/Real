#version 460 core
layout (location = 0) in vec3 aPos;
layout (location = 1) in vec3 aColor;

layout (location = 0) out vec3 ourColor;

#if defined(VULKAN) || defined(GL_VULKAN_GLSL)
layout(push_constant) uniform PushConstants {
    mat4 uMVP;
    float uTime;
    float uAspect;
};
#else
uniform mat4 uMVP;
uniform float uTime;
uniform float uAspect;
#endif

void main()
{
    // If a full MVP matrix is provided, use it
    if (uMVP[3][3] != 0.0)
    {
        gl_Position = uMVP * vec4(aPos, 1.0);
    }
    else
    {
        // Continuous rotation based on uTime with aspect ratio correction
        float t = uTime;
        float cy = cos(t * 1.1), sy = sin(t * 1.1);
        float cx = cos(t * 0.75), sx = sin(t * 0.75);

        mat3 rotY = mat3(
             cy, 0.0,  sy,
            0.0, 1.0, 0.0,
            -sy, 0.0,  cy
        );
        mat3 rotX = mat3(
            1.0, 0.0, 0.0,
            0.0,  cx, -sx,
            0.0,  sx,  cx
        );

        vec3 rotated = rotX * (rotY * (aPos * 0.7));
        float zDist = rotated.z * 0.4 + 1.8;
        float aspect = (uAspect > 0.01) ? uAspect : 1.0;
        gl_Position = vec4(rotated.x / (zDist * aspect), rotated.y / zDist, rotated.z * 0.2 + 0.5, 1.0);
    }

    ourColor = aColor;
}


