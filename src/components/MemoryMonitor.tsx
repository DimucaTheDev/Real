import React, { useState } from 'react';
import { HardDrive, Cpu, ShieldCheck, Database, Zap, RefreshCw } from 'lucide-react';
import { fmodEngine } from '../engine/audio';

interface MemoryMonitorProps {
  onLogMessage: (msg: string, level?: 'INF' | 'VRB' | 'DBG' | 'WRN') => void;
}

export const MemoryMonitor: React.FC<MemoryMonitorProps> = ({ onLogMessage }) => {
  const [fencesSignaled, setFencesSignaled] = useState([true, false, false]);

  const toggleFence = (idx: number) => {
    const updated = [...fencesSignaled];
    updated[idx] = !updated[idx];
    setFencesSignaled(updated);
    fmodEngine.playSoundEvent('fence_signal');
    onLogMessage(`VkFence[${idx}] state changed to: ${updated[idx] ? 'SIGNALED' : 'UNSIGNALED'}`, 'VRB');
  };

  return (
    <div className="flex-1 p-4 md:p-6 overflow-y-auto bg-neutral-950 space-y-6">
      <div className="flex items-center justify-between border-b border-neutral-800 pb-4">
        <div>
          <h2 className="text-sm font-semibold text-neutral-100 flex items-center gap-2">
            <Database className="w-4 h-4 text-red-400" />
            Vulkan Memory Allocator & Resource Pools
          </h2>
          <p className="text-xs text-neutral-400">
            Silk.NET Vulkan physical device memory heap metrics, pool occupancy, and frame sync fences.
          </p>
        </div>

        <button
          onClick={() => {
            setFencesSignaled([true, true, true]);
            fmodEngine.playSoundEvent('fence_signal');
            onLogMessage('vkResetFences & vkWaitForFences idle sync complete', 'INF');
          }}
          className="flex items-center gap-1.5 bg-neutral-800 hover:bg-neutral-700 text-neutral-200 text-xs px-3 py-1.5 rounded-md font-mono transition-colors border border-neutral-700"
        >
          <RefreshCw className="w-3.5 h-3.5 text-emerald-400" />
          WaitIdle & Reset Fences
        </button>
      </div>

      {/* Memory Heaps Grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        {/* Device Local Heap */}
        <div className="p-4 rounded-xl bg-neutral-900 border border-neutral-800 space-y-3">
          <div className="flex items-center justify-between">
            <div className="text-xs font-semibold text-neutral-200 flex items-center gap-2">
              <Zap className="w-3.5 h-3.5 text-red-400" />
              Heap 0: Device Local (VRAM)
            </div>
            <span className="text-[10px] px-2 py-0.5 rounded bg-red-950/60 border border-red-800/60 text-red-300 font-mono">
              VK_MEMORY_PROPERTY_DEVICE_LOCAL_BIT
            </span>
          </div>

          <div className="space-y-1">
            <div className="flex justify-between text-xs font-mono text-neutral-400">
              <span>Allocated / Budget</span>
              <span className="text-neutral-200 font-semibold">128 MB / 8,192 MB (1.5%)</span>
            </div>
            <div className="w-full h-2 bg-neutral-950 rounded-full overflow-hidden border border-neutral-800">
              <div className="h-full bg-red-500 rounded-full w-[1.5%]" />
            </div>
          </div>

          <div className="grid grid-cols-2 gap-2 text-[11px] font-mono pt-2 border-t border-neutral-800/80 text-neutral-400">
            <div>
              <span className="text-neutral-500">Render Targets:</span> 3 Swapchain Images
            </div>
            <div>
              <span className="text-neutral-500">Depth Buffer:</span> 1 (Depth24Stencil8)
            </div>
          </div>
        </div>

        {/* Host Visible / Coherent Heap */}
        <div className="p-4 rounded-xl bg-neutral-900 border border-neutral-800 space-y-3">
          <div className="flex items-center justify-between">
            <div className="text-xs font-semibold text-neutral-200 flex items-center gap-2">
              <Cpu className="w-3.5 h-3.5 text-blue-400" />
              Heap 1: Host Visible & Coherent (RAM)
            </div>
            <span className="text-[10px] px-2 py-0.5 rounded bg-blue-950/60 border border-blue-800/60 text-blue-300 font-mono">
              HOST_VISIBLE | HOST_COHERENT
            </span>
          </div>

          <div className="space-y-1">
            <div className="flex justify-between text-xs font-mono text-neutral-400">
              <span>Allocated / Budget</span>
              <span className="text-neutral-200 font-semibold">16.4 MB / 4,096 MB (0.4%)</span>
            </div>
            <div className="w-full h-2 bg-neutral-950 rounded-full overflow-hidden border border-neutral-800">
              <div className="h-full bg-blue-500 rounded-full w-[0.4%]" />
            </div>
          </div>

          <div className="grid grid-cols-2 gap-2 text-[11px] font-mono pt-2 border-t border-neutral-800/80 text-neutral-400">
            <div>
              <span className="text-neutral-500">Vertex Buffers:</span> 1 (TriangleVBO 60 B)
            </div>
            <div>
              <span className="text-neutral-500">Uniform Buffers:</span> 2 Active Staging
            </div>
          </div>
        </div>
      </div>

      {/* Pools & Allocators Breakdown */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <div className="p-4 rounded-xl bg-neutral-900 border border-neutral-800 space-y-2 font-mono text-xs">
          <div className="text-neutral-200 font-semibold flex items-center gap-2">
            <HardDrive className="w-3.5 h-3.5 text-amber-400" />
            VulkanBufferPool
          </div>
          <div className="text-[11px] text-neutral-500">
            Reusable chunk pools for Vertex, Index, and Staging buffers.
          </div>
          <div className="pt-2 border-t border-neutral-800 space-y-1 text-neutral-400">
            <div className="flex justify-between">
              <span>Active Buffers:</span>
              <span className="text-neutral-200">1</span>
            </div>
            <div className="flex justify-between">
              <span>Pool Capacity:</span>
              <span className="text-neutral-200">64 Slots</span>
            </div>
            <div className="flex justify-between">
              <span>Alignment:</span>
              <span className="text-neutral-200">256 bytes</span>
            </div>
          </div>
        </div>

        <div className="p-4 rounded-xl bg-neutral-900 border border-neutral-800 space-y-2 font-mono text-xs">
          <div className="text-neutral-200 font-semibold flex items-center gap-2">
            <ShieldCheck className="w-3.5 h-3.5 text-emerald-400" />
            VulkanPipelinePool
          </div>
          <div className="text-[11px] text-neutral-500">
            Cached VkPipeline state objects hashed by PipelineDescriptor.
          </div>
          <div className="pt-2 border-t border-neutral-800 space-y-1 text-neutral-400">
            <div className="flex justify-between">
              <span>Cached PSOs:</span>
              <span className="text-neutral-200">1 (testPipeline)</span>
            </div>
            <div className="flex justify-between">
              <span>Cache Hits:</span>
              <span className="text-emerald-400">1,842</span>
            </div>
            <div className="flex justify-between">
              <span>Cache Misses:</span>
              <span className="text-neutral-200">1</span>
            </div>
          </div>
        </div>

        <div className="p-4 rounded-xl bg-neutral-900 border border-neutral-800 space-y-2 font-mono text-xs">
          <div className="text-neutral-200 font-semibold flex items-center gap-2">
            <Zap className="w-3.5 h-3.5 text-purple-400" />
            VulkanDescriptorAllocator
          </div>
          <div className="text-[11px] text-neutral-500">
            Per-frame linear allocator resetting on FrameSyncContext swap.
          </div>
          <div className="pt-2 border-t border-neutral-800 space-y-1 text-neutral-400">
            <div className="flex justify-between">
              <span>Allocated Sets:</span>
              <span className="text-neutral-200">3 per Frame</span>
            </div>
            <div className="flex justify-between">
              <span>Pool Size:</span>
              <span className="text-neutral-200">1,024 Descriptors</span>
            </div>
            <div className="flex justify-between">
              <span>Current Frame:</span>
              <span className="text-neutral-200">FrameIndex: 0</span>
            </div>
          </div>
        </div>
      </div>

      {/* Frame Synchronization Context */}
      <div className="p-4 rounded-xl bg-neutral-900 border border-neutral-800 space-y-3">
        <div className="text-xs font-semibold text-neutral-200 flex items-center gap-2">
          <ShieldCheck className="w-3.5 h-3.5 text-emerald-400" />
          FrameSyncContext Synchronization Primitives
        </div>
        <p className="text-xs text-neutral-400">
          Triple-buffering sync using VkFence and VkSemaphore to prevent CPU-GPU race conditions.
        </p>

        <div className="grid grid-cols-1 md:grid-cols-3 gap-3 pt-2">
          {fencesSignaled.map((signaled, idx) => (
            <div
              key={idx}
              onClick={() => toggleFence(idx)}
              className="p-3 rounded-lg bg-neutral-950 border border-neutral-800 cursor-pointer hover:border-neutral-700 transition-colors font-mono text-xs space-y-1.5"
            >
              <div className="flex items-center justify-between">
                <span className="text-neutral-300 font-semibold">Frame In-Flight {idx}</span>
                <span
                  className={`text-[10px] px-1.5 py-0.5 rounded font-bold ${
                    signaled
                      ? 'bg-emerald-950 text-emerald-300 border border-emerald-800/60'
                      : 'bg-amber-950 text-amber-300 border border-amber-800/60'
                  }`}
                >
                  {signaled ? 'FENCE_SIGNALED' : 'WAITING_ON_GPU'}
                </span>
              </div>
              <div className="text-[11px] text-neutral-500">
                Semaphore: <span className="text-neutral-400">ImageAvailable[{idx}]</span>
              </div>
              <div className="text-[10px] text-neutral-600">Click to toggle fence signal</div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
};
