/**
 * 咬钩 / 脱钩概率校准报告（品质×尺寸、体长曲线）
 * 数据来源 shared/fishing.ts 公式，与运行时一致
 */
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import {
  FISH_BITE_CHECK_MS,
  FISH_QUALITIES,
  type FishQuality,
} from '@fish-social/shared';
import {
  BITE_BASE_SCALE,
  calcEffectiveEscapeRate,
  calcQualitySizeBiteRate,
  calcSingleFishBiteProbability,
  calcSizeEscapeRate,
  calcSizeHookDurationMs,
  ESCAPE_AT_40M,
  ESCAPE_AT_JUVENILE_MAX,
  ESCAPE_AT_JUVENILE_MIN,
  JUVENILE_ESCAPE_SIZE_M,
  JUVENILE_SIZE_M_MIN,
  QUALITY_BITE_BASE,
  QUALITY_SIZE_CAP,
  REFERENCE_SIZE_M,
  SIZE_BITE_K,
  TACKLES,
} from '@fish-social/shared';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const OUT_DIR = path.join(__dirname, '../docs/analytics/bite-escape-calibration');

const Q_IDS = FISH_QUALITIES.map((q) => q.id);
const Q_NAMES = FISH_QUALITIES.map((q) => q.name);
const Q_COLORS = ['#9E9E9E', '#4CAF50', '#2196F3', '#9C27B0', '#F44336', '#FF9800', '#FFC107'];

const N_TIERS = [0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1.0];
const ESCAPE_SIZES = [
  0.08, 0.1, 0.12, 0.14, 0.16, 0.18, 0.2, 0.25, 0.3, 0.35, 0.5, 1, 2, 4, 8, 12, 20, 30, 40,
];

function pct(rate: number, digits = 2): number {
  return Math.round(rate * 100 * 10 ** digits) / 10 ** digits;
}

function fmtPct(rate: number, digits = 2): string {
  return `${pct(rate, digits)}%`;
}

function fmtSize(m: number): string {
  return m < 1 ? `${m.toFixed(2)}m` : `${m}m`;
}

function fmtDuration(ms: number): string {
  if (ms < 60_000) return `${Math.round(ms / 1000)}s`;
  const min = ms / 60_000;
  if (min < 60) return `${min.toFixed(1)}min`;
  return `${(min / 60).toFixed(1)}h`;
}

function mockFish(quality: FishQuality, sizeM: number) {
  return {
    id: 'mock',
    pondId: 'mock',
    spotId: 's0',
    quality,
    speciesId: 'carp' as const,
    sizeM,
    birthSizeM: sizeM,
    bornAt: 0,
    biteMultiplier: 1,
    escapeMultiplier: 1,
    generation: 0,
  };
}

// ─── 数据集 ───────────────────────────────────────────────────────────────────
const biteByN = N_TIERS.map((n) => {
  const row: Record<string, number> = { n };
  for (const q of Q_IDS) {
    const cap = QUALITY_SIZE_CAP[q];
    row[q] = pct(calcQualitySizeBiteRate(q, cap * n));
  }
  return row;
});

const biteHeatmap = Q_IDS.map((q) => {
  const cap = QUALITY_SIZE_CAP[q];
  return N_TIERS.map((n) => ({
    n,
    sizeM: Math.round(cap * n * 1000) / 1000,
    bitePct: pct(calcQualitySizeBiteRate(q, cap * n)),
    basePct: pct(QUALITY_BITE_BASE[q] * BITE_BASE_SCALE),
  }));
});

const escapeCurve = ESCAPE_SIZES.map((sizeM) => ({
  sizeM,
  raw: pct(calcSizeEscapeRate(sizeM)),
  basic: pct(calcEffectiveEscapeRate(sizeM, 'basic', 1)),
  master: pct(calcEffectiveEscapeRate(sizeM, 'master', 1)),
  hook: calcSizeHookDurationMs(sizeM),
}));

const juvenileTable = Array.from({ length: 14 }, (_, i) => {
  const sizeM = Math.round((JUVENILE_SIZE_M_MIN + i * 0.02) * 100) / 100;
  if (sizeM > JUVENILE_ESCAPE_SIZE_M) return null;
  return {
    sizeM,
    escape: pct(calcSizeEscapeRate(sizeM)),
    biteGray: pct(calcQualitySizeBiteRate('gray', sizeM)),
  };
}).filter(Boolean);

