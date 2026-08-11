/**
 * 阶段 3/4 验收：leave_pond metric + player 时间线 API
 * 运行: npm run verify:afk-diag
 */
import '../server/src/db.js';
import { joinPond, leavePond, getUserById } from '../server/src/gameState.js';
import {
  getPlayerFishingTimeline,
  recordFishingMetric,
} from '../server/src/fishingMetrics.js';

const POND_ID = 'pond-calm';

function assert(cond: boolean, msg: string): void {
  if (!cond) throw new Error(`FAIL: ${msg}`);
  console.log(`  OK: ${msg}`);
}

function testPlayerTimelineSummary(): void {
  console.log('\n=== TC: player fishing timeline ===');
  const playerId = `verify-timeline-${Date.now()}`;
  const socketId = `sock-timeline-${Date.now()}`;

  const joined = joinPond(socketId, POND_ID, '钓友', playerId);
  assert(joined.ok, 'join pond');
  const userId = joined.user.id;

  recordFishingMetric('disconnect', { playerId, pondId: POND_ID, payload: { spotId: 'calm-spot-1' } });
  recordFishingMetric('reconnect', { playerId, pondId: POND_ID });
  recordFishingMetric('leave_pond', {
    playerId,
    pondId: POND_ID,
    payload: { reason: 'navigation_back', spotId: null, fishingPhase: 'idle' },
  });

  const timeline = getPlayerFishingTimeline(playerId, 1, 100);
  assert(timeline.playerId === playerId, 'playerId matches');
  assert(timeline.events.length >= 3, 'has recorded events');
  assert(timeline.summary.disconnectCount >= 1, 'disconnect count');
  assert(timeline.summary.reconnectCount >= 1, 'reconnect count');
  assert(timeline.summary.leavePondCount >= 1, 'leave_pond count');

  const ordered = timeline.events.every(
    (e, i) => i === 0 || e.createdAt >= timeline.events[i - 1]!.createdAt,
  );
  assert(ordered, 'events ordered ASC by createdAt');

  const leave = timeline.events.find((e) => e.eventType === 'leave_pond');
  assert(leave?.payload.reason === 'navigation_back', 'leave_pond reason in payload');

  leavePond(socketId);
  assert(getUserById(POND_ID, userId) === undefined, 'cleanup leave pond');
}

function main(): void {
  console.log('verify-afk-diag');
  testPlayerTimelineSummary();
  console.log('\nAll afk diag checks passed.');
}

main();
