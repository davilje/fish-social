import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { CURRENT_FISH_RULESET_ID, isCurrentSimulation, readCurrentSimulationReference,
  loadLatestCurrentSimulation } from './simulation-policy.mjs';

function fixture(t) {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'fish-sim-policy-'));
  t.after(() => fs.rmSync(dir, { recursive: true, force: true }));
  return dir;
}
function write(file, value) {
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, JSON.stringify(value));
}
const current = { rulesetId: CURRENT_FISH_RULESET_ID, modelKind: 'full-flow-fishing' };

test('a date/version alone does not promote growth-only or historical models to catch baselines', () => {
  assert.equal(isCurrentSimulation({ rulesVersion: 'v0.4.1' }), false);
  assert.equal(isCurrentSimulation({ ...current, modelKind: 'growth-restock-stress' }), false);
  assert.equal(isCurrentSimulation({ ...current, rulesetId: 'future-unconfirmed' }), false);
  assert.equal(isCurrentSimulation(current), true);
});

test('daily reference ignores old, malformed, absent, and wrong-scenario data; valid zero remains zero', t => {
  const file = path.join(fixture(t), 'analysis.json');
  assert.equal(readCurrentSimulationReference(file), null);
  write(file, { scenarioRows: [{ anglers: 5, perDayCaught: 118 }] });
  assert.equal(readCurrentSimulationReference(file), null);
  write(file, { ...current, scenarioRows: [{ anglers: 20, perDayCaught: 500 }] });
  assert.equal(readCurrentSimulationReference(file), null);
  write(file, { ...current, scenarioRows: [{ anglers: 5, perDayCaught: 0 }] });
  assert.equal(readCurrentSimulationReference(file), 0);
  fs.writeFileSync(file, '{invalid');
  assert.equal(readCurrentSimulationReference(file), null);
});

test('latest-run selection requires matching metadata and analysis, ignoring newer historical runs', t => {
  const root = fixture(t);
  const meta = { ...current, type: 'pond-day', rulesVersion: 'validated-fixture' };
  const analysis = { ...current, scenarioRows: [{ anglers: 5, perDayCaught: 24 }] };
  write(path.join(root, '2026-11-04-valid/meta.json'), meta);
  write(path.join(root, '2026-11-04-valid/analysis.json'), analysis);
  write(path.join(root, '2026-11-05-old/meta.json'), { type: 'pond-day', rulesVersion: 'v0.4.1' });
  write(path.join(root, '2026-11-05-old/analysis.json'), { scenarioRows: [{ anglers: 5, perDayCaught: 118 }] });
  write(path.join(root, '2026-11-06-mixed/meta.json'), meta);
  write(path.join(root, '2026-11-06-mixed/analysis.json'), { scenarioRows: [{ anglers: 5, perDayCaught: 999 }] });
  assert.equal(loadLatestCurrentSimulation(root).runId, '2026-11-04-valid');
  assert.equal(loadLatestCurrentSimulation(root).perDayCaught, 24);
});

test('no valid full-flow run produces no fallback baseline', t => {
  const root = fixture(t);
  write(path.join(root, 'legacy/meta.json'), { type: 'pond-day', rulesVersion: 'v0.4.1' });
  write(path.join(root, 'legacy/analysis.json'), { scenarioRows: [{ anglers: 5, perDayCaught: 118 }] });
  assert.equal(loadLatestCurrentSimulation(root), null);
});

for (const script of ['pond-day-pipeline.mjs', 'generate-analysis-report.mjs', 'generate-pond-day-report.mjs', 'archive-run.mjs']) {
  test(`retired ${script} fails with replacement guidance before reading/writing reports`, () => {
    const scriptPath = fileURLToPath(new URL(script, import.meta.url));
    const result = spawnSync(process.execPath, [scriptPath], { encoding: 'utf8' });
    assert.notEqual(result.status, 0);
    assert.match(result.stderr, /历史模拟入口已停用/);
    assert.match(result.stderr, /report:fishing-current/);
  });
}
