/**
 * Admin 排障增强 v1.1 MVP 验收
 * 运行: npm run verify:admin-observability
 */
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import { isFishingActive, type PondUser } from '@fish-social/shared';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');

function assert(cond: boolean, msg: string): void {
  if (!cond) throw new Error(`FAIL: ${msg}`);
  console.log(`  OK: ${msg}`);
}

function read(rel: string): string {
  return fs.readFileSync(path.join(ROOT, rel), 'utf8');
}

function testSourceGuards(): void {
  console.log('\n=== TC: source guards ===');
  const inspector = read('server/src/liveSessionInspector.ts');
  assert(!inspector.includes('lastEvent.createdAt'), 'Live Inspector no longer uses lastEvent.createdAt for fishingMs');
  assert(inspector.includes('getPlayerLiveState'), 'Live Inspector uses getPlayerLiveState');
  assert(inspector.includes("type: 'tick'"), 'SSE tick payload type');

  const admin = read('server/src/admin.ts');
  assert(admin.includes("/api/admin/players/:playerId/live-state"), 'live-state route registered');

  const debug = read('server/src/fishingDebug.ts');
  assert(debug.includes('fishingStartedAt'), 'activeFishers includes fishingStartedAt');
  assert(debug.includes('sessionFishingMs'), 'activeFishers includes sessionFishingMs');

  const checkpoint = read('server/src/playerPondSession.ts');
  assert(checkpoint.includes('ensureFishingStartedAt'), 'applyCheckpointToUser calls ensureFishingStartedAt');

  const sm = read('server/src/fishingStateMachine.ts');
  assert(sm.includes('ensureFishingStartedAt'), 'state machine uses ensureFishingStartedAt');

  const pondsPage = read('admin-web/src/pages/PondsPage.tsx');
  assert(
    pondsPage.includes('summary?.fishCount') || pondsPage.includes('summary.fishCount') || pondsPage.includes('humanCount'),
    'PondsPage uses summary.fishCount or humanCount',
  );

  const bh = read('admin-web/src/pages/BusinessHealthPage.tsx');
  assert(bh.includes('dateKey') && bh.includes('totalCatch'), 'BusinessHealthPage uses dateKey/totalCatch');

  const api = read('admin-web/src/api.ts');
  assert(api.includes('liveState'), 'api.ts has liveState helper');

  const schema = read('shared/metrics-schema.ts');
  assert(schema.includes("eventType: 'server_start'"), 'metrics schema has server_start');
  assert(schema.includes("eventType: 'server_stop'"), 'metrics schema has server_stop');

  const index = read('server/src/index.ts');
  assert(index.includes("recordFishingMetric('server_start'"), 'index emits server_start');
  assert(index.includes("recordFishingMetric('server_stop'"), 'index emits server_stop');
}

function testV12SourceGuards(): void {
  console.log('\n=== TC: ADMIN-OBS-1.2 source guards ===');
  const colors = read('admin-web/src/sopEventColors.ts');
  assert(colors.includes('disconnect_timeout'), 'SOP colors cover disconnect_timeout');
  assert(colors.includes('server_start'), 'SOP colors cover server_start');
  assert(colors.includes('leave_pond'), 'SOP colors cover leave_pond');
  assert(colors.includes('checkpoint_restore') || colors.includes('joinKind'), 'SOP colors cover checkpoint');

  const timeline = read('admin-web/src/pages/TimelinePage.tsx');
  assert(timeline.includes('sopEventRowClass'), 'TimelinePage uses SOP colors');

  const inspector = read('admin-web/src/pages/LiveInspectorPage.tsx');
  assert(inspector.includes('sopEventRowClass'), 'LiveInspector uses SOP colors');
  assert(inspector.includes('banner-error') || inspector.includes('missing_fishing_started_at'), 'Inspector timing-risk banner');
  assert(inspector.includes('Checkpoint'), 'Inspector checkpoint summary');

  const debug = read('admin-web/src/pages/FishingDebugPage.tsx');
  assert(debug.includes('humansOnly') || debug.includes('仅真人'), 'FishingDebug humans-only filter');
  assert(debug.includes('activeFishers'), 'FishingDebug activeFishers table');

  const ponds = read('admin-web/src/pages/PondsPage.tsx');
  assert(ponds.includes('仅真人') || ponds.includes('humansOnly'), 'PondsPage humans-only filter');

  const mobile = read('mobile/components/AdminPondFishDebugGrid.tsx');
  assert(mobile.includes('activeFishers'), 'mobile Debug renders activeFishers');
  assert(mobile.includes('fishingStartedAt'), 'mobile Debug shows fishingStartedAt');
  assert(mobile.includes('sessionFishingMs'), 'mobile Debug shows sessionFishingMs');
  assert(mobile.includes('仅真人') || mobile.includes('humansOnly'), 'mobile humans-only filter');

  const css = read('admin-web/src/styles.css');
  assert(css.includes('row-server'), 'styles have row-server for process events');
}

