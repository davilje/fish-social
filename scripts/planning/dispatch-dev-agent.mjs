#!/usr/bin/env node
/**
 * 将开发交接提示词派发给 Cursor Cloud Agent（需 CURSOR_API_KEY）
 *
 * Usage:
 *   CURSOR_API_KEY=... npm run planning:dispatch -- v0.4.0
 *   npm run planning:dispatch -- v0.4.0 --dry-run
 *
 * 若无 API Key，请用：
 *   - 新对话 @docs/planning/prompts/v0.4.0-dev.prompt.md
 *   - 或策划 Agent 内 Task 子代理（见 HANDOFF.md）
 */
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import { spawnSync } from 'child_process';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.join(__dirname, '../..');

const version = process.argv[2];
const dryRun = process.argv.includes('--dry-run');

if (!version) {
  console.error('Usage: dispatch-dev-agent.mjs <version> [--dry-run]');
  process.exit(1);
}

const v = version.startsWith('v') ? version : `v${version}`;
const promptPath = path.join(ROOT, 'docs/planning/prompts', `${v}-dev.prompt.md`);

if (!fs.existsSync(promptPath)) {
  console.error(`Prompt file missing: ${promptPath}`);
  console.error(`Run: npm run planning:prompt -- ${v}`);
  process.exit(1);
}

const prompt = fs.readFileSync(promptPath, 'utf8').replace(/^<!--[\s\S]*?-->\n*/g, '');

const preamble = `工作目录：${ROOT}\n请在本仓库内实现，不要只给建议。\n\n`;

if (dryRun) {
  console.log('--- DRY RUN: would send to Cursor Agent ---\n');
  console.log(preamble + prompt.slice(0, 2000));
  if (prompt.length > 2000) console.log('\n... [truncated]');
  process.exit(0);
}

const apiKey = process.env.CURSOR_API_KEY;
if (!apiKey) {
  console.error('CURSOR_API_KEY is not set.');
  console.error('\nAlternatives without API key:');
  console.error(`  1. New Agent chat → @${path.relative(ROOT, promptPath)} → "按此实现"`);
  console.error('  2. Ask planner Agent to launch Task subagent with this file');
  process.exit(1);
}

// Dynamic import @cursor/sdk if installed
let Agent;
try {
  ({ Agent } = await import('@cursor/sdk'));
} catch {
  console.error('@cursor/sdk not installed. Run: npm i -D @cursor/sdk');
  console.error(`Or use @file: ${path.relative(ROOT, promptPath)}`);
  process.exit(1);
}

console.log(`Dispatching ${v} dev agent (cloud) ...`);

try {
  const result = await Agent.prompt(preamble + prompt, {
    apiKey,
    model: { id: 'composer-2.5' },
    cloud: {
      repos: [{ url: process.env.GITHUB_REPO_URL || 'https://github.com/YOUR_ORG/fish-social' }],
    },
  });
  console.log('Status:', result.status);
  if (result.result) console.log(result.result.slice(0, 500));
  if (result.agentId) console.log('Agent ID:', result.agentId);
} catch (err) {
  console.error('Dispatch failed:', err.message);
  console.error('Tip: set GITHUB_REPO_URL to your remote, or use local runtime in a custom script.');
  process.exit(1);
}
