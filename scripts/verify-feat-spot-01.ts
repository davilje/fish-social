/**
 * FEAT-SPOT-02: tag-matched spot_clue_texts + pond_spot_tags + wording guards.
 * Run: npm run verify:feat-spot-01
 */
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  filterSpotCluePool,
  parseSpotTags,
  pickSpotClueFromPool,
  validateSpotClueWording,
  type SpotClueTextDef,
} from '@fish-social/shared';

function section(name: string) {
  console.log(`\n== ${name} ==`);
}

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const textsPath = join(root, 'shared/generated/game-data/spot_clue_texts.json');
const tagsPath = join(root, 'shared/generated/game-data/pond_spot_tags.json');
const tagDefsPath = join(root, 'shared/generated/game-data/spot_tag_defs.json');

const rows = JSON.parse(readFileSync(textsPath, 'utf8')) as SpotClueTextDef[];
const spotRows = JSON.parse(readFileSync(tagsPath, 'utf8')) as Array<{
  pondId: string;
  spotId: string;
  tags: string;
}>;
const tagDefs = JSON.parse(readFileSync(tagDefsPath, 'utf8')) as Array<{ tagId: string }>;
const tagIds = new Set(tagDefs.map((t) => t.tagId));

section('tables');
assert.ok(Array.isArray(rows) && rows.length >= 100, 'spot_clue_texts size');
assert.ok(spotRows.length === 420, `pond_spot_tags rows=${spotRows.length}`);
assert.equal(tagIds.size, 22, 'spot_tag_defs count');

section('wording');
const issues: string[] = [];
for (const row of rows) {
  issues.push(...validateSpotClueWording(row));
}
assert.equal(issues.length, 0, issues.slice(0, 5).join('; '));

section('per-tag coverage');
const byTag = new Map<string, SpotClueTextDef[]>();
for (const row of rows) {
  const tag = row.spotTag?.trim();
  if (!tag) continue;
  const list = byTag.get(tag) ?? [];
  list.push(row);
  byTag.set(tag, list);
}
for (const tid of tagIds) {
  const list = byTag.get(tid) ?? [];
  assert.ok(list.some((r) => r.clueType === 'habitat'), `${tid} habitat`);
  assert.ok(
    list.some((r) => r.clueType === 'activity' && r.activitySignal?.startsWith('active')),
    `${tid} active activity`,
  );
  assert.ok(
    list.some((r) => r.clueType === 'activity' && r.activitySignal === 'inactive'),
    `${tid} inactive activity`,
  );
}

section('spot tags valid');
for (const spot of spotRows) {
  const tags = parseSpotTags(spot.tags);
  assert.ok(tags.length >= 4 && tags.length <= 6, `${spot.spotId} tag count`);
  for (const t of tags) assert.ok(tagIds.has(t), `${spot.spotId} unknown ${t}`);
}

section('tag-filtered pool');
const sample = spotRows.find((s) => s.pondId === 'pond-calm' && s.spotId === 'calm-spot-1')!;
const sampleTags = parseSpotTags(sample.tags);
const pool = filterSpotCluePool(rows, {
  playerLevel: 1,
  pondLevel: 1,
  pondCategory: 'advanced',
  spotTags: sampleTags,
});
assert.ok(pool.length >= 5, 'calm-spot-1 pool');
assert.ok(pool.every((r) => r.spotTag && sampleTags.includes(r.spotTag)));
assert.ok(pool.some((r) => r.clueType === 'habitat'));
assert.ok(pool.some((r) => r.clueType === 'activity'));

section('pick varies');
const seen = new Set<string>();
for (let i = 0; i < 30; i++) {
  const picked = pickSpotClueFromPool(pool, i * 11 + 3);
  if (picked) seen.add(picked.clueId);
}
assert.ok(seen.size >= 3, 'random varies');

console.log('\nFEAT-SPOT-02 tag-matched clues ok');
