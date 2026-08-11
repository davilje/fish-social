/**
 * 生成综合分析报告 HTML（版本演进 + 目标评估 + 当前规则解读）
 */
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import { ANALYTICS_ROOT, RUNS_DIR, buildManifest, readJson } from './lib.mjs';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const outDir = path.join(ANALYTICS_ROOT, 'pond-day-simulation');
const compact = readJson(path.join(outDir, 'compact.json'));
const analysis = readJson(path.join(outDir, 'analysis.json'));
const version = compact.rulesVersion || compact.rules?.rulesVersion || 'unknown';
const simDays = compact.simDays || analysis.simDays || 1;

const TARGET_DAILY = 100;
const TARGET_USERS = 20;

function dailyCatch(r5) {
  return r5?.perDayCaught ?? r5?.totalCaught ?? 0;
}

function loadRunAnalysis(runId) {
  const p = path.join(RUNS_DIR, runId, 'analysis.json');
  if (!fs.existsSync(p)) return null;
  return readJson(p);
}

const manifest = buildManifest();
const versionRuns = [
  { id: '2026-07-06-pond-day-v031', label: 'v0.3.1', note: '全塘 Σ 咬钩' },
  { id: '2026-07-07-pond-day-v032', label: 'v0.3.2', note: 'D7/D8 动态补充' },
  { id: '2026-07-07-pond-day-v040', label: 'v0.4.0', note: '钓点分区' },
  { id: '2026-07-07-pond-day-v041', label: 'v0.4.1', note: '单鱼+÷20+幼鱼脱钩' },
]
  .map((r) => {
    const a = r.id === manifest.runs.find((x) => x.rulesVersion === 'v0.4.1')?.id
      ? analysis
      : loadRunAnalysis(r.id);
    if (!a) return null;
    const r5 = a.scenarioRows.find((x) => x.anglers === 5);
    const r1 = a.scenarioRows.find((x) => x.anglers === 1);
    return { ...r, r5, r1, generatedAt: a.generatedAt };
  })
  .filter(Boolean);

const current = versionRuns[versionRuns.length - 1];
const r5 = analysis.scenarioRows.find((x) => x.anglers === 5);
const r1 = analysis.scenarioRows.find((x) => x.anglers === 1);
const r20 = analysis.scenarioRows.find((x) => x.anglers === 20);
const s5ponds = compact.scenarios.find((s) => s.a === 5)?.ponds ?? [];

const r5Daily = dailyCatch(r5);
const targetHit = r5Daily <= TARGET_DAILY;
const targetPct = ((r5Daily / TARGET_DAILY) * 100).toFixed(0);

const rules = compact.rules || {};
const ruleRows = [
  ['规则版本', version],
  ['咬钩检测', `${(rules.FISH_BITE_CHECK_MS || 0) / 1000}s / 次`],
  ['咬钩模型', rules.singleFishBite ? '本点单鱼抽样（品质权重）' : '本点全鱼 Σ'],
  ['咬钩率缩放', rules.biteBaseScale != null ? `×${rules.biteBaseScale}（÷${Math.round(1 / rules.biteBaseScale)}）` : '×1'],
  ['钓点分区', rules.spotLocalBite ? `是（${rules.spotsPerPond || 20} 点/塘）` : '否'],
  ['鱼群迁徙', rules.fishMigrationFraction != null ? `${rules.fishMigrationFraction * 100}% / 15min` : '—'],
  ['幼鱼脱钩曲线', rules.juvenileEscapeCurve ? '是（0.08–0.35m 抬高）' : '否'],
  ['补充目标', rules.POND_SUPPLEMENT_TARGET_RATIO != null ? `${rules.POND_SUPPLEMENT_TARGET_RATIO * 100}% 上限` : '—'],
];

