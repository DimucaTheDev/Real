#version 460 core
layout (location = 0) in vec3 aPos;
layout (location = 1) in vec3 aColor;

layout (location = 0) out vec3 ourColor;

void main()
{
    // RealEngine 3D Cube Rotation & Projection
    float angleY = 0.785398; // 45 degrees
    float angleX = 0.61548;  // Isometric pitch (~35.26 degrees)
    float cy = cos(angleY);
    float sy = sin(angleY);
    float cx = cos(angleX);
    float sx = sin(angleX);

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
    gl_Position = vec4(rotated.xy / zDist, rotated.z * 0.2 + 0.5, 1.0);
    ourColor = aColor;
}

