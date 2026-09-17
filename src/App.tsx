import React, { useState } from 'react';
import { Header } from './components/Header';
import { Viewport } from './components/Viewport';
import { RenderGraphView } from './components/RenderGraphView';
import { PipelineEditor } from './components/PipelineEditor';
import { VfsExplorer } from './components/VfsExplorer';
import { MemoryMonitor } from './components/MemoryMonitor';
import { AudioPanel } from './components/AudioPanel';
import { ConsoleLogs } from './components/ConsoleLogs';
import { engineRHI, GpuStats } from './engine/rhi';
import { fmodEngine } from './engine/audio';
import { SerilogEntry } from './types/rhi';

export function App() {
  const [backend, setBackend] = useState<'Vulkan' | 'OpenGL'>('Vulkan');
  const [activeTab, setActiveTab] = useState('viewport');
  const [isPaused, setIsPaused] = useState(false);
  const [isHumming, setIsHumming] = useState(false);
  const [stats, setStats] = useState<GpuStats>({ ...engineRHI.stats });

  const [logs, setLogs] = useState<SerilogEntry[]>([
    {
      id: '1',
      timestamp: '12:00:01',
      level: 'INF',
      source: 'RealEngine.Core',
      message: 'Engine bootstrap initiated on Linux x64 (.NET 11 preview runtime)',
    },
    {
      id: '2',
      timestamp: '12:00:01',
      level: 'INF',
      source: 'Real.Core.Assets.Vfs',
      message: "Registered scheme 'file://' (DiskStorageProvider)",
    },
    {
      id: '3',
      timestamp: '12:00:01',
      level: 'INF',
      source: 'Real.Core.Assets.Vfs',
      message: "Registered scheme 'pak://' (PakStorageProvider)",
    },
    {
      id: '4',
      timestamp: '12:00:01',
      level: 'INF',
      source: 'Real.Core.Assets.Vfs',
      message: "Registered scheme 'assembly://' (AssemblyStorageProvider)",
    },
    {
      id: '5',
      timestamp: '12:00:02',
      level: 'INF',
      source: 'Real.Windowing.Glfw',
      message: 'GlfwWindow created (800x600, GraphicsApi.Vulkan, VSync Enabled)',
    },
    {
      id: '6',
      timestamp: '12:00:02',
      level: 'INF',
      source: 'Real.Graphics.Vulkan',
      message: 'Selected PhysicalDevice: Vulkan 1.3 Generic Adapter (Discrete GPU)',
    },
    {
      id: '7',
      timestamp: '12:00:02',
      level: 'DBG',
      source: 'VulkanLogicalDevice',
      message: 'Created Device with extension: VK_KHR_swapchain',
    },
    {
      id: '8',
      timestamp: '12:00:02',
      level: 'INF',
      source: 'Real.Graphics.Vulkan',
      message: 'VulkanSwapchain initialized: 3 images, format: B8G8R8A8_UNORM, PresentMode: FifoKHR',
    },
    {
      id: '9',
      timestamp: '12:00:03',
      level: 'INF',
      source: 'Real.Fmod',
      message: 'FMOD Studio System initialized with 32 virtual channels (Master.bank mounted)',
    },
  ]);

  const addLogMessage = (message: string, level: 'INF' | 'VRB' | 'DBG' | 'WRN' = 'INF') => {
    const now = new Date();
    const timeStr = `${now.getHours().toString().padStart(2, '0')}:${now
      .getMinutes()
      .toString()
      .padStart(2, '0')}:${now.getSeconds().toString().padStart(2, '0')}`;

    const newLog: SerilogEntry = {
      id: Math.random().toString(36).substring(2, 9),
      timestamp: timeStr,
      level,
      source: backend === 'Vulkan' ? 'Real.Graphics.Vulkan' : 'Real.Graphics.OpenGL',
      message,
    };

    setLogs((prev) => [...prev.slice(-150), newLog]);
  };

  const handleToggleHum = () => {
    const active = fmodEngine.toggleEngineHum();
    setIsHumming(active);
    addLogMessage(`FMOD Studio: Engine ambient hum ${active ? 'started' : 'stopped'}`, 'VRB');
  };

  const handleBackendChange = (newBackend: 'Vulkan' | 'OpenGL') => {
    setBackend(newBackend);
    fmodEngine.playSoundEvent('pipeline_compile');
    addLogMessage(`Graphics backend swapped to: ${newBackend} RHI Provider`, 'INF');
  };

  return (
    <div className="flex flex-col h-screen w-screen overflow-hidden bg-neutral-950 text-neutral-100 font-sans select-none">
      {/* Top Navigation & Status Bar */}
      <Header
        backend={backend}
        onBackendChange={handleBackendChange}
        fps={stats.fps}
        frameTimeMs={stats.frameTimeMs}
        isHumming={isHumming}
        onToggleHum={handleToggleHum}
        isPaused={isPaused}
        onTogglePause={() => setIsPaused(!isPaused)}
        activeTab={activeTab}
        onTabChange={(tab) => {
          setActiveTab(tab);
          fmodEngine.playSoundEvent('click');
        }}
      />

      {/* Main Workspace Area */}
      <main className="flex-1 overflow-hidden flex flex-col relative">
        {activeTab === 'viewport' && (
          <Viewport
            backend={backend}
            isPaused={isPaused}
            onStatsUpdate={setStats}
            onLogMessage={addLogMessage}
          />
        )}

        {activeTab === 'rendergraph' && <RenderGraphView onLogMessage={addLogMessage} />}

        {activeTab === 'pipeline' && <PipelineEditor onLogMessage={addLogMessage} />}

        {activeTab === 'vfs' && <VfsExplorer onLogMessage={addLogMessage} />}

        {activeTab === 'memory' && <MemoryMonitor onLogMessage={addLogMessage} />}

        {activeTab === 'audio' && (
          <AudioPanel
            isHumming={isHumming}
            onToggleHum={handleToggleHum}
            onLogMessage={addLogMessage}
          />
        )}
      </main>

      {/* Bottom Diagnostics Console (Serilog) */}
      <ConsoleLogs logs={logs} onClear={() => setLogs([])} />
    </div>
  );
}

export default App;
