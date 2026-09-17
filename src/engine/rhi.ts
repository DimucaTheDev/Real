import {
  PrimitiveTopology,
  CullMode,
  BlendFactor,
  PipelineDescriptor,
  RenderGraphPass,
  BarrierTransition,
  ImageLayout,
} from '../types/rhi';

export interface GpuStats {
  fps: number;
  frameTimeMs: number;
  drawCalls: number;
  vertexCount: number;
  triangles: number;
  bufferMemoryBytes: number;
  barriersInserted: number;
  pipelineBinds: number;
}

export class RealEngineRHI {
  private gl: WebGL2RenderingContext | null = null;
  private canvas: HTMLCanvasElement | null = null;
  private program: WebGLProgram | null = null;
  private vao: WebGLVertexArrayObject | null = null;
  private vertexBuffer: WebGLBuffer | null = null;

  private currentTopology: PrimitiveTopology = PrimitiveTopology.TriangleList;
  private cullMode: CullMode = CullMode.None;
  private blendEnabled = false;
  private rotationAngle = 0;
  private autoRotate = true;
  private rotationSpeed = 1.0;
  private scale = 1.0;
  private clearColor: [number, number, number, number] = [0.08, 0.09, 0.11, 1.0];

  public passes: RenderGraphPass[] = [
    {
      id: 'pass-0',
      name: 'DepthPrePass',
      reads: [],
      writes: [],
      depthWrite: 'DepthBuffer_Main',
      enabled: false,
      clearColor: [0, 0, 0, 1],
    },
    {
      id: 'pass-1',
      name: 'TestPass',
      reads: [],
      writes: ['Swapchain_Backbuffer_0'],
      enabled: true,
      clearColor: [0.08, 0.09, 0.11, 1.0],
    },
    {
      id: 'pass-2',
      name: 'PostProcessComposite',
      reads: ['Swapchain_Backbuffer_0'],
      writes: ['FinalOutput_Target'],
      enabled: false,
      clearColor: [0, 0, 0, 1],
    },
  ];

  public lastBarriers: BarrierTransition[] = [];
  public stats: GpuStats = {
    fps: 60,
    frameTimeMs: 16.6,
    drawCalls: 1,
    vertexCount: 3,
    triangles: 1,
    bufferMemoryBytes: 60, // 3 vertices * 20 bytes (float2 pos + float3 color)
    barriersInserted: 2,
    pipelineBinds: 1,
  };

  private frameCount = 0;
  private lastFpsTime = performance.now();
  private lastFrameTime = performance.now();

  public init(canvas: HTMLCanvasElement): boolean {
    this.canvas = canvas;
    const gl = canvas.getContext('webgl2', {
      alpha: false,
      antialias: true,
      depth: true,
      stencil: false,
      preserveDrawingBuffer: false,
    });

    if (!gl) {
      console.error('WebGL2 is not supported');
      return false;
    }

    this.gl = gl;
    return this.setupPipeline();
  }

