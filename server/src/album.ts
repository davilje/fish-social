import { randomUUID } from 'node:crypto';
import type { AlbumCard, AlbumCardSource, FishQuality } from '@fish-social/shared';
import { getAlbumPinCap, getGamePondDef, getSpecies, qualityIndex } from '@fish-social/shared';
import { db } from './db.js';

export { getAlbumPinCap };
type CandidateRow = {
  candidate_id: string;
  player_id: string;
  species_id: string;
  quality: string;
  size_m: number;
  pond_id: string | null;
  pond_name: string | null;
  source: string;
  event_at: number;
  inventory_item_id: string | null;
};

type PinRow = {
  pin_id: string;
  player_id: string;
  sort_order: number;
  candidate_id: string | null;
  species_id: string;
  quality: string;
  size_m: number;
  pond_id: string | null;
  pond_name: string | null;
  source: string;
  event_at: number;
  pinned_at: number;
};

function rowToCard(row: CandidateRow | PinRow, idKey: 'candidate_id' | 'pin_id'): AlbumCard {
  const id = idKey === 'candidate_id'
    ? (row as CandidateRow).candidate_id
    : (row as PinRow).pin_id;
  return {
    id,
    speciesId: row.species_id,
    quality: row.quality,
    sizeM: row.size_m,
    pondId: row.pond_id,
    pondName: row.pond_name,
    source: row.source as AlbumCardSource,
    eventAt: row.event_at,
    inventoryItemId: 'inventory_item_id' in row ? row.inventory_item_id : null,
  };
}

export function listAlbumCandidates(playerId: string, limit = 40): AlbumCard[] {
  const rows = db
    .prepare(
      `SELECT * FROM player_album_candidates
       WHERE player_id = ?
       ORDER BY event_at DESC
       LIMIT ?`,
    )
    .all(playerId, limit) as CandidateRow[];
  return rows.map((r) => rowToCard(r, 'candidate_id'));
}

export function listAlbumPins(playerId: string): AlbumCard[] {
  const rows = db
    .prepare(
      `SELECT * FROM player_album_pins
       WHERE player_id = ?
       ORDER BY sort_order ASC`,
    )
    .all(playerId) as PinRow[];
  return rows.map((r) => rowToCard(r, 'pin_id'));
}

export function countAlbumPins(playerId: string): number {
  const row = db
    .prepare(`SELECT COUNT(*) AS c FROM player_album_pins WHERE player_id = ?`)
    .get(playerId) as { c: number };
  return row?.c ?? 0;
}

/** 稀有(blue)及以上 / 尺寸达种典型上限 80%+ / 回鱼 / 首次图鉴 */
export function shouldAutoCandidate(input: {
  quality: string;
  sizeM: number;
  speciesId: string;
  source: AlbumCardSource;
  isFirstCodex?: boolean;
}): boolean {
  if (input.source === 'return') return true;
  if (input.source === 'first_codex' || input.isFirstCodex) return true;
  if (qualityIndex(input.quality as FishQuality) >= qualityIndex('blue')) return true;
  const species = getSpecies(input.speciesId as never);
  const maxM = species?.typicalMaxM ?? 0;
  if (maxM > 0 && input.sizeM >= maxM * 0.8) return true;
  return false;
}

export function addAlbumCandidate(input: {
  playerId: string;
  speciesId: string;
  quality: string;
  sizeM: number;
  pondId?: string | null;
  source: AlbumCardSource;
  inventoryItemId?: string | null;
  eventAt?: number;
}): AlbumCard | null {
  if (
    !shouldAutoCandidate({
      quality: input.quality,
      sizeM: input.sizeM,
      speciesId: input.speciesId,
      source: input.source,
      isFirstCodex: input.source === 'first_codex',
    })
  ) {
    return null;
  }

  const pondName = input.pondId
    ? (getGamePondDef(input.pondId)?.name ?? input.pondId)
    : null;
  const candidateId = randomUUID();
  const eventAt = input.eventAt ?? Date.now();
  db.prepare(
    `INSERT INTO player_album_candidates
      (candidate_id, player_id, species_id, quality, size_m, pond_id, pond_name, source, event_at, inventory_item_id)
     VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)`,
  ).run(
    candidateId,
    input.playerId,
    input.speciesId,
    input.quality,
    input.sizeM,
    input.pondId ?? null,
    pondName,
    input.source,
    eventAt,
    input.inventoryItemId ?? null,
  );

  // FEAT-ALBUM-01 UI：相册自动入墙，无需玩家钉选候选
  autoAppendAlbumPin(input.playerId, candidateId);

  return {
    id: candidateId,
    speciesId: input.speciesId,
    quality: input.quality,
    sizeM: input.sizeM,
    pondId: input.pondId ?? null,
    pondName,
    source: input.source,
    eventAt,
    inventoryItemId: input.inventoryItemId ?? null,
  };
}

