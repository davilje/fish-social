/** @deprecated 使用 scripts/analytics/generate-pond-day-report.mjs */
import { spawnSync } from 'child_process';
import path from 'path';
import { fileURLToPath } from 'url';

const script = path.join(path.dirname(fileURLToPath(import.meta.url)), 'analytics/generate-pond-day-report.mjs');
spawnSync(process.execPath, [script, ...process.argv.slice(2)], { stdio: 'inherit' });
