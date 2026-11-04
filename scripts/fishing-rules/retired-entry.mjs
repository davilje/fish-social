import path from 'node:path';
import { fileURLToPath } from 'node:url';

/** Old simulations must never overwrite current reports, even via a direct script invocation. */
export function retiredFishingEntry(entry) {
  const name = entry.startsWith('file:') ? path.basename(fileURLToPath(entry)) : entry;
  console.error(`[历史钓鱼工具已停用] ${name}\n原始脚本保存在 scripts/legacy/fishing-2026-11-04/，仅供追溯。\n现行规则：docs/策划/规格/玩法/钓鱼系统现行规则.md\n现行核对：npm run verify:fishing-current\n现行概率说明：npm run report:fishing-current\n成长补鱼压力模型：npm run simulate:fish-entry（不代表真实每小时鱼获）\n未生成或覆盖任何模拟报告。`);
  process.exit(1);
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  retiredFishingEntry(process.argv[2] ?? 'legacy fishing simulation');
}
