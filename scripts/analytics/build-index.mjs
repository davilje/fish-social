/**
 * 生成 docs/analytics/index.html + compare.html + manifest.json
 */
import fs from 'fs';
import path from 'path';
import { ANALYTICS_ROOT, buildManifest, writeJson } from './lib.mjs';

const manifest = buildManifest();
writeJson(path.join(ANALYTICS_ROOT, 'manifest.json'), manifest);

const indexHtml = `<!DOCTYPE html>
<html lang="zh-CN">
<head>
  <meta charset="UTF-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>鱼塘数据分析归档</title>
  <style>
    :root { --bg:#0f1419; --surface:#1a2332; --text:#e8edf2; --muted:#8b9cb3; --accent:#4a9eff; --border:#2d3a4d; --ok:#48bb78; --warn:#ed8936; }
    * { box-sizing:border-box; margin:0; padding:0; }
    body { font-family:"Segoe UI","PingFang SC",sans-serif; background:var(--bg); color:var(--text); padding:2rem; line-height:1.5; }
    .wrap { max-width:960px; margin:0 auto; }
    h1 { font-size:1.6rem; margin-bottom:.5rem; }
    .meta { color:var(--muted); font-size:.9rem; margin-bottom:2rem; }
    .actions { margin-bottom:1.5rem; }
    .btn { display:inline-block; padding:.5rem 1rem; background:var(--accent); color:#fff; text-decoration:none; border-radius:6px; font-size:.9rem; margin-right:.5rem; border:none; cursor:pointer; }
    .btn.secondary { background:var(--surface); border:1px solid var(--border); color:var(--text); }
    .btn:disabled { opacity:.4; cursor:not-allowed; }
    table { width:100%; border-collapse:collapse; background:var(--surface); border-radius:8px; overflow:hidden; }
    th,td { padding:.75rem 1rem; text-align:left; border-bottom:1px solid var(--border); }
    th { background:#243044; font-size:.85rem; color:var(--muted); }
    tr:hover td { background:rgba(74,158,255,.06); }
    a { color:var(--accent); }
    .type-tag { font-size:.75rem; padding:.15rem .4rem; border-radius:4px; background:#243044; color:var(--muted); }
    .chk { width:18px; height:18px; cursor:pointer; }
    .empty { text-align:center; padding:3rem; color:var(--muted); }
    .latest { margin-bottom:2rem; }
    .latest .card { background:var(--surface); border:1px solid var(--border); border-radius:8px; padding:1.25rem; display:flex; justify-content:space-between; align-items:center; flex-wrap:wrap; gap:1rem; }
  </style>
</head>
<body>
  <div class="wrap">
    <h1>鱼塘数据分析归档</h1>
    <p class="meta">
      <strong>两栏内容请分清：</strong><br>
      · <strong>线上日报</strong>：真实服日批产出的 <code>daily/YYYY-MM-DD/report.html</code>（下方「最新运营日报」卡片 + 表中 <code>live-daily</code>）。<br>
      · <strong>模拟 / 校准</strong>：鱼塘模拟、咬钩脱钩校准、初始生态等实验报告（表中其它 type；也可点右侧快捷链）。<br>
      本页是<strong>给人看的报告目录</strong>，不是 Excel 下载页——CSV 请去
      <a href="/ops/">运营平台</a> 的「BI CSV」或 <a href="warehouse/latest/">warehouse/latest</a>。<br>
      每次模拟/导出自动归档，支持多版本对比 · 更新于 <span id="updated"></span>
    </p>

    <div class="latest" id="latest"></div>

    <div class="actions">
      <button class="btn" id="btnCompare" disabled>对比已选 (<span id="selCount">0</span>)</button>
      <a class="btn secondary" href="growth/">增长看板</a>
      <a class="btn secondary" href="live-vs-sim.html">线上 vs 模拟</a>
      <a class="btn secondary" href="pond-day-simulation/report.html">最新模拟报告</a>
      <a class="btn secondary" href="bite-escape-calibration/report.html">咬钩/脱钩校准</a>
      <a class="btn secondary" href="pond-ecology-initial/report.html">初始生态报告</a>
    </div>

    <table>
      <thead>
        <tr><th></th><th>归档 ID</th><th>类型</th><th>规则版本</th><th>生成时间</th><th>种子</th><th>报告</th></tr>
      </thead>
      <tbody id="tbody"></tbody>
    </table>
    <p class="empty" id="empty" style="display:none">暂无归档。运行 <code>npm run analytics:pond-day</code> 生成首份报告。</p>
  </div>
  <script>
    const MANIFEST = ${JSON.stringify(manifest)};
    const selected = new Set();

    document.getElementById('updated').textContent = new Date(MANIFEST.generatedAt).toLocaleString('zh-CN');

    if (MANIFEST.runs.length) {
      const latestOps = MANIFEST.runs.find(r => r.type === 'live-daily');
      const latest = latestOps || MANIFEST.runs[0];
      const opsExtra = latestOps
        ? '<br><span style="color:var(--muted);font-size:.85rem">日钓 ' + (latestOps.totalCatches ?? '—') + ' · DAU ' + (latestOps.activePlayers ?? '—') + '</span>'
        : '';
      const title = latestOps ? '最新运营日报' : '最新归档';
      document.getElementById('latest').innerHTML = '<div class="card"><div><strong>' + title + '</strong><br><span style="color:var(--muted);font-size:.9rem">' + latest.title + ' · ' + latest.rulesVersion + ' · ' + new Date(latest.generatedAt).toLocaleString('zh-CN') + '</span>' + opsExtra + '</div><a class="btn" href="' + latest.reportPath + '">打开报告</a></div>';
    }

    const tbody = document.getElementById('tbody');
    if (!MANIFEST.runs.length) {
      document.getElementById('empty').style.display = 'block';
      tbody.parentElement.style.display = 'none';
    } else {
      MANIFEST.runs.forEach(r => {
        const tr = document.createElement('tr');
        const canCompare = r.type === 'pond-day';
        const isLive = r.type === 'live-daily';
        tr.innerHTML = '<td>' + (canCompare ? '<input type="checkbox" class="chk" data-id="' + r.id + '">' : '') + '</td>' +
          '<td><code>' + r.id + '</code></td>' +
          '<td><span class="type-tag">' + r.type + '</span></td>' +
          '<td>' + (r.rulesVersion || '—') + '</td>' +
          '<td>' + new Date(r.generatedAt).toLocaleString('zh-CN') + '</td>' +
          '<td>' + (r.seed ?? (isLive ? (r.totalCatches ?? '—') : '—')) + '</td>' +
          '<td><a href="' + r.reportPath + '">report.html</a></td>';
        tbody.appendChild(tr);
      });
    }

    function updateSel() {
      document.getElementById('selCount').textContent = selected.size;
      document.getElementById('btnCompare').disabled = selected.size < 2;
    }

    tbody.addEventListener('change', e => {
      if (!e.target.classList.contains('chk')) return;
      const id = e.target.dataset.id;
      if (e.target.checked) selected.add(id); else selected.delete(id);
      updateSel();
    });

    document.getElementById('btnCompare').addEventListener('click', () => {
      if (selected.size < 2) return;
      const ids = [...selected].join(',');
      location.href = 'compare.html?runs=' + encodeURIComponent(ids);
    });
  </script>
</body>
</html>`;

