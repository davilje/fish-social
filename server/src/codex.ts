import type { FishSpeciesId, FishCodexEntry, CodexUnlockPayload } from '@fish-social/shared';
import { FISH_SPECIES, getSpecies } from '@fish-social/shared';
import { db } from './db.js';

const getEntryStmt = db.prepare(
  'SELECT * FROM fish_codex WHERE player_id = ? AND species_id = ?',
);
const listStmt = db.prepare('SELECT * FROM fish_codex WHERE player_id = ? ORDER BY species_id');
const upsertStmt = db.prepare(`
  INSERT INTO fish_codex (player_id, species_id, total_caught, max_size_m, first_caught_at, last_caught_at)
  VALUES (@playerId, @speciesId, @totalCaught, @maxSizeM, @firstCaughtAt, @lastCaughtAt)
  ON CONFLICT(player_id, species_id) DO UPDATE SET
    total_caught = excluded.total_caught,
    max_size_m = excluded.max_size_m,
    first_caught_at = COALESCE(fish_codex.first_caught_at, excluded.first_caught_at),
    last_caught_at = excluded.last_caught_at
`);

function rowToEntry(row: {
  species_id: string;
  total_caught: number;
  max_size_m: number;
  first_caught_at: number | null;
  last_caught_at: number | null;
}): FishCodexEntry {
  return {
    speciesId: row.species_id as FishSpeciesId,
    totalCaught: row.total_caught,
    maxSizeM: row.max_size_m,
    firstCaughtAt: row.first_caught_at,
    lastCaughtAt: row.last_caught_at,
  };
}

export function isCodexNewForPlayer(playerId: string, speciesId: FishSpeciesId): boolean {
  const existing = getEntryStmt.get(playerId, speciesId) as { total_caught: number } | undefined;
  return (existing?.total_caught ?? 0) === 0;
}

export function getPlayerCodex(playerId: string): FishCodexEntry[] {
  const rows = listStmt.all(playerId) as Array<{
    species_id: string;
    total_caught: number;
    max_size_m: number;
    first_caught_at: number | null;
    last_caught_at: number | null;
  }>;
  const byId = new Map(rows.map((r) => [r.species_id, rowToEntry(r)]));
  return FISH_SPECIES.map((s) => byId.get(s.id) ?? {
    speciesId: s.id,
    totalCaught: 0,
    maxSizeM: 0,
    firstCaughtAt: null,
    lastCaughtAt: null,
  });
}

export function recordCodexCatch(
  playerId: string,
  speciesId: FishSpeciesId,
  sizeM: number,
): CodexUnlockPayload | null {
  const now = Date.now();
  const existing = getEntryStmt.get(playerId, speciesId) as
    | {
        total_caught: number;
        max_size_m: number;
        first_caught_at: number | null;
      }
    | undefined;

  const isFirstCatch = !existing || existing.total_caught === 0;
  const totalCaught = (existing?.total_caught ?? 0) + 1;
  const maxSizeM = Math.max(existing?.max_size_m ?? 0, sizeM);
  const firstCaughtAt = existing?.first_caught_at ?? now;

  upsertStmt.run({
    playerId,
    speciesId,
    totalCaught,
    maxSizeM,
    firstCaughtAt,
    lastCaughtAt: now,
  });

  if (!isFirstCatch) return null;
  const species = getSpecies(speciesId);
  return { speciesId, speciesName: species.name, isFirstCatch: true };
}
