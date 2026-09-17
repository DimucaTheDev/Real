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

export type ColorTheme = 'spectrum' | 'cyberpunk' | 'pastel' | 'monochrome';

// Standard 4x4 matrix helpers (column-major)
function mat4Create(): Float32Array {
  const out = new Float32Array(16);
  out[0] = 1;
  out[5] = 1;
  out[10] = 1;
  out[15] = 1;
  return out;
}

function mat4Multiply(out: Float32Array, a: Float32Array, b: Float32Array): Float32Array {
  const a00 = a[0], a01 = a[1], a02 = a[2], a03 = a[3];
  const a10 = a[4], a11 = a[5], a12 = a[6], a13 = a[7];
  const a20 = a[8], a21 = a[9], a22 = a[10], a23 = a[11];
  const a30 = a[12], a31 = a[13], a32 = a[14], a33 = a[15];

  let b0 = b[0], b1 = b[1], b2 = b[2], b3 = b[3];
  out[0] = b0 * a00 + b1 * a10 + b2 * a20 + b3 * a30;
  out[1] = b0 * a01 + b1 * a11 + b2 * a21 + b3 * a31;
  out[2] = b0 * a02 + b1 * a12 + b2 * a22 + b3 * a32;
  out[3] = b0 * a03 + b1 * a13 + b2 * a23 + b3 * a33;

  b0 = b[4]; b1 = b[5]; b2 = b[6]; b3 = b[7];
  out[4] = b0 * a00 + b1 * a10 + b2 * a20 + b3 * a30;
  out[5] = b0 * a01 + b1 * a11 + b2 * a21 + b3 * a31;
  out[6] = b0 * a02 + b1 * a12 + b2 * a22 + b3 * a32;
  out[7] = b0 * a03 + b1 * a13 + b2 * a23 + b3 * a33;

  b0 = b[8]; b1 = b[9]; b2 = b[10]; b3 = b[11];
  out[8] = b0 * a00 + b1 * a10 + b2 * a20 + b3 * a30;
  out[9] = b0 * a01 + b1 * a11 + b2 * a21 + b3 * a31;
  out[10] = b0 * a02 + b1 * a12 + b2 * a22 + b3 * a32;
  out[11] = b0 * a03 + b1 * a13 + b2 * a23 + b3 * a33;

  b0 = b[12]; b1 = b[13]; b2 = b[14]; b3 = b[15];
  out[12] = b0 * a00 + b1 * a10 + b2 * a20 + b3 * a30;
  out[13] = b0 * a01 + b1 * a11 + b2 * a21 + b3 * a31;
  out[14] = b0 * a02 + b1 * a12 + b2 * a22 + b3 * a32;
  out[15] = b0 * a03 + b1 * a13 + b2 * a23 + b3 * a33;
  return out;
}

function mat4Perspective(out: Float32Array, fovy: number, aspect: number, near: number, far: number): Float32Array {
  const f = 1.0 / Math.tan(fovy / 2);
  const nf = 1 / (near - far);
  out[0] = f / aspect;
  out[1] = 0;
  out[2] = 0;
  out[3] = 0;
  out[4] = 0;
  out[5] = f;
  out[6] = 0;
  out[7] = 0;
  out[8] = 0;
  out[9] = 0;
  out[10] = (far + near) * nf;
  out[11] = -1;
  out[12] = 0;
  out[13] = 0;
  out[14] = 2 * far * near * nf;
  out[15] = 0;
  return out;
}

function mat4LookAt(
  out: Float32Array,
  eye: [number, number, number],
  center: [number, number, number],
  up: [number, number, number]
): Float32Array {
  const eyex = eye[0], eyey = eye[1], eyez = eye[2];
  const upx = up[0], upy = up[1], upz = up[2];
  const centerx = center[0], centery = center[1], centerz = center[2];

  let z0 = eyex - centerx;
  let z1 = eyey - centery;
  let z2 = eyez - centerz;
  let len = 1 / Math.hypot(z0, z1, z2);
  z0 *= len; z1 *= len; z2 *= len;

  let x0 = upy * z2 - upz * z1;
  let x1 = upz * z0 - upx * z2;
  let x2 = upx * z1 - upy * z0;
  len = Math.hypot(x0, x1, x2);
  if (!len) {
    x0 = 0; x1 = 0; x2 = 0;
  } else {
    len = 1 / len;
    x0 *= len; x1 *= len; x2 *= len;
  }

  let y0 = z1 * x2 - z2 * x1;
  let y1 = z2 * x0 - z0 * x2;
  let y2 = z0 * x1 - z1 * x0;
  len = Math.hypot(y0, y1, y2);
  if (!len) {
    y0 = 0; y1 = 0; y2 = 0;
  } else {
    len = 1 / len;
    y0 *= len; y1 *= len; y2 *= len;
  }

  out[0] = x0; out[1] = y0; out[2] = z0; out[3] = 0;
  out[4] = x1; out[5] = y1; out[6] = z1; out[7] = 0;
  out[8] = x2; out[9] = y2; out[10] = z2; out[11] = 0;
  out[12] = -(x0 * eyex + x1 * eyey + x2 * eyez);
  out[13] = -(y0 * eyex + y1 * eyey + y2 * eyez);
  out[14] = -(z0 * eyex + z1 * eyey + z2 * eyez);
  out[15] = 1;
  return out;
}