const compareHtml = `<!DOCTYPE html>
<html lang="zh-CN">
<head>
  <meta charset="UTF-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>鱼塘模拟对比</title>
  <script src="https://cdn.jsdelivr.net/npm/chart.js@4.4.1/dist/chart.umd.min.js"></script>
  <style>
    :root { --bg:#f6f7f9; --card:#fff; --text:#1a1d23; --muted:#5c6570; --border:#e2e6eb; --accent:#2b6cb0; }
    * { box-sizing:border-box; margin:0; padding:0; }
    body { font-family:"Segoe UI",sans-serif; background:var(--bg); color:var(--text); padding:2rem 1.5rem; }
    .wrap { max-width:1200px; margin:0 auto; }
    h1 { font-size:1.5rem; margin-bottom:.5rem; }
    .nav { margin-bottom:1.5rem; font-size:.9rem; }
    .nav a { color:var(--accent); margin-right:1rem; }
    .controls { display:flex; flex-wrap:wrap; gap:1rem; margin-bottom:1.5rem; align-items:flex-end; }
    label { font-size:.85rem; color:var(--muted); display:block; margin-bottom:.25rem; }
    select { padding:.45rem .75rem; border:1px solid var(--border); border-radius:6px; }
    .grid { display:grid; grid-template-columns:repeat(auto-fit,minmax(280px,1fr)); gap:1rem; margin-bottom:1.5rem; }
    .card { background:var(--card); border:1px solid var(--border); border-radius:8px; padding:1rem; }
    .card h3 { font-size:.95rem; margin-bottom:.75rem; color:var(--muted); }
    table { width:100%; border-collapse:collapse; font-size:.85rem; }
    th,td { border:1px solid var(--border); padding:.4rem .6rem; }
    th { background:#ebf4ff; }
    td.num { text-align:right; font-variant-numeric:tabular-nums; }
    .delta { font-size:.8rem; }
    .delta.up { color:#276749; }
    .delta.down { color:#c53030; }
    .chart-wrap { height:280px; position:relative; }
    .run-pill { display:inline-block; padding:.2rem .5rem; border-radius:4px; font-size:.8rem; margin-right:.25rem; color:#fff; }
    .err { background:#fed7d7; color:#c53030; padding:1rem; border-radius:8px; }
  </style>
</head>
<body>
  <div class="wrap">
    <nav class="nav"><a href="index.html">← 归档索引</a></nav>
    <h1>鱼塘模拟 · 多版本对比</h1>
    <p style="color:var(--muted);margin-bottom:1rem" id="runLabels"></p>

    <div class="controls">
      <div><label>钓鱼人数</label><select id="selAnglers"></select></div>
      <div><label>鱼塘</label><select id="selPond"></select></div>
    </div>

    <div id="err" class="err" style="display:none"></div>

    <h2 style="font-size:1.1rem;margin:1.5rem 0 .75rem">场景指标对比</h2>
    <div id="tblCompare"></div>

    <h2 style="font-size:1.1rem;margin:1.5rem 0 .75rem">时间线叠加</h2>
    <div class="grid">
      <div class="card"><h3>塘内鱼数</h3><div class="chart-wrap"><canvas id="chartPop"></canvas></div></div>
      <div class="card"><h3>平均体长 (m)</h3><div class="chart-wrap"><canvas id="chartSize"></canvas></div></div>
    </div>

    <h2 style="font-size:1.1rem;margin:1.5rem 0 .75rem">结论对比</h2>
    <div class="grid" id="conclusions"></div>
  </div>
  <script>
    const MANIFEST = ${JSON.stringify(manifest)};
    const COLORS = ['#2b6cb0','#c05621','#276749','#805ad5','#d53f8c'];
    const params = new URLSearchParams(location.search);
    const runIds = (params.get('runs') || '').split(',').filter(Boolean);

    let runs = [];
    let charts = {};

    async function loadRun(id) {
      const entry = MANIFEST.runs.find(r => r.id === id);
      if (!entry) throw new Error('归档不存在: ' + id);
      if (entry.type !== 'pond-day') throw new Error(id + ' 不是 pond-day 类型，无法对比');
      const base = 'runs/' + id + '/';
      const metaRes = await fetch(base + 'meta.json');
      if (!metaRes.ok) throw new Error(id + ' meta.json 加载失败');
      const meta = await metaRes.json();
      const compactRes = await fetch(base + 'compact.json');
      if (!compactRes.ok) throw new Error(id + ' 缺少 compact.json（仅 pond-day 归档可对比）');
      const compact = await compactRes.json();
      const analysisRes = await fetch(base + 'analysis.json');
      if (!analysisRes.ok) throw new Error(id + ' analysis.json 加载失败');
      const analysis = await analysisRes.json();
      return { id, compact, analysis, meta };
    }

    function fmtPct(v) { return v == null || Number.isNaN(v) ? '—' : v + '%'; }
    function fmtSize(v) { return v == null || Number.isNaN(v) ? '—' : v + 'm'; }
    function fmtNum(v, d) { return v == null || Number.isNaN(v) ? '—' : (d != null ? Number(v).toFixed(d) : v); }

    function unionAnglers() {
      const set = new Set();
      runs.forEach(r => (r.compact.scenarios || []).forEach(s => set.add(s.a)));
      return [...set].sort((a, b) => a - b);
    }

    function rowFor(run, anglers) {
      return run.analysis.scenarioRows.find(x => x.anglers === anglers);
    }

    function buildAnglerSelect() {
      const sel = document.getElementById('selAnglers');
      sel.innerHTML = '';
      const anglers = unionAnglers();
      const preferred = anglers.includes(5) ? 5 : anglers.includes(1) ? 1 : anglers[0];
      anglers.forEach(a => {
        const o = document.createElement('option');
        o.value = a;
        o.textContent = a + ' 人/塘';
        if (a === preferred) o.selected = true;
        sel.appendChild(o);
      });
    }

    function timelineLabels(anglers, pondIdx) {
      let best = [];
      runs.forEach(r => {
        const tl = getPond(r, anglers, pondIdx)?.tl || [];
        if (tl.length > best.length) best = tl.map(t => t.h);
      });
      return best;
    }

    function alignSeries(tl, labels) {
      if (!tl?.length) return labels.map(() => null);
      const byH = Object.fromEntries(tl.map(t => [t.h, t]));
      return labels.map(h => byH[h] ?? null);
    }

    async function init() {
      if (runIds.length < 2) {
        document.getElementById('err').style.display = 'block';
        document.getElementById('err').textContent = '请在索引页勾选至少 2 份 pond-day 归档后点击「对比已选」。';
        return;
      }
      try {
        runs = await Promise.all(runIds.map(loadRun));
      } catch (e) {
        document.getElementById('err').style.display = 'block';
        document.getElementById('err').textContent = '加载失败: ' + e.message;
        return;
      }

      document.getElementById('runLabels').innerHTML = runs.map((r, i) =>
        '<span class="run-pill" style="background:' + COLORS[i % COLORS.length] + '">' + r.meta.rulesVersion + ' · ' + r.id + '</span>'
      ).join('');

      buildAnglerSelect();

      const pondNames = new Set();
      runs.forEach(r => (r.compact.scenarios[0]?.ponds || []).forEach(p => pondNames.add(p.n)));
      const selPond = document.getElementById('selPond');
      selPond.innerHTML = '';
      const firstPonds = runs[0].compact.scenarios[0]?.ponds || [];
      firstPonds.forEach((p, i) => {
        const o = document.createElement('option');
        o.value = i; o.textContent = p.n;
        selPond.appendChild(o);
      });

      document.getElementById('selAnglers').addEventListener('change', render);
      selPond.addEventListener('change', render);
      render();
    }

    function getPond(run, anglers, pondIdx) {
      const sc = run.compact.scenarios.find(s => s.a === anglers);
      return sc ? sc.ponds[pondIdx] : null;
    }

    function renderScenarioTable() {
      const anglers = Number(document.getElementById('selAnglers').value);
      let h = '<table><tr><th>指标</th>';
      runs.forEach(r => h += '<th>' + r.meta.rulesVersion + '<br><small>' + r.id + '</small></th>');
      if (runs.length === 2) h += '<th>变化</th>';
      h += '</tr>';

      const metrics = [
        ['日均上岸', r => rowFor(r, anglers)?.perDayCaught ?? rowFor(r, anglers)?.totalCaught, true],
        ['合计上岸', r => rowFor(r, anglers)?.totalCaught, true],
        ['塘/小时上岸', r => rowFor(r, anglers)?.perPondPerHour, true],
        ['人/小时上岸', r => rowFor(r, anglers)?.perAnglerPerHour, true],
        ['末人口/上限', r => rowFor(r, anglers)?.popRatio, 'pct'],
        ['被钓均长', r => rowFor(r, anglers)?.avgCaughtSize, 'size'],
        ['剩余均长', r => rowFor(r, anglers)?.avgFinalSize, 'size'],
      ];

      metrics.forEach(([label, fn, fmt]) => {
        const raw = runs.map(fn);
        const vals = raw.map(v => {
          if (fmt === 'pct') return fmtPct(v);
          if (fmt === 'size') return fmtSize(v);
          if (fmt === true) return fmtNum(v, typeof v === 'number' && !Number.isInteger(v) ? 2 : null);
          return v ?? '—';
        });
        h += '<tr><td>' + label + '</td>';
        vals.forEach(v => h += '<td class="num">' + v + '</td>');
        if (runs.length === 2) {
          const a = raw[0], b = raw[1];
          if (typeof a === 'number' && typeof b === 'number' && !Number.isNaN(a) && !Number.isNaN(b)) {
            const d = b - a;
            const cls = d >= 0 ? 'up' : 'down';
            const sign = d >= 0 ? '+' : '';
            const txt = fmt === 'pct' ? sign + d.toFixed(1) + 'pp' : fmt === 'size' ? sign + d.toFixed(3) + 'm' : sign + (Number.isInteger(d) ? d : d.toFixed(2));
            h += '<td class="num"><span class="delta ' + cls + '">' + txt + '</span></td>';
          } else h += '<td>—</td>';
        }
        h += '</tr>';
      });
      h += '</table>';
      document.getElementById('tblCompare').innerHTML = h;
    }

    function renderCharts() {
      Object.values(charts).forEach(c => c.destroy());
      charts = {};
      const anglers = Number(document.getElementById('selAnglers').value);
      const pondIdx = Number(document.getElementById('selPond').value);
      const labels = timelineLabels(anglers, pondIdx);

      charts.pop = new Chart(document.getElementById('chartPop'), {
        type: 'line',
        data: {
          labels,
          datasets: runs.map((r, i) => {
            const p = getPond(r, anglers, pondIdx);
            const tl = p?.tl || [];
            return { label: r.meta.rulesVersion, data: alignSeries(tl, labels).map(t => t?.n ?? null), borderColor: COLORS[i], tension: 0.2, spanGaps: true };
          }),
        },
        options: { responsive: true, maintainAspectRatio: false },
      });

      charts.size = new Chart(document.getElementById('chartSize'), {
        type: 'line',
        data: {
          labels,
          datasets: runs.map((r, i) => {
            const p = getPond(r, anglers, pondIdx);
            const tl = p?.tl || [];
            return { label: r.meta.rulesVersion, data: alignSeries(tl, labels).map(t => t?.a ?? null), borderColor: COLORS[i], tension: 0.2, spanGaps: true };
          }),
        },
        options: { responsive: true, maintainAspectRatio: false },
      });
    }

    function renderConclusions() {
      document.getElementById('conclusions').innerHTML = runs.map((r, i) =>
        '<div class="card"><h3><span class="run-pill" style="background:' + COLORS[i % COLORS.length] + '">' + r.meta.rulesVersion + '</span></h3><ol style="margin-left:1.2rem;font-size:.9rem">' +
        r.analysis.conclusions.map(c => '<li style="margin-bottom:.3rem">' + c + '</li>').join('') + '</ol></div>'
      ).join('');
    }

    function render() {
      renderScenarioTable();
      renderCharts();
      renderConclusions();
    }

    init();
  </script>
</body>
</html>`;

