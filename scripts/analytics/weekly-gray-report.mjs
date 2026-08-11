/**
 * C7: 每周灰度指标报表（stdout / 可重定向推送）
 * 运行: npm run analytics:weekly-gray
 */
import { spawnSync } from 'child_process';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const projectRoot = path.resolve(__dirname, '../..');

const result = spawnSync(
  'npx',
  ['tsx', '-e', `
    import { getGrayMetricsDashboard } from './server/src/grayMetrics.js';
    const d = getGrayMetricsDashboard(168);
    console.log(JSON.stringify({
      generatedAt: new Date().toISOString(),
      grayReleasePercent: d.grayReleasePercent,
      rulesVersion: d.rulesVersion,
      abandonRate: d.abandonRate,
      catchCount: d.catchCount,
      faucetSinkRatio: d.faucetSinkRatio,
      alerts: d.alerts,
      business7d: d.businessHealth7d.totals,
    }, null, 2));
  `],
  { cwd: projectRoot, encoding: 'utf8', shell: true },
);

if (result.status !== 0) {
  console.error(result.stderr || result.stdout);
  process.exit(1);
}
console.log(result.stdout);
