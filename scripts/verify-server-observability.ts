/**
 * v0.4.4 验收：join/socket 观测与 phase transition 时间线
 * 运行: npm run verify:server-observability
 */
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import type { Server } from 'socket.io';
import { phaseToCode, type ClientToServerEvents, type ServerToClientEvents } from '@fish-social/shared';
import '../server/src/db.js';
import { joinPond, reconnectSession, startFishing, getUserById, removeDisconnectedUser } from '../server/src/gameState.js';
import { beginFishingSequence, handleDisconnect, resumeAfterReconnect, scheduleHookFromBite } from '../server/src/fishingStateMachine.js';
import {
  clearBiteSessionCounters,
  isBiteTickPersistEnabled,
  noteBiteEscape,
  noteBiteMiss,
  resetBiteSessionCounters,
} from '../server/src/biteSessionCounters.js';
import { recordStructuredMetric } from '../server/src/fishingObservability.js';
import { getFishingMetricsSummary, getPlayerFishingTimeline, recordFishingMetric } from '../server/src/fishingMetrics.js';
import { lockPendingCatch } from '../server/src/inventory.js';

function transitionPayload(e: { payload: Record<string, unknown> }): { f?: number; t?: number; c?: string } {
  return e.payload as { f?: number; t?: number; c?: string };
}
const rootDir = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const POND_ID = 'pond-calm';
const SPOT_ID = 'calm-spot-1';

const mockIo = {
  to: () => ({ emit: () => {} }),
} as unknown as Server<ClientToServerEvents, ServerToClientEvents>;

function assert(cond: boolean, msg: string): void {
  if (!cond) throw new Error(`FAIL: ${msg}`);
  console.log(`  OK: ${msg}`);
}

function testJoinEventsInTimeline(): void {
  console.log('\n=== TC: join events in timeline ===');
  const playerId = `verify-obs-join-${Date.now()}`;
  recordStructuredMetric('socket_connect', {
    playerId,
    socketId: `sock-connect-${Date.now()}`,
    reason: 'socket_connected',
  });
  recordStructuredMetric('join_pond_attempt', {
    playerId,
    socketId: `sock-attempt-${Date.now()}`,
    pondId: POND_ID,
    reason: 'join_pond_attempt',
  });
  recordStructuredMetric('join_pond_success', {
    playerId,
    userId: `user-join-${Date.now()}`,
    socketId: `sock-success-${Date.now()}`,
    pondId: POND_ID,
    joinKind: 'fresh',
    reason: 'join_pond_success',
  });
  recordStructuredMetric('join_pond_fail', {
    playerId,
    socketId: `sock-fail-${Date.now()}`,
    pondId: POND_ID,
    reason: 'join_pond_fail',
    ackError: '鱼塘已满',
  });

  const timeline = getPlayerFishingTimeline(playerId, 1, 50);
  assert(timeline.summary.socketConnectCount >= 1, 'socket_connect counted');
  assert(timeline.summary.joinPondAttemptCount >= 1, 'join_pond_attempt counted');
  assert(timeline.summary.joinPondSuccessCount >= 1, 'join_pond_success counted');
  assert(timeline.summary.joinPondFailCount >= 1, 'join_pond_fail counted');
}

function testDisconnectReconnectPhaseTransitions(): void {
  console.log('\n=== TC: disconnect reconnect phase transitions ===');
  const playerId = `verify-obs-phase-${Date.now()}`;
  const socketA = `sock-phase-a-${Date.now()}`;
  const socketB = `sock-phase-b-${Date.now()}`;

  const joined = joinPond(socketA, POND_ID, '钓友观测A', playerId);
  assert(joined.ok, 'join pond');

  const start = startFishing(socketA, POND_ID, SPOT_ID);
  assert(start.ok, 'take spot');

  const seq = beginFishingSequence(mockIo, POND_ID, start.user.id, socketA);
  assert(seq.ok, 'enter baiting');

  handleDisconnect(POND_ID, start.user.id, () => {
    removeDisconnectedUser(POND_ID, start.user.id);
  });
  reconnectSession(socketB, POND_ID, start.user.id, playerId, '钓友观测A');
  resumeAfterReconnect(mockIo, POND_ID, getUserById(POND_ID, start.user.id)!, socketB);

  const timeline = getPlayerFishingTimeline(playerId, 1, 200);
  const transitions = timeline.events.filter((e) => e.eventType === 'fishing_phase_transition');
  assert(
    transitions.some((e) => {
      const p = transitionPayload(e);
      return p.f === phaseToCode('idle') && p.t === phaseToCode('seated');
    }),
    'idle -> seated',
  );
  assert(
    transitions.some((e) => {
      const p = transitionPayload(e);
      return p.f === phaseToCode('seated') && p.t === phaseToCode('baiting');
    }),
    'seated -> baiting',
  );
  assert(
    transitions.some((e) => {
      const p = transitionPayload(e);
      return p.t === phaseToCode('disconnected') && p.c === 'socket_disconnect';
    }),
    '* -> disconnected',
  );
  assert(
    transitions.some((e) => {
      const p = transitionPayload(e);
      return p.f === phaseToCode('disconnected') && p.c === 'socket_reconnect';
    }),
    'disconnected -> restored',
  );
  removeDisconnectedUser(POND_ID, start.user.id);
}