  public setupPipeline(vertSource?: string, fragSource?: string): boolean {
    if (!this.gl) return false;
    const gl = this.gl;

    const vsCode = vertSource || `#version 300 es
layout (location = 0) in vec2 aPos;
layout (location = 1) in vec3 aColor;

out vec3 ourColor;
uniform float uAngle;
uniform float uScale;

void main() {
    float cosA = cos(uAngle);
    float sinA = sin(uAngle);
    mat2 rot = mat2(cosA, -sinA, sinA, cosA);
    vec2 pos = rot * (aPos * uScale);
    gl_Position = vec4(pos, 0.0, 1.0);
    ourColor = aColor;
}`;

    const fsCode = fragSource || `#version 300 es
precision highp float;
in vec3 ourColor;
out vec4 FragColor;

void main() {
    FragColor = vec4(ourColor, 1.0);
}`;

    const vs = gl.createShader(gl.VERTEX_SHADER);
    const fs = gl.createShader(gl.FRAGMENT_SHADER);
    if (!vs || !fs) return false;

    gl.shaderSource(vs, vsCode);
    gl.compileShader(vs);
    if (!gl.getShaderParameter(vs, gl.COMPILE_STATUS)) {
      console.error('Vertex shader compile error:', gl.getShaderInfoLog(vs));
      gl.deleteShader(vs);
      gl.deleteShader(fs);
      return false;
    }

    gl.shaderSource(fs, fsCode);
    gl.compileShader(fs);
    if (!gl.getShaderParameter(fs, gl.COMPILE_STATUS)) {
      console.error('Fragment shader compile error:', gl.getShaderInfoLog(fs));
      gl.deleteShader(vs);
      gl.deleteShader(fs);
      return false;
    }

    const prog = gl.createProgram();
    if (!prog) return false;

    gl.attachShader(prog, vs);
    gl.attachShader(prog, fs);
    gl.linkProgram(prog);

    if (!gl.getProgramParameter(prog, gl.LINK_STATUS)) {
      console.error('Program link error:', gl.getProgramInfoLog(prog));
      gl.deleteProgram(prog);
      return false;
    }

    if (this.program) {
      gl.deleteProgram(this.program);
    }
    this.program = prog;

    // Setup Geometry matching Test/Program.cs
    // Vertex layout: Location 0 = vec2 aPos (8 bytes), Location 1 = vec3 aColor (12 bytes)
    // Stride = 20 bytes
    const vertices = new Float32Array([
      // X,     Y,       R,   G,   B
       0.0,   0.55,    1.0, 0.2, 0.3,   // Top (Vibrant Red)
       0.55, -0.45,    0.2, 0.9, 0.4,   // Bottom Right (Vibrant Green)
      -0.55, -0.45,    0.2, 0.4, 1.0,   // Bottom Left (Vibrant Blue)
    ]);

    if (!this.vao) {
      this.vao = gl.createVertexArray();
    }
    gl.bindVertexArray(this.vao);

    if (!this.vertexBuffer) {
      this.vertexBuffer = gl.createBuffer();
    }
    gl.bindBuffer(gl.ARRAY_BUFFER, this.vertexBuffer);
    gl.bufferData(gl.ARRAY_BUFFER, vertices, gl.STATIC_DRAW);

    // Stride = 5 floats * 4 bytes = 20 bytes
    const stride = 5 * Float32Array.BYTES_PER_ELEMENT;

    // Location 0: aPos (vec2)
    gl.enableVertexAttribArray(0);
    gl.vertexAttribPointer(0, 2, gl.FLOAT, false, stride, 0);

    // Location 1: aColor (vec3)
    gl.enableVertexAttribArray(1);
    gl.vertexAttribPointer(1, 3, gl.FLOAT, false, stride, 2 * Float32Array.BYTES_PER_ELEMENT);

    gl.bindVertexArray(null);
    return true;
  }

  public setTopology(top: PrimitiveTopology) {
    this.currentTopology = top;
  }

  public setCullMode(mode: CullMode) {
    this.cullMode = mode;
  }

  public setBlend(enabled: boolean) {
    this.blendEnabled = enabled;
  }

  public setAutoRotate(auto: boolean) {
    this.autoRotate = auto;
  }

  public setRotationSpeed(speed: number) {
    this.rotationSpeed = speed;
  }

  public setRotationAngle(angle: number) {
    this.rotationAngle = angle;
  }

  public setScale(scale: number) {
    this.scale = scale;
  }

  public setClearColor(r: number, g: number, b: number, a = 1.0) {
    this.clearColor = [r, g, b, a];
  }

