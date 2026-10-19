import type { FishingPhase } from '@fish-social/shared';
import catalog from '../assets/cats/catalog.json';

type Clip = { count: number; playback: string; durationsMs: number[]; loopStart: number; next: string | null };
const clips: Record<string, Clip> = catalog.clips;
const outcomes = new Set(['reel', 'catch', 'reset_catch', 'reel_empty', 'reset_empty']);

export class CatAnimationPlayer {
  clip = 'idle';
  frame = 0;
  private elapsed = 0;
  private requestKey = '';
  private requested = 'idle';
  private deferred: string | null = null;

  request(phase?: FishingPhase, outcome?: string): void {
    const key = `${phase ?? 'idle'}:${outcome ?? ''}`;
    if (key === this.requestKey) return;
    const previous = this.requested;
    this.requestKey = key;
    this.requested = phase === 'seated' ? 'ready' : phase === 'baiting' ? 'bait'
      : phase === 'casting' ? 'cast' : phase === 'waiting' ? 'fishing'
        : phase === 'hooked' ? 'hooked' : phase === 'groundbaiting' ? 'groundbait'
          : phase === 'resolving' ? (outcome === 'catch' ? 'reel' : 'reel_empty')
            : phase === 'stopping' ? 'leave' : 'idle';
    if (outcomes.has(this.clip) && this.requested !== 'idle' && this.requested !== 'leave') {
      this.deferred = this.requested;
      return;
    }
    this.deferred = null;
    this.start(this.requested === 'ready' && previous === 'idle' ? 'sit' : this.requested);
  }

  advance(milliseconds: number): void {
    if (!Number.isFinite(milliseconds) || milliseconds < 0) return;
    this.elapsed += milliseconds;
    let definition = clips[this.clip];
    while (definition.playback !== 'hold' && this.elapsed >= definition.durationsMs[this.frame]) {
      this.elapsed -= definition.durationsMs[this.frame];
      if (this.frame + 1 < definition.count) {
        this.frame++;
      } else if (definition.playback === 'intro-loop') {
        this.frame = definition.loopStart;
      } else if (definition.next) {
        const next = this.deferred && (this.clip === 'reset_catch' || this.clip === 'reset_empty')
          ? this.deferred : definition.next;
        if (this.clip === 'reset_catch' || this.clip === 'reset_empty') this.deferred = null;
        this.clip = next;
        this.frame = 0;
      } else {
        this.elapsed = 0;
        break;
      }
      definition = clips[this.clip];
    }
  }

  private start(clip: string): void {
    this.clip = clip;
    this.frame = 0;
    this.elapsed = 0;
  }
}
