import { useEffect, useRef, useState } from 'react';
import { Platform, Pressable, StyleSheet, Text, Vibration, View } from 'react-native';
import type { PondUser } from '@fish-social/shared';

const KEY = 'fishsocial.landing-reminder.sound';
const consumed = new Set<string>();
function savedSound(): boolean {
  try { return Platform.OS === 'web' && localStorage.getItem(KEY) === '1'; } catch { return false; }
}

export function FishLandingReminder({ user, now }: { user?: PondUser; now: number }) {
  const [sound, setSound] = useState(savedSound);
  const audio = useRef<AudioContext | null>(null);
  const activeTone = useRef<OscillatorNode | null>(null);
  const soundRef = useRef(sound);
  soundRef.current = sound;
  const remaining = (user?.phaseEndsAt ?? 0) - now;
  const near = !user?.fishingAsleep && user?.fishingPhase === 'hooked' && remaining > 0 && remaining <= 5000;
  const identity = `${user?.id ?? ''}:${user?.phaseContext?.catchId ?? ''}`;

  function unlock() {
    if (Platform.OS !== 'web' || !soundRef.current || typeof window.AudioContext !== 'function') return;
    audio.current ??= new window.AudioContext();
    void audio.current.resume().catch(() => {});
  }

  useEffect(() => {
    if (Platform.OS !== 'web') return;
    document.addEventListener('pointerdown', unlock);
    return () => { document.removeEventListener('pointerdown', unlock); void audio.current?.close(); audio.current = null; };
  }, []);

  useEffect(() => {
    if (!near || !user?.phaseContext?.catchId || consumed.has(identity)) return;
    consumed.add(identity);
    if (consumed.size > 512) consumed.delete(consumed.values().next().value!);
    if (!sound) return;
    if (Platform.OS !== 'web') { Vibration.vibrate(150); return; }
    const context = audio.current;
    if (!context || context.state !== 'running') return;
    const tone = context.createOscillator();
    const volume = context.createGain();
    tone.frequency.value = 660;
    volume.gain.setValueAtTime(0, context.currentTime);
    volume.gain.linearRampToValueAtTime(.08, context.currentTime + .025);
    volume.gain.exponentialRampToValueAtTime(.001, context.currentTime + .45);
    tone.connect(volume); volume.connect(context.destination);
    activeTone.current = tone;
    tone.start(); tone.stop(context.currentTime + .45);
    tone.onended = () => { if (activeTone.current === tone) activeTone.current = null; tone.disconnect(); volume.disconnect(); };
  }, [near, identity, sound]);

  return <View style={[styles.row, near && styles.active]}>
    <Text accessibilityLiveRegion="polite" style={styles.message}>{near ? '🎣 鱼儿快靠岸了，马上揭晓这次收获' : '静静等待，鱼儿靠岸时会提示你'}</Text>
    <Pressable accessibilityRole="switch" accessibilityState={{ checked: sound }} onPress={() => {
      const next = !sound; soundRef.current = next; setSound(next);
      try { if (Platform.OS === 'web') localStorage.setItem(KEY, next ? '1' : '0'); } catch { /* session setting still works */ }
      if (next) unlock();
      else {
        Vibration.cancel();
        if (activeTone.current) { activeTone.current.stop(); activeTone.current.disconnect(); activeTone.current = null; }
        if (audio.current) void audio.current.suspend();
      }
    }}>
      <Text style={styles.toggle}>{Platform.OS === 'web' ? '提示音' : '震动'}：{sound ? '开' : '关'}</Text>
    </Pressable>
  </View>;
}
const styles = StyleSheet.create({
  row: { padding: 10, backgroundColor: '#edf2ea', flexDirection: 'row', alignItems: 'center', gap: 10 },
  active: { backgroundColor: '#f5e8bd' }, message: { flex: 1, fontSize: 12, color: '#476252' },
  toggle: { fontSize: 12, color: '#2C5F6F', paddingVertical: 5 },
});