function testHookedReconnectTimeline(): void {
  console.log('\n=== TC: hooked reconnect transition chain ===');
  const playerId = `verify-obs-hooked-${Date.now()}`;
  const socketA = `sock-hooked-a-${Date.now()}`;
  const socketB = `sock-hooked-b-${Date.now()}`;

  const joined = joinPond(socketA, POND_ID, '钓友观测B', playerId);
  assert(joined.ok, 'join pond');

  const start = startFishing(socketA, POND_ID, SPOT_ID);
  assert(start.ok, 'take spot');

  const user = getUserById(POND_ID, start.user.id)!;
  user.status = 'fishing';
  user.fishingPhase = 'waiting';
  user.spotId = SPOT_ID;

  scheduleHookFromBite(mockIo, POND_ID, start.user.id, socketA, {
    fish: {
      id: `verify-fish-${Date.now()}`,
      pondId: POND_ID,
      spotId: SPOT_ID,
      speciesId: 'carp',
      quality: 'gray',
      sizeM: 0.3,
      bornAt: Date.now(),
      generation: 1,
    },
    escaped: false,
    hookDurationMs: 2000,
  });

  handleDisconnect(POND_ID, start.user.id, () => {
    removeDisconnectedUser(POND_ID, start.user.id);
  });
  reconnectSession(socketB, POND_ID, start.user.id, playerId, '钓友观测B');
  resumeAfterReconnect(mockIo, POND_ID, getUserById(POND_ID, start.user.id)!, socketB);

  const timeline = getPlayerFishingTimeline(playerId, 1, 200);
  const transitions = timeline.events.filter((e) => e.eventType === 'fishing_phase_transition');
  assert(
    transitions.some((e) => {
      const p = transitionPayload(e);
      return p.f === phaseToCode('waiting') && p.t === phaseToCode('hooked');
    }),
    'waiting -> hooked',
  );
  assert(
    transitions.some((e) => {
      const p = transitionPayload(e);
      return (
        p.f === phaseToCode('disconnected') &&
        (p.t === phaseToCode('hooked') || p.t === phaseToCode('resolving'))
      );
    }),
    'hooked reconnect transition recorded',
  );
  removeDisconnectedUser(POND_ID, start.user.id);
}

function testP1ObservabilitySummary(): void {
  console.log('\n=== TC: p1 observability summary ===');
  const playerId = `verify-obs-p1-${Date.now()}`;
  const userId = `user-p1-${Date.now()}`;
  const pondFishId = `pond-fish-p1-${Date.now()}`;

  recordFishingMetric('spot_take_success', {
    playerId,
    pondId: POND_ID,
    payload: { userId, spotId: SPOT_ID },
  });
  recordFishingMetric('spot_take_fail', {
    playerId,
    pondId: POND_ID,
    payload: { userId, spotId: SPOT_ID, reason: 'occupied_by_human' },
  });
  recordFishingMetric('spot_release', {
    playerId,
    pondId: POND_ID,
    payload: { userId, spotId: SPOT_ID, reason: 'disconnect_timeout' },
  });
  recordFishingMetric('pond_full_reject', {
    playerId,
    pondId: POND_ID,
    payload: { reason: 'max_pond_users' },
  });
  recordFishingMetric('bot_evicted_for_human', {
    playerId,
    pondId: POND_ID,
    payload: { userId: 'bot-user', reason: 'spot_take_bot_eviction' },
  });
  recordFishingMetric('bite_tick_miss', {
    playerId,
    pondId: POND_ID,
    payload: { userId, spotId: SPOT_ID, reason: 'failed' },
  });
  recordFishingMetric('bite_tick_hit', {
    playerId,
    pondId: POND_ID,
    payload: { userId, spotId: SPOT_ID, speciesId: 'carp' },
  });
  // tick 行仍可被 timeline 计数（METRICS_BITE_TICK_PERSIST=1 / 直写）；默认生产路径不再写
  recordFishingMetric('bait_depleted', {
    playerId,
    pondId: POND_ID,
    payload: { userId, reason: 'bait_insufficient' },
  });
  lockPendingCatch(
    userId,
    {
      catchId: `catch-p1-${Date.now()}`,
      pondFishId,
      speciesId: 'carp',
      quality: 'gray',
      sizeM: 0.2,
      hookDurationMs: 1000,
    },
    { playerId, pondId: POND_ID },
  );

  const timeline = getPlayerFishingTimeline(playerId, 1, 200);
  assert(timeline.summary.spotTakeSuccessCount >= 1, 'spot_take_success counted');
  assert(timeline.summary.spotTakeFailCount >= 1, 'spot_take_fail counted');
  assert(timeline.summary.spotReleaseCount >= 1, 'spot_release counted');
  assert(timeline.summary.pondFullRejectCount >= 1, 'pond_full_reject counted');
  assert(timeline.summary.botEvictedForHumanCount >= 1, 'bot_evicted_for_human counted');
  assert(timeline.summary.biteTickMissCount >= 1, 'bite_tick_miss counted when present');
  assert(timeline.summary.biteTickHitCount >= 1, 'bite_tick_hit counted when present');
  assert(timeline.summary.pendingCatchCreatedCount >= 1, 'pending_catch_created counted');
  assert(timeline.summary.baitDepletedCount >= 1, 'bait_depleted counted');
}