function mat4RotateX(out: Float32Array, a: Float32Array, rad: number): Float32Array {
  const s = Math.sin(rad);
  const c = Math.cos(rad);
  const a10 = a[4], a11 = a[5], a12 = a[6], a13 = a[7];
  const a20 = a[8], a21 = a[9], a22 = a[10], a23 = a[11];

  if (a !== out) {
    out[0] = a[0]; out[1] = a[1]; out[2] = a[2]; out[3] = a[3];
    out[12] = a[12]; out[13] = a[13]; out[14] = a[14]; out[15] = a[15];
  }

  out[4] = a10 * c + a20 * s;
  out[5] = a11 * c + a21 * s;
  out[6] = a12 * c + a22 * s;
  out[7] = a13 * c + a23 * s;
  out[8] = a20 * c - a10 * s;
  out[9] = a21 * c - a11 * s;
  out[10] = a22 * c - a12 * s;
  out[11] = a23 * c - a13 * s;
  return out;
}

function mat4RotateY(out: Float32Array, a: Float32Array, rad: number): Float32Array {
  const s = Math.sin(rad);
  const c = Math.cos(rad);
  const a00 = a[0], a01 = a[1], a02 = a[2], a03 = a[3];
  const a20 = a[8], a21 = a[9], a22 = a[10], a23 = a[11];

  if (a !== out) {
    out[4] = a[4]; out[5] = a[5]; out[6] = a[6]; out[7] = a[7];
    out[12] = a[12]; out[13] = a[13]; out[14] = a[14]; out[15] = a[15];
  }

  out[0] = a00 * c - a20 * s;
  out[1] = a01 * c - a21 * s;
  out[2] = a02 * c - a22 * s;
  out[3] = a03 * c - a23 * s;
  out[8] = a00 * s + a20 * c;
  out[9] = a01 * s + a21 * c;
  out[10] = a02 * s + a22 * c;
  out[11] = a03 * s + a23 * c;
  return out;
}

function mat4RotateZ(out: Float32Array, a: Float32Array, rad: number): Float32Array {
  const s = Math.sin(rad);
  const c = Math.cos(rad);
  const a00 = a[0], a01 = a[1], a02 = a[2], a03 = a[3];
  const a10 = a[4], a11 = a[5], a12 = a[6], a13 = a[7];

  if (a !== out) {
    out[8] = a[8]; out[9] = a[9]; out[10] = a[10]; out[11] = a[11];
    out[12] = a[12]; out[13] = a[13]; out[14] = a[14]; out[15] = a[15];
  }

  out[0] = a00 * c + a10 * s;
  out[1] = a01 * c + a11 * s;
  out[2] = a02 * c + a12 * s;
  out[3] = a03 * c + a13 * s;
  out[4] = a10 * c - a00 * s;
  out[5] = a11 * c - a01 * s;
  out[6] = a12 * c - a02 * s;
  out[7] = a13 * c - a03 * s;
  return out;
}

function mat4Scale(out: Float32Array, a: Float32Array, s: number): Float32Array {
  out[0] = a[0] * s;
  out[1] = a[1] * s;
  out[2] = a[2] * s;
  out[3] = a[3] * s;
  out[4] = a[4] * s;
  out[5] = a[5] * s;
  out[6] = a[6] * s;
  out[7] = a[7] * s;
  out[8] = a[8] * s;
  out[9] = a[9] * s;
  out[10] = a[10] * s;
  out[11] = a[11] * s;
  out[12] = a[12];
  out[13] = a[13];
  out[14] = a[14];
  out[15] = a[15];
  return out;
}

