import {
  calcEntryFishLength, getFishEntryGrowthRate, getFishingFormulaConstant, growFishSizeV2,
  getSpecies, isFishEntryGrowthState, roundFishLength,
  type FishHistorySnapshot, type FishQuality, type PondFishEntity,
} from '@fish-social/shared';
import { db } from './db.js';

export interface PondFishRow {
  id: string; pond_id: string; spot_id: string; species_id: string; quality: string;
  size_m: number; born_at: number; generation: number; bite_weight: number | null;
  bite_multiplier: number | null; escape_multiplier: number | null; birth_size_m: number | null;
  entered_at: number | null; entry_size_m: number | null; juvenile_entry_size_m: number | null;
  individual_cap_m: number | null; growth_bonus_minutes: number; growth_rule_version: string | null;
  source: string | null; stocking_batch_id: string | null; out_of_pond_ms: number;
}

/** Reads calculate natural growth without turning every query into a write. */
export function fishRowToEntity(row: PondFishRow, at = Date.now()): PondFishEntity {
  const fish: PondFishEntity = {
    id: row.id, pondId: row.pond_id, spotId: row.spot_id, speciesId: row.species_id,
    quality: row.quality as FishQuality, sizeM: row.size_m, bornAt: row.born_at,
    generation: row.generation, biteMultiplier: row.bite_multiplier ?? 1,
    escapeMultiplier: row.escape_multiplier ?? 1, biteWeight: row.bite_weight,
    birthSizeM: row.growth_rule_version ? undefined : row.birth_size_m ?? row.size_m,
    ...(row.entered_at != null && row.growth_rule_version ? {
      enteredAt: row.entered_at, entrySizeM: row.entry_size_m!,
      juvenileEntrySizeM: row.juvenile_entry_size_m!, individualCapM: row.individual_cap_m!,
      outOfPondMs: row.out_of_pond_ms ?? 0,
      growthBonusMinutes: row.growth_bonus_minutes, growthRuleVersion: row.growth_rule_version,
      source: row.source as PondFishEntity['source'], stockingBatchId: row.stocking_batch_id ?? undefined,
    } : {}),
  };
  fish.sizeM = isFishEntryGrowthState(fish)
    ? roundFishLength(calcEntryFishLength(fish.quality, fish, at))
    : growFishSizeV2(fish.quality, getSpecies(fish.speciesId), fish.sizeM,
      fish.birthSizeM ?? fish.sizeM, fish.bornAt, at, fish.id);
  return fish;
}

export function readFishEntity(id: string, at = Date.now()): PondFishEntity | null {
  const row = db.prepare('SELECT * FROM pond_fish WHERE id = ?').get(id) as PondFishRow | undefined;
  return row ? fishRowToEntity(row, at) : null;
}

type FeedKind = 'hook_feed' | 'groundbait_feed' | 'return';
export interface FeedResult { fish: PondFishEntity; grantedMinutes: number; duplicate: boolean }

