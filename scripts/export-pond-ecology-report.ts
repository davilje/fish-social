/**
 * 导出鱼塘初始生态数据分布与成长曲线（JSON + HTML 报告 + SVG 图表）
 * 基于 shared: rollFishQuality, rollInitialSize, growFishSizeV2
 */
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import {
  FISH_QUALITIES,
  getSpecies,
  rollFishQuality,
  PONDS,
  POND_GROWTH_RATE_PER_HOUR,
} from '@fish-social/shared';
import {
  growFishSizeV2,
  QUALITY_SIZE_CAP,
  rollInitialSize,
} from '@fish-social/shared';
import { getPondStockConfig } from '@fish-social/shared';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const OUT_DIR = path.join(__dirname, '../docs/analytics/pond-ecology-initial');
const CHARTS_DIR = path.join(OUT_DIR, 'charts');

const MONTE_CARLO_N = 8000;
const GROWTH_HOURS = [0, 6, 12, 24, 48, 72, 120, 168, 240, 360, 480];
const GROWTH_SPECIES_ID = 'carp' as const;

const SIZE_BUCKETS = [
  { label: '0–0.05m', lo: 0, hi: 0.05 },
  { label: '0.05–0.1m', lo: 0.05, hi: 0.1 },
  { label: '0.1–0.2m', lo: 0.1, hi: 0.2 },
  { label: '0.2–0.35m', lo: 0.2, hi: 0.35 },
  { label: '0.35–0.5m', lo: 0.35, hi: 0.5 },
  { label: '0.5–0.8m', lo: 0.5, hi: 0.8 },
  { label: '0.8–1.2m', lo: 0.8, hi: 1.2 },
  { label: '1.2–2m', lo: 1.2, hi: 2 },
  { label: '2–4m', lo: 2, hi: 4 },
];

type QualityRow = {
  id: string;
  name: string;
  color: string;
  count: number;
  pct: number;
  avgSize: number;
};

type GrowthRow = {
  quality: string;
  name: string;
  color: string;
  cap: number;
  birth: number;
  points: { h: number; size: number }[];
};

type PondDataset = {
  pondId: string;
  pondName: string;
  initial: number;
  maxPopulation: number;
  qDist: QualityRow[];
  hist: { label: string; count: number }[];
  growth: GrowthRow[];
};

function pickSpecies(pondId: string): string {
  const config = getPondStockConfig(pondId)!;
  const pool =
    Math.random() < config.rareSpawnRate ? config.rareSpecies : config.commonSpecies;
  return pool[Math.floor(Math.random() * pool.length)];
}

function simulatePond(pondId: string, initial: number): Omit<PondDataset, 'growth' | 'pondName'> {
  const qualityCounts = Object.fromEntries(FISH_QUALITIES.map((q) => [q.id, 0]));
  const byQualitySize = Object.fromEntries(
    FISH_QUALITIES.map((q) => [q.id, [] as number[]]),
  );
  const sizes: number[] = [];

  for (let i = 0; i < MONTE_CARLO_N; i++) {
    const sid = pickSpecies(pondId);
    const sp = getSpecies(sid as Parameters<typeof getSpecies>[0]);
    const q = rollFishQuality();
    const sz = rollInitialSize(q, sp);
    qualityCounts[q]++;
    sizes.push(sz);
    byQualitySize[q].push(sz);
  }

  const qDist: QualityRow[] = FISH_QUALITIES.map((q) => {
    const n = qualityCounts[q.id];
    const avg =
      byQualitySize[q.id].length > 0
        ? byQualitySize[q.id].reduce((a, b) => a + b, 0) / byQualitySize[q.id].length
        : 0;
    return {
      id: q.id,
      name: q.name,
      color: q.color,
      count: Math.round((n / MONTE_CARLO_N) * initial),
      pct: +((n / MONTE_CARLO_N) * 100).toFixed(1),
      avgSize: +avg.toFixed(3),
    };
  });

  const sum = qDist.reduce((s, q) => s + q.count, 0);
  if (sum < initial) qDist[0].count += initial - sum;
  if (sum > initial) qDist[0].count -= sum - initial;

  const hist = SIZE_BUCKETS.map(({ label, lo, hi }) => {
    const c = sizes.filter((s) => s >= lo && s < hi).length;
    return { label, count: Math.round((c / MONTE_CARLO_N) * initial) };
  });

  return { pondId, initial, maxPopulation: getPondStockConfig(pondId)!.maxPopulation, qDist, hist };
}