// Build live-daily section
const DAILY_DIR = path.join(ANALYTICS_ROOT, 'daily');
const dailyReports = [];
if (fs.existsSync(DAILY_DIR)) {
  const entries = fs.readdirSync(DAILY_DIR, { withFileTypes: true })
    .filter(d => d.isDirectory())
    .sort((a, b) => b.name.localeCompare(a.name));
  for (const entry of entries) {
    const reportPath = path.join(entry.name, 'report.html');
    const compactPath = path.join(entry.name, 'compact.json');
    const compactFile = path.join(DAILY_DIR, compactPath);
    let catches = 0, players = 0;
    if (fs.existsSync(compactFile)) {
      try {
        const c = JSON.parse(fs.readFileSync(compactFile, 'utf8'));
        catches = c.totalCatches ?? 0;
        players = c.activePlayers ?? 0;
      } catch {}
    }
    dailyReports.push({ date: entry.name, catches, players, reportPath, compactPath });
  }
}

const dailySectionHtml = dailyReports.length > 0
  ? `<h2 style="margin-top:2rem;font-size:1.2rem">线上日报</h2>
<table>
  <thead><tr><th>日期</th><th>总钓获</th><th>活跃玩家</th><th>报告</th></tr></thead>
  <tbody>
    ${dailyReports.map(r => `<tr><td><code>${r.date}</code></td><td>${r.catches}</td><td>${r.players}</td><td><a href="daily/${r.reportPath}">report.html</a> · <a href="daily/${r.compactPath}">compact.json</a></td></tr>`).join('\n')}
  </tbody></table>`
  : '';

const indexHtmlWithDaily = indexHtml.replace('</table>', `</table>${dailySectionHtml}`);
fs.writeFileSync(path.join(ANALYTICS_ROOT, 'index.html'), indexHtmlWithDaily, 'utf8');
fs.writeFileSync(path.join(ANALYTICS_ROOT, 'compare.html'), compareHtml, 'utf8');
console.log('Wrote index.html, compare.html (' + manifest.runs.length + ' runs in manifest, ' + dailyReports.length + ' daily reports)');
