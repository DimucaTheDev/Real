import React, { useState } from 'react';
import { Volume2, Sliders, Play, Radio, Activity, Sparkles } from 'lucide-react';
import { fmodEngine } from '../engine/audio';

interface AudioPanelProps {
  isHumming: boolean;
  onToggleHum: () => void;
  onLogMessage: (msg: string, level?: 'INF' | 'VRB' | 'DBG' | 'WRN') => void;
}

export const AudioPanel: React.FC<AudioPanelProps> = ({
  isHumming,
  onToggleHum,
  onLogMessage,
}) => {
  const [volume, setVolume] = useState(fmodEngine.masterVolume);
  const [cutoff, setCutoff] = useState(fmodEngine.cutoffFreq);

  const handleVolumeChange = (val: number) => {
    setVolume(val);
    fmodEngine.setVolume(val);
  };

  const handleCutoffChange = (val: number) => {
    setCutoff(val);
    fmodEngine.setCutoff(val);
  };

  const playEvent = (name: 'triangle_hit' | 'pipeline_compile' | 'click' | 'fence_signal') => {
    fmodEngine.playSoundEvent(name);
    onLogMessage(`FMOD: EventInstance.start() -> "event:/sfx/${name}"`, 'INF');
  };

  return (
    <div className="flex-1 p-4 md:p-6 overflow-y-auto bg-neutral-950 space-y-6">
      <div className="flex items-center justify-between border-b border-neutral-800 pb-4">
        <div>
          <h2 className="text-sm font-semibold text-neutral-100 flex items-center gap-2">
            <Volume2 className="w-4 h-4 text-red-400" />
            Real.Fmod Audio Studio & DSP Subsystem
          </h2>
          <p className="text-xs text-neutral-400">
            FMOD Studio event dispatching, bank mounting, and DSP filtering.
          </p>
        </div>

        <button
          onClick={onToggleHum}
          className={`flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs font-mono border transition-colors ${
            isHumming
              ? 'bg-amber-950/80 border-amber-800/80 text-amber-300 font-semibold shadow-sm'
              : 'bg-neutral-900 border-neutral-700 text-neutral-400 hover:text-neutral-200'
          }`}
        >
          <Radio className={`w-3.5 h-3.5 ${isHumming ? 'animate-pulse text-amber-400' : ''}`} />
          <span>{isHumming ? 'Engine Hum: Active' : 'Start Engine Hum'}</span>
        </button>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        {/* DSP Master Controls */}
        <div className="p-4 rounded-xl bg-neutral-900 border border-neutral-800 space-y-4 font-mono text-xs">
          <div className="text-neutral-200 font-semibold flex items-center gap-2">
            <Sliders className="w-3.5 h-3.5 text-red-400" />
            FMOD Master Channel & DSP Chain
          </div>

          <div className="space-y-2">
            <div className="flex justify-between text-neutral-400">
              <span>Master Volume</span>
              <span className="text-neutral-200">{Math.round(volume * 100)}%</span>
            </div>
            <input
              type="range"
              min="0"
              max="1"
              step="0.05"
              value={volume}
              onChange={(e) => handleVolumeChange(parseFloat(e.target.value))}
              className="w-full accent-red-500 h-1.5 bg-neutral-950 rounded-lg cursor-pointer"
            />
          </div>

          <div className="space-y-2">
            <div className="flex justify-between text-neutral-400">
              <span>DSP LowPass Cutoff</span>
              <span className="text-neutral-200">{cutoff} Hz</span>
            </div>
            <input
              type="range"
              min="200"
              max="8000"
              step="100"
              value={cutoff}
              onChange={(e) => handleCutoffChange(parseFloat(e.target.value))}
              className="w-full accent-red-500 h-1.5 bg-neutral-950 rounded-lg cursor-pointer"
            />
          </div>

          <div className="pt-2 border-t border-neutral-800 space-y-1 text-[11px] text-neutral-500">
            <div className="flex justify-between">
              <span>FMOD Studio Version:</span>
              <span className="text-neutral-300">2.02.04 (C# P/Invoke)</span>
            </div>
            <div className="flex justify-between">
              <span>Mounted Banks:</span>
              <span className="text-neutral-300">Master.bank, Master.strings.bank</span>
            </div>
          </div>
        </div>

        {/* FMOD Event Dispatcher */}
        <div className="p-4 rounded-xl bg-neutral-900 border border-neutral-800 space-y-3 font-mono text-xs">
          <div className="text-neutral-200 font-semibold flex items-center gap-2">
            <Sparkles className="w-3.5 h-3.5 text-amber-400" />
            Sound Event Trigger Board
          </div>
          <div className="text-[11px] text-neutral-500">
            Trigger simulated 3D spatial and UI audio events defined in RealEngine game banks.
          </div>

          <div className="grid grid-cols-2 gap-2 pt-2">
            <button
              onClick={() => playEvent('triangle_hit')}
              className="p-2.5 rounded-lg bg-neutral-950 border border-neutral-800 hover:border-red-500/60 text-left transition-colors group"
            >
              <div className="text-neutral-200 group-hover:text-red-400 font-semibold">Triangle Hit</div>
              <div className="text-[10px] text-neutral-500">event:/sfx/triangle_hit</div>
            </button>

            <button
              onClick={() => playEvent('pipeline_compile')}
              className="p-2.5 rounded-lg bg-neutral-950 border border-neutral-800 hover:border-blue-500/60 text-left transition-colors group"
            >
              <div className="text-neutral-200 group-hover:text-blue-400 font-semibold">Pipeline Link</div>
              <div className="text-[10px] text-neutral-500">event:/sfx/pipeline_compile</div>
            </button>

            <button
              onClick={() => playEvent('fence_signal')}
              className="p-2.5 rounded-lg bg-neutral-950 border border-neutral-800 hover:border-purple-500/60 text-left transition-colors group"
            >
              <div className="text-neutral-200 group-hover:text-purple-400 font-semibold">Fence Signal</div>
              <div className="text-[10px] text-neutral-500">event:/sfx/fence_signal</div>
            </button>

            <button
              onClick={() => playEvent('click')}
              className="p-2.5 rounded-lg bg-neutral-950 border border-neutral-800 hover:border-emerald-500/60 text-left transition-colors group"
            >
              <div className="text-neutral-200 group-hover:text-emerald-400 font-semibold">UI Click</div>
              <div className="text-[10px] text-neutral-500">event:/ui/select</div>
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};