function grantFeed(input: {
  fishId: string; eventId: string; kind: FeedKind; at: number;
  requestedMinutes: number; portions?: number; castId?: string; payload?: Record<string, unknown>;
}): FeedResult | null {
  const fish = readFishEntity(input.fishId, input.at);
  if (!fish) return null;
  const old = db.prepare('SELECT granted_bonus_minutes FROM fish_lifecycle_events WHERE event_id = ?')
    .get(input.eventId) as { granted_bonus_minutes: number } | undefined;
  if (old) return { fish, grantedMinutes: old.granted_bonus_minutes, duplicate: true };
  const used = db.prepare(`SELECT COALESCE(SUM(granted_bonus_minutes), 0) n
    FROM fish_lifecycle_events WHERE fish_id = ? AND occurred_at > ? AND occurred_at <= ?`)
    .get(fish.id, input.at - 86_400_000, input.at) as { n: number };
  const capMinutes = getFishingFormulaConstant('FISH_NUTRITION_ROLLING_CAP_MINUTES', 360);
  const requested = Math.max(0, Number.isFinite(input.requestedMinutes) ? input.requestedMinutes : 0);
  const granted = isFishEntryGrowthState(fish) && fish.sizeM < fish.individualCapM
    ? Math.max(0, Math.min(requested, capMinutes - used.n)) : 0;
  if (granted > 0 && isFishEntryGrowthState(fish)) {
    fish.growthBonusMinutes += granted;
    fish.sizeM = roundFishLength(calcEntryFishLength(fish.quality, fish, input.at));
    db.prepare('UPDATE pond_fish SET growth_bonus_minutes = ?, size_m = ? WHERE id = ?')
      .run(fish.growthBonusMinutes, fish.sizeM, fish.id);
  }
  db.prepare(`INSERT INTO fish_lifecycle_events
    (event_id, fish_id, pond_id, spot_id, kind, occurred_at, size_m,
     requested_bonus_minutes, granted_bonus_minutes, feed_portions, groundbait_cast_id, payload_json)
    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)`)
    .run(input.eventId, fish.id, fish.pondId, fish.spotId, input.kind, input.at, fish.sizeM,
      requested, granted, input.portions ?? 0, input.castId ?? null, JSON.stringify(input.payload ?? {}));
  return { fish, grantedMinutes: granted, duplicate: false };
}

export function getHookLockedFishIds(at = Date.now()): Set<string> {
  const rows = db.prepare('SELECT fish_id FROM fish_hook_locks WHERE expires_at > ?')
    .all(at) as Array<{ fish_id: string }>;
  return new Set(rows.map(row => row.fish_id));
}

/** Reserve before awarding food; competing users cannot eat/hook the same fish. */
export function claimFishHook(
  fishId: string, catchId: string, userId: string | null, at = Date.now(), expiresAt = at + 3_600_000,
): PondFishEntity | null {
  return db.transaction(() => {
    if (!readFishEntity(fishId, at)) return null;
    const pending = db.prepare('SELECT catch_id FROM pending_catch_locks WHERE pond_fish_id = ?').get(fishId) as { catch_id: string } | undefined;
    if (pending && pending.catch_id !== catchId) return null;
    const previous = db.prepare('SELECT catch_id, expires_at FROM fish_hook_locks WHERE fish_id = ?').get(fishId) as { catch_id: string; expires_at: number } | undefined;
    if (previous && previous.expires_at > at && previous.catch_id !== catchId) return null;
    db.prepare(`INSERT INTO fish_hook_locks(fish_id, catch_id, user_id, locked_at, expires_at)
      VALUES (?, ?, ?, ?, ?) ON CONFLICT(fish_id) DO UPDATE SET catch_id=excluded.catch_id,
      user_id=COALESCE(excluded.user_id,fish_hook_locks.user_id), locked_at=excluded.locked_at,
      expires_at=excluded.expires_at`).run(fishId, catchId, userId, at, Math.max(at + 1, expiresAt));
    return grantFeed({ fishId, eventId: `hook-feed:${catchId}`, kind: 'hook_feed', at,
      requestedMinutes: getFishingFormulaConstant('FISH_HOOK_FEED_MINUTES', 15), portions: 1,
      payload: { catchId, userId } })?.fish ?? null;
  })();
}

export function releaseFishHook(catchId: string): void {
  db.prepare('DELETE FROM fish_hook_locks WHERE catch_id = ?').run(catchId);
}

export function recordFishEscape(
  fishId: string, catchId: string, at = Date.now(), spotId?: string,
): PondFishEntity | null {
  return db.transaction(() => {
    const fish = readFishEntity(fishId, at);
    if (fish) db.prepare(`INSERT OR IGNORE INTO fish_lifecycle_events
      (event_id,fish_id,pond_id,spot_id,kind,occurred_at,size_m,payload_json)
      VALUES (?,?,?,?,'escape',?,?,?)`).run(`escape:${catchId}`, fishId, fish.pondId,
        spotId ?? fish.spotId, at, fish.sizeM, JSON.stringify({ catchId }));
    releaseFishHook(catchId);
    return fish;
  })();
}

