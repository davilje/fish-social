import type { Server } from 'socket.io';
import {
  getBait,
  getTackle,
  type BaitId,
  type ClientToServerEvents,
  type PlayerGearState,
  type ServerToClientEvents,
  type TackleId,
} from '@fish-social/shared';
import { getConfigBool } from './gameConfig.js';
import { recordFishingMetric } from './fishingMetrics.js';
import { deductCoins } from './players.js';
import { resolveSocketByPlayer } from './sessionRegistry.js';
import { db } from './db.js';

const DEFAULT_DURABILITY = 100;
const REPAIR_COST_RATIO = 0.05;

interface GearRow {
  player_id: string;
  equipped_bait: string;
  equipped_tackle: string;
  bait_inventory: string;
  owned_tackles: string;
  tackle_durability: string;
  updated_at: number;
}

const getGearStmt = db.prepare('SELECT * FROM player_gear WHERE player_id = ?');
const playerExistsStmt = db.prepare('SELECT 1 AS ok FROM players WHERE player_id = ? LIMIT 1');

function playerExists(playerId: string): boolean {
  return !!playerExistsStmt.get(playerId);
}

function defaultGearState(): PlayerGearState {
  return {
    equippedBait: 'basic',
    equippedTackle: 'basic',
    baitInventory: { corn: 5 },
    ownedTackles: ['basic'],
    tackleDurability: { basic: DEFAULT_DURABILITY },
  };
}
const upsertGearStmt = db.prepare(`
  INSERT INTO player_gear (
    player_id, equipped_bait, equipped_tackle, bait_inventory, owned_tackles, tackle_durability, updated_at
  ) VALUES (
    @playerId, @equippedBait, @equippedTackle, @baitInventory, @ownedTackles, @tackleDurability, @updatedAt
  )
  ON CONFLICT(player_id) DO UPDATE SET
    equipped_bait = @equippedBait,
    equipped_tackle = @equippedTackle,
    bait_inventory = @baitInventory,
    owned_tackles = @ownedTackles,
    tackle_durability = @tackleDurability,
    updated_at = @updatedAt
`);

function parseBaitInventory(raw: string): Record<string, number> {
  try {
    const parsed = JSON.parse(raw) as Record<string, number>;
    if (!parsed || typeof parsed !== 'object') return {};
    const out: Record<string, number> = {};
    for (const [k, v] of Object.entries(parsed)) {
      if (typeof v === 'number' && v > 0) out[k] = Math.floor(v);
    }
    return out;
  } catch {
    return {};
  }
}

function parseOwnedTackles(raw: string): TackleId[] {
  try {
    const parsed = JSON.parse(raw) as string[];
    if (!Array.isArray(parsed)) return ['basic'];
    const ids = parsed.filter((id) => getTackle(id)) as TackleId[];
    return ids.includes('basic') ? ids : (['basic', ...ids] as TackleId[]);
  } catch {
    return ['basic'];
  }
}

function parseTackleDurability(raw: string | undefined): Record<string, number> {
  try {
    const parsed = JSON.parse(raw ?? '{}') as Record<string, number>;
    if (!parsed || typeof parsed !== 'object') return {};
    const out: Record<string, number> = {};
    for (const [k, v] of Object.entries(parsed)) {
      if (typeof v === 'number' && v >= 0) out[k] = Math.min(100, Math.floor(v));
    }
    return out;
  } catch {
    return {};
  }
}

function ensureDurabilityForOwned(gear: PlayerGearState): Record<string, number> {
  const d = { ...gear.tackleDurability };
  for (const id of gear.ownedTackles) {
    if (d[id] === undefined) d[id] = DEFAULT_DURABILITY;
  }
  return d;
}

function rowToGear(row: GearRow): PlayerGearState {
  return {
    equippedBait: (getBait(row.equipped_bait)?.id ?? 'basic') as BaitId,
    equippedTackle: (getTackle(row.equipped_tackle)?.id ?? 'basic') as TackleId,
    baitInventory: parseBaitInventory(row.bait_inventory),
    ownedTackles: parseOwnedTackles(row.owned_tackles),
    tackleDurability: parseTackleDurability(row.tackle_durability),
  };
}