async function testEnsureFishingStartedAt(): Promise<void> {
  console.log('\n=== TC: ensureFishingStartedAt behavior ===');
  const { ensureFishingStartedAt } = await import('../server/src/fishingStartedAt.js');
  const { getPlayerLiveState } = await import('../server/src/playerLiveState.js');

  const user = {
    id: 'u1',
    playerId: 'p-obs-test',
    nickname: '测',
    color: '#000',
    status: 'fishing',
    spotId: 'calm-spot-1',
    fishingPhase: 'waiting',
    fishingStartedAt: null,
    todayFishingMs: 0,
  } as PondUser;

  assert(isFishingActive(user.fishingPhase), 'waiting is active phase');
  ensureFishingStartedAt(user);
  assert(user.fishingStartedAt != null, 'ensure sets fishingStartedAt for waiting');

  const idle = { ...user, fishingPhase: 'idle' as const, status: 'idle' as const, fishingStartedAt: null };
  ensureFishingStartedAt(idle);
  assert(idle.fishingStartedAt == null, 'ensure does not set anchor for idle');

  const live = getPlayerLiveState('nonexistent-player-obs');
  assert(live.found === false, 'unknown player found=false');
  assert(live.diagnostics.some((d) => d.id === 'player_not_in_pond'), 'player_not_in_pond diagnostic');
  assert(typeof live.server.startedAt === 'number', 'server.startedAt present');
  assert(typeof live.server.uptimeSec === 'number', 'server.uptimeSec present');
}

async function testMissingAnchorDiagnostic(): Promise<void> {
  console.log('\n=== TC: missing_fishing_started_at diagnostic via memory user ===');
  const { createHumanPondUser, updatePondUser, removeDisconnectedUser } = await import(
    '../server/src/pondUserManager.js',
  );
  const { getPlayerLiveState } = await import('../server/src/playerLiveState.js');

  const playerId = `obs-diag-${Date.now()}`;
  const user = createHumanPondUser('pond-calm', playerId, 'Diag');
  user.status = 'fishing';
  user.fishingPhase = 'waiting';
  user.spotId = 'calm-spot-1';
  user.fishingStartedAt = null;
  updatePondUser('pond-calm', user);

  const live = getPlayerLiveState(playerId);
  assert(live.found, 'test user found in pond');
  assert(
    live.diagnostics.some((d) => d.id === 'missing_fishing_started_at'),
    'diagnostics includes missing_fishing_started_at',
  );

  const { ensureFishingStartedAt } = await import('../server/src/fishingStartedAt.js');
  ensureFishingStartedAt(user);
  updatePondUser('pond-calm', user);
  const fixed = getPlayerLiveState(playerId);
  assert(fixed.user?.fishingStartedAt != null, 'after ensure, fishingStartedAt set');
  assert(
    !fixed.diagnostics.some((d) => d.id === 'missing_fishing_started_at'),
    'missing_fishing_started_at cleared after ensure',
  );

  removeDisconnectedUser('pond-calm', user.id);
}

async function main(): Promise<void> {
  console.log('verify-admin-observability-v1');
  testSourceGuards();
  testV12SourceGuards();
  await testEnsureFishingStartedAt();
  await testMissingAnchorDiagnostic();
  console.log('\nAll admin observability v1.1 + v1.2 checks passed.');
}

main().catch((err) => {
  console.error(err instanceof Error ? err.message : err);
  process.exit(1);
});
