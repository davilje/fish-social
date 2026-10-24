import { describe, expect, it } from 'vitest';
import type { PondUser } from '@fish-social/shared';
import { advanceFishingPresentation } from '../fishingPresentation.js';

describe('authoritative fishing presentation identity', () => {
  it('retains action through cast/bite; snapshot and repeated phase cannot mint events', () => {
    const user = { fishingPhase: 'seated' } as PondUser;
    advanceFishingPresentation(user, 'baiting'); user.fishingPhase = 'baiting';
    const action = user.presentationActionId, event = user.presentationEventId;
    advanceFishingPresentation(user, 'baiting');
    expect(user.presentationEventId).toBe(event);
    expect(JSON.parse(JSON.stringify(user)).presentationEventId).toBe(event);
    advanceFishingPresentation(user, 'casting'); user.fishingPhase = 'casting';
    expect(user.presentationActionId).toBe(action);
    expect(user.presentationEventId).not.toBe(event);
    advanceFishingPresentation(user, 'hooked'); user.fishingPhase = 'hooked';
    expect(user.presentationActionId).toBe(action);
    expect(user.presentationRevision).toBe(3);
    advanceFishingPresentation(user, 'baiting');
    expect(user.presentationActionId).not.toBe(action);
  });
  it('gives every groundbait action a unique stable identity', () => {
    const a = { fishingPhase: 'seated' } as PondUser;
    const b = { fishingPhase: 'seated' } as PondUser;
    advanceFishingPresentation(a, 'groundbaiting');
    advanceFishingPresentation(b, 'groundbaiting');
    expect(a.presentationActionId).not.toBe(b.presentationActionId);
  });
});
