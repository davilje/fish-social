/**
 * 从 compact.json + analysis.json 生成单份 HTML 分析报告
 * 用法: node scripts/analytics/generate-pond-day-report.mjs [compact.json] [out.html]
 */
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import { computeAnalysis, inferRulesVersion, readJson } from './lib.mjs';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const compactPath = process.argv[2] || path.join(__dirname, '../../docs/analytics/pond-day-simulation/compact.json');
const outPath = process.argv[3] || path.join(__dirname, '../../docs/analytics/pond-day-simulation/report.html');
const analysisPath = process.argv[4] || path.join(path.dirname(outPath), 'analysis.json');

const DATA = readJson(compactPath);
const analysis = fs.existsSync(analysisPath) ? readJson(analysisPath) : computeAnalysis(DATA);
const version = DATA.rulesVersion || inferRulesVersion(DATA.rules);
const simDays = DATA.simDays || analysis.simDays || 1;
const periodLabel = simDays > 1 ? `${simDays} 天` : '24 小时';

const statusLabel = { baseline: '基线', sustainable: '可持续', consuming: '消耗态', unsustainable: '不可持续' };
const statusClass = { baseline: 'ok', sustainable: 'ok', consuming: 'warn', unsustainable: 'bad' };

const html = `<!DOCTYPE html>
<html lang="zh-CN">
<head>
  <meta charset="UTF-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>鱼塘 ${periodLabel}生态模拟 · ${version}</title>
  <script src="https://cdn.jsdelivr.net/npm/chart.js@4.4.1/dist/chart.umd.min.js"></script>
  <style>
    :root { --bg:#f6f7f9; --card:#fff; --text:#1a1d23; --muted:#5c6570; --border:#e2e6eb; --accent:#2b6cb0; --accent-soft:#ebf4ff; --ok:#276749; --warn:#c05621; --bad:#c53030; }
    * { box-sizing:border-box; margin:0; padding:0; }
    body { font-family:"Segoe UI",system-ui,sans-serif; background:var(--bg); color:var(--text); line-height:1.6; padding:2rem 1.5rem 4rem; }
    .wrap { max-width:1100px; margin:0 auto; }
    h1 { font-size:1.75rem; font-weight:700; margin-bottom:.25rem; }
    .meta { color:var(--muted); font-size:.9rem; margin-bottom:1rem; }
    .nav { margin-bottom:1.5rem; font-size:.9rem; }
    .nav a { color:var(--accent); margin-right:1rem; }
    h2 { font-size:1.2rem; margin:2rem 0 1rem; padding-bottom:.4rem; border-bottom:2px solid var(--accent); }
    .card { background:var(--card); border:1px solid var(--border); border-radius:8px; padding:1.25rem; margin-bottom:1rem; }
    .controls { display:flex; flex-wrap:wrap; gap:1rem; margin-bottom:1.5rem; }
    label { font-size:.85rem; color:var(--muted); display:block; margin-bottom:.25rem; }
    select { padding:.45rem .75rem; border:1px solid var(--border); border-radius:6px; font-size:.95rem; min-width:140px; }
    .stats { display:grid; grid-template-columns:repeat(auto-fit,minmax(140px,1fr)); gap:.75rem; margin-bottom:1.5rem; }
    .stat { background:var(--card); border:1px solid var(--border); border-radius:8px; padding:1rem; }
    .stat .val { font-size:1.5rem; font-weight:700; color:var(--accent); }
    .stat .lbl { font-size:.8rem; color:var(--muted); }
    table { width:100%; border-collapse:collapse; font-size:.9rem; margin-bottom:1rem; }
    th,td { border:1px solid var(--border); padding:.5rem .75rem; text-align:left; }
    th { background:var(--accent-soft); font-weight:600; }
    td.num { text-align:right; font-variant-numeric:tabular-nums; }
    .charts { display:grid; grid-template-columns:1fr 1fr; gap:1rem; margin-bottom:1rem; }
    @media(max-width:768px){ .charts{ grid-template-columns:1fr; } }
    .chart-box { background:var(--card); border:1px solid var(--border); border-radius:8px; padding:1rem; }
    .chart-box h4 { font-size:.9rem; margin-bottom:.75rem; }
    .chart-wrap { position:relative; height:260px; }
    .conclusion { background:var(--accent-soft); border-left:4px solid var(--accent); padding:1rem 1.25rem; border-radius:0 8px 8px 0; }
    .conclusion ol { margin-left:1.25rem; }
    .conclusion li { margin-bottom:.35rem; }
    .badge { display:inline-block; padding:.15rem .5rem; border-radius:4px; font-size:.75rem; font-weight:600; }
    .badge.ok { background:#c6f6d5; color:var(--ok); }
    .badge.warn { background:#feebc8; color:var(--warn); }
    .badge.bad { background:#fed7d7; color:var(--bad); }
    .delta-pos { color:var(--ok); }
    .delta-neg { color:var(--bad); }
    footer { margin-top:3rem; font-size:.8rem; color:var(--muted); }
  </style>
</head>
<body>
  <div class="wrap">
    <nav class="nav"><a href="../index.html">← 分析归档索引</a><a href="../compare.html">对比查看</a><a href="../bite-escape-calibration/report.html">咬钩/脱钩校准</a></nav>
    <h1>鱼塘 ${periodLabel}生态模拟报告</h1>
    <p class="meta">
      规则 ${version} · 种子 ${DATA.seed} · 生成 ${DATA.generatedAt || '—'}<br>
      1min 咬钩 · 15min 补充 · ${simDays} 天连续运营 · 7 天生长曲线 · 无繁殖
    </p>

    <div class="controls">
      <div><label for="selAnglers">钓鱼人数 / 塘</label><select id="selAnglers"></select></div>
      <div><label for="selPond">鱼塘</label><select id="selPond"></select></div>
    </div>

    <div class="stats" id="stats"></div>
    <div class="charts">
      <div class="chart-box"><h4>塘内鱼数随时间</h4><div class="chart-wrap"><canvas id="chartPop"></canvas></div></div>
      <div class="chart-box"><h4>存活鱼平均体长 (m)</h4><div class="chart-wrap"><canvas id="chartSize"></canvas></div></div>
      <div class="chart-box"><h4>品质分布：初始 vs 期末剩余（%）</h4><div class="chart-wrap"><canvas id="chartQuality"></canvas></div></div>
      <div class="chart-box"><h4>被钓起品质占比（%）</h4><div class="chart-wrap"><canvas id="chartCaught"></canvas></div></div>
    </div>

    <h2>各品质体长 (m)</h2>
    <div class="charts">
      <div class="chart-box"><h4>均长对比：初始 / 期末 / 被钓</h4><div class="chart-wrap"><canvas id="chartQualitySize"></canvas></div></div>
    </div>
    <div id="tblQualitySize"></div>

    <h2>品质占比对比（1 人/塘 · 四塘合计）</h2>
    <div id="tblQualityShift"></div>

    ${simDays > 1 ? `<h2>${simDays} 日趋势</h2>
    <div class="charts">
      <div class="chart-box"><h4>四塘日钓量（当前场景）</h4><div class="chart-wrap"><canvas id="chartDailyCatch"></canvas></div></div>
      <div class="chart-box"><h4>四塘末人口 / 上限（当前场景）</h4><div class="chart-wrap"><canvas id="chartDailyPop"></canvas></div></div>
    </div>
    <div id="tblDaily"></div>` : ''}

    <h2>场景对比总览</h2>
    <div id="tblOverview"></div>

    <h2>${periodLabel}末剩余鱼群（全场景）</h2>
    <div id="tblScenarios"></div>

    <h2>数据分析结论</h2>
    <div class="conclusion"><ol id="conclusions"></ol></div>

    <footer>数据来源 simulate-pond-day.ts · 复现 <code>npm run analytics:pond-day</code></footer>
  </div>

  <script>
    const DATA = ${JSON.stringify(DATA)};
    const ANALYSIS = ${JSON.stringify(analysis)};
    const SIM_DAYS = ${simDays};
    const QN = ['普通','优良','稀有','史诗','传说','神话','至尊'];
    const QC = ['#9E9E9E','#4CAF50','#2196F3','#9C27B0','#F44336','#FF9800','#FFC107'];
    const STATUS = ${JSON.stringify(statusLabel)};
    const STATUS_CLS = ${JSON.stringify(statusClass)};

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
            ctx.fillStyle = '#1a1d23';
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

    function pctArr(counts, total) {
      return counts.map(c => total ? Math.round(c / total * 1000) / 10 : 0);
    }

    function fmtSize(v) { return v == null || v === 0 ? '—' : v + 'm'; }

    let anglers = 1, pondIdx = 0, charts = {};
    const selAnglers = document.getElementById('selAnglers');
    const selPond = document.getElementById('selPond');

    DATA.scenarios.forEach(s => {
      const o = document.createElement('option');
      o.value = s.a; o.textContent = s.a + ' 人/塘';
      selAnglers.appendChild(o);
    });
    selAnglers.value = '1';

    function getScenario() { return DATA.scenarios.find(s => s.a === anglers) || DATA.scenarios[1]; }

    function fillPondSelect() {
      selPond.innerHTML = '';
      getScenario().ponds.forEach((p, i) => {
        const o = document.createElement('option');
        o.value = i; o.textContent = p.n;
        selPond.appendChild(o);
      });
      pondIdx = 0;
    }

    function renderOverview() {
      let h = '<table><tr><th>场景</th><th>日均上岸</th><th>合计上岸</th><th>塘/小时</th><th>人/小时</th><th>末人口/上限</th><th>被钓均长</th><th>剩余均长</th><th>状态</th></tr>';
      ANALYSIS.scenarioRows.forEach(r => {
        h += '<tr><td>' + r.anglers + ' 人/塘</td><td class="num">' + (r.perDayCaught ?? '—') + '</td><td class="num">' + r.totalCaught + '</td><td class="num">' + r.perPondPerHour + '</td><td class="num">' + r.perAnglerPerHour + '</td><td class="num">' + r.popRatio + '%</td><td class="num">' + (r.avgCaughtSize || '—') + (r.avgCaughtSize ? 'm' : '') + '</td><td class="num">' + r.avgFinalSize + 'm</td><td><span class="badge ' + STATUS_CLS[r.status] + '">' + STATUS[r.status] + '</span></td></tr>';
      });
      h += '</table>';
      document.getElementById('tblOverview').innerHTML = h;
    }

    function renderDailyTable() {
      if (SIM_DAYS <= 1 || !document.getElementById('tblDaily')) return;
      const row = ANALYSIS.scenarioRows.find(r => r.anglers === anglers);
      if (!row?.dailyTrend?.length) return;
      let h = '<table><tr><th>日次</th><th>四塘日钓</th><th>人口/上限</th><th>均存活鱼数/塘</th></tr>';
      row.dailyTrend.forEach(d => {
        h += '<tr><td>D' + d.day + '</td><td class="num">' + d.totalCaught + '</td><td class="num">' + d.popRatio + '%</td><td class="num">' + d.avgPop + '</td></tr>';
      });
      h += '</table>';
      document.getElementById('tblDaily').innerHTML = h;
    }

    function renderQualityShift() {
      let h = '<table><tr><th>品质</th><th>初始占比</th><th>被钓占比</th></tr>';
      ANALYSIS.qualityShift.forEach(q => {
        h += '<tr><td>' + q.quality + '</td><td class="num">' + q.initPct + '%</td><td class="num">' + q.caughtPct + '%</td></tr>';
      });
      h += '</table>';
      document.getElementById('tblQualityShift').innerHTML = h;
    }

    function renderQualitySize() {
      const p = getScenario().ponds[pondIdx];
      const initAvg = p.i.qa || p.i.q.map(() => null);
      let h = '<table><tr><th>品质</th><th>初始均长</th><th>期末均长</th><th>被钓均长</th></tr>';
      QN.forEach((name, i) => {
        const caught = p.c.q[i];
        h += '<tr><td>' + name + '</td><td class="num">' + fmtSize(initAvg[i]) + '</td><td class="num">' + fmtSize(p.f.q[i]?.a) + '</td><td class="num">' + fmtSize(caught?.c ? caught.a : null) + '</td></tr>';
      });
      h += '</table>';
      document.getElementById('tblQualitySize').innerHTML = h;
    }

    function renderScenarioTable() {
      const rows = [];
      DATA.scenarios.forEach(sc => sc.ponds.forEach(p => rows.push({ a: sc.a, name: p.n, init: p.i.n, final: p.f.n, favg: p.f.avg, caught: p.c.n, cavg: p.c.avg })));
      let h = '<table><tr><th>场景</th><th>鱼塘</th><th>初始→剩余</th><th>剩余均长</th><th>钓走</th><th>被钓均长</th></tr>';
      rows.forEach(r => {
        h += '<tr><td>' + r.a + ' 人</td><td>' + r.name + '</td><td class="num">' + r.init + '→' + r.final + '</td><td class="num">' + r.favg + 'm</td><td class="num">' + r.caught + '</td><td class="num">' + (r.cavg || '—') + (r.cavg ? 'm' : '') + '</td></tr>';
      });
      h += '</table>';
      document.getElementById('tblScenarios').innerHTML = h;
    }

    function renderConclusions() {
      document.getElementById('conclusions').innerHTML = ANALYSIS.conclusions.map(c => '<li>' + c + '</li>').join('');
    }

    function renderStats() {
      const p = getScenario().ponds[pondIdx];
      document.getElementById('stats').innerHTML = [
        { v: p.i.n, l: '初始数量', h: p.i.avg + 'm 均长' },
        { v: p.f.n, l: SIM_DAYS > 1 ? SIM_DAYS + '天末剩余' : '24h 剩余', h: p.f.avg + 'm 均长' },
        { v: p.c.n, l: SIM_DAYS > 1 ? SIM_DAYS + '天累计钓走' : '被钓起', h: (p.c.avg || 0) + 'm 均长' },
        { v: Math.round(p.f.n / p.mx * 100) + '%', l: '人口/上限', h: 'max ' + p.mx },
      ].map(s => '<div class="stat"><div class="val">' + s.v + '</div><div class="lbl">' + s.l + '</div><div class="lbl">' + s.h + '</div></div>').join('');
    }

    function destroyCharts() { Object.values(charts).forEach(c => c.destroy()); charts = {}; }

    function renderCharts() {
      destroyCharts();
      const p = getScenario().ponds[pondIdx];
      const initAvg = p.i.qa || p.i.q.map(() => null);
      const labels = p.tl.map(t => t.h);
      const opts = { responsive: true, maintainAspectRatio: false };

      charts.pop = new Chart(document.getElementById('chartPop'), {
        type: 'line', data: { labels, datasets: [{ label: '塘内鱼数', data: p.tl.map(t => t.n), borderColor: '#2b6cb0', backgroundColor: 'rgba(43,108,176,0.1)', fill: true, tension: 0.2 }] },
        options: { ...opts, scales: { y: { title: { display: true, text: '条' } } } },
      });
      charts.size = new Chart(document.getElementById('chartSize'), {
        type: 'line', data: { labels, datasets: [{ label: '均长 (m)', data: p.tl.map(t => t.a), borderColor: '#5c6570', tension: 0.2 }] },
        options: { ...opts, scales: { y: { title: { display: true, text: '米' } } } },
      });
      charts.quality = new Chart(document.getElementById('chartQuality'), {
        type: 'bar',
        data: {
          labels: QN,
          datasets: [
            { label: '初始', data: pctArr(p.i.q, p.i.n), backgroundColor: '#c5cdd6' },
            { label: '期末剩余', data: p.f.q.map(x => x.p), backgroundColor: '#2b6cb0' },
          ],
        },
        options: { ...opts, plugins: { legend: { position: 'top' } }, scales: { y: { max: 100, title: { display: true, text: '%' } } } },
        plugins: [pctLabelPlugin],
      });
      const caughtItems = p.c.q.map((x, i) => ({ i, p: x.p, c: x.c })).filter(x => x.c > 0);
      charts.caught = new Chart(document.getElementById('chartCaught'), {
        type: 'doughnut',
        data: {
          labels: caughtItems.length ? caughtItems.map(x => QN[x.i]) : ['无'],
          datasets: [{ data: caughtItems.length ? caughtItems.map(x => x.p) : [100], backgroundColor: caughtItems.length ? caughtItems.map(x => QC[x.i]) : ['#e2e6eb'] }],
        },
        options: { ...opts, plugins: { legend: { position: 'right' } } },
        plugins: caughtItems.length ? [pctLabelPlugin] : [],
      });

      const initSizes = QN.map((_, i) => initAvg[i] ?? null);
      const finalSizes = p.f.q.map(x => x.a ?? null);
      const caughtSizes = p.c.q.map(x => (x.c ? x.a : null));
      charts.qualitySize = new Chart(document.getElementById('chartQualitySize'), {
        type: 'bar',
        data: {
          labels: QN,
          datasets: [
            { label: '初始', data: initSizes, backgroundColor: '#c5cdd6' },
            { label: '期末', data: finalSizes, backgroundColor: '#2b6cb0' },
            { label: '被钓', data: caughtSizes, backgroundColor: '#c05621' },
          ],
        },
        options: { ...opts, scales: { y: { title: { display: true, text: '米' }, beginAtZero: true } } },
      });

      if (SIM_DAYS > 1) {
        const row = ANALYSIS.scenarioRows.find(r => r.anglers === anglers);
        if (row?.dailyTrend?.length) {
          const dayLabels = row.dailyTrend.map(d => 'D' + d.day);
          charts.dailyCatch = new Chart(document.getElementById('chartDailyCatch'), {
            type: 'line',
            data: { labels: dayLabels, datasets: [{ label: '日钓量', data: row.dailyTrend.map(d => d.totalCaught), borderColor: '#2b6cb0', tension: 0.2, fill: true, backgroundColor: 'rgba(43,108,176,0.1)' }] },
            options: { ...opts, scales: { y: { title: { display: true, text: '条/天' } } } },
          });
          charts.dailyPop = new Chart(document.getElementById('chartDailyPop'), {
            type: 'line',
            data: { labels: dayLabels, datasets: [{ label: '人口率', data: row.dailyTrend.map(d => d.popRatio), borderColor: '#276749', tension: 0.2 }] },
            options: { ...opts, scales: { y: { min: 0, max: 100, title: { display: true, text: '%' } } } },
          });
        }
      }
    }

    function refresh() { renderStats(); renderCharts(); renderQualitySize(); renderDailyTable(); }

    selAnglers.addEventListener('change', () => { anglers = Number(selAnglers.value); fillPondSelect(); refresh(); });
    selPond.addEventListener('change', () => { pondIdx = Number(selPond.value); refresh(); });

    fillPondSelect();
    renderOverview();
    renderQualityShift();
    renderScenarioTable();
    renderConclusions();
    refresh();
  </script>
</body>
</html>`;

fs.mkdirSync(path.dirname(outPath), { recursive: true });
fs.writeFileSync(outPath, html, 'utf8');
console.log('Wrote', outPath, '(' + Math.round(html.length / 1024) + ' KB)');