function testPhaseTransitionInvalidDetection(): void {
  console.log('\n=== TC: phase_transition_invalid detection ===');
  const playerId = `verify-obs-invalid-${Date.now()}`;
  const socketId = `sock-invalid-${Date.now()}`;
  const joined = joinPond(socketId, POND_ID, '钓友非法迁移', playerId);
  assert(joined.ok, 'join pond');

  // idle -> hooked 属于非法迁移，应该只告警不阻断流程
  scheduleHookFromBite(mockIo, POND_ID, joined.user.id, socketId, {
    fish: {
      id: `verify-fish-invalid-${Date.now()}`,
      pondId: POND_ID,
      spotId: SPOT_ID,
      speciesId: 'carp',
      quality: 'gray',
      sizeM: 0.2,
      bornAt: Date.now(),
      generation: 1,
    },
    escaped: false,
    hookDurationMs: 500,
  });

  const timeline = getPlayerFishingTimeline(playerId, 1, 100);
  assert(timeline.summary.phaseTransitionInvalidCount >= 1, 'phase_transition_invalid counted');
}

function testEventTypeAliasesAndSessionRebound(): void {
  console.log('\n=== TC: eventType aliases + session_rebound path ===');
  const playerId = `verify-obs-alias-${Date.now()}`;

  recordFishingMetric('socket_disconnect', {
    playerId,
    pondId: POND_ID,
    payload: { reason: 'socket_disconnect' },
  });
  recordFishingMetric('disconnect', {
    playerId,
    pondId: POND_ID,
    payload: { reason: 'legacy_disconnect' },
  });
  recordFishingMetric('pending_catch_accept', {
    playerId,
    pondId: POND_ID,
    payload: { speciesId: 'carp', quality: 'gray', sizeM: 0.32 },
  });
  recordFishingMetric('catch_accept', {
    playerId,
    pondId: POND_ID,
    payload: { speciesId: 'carp', quality: 'gray', sizeM: 0.28 },
  });

  const timeline = getPlayerFishingTimeline(playerId, 1, 50);
  assert(timeline.summary.disconnectCount >= 2, 'socket_disconnect + disconnect merged');
  const acceptEvents = timeline.events.filter(
    (e) => e.eventType === 'pending_catch_accept' || e.eventType === 'catch_accept',
  );
  assert(acceptEvents.length >= 2, 'pending_catch_accept + catch_accept recorded');
  assert(
    getFishingMetricsSummary(1).catchCount >= 2,
    'catch_accept aliases merged in metrics summary',
  );

  // session_rebound 仅为结构化日志路径，phase2 拆分后落在 lifecycle/pond handlers
  const lifecycleSrc = fs.readFileSync(path.join(rootDir, 'server/src/socketLifecycle.ts'), 'utf8');
  const pondHandlersSrc = fs.readFileSync(path.join(rootDir, 'server/src/socketPondHandlers.ts'), 'utf8');
  const registrySrc = fs.readFileSync(path.join(rootDir, 'server/src/sessionRegistry.ts'), 'utf8');
  assert(
    lifecycleSrc.includes("logStructuredEvent('session_rebound'") ||
      pondHandlersSrc.includes("logStructuredEvent('session_rebound'"),
    'session_rebound log path exists',
  );
  assert(
    registrySrc.includes("reason: 'session_registry_bind_player'") ||
      lifecycleSrc.includes("reason: 'session_registry_bind_player'") ||
      lifecycleSrc.includes("reason: 'register_player'"),
    'session_rebound register_player reason',
  );
  assert(pondHandlersSrc.includes("reason: 'join_pond_reconnect'"), 'session_rebound join_pond_reconnect reason');
}

