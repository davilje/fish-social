export type PondSocketLogEvent =
  | 'connect'
  | 'disconnect'
  | 'connect_error'
  | 'reconnect_attempt'
  | 'join_pond_ok'
  | 'join_pond_fail'
  | 'leave_pond';

export type PondAppLogState = 'foreground' | 'background' | 'visible' | 'hidden';

const RING_MAX = 200;
const ringBuffer: Array<{ tag: 'pond-socket' | 'pond-app'; event: string; data: Record<string, unknown> }> =
  [];

function pushRing(tag: 'pond-socket' | 'pond-app', event: string, data: Record<string, unknown>): void {
  if (typeof __DEV__ === 'undefined' || !__DEV__) return;
  ringBuffer.push({ tag, event, data: { ts: Date.now(), ...data } });
  if (ringBuffer.length > RING_MAX) ringBuffer.shift();
}

export function logPondSocket(event: PondSocketLogEvent, data: Record<string, unknown>): void {
  const payload = { ts: Date.now(), ...data };
  console.log(`[pond-socket] ${event} ${JSON.stringify(payload)}`);
  pushRing('pond-socket', event, data);
}

export function logPondApp(state: PondAppLogState, data: Record<string, unknown> = {}): void {
  const payload = { ts: Date.now(), ...data };
  console.log(`[pond-app] ${state} ${JSON.stringify(payload)}`);
  pushRing('pond-app', state, data);
}

export function getPondLifecycleRingBuffer(): ReadonlyArray<{
  tag: 'pond-socket' | 'pond-app';
  event: string;
  data: Record<string, unknown>;
}> {
  return ringBuffer;
}
