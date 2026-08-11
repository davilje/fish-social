/**
 * 从 summary.json 生成运营日报 HTML
 */
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import { readJson } from './lib.mjs';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const summaryPath = process.argv[2] || path.join(__dirname, '../../docs/analytics/daily/latest/summary.json');
const outPath = process.argv[3] || path.join(path.dirname(summaryPath), 'report.html');

const SUMMARY = readJson(summaryPath);
const { meta, kpis, sections, alerts } = SUMMARY;

const KPI_LABELS = {
  kpi_daily_catch: '日钓总量（背包）',
  kpi_daily_catch_human: '日钓·真人',
  kpi_daily_catch_bot: '日钓·机器人',
  kpi_dau: 'DAU',
  kpi_fishing_dau: '钓鱼 DAU',
  kpi_catch_per_fisher: '人均上岸（真人）',
  kpi_disconnect_rate: '断线率',
  kpi_abandon_rate: '弃钓率',
  kpi_avg_pop_ratio: '四塘平均人口率',
};

const QUALITY_NAMES = {
  gray: '普通', green: '优良', blue: '稀有', purple: '史诗',
  red: '传说', orange: '神话', gold: '至尊',
};

function fmtVal(kpi, suffix = '') {
  if (kpi?.value == null) return '—';
  return `${kpi.value}${suffix}`;
}

function fmtDelta(kpi) {
  if (kpi?.deltaPct == null) return '';
  const sign = kpi.deltaPct >= 0 ? '+' : '';
  const cls = kpi.deltaPct >= 0 ? 'delta-pos' : 'delta-neg';
  return `<span class="${cls}">${sign}${kpi.deltaPct}%</span> vs 前日`;
}

function badge(status) {
  if (!status) return '';
  const labels = { ok: '正常', warn: '关注', bad: '异常' };
  return `<span class="badge ${status}">${labels[status] || status}</span>`;
}

function fmtTime(ms) {
  if (ms == null) return '—';
  return new Date(ms).toLocaleString('zh-CN', { timeZone: 'Asia/Shanghai' });
}

function fmtMs(ms) {
  if (ms == null) return '—';
  const m = Math.round(ms / 60000);
  return m >= 60 ? `${Math.floor(m / 60)}h${m % 60}m` : `${m}m`;
}

const kpiCards = Object.entries(KPI_LABELS).map(([id, label]) => {
  const k = kpis[id];
  const note = k?.note ? `<div class="note">${k.note}</div>` : '';
  const suffix = id.includes('rate') || id.includes('pop_ratio') ? '%' : '';
  return `<div class="stat">
    <div class="val">${fmtVal(k, suffix)} ${badge(k?.status)}</div>
    <div class="lbl">${label}</div>
    <div class="sub">${k?.prev != null ? `前日 ${k.prev}${suffix}` : ''} ${k?.avg7d != null ? `· 7日均 ${k.avg7d}${suffix}` : ''}</div>
    <div class="sub">${fmtDelta(k)}</div>${note}
  </div>`;
}).join('\n');

const players = sections.players || {};
const retention = sections.retention || {};
const stability = sections.stability || {};
const economy = sections.economy || {};
const ecology = sections.ecology || {};

const pondCatchRows = (sections.catch?.byPond || []).map((p) => {
  const split =
    p.catchesHuman != null || p.catchesBot != null
      ? `<td class="num">${p.catchesHuman ?? 0}</td><td class="num">${p.catchesBot ?? 0}</td>`
      : '';
  return `<tr><td>${p.pondId}</td><td class="num">${p.catches}</td>${split}<td class="num">${p.hookCount ?? p.biteTickHit ?? 0}</td><td class="num">${p.escapeCount ?? 0}</td><td class="num">${p.disconnects}</td><td class="num">${p.avgPopulation ?? '—'}</td><td class="num">${p.popRatio != null ? p.popRatio + '%' : '—'}</td></tr>`;
}).join('\n');

const qualityRows = (sections.catch?.byQuality || []).map((q) => {
  const split =
    q.human != null || q.bot != null
      ? `<td class="num">${q.human ?? 0}</td><td class="num">${q.bot ?? 0}</td>`
      : '';
  return `<tr><td>${q.label}</td><td class="num">${q.count}</td>${split}</tr>`;
}).join('\n');

