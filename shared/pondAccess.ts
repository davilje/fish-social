import {
  getGamePondDef,
  type GamePondDef,
  type PondCategory,
} from './gameData.client';

export type PondAccessDenial =
  | 'pond_missing'
  | 'not_open'
  | 'onboarding_required'
  | 'novice_completed'
  | 'level_required'
  | 'giant_closed';

export interface PondAccessContext {
  onboardingCompleted: boolean;
  playerLevel: number;
}

export interface PondAccessResult {
  ok: boolean;
  error?: string;
  code?: PondAccessDenial;
  pond?: GamePondDef;
}

export function evaluatePondAccess(
  pondId: string,
  ctx: PondAccessContext,
): PondAccessResult {
  const pond = getGamePondDef(pondId);
  if (!pond) {
    // Legacy ponds without table row: allow (transitional)
    return { ok: true };
  }

  if (pond.pondCategory === 'novice') {
    if (ctx.onboardingCompleted) {
      return {
        ok: false,
        code: 'novice_completed',
        error: '新手引导已完成，无法再进入新手塘',
        pond,
      };
    }
    return { ok: true, pond };
  }

  if (!ctx.onboardingCompleted) {
    return {
      ok: false,
      code: 'onboarding_required',
      error: '请先完成新手引导',
      pond,
    };
  }

  if (pond.pondCategory === 'giant' || !pond.isOpen) {
    return {
      ok: false,
      code: pond.pondCategory === 'giant' ? 'giant_closed' : 'not_open',
      error: pond.pondCategory === 'giant' ? '巨物塘暂未开放' : '该鱼塘暂未开放',
      pond,
    };
  }

  const minLevel = Math.max(0, Number(pond.minPlayerLevel) || 0);
  if (minLevel > 0 && ctx.playerLevel < minLevel) {
    return {
      ok: false,
      code: 'level_required',
      error: `需要钓鱼等级 ${minLevel}（当前 ${ctx.playerLevel}）`,
      pond,
    };
  }

  return { ok: true, pond };
}

export function isPaidPondCategory(category: PondCategory | undefined): boolean {
  return category === 'advanced' || category === 'veteran' || category === 'giant';
}
