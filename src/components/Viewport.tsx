import React, { useEffect, useRef, useState } from 'react';
import {
  RotateCw,
  Sliders,
  Zap,
  Box,
  Palette,
  Sun,
  RotateCcw,
  Sparkles,
} from 'lucide-react';
import { engineRHI, ColorTheme } from '../engine/rhi';
import { fmodEngine } from '../engine/audio';
import { PrimitiveTopology, CullMode } from '../types/rhi';

interface ViewportProps {
  backend: 'Vulkan' | 'OpenGL';
  isPaused: boolean;
  onStatsUpdate: (stats: typeof engineRHI.stats) => void;
  onLogMessage: (msg: string, level?: 'INF' | 'VRB' | 'DBG' | 'WRN') => void;
}

export const Viewport: React.FC<ViewportProps> = ({
  backend,
  isPaused,
  onStatsUpdate,
  onLogMessage,
}) => {
  const canvasRef = useRef<HTMLCanvasElement | null>(null);
  const [autoRotate, setAutoRotate] = useState(true);
  const [rotationSpeed, setSpeed] = useState(1.2);
  const [scale, setScale] = useState(1.15);
  const [topology, setTopology] = useState<PrimitiveTopology>(PrimitiveTopology.TriangleList);
  const [cullMode, setCullMode] = useState<CullMode>(CullMode.None);
  const [blendEnabled, setBlendEnabled] = useState(false);
  const [lighting, setLighting] = useState(true);
  const [colorTheme, setColorTheme] = useState<ColorTheme>('spectrum');
  const [clearColorHex, setClearColorHex] = useState('#141820');

  // Drag-to-orbit state
  const isDraggingRef = useRef(false);
  const lastMousePos = useRef({ x: 0, y: 0 });
  const didDragRef = useRef(false);

  useEffect(() => {
    if (!canvasRef.current) return;
    const ok = engineRHI.init(canvasRef.current);
    if (ok) {
      onLogMessage(`GraphicsDevice initialized with ${backend} RHI backend`, 'INF');
      onLogMessage('VulkanPipelinePool created PSO: testPipeline (Topology: TriangleList, DepthTest: Enabled)', 'DBG');
      onLogMessage('VulkanBufferPool allocated CubeVertexBuffer (864 bytes, 36 vertices)', 'DBG');
    }
  }, [backend]);

  // Main animation frame loop
  useEffect(() => {
    let animationFrameId: number;
    let lastTime = performance.now();

    const loop = (time: number) => {
      const delta = (time - lastTime) / 1000;
      lastTime = time;

      if (!isPaused) {
        engineRHI.render(delta);
        onStatsUpdate({ ...engineRHI.stats });
      }

      animationFrameId = requestAnimationFrame(loop);
    };

    animationFrameId = requestAnimationFrame(loop);
    return () => cancelAnimationFrame(animationFrameId);
  }, [isPaused]);

  const handlePointerDown = (e: React.PointerEvent<HTMLCanvasElement>) => {
    isDraggingRef.current = true;
    didDragRef.current = false;
    lastMousePos.current = { x: e.clientX, y: e.clientY };
    (e.target as HTMLElement).setPointerCapture(e.pointerId);
  };

  const handlePointerMove = (e: React.PointerEvent<HTMLCanvasElement>) => {
    if (!isDraggingRef.current) return;
    const dx = e.clientX - lastMousePos.current.x;
    const dy = e.clientY - lastMousePos.current.y;
    if (Math.abs(dx) > 2 || Math.abs(dy) > 2) {
      didDragRef.current = true;
    }
    lastMousePos.current = { x: e.clientX, y: e.clientY };
    engineRHI.addManualRotation(dx, dy);
  };

  const handlePointerUp = (e: React.PointerEvent<HTMLCanvasElement>) => {
    isDraggingRef.current = false;
    (e.target as HTMLElement).releasePointerCapture(e.pointerId);
    if (!didDragRef.current) {
      // Click without dragging triggers FMOD sound
      fmodEngine.playSoundEvent('triangle_hit');
      onLogMessage('FMOD Event triggered: event:/sfx/cube_hit', 'VRB');
    }
  };

  const handleTopologyChange = (top: PrimitiveTopology) => {
    setTopology(top);
    engineRHI.setTopology(top);
    fmodEngine.playSoundEvent('click');
    onLogMessage(`Pipeline re-bound with PrimitiveTopology: ${top}`, 'DBG');
  };

  const handleCullChange = (mode: CullMode) => {
    setCullMode(mode);
    engineRHI.setCullMode(mode);
    fmodEngine.playSoundEvent('click');
    onLogMessage(`RasterState.CullMode changed to: ${mode}`, 'DBG');
  };

  const handleBlendToggle = () => {
    const next = !blendEnabled;
    setBlendEnabled(next);
    engineRHI.setBlend(next);
    fmodEngine.playSoundEvent('click');
    onLogMessage(`BlendState.Enabled toggled to: ${next}`, 'DBG');
  };

  const handleLightingToggle = () => {
    const next = !lighting;
    setLighting(next);
    engineRHI.setLightingEnabled(next);
    fmodEngine.playSoundEvent('click');
    onLogMessage(`Shader uniform uLighting toggled: ${next ? 'Directional Shading' : 'Vivid Flat'}`, 'DBG');
  };

  const handleThemeChange = (theme: ColorTheme) => {
    setColorTheme(theme);
    engineRHI.setColorTheme(theme);
    fmodEngine.playSoundEvent('click');
    onLogMessage(`Cube vertex colors updated: palette '${theme}' re-uploaded to VBO`, 'INF');
  };

  const handleAutoRotateToggle = () => {
    const next = !autoRotate;
    setAutoRotate(next);
    engineRHI.setAutoRotate(next);
    fmodEngine.playSoundEvent('click');
  };

  const handleResetRotation = () => {
    engineRHI.resetRotation();
    fmodEngine.playSoundEvent('click');
    onLogMessage('Camera / Cube orientation reset to standard isometric view', 'VRB');
  };

  const handleSpeedChange = (val: number) => {
    setSpeed(val);
    engineRHI.setRotationSpeed(val);
  };

  const handleScaleChange = (val: number) => {
    setScale(val);
    engineRHI.setScale(val);
  };

  const handleClearColorChange = (hex: string) => {
    setClearColorHex(hex);
    const r = parseInt(hex.slice(1, 3), 16) / 255;
    const g = parseInt(hex.slice(3, 5), 16) / 255;
    const b = parseInt(hex.slice(5, 7), 16) / 255;
    engineRHI.setClearColor(r, g, b, 1.0);
  };

  return (
    <div className="flex-1 flex flex-col md:flex-row h-full overflow-hidden bg-neutral-950">
      {/* Canvas Viewport Area */}
      <div className="relative flex-1 flex items-center justify-center bg-neutral-950 p-3 overflow-hidden">
        <div className="relative w-full h-full rounded-xl overflow-hidden border border-neutral-800 bg-neutral-900/50 shadow-2xl flex items-center justify-center">
          <canvas
            ref={canvasRef}
            onPointerDown={handlePointerDown}
            onPointerMove={handlePointerMove}
            onPointerUp={handlePointerUp}
            className="w-full h-full cursor-grab active:cursor-grabbing select-none touch-none"
            title="Drag with mouse to orbit the 3D cube. Click to trigger FMOD audio event."
          />

          {/* Viewport Overlay HUD (Top-Left) */}
          <div className="absolute top-3 left-3 bg-neutral-950/80 backdrop-blur-md border border-neutral-800 rounded-lg p-2.5 text-xs font-mono space-y-1 text-neutral-300 pointer-events-none select-none">
            <div className="flex items-center gap-2 text-red-400 font-semibold">
              <Zap className="w-3.5 h-3.5" />
              <span>RealEngine {backend} Swapchain Viewport</span>
            </div>
            <div className="text-[11px] text-neutral-400">
              Mesh: <span className="text-emerald-400 font-semibold">3D Rotating Colored Cube</span> &bull; Vertices:{' '}
              <span className="text-neutral-200">36 (6 Faces, 12 Triangles)</span>
            </div>
            <div className="text-[11px] text-neutral-400">
              Pass: <span className="text-emerald-400 font-semibold">TestPass</span> &bull; Format:{' '}
              <span className="text-neutral-200">B8G8R8A8_UNORM + D24_UNORM_S8</span>
            </div>
            <div className="text-[10px] text-neutral-500 pt-0.5">
              Drag on viewport to orbit cube &bull; Click to trigger FMOD audio
            </div>
          </div>

          {/* Viewport Overlay HUD (Top-Right) */}
          <div className="absolute top-3 right-3 flex items-center gap-1.5 bg-neutral-950/80 backdrop-blur-md border border-neutral-800 rounded-lg p-1.5 text-xs font-mono">
            <button
              onClick={handleResetRotation}
              title="Reset View Orientation"
              className="p-1 rounded text-xs text-neutral-400 hover:text-neutral-200 hover:bg-neutral-800 transition-colors"
            >
              <RotateCcw className="w-3.5 h-3.5" />
            </button>
            <button
              onClick={handleAutoRotateToggle}
              title="Toggle Auto-Rotation"
              className={`p-1 rounded text-xs transition-colors ${
                autoRotate
                  ? 'bg-neutral-800 text-emerald-400'
                  : 'text-neutral-400 hover:text-neutral-200'
              }`}
            >
              <RotateCw className={`w-3.5 h-3.5 ${autoRotate ? 'animate-spin' : ''}`} />
            </button>
            <span className="text-[11px] text-neutral-400 px-1 font-mono">
              Topology: {topology}
            </span>
          </div>
        </div>
      </div>

      {/* RHI Pipeline Controls Sidebar */}
      <aside className="w-full md:w-80 bg-neutral-900 border-t md:border-t-0 md:border-l border-neutral-800 p-4 flex flex-col gap-4 overflow-y-auto select-none">
        <div>
          <h2 className="text-xs font-semibold uppercase tracking-wider text-neutral-400 flex items-center gap-1.5 mb-1">
            <Sliders className="w-3.5 h-3.5 text-red-400" />
            RHI Pipeline Controls
          </h2>
          <p className="text-[11px] text-neutral-500">
            RealEngine 3D rotating colored cube descriptors, rasterizer, and shader controls.
          </p>
        </div>

        {/* Primitive Topology */}
        <div className="space-y-1.5">
          <label className="text-xs font-medium text-neutral-300 flex justify-between">
            <span className="flex items-center gap-1">
              <Box className="w-3 h-3 text-red-400" />
              Primitive Topology
            </span>
            <span className="text-[10px] text-neutral-500 font-mono">PipelineDescriptor</span>
          </label>
          <div className="grid grid-cols-3 gap-1.5">
            {[
              { id: PrimitiveTopology.TriangleList, label: 'Triangles' },
              { id: PrimitiveTopology.LineList, label: 'Wireframe' },
              { id: PrimitiveTopology.PointList, label: 'Points' },
            ].map((item) => (
              <button
                key={item.id}
                onClick={() => handleTopologyChange(item.id)}
                className={`py-1 px-2 rounded text-xs font-medium border transition-colors ${
                  topology === item.id
                    ? 'bg-red-500/20 border-red-500/50 text-red-300'
                    : 'bg-neutral-950 border-neutral-800 text-neutral-400 hover:text-neutral-200'
                }`}
              >
                {item.label}
              </button>
            ))}
          </div>
        </div>

        {/* Color Theme Selector */}
        <div className="space-y-1.5">
          <label className="text-xs font-medium text-neutral-300 flex justify-between">
            <span className="flex items-center gap-1">
              <Palette className="w-3 h-3 text-amber-400" />
              Cube Color Palette
            </span>
            <span className="text-[10px] text-neutral-500 font-mono">VBO Colors</span>
          </label>
          <div className="grid grid-cols-2 gap-1.5">
            {[
              { id: 'spectrum' as ColorTheme, label: 'Spectrum (6-Color)' },
              { id: 'cyberpunk' as ColorTheme, label: 'Cyberpunk Neon' },
              { id: 'pastel' as ColorTheme, label: 'Soft Pastel' },
              { id: 'monochrome' as ColorTheme, label: 'Titanium Steel' },
            ].map((theme) => (
              <button
                key={theme.id}
                onClick={() => handleThemeChange(theme.id)}
                className={`py-1.5 px-2 rounded text-[11px] font-medium border text-left transition-colors ${
                  colorTheme === theme.id
                    ? 'bg-amber-500/20 border-amber-500/50 text-amber-300'
                    : 'bg-neutral-950 border-neutral-800 text-neutral-400 hover:text-neutral-200'
                }`}
              >
                {theme.label}
              </button>
            ))}
          </div>
        </div>

        {/* Shading & Directional Lighting */}
        <div className="flex items-center justify-between p-2.5 rounded-lg bg-neutral-950 border border-neutral-800">
          <div>
            <div className="text-xs font-medium text-neutral-200 flex items-center gap-1.5">
              <Sun className="w-3.5 h-3.5 text-amber-400" />
              Directional Shading
            </div>
            <div className="text-[10px] text-neutral-500">Screen-space derivative normals</div>
          </div>
          <button
            onClick={handleLightingToggle}
            className={`w-9 h-5 rounded-full transition-colors relative ${
              lighting ? 'bg-amber-600' : 'bg-neutral-800'
            }`}
          >
            <div
              className={`w-3.5 h-3.5 rounded-full bg-white transition-transform absolute top-0.5 ${
                lighting ? 'left-4.5' : 'left-1'
              }`}
            />
          </button>
        </div>

        {/* Rasterizer Cull Mode */}
        <div className="space-y-1.5">
          <label className="text-xs font-medium text-neutral-300 flex justify-between">
            <span>Rasterizer Cull Mode</span>
            <span className="text-[10px] text-neutral-500 font-mono">RasterState</span>
          </label>
          <div className="grid grid-cols-3 gap-1.5">
            {[CullMode.None, CullMode.Back, CullMode.Front].map((mode) => (
              <button
                key={mode}
                onClick={() => handleCullChange(mode)}
                className={`py-1 px-2 rounded text-xs font-medium border transition-colors ${
                  cullMode === mode
                    ? 'bg-red-500/20 border-red-500/50 text-red-300'
                    : 'bg-neutral-950 border-neutral-800 text-neutral-400 hover:text-neutral-200'
                }`}
              >
                {mode}
              </button>
            ))}
          </div>
        </div>

        {/* Alpha Blend State */}
        <div className="flex items-center justify-between p-2.5 rounded-lg bg-neutral-950 border border-neutral-800">
          <div>
            <div className="text-xs font-medium text-neutral-200">BlendState.AlphaBlend</div>
            <div className="text-[10px] text-neutral-500">SrcAlpha / OneMinusSrcAlpha</div>
          </div>
          <button
            onClick={handleBlendToggle}
            className={`w-9 h-5 rounded-full transition-colors relative ${
              blendEnabled ? 'bg-red-600' : 'bg-neutral-800'
            }`}
          >
            <div
              className={`w-3.5 h-3.5 rounded-full bg-white transition-transform absolute top-0.5 ${
                blendEnabled ? 'left-4.5' : 'left-1'
              }`}
            />
          </button>
        </div>

        {/* Transform & Motion Controls */}
        <div className="space-y-3 pt-2 border-t border-neutral-800/80">
          <div className="space-y-1.5">
            <div className="flex justify-between text-xs text-neutral-300">
              <span className="flex items-center gap-1.5">
                <RotateCw className="w-3.5 h-3.5 text-neutral-400" />
                Rotation Speed
              </span>
              <span className="font-mono text-neutral-400">{rotationSpeed.toFixed(1)}x</span>
            </div>
            <input
              type="range"
              min="0"
              max="4"
              step="0.1"
              value={rotationSpeed}
              onChange={(e) => handleSpeedChange(parseFloat(e.target.value))}
              className="w-full accent-red-500 h-1.5 bg-neutral-950 rounded-lg cursor-pointer"
            />
          </div>

          <div className="space-y-1.5">
            <div className="flex justify-between text-xs text-neutral-300">
              <span className="flex items-center gap-1.5">
                <Sparkles className="w-3.5 h-3.5 text-neutral-400" />
                Cube Scale
              </span>
              <span className="font-mono text-neutral-400">{scale.toFixed(2)}</span>
            </div>
            <input
              type="range"
              min="0.5"
              max="2.0"
              step="0.05"
              value={scale}
              onChange={(e) => handleScaleChange(parseFloat(e.target.value))}
              className="w-full accent-red-500 h-1.5 bg-neutral-950 rounded-lg cursor-pointer"
            />
          </div>

          <div className="space-y-1.5">
            <div className="flex justify-between text-xs text-neutral-300">
              <span>Clear Color (Attachment 0)</span>
              <span className="font-mono text-neutral-400">{clearColorHex}</span>
            </div>
            <div className="flex items-center gap-2">
              <input
                type="color"
                value={clearColorHex}
                onChange={(e) => handleClearColorChange(e.target.value)}
                className="w-8 h-8 rounded border border-neutral-700 bg-transparent cursor-pointer"
              />
              <div className="text-[11px] text-neutral-400 font-mono flex-1">
                VkClearColorValue(R, G, B, 1.0)
              </div>
            </div>
          </div>
        </div>

        {/* RHI State Snapshot */}
        <div className="mt-auto pt-3 border-t border-neutral-800/80 space-y-1.5 text-[11px] font-mono text-neutral-400">
          <div className="flex justify-between">
            <span>Buffer Handle:</span>
            <span className="text-neutral-200">0x0001 (CubeVertexBuffer)</span>
          </div>
          <div className="flex justify-between">
            <span>Pipeline Handle:</span>
            <span className="text-neutral-200">0x0002 (cubePipeline)</span>
          </div>
          <div className="flex justify-between">
            <span>Vertex Layout:</span>
            <span className="text-neutral-200">Pos3f + Col3f (24B Stride)</span>
          </div>
          <div className="flex justify-between">
            <span>Depth/Stencil:</span>
            <span className="text-emerald-400">LEQUAL (Depth Enabled)</span>
          </div>
        </div>
      </aside>
    </div>
  );
};

