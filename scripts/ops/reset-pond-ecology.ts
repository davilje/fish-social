/**
 * 清空并按当前表规则重种所有鱼塘（pond_fish / pond_state / spot_bite_weights）。
 *
 * 建议先停游戏服务端，避免并发写。
 *
 *   npx tsx scripts/ops/reset-pond-ecology.ts --dry-run
 *   npx tsx scripts/ops/reset-pond-ecology.ts --apply
 */
import '../../server/src/db.js';
import { db } from '../../server/src/db.js';
import { FISH_QUALITIES, listGamePonds } from '@fish-social/shared';
import { resetAllEcology } from '../../server/src/pondEcology.js';

const dryRun = process.argv.includes('--dry-run');
const apply = process.argv.includes('--apply');

if (!dryRun && !apply) {
  console.error('Usage: npx tsx scripts/ops/reset-pond-ecology.ts --dry-run | --apply');
  process.exit(1);
}

function emptyQuality(): Record<string, number> {
  return Object.fromEntries(FISH_QUALITIES.map((q) => [q.id, 0])) as Record<string, number>;
}

function countByQuality(): Record<string, number> {
  const out = emptyQuality();
  const rows = db
    .prepare('SELECT quality, COUNT(*) AS c FROM pond_fish GROUP BY quality')
    .all() as Array<{ quality: string; c: number }>;
  for (const row of rows) out[row.quality] = row.c;
  return out;
}

const beforeTotal = (db.prepare('SELECT COUNT(*) AS c FROM pond_fish').get() as { c: number }).c;
console.log(`[reset-pond-ecology] ponds=${listGamePonds().length} fish=${beforeTotal}`);
console.log('  before byQuality=', countByQuality());

if (dryRun) {
  console.log('[dry-run] would DELETE pond_fish / pond_state / spot_bite_weights then initPondEcology()');
  process.exit(0);
}

resetAllEcology();

console.log(
  `[apply] reseeding done. fish=${(db.prepare('SELECT COUNT(*) AS c FROM pond_fish').get() as { c: number }).c}`,
);
console.log('  after byQuality=', countByQuality());

const perPond = db
  .prepare(
    `SELECT pond_id AS pondId, quality, COUNT(*) AS c
     FROM pond_fish GROUP BY pond_id, quality ORDER BY pond_id, quality`,
  )
  .all() as Array<{ pondId: string; quality: string; c: number }>;

const byPond = new Map<string, Record<string, number>>();
for (const row of perPond) {
  if (!byPond.has(row.pondId)) byPond.set(row.pondId, emptyQuality());
  byPond.get(row.pondId)![row.quality] = row.c;
}

for (const pond of listGamePonds()) {
  const q = byPond.get(pond.pondId) ?? emptyQuality();
  const pop = FISH_QUALITIES.reduce((s, x) => s + (q[x.id] ?? 0), 0);
  console.log(
    `  ${pond.pondId.padEnd(14)} pop=${String(pop).padStart(3)}/${pond.maxPopulation}` +
      ` gray=${q.gray ?? 0} green=${q.green ?? 0} blue=${q.blue ?? 0}` +
      ` purple=${q.purple ?? 0} red=${q.red ?? 0} orange=${q.orange ?? 0} gold=${q.gold ?? 0}`,
  );
}