function testDL215HookEscapeCounters(): void {
  console.log('\n=== TC: D-L2-15 hook/escape session counters ===');
  assert(!isBiteTickPersistEnabled(), 'METRICS_BITE_TICK_PERSIST default off');

  const sm = fs.readFileSync(path.join(rootDir, 'server/src/fishingStateMachine.ts'), 'utf8');
  assert(sm.includes('isBiteTickPersistEnabled()'), 'tick writes gated by persist flag');
  assert(sm.includes('noteBiteHook'), 'bite_hook increments session counters');
  assert(sm.includes('noteBiteEscape'), 'escape increments session counters');

  const playerId = `verify-dl215-${Date.now()}`;
  const socketId = `sock-dl215-${Date.now()}`;
  const joined = joinPond(socketId, POND_ID, '钓友DL215', playerId);
  assert(joined.ok, 'join pond');
  const start = startFishing(socketId, POND_ID, SPOT_ID);
  assert(start.ok, 'take spot');
  const userId = start.user.id;

  resetBiteSessionCounters(userId);
  noteBiteMiss(userId);
  noteBiteMiss(userId);
  noteBiteMiss(userId);

  scheduleHookFromBite(mockIo, POND_ID, userId, socketId, {
    fish: {
      id: `verify-fish-dl215-${Date.now()}`,
      pondId: POND_ID,
      spotId: SPOT_ID,
      speciesId: 'carp',
      quality: 'gray',
      sizeM: 0.25,
      bornAt: Date.now(),
      generation: 1,
    },
    escaped: false,
    hookDurationMs: 500,
  });

  const countersAfterEscape = noteBiteEscape(userId);
  recordFishingMetric('escape', {
    playerId,
    pondId: POND_ID,
    payload: {
      sessionHooks: countersAfterEscape.sessionHooks,
      sessionEscapes: countersAfterEscape.sessionEscapes,
      sessionMissTicks: countersAfterEscape.sessionMissTicks,
    },
  });

  const timeline = getPlayerFishingTimeline(playerId, 1, 50);
  const hookEv = timeline.events.find((e) => e.eventType === 'bite_hook');
  assert(!!hookEv, 'bite_hook recorded');
  assert(Number(hookEv!.payload.sessionHooks) >= 1, 'bite_hook.sessionHooks >= 1');
  assert(Number(hookEv!.payload.sessionMissTicks) >= 3, 'bite_hook.sessionMissTicks from memory');
  assert(
    Number(hookEv!.payload.missTicksSinceLastHook) >= 3,
    'bite_hook.missTicksSinceLastHook present',
  );

  const escEv = timeline.events.find((e) => e.eventType === 'escape');
  assert(!!escEv, 'escape recorded');
  assert(Number(escEv!.payload.sessionEscapes) >= 1, 'escape.sessionEscapes >= 1');
  assert(Number(escEv!.payload.sessionHooks) >= 1, 'escape.sessionHooks present');

  clearBiteSessionCounters(userId);
  removeDisconnectedUser(POND_ID, userId);
}

function testPendingTimeoutVerifyArtifacts(): void {
  console.log('\n=== TC: pending-timeout verify artifacts ===');
  const verifyScript = path.join(rootDir, 'scripts/verify-pending-timeout.ts');
  const pkgPath = path.join(rootDir, 'package.json');
  assert(fs.existsSync(verifyScript), 'scripts/verify-pending-timeout.ts exists');
  const pkg = JSON.parse(fs.readFileSync(pkgPath, 'utf8')) as { scripts?: Record<string, string> };
  assert(
    typeof pkg.scripts?.['verify:pending-timeout'] === 'string',
    'package.json has verify:pending-timeout script',
  );
}

function main(): void {
  console.log('verify-server-observability');
  testJoinEventsInTimeline();
  testDisconnectReconnectPhaseTransitions();
  testHookedReconnectTimeline();
  testP1ObservabilitySummary();
  testPhaseTransitionInvalidDetection();
  testEventTypeAliasesAndSessionRebound();
  testDL215HookEscapeCounters();
  testPendingTimeoutVerifyArtifacts();
  console.log('\nAll server observability checks passed.');
}

main();
