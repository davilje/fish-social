/**
 * Generate art via local ComfyUI → gpt-image-2 workflow.
 *
 * Usage:
 *   npm run art:generate -- --preset desktop-cat --prompt "orange tabby fishing"
 *   npm run art:generate -- --preset pond-scene --prompt "misty bamboo pond" --count 2
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

function writeJson(filePath, data) {
  fs.mkdirSync(path.dirname(filePath), { recursive: true });
  fs.writeFileSync(filePath, `${JSON.stringify(data, null, 2)}\n`, 'utf8');
}

function parseArgs(argv) {
  const out = {
    preset: null,
    prompt: null,
    count: 1,
    workflow: null,
    size: null,
    dryRun: false,
  };
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    const next = () => {
      const v = argv[++i];
      if (v == null) throw new Error(`缺少参数值: ${a}`);
      return v;
    };
    if (a === '--preset') out.preset = next();
    else if (a === '--prompt') out.prompt = next();
    else if (a === '--count') out.count = Math.max(1, Number(next()) || 1);
    else if (a === '--workflow') out.workflow = next();
    else if (a === '--size') out.size = next();
    else if (a === '--dry-run') out.dryRun = true;
    else if (a === '--help' || a === '-h') out.help = true;
    else throw new Error(`未知参数: ${a}`);
  }
  return out;
}

function usage() {
  console.log(`Usage:
  npm run art:generate -- --preset <name> --prompt "<text>" [--count N] [--size WxH]
  npm run art:generate -- --workflow gpt-image-2-t2i.api.json --prompt "<text>"

Presets: see config/art/direction.json
`);
}

function deepClone(obj) {
  return JSON.parse(JSON.stringify(obj));
}

function setNodeInput(workflow, nodeId, key, value) {
  if (!nodeId) return;
  const node = workflow[nodeId];
  if (!node) throw new Error(`workflow 缺少节点 id=${nodeId}`);
  if (!node.inputs) node.inputs = {};
  node.inputs[key] = value;
}

function shanghaiDate() {
  return new Intl.DateTimeFormat('en-CA', {
    timeZone: 'Asia/Shanghai',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).format(new Date());
}

function stamp() {
  const d = new Date();
  const pad = (n) => String(n).padStart(2, '0');
  return `${pad(d.getHours())}${pad(d.getMinutes())}${pad(d.getSeconds())}`;
}

async function main() {
  const args = parseArgs(process.argv.slice(2));
  if (args.help) {
    usage();
    return;
  }
  if (!args.prompt || !String(args.prompt).trim()) {
    usage();
    throw new Error('必须提供 --prompt');
  }

  const comfyCfg = readJson(path.join(ROOT, 'config/art/comfyui.json'));
  const direction = readJson(path.join(ROOT, 'config/art/direction.json'));
  const presets = direction.presets || {};
  const defaultNodes = direction.defaultNodes || {};

  let preset = null;
  if (args.preset) {
    preset = presets[args.preset];
    if (!preset) {
      throw new Error(
        `未知 preset "${args.preset}"。可选: ${Object.keys(presets).join(', ')}`,
      );
    }
  }

  const workflowName =
    args.workflow ||
    preset?.workflow ||
    direction.defaultWorkflow ||
    'gpt-image-2-t2i.api.json';
  const nodes = { ...defaultNodes, ...(preset?.nodes || {}) };
  const size = args.size || preset?.size || '1024x1024';
  const outputSubdir = preset?.outputSubdir || 'misc';
  const filenamePrefix = preset?.filenamePrefix || 'fish-social-art';

  const fullPrompt = [
    preset?.stylePrefix?.trim(),
    args.prompt.trim(),
    preset?.styleSuffix?.trim(),
  ]
    .filter(Boolean)
    .join(' ');

  const apiKey = process.env.OPENAI_API_KEY;
  const baseUrlOpenAI =
    process.env.OPENAI_BASE_URL?.trim() || 'https://api.openai.com';

  const wfPath = path.join(
    ROOT,
    comfyCfg.workflowsDir || 'config/art/workflows',
    workflowName,
  );
  if (!fs.existsSync(wfPath)) {
    throw new Error(`找不到 workflow: ${wfPath}`);
  }

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

  console.log(`ComfyUI: ${client.baseUrl}`);
  console.log(`Preset:  ${args.preset || '(none)'}`);
  console.log(`Workflow:${workflowName}`);
  console.log(`Size:    ${size}`);
  console.log(`Count:   ${args.count}`);
  console.log(`Prompt:  ${fullPrompt.slice(0, 160)}${fullPrompt.length > 160 ? '…' : ''}`);

  if (args.dryRun) {
    console.log('[dry-run] 跳过提交');
    return;
  }

  if (!apiKey || !apiKey.trim()) {
    throw new Error('缺少 OPENAI_API_KEY。请在项目根 .env 中配置（勿提交 git）。');
  }

  await client.healthCheck();
  const required = comfyCfg.requiredNodeClass;
  if (required && !(await client.hasNodeClass(required))) {
    throw new Error(
      `ComfyUI 未注册节点 ${required}。请安装 ComfyUI-APIimage 并重启 ComfyUI。见 docs/art/direction/comfyui-setup.md`,
    );
  }

  const day = shanghaiDate();
  const runDir = path.join(
    ROOT,
    'docs/art/generated',
    day,
    `${outputSubdir}-${stamp()}`,
  );
  fs.mkdirSync(runDir, { recursive: true });

  const savedFiles = [];
  const jobs = [];

  for (let i = 0; i < args.count; i++) {
    const workflow = deepClone(readJson(wfPath));
    setNodeInput(workflow, nodes.prompt, 'prompt', fullPrompt);
    setNodeInput(workflow, nodes.api_key, 'api_key', apiKey.trim());
    setNodeInput(workflow, nodes.base_url, 'base_url', baseUrlOpenAI);
    setNodeInput(workflow, nodes.model_name, 'model_name', 'gpt-image-2');
    setNodeInput(workflow, nodes.size, 'size', size);
    setNodeInput(workflow, nodes.num_images, 'num_images', 1);
    setNodeInput(
      workflow,
      nodes.filename_prefix,
      'filename_prefix',
      `${filenamePrefix}-${i + 1}`,
    );

    const { promptId } = await client.queuePrompt(workflow);
    console.log(`[${i + 1}/${args.count}] queued prompt_id=${promptId}`);
    const entry = await client.waitForCompletion(promptId);
    const images = client.resolveOutputImages(entry);
    if (!images.length) {
      throw new Error(`prompt_id=${promptId} 完成但无输出图片（检查 SaveImage 节点）`);
    }

    for (const img of images) {
      const buf = await client.fetchImageBuffer(img);
      const destName = img.filename || `${filenamePrefix}-${i + 1}.png`;
      const dest = path.join(runDir, destName);
      fs.writeFileSync(dest, buf);
      const rel = path.relative(ROOT, dest).replace(/\\/g, '/');
      savedFiles.push(rel);
      console.log(`  saved ${rel}`);
    }
    jobs.push({ promptId, images: images.map((x) => x.filename) });
  }

  const manifestPath = path.join(ROOT, 'docs/art/generated/manifest.json');
  let manifest = { version: 1, entries: [] };
  if (fs.existsSync(manifestPath)) {
    try {
      manifest = readJson(manifestPath);
      if (!Array.isArray(manifest.entries)) manifest.entries = [];
    } catch {
      manifest = { version: 1, entries: [] };
    }
  }
  manifest.entries.unshift({
    at: new Date().toISOString(),
    preset: args.preset || null,
    workflow: workflowName,
    size,
    prompt: fullPrompt,
    outputDir: path.relative(ROOT, runDir).replace(/\\/g, '/'),
    files: savedFiles,
    jobs,
  });
  // keep last 200 jobs
  manifest.entries = manifest.entries.slice(0, 200);
  writeJson(manifestPath, manifest);

  console.log(`Done. ${savedFiles.length} file(s) → ${path.relative(ROOT, runDir)}`);
}

main().catch((err) => {
  console.error(err.message || err);
  process.exit(1);
});