const spotMult = 2.5;
const exampleRows = [
  { label: '幼鱼灰 0.10m', q: 'gray' as FishQuality, sizeM: 0.1 },
  { label: '灰 满尺寸 0.30m', q: 'gray' as FishQuality, sizeM: 0.3 },
  { label: '蓝 半长 1.00m', q: 'blue' as FishQuality, sizeM: 1.0 },
  { label: '紫 半长 2.25m', q: 'purple' as FishQuality, sizeM: 2.25 },
  { label: '金 满尺寸 40m', q: 'gold' as FishQuality, sizeM: 40 },
].map((ex) => {
  const fish = mockFish(ex.q, ex.sizeM);
  const baseBite = calcQualitySizeBiteRate(ex.q, ex.sizeM);
  const pBite = calcSingleFishBiteProbability(fish, spotMult, 0);
  return {
    ...ex,
    cap: QUALITY_SIZE_CAP[ex.q],
    n: Math.round((ex.sizeM / QUALITY_SIZE_CAP[ex.q]) * 1000) / 1000,
    baseBitePct: pct(baseBite),
    spotBitePct: pct(pBite),
    escapePct: pct(calcEffectiveEscapeRate(ex.sizeM, 'basic', 1)),
    pickWeight: FISH_QUALITIES.find((x) => x.id === ex.q)?.weight ?? 0,
  };
});

const payload = {
  generatedAt: new Date().toISOString(),
  constants: {
    BITE_BASE_SCALE,
    SIZE_BITE_K,
    FISH_BITE_CHECK_MS,
    JUVENILE_ESCAPE_SIZE_M,
    ESCAPE_AT_JUVENILE_MIN,
    ESCAPE_AT_JUVENILE_MAX,
    REFERENCE_SIZE_M,
    ESCAPE_AT_40M,
    spotMultExample: spotMult,
  },
  qualityCaps: QUALITY_SIZE_CAP,
  qualityBiteBase: QUALITY_BITE_BASE,
  biteByN,
  biteHeatmap: Object.fromEntries(Q_IDS.map((q, i) => [q, biteHeatmap[i]])),
  escapeCurve,
  juvenileTable,
  exampleRows,
};

function heatColor(pctVal: number, max = 5): string {
  const t = Math.min(1, pctVal / max);
  const r = Math.round(235 - t * 120);
  const g = Math.round(244 - t * 80);
  const b = Math.round(255 - t * 40);
  return `rgb(${r},${g},${b})`;
}

function renderHeatmapRows(): string {
  return Q_IDS.map((q, qi) => {
    const cap = QUALITY_SIZE_CAP[q];
    const cells = biteHeatmap[qi]!
      .map(
        (cell) =>
          `<td class="num heat" style="background:${heatColor(cell.bitePct)}">${cell.bitePct}%</td>`,
      )
      .join('');
    return `<tr><td>${Q_NAMES[qi]}<br><small style="color:var(--muted)">cap ${cap}m</small></td>${cells}</tr>`;
  }).join('');
}

const checkSec = FISH_BITE_CHECK_MS / 1000;
const masterReduction = TACKLES.find((t) => t.id === 'master')?.escapeReduction ?? 0;

