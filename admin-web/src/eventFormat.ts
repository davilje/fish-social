const EVENT_LABELS: Record<string, string> = {
  pending_catch_accept: '收鱼',
  catch_accept: '收鱼',
  bite_hook: '上钩',
  escape: '脱钩',
  fishing_start: '开始钓鱼',
  fishing_stop: '停止钓鱼',
  fishing_phase_transition: '相位切换',
  phase_transition_invalid: '非法相位',
  disconnect: '断线',
  socket_disconnect: '断线',
  reconnect: '重连',
  disconnect_timeout: '断线超时',
  join_pond_success: '进塘成功',
  join_pond_fail: '进塘失败',
  leave_pond: '离塘',
  spot_take_success: '占钓位',
  spot_take_fail: '占钓位失败',
  bait_depleted: '饵耗尽',
  pending_catch_created: '待领鱼',
  pending_catch_expired: '待领超时',
};

const PHASE_LABELS: Record<string, string> = {
  idle: '空闲',
  seated: '入座',
  baiting: '上饵',
  casting: '抛竿',
  waiting: '等待',
  hooked: '中鱼',
  resolving: '结算',
  stopping: '收杆',
  disconnected: '断线中',
};

const PHASE_FROM_CODE: Record<number, string> = {
  0: 'idle',
  1: 'seated',
  2: 'baiting',
  3: 'casting',
  4: 'waiting',
  5: 'hooked',
  6: 'resolving',
  7: 'stopping',
  8: 'disconnected',
};

export const ADMIN_POND_IDS = ['pond-calm', 'pond-mist', 'pond-sunset', 'pond-bamboo'] as const;

export function eventLabel(eventType: string): string {
  return EVENT_LABELS[eventType] ?? eventType;
}

export function phaseLabel(phase: string | null | undefined): string {
  if (!phase) return '—';
  return PHASE_LABELS[phase] ?? phase;
}

export function formatDurationMs(ms: number | null | undefined): string {
  if (ms == null || !Number.isFinite(ms)) return '—';
  const s = Math.max(0, Math.floor(ms / 1000));
  if (s < 60) return `${s}s`;
  const m = Math.floor(s / 60);
  const rem = s % 60;
  if (m < 60) return `${m}m${rem.toString().padStart(2, '0')}s`;
  const h = Math.floor(m / 60);
  return `${h}h${(m % 60).toString().padStart(2, '0')}m`;
}

function phaseFromCode(code: unknown): string | null {
  if (typeof code !== 'number') return null;
  if (code < 0) return null;
  return PHASE_FROM_CODE[code] ?? null;
}

/** Compact readable payload summary (≤ ~80 chars). Full JSON via title. */
export function summarizeEventPayload(
  eventType: string,
  payload: Record<string, unknown> | undefined | null,
): string {
  if (!payload || typeof payload !== 'object') return '—';
  const parts: string[] = [];

  if (eventType === 'fishing_phase_transition' || eventType === 'phase_transition_invalid') {
    const from = phaseFromCode(payload.f) ?? (typeof payload.fromPhase === 'string' ? payload.fromPhase : null);
    const to = phaseFromCode(payload.t) ?? (typeof payload.toPhase === 'string' ? payload.toPhase : null);
    const cause = typeof payload.c === 'string' ? payload.c : typeof payload.cause === 'string' ? payload.cause : null;
    if (from || to) parts.push(`${phaseLabel(from)}→${phaseLabel(to)}`);
    if (cause) parts.push(cause);
  }

  if (typeof payload.sessionFishingMs === 'number') {
    parts.push(`时长 ${formatDurationMs(payload.sessionFishingMs)}`);
  }
  if (typeof payload.reason === 'string') parts.push(payload.reason);
  if (typeof payload.joinKind === 'string') parts.push(payload.joinKind);
  if (typeof payload.quality === 'string') parts.push(payload.quality);
  if (typeof payload.sizeM === 'number') parts.push(`${payload.sizeM.toFixed(2)}m`);
  if (typeof payload.speciesId === 'string') parts.push(payload.speciesId);
  if (typeof payload.spotId === 'string') parts.push(`钓位 ${payload.spotId}`);

  const text = parts.filter(Boolean).join(' · ') || '—';
  return text.length > 80 ? `${text.slice(0, 77)}…` : text;
}

export function payloadTitle(payload: Record<string, unknown> | undefined | null): string {
  if (!payload) return '';
  try {
    return JSON.stringify(payload);
  } catch {
    return '';
  }
}
