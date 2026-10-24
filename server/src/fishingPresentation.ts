import { randomUUID } from 'crypto';
import type { FishingPhase, PondUser } from '@fish-social/shared';

/** Called only at authoritative transitions; serialization/snapshots never mint identities. */
export function advanceFishingPresentation(user: PondUser, phase: FishingPhase): void {
  if (phase === user.fishingPhase && user.presentationEventId) return;
  if (phase === 'baiting' || phase === 'groundbaiting' || !user.presentationActionId) {
    user.presentationActionId = randomUUID();
  }
  user.presentationEventId = randomUUID();
  user.presentationRevision = (user.presentationRevision ?? 0) + 1;
}
