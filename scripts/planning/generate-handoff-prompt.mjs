#!/usr/bin/env node
/**
 * 从 *-开发交接.md 提取「交接提示词」代码块，写入 docs/planning/prompts/
 *
 * Usage:
 *   node scripts/planning/generate-handoff-prompt.mjs v0.4.0
 *   node scripts/planning/generate-handoff-prompt.mjs docs/archive/2026-10-26/versions/planning/specs/v0.4.0-开发交接.md
 */
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.join(__dirname, '../..');
const PROMPTS_DIR = path.join(ROOT, 'docs/planning/prompts/versions');
const SPECS_DIR = path.join(ROOT, 'docs/planning/specs');
const DOCUMENT_PATHS = JSON.parse(fs.readFileSync(path.join(ROOT, 'docs/maintenance/path-map.json'), 'utf8'));

function resolveHandoffPath(arg) {
  if (!arg) {
    console.error('Usage: generate-handoff-prompt.mjs <version|handoff.md>');
    process.exit(1);
  }
  if (arg.endsWith('.md')) {
    return path.isAbsolute(arg) ? arg : path.join(ROOT, arg);
  }
  const version = arg.replace(/^v/, 'v');
  const v = version.startsWith('v') ? version : `v${version}`;
  const oldPath = `docs/planning/specs/${v}-开发交接.md`;
  return DOCUMENT_PATHS[oldPath]
    ? path.join(ROOT, DOCUMENT_PATHS[oldPath])
    : path.join(SPECS_DIR, 'versions', `${v}-开发交接.md`);
}

function extractPromptBlock(markdown) {
  const marker = '## 交接提示词';
  const idx = markdown.indexOf(marker);
  if (idx < 0) return null;

  const after = markdown.slice(idx);
  const match = after.match(/```(?:\w*\n)?([\s\S]*?)```/);
  return match ? match[1].trim() : null;
}

function versionFromHandoffFilename(filePath) {
  const base = path.basename(filePath, '.md');
  const m = base.match(/^(v[\d.]+)-开发交接$/);
  return m ? m[1] : base;
}

const handoffPath = resolveHandoffPath(process.argv[2]);
if (path.relative(ROOT, handoffPath).split(path.sep).includes('archive')) {
  console.error('This handoff is archived. Create a current spec before generating a new task.');
  process.exit(1);
}
if (!fs.existsSync(handoffPath)) {
  console.error(`Handoff not found: ${handoffPath}`);
  process.exit(1);
}

const md = fs.readFileSync(handoffPath, 'utf8');
const prompt = extractPromptBlock(md);
if (!prompt) {
  console.error(
    `No prompt block in ${handoffPath}\n` +
      'Add section "## 交接提示词（复制给开发 Agent）" with a fenced code block.',
  );
  process.exit(1);
}

const version = versionFromHandoffFilename(handoffPath);
fs.mkdirSync(PROMPTS_DIR, { recursive: true });
const outPath = path.join(PROMPTS_DIR, `${version}-dev.prompt.md`);
const header = `<!-- Auto-generated from ${path.relative(ROOT, handoffPath)} -->\n<!-- Regenerate: npm run planning:prompt -- ${version} -->\n\n`;

fs.writeFileSync(outPath, header + prompt + '\n', 'utf8');
console.log(`Wrote ${path.relative(ROOT, outPath)} (${prompt.length} chars)`);
