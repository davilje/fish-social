// Create the editable workbook from the catalog, or import edited zhCN/notes.
// Use the Codex bundled Node runtime. TEXT_CATALOG_NODE_MODULES can override
// its public package location on another machine.
import fs from 'node:fs/promises';
import path from 'node:path';
import os from 'node:os';
import { createRequire } from 'node:module';
import { fileURLToPath, pathToFileURL } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const modules = process.env.TEXT_CATALOG_NODE_MODULES || path.join(os.homedir(), '.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules');
const require = createRequire(path.join(modules, '../package.json'));
const { Workbook, SpreadsheetFile, FileBlob } = await import(pathToFileURL(require.resolve('@oai/artifact-tool')).href);
const jsonPath = path.join(root, 'unity-overlay文本表.json');
const outputPath = path.join(root, 'unity-overlay文本表.xlsx');
const previewDir = process.env.TEXT_CATALOG_PREVIEW_DIR || path.join(os.tmpdir(), 'unity-overlay-text-audit');
const data = JSON.parse(await fs.readFile(jsonPath, 'utf8'));
const editable = new Map(data.entries.map(e => [e.key, e]));
const placeholders = value => [...value.matchAll(/(?<!\{)\{\d+(?:,[^{}:]+)?(?::[^{}]+)?\}(?!\})/g)].map(m => m[0]).sort();

if (process.argv.includes('--import-xlsx')) {
  const imported = await SpreadsheetFile.importXlsx(await FileBlob.load(outputPath));
  const rows = imported.worksheets.getItem('文本配置').getUsedRange().values;
  const headerIndex = rows.findIndex(row => row[0] === 'Key' && row[4] === 'zhCN（可编辑）');
  if (headerIndex < 0) throw new Error('找不到文本配置表头。');
  const seen = new Set();
  const edits = [];
  for (const row of rows.slice(headerIndex + 1)) {
    const key = row[0];
    if (!key) continue;
    const entry = editable.get(key);
    if (!entry || seen.has(key)) throw new Error(`未知或重复 Key: ${key}`);
    seen.add(key);
    if (row[3] !== entry.sourceText) throw new Error(`原始文本被修改: ${key}`);
    const value = row[4] == null ? '' : String(row[4]);
    if (JSON.stringify(placeholders(value)) !== JSON.stringify(placeholders(entry.sourceText))) throw new Error(`占位符不一致: ${key}`);
    edits.push([entry, value, row[7] == null ? '' : String(row[7])]);
  }
  if (seen.size !== editable.size) throw new Error('工作簿缺少配置行，请勿删除 Key。');
  // Commit only after validating every row.
  for (const [entry, value, note] of edits) { entry.zhCN = value; entry.note = note; }
  await fs.writeFile(jsonPath, JSON.stringify(data, null, 2) + '\n');
  console.log(`已导入 ${edits.length} 条配置；未修改运行时源码。`);
}

const workbook = Workbook.create();
const config = workbook.worksheets.add('文本配置');
const sources = workbook.worksheets.add('来源映射');
const guide = workbook.worksheets.add('使用说明');
for (const sheet of [config, sources, guide]) {
  sheet.showGridLines = false;
  sheet.tabColor = sheet === config ? '#375B7D' : '#8B97A3';
}
const safe = value => typeof value === 'string' && value.startsWith('=') ? "'" + value : value;
function table(sheet, title, subtitle, headers, rows, widths, name) {
  sheet.getRange('A2').values = [[title]];
  sheet.getRange('A2').format.font = { name: 'Arial', size: 14, bold: true, color: '#253B53' };
  sheet.getRange('A3').values = [[subtitle]];
  sheet.getRange('A3').format.font = { name: 'Arial', size: 10, color: '#5F6C78' };
  sheet.getRangeByIndexes(3, 0, 1, headers.length).values = [headers];
  for (let start = 0; start < rows.length; start += 1000) {
    const chunk = rows.slice(start, start + 1000).map(row => row.map(safe));
    sheet.getRangeByIndexes(start + 4, 0, chunk.length, headers.length).values = chunk;
  }
  const used = sheet.getRangeByIndexes(3, 0, rows.length + 1, headers.length);
  used.format.font = { name: 'Arial', size: 10, color: '#22313F' };
  used.format.verticalAlignment = 'center';
  used.format.rowHeight = 28;
  const header = sheet.getRangeByIndexes(3, 0, 1, headers.length);
  header.format = { fill: '#375B7D', font: { name: 'Arial', size: 10, bold: true, color: '#FFFFFF' }, horizontalAlignment: 'center', verticalAlignment: 'center', rowHeight: 30 };
  widths.forEach((width, i) => { sheet.getRangeByIndexes(3, i, rows.length + 1, 1).format.columnWidthPx = width; });
  sheet.tables.add(`A4:${String.fromCharCode(64 + headers.length)}${rows.length + 4}`, true, name);
  sheet.freezePanes.freezeRows(4);
  sheet.freezePanes.freezeColumns(1);
}

