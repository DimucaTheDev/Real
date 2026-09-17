export interface VfsFile {
  path: string;
  scheme: 'file' | 'pak' | 'assembly';
  content: string;
  isBinary: boolean;
  sizeBytes: number;
  lastModified: string;
}

export class VirtualFileSystem {
  private files: Map<string, VfsFile> = new Map();

  constructor() {
    this.seedDefaultFiles();
  }

  private seedDefaultFiles() {
    // Shaders from Test/shaders/
    this.registerFile('file://shaders/triangle.vert', `#version 460 core
layout (location = 0) in vec2 aPos;
layout (location = 1) in vec3 aColor;

layout (location = 0) out vec3 ourColor;

void main()
{
    gl_Position = vec4(aPos, 0.0, 1.0);
    ourColor = aColor;
}`, false, '2026-03-10T12:00:00Z');

    this.registerFile('file://shaders/triangle.frag', `#version 460 core
layout (location = 0) in vec3 ourColor;

layout (location = 0) out vec4 FragColor;

void main()
{
    FragColor = vec4(ourColor, 1.0f);
}`, false, '2026-03-10T12:00:00Z');

    this.registerFile('file://shaders/main.vert.spv', `[SPIR-V Magic 0x07230203 | Version 1.5 | Generator: GlslangValidator | Bound: 24 | Schema: 0]
OpCapability Shader
OpMemoryModel Logical GLSL450
OpEntryPoint Vertex %main "main" %aPos %aColor %ourColor
OpName %aPos "aPos"
OpName %aColor "aColor"
OpDecorate %aPos Location 0
OpDecorate %aColor Location 1
OpDecorate %ourColor Location 0`, true, '2026-03-10T12:00:00Z');

    this.registerFile('file://shaders/main.frag.spv', `[SPIR-V Magic 0x07230203 | Version 1.5 | Generator: GlslangValidator | Bound: 18 | Schema: 0]
OpCapability Shader
OpMemoryModel Logical GLSL450
OpEntryPoint Fragment %main "main" %ourColor %FragColor
OpName %ourColor "ourColor"
OpName %FragColor "FragColor"
OpDecorate %ourColor Location 0
OpDecorate %FragColor Location 0`, true, '2026-03-10T12:00:00Z');

    // Archive storage provider: pak://
    this.registerFile('pak://textures/checkerboard.rgba', `[REAL_PAK_V1 HEADER | Offset: 0x0000 | 256x256 RGBA8Unorm texture data buffer]`, true, '2026-03-11T14:20:00Z');
    this.registerFile('pak://audio/bank/Master.bank', `[FMOD Studio Bank File | Version 2.02.04 | Events: "event:/sfx/triangle_hit", "event:/music/engine_hum"]`, true, '2026-03-11T14:20:00Z');
    this.registerFile('pak://audio/bank/Master.strings.bank', `[FMOD Studio Bank Strings Index | 2 strings cached]`, true, '2026-03-11T14:20:00Z');

    // Embedded provider: assembly://
    this.registerFile('assembly://Real.Graphics.Vulkan/VulkanPipelineCache.bin', `[Vulkan Pipeline Cache PipelineLayout=0x01 SPIR-V Hash=0x9a8f231e State=PSO_READY]`, true, '2026-03-12T09:15:00Z');
    this.registerFile('assembly://Real.Core/engine.config.json', JSON.stringify({
      engine: 'RealEngine',
      version: '1.0.0-preview',
      rhi: 'Vulkan',
      defaultSwapchainFormat: 'B8G8R8A8_UNORM',
      preferredPresentMode: 'FifoKHR',
      validationLayers: ['VK_LAYER_KHRONOS_validation'],
      deviceExtensions: ['VK_KHR_swapchain']
    }, null, 2), false, '2026-03-12T09:15:00Z');
  }

  public registerFile(fullPath: string, content: string, isBinary: boolean, lastModified = new Date().toISOString()) {
    const url = new URL(fullPath);
    const scheme = url.protocol.replace(':', '') as 'file' | 'pak' | 'assembly';
    const sizeBytes = isBinary ? content.length * 4 : new Blob([content]).size;

    this.files.set(fullPath, {
      path: fullPath,
      scheme,
      content,
      isBinary,
      sizeBytes,
      lastModified,
    });
  }

  public getFile(fullPath: string): VfsFile | undefined {
    return this.files.get(fullPath);
  }

  public setFileContent(fullPath: string, content: string): boolean {
    const existing = this.files.get(fullPath);
    if (!existing) return false;
    existing.content = content;
    existing.sizeBytes = new Blob([content]).size;
    existing.lastModified = new Date().toISOString();
    return true;
  }

  public listFiles(): VfsFile[] {
    return Array.from(this.files.values());
  }

  public listFilesByScheme(scheme: 'file' | 'pak' | 'assembly'): VfsFile[] {
    return this.listFiles().filter(f => f.scheme === scheme);
  }
}

export const engineVfs = new VirtualFileSystem();
