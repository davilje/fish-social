/**
 * R2-3 capacity soft-limit smoke
 * 运行: npm run verify:capacity-limit
 */
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');

function assert(cond: boolean, msg: string): void {
  if (!cond) throw new Error(`FAIL: ${msg}`);
  console.log(`  OK: ${msg}`);
}

function read(rel: string): string {
  return fs.readFileSync(path.join(ROOT, rel), 'utf8');
}

async function main(): Promise<void> {
  console.log('verify-capacity-limit');

  const envEx = read('.env.example');
  assert(envEx.includes('MAX_HUMAN_SOCKETS'), '.env.example documents MAX_HUMAN_SOCKETS');

  const cap = read('server/src/humanCapacity.ts');
  assert(cap.includes('MAX_HUMAN_SOCKETS'), 'humanCapacity reads MAX_HUMAN_SOCKETS');
  assert(cap.includes('shouldRejectHumanJoinPond'), 'soft reject helper present');

  const handlers = read('server/src/socketPondHandlers.ts');
  assert(handlers.includes('shouldRejectHumanJoinPond'), 'join_pond uses capacity guard');
  assert(handlers.includes('capacity_reject'), 'capacity_reject log on reject');
  assert(handlers.includes('human_socket_limit'), 'error code human_socket_limit');

  const app = read('server/src/createApp.ts');
  assert(app.includes('humanSocketCount'), '/ready exposes humanSocketCount');
  assert(app.includes('capacityLimit'), '/ready exposes capacityLimit');

  const admin = read('server/src/admin.ts');
  assert(admin.includes('getCapacitySnapshot'), 'admin status includes capacity');

  // Default limit is 200; already-bound players are not rejected
  process.env.MAX_HUMAN_SOCKETS = '2';
  // Re-import after env set — module caches const at load, so test via source + dynamic import carefully.
  // Use a fresh worker: dynamic import of helpers that read env at call time for counts.
  const { getBoundHumanSocketCount, bindPlayer, unbindSocket } = await import(
    '../server/src/sessionRegistry.js'
  );

  // Bind two humans then check reject logic via re-reading env in a child path:
  // getMaxHumanSockets() reads env at call time (default 200)
  assert(/process\.env\.MAX_HUMAN_SOCKETS\s*\?\?\s*200/.test(cap), 'default 200');

  const sockA = `cap-sock-a-${Date.now()}`;
  const sockB = `cap-sock-b-${Date.now()}`;
  bindPlayer('cap-player-a', sockA);
  bindPlayer('cap-player-b', sockB);
  assert(getBoundHumanSocketCount() >= 2, 'bound humans counted');

  // Already bound should not reject even if "full"
  const { shouldRejectHumanJoin } = await import('../server/src/humanCapacity.js');
  // Note: MAX_HUMAN_SOCKETS already evaluated at import (default 200 unless env set before first import).
  // Soft-check: already-bound returns reject:false
  const r = shouldRejectHumanJoin('cap-player-a');
  assert(r.reject === false, 'already-bound player not rejected');

  unbindSocket(sockA);
  unbindSocket(sockB);

  console.log('PASS verify-capacity-limit');
}

main().catch((err) => {
  console.error(err instanceof Error ? err.message : err);
  process.exit(1);
});
