/**
 * Sprint 2 ARC-08～11 / BUG-08 工程验收
 * 运行: npm run verify:engineering
 */
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import { isFishingActive, type FishingPhase } from '@fish-social/shared';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');

function assert(cond: boolean, msg: string): void {
  if (!cond) throw new Error(`FAIL: ${msg}`);
  console.log(`  OK: ${msg}`);
}

function read(relPath: string): string {
  return fs.readFileSync(path.join(ROOT, relPath), 'utf8');
}

function lineCount(relPath: string): number {
  return read(relPath).split('\n').length;
}

function testGameStateSplit(): void {
  console.log('\n=== TC: ARC-08 gameState split ===');
  assert(fs.existsSync(path.join(ROOT, 'server/src/pondSession.ts')), 'pondSession.ts exists');
  assert(fs.existsSync(path.join(ROOT, 'server/src/pondUserManager.ts')), 'pondUserManager.ts exists');
  assert(fs.existsSync(path.join(ROOT, 'server/src/pondChat.ts')), 'pondChat.ts exists');

  const facade = read('server/src/gameState.ts');
  const facadeLines = lineCount('server/src/gameState.ts');
  assert(facadeLines <= 20, `gameState.ts is thin facade (${facadeLines} lines)`);
  assert(facade.includes("export * from './pondSession.js'"), 're-exports pondSession');
  assert(facade.includes("export * from './pondUserManager.js'"), 're-exports pondUserManager');
  assert(facade.includes("export * from './pondChat.js'"), 're-exports pondChat');

  const session = read('server/src/pondSession.ts');
  assert(session.includes('joinPond'), 'pondSession owns joinPond');
  assert(session.includes('leavePond'), 'pondSession owns leavePond');
  assert(!session.includes('pondChats'), 'pondSession has no chat storage');

  const users = read('server/src/pondUserManager.ts');
  assert(users.includes('buildSnapshot'), 'pondUserManager owns buildSnapshot');
  assert(users.includes('ensurePondUsers'), 'pondUserManager owns user index');

  const chat = read('server/src/pondChat.ts');
  assert(chat.includes('appendChatMessage'), 'pondChat owns chat append');
  assert(chat.includes('postAnnouncement'), 'pondChat owns announcements');
}

async function testReExports(): Promise<void> {
  console.log('\n=== TC: ARC-08 facade re-exports ===');
  const gs = await import('../server/src/gameState.js');
  const required = [
    'joinPond',
    'leavePond',
    'buildSnapshot',
    'postAnnouncement',
    'detachPondUserForCheckpointTest',
    'getSession',
    'listUsersInPond',
  ] as const;
  for (const name of required) {
    assert(typeof (gs as Record<string, unknown>)[name] === 'function', `${name} exported from gameState facade`);
  }
}

function testNoCircularImports(): void {
  console.log('\n=== TC: ARC-08 import boundaries ===');
  const session = read('server/src/pondSession.ts');
  const users = read('server/src/pondUserManager.ts');
  const chat = read('server/src/pondChat.ts');
  assert(!chat.includes('pondSession'), 'pondChat does not import pondSession');
  assert(!chat.includes('pondUserManager'), 'pondChat does not import pondUserManager');
  assert(!users.includes('pondSession'), 'pondUserManager does not import pondSession');
  assert(session.includes('pondUserManager'), 'pondSession imports pondUserManager');
  assert(session.includes('pondChat'), 'pondSession imports pondChat');
}

