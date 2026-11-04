/** A dated ruleset marks comparable full-flow simulations, never old snapshots. */
import fs from 'node:fs';
import path from 'node:path';

const registry = JSON.parse(fs.readFileSync(new URL('../fishing-rules/current-rules.json', import.meta.url), 'utf8'));
if (typeof registry.rulesetId !== 'string' || !registry.rulesetId.trim()) {
  throw new Error('现行钓鱼规则登记缺少 rulesetId，拒绝选取模拟基准。');
}
export const CURRENT_FISH_RULESET_ID = registry.rulesetId;

export function isCurrentSimulation(data) {
  return (data?.rulesetId ?? data?.meta?.rulesetId) === CURRENT_FISH_RULESET_ID &&
    (data?.modelKind ?? data?.meta?.modelKind) === 'full-flow-fishing';
}

export function rejectRetiredSimulationEntry(entry) {
  throw new Error(`[历史模拟入口已停用] ${entry} 使用旧模型，不能重建现行报告。` +
    '原代码仅保留追溯；当前规则见 docs/策划/规格/玩法/钓鱼系统现行规则.md，' +
    '现行公式报告运行 npm run report:fishing-current，验证运行 npm run verify:fishing-current。' +
    '现行完整流程模拟基准尚未建立。');
}

export function readCurrentSimulationReference(analysisPath, anglers = 5) {
  if (!fs.existsSync(analysisPath)) return null;
  try {
    const data = JSON.parse(fs.readFileSync(analysisPath, 'utf8'));
    if (!isCurrentSimulation(data)) return null;
    const row = data.scenarioRows?.find(r => r.anglers === anglers);
    return Number.isFinite(row?.perDayCaught) && row.perDayCaught >= 0 ? row.perDayCaught : null;
  } catch { return null; }
}

export function loadLatestCurrentSimulation(runsDir, anglers = 5) {
  if (!fs.existsSync(runsDir)) return null;
  const dirs = fs.readdirSync(runsDir, { withFileTypes: true })
    .filter(d => d.isDirectory()).sort((a, b) => b.name.localeCompare(a.name));
  for (const dir of dirs) {
    try {
      const base = path.join(runsDir, dir.name);
      const meta = JSON.parse(fs.readFileSync(path.join(base, 'meta.json'), 'utf8'));
      const analysis = JSON.parse(fs.readFileSync(path.join(base, 'analysis.json'), 'utf8'));
      if (meta.type !== 'pond-day' || !isCurrentSimulation(meta) || !isCurrentSimulation(analysis)) continue;
      const row = analysis.scenarioRows?.find(r => r.anglers === anglers);
      if (!Number.isFinite(row?.perDayCaught) || row.perDayCaught < 0) continue;
      return { runId: dir.name, rulesetId: CURRENT_FISH_RULESET_ID,
        rulesVersion: meta.rulesVersion ?? CURRENT_FISH_RULESET_ID,
        perDayCaught: row.perDayCaught, perAnglerPerHour: row.perAnglerPerHour ?? null,
        popRatio: row.popRatio ?? null };
    } catch { /* A partial or malformed historical run cannot become a baseline. */ }
  }
  return null;
}