  public resolveRenderGraphBarriers() {
    const barriers: BarrierTransition[] = [];
    for (const pass of this.passes) {
      if (!pass.enabled) continue;

      for (const write of pass.writes) {
        barriers.push({
          resource: write,
          oldLayout: ImageLayout.Undefined,
          newLayout: ImageLayout.ColorAttachmentOptimal,
          srcStage: 'VK_PIPELINE_STAGE_TOP_OF_PIPE_BIT',
          dstStage: 'VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT',
          srcAccess: '0',
          dstAccess: 'VK_ACCESS_COLOR_ATTACHMENT_WRITE_BIT',
        });
      }

      for (const read of pass.reads) {
        barriers.push({
          resource: read,
          oldLayout: ImageLayout.ColorAttachmentOptimal,
          newLayout: ImageLayout.ShaderReadOnlyOptimal,
          srcStage: 'VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT',
          dstStage: 'VK_PIPELINE_STAGE_FRAGMENT_SHADER_BIT',
          srcAccess: 'VK_ACCESS_COLOR_ATTACHMENT_WRITE_BIT',
          dstAccess: 'VK_ACCESS_SHADER_READ_BIT',
        });
      }

      if (pass.writes.some(w => w.includes('Backbuffer'))) {
        barriers.push({
          resource: 'Swapchain_Backbuffer_0',
          oldLayout: ImageLayout.ColorAttachmentOptimal,
          newLayout: ImageLayout.PresentSrcKhr,
          srcStage: 'VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT',
          dstStage: 'VK_PIPELINE_STAGE_BOTTOM_OF_PIPE_BIT',
          srcAccess: 'VK_ACCESS_COLOR_ATTACHMENT_WRITE_BIT',
          dstAccess: '0',
        });
      }
    }

    this.lastBarriers = barriers;
    this.stats.barriersInserted = barriers.length;
  }

  public render(deltaTime: number) {
    if (!this.gl || !this.canvas || !this.program || !this.vao) return;
    const gl = this.gl;

    const now = performance.now();
    this.stats.frameTimeMs = parseFloat((now - this.lastFrameTime).toFixed(2));
    this.lastFrameTime = now;

    this.frameCount++;
    if (now - this.lastFpsTime >= 1000) {
      this.stats.fps = Math.round((this.frameCount * 1000) / (now - this.lastFpsTime));
      this.frameCount = 0;
      this.lastFpsTime = now;
    }

    if (this.autoRotate) {
      this.rotationAngle += deltaTime * this.rotationSpeed;
    }

    // Resize viewport dynamically
    const displayWidth = this.canvas.clientWidth;
    const displayHeight = this.canvas.clientHeight;
    if (this.canvas.width !== displayWidth || this.canvas.height !== displayHeight) {
      this.canvas.width = displayWidth;
      this.canvas.height = displayHeight;
    }
    gl.viewport(0, 0, this.canvas.width, this.canvas.height);

    // Rasterizer state
    if (this.cullMode === CullMode.None) {
      gl.disable(gl.CULL_FACE);
    } else {
      gl.enable(gl.CULL_FACE);
      gl.cullFace(this.cullMode === CullMode.Front ? gl.FRONT : gl.BACK);
    }

    // Blend state
    if (this.blendEnabled) {
      gl.enable(gl.BLEND);
      gl.blendFunc(gl.SRC_ALPHA, gl.ONE_MINUS_SRC_ALPHA);
    } else {
      gl.disable(gl.BLEND);
    }

    // Clear
    gl.clearColor(this.clearColor[0], this.clearColor[1], this.clearColor[2], this.clearColor[3]);
    gl.clear(gl.COLOR_BUFFER_BIT | gl.DEPTH_BUFFER_BIT);

    // Bind Pipeline & Shader Program
    gl.useProgram(this.program);

    const uAngleLoc = gl.getUniformLocation(this.program, 'uAngle');
    const uScaleLoc = gl.getUniformLocation(this.program, 'uScale');
    if (uAngleLoc) gl.uniform1f(uAngleLoc, this.rotationAngle);
    if (uScaleLoc) gl.uniform1f(uScaleLoc, this.scale);

    // Bind Vertex Array
    gl.bindVertexArray(this.vao);

    // Draw with configured PrimitiveTopology
    let mode: GLenum = gl.TRIANGLES;
    if (this.currentTopology === PrimitiveTopology.LineList || this.currentTopology === PrimitiveTopology.LineStrip) {
      mode = gl.LINE_LOOP;
    } else if (this.currentTopology === PrimitiveTopology.PointList) {
      mode = gl.POINTS;
    }

    gl.drawArrays(mode, 0, 3);
    gl.bindVertexArray(null);

    this.resolveRenderGraphBarriers();
  }

  public getRotationAngle(): number {
    return this.rotationAngle;
  }

  public getAutoRotate(): boolean {
    return this.autoRotate;
  }
}

export const engineRHI = new RealEngineRHI();