function saveGear(playerId: string, gear: PlayerGearState): PlayerGearState {
  const normalized = normalizeGear(gear);
  if (!playerExists(playerId)) {
    return normalized;
  }
  upsertGearStmt.run({
    playerId,
    equippedBait: normalized.equippedBait,
    equippedTackle: normalized.equippedTackle,
    baitInventory: JSON.stringify(normalized.baitInventory),
    ownedTackles: JSON.stringify(normalized.ownedTackles),
    tackleDurability: JSON.stringify(normalized.tackleDurability),
    updatedAt: Date.now(),
  });
  return normalized;
}

export function normalizeGear(gear: PlayerGearState): PlayerGearState {
  const baitInventory = { ...gear.baitInventory };
  let equippedBait = gear.equippedBait;
  const bait = getBait(equippedBait);
  if (!bait) equippedBait = 'basic';
  else if (bait.consumed) {
    const count = baitInventory[equippedBait] ?? 0;
    if (count <= 0) equippedBait = 'basic';
  }

  let equippedTackle = gear.equippedTackle;
  const ownedTackles = [...new Set(gear.ownedTackles.filter((id) => getTackle(id)))] as TackleId[];
  if (!ownedTackles.includes('basic')) ownedTackles.unshift('basic');
  if (!getTackle(equippedTackle) || !ownedTackles.includes(equippedTackle)) {
    equippedTackle = 'basic';
  }

  const tackleDurability = ensureDurabilityForOwned({
    ...gear,
    ownedTackles,
    equippedBait,
    equippedTackle,
  });

  if (
    getConfigBool('C3_SINK_ENABLED', true) &&
    equippedTackle !== 'basic' &&
    (tackleDurability[equippedTackle] ?? DEFAULT_DURABILITY) <= 0
  ) {
    equippedTackle = 'basic';
  }

  return {
    equippedBait,
    equippedTackle,
    baitInventory,
    ownedTackles,
    tackleDurability,
  };
}

export function ensurePlayerGear(playerId: string): PlayerGearState {
  const row = getGearStmt.get(playerId) as GearRow | undefined;
  if (row) return normalizeGear(rowToGear(row));

  const gear = defaultGearState();
  if (!playerExists(playerId)) {
    return normalizeGear(gear);
  }
  return saveGear(playerId, gear);
}

export function getPlayerGear(playerId: string): PlayerGearState | undefined {
  const row = getGearStmt.get(playerId) as GearRow | undefined;
  return row ? normalizeGear(rowToGear(row)) : undefined;
}

export function setPlayerGear(playerId: string, gear: PlayerGearState): PlayerGearState {
  return saveGear(playerId, gear);
}

export function addBaitToInventory(
  playerId: string,
  baitId: BaitId,
  quantity: number,
): PlayerGearState {
  const gear = ensurePlayerGear(playerId);
  gear.baitInventory[baitId] = (gear.baitInventory[baitId] ?? 0) + quantity;
  return saveGear(playerId, gear);
}

export function addOwnedTackle(playerId: string, tackleId: TackleId): PlayerGearState {
  const gear = ensurePlayerGear(playerId);
  if (!gear.ownedTackles.includes(tackleId)) {
    gear.ownedTackles.push(tackleId);
    gear.tackleDurability[tackleId] = DEFAULT_DURABILITY;
  }
  return saveGear(playerId, gear);
}

export function equipBait(playerId: string, baitId: BaitId): PlayerGearState {
  const gear = ensurePlayerGear(playerId);
  gear.equippedBait = baitId;
  return saveGear(playerId, normalizeGear(gear));
}

export function equipTackle(playerId: string, tackleId: TackleId): PlayerGearState {
  const gear = ensurePlayerGear(playerId);
  gear.equippedTackle = tackleId;
  return saveGear(playerId, normalizeGear(gear));
}

