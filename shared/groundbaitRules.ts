/**
 * FEAT-GROUND-01：打窝非线性加成公式（纯函数，可单测）。
 */
import type { GroundbaitDef } from './gameDataTypes';

export const GROUNDBAIT_MAX_STACK_DEFAULT = 50;
export const GROUNDBAIT_BITE_MUL_GLOBAL_CAP_DEFAULT = 1.5;

/** bonus = maxBonus × (1 - exp(-stackK × stackCount)) */
export function calcGroundbaitBiteBonus(
  stackCount: number,
  maxBonus: number,
  stackK: number,
): number {
  const stack = Math.max(0, Math.floor(stackCount));
  if (stack <= 0 || maxBonus <= 0 || stackK <= 0) return 0;
  return maxBonus * (1 - Math.exp(-stackK * stack));
}

export function calcGroundbaitSizeBonus(
  stackCount: number,
  sizeBonusPerStack: number,
  maxSizeBonus: number,
): number {
  const stack = Math.max(0, Math.floor(stackCount));
  if (stack <= 0) return 0;
  return Math.min(maxSizeBonus, sizeBonusPerStack * stack);
}

/** 线性满额（禁止作为实际公式）：perStack × stack */
export function linearGroundbaitBiteCap(perStack: number, stack: number): number {
  return Math.max(0, perStack) * Math.max(0, Math.floor(stack));
}

export function applyBiteMulGlobalCap(
  effectiveMul: number,
  globalCap: number = GROUNDBAIT_BITE_MUL_GLOBAL_CAP_DEFAULT,
): number {
  const cap = globalCap > 0 ? globalCap : GROUNDBAIT_BITE_MUL_GLOBAL_CAP_DEFAULT;
  return Math.min(effectiveMul, cap);
}

export function computeGroundbaitBuffs(
  def: Pick<GroundbaitDef, 'maxBonus' | 'stackK' | 'sizeBonusPerStack' | 'maxSizeBonus'>,
  stackCount: number,
): { biteBonus: number; sizeBonus: number } {
  return {
    biteBonus: calcGroundbaitBiteBonus(stackCount, def.maxBonus, def.stackK),
    sizeBonus: calcGroundbaitSizeBonus(
      stackCount,
      def.sizeBonusPerStack,
      def.maxSizeBonus,
    ),
  };
}