function buildGrowthCurves(): GrowthRow[] {
  const species = getSpecies(GROWTH_SPECIES_ID);
  return FISH_QUALITIES.map((q) => {
    const qIdx = FISH_QUALITIES.findIndex((x) => x.id === q.id);
    const cap = QUALITY_SIZE_CAP[q.id];
    const floor = Math.max(0.03, species.typicalMinM * 0.6);
    const ceiling = Math.min(cap, species.typicalMaxM * 0.45);
    const r = Math.pow(0.5, 2 + qIdx * 0.3);
    let cur = floor + (ceiling - floor) * r;
    const pts: { h: number; size: number }[] = [{ h: 0, size: +cur.toFixed(3) }];
    for (let i = 1; i < GROWTH_HOURS.length; i++) {
      const dh = GROWTH_HOURS[i] - GROWTH_HOURS[i - 1];
      cur = growFishSizeV2(q.id, species, cur, dh);
      pts.push({ h: GROWTH_HOURS[i], size: +cur.toFixed(3) });
    }
    return {
      quality: q.id,
      name: q.name,
      color: q.color,
      cap,
      birth: pts[0].size,
      points: pts,
    };
  });
}

function buildDatasets(): {
  generatedAt: string;
  monteCarloSamples: number;
  growthSpecies: string;
  growthRatePerHour: number;
  qualitySizeCap: typeof QUALITY_SIZE_CAP;
  ponds: PondDataset[];
} {
  const growth = buildGrowthCurves();
  const ponds: PondDataset[] = PONDS.map((p) => {
    const config = getPondStockConfig(p.id)!;
    const sim = simulatePond(p.id, config.initialPopulation);
    return {
      ...sim,
      pondName: p.name,
      growth,
    };
  });
  return {
    generatedAt: new Date().toISOString(),
    monteCarloSamples: MONTE_CARLO_N,
    growthSpecies: GROWTH_SPECIES_ID,
    growthRatePerHour: POND_GROWTH_RATE_PER_HOUR,
    qualitySizeCap: QUALITY_SIZE_CAP,
    ponds,
  };
}

function esc(s: string): string {
  return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/"/g, '&quot;');
}

function svgPie(
  items: { label: string; value: number; color: string }[],
  w = 280,
  h = 280,
): string {
  const cx = w / 2;
  const cy = h / 2;
  const r = Math.min(w, h) / 2 - 8;
  const ir = r * 0.52;
  const total = items.reduce((s, i) => s + i.value, 0) || 1;
  let angle = -Math.PI / 2;
  const slices = items
    .filter((i) => i.value > 0)
    .map((item) => {
      const sweep = (item.value / total) * Math.PI * 2;
      const x1 = cx + r * Math.cos(angle);
      const y1 = cy + r * Math.sin(angle);
      angle += sweep;
      const x2 = cx + r * Math.cos(angle);
      const y2 = cy + r * Math.sin(angle);
      const large = sweep > Math.PI ? 1 : 0;
      const ix1 = cx + ir * Math.cos(angle - sweep);
      const iy1 = cy + ir * Math.sin(angle - sweep);
      const ix2 = cx + ir * Math.cos(angle);
      const iy2 = cy + ir * Math.sin(angle);
      const d = `M ${x1} ${y1} A ${r} ${r} 0 ${large} 1 ${x2} ${y2} L ${ix2} ${iy2} A ${ir} ${ir} 0 ${large} 0 ${ix1} ${iy1} Z`;
      return `<path d="${d}" fill="${item.color}" stroke="#1a2c33" stroke-width="1"><title>${esc(item.label)}: ${item.value}</title></path>`;
    })
    .join('\n');
  return `<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}" viewBox="0 0 ${w} ${h}">${slices}</svg>`;
}

