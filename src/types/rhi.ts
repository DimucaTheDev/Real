export enum BufferUsage {
  Vertex = 'Vertex',
  Index = 'Index',
  Uniform = 'Uniform',
  Storage = 'Storage',
  TransferSrc = 'TransferSrc',
  TransferDst = 'TransferDst',
}

export enum PrimitiveTopology {
  TriangleList = 'TriangleList',
  TriangleStrip = 'TriangleStrip',
  LineList = 'LineList',
  LineStrip = 'LineStrip',
  PointList = 'PointList',
}

export enum CullMode {
  None = 'None',
  Front = 'Front',
  Back = 'Back',
  FrontAndBack = 'FrontAndBack',
}

export enum TextureFormat {
  R8Unorm = 'R8Unorm',
  Rg8Unorm = 'Rg8Unorm',
  Rgb8Unorm = 'Rgb8Unorm',
  Rgba8Unorm = 'Rgba8Unorm',
  Bgra8Unorm = 'Bgra8Unorm',
  Depth24Stencil8 = 'Depth24Stencil8',
  Depth32Float = 'Depth32Float',
}

export enum ShaderStage {
  Vertex = 'Vertex',
  Fragment = 'Fragment',
  Compute = 'Compute',
}

export enum ImageLayout {
  Undefined = 'VK_IMAGE_LAYOUT_UNDEFINED',
  General = 'VK_IMAGE_LAYOUT_GENERAL',
  ColorAttachmentOptimal = 'VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL',
  DepthStencilAttachmentOptimal = 'VK_IMAGE_LAYOUT_DEPTH_STENCIL_ATTACHMENT_OPTIMAL',
  ShaderReadOnlyOptimal = 'VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL',
  TransferSrcOptimal = 'VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL',
  TransferDstOptimal = 'VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL',
  PresentSrcKhr = 'VK_IMAGE_LAYOUT_PRESENT_SRC_KHR',
}

export enum BlendFactor {
  Zero = 'Zero',
  One = 'One',
  SrcAlpha = 'SrcAlpha',
  OneMinusSrcAlpha = 'OneMinusSrcAlpha',
  DstAlpha = 'DstAlpha',
  OneMinusDstAlpha = 'OneMinusDstAlpha',
}

export interface BlendState {
  enabled: boolean;
  srcColorBlendFactor: BlendFactor;
  dstColorBlendFactor: BlendFactor;
  srcAlphaBlendFactor: BlendFactor;
  dstAlphaBlendFactor: BlendFactor;
}

export interface RasterState {
  cullMode: CullMode;
  frontFaceClockwise: boolean;
  polygonMode: 'Fill' | 'Line' | 'Point';
  lineWidth: number;
}

export interface DepthStencilState {
  depthTestEnable: boolean;
  depthWriteEnable: boolean;
  compareOp: 'Less' | 'LessOrEqual' | 'Greater' | 'Always';
}

export interface VertexAttribute {
  location: number;
  format: TextureFormat;
  offset: number;
}

export interface VertexLayout {
  stride: number;
  attributes: VertexAttribute[];
}

export interface PipelineDescriptor {
  debugName: string;
  vertexShader: string;
  fragmentShader: string;
  vertexLayout: VertexLayout;
  topology: PrimitiveTopology;
  blend: BlendState;
  raster: RasterState;
  depthStencil: DepthStencilState;
  colorAttachmentFormats: TextureFormat[];
  depthAttachmentFormat?: TextureFormat | null;
}

export interface RenderGraphPass {
  id: string;
  name: string;
  reads: string[];
  writes: string[];
  depthWrite?: string;
  enabled: boolean;
  clearColor: [number, number, number, number];
}

export interface BarrierTransition {
  resource: string;
  oldLayout: ImageLayout;
  newLayout: ImageLayout;
  srcStage: string;
  dstStage: string;
  srcAccess: string;
  dstAccess: string;
}

export interface SerilogEntry {
  id: string;
  timestamp: string;
  level: 'VRB' | 'DBG' | 'INF' | 'WRN' | 'ERR';
  message: string;
  source: string;
}
