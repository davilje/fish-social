#!/usr/bin/env node
import { execSync } from 'child_process';
import readline from 'readline';
import path from 'path';
import { fileURLToPath } from 'url';
import { loadEnv } from './load-env.mjs';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, '..');
loadEnv(ROOT);

const isWin = process.platform === 'win32';
const ROOT_MARKER = ROOT.replace(/\\/g, '/').toLowerCase();

function getPortsToCheck(serverOnly) {
  const serverPort = Number(process.env.PORT) || 3001;
  const webPort = Number(process.env.EXPO_WEB_PORT) || 8082;
  if (serverOnly) return [{ label: 'API + WebSocket', port: serverPort }];
  return [
    { label: 'API + WebSocket', port: serverPort },
    { label: 'Expo Web', port: webPort },
  ];
}

function getListeningPids(port) {
  const pids = new Set();

  if (isWin) {
    let out = '';
    try {
      out = execSync('netstat -ano', { encoding: 'utf8', stdio: ['pipe', 'pipe', 'pipe'] });
    } catch {
      return [];
    }
    const portSuffix = `:${port}`;
    for (const line of out.split(/\r?\n/)) {
      if (!line.includes(portSuffix) || !/LISTENING/i.test(line)) continue;
      const parts = line.trim().split(/\s+/);
      const pid = Number(parts[parts.length - 1]);
      if (pid > 0) pids.add(pid);
    }
    return [...pids];
  }

  try {
    const out = execSync(`lsof -iTCP:${port} -sTCP:LISTEN -t`, {
      encoding: 'utf8',
      stdio: ['pipe', 'pipe', 'pipe'],
    });
    return out
      .split(/\r?\n/)
      .map((s) => Number(s.trim()))
      .filter((n) => n > 0);
  } catch {
    return [];
  }
}

function listNodeProcesses() {
  const rows = [];

  if (isWin) {
    let out = '';
    try {
      out = execSync('wmic process where "name=\'node.exe\'" get ProcessId,CommandLine /format:list', {
        encoding: 'utf8',
        stdio: ['pipe', 'pipe', 'pipe'],
      });
    } catch {
      return rows;
    }

    const re = /CommandLine=(.*?)\r?\nProcessId=(\d+)/gs;
    for (const match of out.matchAll(re)) {
      const command = match[1]?.trim() ?? '';
      const pid = Number(match[2]);
      if (pid > 0 && command) rows.push({ pid, command });
    }
    return rows;
  }

  try {
    const out = execSync('ps -ax -o pid=,command=', {
      encoding: 'utf8',
      stdio: ['pipe', 'pipe', 'pipe'],
    });
    for (const line of out.split(/\r?\n/)) {
      const match = line.trim().match(/^(\d+)\s+(.*)$/);
      if (!match) continue;
      const pid = Number(match[1]);
      const command = match[2];
      if (/node|tsx|expo/i.test(command)) rows.push({ pid, command });
    }
  } catch {
    // ignore
  }
  return rows;
}

function isFishSocialDevCommand(commandLine) {
  const cmd = commandLine.replace(/\\/g, '/').toLowerCase();
  if (!cmd.includes('fish-social') && !cmd.includes(ROOT_MARKER)) return false;
  if (cmd.includes('ports.mjs')) return false;

  const devMarkers = [
    'tsx/dist/cli.mjs',
    'tsx watch',
    'concurrently',
    'expo-web.mjs',
    'expo start',
    'fish-social/server',
    'npm run dev',
    'npm.cmd run dev',
    '@fish-social/server',
  ];
  return devMarkers.some((m) => cmd.includes(m.replace(/\\/g, '/')));
}

function findStrayFishSocialPids() {
  const pids = new Set();
  for (const { pid, command } of listNodeProcesses()) {
    if (pid === process.pid) continue;
    if (isFishSocialDevCommand(command)) pids.add(pid);
  }
  return [...pids];
}

function getProcessCommand(pid) {
  if (isWin) {
    try {
      const out = execSync(
        `wmic process where "ProcessId=${pid}" get CommandLine /format:list`,
        { encoding: 'utf8', stdio: ['pipe', 'pipe', 'pipe'] },
      );
      const match = out.match(/CommandLine=(.*)/);
      return match?.[1]?.trim() ?? '';
    } catch {
      return '';
    }
  }
  try {
    return execSync(`ps -p ${pid} -o command=`, {
      encoding: 'utf8',
      stdio: ['pipe', 'pipe', 'pipe'],
    }).trim();
  } catch {
    return '';
  }
}

function getProcessName(pid) {
  if (isWin) {
    try {
      const out = execSync(
        `wmic process where "ProcessId=${pid}" get Name /format:list`,
        { encoding: 'utf8', stdio: ['pipe', 'pipe', 'pipe'] },
      );
      const match = out.match(/Name=(.*)/);
      return match?.[1]?.trim().toLowerCase() ?? '';
    } catch {
      return '';
    }
  }
  try {
    return execSync(`ps -p ${pid} -o comm=`, {
      encoding: 'utf8',
      stdio: ['pipe', 'pipe', 'pipe'],
    })
      .trim()
      .toLowerCase();
  } catch {
    return '';
  }
}