function svgBar(
  categories: string[],
  values: number[],
  color = '#4a90a4',
  w = 520,
  h = 260,
): string {
  const pad = { l: 36, r: 12, t: 16, b: 56 };
  const iw = w - pad.l - pad.r;
  const ih = h - pad.t - pad.b;
  const max = Math.max(...values, 1);
  const bw = iw / categories.length;
  const bars = values
    .map((v, i) => {
      const bh = (v / max) * ih;
      const x = pad.l + i * bw + bw * 0.15;
      const y = pad.t + ih - bh;
      const bw2 = bw * 0.7;
      return `<rect x="${x}" y="${y}" width="${bw2}" height="${bh}" fill="${color}" rx="2"><title>${esc(categories[i])}: ${v}</title></rect>`;
    })
    .join('');
  const labels = categories
    .map((c, i) => {
      const x = pad.l + i * bw + bw / 2;
      const y = h - 8;
      return `<text x="${x}" y="${y}" fill="#8aa4ad" font-size="9" text-anchor="end" transform="rotate(-35 ${x} ${y})">${esc(c)}</text>`;
    })
    .join('');
  return `<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}" viewBox="0 0 ${w} ${h}">
  <line x1="${pad.l}" y1="${pad.t + ih}" x2="${pad.l + iw}" y2="${pad.t + ih}" stroke="#3a5560"/>
  <line x1="${pad.l}" y1="${pad.t}" x2="${pad.l}" y2="${pad.t + ih}" stroke="#3a5560"/>
  ${bars}${labels}
</svg>`;
}

function svgLineChart(series: GrowthRow[], w = 640, h = 320): string {
  const pad = { l: 48, r: 16, t: 16, b: 40 };
  const iw = w - pad.l - pad.r;
  const ih = h - pad.t - pad.b;
  const cats = series[0]?.points.map((p) => p.h) ?? [];
  const allY = series.flatMap((s) => s.points.map((p) => p.size));
  const maxY = Math.max(...allY, 0.1);
  const xStep = cats.length > 1 ? iw / (cats.length - 1) : iw;

  const grid = cats
    .map((h, i) => {
      const x = pad.l + i * xStep;
      return `<line x1="${x}" y1="${pad.t}" x2="${x}" y2="${pad.t + ih}" stroke="#243a44" stroke-dasharray="3 3"/>`;
    })
    .join('');

  const paths = series
    .map((s) => {
      const d = s.points
        .map((p, i) => {
          const x = pad.l + i * xStep;
          const y = pad.t + ih - (p.size / maxY) * ih;
          return `${i === 0 ? 'M' : 'L'} ${x} ${y}`;
        })
        .join(' ');
      return `<path d="${d}" fill="none" stroke="${s.color}" stroke-width="2.5"/><text x="${pad.l + iw - 4}" y="${pad.t + ih - (s.points[s.points.length - 1].size / maxY) * ih}" fill="${s.color}" font-size="10" text-anchor="end">${esc(s.name)}</text>`;
    })
    .join('');

  const xLabels = cats
    .map((hour, i) => {
      const x = pad.l + i * xStep;
      return `<text x="${x}" y="${pad.t + ih + 18}" fill="#8aa4ad" font-size="10" text-anchor="middle">${hour}h</text>`;
    })
    .join('');

  return `<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}" viewBox="0 0 ${w} ${h}">
  <text x="8" y="${pad.t + 8}" fill="#8aa4ad" font-size="10">体长 (m)</text>
  ${grid}
  <line x1="${pad.l}" y1="${pad.t + ih}" x2="${pad.l + iw}" y2="${pad.t + ih}" stroke="#3a5560"/>
  <line x1="${pad.l}" y1="${pad.t}" x2="${pad.l}" y2="${pad.t + ih}" stroke="#3a5560"/>
  ${paths}${xLabels}
</svg>`;
}

function fishSvg(color: string, sizeM: number, w = 48): string {
  const width = Math.max(14, Math.min(52, 14 + sizeM * 90));
  const height = width * 0.55;
  const scale = width / 48;
  return `<svg width="${width}" height="${height}" viewBox="0 0 48 28" aria-hidden="true">
  <ellipse cx="22" cy="14" rx="17" ry="10" fill="${color}"/>
  <polygon points="38,14 48,7 48,21" fill="${color}"/>
  <circle cx="12" cy="11" r="2.5" fill="#e8f0f2"/>
</svg>`;
}