function grepServerHandlersForBareLogs(): string[] {
  const hits: string[] = [];
  const skip = new Set(['logger.ts', 'errorLog.ts']);
  const walk = (dir: string) => {
    for (const name of fs.readdirSync(dir)) {
      const full = path.join(dir, name);
      if (fs.statSync(full).isDirectory()) walk(full);
      else if (name.endsWith('.ts') && !name.endsWith('.test.ts') && !skip.has(name)) {
        const content = fs.readFileSync(full, 'utf8');
        if (!content.includes("from './logger.js'") && !content.includes('from "./logger.js"')) continue;
        if (/\blogInfo\(/.test(content) || /\blogWarn\(/.test(content)) {
          hits.push(path.relative(path.join(ROOT, 'server/src'), full).replace(/\\/g, '/'));
        }
      }
    }
  };
  walk(path.join(ROOT, 'server/src'));
  return hits;
}

function testArc09(): void {
  console.log('\n=== TC: ARC-09 unified logging API ===');
  assert(read('server/src/logger.ts').includes('@deprecated'), 'legacy log APIs deprecated');
  assert(grepServerHandlersForBareLogs().length === 0, 'no bare logInfo/logWarn in server handlers');
  assert(read('server/src/httpMetricsMiddleware.ts').includes('httpRequestCounter'), 'HTTP RED metrics middleware');
  assert(read('server/src/createApp.ts').includes('registerHttpMetricsMiddleware'), 'HTTP metrics wired in createApp');
}

function testArc10(): void {
  console.log('\n=== TC: ARC-10 security hardening ===');
  assert(read('server/src/securityMiddleware.ts').includes('rateLimit'), 'rate limit middleware');
  assert(read('server/src/createApp.ts').includes('requireLocalhostDevToken'), 'dev-token localhost only');
  assert(read('server/src/socketLifecycle.ts').includes('socket_connection_rejected'), 'max socket connections');
}

function testArc11(): void {
  console.log('\n=== TC: ARC-11 unit tests + CI ===');
  assert(fs.existsSync(path.join(ROOT, 'server/vitest.config.ts')), 'vitest config');
  assert(fs.existsSync(path.join(ROOT, 'server/src/__tests__/timerRegistry.test.ts')), 'timerRegistry tests');
  assert(fs.existsSync(path.join(ROOT, '.github/workflows/ci.yml')), 'GitHub Actions CI');
  assert(read('package.json').includes('"test"'), 'npm test script');
}

function testBug08(): void {
  console.log('\n=== TC: BUG-08 Modal session timer ===');
  const socket = read('mobile/lib/usePondSocket.ts');
  assert(socket.includes('interpolateSessionFishingMs'), 'client timer interpolation');
  assert(socket.includes('isFishingActive'), 'client uses shared isFishingActive for timer');
  // 禁止要求客户端再复制 SESSION_TIMER_PHASES；与服务端广播相位语义对齐即可
  assert(!socket.includes('SESSION_TIMER_PHASES'), 'client does not duplicate SESSION_TIMER_PHASES');

  const loops = read('server/src/serverLoops.ts');
  const phaseMatch = loops.match(
    /SESSION_TIMER_PHASES:\s*FishingPhase\[\]\s*=\s*\[([^\]]+)\]/,
  );
  assert(!!phaseMatch, 'server defines SESSION_TIMER_PHASES');
  const serverPhases = (phaseMatch![1].match(/'([a-z]+)'/g) ?? []).map((s) =>
    s.replace(/'/g, ''),
  ) as FishingPhase[];
  assert(serverPhases.length > 0, 'server SESSION_TIMER_PHASES non-empty');
  for (const phase of serverPhases) {
    assert(isFishingActive(phase), `server timer phase "${phase}" is fishing-active`);
  }
  const allActive: FishingPhase[] = [
    'baiting',
    'casting',
    'waiting',
    'hooked',
    'resolving',
    'stopping',
  ];
  for (const phase of allActive) {
    assert(
      serverPhases.includes(phase),
      `isFishingActive phase "${phase}" included in SESSION_TIMER_PHASES`,
    );
  }
}

async function main(): Promise<void> {
  console.log('verify-engineering');
  testGameStateSplit();
  await testReExports();
  testNoCircularImports();
  testArc09();
  testArc10();
  testArc11();
  testBug08();
  console.log('\nAll engineering checks passed.');
}

main().catch((err) => {
  console.error(err instanceof Error ? err.message : err);
  process.exit(1);
});