export class RealEngineRHI {
  private gl: WebGL2RenderingContext | null = null;
  private canvas: HTMLCanvasElement | null = null;
  private program: WebGLProgram | null = null;
  private vao: WebGLVertexArrayObject | null = null;
  private vertexBuffer: WebGLBuffer | null = null;
  private wireframeBuffer: WebGLBuffer | null = null;

  private currentTopology: PrimitiveTopology = PrimitiveTopology.TriangleList;
  private cullMode: CullMode = CullMode.None;
  private blendEnabled = false;
  private autoRotate = true;
  private rotationSpeed = 1.2;
  private scale = 1.0;
  private clearColor: [number, number, number, number] = [0.08, 0.09, 0.11, 1.0];
  private lightingEnabled = 1;
  private colorTheme: ColorTheme = 'spectrum';

  // Euler angles (radians)
  private rotX = 0.35;
  private rotY = 0.55;
  private rotZ = 0.15;
  private manualYaw = 0;
  private manualPitch = 0;

  public passes: RenderGraphPass[] = [
    {
      id: 'pass-0',
      name: 'DepthPrePass',
      reads: [],
      writes: [],
      depthWrite: 'DepthBuffer_Main',
      enabled: true,
      clearColor: [0, 0, 0, 1],
    },
    {
      id: 'pass-1',
      name: 'TestPass',
      reads: [],
      writes: ['Swapchain_Backbuffer_0'],
      depthWrite: 'DepthBuffer_Main',
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
    vertexCount: 36,
    triangles: 12,
    bufferMemoryBytes: 864, // 36 vertices * 24 bytes (vec3 pos + vec3 color)
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
      stencil: true,
      preserveDrawingBuffer: false,
    });

    if (!gl) {
      console.error('WebGL2 is not supported');
      return false;
    }

    this.gl = gl;
    gl.enable(gl.DEPTH_TEST);
    gl.depthFunc(gl.LEQUAL);

