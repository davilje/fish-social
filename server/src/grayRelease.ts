export function hashPlayerCohort(playerId: string): number {
  let hash = 0;
  for (let i = 0; i < playerId.length; i++) {
    hash = (hash * 31 + playerId.charCodeAt(i)) >>> 0;
  }
  return hash % 100;
}

export function isPlayerInGrayCohort(playerId: string, percent: number): boolean {
  if (percent >= 100) return true;
  if (percent <= 0) return false;
  return hashPlayerCohort(playerId) < percent;
}