/** 自动写入精选墙；满则挤掉最旧一张 */
function autoAppendAlbumPin(playerId: string, candidateId: string): void {
  const existing = listAlbumPins(playerId);
  if (existing.some((p) => p.id === candidateId)) return;
  const cap = getAlbumPinCap();
  const ids = existing.map((p) => p.id);
  while (ids.length >= cap) ids.shift();
  ids.push(candidateId);
  setAlbumPins(playerId, ids);
}

/** 旧数据：墙为空但已有候选时，按时间取最近 N 张入墙（一次性兼容） */
export function seedAlbumPinsFromCandidatesIfEmpty(playerId: string): void {
  if (countAlbumPins(playerId) > 0) return;
  const cands = listAlbumCandidates(playerId, getAlbumPinCap());
  if (cands.length === 0) return;
  setAlbumPins(
    playerId,
    cands.map((c) => c.id),
  );
}

export function setAlbumPins(
  playerId: string,
  candidateIds: string[],
): { ok: true; pins: AlbumCard[]; pinCount: number } | { ok: false; error: string; code?: string } {
  const cap = getAlbumPinCap();
  if (!Array.isArray(candidateIds)) {
    return { ok: false, error: '缺少钉选列表', code: 'BAD_REQUEST' };
  }
  if (candidateIds.length > cap) {
    return { ok: false, error: `相册精选最多 ${cap} 张`, code: 'PIN_CAP' };
  }

  const unique = [...new Set(candidateIds.map(String))];
  const getCand = db.prepare(
    `SELECT * FROM player_album_candidates WHERE player_id = ? AND candidate_id = ?`,
  );
  const cards: CandidateRow[] = [];
  for (const id of unique) {
    const row = getCand.get(playerId, id) as CandidateRow | undefined;
    if (!row) return { ok: false, error: '候选不存在', code: 'CANDIDATE_MISSING' };
    cards.push(row);
  }

  const now = Date.now();
  const tx = db.transaction(() => {
    db.prepare(`DELETE FROM player_album_pins WHERE player_id = ?`).run(playerId);
    const insert = db.prepare(
      `INSERT INTO player_album_pins
        (player_id, pin_id, sort_order, candidate_id, species_id, quality, size_m, pond_id, pond_name, source, event_at, pinned_at)
       VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)`,
    );
    cards.forEach((c, i) => {
      insert.run(
        playerId,
        c.candidate_id,
        i,
        c.candidate_id,
        c.species_id,
        c.quality,
        c.size_m,
        c.pond_id,
        c.pond_name,
        c.source,
        c.event_at,
        now,
      );
    });
  });
  tx();

  const pins = listAlbumPins(playerId);
  return { ok: true, pins, pinCount: pins.length };
}

export function pinAlbumCandidate(
  playerId: string,
  candidateId: string,
): { ok: true; pins: AlbumCard[]; pinCount: number; action: 'pin' } | { ok: false; error: string; code?: string } {
  const existing = listAlbumPins(playerId);
  if (existing.some((p) => p.id === candidateId)) {
    return { ok: true, pins: existing, pinCount: existing.length, action: 'pin' };
  }
  if (existing.length >= getAlbumPinCap()) {
    return { ok: false, error: `相册精选已满（${getAlbumPinCap()}）`, code: 'PIN_CAP' };
  }
  return {
    ...setAlbumPins(
      playerId,
      [...existing.map((p) => p.id), candidateId],
    ),
    action: 'pin' as const,
  } as
    | { ok: true; pins: AlbumCard[]; pinCount: number; action: 'pin' }
    | { ok: false; error: string; code?: string };
}

export function unpinAlbumPin(
  playerId: string,
  pinId: string,
): { ok: true; pins: AlbumCard[]; pinCount: number; action: 'unpin' } | { ok: false; error: string; code?: string } {
  const existing = listAlbumPins(playerId).map((p) => p.id).filter((id) => id !== pinId);
  const result = setAlbumPins(playerId, existing);
  if (!result.ok) return result;
  return { ...result, action: 'unpin' };
}