const order = ['运行时界面文本', '游戏数据文案', 'HUD配置', '预制体与场景初始文本', '关联服务端提示', 'HUD预览占位', '界面预览样例', '玩法调试', '编辑器与界面构建', '资源配置与说明', '日志与诊断'];
const entries = [...data.entries].sort((a, b) => order.indexOf(a.category) - order.indexOf(b.category) || a.module.localeCompare(b.module, 'zh-CN') || a.sources[0].file.localeCompare(b.sources[0].file) || a.sources[0].line - b.sources[0].line);
const rows = entries.map(e => [e.key, e.module, e.category, e.sourceText, e.zhCN, e.placeholders.join('、'), e.sources.length, e.note]);
table(config, 'Unity / Overlay 文本配置', `提取日期 ${data.generatedAt.slice(0, 10)}。黄色列可编辑；按模块、类型筛选。`,
  ['Key', '模块', '类型', '原始文本', 'zhCN（可编辑）', '占位符', '来源数', '备注（可编辑）'], rows,
  [210, 112, 165, 440, 440, 150, 72, 360], 'TextCatalog');
config.getRange(`D5:E${rows.length + 4}`).format.wrapText = true;
config.getRange(`E5:E${rows.length + 4}`).format.fill = '#FFF4D6';
config.getRange(`H5:H${rows.length + 4}`).format.fill = '#FFF4D6';
config.getRange(`H5:H${rows.length + 4}`).format.wrapText = true;
config.getRange(`G5:G${rows.length + 4}`).setNumberFormat('0');
rows.forEach((row, i) => {
  const lineCount = Math.max(...[row[3], row[4]].map(value => value.split('\n').reduce((n, line) => n + Math.max(1, Math.ceil([...line].reduce((w, c) => w + (c.charCodeAt(0) > 255 ? 2 : 1), 0) / 57)), 0)));
  if (lineCount > 1) config.getRange(`A${i + 5}:H${i + 5}`).format.rowHeight = Math.min(409, Math.max(28, lineCount * 15 + 8));
});

const sourceRows = entries.flatMap(e => e.sources.map(s => [e.key, s.file, s.line, s.column, s.kind, s.location, s.variables ? JSON.stringify(s.variables) : '', s.expression || '']));
table(sources, '文本来源映射', '每行对应一次文本出现。使用 Key 关联配置；变量顺序以本行表达式为准。',
  ['Key', '源文件', '行号', '列号', '提取方式', '控件 / JSON Pointer / 方法', '参数表达式', '原始拼接 / 插值表达式'], sourceRows,
  [210, 650, 72, 72, 100, 540, 650, 800], 'TextSources');
sources.getRange(`C5:D${sourceRows.length + 4}`).setNumberFormat('0');
// Source expressions are intentionally single-line in Excel; JSON retains exact newlines.
for (let index = 0; index < sourceRows.length; index++) {
  for (const col of [5, 6, 7]) sourceRows[index][col] = sourceRows[index][col].replace(/[\r\n]+/g, ' ⏎ ');
}
for (let start = 0; start < sourceRows.length; start += 1000) {
  const chunk = sourceRows.slice(start, start + 1000).map(row => row.map(safe));
  sources.getRangeByIndexes(start + 4, 0, chunk.length, 8).values = chunk;
}