const pondCatchHasSplit = (sections.catch?.byPond || []).some(
  (p) => p.catchesHuman != null || p.catchesBot != null,
);
const qualityHasSplit = (sections.catch?.byQuality || []).some(
  (q) => q.human != null || q.bot != null,
);

const leaveRows = (stability.leaveByReason || []).map((r) =>
  `<tr><td>${r.reason}</td><td class="num">${r.count}</td></tr>`,
).join('\n');

const ecoRows = (ecology.ponds || []).map((p) => {
  const qParts = Object.entries(p.byQuality || {}).map(([q, c]) => `${QUALITY_NAMES[q] || q}:${c}`);
  return `<tr>
    <td>${p.pondId}</td><td class="num">${p.population ?? '—'}</td><td class="num">${p.maxPopulation ?? '—'}</td>
    <td class="num">${p.popRatio != null ? p.popRatio + '%' : '—'} ${badge(p.status)}</td>
    <td class="num">${p.avgSizeM ?? '—'}</td><td>${qParts.join(' · ') || '—'}</td>
  </tr>`;
}).join('\n');

const faucetRows = (economy.breakdown?.faucetBySource || []).map((r) =>
  `<tr><td>${r.source}</td><td class="num">${r.total}</td></tr>`,
).join('\n');
const sinkRows = (economy.breakdown?.sinkByType || []).map((r) =>
  `<tr><td>${r.type}</td><td class="num">${r.total}</td></tr>`,
).join('\n');

const rulesRows = (sections.rulesHistory || []).map((r) =>
  `<tr><td>${r.dateKey}</td><td>${r.rulesVersion}</td><td class="num">${r.dailyCatch ?? '—'}</td></tr>`,
).join('\n');

const tc = sections.targetCompare || {};
const alertItems = (alerts || []).map((a) =>
  `<li class="alert-${a.level}"><strong>${a.id}</strong>：${a.message}</li>`,
).join('\n');

const comp = meta.completeness || {};
const economyNote = economy.note ? `<p class="note-warn">${economy.note}</p>` : '';
const retentionNote = retention.note || retention.d7Note || '';