function buildFishSwarmHtml(qDist: QualityRow[]): string {
  const fish: string[] = [];
  for (const q of qDist) {
    for (let i = 0; i < q.count; i++) {
      const jitter = ((i % 5) - 2) * 0.008;
      const sizeM = Math.max(0.03, q.avgSize + jitter);
      fish.push(
        `<span title="${esc(q.name)} · ${sizeM.toFixed(2)}m">${fishSvg(q.color, sizeM)}</span>`,
      );
    }
  }
  return fish.join('');
}

function writeHtmlReport(data: ReturnType<typeof buildDatasets>): void {
  const pondSections = data.ponds
    .map((pond) => {
      const pieFile = `charts/${pond.pondId}-quality.svg`;
      const barFile = `charts/${pond.pondId}-size-hist.svg`;
      const legend = pond.qDist
        .filter((q) => q.count > 0)
        .map(
          (q) =>
            `<tr><td><span style="display:inline-block;width:12px;height:12px;background:${q.color};border-radius:2px"></span> ${esc(q.name)}</td><td>${q.count}</td><td>${q.pct}%</td><td>${q.avgSize}m</td></tr>`,
        )
        .join('');
      return `
<section id="${pond.pondId}" class="pond-panel">
  <h2>${esc(pond.pondName)} <small>(${pond.pondId})</small></h2>
  <p class="meta">初始 ${pond.initial} 条 · 上限 ${pond.maxPopulation} · Monte Carlo ${data.monteCarloSamples} 次采样缩放</p>
  <div class="swarm">${buildFishSwarmHtml(pond.qDist)}</div>
  <div class="chart-row">
    <figure><figcaption>品质分布</figcaption><img src="${pieFile}" alt="品质分布" width="280"/></figure>
    <figure><figcaption>体长分布</figcaption><img src="${barFile}" alt="体长分布" width="520"/></figure>
  </div>
  <table class="data-table"><thead><tr><th>品质</th><th>条数</th><th>占比</th><th>均长</th></tr></thead><tbody>${legend}</tbody></table>
</section>`;
    })
    .join('');

  const growthTable = data.ponds[0].growth
    .map(
      (g) =>
        `<tr><td style="color:${g.color}">${esc(g.name)}</td><td>${g.birth}m</td><td>${g.points[g.points.length - 1].size}m</td><td>${g.cap}m</td></tr>`,
    )
    .join('');

  const html = `<!DOCTYPE html>
<html lang="zh-CN">
<head>
  <meta charset="UTF-8"/>
  <meta name="viewport" content="width=device-width, initial-scale=1"/>
  <title>鱼塘初始生态 · 体型 · 品质 · 成长</title>
  <style>
    :root { --bg:#0f1a1e; --surface:#1a2c33; --text:#e8f0f2; --muted:#8aa4ad; --accent:#4a90a4; }
    * { box-sizing:border-box; }
    body { font-family:"Segoe UI","PingFang SC","Microsoft YaHei",sans-serif; background:var(--bg); color:var(--text); margin:0; padding:24px; line-height:1.5; }
    h1 { font-size:1.6rem; margin:0 0 8px; }
    h2 { font-size:1.2rem; margin:24px 0 8px; }
    h2 small { color:var(--muted); font-weight:normal; }
    .meta, .note { color:var(--muted); font-size:.9rem; }
    .pond-panel { background:var(--surface); border-radius:12px; padding:20px; margin:20px 0; border:1px solid #243a44; }
    .swarm { display:flex; flex-wrap:wrap; gap:6px; align-items:flex-end; padding:12px; background:#243a44; border-radius:8px; margin:12px 0; }
    .swarm span { line-height:0; }
    .chart-row { display:flex; flex-wrap:wrap; gap:24px; margin:16px 0; }
    figure { margin:0; }
    figcaption { color:var(--muted); font-size:.85rem; margin-bottom:8px; }
    .data-table { width:100%; border-collapse:collapse; font-size:.9rem; margin-top:12px; }
    .data-table th, .data-table td { border:1px solid #243a44; padding:8px 10px; text-align:left; }
    .data-table th { background:#243a44; }
    .growth-section { background:var(--surface); border-radius:12px; padding:20px; margin:20px 0; border:1px solid #243a44; }
    nav a { color:var(--accent); margin-right:12px; }
    .callout { background:#1a3a4a; border-left:4px solid var(--accent); padding:12px 16px; border-radius:0 8px 8px 0; margin:16px 0; }
  </style>
</head>
<body>
  <h1>鱼塘初始生态 · 体型 · 品质 · 成长</h1>
  <p class="note">生成时间 ${esc(data.generatedAt)} · 公式：rollFishQuality + rollInitialSize + growFishSizeV2 · 成长示范鱼种：${data.growthSpecies} · POND_GROWTH_RATE_PER_HOUR=${data.growthRatePerHour}</p>
  <div class="callout">高品质鱼出生时体型更小，但品质尺寸上限更高 → 长期成长空间更大。初始种群约 90% 体长落在 0.05–0.2m。</div>
  <nav>${data.ponds.map((p) => `<a href="#${p.pondId}">${esc(p.pondName)}</a>`).join('')}<a href="#growth">成长曲线</a></nav>
  ${pondSections}
  <section id="growth" class="growth-section">
    <h2>成长曲线（按品质 · ${data.growthSpecies} 鲤鱼示范）</h2>
    <p class="meta">纵轴体长(m) · 横轴存活时间(h) · 见 charts/growth-curves-carp.svg</p>
    <img src="charts/growth-curves-carp.svg" alt="成长曲线" width="640"/>
    <table class="data-table"><thead><tr><th>品质</th><th>出生</th><th>480h</th><th>上限</th></tr></thead><tbody>${growthTable}</tbody></table>
  </section>
  <p class="note">数据文件：<code>data.json</code> · 由 <code>npm run export:pond-ecology</code> 生成</p>
</body>
</html>`;
  fs.writeFileSync(path.join(OUT_DIR, 'report.html'), html, 'utf8');
}