    return this.setupPipeline();
  }

  private getThemeColors(): [number, number, number][] {
    switch (this.colorTheme) {
      case 'cyberpunk':
        return [
          [1.0, 0.08, 0.58], // Neon Pink (Front)
          [0.0, 0.95, 0.95], // Electric Cyan (Back)
          [0.2, 1.0, 0.2],   // Acid Green (Top)
          [0.6, 0.1, 1.0],   // Neon Purple (Bottom)
          [1.0, 0.9, 0.0],   // Bright Amber (Right)
          [0.0, 0.4, 1.0],   // Deep Blue (Left)
        ];
      case 'pastel':
        return [
          [0.96, 0.58, 0.58], // Pastel Coral
          [0.98, 0.77, 0.55], // Pastel Peach
          [0.55, 0.88, 0.72], // Mint Green
          [0.58, 0.82, 0.95], // Sky Blue
          [0.72, 0.68, 0.92], // Soft Lavender
          [0.94, 0.62, 0.80], // Soft Rose
        ];
      case 'monochrome':
        return [
          [0.92, 0.93, 0.95], // Platinum White
          [0.72, 0.75, 0.79], // Silver
          [0.55, 0.58, 0.62], // Cool Gray
          [0.40, 0.43, 0.48], // Slate Gray
          [0.30, 0.33, 0.38], // Gunmetal
          [0.20, 0.22, 0.27], // Dark Graphite
        ];
      case 'spectrum':
      default:
        return [
          [0.95, 0.22, 0.28], // Ruby Red (Front)
          [0.98, 0.55, 0.12], // Amber Orange (Back)
          [0.15, 0.85, 0.45], // Emerald Green (Top)
          [0.12, 0.80, 0.90], // Turquoise Cyan (Bottom)
          [0.25, 0.45, 0.98], // Royal Blue (Right)
          [0.85, 0.25, 0.92], // Orchid Magenta (Left)
        ];
    }
  }

  public generateCubeData(): Float32Array {
    const colors = this.getThemeColors();
    const cF = colors[0]; // Front
    const cB = colors[1]; // Back
    const cT = colors[2]; // Top
    const cD = colors[3]; // Bottom
    const cR = colors[4]; // Right
    const cL = colors[5]; // Left

    // 36 vertices: 6 faces * 2 triangles * 3 vertices
    // Layout: Pos3f (x, y, z), Col3f (r, g, b)
    const vertices: number[] = [
      // Front Face (+Z) - Red
      -0.5, -0.5,  0.5,   cF[0], cF[1], cF[2],
       0.5, -0.5,  0.5,   cF[0], cF[1], cF[2],
       0.5,  0.5,  0.5,   cF[0], cF[1], cF[2],
      -0.5, -0.5,  0.5,   cF[0], cF[1], cF[2],
       0.5,  0.5,  0.5,   cF[0], cF[1], cF[2],
      -0.5,  0.5,  0.5,   cF[0], cF[1], cF[2],

      // Back Face (-Z) - Orange
       0.5, -0.5, -0.5,   cB[0], cB[1], cB[2],
      -0.5, -0.5, -0.5,   cB[0], cB[1], cB[2],
      -0.5,  0.5, -0.5,   cB[0], cB[1], cB[2],
       0.5, -0.5, -0.5,   cB[0], cB[1], cB[2],
      -0.5,  0.5, -0.5,   cB[0], cB[1], cB[2],
       0.5,  0.5, -0.5,   cB[0], cB[1], cB[2],

      // Top Face (+Y) - Green
      -0.5,  0.5,  0.5,   cT[0], cT[1], cT[2],
       0.5,  0.5,  0.5,   cT[0], cT[1], cT[2],
       0.5,  0.5, -0.5,   cT[0], cT[1], cT[2],
      -0.5,  0.5,  0.5,   cT[0], cT[1], cT[2],
       0.5,  0.5, -0.5,   cT[0], cT[1], cT[2],
      -0.5,  0.5, -0.5,   cT[0], cT[1], cT[2],

      // Bottom Face (-Y) - Cyan
      -0.5, -0.5, -0.5,   cD[0], cD[1], cD[2],
       0.5, -0.5, -0.5,   cD[0], cD[1], cD[2],
       0.5, -0.5,  0.5,   cD[0], cD[1], cD[2],
      -0.5, -0.5, -0.5,   cD[0], cD[1], cD[2],
       0.5, -0.5,  0.5,   cD[0], cD[1], cD[2],
      -0.5, -0.5,  0.5,   cD[0], cD[1], cD[2],

      // Right Face (+X) - Blue
       0.5, -0.5,  0.5,   cR[0], cR[1], cR[2],
       0.5, -0.5, -0.5,   cR[0], cR[1], cR[2],
       0.5,  0.5, -0.5,   cR[0], cR[1], cR[2],
       0.5, -0.5,  0.5,   cR[0], cR[1], cR[2],
       0.5,  0.5, -0.5,   cR[0], cR[1], cR[2],
       0.5,  0.5,  0.5,   cR[0], cR[1], cR[2],

      // Left Face (-X) - Magenta
      -0.5, -0.5, -0.5,   cL[0], cL[1], cL[2],
      -0.5, -0.5,  0.5,   cL[0], cL[1], cL[2],
      -0.5,  0.5,  0.5,   cL[0], cL[1], cL[2],
      -0.5, -0.5, -0.5,   cL[0], cL[1], cL[2],
      -0.5,  0.5,  0.5,   cL[0], cL[1], cL[2],
      -0.5,  0.5, -0.5,   cL[0], cL[1], cL[2],
    ];

    return new Float32Array(vertices);
  }

  public generateWireframeData(): Float32Array {
    const lines: number[] = [
      // Back face edges
      -0.5, -0.5, -0.5,  1, 1, 1,    0.5, -0.5, -0.5,  1, 1, 1,
       0.5, -0.5, -0.5,  1, 1, 1,    0.5,  0.5, -0.5,  1, 1, 1,
       0.5,  0.5, -0.5,  1, 1, 1,   -0.5,  0.5, -0.5,  1, 1, 1,
      -0.5,  0.5, -0.5,  1, 1, 1,   -0.5, -0.5, -0.5,  1, 1, 1,

      // Front face edges
      -0.5, -0.5,  0.5,  1, 1, 1,    0.5, -0.5,  0.5,  1, 1, 1,
       0.5, -0.5,  0.5,  1, 1, 1,    0.5,  0.5,  0.5,  1, 1, 1,
       0.5,  0.5,  0.5,  1, 1, 1,   -0.5,  0.5,  0.5,  1, 1, 1,
      -0.5,  0.5,  0.5,  1, 1, 1,   -0.5, -0.5,  0.5,  1, 1, 1,

      // Connecting edges
      -0.5, -0.5, -0.5,  1, 1, 1,   -0.5, -0.5,  0.5,  1, 1, 1,
       0.5, -0.5, -0.5,  1, 1, 1,    0.5, -0.5,  0.5,  1, 1, 1,
       0.5,  0.5, -0.5,  1, 1, 1,    0.5,  0.5,  0.5,  1, 1, 1,
      -0.5,  0.5, -0.5,  1, 1, 1,   -0.5,  0.5,  0.5,  1, 1, 1,
    ];
    return new Float32Array(lines);
  }

  public setupPipeline(vertSource?: string, fragSource?: string): boolean {
    if (!this.gl) return false;
    const gl = this.gl;

    const vsCode = vertSource || `#version 300 es
layout (location = 0) in vec3 aPos;
layout (location = 1) in vec3 aColor;

out vec3 ourColor;
out vec3 vPos;

uniform mat4 uMVP;
uniform mat4 uModel;

void main() {
    gl_Position = uMVP * vec4(aPos, 1.0);
    ourColor = aColor;
    vPos = (uModel * vec4(aPos, 1.0)).xyz;
}`;

    const fsCode = fragSource || `#version 300 es
precision highp float;
in vec3 ourColor;
in vec3 vPos;
out vec4 FragColor;

uniform int uLighting;

void main() {
    if (uLighting == 1) {
        vec3 dx = dFdx(vPos);
        vec3 dy = dFdy(vPos);
        vec3 N = normalize(cross(dx, dy));
        vec3 L = normalize(vec3(0.5, 0.8, 1.0));
        float diff = max(dot(N, L), 0.0);
        vec3 col = ourColor * (0.45 + 0.55 * diff);
        FragColor = vec4(col, 1.0);
    } else {
        FragColor = vec4(ourColor, 1.0);
    }
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

    this.uploadBuffers();
    return true;
  }

  public uploadBuffers() {
    if (!this.gl) return;
    const gl = this.gl;

    const vertices = this.generateCubeData();
    const wireframe = this.generateWireframeData();

    if (!this.vao) {
      this.vao = gl.createVertexArray();
    }
    gl.bindVertexArray(this.vao);

    if (!this.vertexBuffer) {
      this.vertexBuffer = gl.createBuffer();
    }
    gl.bindBuffer(gl.ARRAY_BUFFER, this.vertexBuffer);
    gl.bufferData(gl.ARRAY_BUFFER, vertices, gl.DYNAMIC_DRAW);

    // Stride = 6 floats * 4 bytes = 24 bytes
    const stride = 6 * Float32Array.BYTES_PER_ELEMENT;

    // Location 0: aPos (vec3)
    gl.enableVertexAttribArray(0);
    gl.vertexAttribPointer(0, 3, gl.FLOAT, false, stride, 0);

    // Location 1: aColor (vec3)
    gl.enableVertexAttribArray(1);
    gl.vertexAttribPointer(1, 3, gl.FLOAT, false, stride, 3 * Float32Array.BYTES_PER_ELEMENT);

    gl.bindVertexArray(null);

    // Wireframe buffer
    if (!this.wireframeBuffer) {
      this.wireframeBuffer = gl.createBuffer();
    }
    gl.bindBuffer(gl.ARRAY_BUFFER, this.wireframeBuffer);
    gl.bufferData(gl.ARRAY_BUFFER, wireframe, gl.STATIC_DRAW);
    gl.bindBuffer(gl.ARRAY_BUFFER, null);

    this.stats.vertexCount = 36;
    this.stats.triangles = 12;
    this.stats.bufferMemoryBytes = vertices.byteLength;
  }

  public setColorTheme(theme: ColorTheme) {
    this.colorTheme = theme;
    this.uploadBuffers();
  }

  public setLightingEnabled(enabled: boolean) {
    this.lightingEnabled = enabled ? 1 : 0;
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

  public setScale(scale: number) {
    this.scale = scale;
  }

  public setClearColor(r: number, g: number, b: number, a = 1.0) {
    this.clearColor = [r, g, b, a];
  }

  public addManualRotation(deltaX: number, deltaY: number) {
    this.manualYaw += deltaX * 0.008;
    this.manualPitch += deltaY * 0.008;
    // Limit pitch to prevent gimbal flip
    this.manualPitch = Math.max(-Math.PI / 2.1, Math.min(Math.PI / 2.1, this.manualPitch));
  }

  public resetRotation() {
    this.rotX = 0.35;
    this.rotY = 0.55;
    this.rotZ = 0.15;
    this.manualYaw = 0;
    this.manualPitch = 0;
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

      if (pass.writes.some((w) => w.includes('Backbuffer'))) {
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

    // Auto-rotation tumbling around 3 axes
    if (this.autoRotate) {
      const step = deltaTime * this.rotationSpeed;
      this.rotX += step * 0.75;
      this.rotY += step * 1.1;
      this.rotZ += step * 0.45;
    }

    // Dynamic canvas resize
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
      gl.frontFace(gl.CCW);
    }

    // Depth test
    gl.enable(gl.DEPTH_TEST);
    gl.depthFunc(gl.LEQUAL);

    // Blend state
    if (this.blendEnabled) {
      gl.enable(gl.BLEND);
      gl.blendFunc(gl.SRC_ALPHA, gl.ONE_MINUS_SRC_ALPHA);
    } else {
      gl.disable(gl.BLEND);
    }

    // Clear color & depth
    gl.clearColor(this.clearColor[0], this.clearColor[1], this.clearColor[2], this.clearColor[3]);
    gl.clear(gl.COLOR_BUFFER_BIT | gl.DEPTH_BUFFER_BIT);

    // Compute Model, View, Projection matrices
    const aspect = Math.max(0.1, this.canvas.width / Math.max(1, this.canvas.height));
    const proj = mat4Create();
    mat4Perspective(proj, (45 * Math.PI) / 180, aspect, 0.1, 100.0);

    const view = mat4Create();
    mat4LookAt(view, [0, 0.2, 3.2], [0, 0, 0], [0, 1, 0]);

    const model = mat4Create();
    mat4Scale(model, model, this.scale);
    mat4RotateX(model, model, this.rotX + this.manualPitch);
    mat4RotateY(model, model, this.rotY + this.manualYaw);
    mat4RotateZ(model, model, this.rotZ);

    const vp = mat4Create();
    mat4Multiply(vp, proj, view);

    const mvp = mat4Create();
    mat4Multiply(mvp, vp, model);

    // Use shader program
    gl.useProgram(this.program);

    const uMVPLoc = gl.getUniformLocation(this.program, 'uMVP');
    const uModelLoc = gl.getUniformLocation(this.program, 'uModel');
    const uLightingLoc = gl.getUniformLocation(this.program, 'uLighting');

    if (uMVPLoc) gl.uniformMatrix4fv(uMVPLoc, false, mvp);
    if (uModelLoc) gl.uniformMatrix4fv(uModelLoc, false, model);
    if (uLightingLoc) gl.uniform1i(uLightingLoc, this.lightingEnabled);

    // Draw cube according to topology
    gl.bindVertexArray(this.vao);

    if (this.currentTopology === PrimitiveTopology.LineList) {
      // Wireframe edge rendering
      gl.bindBuffer(gl.ARRAY_BUFFER, this.wireframeBuffer);
      const stride = 6 * Float32Array.BYTES_PER_ELEMENT;
      gl.vertexAttribPointer(0, 3, gl.FLOAT, false, stride, 0);
      gl.vertexAttribPointer(1, 3, gl.FLOAT, false, stride, 3 * Float32Array.BYTES_PER_ELEMENT);
      gl.drawArrays(gl.LINES, 0, 24);
    } else if (this.currentTopology === PrimitiveTopology.PointList) {
      gl.bindBuffer(gl.ARRAY_BUFFER, this.vertexBuffer);
      const stride = 6 * Float32Array.BYTES_PER_ELEMENT;
      gl.vertexAttribPointer(0, 3, gl.FLOAT, false, stride, 0);
      gl.vertexAttribPointer(1, 3, gl.FLOAT, false, stride, 3 * Float32Array.BYTES_PER_ELEMENT);
      gl.drawArrays(gl.POINTS, 0, 36);
    } else {
      // TriangleList
      gl.bindBuffer(gl.ARRAY_BUFFER, this.vertexBuffer);
      const stride = 6 * Float32Array.BYTES_PER_ELEMENT;
      gl.vertexAttribPointer(0, 3, gl.FLOAT, false, stride, 0);
      gl.vertexAttribPointer(1, 3, gl.FLOAT, false, stride, 3 * Float32Array.BYTES_PER_ELEMENT);
      gl.drawArrays(gl.TRIANGLES, 0, 36);
    }

    gl.bindVertexArray(null);

    this.resolveRenderGraphBarriers();
  }

  public getAutoRotate(): boolean {
    return this.autoRotate;
  }
}

export const engineRHI = new RealEngineRHI();

