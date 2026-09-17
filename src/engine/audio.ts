class FmodAudioEngine {
  private ctx: AudioContext | null = null;
  private masterGain: GainNode | null = null;
  private filterNode: BiquadFilterNode | null = null;
  private humOsc: OscillatorNode | null = null;
  private humGain: GainNode | null = null;
  public isHumming = false;
  public cutoffFreq = 2500;
  public masterVolume = 0.5;

  private init() {
    if (!this.ctx) {
      const AudioContextClass = window.AudioContext || (window as unknown as { webkitAudioContext: typeof AudioContext }).webkitAudioContext;
      this.ctx = new AudioContextClass();

      this.masterGain = this.ctx.createGain();
      this.masterGain.gain.value = this.masterVolume;

      this.filterNode = this.ctx.createBiquadFilter();
      this.filterNode.type = 'lowpass';
      this.filterNode.frequency.value = this.cutoffFreq;

      this.masterGain.connect(this.filterNode);
      this.filterNode.connect(this.ctx.destination);
    }

    if (this.ctx.state === 'suspended') {
      this.ctx.resume();
    }
  }

  public setVolume(vol: number) {
    this.masterVolume = Math.max(0, Math.min(1, vol));
    if (this.masterGain && this.ctx) {
      this.masterGain.gain.setValueAtTime(this.masterVolume, this.ctx.currentTime);
    }
  }

  public setCutoff(freq: number) {
    this.cutoffFreq = freq;
    if (this.filterNode && this.ctx) {
      this.filterNode.frequency.setValueAtTime(freq, this.ctx.currentTime);
    }
  }

  public playSoundEvent(name: 'triangle_hit' | 'pipeline_compile' | 'click' | 'fence_signal') {
    this.init();
    if (!this.ctx || !this.masterGain) return;

    const now = this.ctx.currentTime;
    const osc = this.ctx.createOscillator();
    const gain = this.ctx.createGain();

    if (name === 'triangle_hit') {
      osc.type = 'triangle';
      osc.frequency.setValueAtTime(440, now);
      osc.frequency.exponentialRampToValueAtTime(880, now + 0.15);
      gain.gain.setValueAtTime(0.3, now);
      gain.gain.exponentialRampToValueAtTime(0.001, now + 0.3);
      osc.connect(gain);
      gain.connect(this.masterGain);
      osc.start(now);
      osc.stop(now + 0.3);
    } else if (name === 'pipeline_compile') {
      osc.type = 'sine';
      osc.frequency.setValueAtTime(587.33, now); // D5
      osc.frequency.setValueAtTime(880, now + 0.08); // A5
      gain.gain.setValueAtTime(0.25, now);
      gain.gain.exponentialRampToValueAtTime(0.001, now + 0.25);
      osc.connect(gain);
      gain.connect(this.masterGain);
      osc.start(now);
      osc.stop(now + 0.25);
    } else if (name === 'click') {
      osc.type = 'square';
      osc.frequency.setValueAtTime(1200, now);
      gain.gain.setValueAtTime(0.1, now);
      gain.gain.exponentialRampToValueAtTime(0.001, now + 0.04);
      osc.connect(gain);
      gain.connect(this.masterGain);
      osc.start(now);
      osc.stop(now + 0.04);
    } else if (name === 'fence_signal') {
      osc.type = 'sawtooth';
      osc.frequency.setValueAtTime(320, now);
      osc.frequency.linearRampToValueAtTime(220, now + 0.1);
      gain.gain.setValueAtTime(0.15, now);
      gain.gain.exponentialRampToValueAtTime(0.001, now + 0.12);
      osc.connect(gain);
      gain.connect(this.masterGain);
      osc.start(now);
      osc.stop(now + 0.12);
    }
  }

  public toggleEngineHum(enable?: boolean): boolean {
    this.init();
    if (!this.ctx || !this.masterGain) return false;

    const targetState = enable !== undefined ? enable : !this.isHumming;
    if (targetState === this.isHumming) return this.isHumming;

    if (targetState) {
      const now = this.ctx.currentTime;
      this.humOsc = this.ctx.createOscillator();
      this.humGain = this.ctx.createGain();

      this.humOsc.type = 'sine';
      this.humOsc.frequency.setValueAtTime(65.41, now); // C2 low engine frequency

      this.humGain.gain.setValueAtTime(0.001, now);
      this.humGain.gain.linearRampToValueAtTime(0.15, now + 0.5);

      this.humOsc.connect(this.humGain);
      this.humGain.connect(this.masterGain);
      this.humOsc.start();
      this.isHumming = true;
    } else {
      if (this.humGain && this.ctx && this.humOsc) {
        const now = this.ctx.currentTime;
        this.humGain.gain.linearRampToValueAtTime(0.001, now + 0.2);
        const osc = this.humOsc;
        setTimeout(() => {
          try {
            osc.stop();
            osc.disconnect();
          } catch {
            // Already stopped
          }
        }, 220);
      }
      this.humOsc = null;
      this.humGain = null;
      this.isHumming = false;
    }

    return this.isHumming;
  }
}

export const fmodEngine = new FmodAudioEngine();
