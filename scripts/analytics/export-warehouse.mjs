/**
 * 聚合级 CSV 导出（D-L3-06）— 默认无明文 playerId
 * 用法: node scripts/analytics/export-warehouse.mjs --date=YYYY-MM-DD
 */
import Database from 'better-sqlite3';
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import { parseDateArg } from './date-utils.mjs';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const projectRoot = path.resolve(__dirname, '../..');
const dbPath = process.env.DB_PATH ?? path.join(projectRoot, 'data/fish-social.db');
const warehouseRoot = path.join(projectRoot, 'docs/analytics/warehouse');

const dateKey = parseDateArg();
const outDir = path.join(warehouseRoot, dateKey);
const latestDir = path.join(warehouseRoot, 'latest');
fs.mkdirSync(outDir, { recursive: true });
fs.mkdirSync(latestDir, { recursive: true });

function csvEscape(v) {
  if (v == null) return '';
  const s = String(v);
  return s.includes(',') || s.includes('"') || s.includes('\n') ? `"${s.replace(/"/g, '""')}"` : s;
}

function writeCsv(filePath, headers, rows) {
  const lines = [headers.join(',')];
  for (const row of rows) {
    lines.push(headers.map((h) => csvEscape(row[h])).join(','));
  }
  fs.writeFileSync(filePath, lines.join('\n') + '\n', 'utf8');
}

const db = new Database(dbPath);
db.pragma('journal_mode = WAL');

const summaryPath = path.join(projectRoot, 'docs/analytics/daily', dateKey, 'summary.json');
let summary = null;
if (fs.existsSync(summaryPath)) {
  summary = JSON.parse(fs.readFileSync(summaryPath, 'utf8'));
}

const pondRows = db.prepare('SELECT * FROM daily_pond_stats WHERE date_key = ?').all(dateKey);
writeCsv(
  path.join(outDir, 'daily_pond_stats.csv'),
  ['date_key', 'pond_id', 'catch_count', 'hook_count', 'escape_count', 'bite_tick_hit', 'bite_tick_miss', 'disconnect_count', 'avg_population'],
  pondRows.map((r) => ({
    date_key: r.date_key,
    pond_id: r.pond_id,
    catch_count: r.catch_count,
    hook_count: r.hook_count ?? r.bite_tick_hit ?? 0,
    escape_count: r.escape_count ?? 0,
    bite_tick_hit: r.bite_tick_hit,
    bite_tick_miss: r.bite_tick_miss,
    disconnect_count: r.disconnect_count,
    avg_population: r.avg_population,
  })),
);

if (summary?.kpis) {
  const kpiRow = { date_key: dateKey, rules_version: summary.meta?.rulesVersion ?? '' };
  for (const [id, kpi] of Object.entries(summary.kpis)) {
    kpiRow[id] = kpi.value;
    kpiRow[`${id}_prev`] = kpi.prev;
    kpiRow[`${id}_avg7d`] = kpi.avg7d;
  }
  const headers = Object.keys(kpiRow);
  writeCsv(path.join(outDir, 'daily_kpi.csv'), headers, [kpiRow]);
}

if (summary?.sections?.economy) {
  const e = summary.sections.economy;
  writeCsv(path.join(outDir, 'daily_economy.csv'), ['date_key', 'faucet_total', 'sink_total', 'net', 'faucet_available'], [
    {
      date_key: dateKey,
      faucet_total: e.faucetTotal,
      sink_total: e.sinkTotal,
      net: e.net,
      faucet_available: e.faucetAvailable,
    },
  ]);
}

const ecologyRows = db.prepare('SELECT * FROM daily_pond_ecology WHERE date_key = ?').all(dateKey);
if (ecologyRows.length) {
  writeCsv(
    path.join(outDir, 'daily_ecology.csv'),
    ['date_key', 'pond_id', 'population', 'max_population', 'pop_ratio', 'avg_size_m', 'quality_json'],
    ecologyRows.map((r) => ({
      date_key: r.date_key,
      pond_id: r.pond_id,
      population: r.population,
      max_population: r.max_population,
      pop_ratio: r.pop_ratio,
      avg_size_m: r.avg_size_m,
      quality_json: r.quality_json,
    })),
  );
} else if (summary?.sections?.ecology?.ponds?.length) {
  writeCsv(
    path.join(outDir, 'daily_ecology.csv'),
    ['date_key', 'pond_id', 'population', 'max_population', 'pop_ratio', 'avg_size_m'],
    summary.sections.ecology.ponds.map((p) => ({
      date_key: dateKey,
      pond_id: p.pondId,
      population: p.population,
      max_population: p.maxPopulation,
      pop_ratio: p.popRatio,
      avg_size_m: p.avgSizeM,
    })),
  );
}

const manifest = {
  dateKey,
  generatedAt: new Date().toISOString(),
  files: ['daily_pond_stats.csv', 'daily_kpi.csv', 'daily_economy.csv', 'daily_ecology.csv'].filter((f) =>
    fs.existsSync(path.join(outDir, f)),
  ),
  note: '聚合级导出，不含明文 player_id',
};
fs.writeFileSync(path.join(outDir, 'manifest.json'), JSON.stringify(manifest, null, 2), 'utf8');

