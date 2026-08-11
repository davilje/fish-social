/**
 * 鱼塘数据分析工具库：compact 转换、指标计算、归档元数据
 */
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
export const ANALYTICS_ROOT = path.join(__dirname, '../../docs/analytics');
export const RUNS_DIR = path.join(ANALYTICS_ROOT, 'runs');
export const Q_IDS = ['gray', 'green', 'blue', 'purple', 'red', 'orange', 'gold'];
export const Q_NAMES = ['普通', '优良', '稀有', '史诗', '传说', '神话', '至尊'];

export const DEFAULT_ANGLER_SCENARIOS = [0, 1, 3, 5, 10, 20];

export function fmtMetric(value, suffix = '') {
  if (value == null || Number.isNaN(value)) return '—';
  return `${value}${suffix}`;
}

export function unionAnglerScenarios(runs) {
  const set = new Set();
  for (const run of runs) {
    for (const s of run.compact?.scenarios ?? run.analysis?.scenarioRows ?? []) {
      set.add(s.a ?? s.anglers);
    }
  }
  return [...set].sort((a, b) => a - b);
}

export function round(n, d = 3) {
  const f = 10 ** d;
  return Math.round(n * f) / f;
}

/** data.json → compact.json */
export function dataToCompact(raw) {
  const { meta, results } = raw;
  const simDays = meta.simDays || 1;
  const tlInterval = simDays > 1 ? 8 : 4; // 2h vs 1h sampling
  const scenarios = meta.anglerScenarios.map((a) => {
    const ponds = results
      .filter((r) => r.anglers === a)
      .map((r) => ({
        id: r.pondId,
        n: r.pondName,
        mx: r.maxPopulation,
        i: {
          n: r.initial.total,
          avg: round(r.initial.avgSizeM),
          q: Q_IDS.map((_, i) => r.initial.byQuality[i].count),
          qa: Q_IDS.map((_, i) => round(r.initial.byQuality[i].avgSizeM) || null),
          h: r.initial.sizeHistogram.map((b) => b.count),
        },
        f: {
          n: r.final.total,
          avg: round(r.final.avgSizeM),
          q: r.final.byQuality.map((q) => ({
            c: q.count,
            p: round(q.pct, 1),
            a: round(q.avgSizeM),
          })),
          h: r.final.sizeHistogram.map((b) => b.count),
        },
        c: {
          n: r.caught.total,
          avg: round(r.caught.avgSizeM),
          q: r.caught.byQuality.map((q) => ({
            c: q.count,
            p: round(q.pct, 1),
            a: q.count ? round(q.avgSizeM) : null,
          })),
        },
        daily: (r.daily || []).map((d) => ({
          d: d.day,
          n: d.total,
          a: round(d.avgSizeM),
          c: d.caughtToday,
          sp: d.supplementedToday,
          pr: d.popRatio,
        })),
        tl: r.timeline
          .filter((_, i) => i % tlInterval === 0 || i === r.timeline.length - 1)
          .map((t) => ({
            h: t.label,
            n: t.total,
            a: round(t.avgSizeM),
            ct: t.caughtThisStep,
            sp: t.supplementedThisStep,
          })),
      }));
    return { a, ponds };
  });

  return {
    generatedAt: meta.generatedAt,
    seed: meta.seed,
    simDays,
    rulesVersion: meta.rules?.rulesVersion || inferRulesVersion(meta.rules),
    rules: meta.rules,
    scenarios,
  };
}

