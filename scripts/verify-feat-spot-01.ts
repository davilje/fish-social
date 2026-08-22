/**
 * FEAT-SPOT-01 (revised): spot_clue_texts library + filter/random rules.
 * Run: npm run verify:feat-spot-01
 */
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

type SpotClueText = {
  clueId: string;
  clueType: string;
  clueText: string;
  weight?: number;
  minPlayerLevel?: number;
  minPondLevel?: number;
  pondCategory?: string;
  spotTag?: string;
  enabled?: boolean;
};

type SpotTagRow = {
  pondId: string;
  spotId: string;
  tags: string;
};

function section(name: string) {
  console.log(`\n== ${name} ==`);
}

function tagMatches(clueTag: string | undefined, spotTags: Set<string>): boolean {
  if (!clueTag || !clueTag.trim()) return true;
  if (spotTags.size === 0) return true;
  return spotTags.has(clueTag.trim());
}

function filterPool(
  rows: SpotClueText[],
  opts: {
    playerLevel: number;
    pondLevel: number;
    pondCategory: string;
    spotTags: Set<string>;
  },
): SpotClueText[] {
  return rows.filter((row) => {
    if (row.enabled === false) return false;
    if (!row.clueText?.trim()) return false;
    if (opts.playerLevel < Math.max(0, row.minPlayerLevel ?? 0)) return false;
    if (opts.pondLevel < Math.max(0, row.minPondLevel ?? 0)) return false;
    if (row.pondCategory && row.pondCategory !== opts.pondCategory) return false;
    if (!tagMatches(row.spotTag, opts.spotTags)) return false;
    return true;
  });
}

function weightedPick(pool: SpotClueText[], roll: number): SpotClueText {
  let total = 0;
  for (const row of pool) total += Math.max(1, row.weight ?? 1);
  let r = ((roll % total) + total) % total;
  for (const row of pool) {
    r -= Math.max(1, row.weight ?? 1);
    if (r < 0) return row;
  }
  return pool[pool.length - 1]!;
}

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const textsPath = join(
  root,
  'fish-social-unity/Assets/Resources/GameData/spot_clue_texts.json',
);
const tagsPath = join(
  root,
  'fish-social-unity/Assets/Resources/GameData/spot_tags.json',
);
const rows = JSON.parse(readFileSync(textsPath, 'utf8')) as SpotClueText[];
const tags = JSON.parse(readFileSync(tagsPath, 'utf8')) as SpotTagRow[];

section('table');
assert.ok(Array.isArray(rows) && rows.length >= 20, 'spot_clue_texts seed size');
const habitats = rows.filter((r) => r.clueType === 'habitat' && r.enabled !== false);
const activities = rows.filter((r) => r.clueType === 'activity' && r.enabled !== false);
assert.ok(habitats.length >= 10, 'habitat clues');
assert.ok(activities.length >= 10, 'activity clues');
for (const row of rows) {
  assert.ok(row.clueId, 'clueId');
  assert.ok(row.clueType === 'habitat' || row.clueType === 'activity', row.clueId);
  assert.ok(typeof row.clueText === 'string' && row.clueText.trim().length > 0, row.clueId);
  assert.ok(!/bite|escape|0\.\d+|rate/i.test(row.clueText), `leak ${row.clueId}`);
}

section('tags');
assert.ok(Array.isArray(tags) && tags.length >= 1, 'spot_tags seed');

section('filter + types in pool');
const calmTags = new Set(
  (tags.find((t) => t.pondId === 'pond-calm' && t.spotId === 'calm-spot-1')?.tags ?? '')
    .split(',')
    .map((s) => s.trim())
    .filter(Boolean),
);
const pool = filterPool(rows, {
  playerLevel: 1,
  pondLevel: 1,
  pondCategory: 'advanced',
  spotTags: calmTags,
});
assert.ok(pool.length >= 5, 'unlocked pool');
assert.ok(pool.some((r) => r.clueType === 'habitat'), 'pool has habitat');
assert.ok(pool.some((r) => r.clueType === 'activity'), 'pool has activity');

section('weighted random varies');
const seen = new Set<string>();
for (let i = 0; i < 40; i++) {
  seen.add(weightedPick(pool, i * 7 + 3).clueId);
}
assert.ok(seen.size >= 3, 'random can vary across rolls');

console.log('\nFEAT-SPOT-01 revised table + filter ok');
