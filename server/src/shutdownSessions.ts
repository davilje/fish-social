/**
 * Best-effort flush of in-pond human sessions on graceful shutdown (STAB-01).
 */
import { PONDS } from '@fish-social/shared';
import { listUsersInPond } from './gameState.js';
import { upsertPlayerPondSession } from './playerPondSession.js';
import { logStructuredEvent } from './fishingObservability.js';

export function flushPlayerPondSessionsOnShutdown(): number {
  let flushed = 0;
  for (const pond of PONDS) {
    for (const user of listUsersInPond(pond.id)) {
      if (user.isBot || !user.playerId) continue;
      try {
        upsertPlayerPondSession(user, pond.id);
        flushed += 1;
      } catch {
        // best effort
      }
    }
  }
  logStructuredEvent('shutdown', 'shutdown_phase', {
    phase: 'flush_sessions',
    flushed,
    eventType: 'shutdown_phase',
  });
  return flushed;
}
