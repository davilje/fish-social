import { createHmac } from 'crypto';

const DEV_FALLBACK_PEPPER = 'dev-erase-pepper-not-for-production';

/** SEC-06: production must set PLAYER_ERASE_PEPPER before start */
export function assertErasePepperConfigured(): void {
  if (process.env.NODE_ENV === 'production' && !process.env.PLAYER_ERASE_PEPPER) {
    throw new Error('PLAYER_ERASE_PEPPER is required in production');
  }
}

export function getErasePepper(): string {
  const pepper = process.env.PLAYER_ERASE_PEPPER;
  if (pepper) return pepper;
  if (process.env.NODE_ENV === 'production') {
    throw new Error('PLAYER_ERASE_PEPPER is required in production');
  }
  return DEV_FALLBACK_PEPPER;
}

/** 不可逆匿名 ID，用于 metrics 等分析表 */
export function anonymizePlayerId(playerId: string): string {
  const digest = createHmac('sha256', getErasePepper()).update(playerId).digest('hex');
  return `anon_${digest.slice(0, 32)}`;
}

export function isAnonymizedPlayerId(playerId: string | null | undefined): boolean {
  return typeof playerId === 'string' && playerId.startsWith('anon_');
}
