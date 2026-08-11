/**
 * D-L3-02: 线上实测 vs 模拟日均对照页
 * 运行: npm run analytics:live-vs-sim
 */
import fs from 'fs';
import path from 'path';
import { ANALYTICS_ROOT, RUNS_DIR, readJson } from './lib.mjs';

const dailyDir = path.join(ANALYTICS_ROOT, 'daily');
const SIM_ANGLERS = 5;

function loadLatestSimDaily() {
  if (!fs.existsSync(RUNS_DIR)) return null;
  const dirs = fs
    .readdirSync(RUNS_DIR, { withFileTypes: true })
    .filter((d) => d.isDirectory())
    .sort((a, b) => b.name.localeCompare(a.name));
  for (const d of dirs) {
    const analysisPath = path.join(RUNS_DIR, d.name, 'analysis.json');
    if (!fs.existsSync(analysisPath)) continue;
    const analysis = readJson(analysisPath);
    const row = analysis.scenarioRows?.find((r) => r.anglers === SIM_ANGLERS);
    if (row) {
      return {
        runId: d.name,
        rulesVersion: analysis.generatedAt ? readJson(path.join(RUNS_DIR, d.name, 'meta.json')).rulesVersion : 'sim',
        perDayCaught: row.perDayCaught,
        perAnglerPerHour: row.perAnglerPerHour,
        popRatio: row.popRatio,
      };
    }
  }
  return { runId: null, rulesVersion: 'v0.4.1', perDayCaught: 118, perAnglerPerHour: 0.98, popRatio: 92, fallback: true };
}

function loadLiveDailyAvg(days = 7) {
  if (!fs.existsSync(dailyDir)) return { avgCatches: 0, days: 0, entries: [] };
  const entries = fs
    .readdirSync(dailyDir, { withFileTypes: true })
    .filter((d) => d.isDirectory())
    .sort((a, b) => b.name.localeCompare(a.name))
    .slice(0, days)
    .map((d) => {
      const compactPath = path.join(dailyDir, d.name, 'compact.json');
      if (!fs.existsSync(compactPath)) return null;
      const c = readJson(compactPath);
      return { date: d.name, catches: c.totalCatches ?? 0, players: c.activePlayers ?? 0 };
    })
    .filter(Boolean);
  const total = entries.reduce((s, e) => s + e.catches, 0);
  return { avgCatches: entries.length ? total / entries.length : 0, days: entries.length, entries };
}

const sim = loadLatestSimDaily();
const live = loadLiveDailyAvg(7);
const deviationPct =
  sim.perDayCaught > 0 ? ((live.avgCatches - sim.perDayCaught) / sim.perDayCaught) * 100 : null;

const report = {
  generatedAt: new Date().toISOString(),
  simAnglers: SIM_ANGLERS,
  sim: { perDayCaught: sim.perDayCaught, rulesVersion: sim.rulesVersion, runId: sim.runId },
  live: { avgDailyCatches: Math.round(live.avgCatches * 10) / 10, sampleDays: live.days },
  deviationPct: deviationPct != null ? Math.round(deviationPct * 10) / 10 : null,
  entries: live.entries,
};

fs.writeFileSync(path.join(ANALYTICS_ROOT, 'live-vs-sim.json'), JSON.stringify(report, null, 2), 'utf8');

const html = `<!DOCTYPE html>
<html lang="zh-CN">
<head><meta charset="utf-8"><title>线上 vs 模拟对照</title>
<style>
body{font-family:sans-serif;max-width:900px;margin:auto;padding:24px;background:#f6f7f9}
.card{background:#fff;border:1px solid #e2e6eb;border-radius:8px;padding:20px;margin-bottom:16px}
.stat{display:inline-block;margin:8px 16px 8px 0;min-width:140px}
.val{font-size:28px;font-weight:700;color:#2b6cb0}.lbl{font-size:12px;color:#666}
.up{color:#276749}.down{color:#c53030}
table{width:100%;border-collapse:collapse;margin-top:12px}th,td{border:1px solid #ddd;padding:8px}
th{background:#ebf4ff}
</style></head>
<body>
<h1>线上实测 vs 模拟对照（${SIM_ANGLERS} 人/塘）</h1>
<p>生成于 ${new Date(report.generatedAt).toLocaleString('zh-CN')}</p>
<div class="card">
  <div class="stat"><div class="val">${sim.perDayCaught}</div><div class="lbl">模拟日均钓获（四塘合计）</div></div>
  <div class="stat"><div class="val">${report.live.avgDailyCatches}</div><div class="lbl">线上 ${live.days} 日均钓获</div></div>
  <div class="stat"><div class="val ${deviationPct != null && Math.abs(deviationPct) > 15 ? 'down' : 'up'}">${deviationPct != null ? (deviationPct >= 0 ? '+' : '') + deviationPct + '%' : '—'}</div><div class="lbl">偏差</div></div>
</div>
<div class="card">
  <p><strong>模拟来源</strong>：${sim.runId ?? '默认基准'} · ${sim.rulesVersion}</p>
  <p><strong>说明</strong>：对比模拟 ${SIM_ANGLERS} 人/塘场景与线上 daily_pond_stats 聚合日均钓获。</p>
</div>
<h2>近 ${live.days} 日线上日报</h2>
<table><tr><th>日期</th><th>钓获</th><th>活跃玩家</th></tr>
${live.entries.map((e) => `<tr><td>${e.date}</td><td>${e.catches}</td><td>${e.players}</td></tr>`).join('')}
</table>
<p><a href="index.html">← 返回归档索引</a></p>
</body></html>`;

fs.writeFileSync(path.join(ANALYTICS_ROOT, 'live-vs-sim.html'), html, 'utf8');
console.log(`[live-vs-sim] sim=${sim.perDayCaught} live=${report.live.avgDailyCatches} deviation=${deviationPct}%`);
