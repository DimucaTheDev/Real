import React, { useState } from 'react';
import { Layers, ArrowRight, ShieldAlert, GitCommit, CheckCircle2, Plus, Trash2, ArrowDown } from 'lucide-react';
import { engineRHI } from '../engine/rhi';
import { RenderGraphPass } from '../types/rhi';
import { fmodEngine } from '../engine/audio';

interface RenderGraphViewProps {
  onLogMessage: (msg: string, level?: 'INF' | 'VRB' | 'DBG' | 'WRN') => void;
}

export const RenderGraphView: React.FC<RenderGraphViewProps> = ({ onLogMessage }) => {
  const [passes, setPasses] = useState<RenderGraphPass[]>(engineRHI.passes);
  const [selectedPass, setSelectedPass] = useState<RenderGraphPass>(engineRHI.passes[1]);
  const [newPassName, setNewPassName] = useState('');

  const handleTogglePass = (id: string) => {
    const updated = passes.map((p) => (p.id === id ? { ...p, enabled: !p.enabled } : p));
    setPasses(updated);
    engineRHI.passes = updated;
    engineRHI.resolveRenderGraphBarriers();
    fmodEngine.playSoundEvent('click');
    onLogMessage(`RenderGraph pass '${id}' toggled`, 'INF');
  };

  const handleAddPass = (e: React.FormEvent) => {
    e.preventDefault();
    if (!newPassName.trim()) return;

    const newPass: RenderGraphPass = {
      id: `pass-${Date.now().toString().slice(-4)}`,
      name: newPassName.trim(),
      reads: ['Swapchain_Backbuffer_0'],
      writes: [`Texture_${newPassName.trim()}_Out`],
      enabled: true,
      clearColor: [0.05, 0.05, 0.05, 1.0],
    };

    const updated = [...passes, newPass];
    setPasses(updated);
    engineRHI.passes = updated;
    engineRHI.resolveRenderGraphBarriers();
    setNewPassName('');
    fmodEngine.playSoundEvent('pipeline_compile');
    onLogMessage(`RenderGraph: AddPass('${newPass.name}') registered`, 'INF');
  };

  const handleDeletePass = (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    if (passes.length <= 1) return;
    const updated = passes.filter((p) => p.id !== id);
    setPasses(updated);
    engineRHI.passes = updated;
    engineRHI.resolveRenderGraphBarriers();
    if (selectedPass.id === id) {
      setSelectedPass(updated[0]);
    }
    fmodEngine.playSoundEvent('click');
    onLogMessage(`RenderGraph: Pass '${id}' removed from execution queue`, 'INF');
  };

  return (
    <div className="flex-1 flex flex-col md:flex-row h-full overflow-hidden bg-neutral-950">
      {/* Pass Execution Graph Column */}
      <div className="flex-1 flex flex-col border-b md:border-b-0 md:border-r border-neutral-800 p-4 overflow-y-auto">
        <div className="flex items-center justify-between mb-4">
          <div>
            <h2 className="text-sm font-semibold text-neutral-100 flex items-center gap-2">
              <Layers className="w-4 h-4 text-red-400" />
              IRenderGraph DAG Pipeline Execution
            </h2>
            <p className="text-xs text-neutral-400">
              Pass declarations derive Vulkan image layout transitions and memory barriers automatically.
            </p>
          </div>

          <form onSubmit={handleAddPass} className="flex items-center gap-2">
            <input
              type="text"
              placeholder="New pass name..."
              value={newPassName}
              onChange={(e) => setNewPassName(e.target.value)}
              className="bg-neutral-900 border border-neutral-700 text-xs px-2.5 py-1.5 rounded-md text-neutral-200 focus:outline-none focus:border-red-500 font-mono w-40"
            />
            <button
              type="submit"
              className="flex items-center gap-1 bg-red-600 hover:bg-red-500 text-white text-xs px-2.5 py-1.5 rounded-md font-medium transition-colors"
            >
              <Plus className="w-3.5 h-3.5" />
              Add Pass
            </button>
          </form>
        </div>

        {/* Pass Graph Nodes List */}
        <div className="space-y-3 relative">
          {passes.map((pass, index) => {
            const isSelected = selectedPass.id === pass.id;
            return (
              <React.Fragment key={pass.id}>
                <div
                  onClick={() => {
                    setSelectedPass(pass);
                    fmodEngine.playSoundEvent('click');
                  }}
                  className={`p-3.5 rounded-xl border transition-all cursor-pointer ${
                    isSelected
                      ? 'bg-neutral-900 border-red-500/80 shadow-lg shadow-red-950/30 ring-1 ring-red-500/30'
                      : pass.enabled
                      ? 'bg-neutral-900/60 border-neutral-800 hover:border-neutral-700'
                      : 'bg-neutral-950/40 border-neutral-800/40 opacity-60'
                  }`}
                >
                  <div className="flex items-center justify-between mb-2">
                    <div className="flex items-center gap-2.5">
                      <button
                        onClick={(e) => {
                          e.stopPropagation();
                          handleTogglePass(pass.id);
                        }}
                        className={`w-5 h-5 rounded-full flex items-center justify-center border transition-colors ${
                          pass.enabled
                            ? 'bg-emerald-500/20 border-emerald-500/50 text-emerald-400'
                            : 'bg-neutral-800 border-neutral-700 text-neutral-500'
                        }`}
                        title={pass.enabled ? 'Enabled' : 'Disabled'}
                      >
                        <CheckCircle2 className="w-3.5 h-3.5" />
                      </button>
                      <div>
                        <span className="text-xs font-mono font-semibold text-neutral-100">
                          {pass.name}
                        </span>
                        <span className="ml-2 text-[10px] text-neutral-500 font-mono">
                          (Stage {index})
                        </span>
                      </div>
                    </div>

                    <div className="flex items-center gap-2">
                      <span
                        className={`text-[10px] px-2 py-0.5 rounded font-mono ${
                          pass.enabled
                            ? 'bg-emerald-950 text-emerald-300 border border-emerald-800/60'
                            : 'bg-neutral-800 text-neutral-500'
                        }`}
                      >
                        {pass.enabled ? 'ACTIVE_PASS' : 'CULLED'}
                      </span>
                      {passes.length > 1 && (
                        <button
                          onClick={(e) => handleDeletePass(pass.id, e)}
                          className="p-1 rounded text-neutral-500 hover:text-red-400 transition-colors"
                          title="Remove Pass"
                        >
                          <Trash2 className="w-3.5 h-3.5" />
                        </button>
                      )}
                    </div>
                  </div>

                  {/* Pass Dependencies */}
                  <div className="grid grid-cols-2 gap-2 text-[11px] font-mono mt-2 pt-2 border-t border-neutral-800/60">
                    <div>
                      <span className="text-neutral-500 text-[10px] uppercase">Reads():</span>
                      <div className="text-neutral-300 mt-0.5">
                        {pass.reads.length > 0 ? pass.reads.join(', ') : 'None (No read dependencies)'}
                      </div>
                    </div>
                    <div>
                      <span className="text-neutral-500 text-[10px] uppercase">Writes():</span>
                      <div className="text-amber-400 mt-0.5">
                        {pass.writes.length > 0 ? pass.writes.join(', ') : 'Depth Attachment only'}
                      </div>
                    </div>
                  </div>
                </div>

                {index < passes.length - 1 && (
                  <div className="flex items-center justify-center py-0.5 text-neutral-600">
                    <ArrowDown className="w-4 h-4" />
                  </div>
                )}
              </React.Fragment>
            );
          })}
        </div>
      </div>

      {/* Vulkan Barrier Builder & Inspection Column */}
      <div className="w-full md:w-96 bg-neutral-900 p-4 flex flex-col gap-4 overflow-y-auto">
        <div>
          <h3 className="text-xs font-semibold uppercase tracking-wider text-neutral-400 flex items-center gap-1.5 mb-1">
            <ShieldAlert className="w-3.5 h-3.5 text-amber-400" />
            VulkanBarrierBuilder Transitions
          </h3>
          <p className="text-[11px] text-neutral-500">
            Hardware synchronization barriers derived from pass declarations.
          </p>
        </div>

        {/* Selected Pass Details */}
        <div className="p-3 bg-neutral-950 rounded-lg border border-neutral-800 space-y-2 text-xs font-mono">
          <div className="text-neutral-400 text-[11px] uppercase tracking-wider border-b border-neutral-800 pb-1">
            Current Pass: <span className="text-neutral-100 font-bold">{selectedPass.name}</span>
          </div>
          <div className="flex justify-between">
            <span className="text-neutral-500">Attachments:</span>
            <span className="text-neutral-300">
              {selectedPass.writes.length} Color / {selectedPass.depthWrite ? '1 Depth' : '0 Depth'}
            </span>
          </div>
          <div className="flex justify-between">
            <span className="text-neutral-500">Clear Color:</span>
            <span className="text-neutral-300">
              [{selectedPass.clearColor.map((c) => c.toFixed(2)).join(', ')}]
            </span>
          </div>
          <div className="flex justify-between">
            <span className="text-neutral-500">Command Buffer:</span>
            <span className="text-neutral-300">Primary (OneTimeSubmit)</span>
          </div>
        </div>

        {/* Generated Barriers List */}
        <div className="space-y-2.5">
          <div className="text-xs font-medium text-neutral-300">
            Active Frame Barriers ({engineRHI.lastBarriers.length})
          </div>
          {engineRHI.lastBarriers.map((barrier, idx) => (
            <div
              key={idx}
              className="p-2.5 rounded-lg bg-neutral-950 border border-neutral-800 text-[11px] font-mono space-y-1.5"
            >
              <div className="flex items-center justify-between text-neutral-300 font-semibold border-b border-neutral-800/80 pb-1">
                <span>{barrier.resource}</span>
                <span className="text-[10px] text-amber-400 bg-amber-950/60 px-1.5 py-0.2 rounded border border-amber-800/50">
                  VkImageMemoryBarrier
                </span>
              </div>

              <div className="flex items-center gap-1.5 text-neutral-400">
                <span className="text-neutral-500 truncate max-w-[120px]" title={barrier.oldLayout}>
                  {barrier.oldLayout.replace('VK_IMAGE_LAYOUT_', '')}
                </span>
                <ArrowRight className="w-3 h-3 text-red-400 shrink-0" />
                <span className="text-emerald-400 truncate max-w-[120px]" title={barrier.newLayout}>
                  {barrier.newLayout.replace('VK_IMAGE_LAYOUT_', '')}
                </span>
              </div>

              <div className="text-[10px] text-neutral-500 space-y-0.5 pt-1">
                <div>
                  <span className="text-neutral-600">Src Stage:</span>{' '}
                  <span className="text-neutral-400">{barrier.srcStage.replace('VK_PIPELINE_STAGE_', '')}</span>
                </div>
                <div>
                  <span className="text-neutral-600">Dst Stage:</span>{' '}
                  <span className="text-neutral-400">{barrier.dstStage.replace('VK_PIPELINE_STAGE_', '')}</span>
                </div>
                <div>
                  <span className="text-neutral-600">Access:</span>{' '}
                  <span className="text-neutral-400">{barrier.dstAccess.replace('VK_ACCESS_', '')}</span>
                </div>
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
};
