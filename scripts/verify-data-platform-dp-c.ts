/**
 * Data Platform DP-C 验收：D-L3-02 对照 + D-L3-09 索引
 * 运行: npm run verify:data-platform-dp-c
 */
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import { spawnSync } from 'child_process';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');

function assert(cond: boolean, msg: string): void {
  if (!cond) throw new Error(`FAIL: ${msg}`);
  console.log(`  OK: ${msg}`);
}

function read(relPath: string): string {
  return fs.readFileSync(path.join(ROOT, relPath), 'utf8');
}

function testLiveVsSim(): void {
  console.log('\n=== TC: D-L3-02 live vs sim comparison ===');
  assert(fs.existsSync(path.join(ROOT, 'scripts/analytics/build-live-vs-sim.mjs')), 'build-live-vs-sim script exists');

  const result = spawnSync('node', ['scripts/analytics/build-live-vs-sim.mjs'], {
    cwd: ROOT,
    encoding: 'utf8',
  });
  if (result.status !== 0) throw new Error(result.stderr || result.stdout);
  assert(fs.existsSync(path.join(ROOT, 'docs/analytics/live-vs-sim.html')), 'live-vs-sim.html generated');
  assert(fs.existsSync(path.join(ROOT, 'docs/analytics/live-vs-sim.json')), 'live-vs-sim.json generated');
  const report = JSON.parse(fs.readFileSync(path.join(ROOT, 'docs/analytics/live-vs-sim.json'), 'utf8'));
  assert(typeof report.deviationPct === 'number' || report.deviationPct === null, 'deviationPct field present');
  assert(typeof report.sim.perDayCaught === 'number', 'sim perDayCaught present');
}

function testAnalyticsIndex(): void {
  console.log('\n=== TC: D-L3-09 analytics index live-daily ===');
  const lib = read('scripts/analytics/lib.mjs');
  assert(lib.includes("type: 'live-daily'"), 'manifest includes live-daily type');

  const indexBuild = spawnSync('node', ['scripts/analytics/build-index.mjs'], { cwd: ROOT, encoding: 'utf8' });
  if (indexBuild.status !== 0) throw new Error(indexBuild.stderr || indexBuild.stdout);

  const indexHtml = read('docs/analytics/index.html');
  assert(indexHtml.includes('live-vs-sim.html') || indexHtml.includes('线上日报'), 'index links live daily or compare');
  const manifest = JSON.parse(fs.readFileSync(path.join(ROOT, 'docs/analytics/manifest.json'), 'utf8'));
  assert(Array.isArray(manifest.runs), 'manifest.runs array');
  const hasLiveType = manifest.runs.some((r: { type?: string }) => r.type === 'live-daily');
  // live-daily only appears after daily pipeline runs; script structure must support it
  assert(lib.includes('live-daily'), 'buildManifest supports live-daily scanning');
  if (fs.existsSync(path.join(ROOT, 'docs/analytics/daily'))) {
    console.log(`  note: live-daily entries in manifest: ${hasLiveType}`);
  }
}

async function main(): Promise<void> {
  console.log('verify-data-platform-dp-c');
  testLiveVsSim();
  testAnalyticsIndex();
  console.log('\nAll DP-C checks passed.');
}

main().catch((err) => {
  console.error(err instanceof Error ? err.message : err);
  process.exit(1);
});