/** C6：baiting 阶段起点扣饵 */
export function consumeBaitAtBaitingStart(playerId: string): {
  gear: PlayerGearState;
  depletedPreviousBaitId?: BaitId;
  insufficient?: boolean;
} {
  let gear = ensurePlayerGear(playerId);
  const bait = getBait(gear.equippedBait);

  if (!bait || !bait.consumed) {
    return { gear };
  }

  const current = gear.baitInventory[gear.equippedBait] ?? 0;
  if (current <= 0) {
    return { gear, insufficient: true };
  }

  gear.baitInventory[gear.equippedBait] = current - 1;
  if (gear.baitInventory[gear.equippedBait] <= 0) {
    delete gear.baitInventory[gear.equippedBait];
    const previous = gear.equippedBait;
    gear.equippedBait = 'basic';
    gear = saveGear(playerId, gear);
    return { gear, depletedPreviousBaitId: previous };
  }

  return { gear: saveGear(playerId, gear) };
}

/** @deprecated C6 后改在 baiting 扣饵；保留供 Bot 简化路径 */
export function consumeBaitOnBiteSuccess(playerId: string): {
  gear: PlayerGearState;
  depletedPreviousBaitId?: BaitId;
} {
  return consumeBaitAtBaitingStart(playerId);
}

export function prepareGearForBiteTick(playerId: string): PlayerGearState {
  const gear = ensurePlayerGear(playerId);
  const normalized = normalizeGear(gear);
  if (
    normalized.equippedBait !== gear.equippedBait ||
    normalized.equippedTackle !== gear.equippedTackle
  ) {
    return saveGear(playerId, normalized);
  }
  return normalized;
}

/** C3：脱钩扣耐久 */
export function applyTackleDurabilityOnEscape(
  playerId: string,
  io?: Server<ClientToServerEvents, ServerToClientEvents>,
): PlayerGearState | null {
  if (!getConfigBool('C3_SINK_ENABLED', true)) return null;

  const gear = ensurePlayerGear(playerId);
  const tackleId = gear.equippedTackle;
  if (tackleId === 'basic') return null;

  const current = gear.tackleDurability[tackleId] ?? DEFAULT_DURABILITY;
  gear.tackleDurability[tackleId] = Math.max(0, current - 1);
  const saved = saveGear(playerId, gear);

  if (saved.tackleDurability[tackleId] <= 0 && io) {
    const socketId = resolveSocketByPlayer(playerId);
    if (socketId) io.to(socketId).emit('gear_updated', saved);
  }
  return saved;
}

export function repairTackle(
  playerId: string,
  tackleId: TackleId,
): { ok: true; gear: PlayerGearState; cost: number; coins: number } | { ok: false; error: string } {
  const tackle = getTackle(tackleId);
  if (!tackle || tackle.id === 'basic') {
    return { ok: false, error: '无法修理该渔具' };
  }

  const gear = ensurePlayerGear(playerId);
  if (!gear.ownedTackles.includes(tackleId)) {
    return { ok: false, error: '未拥有该渔具' };
  }

  const durability = gear.tackleDurability[tackleId] ?? DEFAULT_DURABILITY;
  if (durability >= DEFAULT_DURABILITY) {
    return { ok: false, error: '渔具无需修理' };
  }

  const cost = Math.max(1, Math.floor(tackle.price * REPAIR_COST_RATIO));
  const spend = deductCoins(playerId, cost);
  if (!spend.ok) {
    return { ok: false, error: '金币不足' };
  }

  gear.tackleDurability[tackleId] = DEFAULT_DURABILITY;
  const saved = saveGear(playerId, gear);
  recordFishingMetric('tackle_repair', {
    playerId,
    payload: { tackleId, cost },
  });

  return { ok: true, gear: saved, cost, coins: spend.coins };
}

export function getTackleDurability(playerId: string, tackleId: TackleId): number {
  const gear = ensurePlayerGear(playerId);
  return gear.tackleDurability[tackleId] ?? DEFAULT_DURABILITY;
}
