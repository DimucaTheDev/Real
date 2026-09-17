import React, { useState } from 'react';
import { Code2, Play, CheckCircle2, AlertCircle, FileCode, Cpu, Layers } from 'lucide-react';
import { engineRHI } from '../engine/rhi';
import { fmodEngine } from '../engine/audio';

interface PipelineEditorProps {
  onLogMessage: (msg: string, level?: 'INF' | 'VRB' | 'DBG' | 'WRN') => void;
}

export const PipelineEditor: React.FC<PipelineEditorProps> = ({ onLogMessage }) => {
  const [activeFile, setActiveFile] = useState<'vert' | 'frag' | 'spirv'>('vert');
  const [vertCode, setVertCode] = useState(`#version 300 es
layout (location = 0) in vec3 aPos;
layout (location = 1) in vec3 aColor;

out vec3 ourColor;
out vec3 vPos;

// Shader Uniforms (Matrix4x4, float, float)
uniform mat4 uMVP;
uniform mat4 uModel;
uniform float uTime;
uniform float uAspect;

void main() {
    if (uMVP[3][3] != 0.0) {
        gl_Position = uMVP * vec4(aPos, 1.0);
    } else {
        float t = uTime;
        float cy = cos(t * 1.1), sy = sin(t * 1.1);
        float cx = cos(t * 0.75), sx = sin(t * 0.75);
        mat3 rotY = mat3(cy, 0.0, sy, 0.0, 1.0, 0.0, -sy, 0.0, cy);
        mat3 rotX = mat3(1.0, 0.0, 0.0, 0.0, cx, -sx, 0.0, sx, cx);
        vec3 rotated = rotX * (rotY * (aPos * 0.7));
        float zDist = rotated.z * 0.4 + 1.8;
        float aspect = (uAspect > 0.01) ? uAspect : 1.0;
        gl_Position = vec4(rotated.x / (zDist * aspect), rotated.y / zDist, rotated.z * 0.2 + 0.5, 1.0);
    }
    ourColor = aColor;
    vPos = aPos;
}`);

  const [fragCode, setFragCode] = useState(`#version 300 es
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
}`);

  const [compileStatus, setCompileStatus] = useState<'idle' | 'success' | 'error'>('idle');
  const [errorMessage, setErrorMessage] = useState('');

  const handleRecompile = () => {
    try {
      const ok = engineRHI.setupPipeline(vertCode, fragCode);
      if (ok) {
        setCompileStatus('success');
        setErrorMessage('');
        fmodEngine.playSoundEvent('pipeline_compile');
        onLogMessage('VulkanPipelinePool: Recompiled PipelineStateObject cubePipeline successfully', 'INF');
      } else {
        setCompileStatus('error');
        setErrorMessage('GLSL compilation or link error. Check console or shader syntax.');
        onLogMessage('VulkanPipelinePool: Failed to compile shader modules', 'WRN');
      }
    } catch (err: unknown) {
      setCompileStatus('error');
      setErrorMessage(String(err));
    }
  };

  const spirvDisassembly = `; SPIR-V Disassembly (RealEngine Vulkan Backend)
; Target: 3D Rotating Colored Cube
; Version: 1.5
; Generator: Khronos Glslang; 11
; Bound: 48
; Schema: 0
               OpCapability Shader
          %1 = OpExtInstImport "GLSL.std.450"
               OpMemoryModel Logical GLSL450
               OpEntryPoint Vertex %main "main" %aPos %aColor %ourColor %vPos %gl_Position
               OpSource GLSL 460
               OpName %main "main"
               OpName %aPos "aPos"
               OpName %aColor "aColor"
               OpName %ourColor "ourColor"
               OpName %vPos "vPos"
               OpName %uMVP "uMVP"
               OpName %uModel "uModel"
               OpDecorate %aPos Location 0
               OpDecorate %aColor Location 1
               OpDecorate %ourColor Location 0
               OpDecorate %vPos Location 1
       %void = OpTypeVoid
          %3 = OpTypeFunction %void
      %float = OpTypeFloat 32
    %v3float = OpTypeVector %float 3
%_ptr_Input_v3float = OpTypePointer Input %v3float
       %aPos = OpVariable %_ptr_Input_v3float Input
     %aColor = OpVariable %_ptr_Input_v3float Input
%_ptr_Output_v3float = OpTypePointer Output %v3float
   %ourColor = OpVariable %_ptr_Output_v3float Output
       %vPos = OpVariable %_ptr_Output_v3float Output
    %v4float = OpTypeVector %float 4
%_ptr_Output_v4float = OpTypePointer Output %v4float
%gl_Position = OpVariable %_ptr_Output_v4float Output
 %mat4v4float = OpTypeMatrix %v4float 4
%_ptr_Uniform_mat4v4float = OpTypePointer Uniform %mat4v4float
       %uMVP = OpVariable %_ptr_Uniform_mat4v4float Uniform
     %uModel = OpVariable %_ptr_Uniform_mat4v4float Uniform`;

  return (
    <div className="flex-1 flex flex-col md:flex-row h-full overflow-hidden bg-neutral-950">
      {/* Code Editor Column */}
      <div className="flex-1 flex flex-col border-b md:border-b-0 md:border-r border-neutral-800">
        {/* Editor Tabs & Actions */}
        <div className="bg-neutral-900 border-b border-neutral-800 px-4 py-2 flex items-center justify-between">
          <div className="flex items-center gap-1.5">
            <button
              onClick={() => setActiveFile('vert')}
              className={`flex items-center gap-1.5 px-3 py-1 rounded text-xs font-mono transition-colors ${
                activeFile === 'vert'
                  ? 'bg-neutral-800 text-neutral-100 border border-neutral-700 font-medium'
                  : 'text-neutral-400 hover:text-neutral-200'
              }`}
            >
              <FileCode className="w-3.5 h-3.5 text-red-400" />
              triangle.vert (GLSL/Vulkan)
            </button>
            <button
              onClick={() => setActiveFile('frag')}
              className={`flex items-center gap-1.5 px-3 py-1 rounded text-xs font-mono transition-colors ${
                activeFile === 'frag'
                  ? 'bg-neutral-800 text-neutral-100 border border-neutral-700 font-medium'
                  : 'text-neutral-400 hover:text-neutral-200'
              }`}
            >
              <FileCode className="w-3.5 h-3.5 text-blue-400" />
              triangle.frag (GLSL/Vulkan)
            </button>
            <button
              onClick={() => setActiveFile('spirv')}
              className={`flex items-center gap-1.5 px-3 py-1 rounded text-xs font-mono transition-colors ${
                activeFile === 'spirv'
                  ? 'bg-neutral-800 text-neutral-100 border border-neutral-700 font-medium'
                  : 'text-neutral-400 hover:text-neutral-200'
              }`}
            >
              <Cpu className="w-3.5 h-3.5 text-amber-400" />
              main.vert.spv (Bytecode)
            </button>
          </div>

          <button
            onClick={handleRecompile}
            className="flex items-center gap-1.5 bg-red-600 hover:bg-red-500 text-white text-xs px-3 py-1 rounded-md font-medium transition-colors shadow-sm"
          >
            <Play className="w-3.5 h-3.5" />
            Build Pipeline
          </button>
        </div>

        {/* Textarea or Disassembly View */}
        <div className="flex-1 relative p-2 bg-neutral-950 font-mono text-xs overflow-hidden">
          {activeFile === 'vert' && (
            <textarea
              value={vertCode}
              onChange={(e) => setVertCode(e.target.value)}
              className="w-full h-full bg-neutral-950 text-neutral-200 p-3 rounded-lg border border-neutral-800/80 focus:outline-none focus:border-red-500 font-mono text-xs resize-none leading-relaxed"
              spellCheck={false}
            />
          )}

          {activeFile === 'frag' && (
            <textarea
              value={fragCode}
              onChange={(e) => setFragCode(e.target.value)}
              className="w-full h-full bg-neutral-950 text-neutral-200 p-3 rounded-lg border border-neutral-800/80 focus:outline-none focus:border-blue-500 font-mono text-xs resize-none leading-relaxed"
              spellCheck={false}
            />
          )}

          {activeFile === 'spirv' && (
            <pre className="w-full h-full bg-neutral-950 text-emerald-400/90 p-3 rounded-lg border border-neutral-800/80 font-mono text-[11px] overflow-auto leading-relaxed select-text">
              {spirvDisassembly}
            </pre>
          )}
        </div>

        {/* Compile Status Feedback Bar */}
        {compileStatus !== 'idle' && (
          <div
            className={`px-4 py-2 text-xs font-mono flex items-center gap-2 border-t ${
              compileStatus === 'success'
                ? 'bg-emerald-950/40 text-emerald-300 border-emerald-800/50'
                : 'bg-red-950/40 text-red-300 border-red-800/50'
            }`}
          >
            {compileStatus === 'success' ? (
              <>
                <CheckCircle2 className="w-4 h-4 text-emerald-400" />
                <span>VkPipeline successfully linked with VulkanDevice</span>
              </>
            ) : (
              <>
                <AlertCircle className="w-4 h-4 text-red-400" />
                <span>{errorMessage}</span>
              </>
            )}
          </div>
        )}
      </div>

      {/* Pipeline Descriptor Specifications */}
      <div className="w-full md:w-80 bg-neutral-900 p-4 flex flex-col gap-4 overflow-y-auto">
        <div>
          <h3 className="text-xs font-semibold uppercase tracking-wider text-neutral-400 flex items-center gap-1.5 mb-1">
            <Layers className="w-3.5 h-3.5 text-red-400" />
            PipelineDescriptor Record
          </h3>
          <p className="text-[11px] text-neutral-500">
            RealEngine RHI immutable pipeline state object definition.
          </p>
        </div>

        <div className="space-y-3 text-xs font-mono">
          <div className="p-3 bg-neutral-950 rounded-lg border border-neutral-800 space-y-2">
            <div className="text-neutral-400 text-[11px] uppercase tracking-wider font-semibold">
              VertexLayout Specification
            </div>
            <div className="flex justify-between">
              <span className="text-neutral-500">Stride:</span>
              <span className="text-neutral-200">24 Bytes (6 Floats)</span>
            </div>
            <div className="flex justify-between">
              <span className="text-neutral-500">Attr 0 (Position):</span>
              <span className="text-neutral-200">Format.Rgb32Float (Loc 0, Offset: 0)</span>
            </div>
            <div className="flex justify-between">
              <span className="text-neutral-500">Attr 1 (Color):</span>
              <span className="text-neutral-200">Format.Rgb32Float (Loc 1, Offset: 12)</span>
            </div>
          </div>

          <div className="p-3 bg-neutral-950 rounded-lg border border-neutral-800 space-y-2">
            <div className="text-neutral-400 text-[11px] uppercase tracking-wider font-semibold">
              Fixed Function States
            </div>
            <div className="flex justify-between">
              <span className="text-neutral-500">Topology:</span>
              <span className="text-emerald-400">PrimitiveTopology.TriangleList</span>
            </div>
            <div className="flex justify-between">
              <span className="text-neutral-500">BlendState:</span>
              <span className="text-neutral-200">BlendState.Opaque</span>
            </div>
            <div className="flex justify-between">
              <span className="text-neutral-500">DepthStencilState:</span>
              <span className="text-emerald-400">DepthTest.LEQUAL (3D Cube)</span>
            </div>
            <div className="flex justify-between">
              <span className="text-neutral-500">Color Attachments:</span>
              <span className="text-neutral-200">[TextureFormat.Bgra8Unorm]</span>
            </div>
          </div>

          <div className="p-3 bg-neutral-950 rounded-lg border border-neutral-800 space-y-2">
            <div className="text-neutral-400 text-[11px] uppercase tracking-wider font-semibold">
              Shader Modules
            </div>
            <div className="flex justify-between">
              <span className="text-neutral-500">Vertex Stage:</span>
              <span className="text-neutral-200">main.vert.spv (1,384 bytes)</span>
            </div>
            <div className="flex justify-between">
              <span className="text-neutral-500">Fragment Stage:</span>
              <span className="text-neutral-200">main.frag.spv (548 bytes)</span>
            </div>
            <div className="flex justify-between">
              <span className="text-neutral-500">Entry Point:</span>
              <span className="text-neutral-200">"main"</span>
            </div>
          </div>

          <div className="p-3 bg-neutral-950 rounded-lg border border-neutral-800 space-y-2">
            <div className="text-neutral-400 text-[11px] uppercase tracking-wider font-semibold flex items-center justify-between">
              <span>Shader Uniform Parameters</span>
              <span className="text-amber-400 text-[10px]">ICommandList</span>
            </div>
            <div className="flex justify-between">
              <span className="text-neutral-500">uMVP (Matrix4x4):</span>
              <span className="text-neutral-200">64B (std140 / PushConstant @ 0)</span>
            </div>
            <div className="flex justify-between">
              <span className="text-neutral-500">uTime (float):</span>
              <span className="text-neutral-200">4B (PushConstant @ 64)</span>
            </div>
            <div className="flex justify-between">
              <span className="text-neutral-500">uAspect (float):</span>
              <span className="text-neutral-200">4B (PushConstant @ 68)</span>
            </div>
            <div className="flex justify-between">
              <span className="text-neutral-500">uResolution (Vector2):</span>
              <span className="text-neutral-200">8B (PushConstant @ 72)</span>
            </div>
            <div className="flex justify-between">
              <span className="text-neutral-500">uLighting (int/bool):</span>
              <span className="text-neutral-200">4B (PushConstant @ 80)</span>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};