/** 从 compact 计算分析指标 */
export function computeAnalysis(compact) {
  const simDays = compact.simDays || 1;
  const simHours = simDays * 24;

  const scenarioRows = compact.scenarios.map((s) => {
    const ponds = s.ponds.length;
    let totalCaught = 0;
    let totalInit = 0;
    let totalFinal = 0;
    let totalMax = 0;
    let sumCaughtSize = 0;
    let sumFinalSize = 0;

    for (const p of s.ponds) {
      totalCaught += p.c.n;
      totalInit += p.i.n;
      totalFinal += p.f.n;
      totalMax += p.mx;
      sumCaughtSize += p.c.avg * p.c.n;
      sumFinalSize += p.f.avg * p.f.n;
    }

    const popRatio = totalMax ? (totalFinal / totalMax) * 100 : 100;
    const perPondPerHour = totalCaught / ponds / simHours;
    const perAnglerPerHour = s.a ? perPondPerHour / s.a : 0;
    const perDayCaught = totalCaught / simDays;

    const dailyTrend = [];
    for (let d = 1; d <= simDays; d++) {
      let dayCaught = 0;
      let dayPop = 0;
      for (const p of s.ponds) {
        const hit = p.daily?.find((x) => x.d === d);
        if (hit) {
          dayCaught += hit.c;
          dayPop += hit.n;
        }
      }
      dailyTrend.push({
        day: d,
        totalCaught: dayCaught,
        avgPop: ponds ? round(dayPop / ponds, 1) : 0,
        popRatio: totalMax ? round((dayPop / totalMax) * 100, 1) : 100,
      });
    }

    let status = 'sustainable';
    if (s.a === 0) status = 'baseline';
    else if (popRatio < 70) status = 'unsustainable';
    else if (popRatio < 85) status = 'consuming';

    return {
      anglers: s.a,
      totalCaught,
      perDayCaught: round(perDayCaught, 1),
      perPondPerHour: round(perPondPerHour, 2),
      perAnglerPerHour: round(perAnglerPerHour, 2),
      popRatio: round(popRatio, 1),
      avgCaughtSize: totalCaught ? round(sumCaughtSize / totalCaught) : 0,
      avgFinalSize: totalFinal ? round(sumFinalSize / totalFinal) : 0,
      status,
      dailyTrend,
    };
  });

  const s1 = compact.scenarios.find((s) => s.a === 1);
  const qualityShift = s1
    ? Q_NAMES.map((name, qi) => {
        let initN = 0;
        let caughtN = 0;
        let initSum = 0;
        let caughtSum = 0;
        let initSizeSum = 0;
        let initSizeN = 0;
        let finalSizeSum = 0;
        let finalSizeN = 0;
        let caughtSizeSum = 0;
        let caughtSizeN = 0;
        for (const p of s1.ponds) {
          const ic = p.i.q[qi];
          initSum += ic;
          initN += p.i.n;
          const ia = p.i.qa?.[qi];
          if (ic && ia) {
            initSizeSum += ia * ic;
            initSizeN += ic;
          }
          const fq = p.f.q[qi];
          if (fq?.c && fq.a) {
            finalSizeSum += fq.a * fq.c;
            finalSizeN += fq.c;
          }
          const cq = p.c.q[qi];
          caughtSum += cq.c;
          caughtN += p.c.n;
          if (cq.c && cq.a) {
            caughtSizeSum += cq.a * cq.c;
            caughtSizeN += cq.c;
          }
        }
        const initPct = initN ? (initSum / initN) * 100 : 0;
        const caughtPct = caughtN ? (caughtSum / caughtN) * 100 : 0;
        return {
          quality: name,
          initPct: round(initPct, 1),
          caughtPct: round(caughtPct, 1),
          deltaPp: round(caughtPct - initPct, 1),
          initAvg: initSizeN ? round(initSizeSum / initSizeN) : null,
          finalAvg: finalSizeN ? round(finalSizeSum / finalSizeN) : null,
          caughtAvg: caughtSizeN ? round(caughtSizeSum / caughtSizeN) : null,
        };
      })
    : [];

  const s0 = compact.scenarios.find((s) => s.a === 0);
  const supplementHours = s0
    ? s0.ponds.map((p) => {
        const hit = p.tl.find((t) => t.n >= p.mx);
        return { pond: p.n, hours: hit ? parseInt(hit.h) || 2 : 24, max: p.mx };
      })
    : [];

  const conclusions = [];
  if (simDays > 1) {
    conclusions.push(`模拟周期 ${simDays} 天连续运营（同场景鱼群状态跨日延续，无每日重置）。`);
  }
  if (scenarioRows.find((r) => r.anglers === 0)) {
    const sh = supplementHours[0]?.hours ?? 2;
    conclusions.push(`补充机制有效：无人时约 ${sh}h 内满塘。`);
  }
  const r1 = scenarioRows.find((r) => r.anglers === 1);
  if (r1) {
    conclusions.push(
      `1 人/塘为推荐运营态：人口 ${r1.popRatio}% 上限，日均 ~${Math.round(r1.perDayCaught)} 条/天（四塘），被钓均长 ${r1.avgCaughtSize}m。`,
    );
  }
  const r3 = scenarioRows.find((r) => r.anglers === 3);
  const r5 = scenarioRows.find((r) => r.anglers === 5);
  if (r3) {
    conclusions.push(
      `3 人/塘：人口 ${r3.popRatio}% 上限，日均 ~${Math.round(r3.perDayCaught)} 条/天（四塘），被钓均长 ${r3.avgCaughtSize}m。`,
    );
  }
  if (r5) {
    const targetOk = r5.perDayCaught <= 120;
    conclusions.push(
      `${targetOk ? '产量达标' : '高压场景'}：5人/塘日均 ${r5.perDayCaught} 条（${simDays}日合计 ${r5.totalCaught}），人均时产 ${r5.perAnglerPerHour}。`,
    );
    conclusions.push(
      `生态${r5.popRatio >= 85 ? '健康' : '承压'}：5人/塘人口 ${r5.popRatio}% 上限，被钓均长 ${r5.avgCaughtSize}m。`,
    );
    if (simDays > 1 && r5.dailyTrend?.length >= 2) {
      const d1 = r5.dailyTrend[0].totalCaught;
      const dN = r5.dailyTrend[r5.dailyTrend.length - 1].totalCaught;
      const drift = d1 ? Math.abs(dN - d1) / d1 : 0;
      conclusions.push(
        `7日产量走势：D1 ${d1} 条/天 → D${simDays} ${dN} 条/天，${drift < 0.15 ? '趋于稳定' : '波动明显'}（人口 D${simDays} ${r5.dailyTrend[r5.dailyTrend.length - 1].popRatio}%）。`,
      );
    }
  }
  const r10 = scenarioRows.find((r) => r.anglers === 10);
  if (r10) {
    conclusions.push(
      `10人/塘：日均 ${r10.perDayCaught} 条（${simDays}日合计 ${r10.totalCaught}），人均时产 ${r10.perAnglerPerHour}，人口 ${r10.popRatio}% 上限。`,
    );
  }
  const r20 = scenarioRows.find((r) => r.anglers === 20);
  if (r20) {
    conclusions.push(
      `20人/塘（满员）：日均 ${r20.perDayCaught} 条（${simDays}日合计 ${r20.totalCaught}），人均时产 ${r20.perAnglerPerHour}，人口 ${r20.popRatio}% 上限。`,
    );
    if (simDays > 1 && r20.dailyTrend?.length >= 2) {
      const d1 = r20.dailyTrend[0].popRatio;
      const dN = r20.dailyTrend[r20.dailyTrend.length - 1].popRatio;
      conclusions.push(
        `满员长期生态：人口 D1 ${d1}% → D${simDays} ${dN}%${dN >= 85 ? '，可持续' : '，需加强补给'}。`,
      );
    }
  }

  return {
    generatedAt: compact.generatedAt,
    seed: compact.seed,
    simDays,
    scenarioRows,
    qualityShift,
    supplementHours,
    conclusions,
  };
}