function main(): void {
  fs.mkdirSync(CHARTS_DIR, { recursive: true });
  const data = buildDatasets();
  fs.writeFileSync(path.join(OUT_DIR, 'data.json'), JSON.stringify(data, null, 2), 'utf8');

  for (const pond of data.ponds) {
    const pie = svgPie(
      pond.qDist
        .filter((q) => q.count > 0)
        .map((q) => ({ label: q.name, value: q.count, color: q.color })),
    );
    fs.writeFileSync(path.join(CHARTS_DIR, `${pond.pondId}-quality.svg`), pie, 'utf8');

    const bar = svgBar(
      pond.hist.map((h) => h.label),
      pond.hist.map((h) => h.count),
    );
    fs.writeFileSync(path.join(CHARTS_DIR, `${pond.pondId}-size-hist.svg`), bar, 'utf8');
  }

  const growthSvg = svgLineChart(data.ponds[0].growth);
  fs.writeFileSync(path.join(CHARTS_DIR, 'growth-curves-carp.svg'), growthSvg, 'utf8');

  writeHtmlReport(data);

  const readme = `# 鱼塘初始生态分析报告

由 \`scripts/export-pond-ecology-report.ts\` 自动生成，对应 Canvas「鱼塘初始生态 · 体型 · 品质 · 成长」。

## 文件

| 文件 | 说明 |
|------|------|
| [report.html](./report.html) | 完整报告（图标墙 + 图表 + 表格），浏览器直接打开 |
| [data.json](./data.json) | 原始数据（四塘品质/体长分布 + 成长曲线点） |
| [charts/](./charts/) | 静态 SVG 图表 |

## 重新生成

\`\`\`bash
npm run export:pond-ecology
\`\`\`

生成时间：${data.generatedAt}
`;
  fs.writeFileSync(path.join(OUT_DIR, 'README.md'), readme, 'utf8');

  console.log(`Exported to ${OUT_DIR}`);
  console.log(`  - data.json`);
  console.log(`  - report.html`);
  console.log(`  - charts/*.svg (${data.ponds.length * 2 + 1} files)`);
}

main();
