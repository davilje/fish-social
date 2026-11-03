/**
 * Explicit L1 test-database rebuild. Run only after stopping the server and settling pending fish.
 * node --import tsx scripts/ops/rebuild-fish-ecology.ts --db /abs/test.sqlite --environment test --dry-run
 * node --import tsx scripts/ops/rebuild-fish-ecology.ts --db /abs/test.sqlite --environment test --backup-dir /abs/backups --batch entry-v1-20261104 --apply
 */
import path from 'node:path';
import { FISH_ENTRY_GROWTH_VERSION } from '@fish-social/shared';
import { assertDatabaseOffline, inspectEcologyRebuild, rebuildTestPondEcology } from '../../server/src/ecologyRebuild.js';
import { seedEcologyRebuild } from '../../server/src/ecologySeed.js';

function argument(name: string): string | undefined {
  const index = process.argv.indexOf(name);
  return index < 0 ? undefined : process.argv[index + 1];
}

async function main(): Promise<void> {
  const dbPath = argument('--db');
  const environment = argument('--environment');
  const apply = process.argv.includes('--apply');
  const dryRun = process.argv.includes('--dry-run');
  if (!dbPath || !path.isAbsolute(dbPath) || environment !== 'test' || apply === dryRun) {
    throw new Error('Specify --db /absolute/test.sqlite --environment test and exactly one of --dry-run or --apply');
  }
  assertDatabaseOffline(dbPath);
  if (dryRun) {
    console.log(JSON.stringify({ mode: 'dry-run', dbPath, ...inspectEcologyRebuild(dbPath),
      target: '40 formal ponds × 100 fish; novice pond uses its own configured initial stock',
      preservation: 'Player assets and history stay in the current DB; old active fish and pond state are archived',
    }, null, 2));
    return;
  }
  const backupDir = argument('--backup-dir');
  const batchId = argument('--batch');
  if (!backupDir || !path.isAbsolute(backupDir) || !batchId) throw new Error('--apply requires --backup-dir /absolute/path and --batch unique-id');
  const result = await rebuildTestPondEcology({ dbPath, backupDir, batchId, environment, seed: seedEcologyRebuild, ruleVersion: FISH_ENTRY_GROWTH_VERSION });
  console.log(JSON.stringify({ mode: 'apply', dbPath, ...result }, null, 2));
}

main().catch(error => { console.error(error instanceof Error ? error.message : String(error)); process.exitCode = 1; });
