/**
 * Probe local ComfyUI + art workflow prerequisites.
 */
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import { loadEnv } from '../load-env.mjs';
import { createComfyClient } from './comfyui-client.mjs';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, '../..');

loadEnv(ROOT);

function readJson(filePath) {
  return JSON.parse(fs.readFileSync(filePath, 'utf8'));
}

const comfyCfg = readJson(path.join(ROOT, 'config/art/comfyui.json'));
const direction = readJson(path.join(ROOT, 'config/art/direction.json'));
const baseUrl =
  process.env[comfyCfg.baseUrlEnv] ||
  process.env.COMFYUI_BASE_URL ||
  comfyCfg.defaultBaseUrl;

const client = createComfyClient({
  baseUrl,
  pollIntervalMs: comfyCfg.pollIntervalMs,
  timeoutMs: comfyCfg.timeoutMs,
  clientIdPrefix: comfyCfg.clientIdPrefix,
});

let ok = true;

try {
  const { stats, queue } = await client.healthCheck();
  const ver = stats?.system?.comfyui_version || 'unknown';
  console.log(`ComfyUI: online @ ${client.baseUrl}`);
  console.log(`  version: ${ver}`);
  console.log(`  python:  ${stats?.system?.python_version || 'n/a'}`);
  if (queue) {
    const running = queue.queue_running?.length ?? 0;
    const pending = queue.queue_pending?.length ?? 0;
    console.log(`  queue:   running=${running} pending=${pending}`);
  }
} catch (err) {
  ok = false;
  console.error(`ComfyUI: OFFLINE — ${err.message}`);
}

const required = comfyCfg.requiredNodeClass || 'APIImage_OpenAIGenerate';
try {
  const has = await client.hasNodeClass(required);
  if (has) {
    console.log(`Node:    ${required} ✓`);
  } else {
    ok = false;
    console.error(
      `Node:    ${required} ✗ — 请安装 ComfyUI-APIimage 并重启 ComfyUI（见 docs/art/direction/comfyui-setup.md）`,
    );
  }
} catch (err) {
  ok = false;
  console.error(`Node:    无法读取 /object_info — ${err.message}`);
}

const wfDir = path.join(ROOT, comfyCfg.workflowsDir || 'config/art/workflows');
const presets = direction.presets || {};
const workflowNames = new Set([
  direction.defaultWorkflow,
  ...Object.values(presets).map((p) => p.workflow).filter(Boolean),
]);

for (const name of workflowNames) {
  if (!name) continue;
  const p = path.join(wfDir, name);
  if (fs.existsSync(p)) {
    console.log(`Workflow: ${name} ✓`);
  } else {
    ok = false;
    console.error(`Workflow: ${name} ✗ missing ${p}`);
  }
}

const rootHint = process.env.COMFYUI_ROOT || '(unset)';
const outHint = process.env.COMFYUI_OUTPUT_DIR || '(unset)';
console.log(`Env:     COMFYUI_ROOT=${rootHint}`);
console.log(`Env:     COMFYUI_OUTPUT_DIR=${outHint}`);
console.log(
  `Env:     OPENAI_API_KEY=${process.env.OPENAI_API_KEY ? '(set)' : '(missing)'}`,
);

if (!ok) process.exit(1);
console.log('Health check passed.');
