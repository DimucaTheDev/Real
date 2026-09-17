import React from 'react';
import { Cpu, Activity, Play, Pause, Volume2, VolumeX, ShieldCheck, Box, RefreshCw } from 'lucide-react';
import { fmodEngine } from '../engine/audio';

interface HeaderProps {
  backend: 'Vulkan' | 'OpenGL';
  onBackendChange: (backend: 'Vulkan' | 'OpenGL') => void;
  fps: number;
  frameTimeMs: number;
  isHumming: boolean;
  onToggleHum: () => void;
  isPaused: boolean;
  onTogglePause: () => void;
  activeTab: string;
  onTabChange: (tab: string) => void;
}

export const Header: React.FC<HeaderProps> = ({
  backend,
  onBackendChange,
  fps,
  frameTimeMs,
  isHumming,
  onToggleHum,
  isPaused,
  onTogglePause,
  activeTab,
  onTabChange,
}) => {
  const tabs = [
    { id: 'viewport', label: 'Viewport & RHI' },
    { id: 'rendergraph', label: 'Render Graph' },
    { id: 'pipeline', label: 'Pipeline & Shaders' },
    { id: 'vfs', label: 'VFS Storage' },
    { id: 'memory', label: 'Vulkan Memory' },
    { id: 'audio', label: 'FMOD Studio' },
  ];

  return (
    <header className="bg-neutral-900 border-b border-neutral-800 px-4 py-2.5 flex flex-wrap items-center justify-between gap-3 select-none">
      {/* Brand & Engine Meta */}
      <div className="flex items-center gap-3">
        <div className="w-8 h-8 rounded-lg bg-red-500/20 border border-red-500/40 flex items-center justify-center text-red-400 font-bold text-lg shadow-sm">
          R
        </div>
        <div>
          <div className="flex items-center gap-2">
            <h1 className="text-sm font-semibold tracking-wide text-neutral-100 uppercase">RealEngine</h1>
            <span className="text-[10px] px-1.5 py-0.5 rounded bg-neutral-800 text-neutral-400 font-mono border border-neutral-700">
              v1.0.0-preview (.NET 11)
            </span>
          </div>
          <p className="text-[11px] text-neutral-400 flex items-center gap-1.5">
            <span className="inline-block w-1.5 h-1.5 rounded-full bg-emerald-500 animate-pulse"></span>
            RHI RenderGraph &bull; Silk.NET Vulkan &bull; FMOD Studio
          </p>
        </div>
      </div>

      {/* Navigation Tabs */}
      <nav className="flex items-center bg-neutral-950 p-1 rounded-lg border border-neutral-800/80">
        {tabs.map((tab) => (
          <button
            key={tab.id}
            onClick={() => onTabChange(tab.id)}
            className={`px-3 py-1 text-xs font-medium rounded-md transition-colors ${
              activeTab === tab.id
                ? 'bg-neutral-800 text-neutral-100 shadow-sm'
                : 'text-neutral-400 hover:text-neutral-200 hover:bg-neutral-900/50'
            }`}
          >
            {tab.label}
          </button>
        ))}
      </nav>

      {/* Engine Metrics & Quick Controls */}
      <div className="flex items-center gap-2.5">
        {/* Backend Switcher */}
        <div className="flex items-center bg-neutral-950 p-0.5 rounded-md border border-neutral-800 text-xs">
          <button
            onClick={() => onBackendChange('Vulkan')}
            className={`px-2 py-0.5 rounded text-[11px] font-mono transition-colors ${
              backend === 'Vulkan'
                ? 'bg-red-950/80 text-red-300 border border-red-800/60 font-semibold'
                : 'text-neutral-400 hover:text-neutral-200'
            }`}
          >
            Vulkan 1.3
          </button>
          <button
            onClick={() => onBackendChange('OpenGL')}
            className={`px-2 py-0.5 rounded text-[11px] font-mono transition-colors ${
              backend === 'OpenGL'
                ? 'bg-blue-950/80 text-blue-300 border border-blue-800/60 font-semibold'
                : 'text-neutral-400 hover:text-neutral-200'
            }`}
          >
            OpenGL 4.6
          </button>
        </div>

        {/* Performance Counters */}
        <div className="flex items-center gap-2 px-2.5 py-1 rounded-md bg-neutral-950 border border-neutral-800 text-xs font-mono">
          <div className="flex items-center gap-1 text-emerald-400">
            <Activity className="w-3.5 h-3.5" />
            <span>{fps} FPS</span>
          </div>
          <span className="text-neutral-600">|</span>
          <span className="text-neutral-400">{frameTimeMs} ms</span>
        </div>

        {/* Hum / Audio Toggle */}
        <button
          onClick={onToggleHum}
          title="Toggle FMOD Engine Ambient Hum"
          className={`p-1.5 rounded-md border text-xs transition-colors ${
            isHumming
              ? 'bg-amber-950/60 border-amber-800/60 text-amber-400'
              : 'bg-neutral-950 border-neutral-800 text-neutral-400 hover:text-neutral-200'
          }`}
        >
          {isHumming ? <Volume2 className="w-4 h-4" /> : <VolumeX className="w-4 h-4" />}
        </button>

        {/* Pause/Resume Render Loop */}
        <button
          onClick={onTogglePause}
          title={isPaused ? 'Resume Render Loop' : 'Pause Render Loop'}
          className={`p-1.5 rounded-md border text-xs transition-colors ${
            isPaused
              ? 'bg-red-950/60 border-red-800/60 text-red-400'
              : 'bg-neutral-950 border-neutral-800 text-emerald-400 hover:bg-neutral-800'
          }`}
        >
          {isPaused ? <Play className="w-4 h-4" /> : <Pause className="w-4 h-4" />}
        </button>
      </div>
    </header>
  );
};