export function buildFishHistorySnapshot(
  fish: PondFishEntity, caughtAt = Date.now(), caughtSizeM = fish.sizeM,
): FishHistorySnapshot | undefined {
  if (!isFishEntryGrowthState(fish)) return undefined;
  const counts = db.prepare(`SELECT kind, COUNT(*) count, SUM(feed_portions) portions
    FROM fish_lifecycle_events WHERE fish_id = ? AND occurred_at <= ? GROUP BY kind`)
    .all(fish.id, caughtAt) as Array<{ kind: string; count: number; portions: number }>;
  const escapes = db.prepare(`SELECT occurred_at, pond_id, spot_id, size_m
    FROM fish_lifecycle_events WHERE fish_id = ? AND kind = 'escape' AND occurred_at <= ?
    ORDER BY occurred_at, event_id`).all(fish.id, caughtAt) as Array<{
      occurred_at: number; pond_id: string; spot_id: string; size_m: number;
    }>;
  return {
    fishId: fish.id, enteredAt: fish.enteredAt, entrySizeM: fish.entrySizeM, caughtAt,
    residenceMs: Math.max(0, caughtAt - fish.enteredAt - (fish.outOfPondMs ?? 0)),
    growthSinceEntryM: roundFishLength(Math.max(0, caughtSizeM - fish.entrySizeM)),
    individualCapM: fish.individualCapM, growthRuleVersion: fish.growthRuleVersion, source: fish.source,
    hookFeedCount: counts.find(c => c.kind === 'hook_feed')?.portions ?? 0,
    groundbaitFeedCount: counts.find(c => c.kind === 'groundbait_feed')?.portions ?? 0,
    escapeCount: escapes.length,
    escapes: escapes.map(e => ({ at: e.occurred_at, pondId: e.pond_id, spotId: e.spot_id, sizeM: e.size_m })),
  };
}

export function parseFishHistory(json?: string | null): FishHistorySnapshot | undefined {
  if (!json) return undefined;
  try {
    const parsed = JSON.parse(json) as FishHistorySnapshot;
    return parsed && typeof parsed.fishId === 'string' && Number.isFinite(parsed.enteredAt)
      && Number.isFinite(parsed.caughtAt) && Array.isArray(parsed.escapes) ? parsed : undefined;
  } catch { return undefined; }
}

export function archiveCapturedFish(fish: PondFishEntity, catchId: string, at: number): void {
  db.prepare(`INSERT OR IGNORE INTO fish_entity_archive
    (archive_id,fish_id,entity_json,archived_at,reason,catch_id)
    VALUES (?,?,?,?,'caught',?)`).run(`caught:${catchId}`, fish.id, JSON.stringify(fish), at, catchId);
  db.prepare(`INSERT OR IGNORE INTO fish_lifecycle_events
    (event_id,fish_id,pond_id,spot_id,kind,occurred_at,size_m,payload_json)
    VALUES (?,?,?,?,'catch',?,?,?)`).run(`catch:${catchId}`, fish.id, fish.pondId, fish.spotId,
      at, fish.sizeM, JSON.stringify({ catchId }));
  releaseFishHook(catchId);
}

export function readCapturedFish(fishId: string): PondFishEntity | null {
  const row = db.prepare(`SELECT entity_json FROM fish_entity_archive
    WHERE fish_id = ? AND reason = 'caught' ORDER BY archived_at DESC, rowid DESC LIMIT 1`)
    .get(fishId) as { entity_json: string } | undefined;
  if (!row) return null;
  try { return JSON.parse(row.entity_json) as PondFishEntity; } catch { return null; }
}

