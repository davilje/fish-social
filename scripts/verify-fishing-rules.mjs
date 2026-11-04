/** Read-only governance check: no server imports, database access, or baseline auto-accept. */
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { createHash } from 'node:crypto';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const registry = JSON.parse(fs.readFileSync(path.join(root, 'scripts/fishing-rules/current-rules.json'), 'utf8'));
const read = file => fs.readFileSync(path.join(root, file), 'utf8');
const digest = file => createHash('sha256').update(fs.readFileSync(path.join(root, file))).digest('hex');
const errors = [];
function check(label, fn) {
  try { fn(); } catch (error) { errors.push(`${label}: ${error.message}`); }
}

for (const [file, expected] of Object.entries(registry.reviewedSources)) {
  check(file, () => assert.equal(digest(file), expected, '核心规则代码／配置已变化；请先审阅并同步受影响规格、审计与模型，再更新规则登记摘要'));
}
for (const file of [registry.currentDocument, registry.growthSpecification, registry.probabilityAudit]) {
  check(file, () => {
    const md = read(file);
    for (const match of md.matchAll(/\]\(([^)]+)\)/g)) {
      const link = match[1].split('#')[0];
      if (!link || /^(https?:|mailto:|data:)/.test(link)) continue;
      assert(fs.existsSync(path.resolve(root, path.dirname(file), link)), `失效本地链接: ${link}`);
    }
  });
}
check('现行规则标识', () => assert(read(registry.currentDocument).includes(registry.rulesetId)));
check('概率方案状态', () => assert.equal(registry.probabilityRedesign, 'pending-user-confirmation'));
check('回归数据库隔离', () => {
  assert(read('server/vitest.config.ts').includes("env: { DB_PATH: ':memory:' }"), 'Vitest必须在导入服务端模块前强制内存库');
  assert(read('scripts/verify-current-fishing.mjs').includes("DB_PATH: ':memory:'"), '现行核对入口不得继承默认或外部数据库路径');
});
for (const file of registry.historicalDocuments) {
  check(file, () => {
    const md = read(file);
    assert(md.slice(0, 3500).includes('fishing-rule-status: historical-partial;'), '缺少页首局部替代标记');
    assert(md.slice(0, 3500).includes('钓鱼系统现行规则.md'), '缺少现行入口');
  });
}
for (const file of registry.historicalAnalysisDocuments) {
  check(file, () => assert(read(file).startsWith('<!-- fishing-rule-status: historical-analysis;')));
}
for (const file of registry.historicalReportPages) {
  check(file, () => assert(read(file).includes('data-fishing-rule-status="historical-analysis"')));
}
for (const item of registry.retiredEntries) {
  check(item.entry, () => {
    assert.equal(digest(item.archive), item.sha256, '历史原文与保存摘要不符');
    const stub = read(item.entry);
    assert(stub.length < 1000, '旧入口未隔离为短跳转／报错');
    assert(stub.includes(item.entry.endsWith('.py') ? 'raise SystemExit' : 'retiredFishingEntry('));
  });
}
for (const [file, modelKind] of Object.entries(registry.currentModels)) {
  check(file, () => {
    const source = read(file);
    assert(source.includes(modelKind), '现行模型缺少范围标签');
    assert(source.includes('rulesetId'), '现行模型缺少规则标识');
  });
}
check('旧命令阻断', () => {
  const pkg = JSON.parse(read('package.json'));
  for (const command of ['verify:a0', 'simulate:pond-day', 'export:pond-ecology', 'report:bite-escape']) {
    assert(pkg.scripts[command]?.includes('retired-entry.mjs'), `${command}仍可运行旧模型`);
  }
  const result = spawnSync(process.execPath, ['scripts/fishing-rules/retired-entry.mjs', 'verify'], { cwd: root, encoding: 'utf8' });
  assert.equal(result.status, 1);
  assert(result.stderr.includes('未生成或覆盖任何模拟报告'));
});
check('现行预览', () => {
  const html = read('现行预览.html');
  assert(html.includes('data-route="fish-growth"'));
  assert(html.includes('data-route="fish-probability"'));
  assert(!/data-src="[^"\n]*(鱼塘日模拟|鱼塘生态初值|咬钩脱钩校准)/.test(html), '旧模型不得充当总预览的现行入口');
});

if (errors.length) {
  console.error(`钓鱼规则核对失败（${errors.length}项）：\n${errors.map(e => `- ${e}`).join('\n')}`);
  process.exit(1);
}
console.log(`${registry.rulesetId}：${Object.keys(registry.reviewedSources).length}个核心源码／配置版本一致，${registry.historicalDocuments.length}份旧策划有替代标记，${registry.retiredEntries.length}个旧工具入口已隔离。未访问数据库。`);
