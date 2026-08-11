/**
 * 日末鱼塘生态快照 → daily_pond_ecology + ecology-snapshot.json
 * 用法: node scripts/analytics/ecology-daily-snapshot.mjs --date=YYYY-MM-DD
 */
import Database from 'better-sqlite3';
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import { parseDateArg } from './date-utils.mjs';
import { POND_IDS, maxPopulation } from './pond-config.mjs';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const projectRoot = path.resolve(__dirname, '../..');
const dbPath = process.env.DB_PATH ?? path.join(projectRoot, 'data/fish-social.db');
const reportDir = path.join(projectRoot, 'docs/analytics/daily');

const dateKey = parseDateArg();
const outDir = path.join(reportDir, dateKey);
fs.mkdirSync(outDir, { recursive: true });

const db = new Database(dbPath);
db.pragma('journal_mode = WAL');

db.exec(`
  CREATE TABLE IF NOT EXISTS daily_pond_ecology (
    date_key TEXT NOT NULL,
    pond_id TEXT NOT NULL,
    population INTEGER NOT NULL DEFAULT 0,
    max_population INTEGER NOT NULL DEFAULT 0,
    pop_ratio REAL,
    quality_json TEXT NOT NULL DEFAULT '{}',
    avg_size_m REAL,
    PRIMARY KEY (date_key, pond_id)
  );
`);

const hasPondFish = db
  .prepare("SELECT name FROM sqlite_master WHERE type='table' AND name='pond_fish'")
  .get();

const qualityRows = hasPondFish
  ? db
      .prepare(
        `SELECT pond_id, quality, COUNT(*) as cnt FROM pond_fish GROUP BY pond_id, quality`,
      )
      .all()
  : [];

const popRows = hasPondFish
  ? db
      .prepare(
        `SELECT pond_id, COUNT(*) as population, AVG(size_m) as avg_size_m
         FROM pond_fish GROUP BY pond_id`,
      )
      .all()
  : [];

const qualityByPond = {};
for (const r of qualityRows) {
  if (!qualityByPond[r.pond_id]) qualityByPond[r.pond_id] = {};
  qualityByPond[r.pond_id][r.quality] = r.cnt;
}

const popByPond = Object.fromEntries(popRows.map((r) => [r.pond_id, r]));

const ponds = POND_IDS.map((pondId) => {
  const max = maxPopulation(pondId) ?? 0;
  const pop = popByPond[pondId]?.population ?? 0;
  const avgSizeM = popByPond[pondId]?.avg_size_m != null ? Math.round(popByPond[pondId].avg_size_m * 1000) / 1000 : null;
  const popRatio = max > 0 ? Math.round((pop / max) * 1000) / 10 : null;
  const byQuality = qualityByPond[pondId] ?? {};
  return { pondId, population: pop, maxPopulation: max, popRatio, byQuality, avgSizeM };
});

const upsert = db.prepare(`
  INSERT OR REPLACE INTO daily_pond_ecology
    (date_key, pond_id, population, max_population, pop_ratio, quality_json, avg_size_m)
  VALUES (@dateKey, @pondId, @population, @maxPopulation, @popRatio, @qualityJson, @avgSizeM)
`);

for (const p of ponds) {
  upsert.run({
    dateKey,
    pondId: p.pondId,
    population: p.population,
    maxPopulation: p.maxPopulation,
    popRatio: p.popRatio,
    qualityJson: JSON.stringify(p.byQuality),
    avgSizeM: p.avgSizeM,
  });
}

const snapshot = {
  dateKey,
  capturedAt: new Date().toISOString(),
  source: hasPondFish ? 'pond_fish' : 'empty',
  note: hasPondFish
    ? '日批运行时 pond_fish 当前态（非历史时点回放）'
    : 'pond_fish 表不存在，四塘填 0',
  ponds,
};

const outPath = path.join(outDir, 'ecology-snapshot.json');
fs.writeFileSync(outPath, JSON.stringify(snapshot, null, 2), 'utf8');
console.log(`[ecology-daily-snapshot] ${dateKey}: ${ponds.length} ponds → ${outPath}`);
db.close();
