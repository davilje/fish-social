/** Timeline / Inspector SOP row coloring (ADMIN-OBS-1.2 B1) */

export type SopEventLike = {
  eventType: string;
  payload?: Record<string, unknown>;
  payloadSummary?: Record<string, unknown>;
};

/**
 * Returns CSS class for SOP-colored rows.
 * - disconnect_timeout → red (row-error)
 * - disconnect / reconnect → yellow (row-warn)
 * - leave_pond → gray (row-muted)
 * - checkpoint_restore → purple (row-checkpoint)
 * - server_start / server_stop → orange (row-server)
 * - unknown → row-muted (still visible)
 */
export function sopEventRowClass(ev: SopEventLike): string {
  const t = ev.eventType;
  const payload = ev.payload ?? ev.payloadSummary ?? {};
  const joinKind = payload.joinKind;

  if (t === 'server_start' || t === 'server_stop') return 'row-server';
  if (t === 'checkpoint_restore' || joinKind === 'checkpoint_restore') return 'row-checkpoint';
  if (t === 'disconnect_timeout' || t.includes('disconnect_timeout')) return 'row-error';
  if (t === 'spot_release') return 'row-error';
  if (t.includes('disconnect') || t === 'reconnect') return 'row-warn';
  if (t === 'leave_pond') return 'row-muted';
  return 'row-muted';
}