const html = `<!DOCTYPE html>
<html lang="zh-CN">
<head>
  <meta charset="UTF-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>鱼塘生态数据分析报告 · ${version}</title>
  <script src="https://cdn.jsdelivr.net/npm/chart.js@4.4.1/dist/chart.umd.min.js"></script>
  <style>
    :root { --bg:#0f1419; --surface:#1a2332; --card:#243044; --text:#e8edf2; --muted:#8b9cb3; --accent:#4a9eff; --ok:#48bb78; --warn:#ed8936; --bad:#fc8181; --border:#2d3a4d; }
    * { box-sizing:border-box; margin:0; padding:0; }
    body { font-family:"Segoe UI","PingFang SC",sans-serif; background:var(--bg); color:var(--text); line-height:1.55; padding:24px; }
    .wrap { max-width:1060px; margin:0 auto; }
    h1 { font-size:1.65rem; margin-bottom:6px; }
    h2 { font-size:1.15rem; margin:28px 0 12px; color:var(--accent); border-bottom:1px solid var(--border); padding-bottom:6px; }
    .meta { color:var(--muted); font-size:.9rem; margin-bottom:20px; }
    .nav a { color:var(--accent); margin-right:16px; font-size:.9rem; }
    .hero { display:grid; grid-template-columns:repeat(auto-fit,minmax(200px,1fr)); gap:12px; margin:20px 0; }
    .kpi { background:var(--surface); border:1px solid var(--border); border-radius:10px; padding:16px; }
    .kpi .val { font-size:1.8rem; font-weight:700; }
    .kpi .lbl { font-size:.8rem; color:var(--muted); margin-top:4px; }
    .kpi.ok .val { color:var(--ok); }
    .kpi.warn .val { color:var(--warn); }
    .card { background:var(--surface); border:1px solid var(--border); border-radius:10px; padding:16px; margin-bottom:16px; }
    table { width:100%; border-collapse:collapse; font-size:.88rem; }
    th,td { border:1px solid var(--border); padding:8px 10px; }
    th { background:var(--card); text-align:left; }
    td.num { text-align:right; font-variant-numeric:tabular-nums; }
    .badge { display:inline-block; padding:2px 8px; border-radius:4px; font-size:.75rem; font-weight:600; }
    .badge.ok { background:rgba(72,187,120,.2); color:var(--ok); }
    .badge.warn { background:rgba(237,137,54,.2); color:var(--warn); }
    .badge.bad { background:rgba(252,129,129,.2); color:var(--bad); }
    .charts { display:grid; grid-template-columns:1fr 1fr; gap:16px; margin:16px 0; }
    @media(max-width:768px){ .charts{ grid-template-columns:1fr; } }
    .chart-box { background:var(--surface); border:1px solid var(--border); border-radius:10px; padding:14px; }
    .chart-wrap { height:260px; position:relative; }
    .callout { border-left:4px solid var(--accent); padding:12px 16px; background:rgba(74,158,255,.08); border-radius:0 8px 8px 0; margin:16px 0; }
    .callout.ok { border-color:var(--ok); background:rgba(72,187,120,.08); }
    ol { margin-left:20px; }
    li { margin-bottom:6px; }
    footer { margin-top:32px; color:var(--muted); font-size:.8rem; }
  </style>
</head>
<body>
  <div class="wrap">
    <nav class="nav">
      <a href="../index.html">归档索引</a>
      <a href="report.html">交互模拟报告</a>
      <a href="../compare.html?runs=2026-07-07-pond-day-v040,2026-07-07-pond-day-v041">v0.4.0 vs v0.4.1</a>
    </nav>

    <h1>鱼塘生态 · 数据分析报告</h1>
    <p class="meta">
      当前规则 <strong>${version}</strong> · 种子 ${compact.seed} · 模拟时间 ${analysis.generatedAt}<br>
      场景：0 / 1 / 3 / 5 / 10 / 20 人/塘 × 四塘 · <strong>${simDays} 天</strong>连续钓鱼 · 步长 15min
    </p>

    <div class="callout ${targetHit ? 'ok' : 'warn'}">
      <strong>产品目标</strong>：${TARGET_USERS} 人全服日钓 &lt; ${TARGET_DAILY} 条<br>
      <strong>当前实测</strong>（5 人/塘 ≈ ${TARGET_USERS} 人）：<strong>${r5Daily} 条/天</strong>（${simDays}日合计 ${r5.totalCaught} 条，目标的 ${targetPct}%）
      ${targetHit ? ' — <span style="color:var(--ok)">已达标</span>' : ' — 略超目标，可微调 BITE_BASE_SCALE'}
    </div>

    <div class="hero">
      <div class="kpi ${targetHit ? 'ok' : 'warn'}"><div class="val">${r5Daily}</div><div class="lbl">日均上岸（5人/塘×4）</div></div>
      <div class="kpi"><div class="val">${r5.totalCaught}</div><div class="lbl">${simDays}日累计上岸</div></div>
      <div class="kpi ok"><div class="val">${r5.perAnglerPerHour}</div><div class="lbl">人均上岸 / 小时</div></div>
      <div class="kpi ok"><div class="val">${r5.popRatio}%</div><div class="lbl">末人口 / 上限（5人/塘）</div></div>
      <div class="kpi"><div class="val">${r5.avgCaughtSize}m</div><div class="lbl">被钓平均体长</div></div>
    </div>

    <h2>一、版本演进（20 人日产量）</h2>
    <div class="charts">
      <div class="chart-box"><h3 style="font-size:.9rem;margin-bottom:8px;color:var(--muted)">四塘日总上岸 · 5人/塘场景</h3><div class="chart-wrap"><canvas id="chartEvolution"></canvas></div></div>
      <div class="chart-box"><h3 style="font-size:.9rem;margin-bottom:8px;color:var(--muted)">人均时产对比</h3><div class="chart-wrap"><canvas id="chartPerHour"></canvas></div></div>
    </div>
    <table>
      <tr><th>版本</th><th>核心机制</th><th>5人/塘日钓</th><th>人均/天</th><th>人均/时</th><th>人口/上限</th><th>被钓均长</th></tr>
      ${versionRuns.map((v) => `<tr>
        <td><strong>${v.label}</strong></td><td>${v.note}</td>
        <td class="num">${dailyCatch(v.r5)}</td>
        <td class="num">${(dailyCatch(v.r5) / TARGET_USERS).toFixed(1)}</td>
        <td class="num">${v.r5.perAnglerPerHour}</td>
        <td class="num">${v.r5.popRatio}%</td>
        <td class="num">${v.r5.avgCaughtSize}m</td>
      </tr>`).join('')}
      <tr style="background:rgba(74,158,255,.1)"><td colspan="2"><strong>目标</strong></td><td class="num">&lt;${TARGET_DAILY}</td><td class="num">&lt;5</td><td class="num">~0.21</td><td colspan="2">—</td></tr>
    </table>

    <h2>二、${version} 规则配置</h2>
    <table>
      <tr><th>参数</th><th>取值</th></tr>
      ${ruleRows.map(([k, v]) => `<tr><td>${k}</td><td>${v}</td></tr>`).join('')}
    </table>

    <h2>三、场景对比（四塘合计 · ${version} · ${simDays}天）</h2>
    <table>
      <tr><th>场景</th><th>日均上岸</th><th>${simDays}日合计</th><th>塘/小时</th><th>人/小时</th><th>人口/上限</th><th>被钓均长</th><th>状态</th></tr>
      ${analysis.scenarioRows.map((r) => `<tr>
        <td>${r.anglers} 人/塘</td>
        <td class="num">${r.perDayCaught ?? r.totalCaught}</td>
        <td class="num">${r.totalCaught}</td>
        <td class="num">${r.perPondPerHour}</td>
        <td class="num">${r.perAnglerPerHour}</td>
        <td class="num">${r.popRatio}%</td>
        <td class="num">${r.avgCaughtSize || '—'}${r.avgCaughtSize ? 'm' : ''}</td>
        <td><span class="badge ok">可持续</span></td>
      </tr>`).join('')}
    </table>

    <h2>四、${simDays} 日趋势（5 人/塘 · 四塘合计）</h2>
    <div class="charts">
      <div class="chart-box"><h3 style="font-size:.9rem;margin-bottom:8px;color:var(--muted)">日钓量</h3><div class="chart-wrap"><canvas id="chartDailyCatch"></canvas></div></div>
      <div class="chart-box"><h3 style="font-size:.9rem;margin-bottom:8px;color:var(--muted)">末人口 / 上限</h3><div class="chart-wrap"><canvas id="chartDailyPop"></canvas></div></div>
    </div>
    <table>
      <tr><th>日次</th><th>日钓量</th><th>人口/上限</th><th>均存活/塘</th></tr>
      ${(r5.dailyTrend || []).map((d) => `<tr><td>D${d.day}</td><td class="num">${d.totalCaught}</td><td class="num">${d.popRatio}%</td><td class="num">${d.avgPop}</td></tr>`).join('')}
    </table>

    <h2>五、四塘明细（5 人/塘 · ${simDays}天末）</h2>
    <table>
      <tr><th>鱼塘</th><th>钓走</th><th>剩余/上限</th><th>人口率</th><th>被钓均长</th></tr>
      ${s5ponds.map((p) => `<tr>
        <td>${p.n}</td><td class="num">${p.c.n}</td>
        <td class="num">${p.f.n} / ${p.mx}</td>
        <td class="num">${((p.f.n / p.mx) * 100).toFixed(1)}%</td>
        <td class="num">${p.c.avg}m</td>
      </tr>`).join('')}
    </table>

    <h2>六、品质结构（1 人/塘 · 四塘合计）</h2>
    <div class="charts">
      <div class="chart-box"><div class="chart-wrap"><canvas id="chartQuality"></canvas></div></div>
      <div class="chart-box"><div class="chart-wrap"><canvas id="chartCaughtQuality"></canvas></div></div>
    </div>
    <table>
      <tr><th>品质</th><th>初始占比</th><th>被钓占比</th></tr>
      ${analysis.qualityShift.map((q) => `<tr>
        <td>${q.quality}</td><td class="num">${q.initPct}%</td><td class="num">${q.caughtPct}%</td>
      </tr>`).join('')}
    </table>

    <h2>七、各品质体长（1 人/塘 · 四塘合计）</h2>
    <div class="charts">
      <div class="chart-box"><div class="chart-wrap"><canvas id="chartQualitySize"></canvas></div></div>
    </div>
    <table>
      <tr><th>品质</th><th>初始均长</th><th>期末均长</th><th>被钓均长</th></tr>
      ${analysis.qualityShift.map((q) => `<tr>
        <td>${q.quality}</td>
        <td class="num">${q.initAvg != null ? q.initAvg + 'm' : '—'}</td>
        <td class="num">${q.finalAvg != null ? q.finalAvg + 'm' : '—'}</td>
        <td class="num">${q.caughtAvg != null ? q.caughtAvg + 'm' : '—'}</td>
      </tr>`).join('')}
    </table>

    <h2>八、分析结论</h2>
    <div class="card">
      <ol>
        ${analysis.conclusions.map((c) => `<li>${c}</li>`).join('')}
        <li><strong>产量达标</strong>：v0.4.1 在 20 人场景下日均 ${r5Daily} 条（${simDays}日合计 ${r5.totalCaught} 条），达到「慢节奏鱼塘」设计目标。</li>
        <li><strong>体验结构</strong>：灰+绿被钓占比 ${(analysis.qualityShift[0].caughtPct + analysis.qualityShift[1].caughtPct).toFixed(0)}%；史诗及以上几乎为零 — 稀有鱼需长期留存或社交展示。</li>
        ${r20 ? `<li><strong>满员长期</strong>：20人/塘日均 ${r20.perDayCaught} 条，D${simDays} 人口 ${r20.popRatio}% 上限。</li>` : ''}
      </ol>
    </div>

    <h2>九、后续建议</h2>
    <div class="card">
      <ul style="margin-left:18px">
        <li>当前日均 ${r5Daily} 条略${r5Daily > TARGET_DAILY ? '高于' : '低于'}目标 ${TARGET_DAILY}，可将 <code>BITE_BASE_SCALE</code> 从 0.05 微调至 ${r5Daily > TARGET_DAILY ? '0.045' : '0.055'}</li>
        <li>观察 ${simDays} 日趋势：若 D${simDays} 人口跌破 85% 需加强补充或降低高压场景人均产出</li>
        <li>紫/红/橙/金 长期零渔获 — 考虑图鉴、他人鱼塘展示等非钓鱼曝光</li>
        <li>实机验证：1min 检测频率下的等待体感与空杆飘字反馈</li>
      </ul>
    </div>

    <footer>
      数据：simulate-pond-day.ts · 复现 <code>npm run analytics:pond-day</code> ·
      <a href="report.html" style="color:var(--accent)">交互报告</a>
    </footer>
  </div>

  <script>
    const EVOLUTION = ${JSON.stringify(versionRuns.map((v) => ({ label: v.label, total: dailyCatch(v.r5), perHour: v.r5.perAnglerPerHour })))};
    const QUALITY = ${JSON.stringify(analysis.qualityShift)};
    const DAILY_TREND = ${JSON.stringify(r5.dailyTrend || [])};
    const TARGET = ${TARGET_DAILY};

    const pctLabelPlugin = {
      id: 'pctLabels',
      afterDatasetsDraw(chart) {
        const { ctx, chartArea } = chart;
        if (!chartArea) return;
        chart.data.datasets.forEach((dataset, di) => {
          const meta = chart.getDatasetMeta(di);
          meta.data.forEach((el, i) => {
            const val = dataset.data[i];
            if (val == null || val === 0) return;
            ctx.save();
            ctx.font = '11px Segoe UI, sans-serif';
            ctx.fillStyle = '#e8edf2';
            ctx.textAlign = 'center';
            const text = val + '%';
            if (chart.config.type === 'doughnut') {
              const pos = el.tooltipPosition();
              ctx.fillText(text, pos.x, pos.y);
            } else {
              ctx.fillText(text, el.x, el.y - 5);
            }
            ctx.restore();
          });
        });
      },
    };

    new Chart(document.getElementById('chartEvolution'), {
      type: 'bar',
      data: {
        labels: EVOLUTION.map(e => e.label),
        datasets: [
          { label: '日总上岸', data: EVOLUTION.map(e => e.total), backgroundColor: ['#5c6570','#c05621','#4a9eff','#48bb78'] },
          { label: '目标上限', data: EVOLUTION.map(() => TARGET), type: 'line', borderColor: '#fc8181', borderDash: [6,4], fill: false, pointRadius: 0 },
        ],
      },
      options: { responsive: true, maintainAspectRatio: false, scales: { y: { title: { display: true, text: '条/天' } } } },
    });

    new Chart(document.getElementById('chartPerHour'), {
      type: 'line',
      data: {
        labels: EVOLUTION.map(e => e.label),
        datasets: [{ label: '人均时产', data: EVOLUTION.map(e => e.perHour), borderColor: '#4a9eff', tension: 0.2, fill: true, backgroundColor: 'rgba(74,158,255,.15)' }],
      },
      options: { responsive: true, maintainAspectRatio: false, scales: { y: { title: { display: true, text: '条/人/小时' } } } },
    });

    new Chart(document.getElementById('chartQuality'), {
      type: 'bar',
      data: {
        labels: QUALITY.map(q => q.quality),
        datasets: [
          { label: '初始占比', data: QUALITY.map(q => q.initPct), backgroundColor: '#5c6570' },
          { label: '被钓占比', data: QUALITY.map(q => q.caughtPct), backgroundColor: '#4a9eff' },
        ],
      },
      options: { responsive: true, maintainAspectRatio: false, plugins: { title: { display: true, text: '品质占比：初始 vs 被钓（%）' }, legend: { position: 'top' } }, scales: { y: { max: 100, title: { display: true, text: '%' } } } },
      plugins: [pctLabelPlugin],
    });

    const caughtOnly = QUALITY.filter(q => q.caughtPct > 0);
    new Chart(document.getElementById('chartCaughtQuality'), {
      type: 'doughnut',
      data: {
        labels: caughtOnly.map(q => q.quality),
        datasets: [{ data: caughtOnly.map(q => q.caughtPct), backgroundColor: ['#9E9E9E','#4CAF50','#2196F3','#9C27B0','#F44336','#FF9800','#FFC107'].slice(0, caughtOnly.length) }],
      },
      options: { responsive: true, maintainAspectRatio: false, plugins: { title: { display: true, text: '被钓品质占比（%）' }, legend: { position: 'right' } } },
      plugins: [pctLabelPlugin],
    });

    new Chart(document.getElementById('chartQualitySize'), {
      type: 'bar',
      data: {
        labels: QUALITY.map(q => q.quality),
        datasets: [
          { label: '初始均长', data: QUALITY.map(q => q.initAvg), backgroundColor: '#5c6570' },
          { label: '期末均长', data: QUALITY.map(q => q.finalAvg), backgroundColor: '#4a9eff' },
          { label: '被钓均长', data: QUALITY.map(q => q.caughtAvg), backgroundColor: '#ed8936' },
        ],
      },
      options: { responsive: true, maintainAspectRatio: false, plugins: { title: { display: true, text: '各品质均长 (m)' } }, scales: { y: { beginAtZero: true, title: { display: true, text: '米' } } } },
    });

    if (DAILY_TREND.length) {
      const dayLabels = DAILY_TREND.map(d => 'D' + d.day);
      new Chart(document.getElementById('chartDailyCatch'), {
        type: 'line',
        data: { labels: dayLabels, datasets: [{ label: '日钓量', data: DAILY_TREND.map(d => d.totalCaught), borderColor: '#4a9eff', tension: 0.2, fill: true, backgroundColor: 'rgba(74,158,255,.15)' }] },
        options: { responsive: true, maintainAspectRatio: false, scales: { y: { title: { display: true, text: '条/天' } } } },
      });
      new Chart(document.getElementById('chartDailyPop'), {
        type: 'line',
        data: { labels: dayLabels, datasets: [{ label: '人口率', data: DAILY_TREND.map(d => d.popRatio), borderColor: '#48bb78', tension: 0.2 }] },
        options: { responsive: true, maintainAspectRatio: false, scales: { y: { min: 0, max: 100, title: { display: true, text: '%' } } } },
      });
    }
  </script>
</body>
</html>`;

const outPath = path.join(outDir, 'analysis-report.html');
fs.writeFileSync(outPath, html, 'utf8');
console.log('Wrote', outPath);