function isKillableDevProcess(pid, commandLine) {
  const name = getProcessName(pid);
  const cmd = commandLine.toLowerCase();
  const devNames = ['node', 'nodejs', 'tsx', 'expo'];
  const nameOk = devNames.some((n) => name.includes(n));
  const cmdOk =
    isFishSocialDevCommand(commandLine) ||
    cmd.includes('tsx') ||
    cmd.includes('expo') ||
    cmd.includes('node_modules');
  return nameOk && cmdOk;
}

function killPid(pid) {
  if (isWin) {
    execSync(`taskkill /PID ${pid} /T /F`, { stdio: 'ignore' });
  } else {
    execSync(`kill -9 ${pid}`, { stdio: 'ignore' });
  }
}

function collectPortStatus(serverOnly) {
  const rows = [];
  for (const { label, port } of getPortsToCheck(serverOnly)) {
    const pids = getListeningPids(port);
    if (pids.length === 0) {
      rows.push({ label, port, status: 'free', processes: [] });
      continue;
    }
    const processes = pids.map((pid) => ({
      pid,
      command: getProcessCommand(pid) || '(unknown)',
      killable: isKillableDevProcess(pid, getProcessCommand(pid)),
    }));
    rows.push({ label, port, status: 'in_use', processes });
  }
  return rows;
}

function collectFreeTargets(serverOnly) {
  const byPid = new Map();

  for (const row of collectPortStatus(serverOnly)) {
    for (const p of row.processes) {
      if (!p.killable) continue;
      byPid.set(p.pid, { ...p, port: row.port, source: 'port' });
    }
  }

  for (const pid of findStrayFishSocialPids()) {
    if (byPid.has(pid)) continue;
    const command = getProcessCommand(pid) || '(unknown)';
    if (!isKillableDevProcess(pid, command)) continue;
    byPid.set(pid, { pid, command, killable: true, port: null, source: 'stray' });
  }

  return [...byPid.values()];
}

function printStatus(rows) {
  console.log('Fish Social 端口占用检查\n');
  for (const row of rows) {
    if (row.status === 'free') {
      console.log(`  ${row.label} :${row.port}  —  空闲`);
      continue;
    }
    console.log(`  ${row.label} :${row.port}  —  占用`);
    for (const p of row.processes) {
      const tag = p.killable ? '可释放' : '跳过(非本项目 dev 进程)';
      const cmd =
        p.command.length > 100 ? `${p.command.slice(0, 97)}...` : p.command;
      console.log(`    PID ${p.pid}  [${tag}]  ${cmd}`);
    }
  }
  console.log('');
}

async function confirm(message) {
  const rl = readline.createInterface({ input: process.stdin, output: process.stdout });
  const answer = await new Promise((resolve) => {
    rl.question(message, resolve);
  });
  rl.close();
  return /^y(es)?$/i.test(answer.trim());
}

async function runFree(serverOnly, force, quiet) {
  const targets = collectFreeTargets(serverOnly);

  if (targets.length === 0) {
    if (quiet) {
      console.log('>>> 端口与 dev 进程已空闲');
      return;
    }
    printStatus(collectPortStatus(serverOnly));
    console.log('没有可释放的本项目 dev 进程。');
    return;
  }

  if (!quiet) {
    printStatus(collectPortStatus(serverOnly));
    const stray = targets.filter((t) => t.source === 'stray');
    if (stray.length > 0) {
      console.log(`另发现 ${stray.length} 个残留 dev 进程（未占端口）：`);
      for (const t of stray) {
        const cmd =
          t.command.length > 100 ? `${t.command.slice(0, 97)}...` : t.command;
        console.log(`    PID ${t.pid}  ${cmd}`);
      }
      console.log('');
    }
  } else {
    console.log(`>>> 正在清理 ${targets.length} 个 dev 进程…`);
  }

  if (!force) {
    const ok = await confirm(`释放以上 ${targets.length} 个进程? [y/N] `);
    if (!ok) {
      console.log('已取消。');
      return;
    }
  }

  for (const t of targets) {
    try {
      killPid(t.pid);
      if (!quiet) {
        const where = t.port ? `端口 ${t.port}` : '残留进程';
        console.log(`已结束 PID ${t.pid} (${where})`);
      }
    } catch {
      if (!quiet) console.warn(`无法结束 PID ${t.pid}`);
    }
  }

  await new Promise((r) => setTimeout(r, 1200));

  if (!quiet) {
    console.log('\n释放后状态：');
    printStatus(collectPortStatus(serverOnly));
  } else {
    console.log('>>> 清理完成');
  }
}

const args = process.argv.slice(2);
const command = args[0];
const force = args.includes('--force');
const quiet = args.includes('--quiet');
const serverOnly = args.includes('--server-only');

if (command === 'check') {
  printStatus(collectPortStatus(serverOnly));
  const stray = findStrayFishSocialPids();
  if (stray.length > 0) {
    console.log(`残留 dev 进程（未占端口）: ${stray.map((p) => `PID ${p}`).join(', ')}`);
    console.log('');
  }
} else if (command === 'free') {
  await runFree(serverOnly, force, quiet);
} else {
  console.log('用法: node scripts/ports.mjs <check|free> [--force] [--quiet] [--server-only]');
  process.exit(1);
}