const html = `<!DOCTYPE html>
<html lang="zh-CN">
<head>
  <meta charset="UTF-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>运营日报 ${meta.dateKey}</title>
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
    h3 { font-size:1rem; margin:1rem 0 .5rem; }
    .card { background:var(--card); border:1px solid var(--border); border-radius:8px; padding:1.25rem; margin-bottom:1rem; }
    .stats { display:grid; grid-template-columns:repeat(auto-fit,minmax(140px,1fr)); gap:.75rem; margin-bottom:1.5rem; }
    .stat { background:var(--card); border:1px solid var(--border); border-radius:8px; padding:1rem; }
    .stat .val { font-size:1.35rem; font-weight:700; color:var(--accent); }
    .stat .lbl { font-size:.8rem; color:var(--muted); margin-top:.25rem; }
    .stat .sub { font-size:.75rem; color:var(--muted); margin-top:.2rem; }
    .stat .note, .note-warn { font-size:.8rem; color:var(--warn); margin-top:.25rem; }
    table { width:100%; border-collapse:collapse; font-size:.9rem; margin-bottom:1rem; }
    th,td { border:1px solid var(--border); padding:.5rem .75rem; text-align:left; }
    th { background:var(--accent-soft); font-weight:600; }
    td.num { text-align:right; font-variant-numeric:tabular-nums; }
    .badge { display:inline-block; padding:.15rem .5rem; border-radius:4px; font-size:.7rem; font-weight:600; margin-left:.25rem; }
    .badge.ok { background:#c6f6d5; color:var(--ok); }
    .badge.warn { background:#feebc8; color:var(--warn); }
    .badge.bad { background:#fed7d7; color:var(--bad); }
    .delta-pos { color:var(--ok); } .delta-neg { color:var(--bad); }
    .alerts { background:#fff5f5; border-left:4px solid var(--bad); padding:1rem 1.25rem; border-radius:0 8px 8px 0; }
    .alerts li { margin-bottom:.35rem; }
    .alert-bad { color:var(--bad); } .alert-warn { color:var(--warn); }
    .target-grid { display:grid; grid-template-columns:repeat(auto-fit,minmax(160px,1fr)); gap:1rem; }
    .target-item { text-align:center; }
    .target-item .num { font-size:1.5rem; font-weight:700; }
    .target-item .lbl { font-size:.8rem; color:var(--muted); }
    footer { margin-top:3rem; font-size:.8rem; color:var(--muted); }
  </style>
</head>
<body>
  <div class="wrap">
    <nav class="nav">
      <a href="../../index.html">← 分析归档索引</a>
      <a href="../../live-vs-sim.html">线上 vs 模拟</a>
      <a href="../../pond-day-simulation/report.html">模拟报告</a>
    </nav>
    <h1>运营日报 · ${meta.dateKey}</h1>
    <p class="meta">
      统计日 ${meta.dateKey}（Asia/Shanghai）· 规则 <strong>${meta.rulesVersion}</strong> · 生成 ${meta.generatedAt}<br>
      数据完整度：${comp.metricCount ?? 0} 条事件 · 首事件 ${fmtTime(comp.firstEventAt)} · 末事件 ${fmtTime(comp.lastEventAt)}
    </p>

    <h2>§1 健康度总览</h2>
    <div class="stats">${kpiCards}</div>

    <h2>§2 玩家与活跃</h2>
    <div class="card">
      <p>DAU ${players.dau ?? '—'} · 钓鱼 DAU ${players.fishingDau ?? '—'} · 人均 session ${players.sessionsPerFisher ?? '—'}</p>
      <p>人均钓鱼时长 ${fmtMs(players.avgFishingMs)} · P50 ${fmtMs(players.p50FishingMs)} · P90 ${fmtMs(players.p90FishingMs)}</p>
      <p>留存 cohort ${retention.cohortSize ?? 0} 人 · D1 ${retention.d1Rate != null ? retention.d1Rate + '%' : '—'} · D7 ${retention.d7Rate != null ? retention.d7Rate + '%' : '—'}${retentionNote ? `（${retentionNote}）` : ''}</p>
    </div>

    <h2>§3 钓鱼产出</h2>
    <div class="card">
      <p>上钩 ${sections.catch?.hookCount ?? 0}
        · 上钩频率 ${sections.catch?.hookFrequencyPerHour != null ? sections.catch.hookFrequencyPerHour + '/时' : '—'}
        · 脱钩率 ${sections.catch?.escapeRate != null ? (sections.catch.escapeRate * 100).toFixed(1) + '%' : '—'}
        · 获鱼率 ${sections.catch?.catchRate != null ? (sections.catch.catchRate * 100).toFixed(1) + '%' : '—'}
        · pending 超时率 ${sections.catch?.pendingExpiredRate != null ? (sections.catch.pendingExpiredRate * 100).toFixed(1) + '%' : '—'}</p>
      <p class="note">${sections.catch?.metricNote || '自 D-L2-15 起不再使用 tick hit/miss'}</p>
    </div>
    <h3>分塘明细</h3>
    <p class="meta">钓获口径：背包入库${pondCatchHasSplit ? '（含真人/机器人分列；无 pond_id 历史行按入库时间对齐 metrics 塘）' : ''}</p>
    <table><tr><th>鱼塘</th><th>钓获</th>${pondCatchHasSplit ? '<th>真人</th><th>机器人</th>' : ''}<th>上钩</th><th>脱钩</th><th>断线</th><th>均人口</th><th>人口率</th></tr>
      ${pondCatchRows || `<tr><td colspan="${pondCatchHasSplit ? 9 : 7}">暂无数据</td></tr>`}
    </table>
    <h3>品质分布</h3>
    <p class="meta">品质口径：背包入库${qualityHasSplit ? '（含真人/机器人分列）' : ''}</p>
    <table><tr><th>品质</th><th>数量</th>${qualityHasSplit ? '<th>真人</th><th>机器人</th>' : ''}</tr>${qualityRows || `<tr><td colspan="${qualityHasSplit ? 4 : 2}">暂无</td></tr>`}</table>

    <h2>§4 体验与稳定</h2>
    <div class="card">
      <p>断线 ${stability.disconnectCount ?? 0} · 重连 ${stability.reconnectCount ?? 0} · 超时清场 ${stability.disconnectTimeoutCount ?? 0}</p>
      <p>join 尝试 ${stability.joinAttempts ?? 0} · 成功 ${stability.joinSuccessCount ?? 0} · 失败 ${stability.joinFails ?? 0}
        · 失败率 ${stability.joinFailRate != null ? (stability.joinFailRate * 100).toFixed(1) + '%' : '—'}</p>
      <p>phase 异常 ${stability.phaseInvalidCount ?? 0}</p>
    </div>
    <h3>leave 按 reason</h3>
    <table><tr><th>reason</th><th>次数</th></tr>${leaveRows || '<tr><td colspan="2">暂无</td></tr>'}</table>

    <h2>§5 经济 faucet / sink</h2>
    ${economyNote}
    <div class="card target-grid">
      <div class="target-item"><div class="num">${economy.faucetTotal ?? '—'}</div><div class="lbl">faucet（金币获得）</div></div>
      <div class="target-item"><div class="num">${economy.sinkTotal ?? 0}</div><div class="lbl">sink（金币消耗）</div></div>
      <div class="target-item"><div class="num">${economy.net ?? '—'}</div><div class="lbl">净变化</div></div>
    </div>
    <h3>faucet 分项</h3>
    <table><tr><th>来源</th><th>金额</th></tr>${faucetRows || '<tr><td colspan="2">暂无</td></tr>'}</table>
    <h3>sink 分项</h3>
    <table><tr><th>类型</th><th>金额</th></tr>${sinkRows || '<tr><td colspan="2">暂无</td></tr>'}</table>

    <h2>§6 鱼塘生态健康</h2>
    <p class="meta">${ecology.snapshotNote || ''}</p>
    <table>
      <tr><th>鱼塘</th><th>人口</th><th>上限</th><th>人口率</th><th>均体长(m)</th><th>品质分布</th></tr>
      ${ecoRows || '<tr><td colspan="6">暂无快照</td></tr>'}
    </table>

    <h2>§7 目标对照</h2>
    <p class="meta">${tc.note || '昨日实测=背包入库总量（含机器人）'}</p>
    <div class="card target-grid">
      <div class="target-item"><div class="num">${tc.target ?? 100}</div><div class="lbl">设计目标</div></div>
      <div class="target-item"><div class="num">${tc.simRef ?? '—'}</div><div class="lbl">模拟参考</div></div>
      <div class="target-item"><div class="num">${tc.actual ?? tc.actualTotal ?? '—'}</div><div class="lbl">昨日实测（总量）</div></div>
      <div class="target-item"><div class="num">${tc.actualHuman ?? '—'}</div><div class="lbl">其中真人</div></div>
      <div class="target-item"><div class="num">${tc.targetDeviationPct != null ? (tc.targetDeviationPct >= 0 ? '+' : '') + tc.targetDeviationPct + '%' : '—'}</div><div class="lbl">总量 vs 目标</div></div>
      <div class="target-item"><div class="num">${tc.deviationPct != null ? (tc.deviationPct >= 0 ? '+' : '') + tc.deviationPct + '%' : '—'}</div><div class="lbl">总量 vs 模拟</div></div>
    </div>
    <h3>近 7 日规则版本与日钓</h3>
    <table><tr><th>日期</th><th>rulesVersion</th><th>日钓</th></tr>${rulesRows || '<tr><td colspan="3">暂无</td></tr>'}</table>

    <h2 id="alerts">§8 异常清单</h2>
    <div class="alerts">${alertItems ? `<ul>${alertItems}</ul>` : '<p>当日无异常触发</p>'}</div>

    <footer>数据来源 inventory + fishing_metrics · <code>npm run analytics:daily</code></footer>
  </div>
</body>
</html>`;

fs.writeFileSync(outPath, html, 'utf8');
console.log(`[generate-daily-ops-report] wrote ${outPath}`);
