/**
 * v0.5.1 / BUG-17：切页误离塘 + 返回地图导航
 * 运行: npm run verify:pond-navigation
 */
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const rootDir = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');

function assert(cond: boolean, msg: string): void {
  if (!cond) throw new Error(`FAIL: ${msg}`);
  console.log(`  OK: ${msg}`);
}

function read(rel: string): string {
  return fs.readFileSync(path.join(rootDir, rel), 'utf8');
}

function testPondScreenNavigation(): void {
  console.log('\n=== TC: pond screen navigation leave rules ===');
  const src = read('mobile/app/pond/[id].tsx');
  assert(src.includes("requestLeaveOnUnmount('navigation_back')"), 'map leave schedules leave on unmount');
  assert(src.includes('router.replace'), 'no-history path uses replace to map');
  assert(src.includes('canGoBack'), 'checks canGoBack before back()');
  assert(!src.includes("leavePondWithReason('navigation_back')"), 'does not leave before navigate');
  assert(!src.includes("leavePondWithReason('navigation_social')"), 'social navigation does not leave pond');
  assert(!src.includes("leavePondWithReason('navigation_profile')"), 'profile navigation does not leave pond');
  assert(src.includes("router.push('/social')"), 'social still navigates to /social');
  assert(src.includes("router.push('/profile')"), 'profile still navigates to /profile');
  assert(src.includes('收杆中'), 'stopping shows 收杆中');
  assert(src.includes('开钓中'), 'start pending shows 开钓中');
  assert(src.includes('rejoinPond'), 'dead-state can rejoin');
}

function testUsePondSocketUnmount(): void {
  console.log('\n=== TC: usePondSocket unmount cleanup ===');
  const src = read('mobile/lib/usePondSocket.ts');
  assert(!src.includes("emitLeavePond('unmount'"), 'unmount no longer emits leave_pond by default');
  assert(src.includes('leaveOnUnmountRef'), 'leave-on-unmount intent for map back');
  assert(src.includes('socket?.disconnect()') || src.includes('socket.disconnect()'), 'cleanup still disconnects socket');
  assert(src.includes('leftPondRef.current = true'), 'leftPondRef set on explicit leave');
  assert(src.includes('rejoinPond'), 'exposes rejoinPond to reset latch');
  assert(src.includes("reason: 'rejoin'"), 'rejoin logs reason');
}

function main(): void {
  console.log('verify-pond-navigation');
  testPondScreenNavigation();
  testUsePondSocketUnmount();
  console.log('\nAll pond navigation checks passed.');
}

main();