const notes = [
  ['覆盖范围', 'fish-social-unity/Assets、FishSocialOverlay、desktop-overlay；补充 shared 游戏数据和关联 server/src 提示。'],
  ['配置状态', '当前是文本清单和配置底表。现有运行时尚未通过 Key 读取此表，修改本表不会自动更改游戏界面。'],
  ['编辑方式', '只修改“文本配置”的 zhCN 和备注列。Key、原始文本、来源映射用于追踪，请保持不变。'],
  ['导入 JSON', '在仓库根目录使用 Codex Node 运行 scripts/text-catalog/build.mjs --import-xlsx，将编辑值写入 JSON 并刷新工作簿。'],
  ['重新抽取', '安装 scripts/text-catalog/requirements.txt 后运行 extract.py，再运行 build.mjs。相同 Key 的 JSON zhCN 和备注保留。'],
  ['占位符', '完整保留 {0}、{1}、{0:N0} 等占位符（含格式和重复次数）。参数表达式见来源映射。'],
  ['模板说明', '拼接和插值统一为编号模板；分支中的文本另行收录。普通格式串保留源格式。接入时按来源逐项迁移。'],
  ['去重口径', '同模块、同类型、同提取方式、同原文合并 Key，所有使用位置保留；不同使用类型不会混成一条。'],
  ['初始与现行', '预制体、场景、HUD 中的初始或预览文字可能被脚本覆盖，不等于当前玩家一定可见。'],
  ['已知差异', '离席按钮：HUD JSON 为“离开座位”，XAML 和运行时代码为“离席”，保留各自来源供统一。'],
  ['动态内容', '昵称、签名、聊天、动态、评论由玩家或服务端提供；不预填玩家内容。外部提示字段详见 JSON externalText。'],
  ['排除范围', '第三方包、构建产物、缓存、测试、文档正文、图片内嵌文字、服务端数据库运营配置和纯技术标识。'],
  ['解析提示', `${data.parseWarnings.length} 个文件含解析器不支持的语法节点，已继续遍历文本；具体位置和核验结果见根目录说明。`],
  ['扫描文件数', data.stats.filesScanned], ['去重文本数', data.stats.entries], ['来源出现次数', data.stats.sourceOccurrences],
  ...order.map(category => [category, data.stats.categories[category] || 0]),
];
table(guide, '文本表使用说明', '本次整理保留原文，不修改业务行为。', ['项目', '说明 / 数量'], notes, [180, 1000], 'CatalogGuide');
guide.getRange(`B5:B${notes.length + 4}`).format.wrapText = true;
guide.getRange(`A5:B${notes.length + 4}`).format.rowHeight = 38;
guide.freezePanes.unfreeze();

workbook.recalculate();
console.log((await workbook.inspect({ kind: 'table', range: '文本配置!A4:H8', tableMaxRows: 5, tableMaxCols: 8, maxChars: 2200 })).ndjson);
const errors = await workbook.inspect({ kind: 'match', searchTerm: '#REF!|#DIV/0!|#VALUE!|#NAME\\?|#NUM!|#SPILL!|#CALC!', options: { useRegex: true, maxResults: 10 }, summary: '文本表公式错误检查', maxChars: 1000 });
console.log(errors.ndjson);
await fs.mkdir(previewDir, { recursive: true });
for (const [sheetName, range, file] of [['文本配置', 'A2:F12', 'config-preview.png'], ['来源映射', 'A2:F10', 'sources-preview.png'], ['使用说明', 'A2:B17', 'guide-preview.png']]) {
  const image = await workbook.render({ sheetName, range, scale: 1.25, format: 'png' });
  await fs.writeFile(path.join(previewDir, file), new Uint8Array(await image.arrayBuffer()));
}
await (await SpreadsheetFile.exportXlsx(workbook)).save(outputPath);
try {
  await fs.rename(outputPath + '.inspect.ndjson', path.join(previewDir, 'workbook-inspect.ndjson'));
} catch (error) {
  if (error.code !== 'ENOENT') throw error;
}
console.log(JSON.stringify({ outputPath, entries: rows.length, sourceRows: sourceRows.length }));