export function inferRulesVersion(rules) {
  if (rules?.rulesVersion) return rules.rulesVersion;
  if (rules?.singleFishBite || rules?.biteBaseScale != null) return 'v0.4.1';
  if (rules?.spotLocalBite) return 'v0.4.0';
  if (rules?.dynamicSupplement) return 'v0.3.2';
  if (rules?.FISH_BITE_CHECK_MS === 300000) return 'v0.3.1';
  return 'unknown';
}

export function makeRunId(type, compact, label) {
  const date = (compact.generatedAt || new Date().toISOString()).slice(0, 10);
  const ver = inferRulesVersion(compact.rules);
  const suffix = label || ver.replace(/\./g, '');
  return `${date}-${type}-${suffix}`;
}

export function ensureDir(dir) {
  fs.mkdirSync(dir, { recursive: true });
}

export function readJson(file) {
  return JSON.parse(fs.readFileSync(file, 'utf8'));
}

export function writeJson(file, data) {
  ensureDir(path.dirname(file));
  fs.writeFileSync(file, JSON.stringify(data, null, 2), 'utf8');
}

/** 扫描 runs/ + daily/ 生成 manifest */
export function buildManifest() {
  const runs = [];
  if (fs.existsSync(RUNS_DIR)) {
    const pondDayRuns = fs
      .readdirSync(RUNS_DIR, { withFileTypes: true })
      .filter((d) => d.isDirectory())
      .map((d) => {
        const dir = path.join(RUNS_DIR, d.name);
        const metaPath = path.join(dir, 'meta.json');
        if (!fs.existsSync(metaPath)) return null;
        const meta = readJson(metaPath);
        return {
          id: d.name,
          type: meta.type || 'pond-day',
          ...meta,
          reportPath: `runs/${d.name}/report.html`,
          compactPath: `runs/${d.name}/compact.json`,
          analysisPath: `runs/${d.name}/analysis.json`,
        };
      })
      .filter(Boolean);
    runs.push(...pondDayRuns);
  }

  const dailyDir = path.join(ANALYTICS_ROOT, 'daily');
  if (fs.existsSync(dailyDir)) {
    const dailyRuns = fs
      .readdirSync(dailyDir, { withFileTypes: true })
      .filter((d) => d.isDirectory())
      .map((d) => {
        const compactPath = path.join(dailyDir, d.name, 'compact.json');
        if (!fs.existsSync(compactPath)) return null;
        const compact = readJson(compactPath);
        return {
          id: `live-${d.name}`,
          type: 'live-daily',
          title: `线上日报 ${d.name}`,
          rulesVersion: compact.rulesVersion || 'live',
          generatedAt: compact.generatedAt || `${d.name}T00:00:00.000Z`,
          date: d.name,
          totalCatches: compact.totalCatches,
          activePlayers: compact.activePlayers,
          reportPath: `daily/${d.name}/report.html`,
          compactPath: `daily/${d.name}/compact.json`,
        };
      })
      .filter(Boolean);
    runs.push(...dailyRuns);
  }

  runs.sort((a, b) => {
    const dateA = a.date || (a.generatedAt || '').slice(0, 10);
    const dateB = b.date || (b.generatedAt || '').slice(0, 10);
    if (dateA !== dateB) return dateB.localeCompare(dateA);
    if (a.type === 'live-daily' && b.type !== 'live-daily') return -1;
    if (b.type === 'live-daily' && a.type !== 'live-daily') return 1;
    return (b.generatedAt || '').localeCompare(a.generatedAt || '');
  });

  return { generatedAt: new Date().toISOString(), runs };
}
