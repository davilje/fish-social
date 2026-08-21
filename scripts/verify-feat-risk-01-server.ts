/**
 * FEAT-RISK-01 server smoke: bans, fine-to-zero, raid escape/timeout.
 * Run: npm run verify:feat-risk-01-server
 */
import '../server/src/db.js';
import { db } from '../server/src/db.js';
import { addCoins, deductCoinsUpTo, ensurePlayer, getPlayer } from '../server/src/players.js';
import {
  checkForbiddenPondBan,
  getActivePoliceRaid,
  notePoliceLeaveIfNeeded,
  resetPoliceStateForTests,
  resolveExpiredRaids,
  startPoliceRaidForUser,
  upsertForbiddenBan,
} from '../server/src/forbiddenPolice.js';
import { POLICE_ESCAPE_BAN_MS } from '@fish-social/shared';

const id = 'test-risk-01-smoke';
const pondId = 'pond-ridge';

db.prepare('DELETE FROM player_forbidden_bans WHERE player_id = ?').run(id);
db.prepare('DELETE FROM players WHERE player_id = ?').run(id);
resetPoliceStateForTests();

ensurePlayer(id, 'RiskTester');
addCoins(id, 200);

const shortBan = Date.now() + 60_000;
upsertForbiddenBan(id, pondId, shortBan, 'escape');
const blocked = checkForbiddenPondBan(id, pondId);
if (blocked.ok) throw new Error('expected 2h escape ban to block join');
if (!blocked.error.includes('2 小时')) throw new Error(blocked.error);

db.prepare('DELETE FROM player_forbidden_bans WHERE player_id = ?').run(id);
const fineUntil = Date.now() + 24 * 60 * 60 * 1000;
upsertForbiddenBan(id, pondId, fineUntil, 'fine');
const dayBan = checkForbiddenPondBan(id, pondId);
if (dayBan.ok) throw new Error('expected day fine ban to block join');
if (!dayBan.error.includes('今日禁止')) throw new Error(dayBan.error);

db.prepare('DELETE FROM player_forbidden_bans WHERE player_id = ?').run(id);

const fine = deductCoinsUpTo(id, 800);
if (fine.charged !== 200) throw new Error(`expected charge 200, got ${fine.charged}`);
if (fine.coinsAfter !== 0) throw new Error(`expected coins 0, got ${fine.coinsAfter}`);
if ((getPlayer(id)?.coins ?? -1) !== 0) throw new Error('coins not zeroed');

resetPoliceStateForTests();
const started = startPoliceRaidForUser({
  playerId: id,
  pondId,
  userId: 'u-risk-1',
  socketId: 's-risk-1',
});
if (!started.ok) throw new Error(started.error);
if (!getActivePoliceRaid(id)) throw new Error('raid missing after start');

notePoliceLeaveIfNeeded(id, pondId);
if (getActivePoliceRaid(id)) throw new Error('raid should clear on escape');
const afterEscape = checkForbiddenPondBan(id, pondId);
if (afterEscape.ok) throw new Error('escape should ban for 2h');
const untilRow = db
  .prepare(`SELECT until_ms FROM player_forbidden_bans WHERE player_id = ? AND pond_id = ?`)
  .get(id, pondId) as { until_ms: number };
if (untilRow.until_ms < Date.now() + POLICE_ESCAPE_BAN_MS - 5_000) {
  throw new Error('escape ban shorter than 2h');
}

db.prepare('DELETE FROM player_forbidden_bans WHERE player_id = ?').run(id);
resetPoliceStateForTests();
addCoins(id, 900);
const timed = startPoliceRaidForUser({
  playerId: id,
  pondId,
  userId: 'u-risk-2',
  socketId: 's-risk-2',
  now: Date.now() - 11_000,
});
if (!timed.ok) throw new Error(timed.error);
resolveExpiredRaids();
if (getActivePoliceRaid(id)) throw new Error('raid should clear after timeout fine');
const afterFine = checkForbiddenPondBan(id, pondId);
if (afterFine.ok) throw new Error('timeout should day-ban the pond');
if ((getPlayer(id)?.coins ?? -1) !== 100) throw new Error('expected 800 fine from 900');

db.prepare('DELETE FROM player_forbidden_bans WHERE player_id = ?').run(id);
db.prepare('DELETE FROM players WHERE player_id = ?').run(id);
resetPoliceStateForTests();
console.log('FEAT-RISK-01 server smoke ok');