for (const f of manifest.files) {
  fs.copyFileSync(path.join(outDir, f), path.join(latestDir, f));
}
fs.copyFileSync(path.join(outDir, 'manifest.json'), path.join(latestDir, 'manifest.json'));

/** BI 浏览入口：目录 URL 需 index.html，否则 express.static 返回 404 */
function writeWarehouseIndex(dir, man, { isLatest = false } = {}) {
  const files = man.files?.length
    ? man.files
    : fs.readdirSync(dir).filter((n) => n.endsWith('.csv') || n === 'manifest.json');
  const links = files
    .map((f) => `<li><a href="./${f}">${f}</a></li>`)
    .join('\n');
  const backLatest = isLatest
    ? ''
    : '<p class="nav"><a href="../latest/">← 最新导出 (latest)</a></p>';
  const html = `<!DOCTYPE html>
<html lang="zh-CN">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>BI CSV · ${man.dateKey || 'warehouse'}</title>
  <style>
    :root { --bg:#0f1419; --card:#1a2332; --text:#e8edf2; --muted:#8b9cb3; --accent:#4a9eff; --border:#2d3a4d; }
    * { box-sizing:border-box; margin:0; padding:0; }
    body { font-family:"Segoe UI","PingFang SC",sans-serif; background:var(--bg); color:var(--text); padding:2rem; line-height:1.5; }
    .wrap { max-width:720px; margin:0 auto; }
    h1 { font-size:1.4rem; margin-bottom:.35rem; }
    .meta { color:var(--muted); font-size:.9rem; margin-bottom:1.25rem; }
    .card { background:var(--card); border:1px solid var(--border); border-radius:8px; padding:1.25rem; margin-bottom:1rem; }
    ul { margin:.75rem 0 0 1.2rem; }
    a { color:var(--accent); }
    .nav { margin-bottom:1rem; font-size:.9rem; }
    .nav a { margin-right:1rem; }
    code { font-size:.85em; background:#243044; padding:.1rem .35rem; border-radius:4px; }
  </style>
</head>
<body>
  <div class="wrap">
    <nav class="nav">
      <a href="/analytics/index.html">分析归档</a>
      <a href="/ops/">运营平台</a>
      ${isLatest ? '<a href="../">按日期目录</a>' : '<a href="../latest/">latest</a>'}
    </nav>
    ${backLatest}
    <h1>BI 聚合 CSV${isLatest ? '（latest）' : ''}</h1>
    <p class="meta">
      这不是运营看板，是<strong>给 Excel / BI 下载的汇总 CSV</strong>。<br>
      想看「昨天钓了多少」请回 <a href="/analytics/index.html">分析归档索引</a> 打开日报 HTML。<br>
      统计日 <code>${man.dateKey || '—'}</code> · 生成 ${man.generatedAt || '—'} · 默认<strong>不含</strong>明文 playerId
    </p>
    <div class="card">
      <strong>文件列表</strong>
      <ul>
        ${links || '<li>暂无文件</li>'}
        ${files.includes('manifest.json') ? '' : '<li><a href="./manifest.json">manifest.json</a></li>'}
      </ul>
    </div>
    <p class="meta">${man.note || ''} · 复现 <code>npm run analytics:export-warehouse</code></p>
  </div>
</body>
</html>`;
  fs.writeFileSync(path.join(dir, 'index.html'), html, 'utf8');
}

writeWarehouseIndex(outDir, manifest, { isLatest: false });
writeWarehouseIndex(latestDir, manifest, { isLatest: true });

/** warehouse 根索引：列出有数据的日期目录 */
function writeWarehouseRootIndex() {
  const entries = fs
    .readdirSync(warehouseRoot, { withFileTypes: true })
    .filter((d) => d.isDirectory() && /^\d{4}-\d{2}-\d{2}$/.test(d.name))
    .map((d) => d.name)
    .sort((a, b) => b.localeCompare(a));
  const rows = entries
    .map((d) => `<li><a href="./${d}/">${d}</a></li>`)
    .join('\n');
  const html = `<!DOCTYPE html>
<html lang="zh-CN">
<head>
  <meta charset="utf-8" />
  <title>BI warehouse 目录</title>
  <style>
    body { font-family:system-ui,sans-serif; background:#0f1419; color:#e8edf2; padding:2rem; }
    a { color:#4a9eff; } .wrap { max-width:640px; margin:0 auto; }
    .meta { color:#8b9cb3; margin:.5rem 0 1rem; }
  </style>
</head>
<body>
  <div class="wrap">
    <p><a href="./latest/">最新导出 (latest)</a> · <a href="/ops/">运营平台</a> · <a href="/analytics/index.html">分析归档</a></p>
    <h1>按日期归档</h1>
    <p class="meta">共 ${entries.length} 日</p>
    <ul>${rows || '<li>暂无</li>'}</ul>
  </div>
</body>
</html>`;
  fs.writeFileSync(path.join(warehouseRoot, 'index.html'), html, 'utf8');
}
writeWarehouseRootIndex();

db.close();
console.log(`[export-warehouse] ${dateKey}: ${manifest.files.length} CSV → ${outDir} (+ latest/index.html)`);