/** Convert the existing return-size award to capped growth minutes, preserving entry time. */
export function applyReturnGrowth(fishId: string, eventId: string, sizeGainM: number, at = Date.now()): FeedResult | null {
  return db.transaction(() => {
    const fish = readFishEntity(fishId, at);
    if (!fish) return null;
    let minutes = 0;
    if (isFishEntryGrowthState(fish) && sizeGainM > 0 && fish.sizeM < fish.individualCapM) {
      const c = fish.individualCapM;
      const nowLength = calcEntryFishLength(fish.quality, fish, at);
      const target = Math.min(c - 1e-9, nowLength + sizeGainM);
      const kPerMinute = getFishEntryGrowthRate(fish.quality, fish.juvenileEntrySizeM, c)/1440;
      if (kPerMinute > 0) minutes = Math.max(0, -Math.log((c-target)/(c-nowLength))/kPerMinute);
    }
    return grantFeed({ fishId, eventId, kind: 'return', at, requestedMinutes: minutes,
      payload: { requestedSizeGainM: sizeGainM } });
  })();
}

export function createGroundbaitBudget(input: {
  castId: string; playerId: string; pondId: string; spotId: string; groundbaitId: string;
  createdAt: number; expiresAt: number;
}): boolean {
  return db.prepare(`INSERT OR IGNORE INTO groundbait_casts
    (cast_id,player_id,pond_id,spot_id,groundbait_id,created_at,expires_at,portions_total)
    VALUES (?,?,?,?,?,?,?,?)`).run(input.castId, input.playerId, input.pondId, input.spotId,
      input.groundbaitId, input.createdAt, input.expiresAt,
      getFishingFormulaConstant('FISH_GROUNDBAIT_PORTIONS', 12)).changes > 0;
}

/** One observed feeder per cast per 30-second opportunity; no offline feeding replay. */
export function feedGroundbaitAtSpot(pondId: string, spotId: string, at = Date.now(), random = Math.random): number {
  return db.transaction(() => {
    db.prepare(`UPDATE groundbait_casts SET status='expired'
      WHERE pond_id=? AND status='active' AND expires_at<=?`).run(pondId, at);
    const casts = db.prepare(`SELECT cast_id FROM groundbait_casts WHERE pond_id=? AND spot_id=?
      AND status='active' AND created_at<=? AND expires_at>? AND portions_consumed<portions_total ORDER BY created_at,cast_id`)
      .all(pondId, spotId, at, at) as Array<{ cast_id: string }>;
    let fed = 0;
    for (const cast of casts) {
      const eventId = `groundbait-feed:${cast.cast_id}:${Math.floor(at/30_000)}`;
      if (db.prepare('SELECT 1 FROM fish_lifecycle_events WHERE event_id=?').get(eventId)) continue;
      const candidates = db.prepare(`SELECT f.id FROM pond_fish f WHERE f.pond_id=? AND f.spot_id=?
        AND f.growth_rule_version IS NOT NULL
        AND NOT EXISTS(SELECT 1 FROM fish_hook_locks h WHERE h.fish_id=f.id AND h.expires_at>?)
        AND NOT EXISTS(SELECT 1 FROM pending_catch_locks p WHERE p.pond_fish_id=f.id)
        AND COALESCE((SELECT SUM(e.feed_portions) FROM fish_lifecycle_events e
          WHERE e.fish_id=f.id AND e.groundbait_cast_id=?),0)<? ORDER BY f.id`)
        .all(pondId, spotId, at, cast.cast_id,
          getFishingFormulaConstant('FISH_GROUNDBAIT_MAX_PORTIONS_PER_FISH', 2)) as Array<{ id: string }>;
      if (!candidates.length) continue;
      const fish = candidates[Math.min(candidates.length-1, Math.max(0, Math.floor(random()*candidates.length)))]!;
      const result = grantFeed({ fishId: fish.id, eventId, kind: 'groundbait_feed', at, portions: 1,
        castId: cast.cast_id, requestedMinutes: getFishingFormulaConstant('FISH_GROUNDBAIT_PORTION_MINUTES', 10) });
      if (!result || result.duplicate) continue;
      db.prepare(`UPDATE groundbait_casts SET portions_consumed=portions_consumed+1,
        status=CASE WHEN portions_consumed+1>=portions_total THEN 'consumed' ELSE 'active' END WHERE cast_id=?`)
        .run(cast.cast_id);
      fed++;
    }
    return fed;
  })();
}