const html = `<!DOCTYPE html>
<html lang="zh-CN">
<head>
  <meta charset="UTF-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>咬钩 / 脱钩概率校准</title>
  <script src="https://cdn.jsdelivr.net/npm/chart.js@4.4.1/dist/chart.umd.min.js"></script>
  <style>
    :root { --bg:#f6f7f9; --card:#fff; --text:#1a1d23; --muted:#5c6570; --border:#e2e6eb; --accent:#2b6cb0; --accent-soft:#ebf4ff; }
    * { box-sizing:border-box; margin:0; padding:0; }
    body { font-family:"Segoe UI","PingFang SC",sans-serif; background:var(--bg); color:var(--text); line-height:1.55; padding:2rem 1.5rem 4rem; }
    .wrap { max-width:1100px; margin:0 auto; }
    h1 { font-size:1.75rem; margin-bottom:.35rem; }
    h2 { font-size:1.15rem; margin:2rem 0 .75rem; padding-bottom:.35rem; border-bottom:2px solid var(--accent); }
    .meta { color:var(--muted); font-size:.9rem; margin-bottom:1.25rem; }
    .nav { margin-bottom:1.25rem; font-size:.9rem; }
    .nav a { color:var(--accent); margin-right:1rem; }
    .callout { background:var(--accent-soft); border-left:4px solid var(--accent); padding:1rem 1.2rem; border-radius:0 8px 8px 0; margin:1rem 0; font-size:.9rem; }
    .callout code { background:rgba(0,0,0,.06); padding:.1rem .35rem; border-radius:4px; }
    .grid { display:grid; grid-template-columns:1fr 1fr; gap:1rem; margin:1rem 0; }
    @media(max-width:768px){ .grid{ grid-template-columns:1fr; } }
    .card { background:var(--card); border:1px solid var(--border); border-radius:8px; padding:1rem; }
    .card h3 { font-size:.9rem; color:var(--muted); margin-bottom:.75rem; }
    .chart-wrap { position:relative; height:280px; }
    table { width:100%; border-collapse:collapse; font-size:.85rem; margin:.75rem 0; }
    th,td { border:1px solid var(--border); padding:.45rem .6rem; }
    th { background:var(--accent-soft); font-weight:600; }
    td.num { text-align:right; font-variant-numeric:tabular-nums; }
    td.heat { text-align:center; font-weight:600; font-size:.8rem; }
    .legend-dot { display:inline-block; width:10px; height:10px; border-radius:2px; margin-right:4px; }
    footer { margin-top:2.5rem; font-size:.8rem; color:var(--muted); }
  </style>
</head>
<body>
  <div class="wrap">
    <nav class="nav">
      <a href="../index.html">← 分析归档</a>
      <a href="../pond-day-simulation/report.html">生态模拟报告</a>
    </nav>
    <h1>咬钩 / 脱钩概率校准</h1>
    <p class="meta">
      生成 ${payload.generatedAt} · 数据源 <code>shared/fishing.ts</code> · 咬钩检测 ${checkSec}s/次<br>
      复现：<code>npm run report:bite-escape</code>
    </p>

    <div class="callout">
      <strong>咬钩</strong>：<code>QUALITY_BITE_BASE[q] × BITE_BASE_SCALE(${BITE_BASE_SCALE}) × (1 − ${SIZE_BITE_K} × (1−n))</code>，n = 体长 / 品质上限<br>
      <strong>脱钩</strong>（运行时）：<strong>仅体长</strong> — 幼鱼 0.08–0.35m 抬高曲线；&gt;0.35m 按 40m 锚点曲线；渔具绝对减免<br>
      <strong>抽鱼</strong>：本点按品质 <code>FISH_QUALITIES.weight</code> 抽样，与咬钩率无关
    </div>

    <h2>一、咬钩率 · 品质 × 相对尺寸 n</h2>
    <div class="grid">
      <div class="card"><h3>各品质咬钩率 vs n（满尺寸=1）</h3><div class="chart-wrap"><canvas id="chartBiteN"></canvas></div></div>
      <div class="card"><h3>脱钩率 vs 体长（幼鱼段 + 主曲线）</h3><div class="chart-wrap"><canvas id="chartEscape"></canvas></div></div>
    </div>

    <table>
      <tr><th>品质</th>${N_TIERS.map((n) => `<th class="num">n=${n}</th>`).join('')}</tr>
      ${renderHeatmapRows()}
    </table>
    <p style="font-size:.8rem;color:var(--muted)">热力格：每 ${checkSec}s 单次咬钩概率（个体 mult=1，钓点 mult=1，无饵）</p>

    <h2>二、脱钩率 · 仅体长</h2>
    <div class="grid">
      <div class="card"><h3>收杆窗口 vs 体长</h3><div class="chart-wrap"><canvas id="chartHook"></canvas></div></div>
      <div class="card"><h3>幼鱼段 0.08–0.35m</h3>
        <table>
          <tr><th>体长</th><th>脱钩率</th><th>灰鱼咬钩率</th></tr>
          ${juvenileTable.map((r) => `<tr><td class="num">${fmtSize(r!.sizeM)}</td><td class="num">${r!.escape}%</td><td class="num">${r!.biteGray}%</td></tr>`).join('')}
        </table>
      </div>
    </div>

    <table>
      <tr><th>体长</th><th>原始脱钩</th><th>基础竿</th><th>大师竿(−${pct(masterReduction)}%)</th><th>收杆窗口</th></tr>
      ${escapeCurve.map((r) => `<tr><td class="num">${fmtSize(r.sizeM)}</td><td class="num">${r.raw}%</td><td class="num">${r.basic}%</td><td class="num">${r.master}%</td><td class="num">${fmtDuration(r.hook)}</td></tr>`).join('')}
    </table>

    <h2>三、综合示例（钓点 ×${spotMult}，饵=0，mult=1）</h2>
    <table>
      <tr><th>场景</th><th>n</th><th>基础咬钩/${checkSec}s</th><th>含钓点咬钩/${checkSec}s</th><th>脱钩</th><th>品质抽中权重</th></tr>
      ${exampleRows.map((r) => `<tr>
        <td>${r.label}</td>
        <td class="num">${r.n}</td>
        <td class="num">${r.baseBitePct}%</td>
        <td class="num"><strong>${r.spotBitePct}%</strong></td>
        <td class="num">${r.escapePct}%</td>
        <td class="num">${r.pickWeight}</td>
      </tr>`).join('')}
    </table>

    <h2>四、品质上限与满尺寸基础咬钩</h2>
    <table>
      <tr><th>品质</th><th>体长上限</th><th>QUALITY_BITE_BASE</th><th>满尺寸咬钩（×SCALE）</th><th>出率权重</th></tr>
      ${Q_IDS.map((q, i) => `<tr>
        <td><span class="legend-dot" style="background:${Q_COLORS[i]}"></span>${Q_NAMES[i]}</td>
        <td class="num">${QUALITY_SIZE_CAP[q]}m</td>
        <td class="num">${fmtPct(QUALITY_BITE_BASE[q], 3)}</td>
        <td class="num">${fmtPct(calcQualitySizeBiteRate(q, QUALITY_SIZE_CAP[q]))}</td>
        <td class="num">${FISH_QUALITIES[i]!.weight}</td>
      </tr>`).join('')}
    </table>

    <footer>
      与 Admin 鱼塘调试（<code>/api/admin/ponds/:id/fishing-debug</code>）使用同一套公式。
      生态模拟中的「被钓均长」见 <a href="../pond-day-simulation/report.html">pond-day 报告</a>（实证结果，非本表理论值）。
    </footer>
  </div>

  <script>
    const N_LABELS = ${JSON.stringify(N_TIERS.map((n) => 'n=' + n))};
    const Q_NAMES = ${JSON.stringify(Q_NAMES)};
    const Q_COLORS = ${JSON.stringify(Q_COLORS)};
    const BITE_BY_N = ${JSON.stringify(biteByN)};
    const ESCAPE = ${JSON.stringify(escapeCurve)};

    const opts = { responsive: true, maintainAspectRatio: false };

    new Chart(document.getElementById('chartBiteN'), {
      type: 'line',
      data: {
        labels: N_LABELS,
        datasets: ${JSON.stringify(Q_IDS)}.map((q, i) => ({
          label: Q_NAMES[i],
          data: BITE_BY_N.map(row => row[q]),
          borderColor: Q_COLORS[i],
          tension: 0.15,
          pointRadius: 3,
        })),
      },
      options: { ...opts, scales: { y: { title: { display: true, text: '咬钩 % / ${checkSec}s' }, beginAtZero: true } } },
    });

    new Chart(document.getElementById('chartEscape'), {
      type: 'line',
      data: {
        labels: ESCAPE.map(r => r.sizeM + 'm'),
        datasets: [
          { label: '原始脱钩', data: ESCAPE.map(r => r.raw), borderColor: '#c05621', tension: 0.2 },
          { label: '基础竿', data: ESCAPE.map(r => r.basic), borderColor: '#2b6cb0', tension: 0.2 },
          { label: '大师竿', data: ESCAPE.map(r => r.master), borderColor: '#276749', tension: 0.2, borderDash: [4,3] },
        ],
      },
      options: { ...opts, scales: { y: { title: { display: true, text: '脱钩 %' }, max: 100 } } },
    });

    new Chart(document.getElementById('chartHook'), {
      type: 'line',
      data: {
        labels: ESCAPE.map(r => r.sizeM + 'm'),
        datasets: [{ label: '收杆窗口 (min)', data: ESCAPE.map(r => r.hook / 60000), borderColor: '#5c6570', tension: 0.2, fill: true, backgroundColor: 'rgba(92,101,112,.1)' }],
      },
      options: { ...opts, scales: { y: { title: { display: true, text: '分钟' }, beginAtZero: true } } },
    });
  </script>
</body>
</html>`;

fs.mkdirSync(OUT_DIR, { recursive: true });
fs.writeFileSync(path.join(OUT_DIR, 'report.html'), html, 'utf8');
fs.writeFileSync(path.join(OUT_DIR, 'data.json'), JSON.stringify(payload, null, 2), 'utf8');
console.log('Wrote', path.join(OUT_DIR, 'report.html'));
console.log('Wrote', path.join(OUT_DIR, 'data.json'));
