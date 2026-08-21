import type { Server } from 'socket.io';
import {
  BASIC_BAIT_ID,
  STARTER_ROD_ID,
  getBait,
  getTackle,
  getRodDef,
  getVesselDef,
  getGameBaitDef,
  hasUsableRod,
  isOversizeForRod,
  noUsableRodError,
  normalizeBaitId,
  pickBaitForDiet,
  shouldDestroyRod,
  unlockedBaitsForPlayerLevel,
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
  equipped_rod?: string;
  owned_rods?: string;
  rod_oversize?: string;
  unlocked_baits?: string;
  owned_vessels?: string;
  starter_rod_granted?: number;
}

const getGearStmt = db.prepare('SELECT * FROM player_gear WHERE player_id = ?');
const playerExistsStmt = db.prepare('SELECT 1 AS ok FROM players WHERE player_id = ? LIMIT 1');

function playerExists(playerId: string): boolean {
  return !!playerExistsStmt.get(playerId);
}

function defaultGearState(): PlayerGearState {
  return {
    equippedBait: BASIC_BAIT_ID,
    equippedTackle: 'basic',
    baitInventory: {},
    ownedTackles: ['basic'],
    tackleDurability: { basic: DEFAULT_DURABILITY },
    equippedRod: '',
    ownedRods: [],
    rodOversizeLandings: {},
    unlockedBaits: [BASIC_BAIT_ID],
    ownedVessels: [],
    starterRodGranted: false,
  };
}
const upsertGearStmt = db.prepare(`
  INSERT INTO player_gear (
    player_id, equipped_bait, equipped_tackle, bait_inventory, owned_tackles, tackle_durability,
    equipped_rod, owned_rods, rod_oversize, unlocked_baits, owned_vessels, starter_rod_granted, updated_at
  ) VALUES (
    @playerId, @equippedBait, @equippedTackle, @baitInventory, @ownedTackles, @tackleDurability,
    @equippedRod, @ownedRods, @rodOversize, @unlockedBaits, @ownedVessels, @starterRodGranted, @updatedAt
  )
  ON CONFLICT(player_id) DO UPDATE SET
    equipped_bait = @equippedBait,
    equipped_tackle = @equippedTackle,
    bait_inventory = @baitInventory,
    owned_tackles = @ownedTackles,
    tackle_durability = @tackleDurability,
    equipped_rod = @equippedRod,
    owned_rods = @ownedRods,
    rod_oversize = @rodOversize,
    unlocked_baits = @unlockedBaits,
    owned_vessels = @ownedVessels,
    starter_rod_granted = @starterRodGranted,
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

function parseStringArray(raw: string | undefined, fallback: string[] = []): string[] {
  try {
    const parsed = JSON.parse(raw ?? '[]') as unknown;
    if (!Array.isArray(parsed)) return [...fallback];
    return parsed.filter((id): id is string => typeof id === 'string' && id.length > 0);
  } catch {
    return [...fallback];
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

function parseCountMap(raw: string | undefined): Record<string, number> {
  try {
    const parsed = JSON.parse(raw ?? '{}') as Record<string, number>;
    if (!parsed || typeof parsed !== 'object') return {};
    const out: Record<string, number> = {};
    for (const [k, v] of Object.entries(parsed)) {
      if (typeof v === 'number' && v >= 0) out[k] = Math.floor(v);
    }
    return out;
  } catch {
    return {};
  }
}

function applyStarterRod(gear: PlayerGearState): void {
  if (!gear.ownedRods.includes(STARTER_ROD_ID)) gear.ownedRods.push(STARTER_ROD_ID);
  if (!gear.equippedRod || !getRodDef(gear.equippedRod)) gear.equippedRod = STARTER_ROD_ID;
  gear.starterRodGranted = true;
}

function rowToGear(row: GearRow): PlayerGearState {
  return {
    equippedBait: normalizeBaitId(row.equipped_bait) as BaitId,
    equippedTackle: (getTackle(row.equipped_tackle)?.id ?? 'basic') as TackleId,
    baitInventory: parseBaitInventory(row.bait_inventory),
    ownedTackles: parseOwnedTackles(row.owned_tackles),
    tackleDurability: parseTackleDurability(row.tackle_durability),
    equippedRod: row.equipped_rod ?? '',
    ownedRods: parseStringArray(row.owned_rods),
    rodOversizeLandings: parseCountMap(row.rod_oversize),
    unlockedBaits: parseStringArray(row.unlocked_baits, [BASIC_BAIT_ID]),
    ownedVessels: parseStringArray(row.owned_vessels),
    starterRodGranted: row.starter_rod_granted === 1,
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
    equippedRod: normalized.equippedRod,
    ownedRods: JSON.stringify(normalized.ownedRods),
    rodOversize: JSON.stringify(normalized.rodOversizeLandings),
    unlockedBaits: JSON.stringify(normalized.unlockedBaits),
    ownedVessels: JSON.stringify(normalized.ownedVessels),
    starterRodGranted: normalized.starterRodGranted ? 1 : 0,
    updatedAt: Date.now(),
  });
  return normalized;
}

export function normalizeGear(gear: PlayerGearState): PlayerGearState {
  const baitInventory = { ...gear.baitInventory };
  let equippedBait = normalizeBaitId(gear.equippedBait);
  if (!getGameBaitOrLegacy(equippedBait)) equippedBait = BASIC_BAIT_ID;

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

  const ownedRods = [...new Set((gear.ownedRods ?? []).filter((id) => getRodDef(id)))];
  let equippedRod = gear.equippedRod ?? '';
  if (equippedRod && (!getRodDef(equippedRod) || !ownedRods.includes(equippedRod))) {
    equippedRod = ownedRods[0] ?? '';
  }

  const unlockedBaits = [...new Set([
    BASIC_BAIT_ID,
    ...(gear.unlockedBaits ?? []).filter((id) => Boolean(getGameBaitDef(id))),
  ])];
  const ownedVessels = [...new Set((gear.ownedVessels ?? []).filter((id) => getVesselDef(id)))];

  return {
    equippedBait,
    equippedTackle,
    baitInventory,
    ownedTackles,
    tackleDurability,
    equippedRod,
    ownedRods,
    rodOversizeLandings: { ...(gear.rodOversizeLandings ?? {}) },
    unlockedBaits,
    ownedVessels,
    starterRodGranted: Boolean(gear.starterRodGranted),
  };
}

function getPlayerLevel(playerId: string): number {
  try {
    const row = db
      .prepare(`SELECT level FROM player_fishing_progress WHERE player_id = ?`)
      .get(playerId) as { level: number } | undefined;
    return Math.max(1, Number(row?.level) || 1);
  } catch {
    return 1;
  }
}

function getGameBaitOrLegacy(id: string): boolean {
  return Boolean(getGameBaitDef(normalizeBaitId(id)) || getBait(id));
}

export function ensurePlayerGear(playerId: string): PlayerGearState {
  const row = getGearStmt.get(playerId) as GearRow | undefined;
  let gear = row ? normalizeGear(rowToGear(row)) : defaultGearState();
  let dirty = !row;

  // Steam local tutorial does not always set onboarding_completed; bamboo is the
  // default gift rod and must be granted once regardless of that flag.
  if (!gear.starterRodGranted) {
    applyStarterRod(gear);
    dirty = true;
  }

  const unlocked = unlockedBaitsForPlayerLevel(getPlayerLevel(playerId));
  const same =
    unlocked.length === gear.unlockedBaits.length &&
    unlocked.every((id) => gear.unlockedBaits.includes(id));
  if (!same) {
    gear.unlockedBaits = unlocked;
    dirty = true;
  }

  if (!playerExists(playerId)) return normalizeGear(gear);
  if (dirty) return saveGear(playerId, gear);
  return gear;
}

export function grantStarterRod(playerId: string): PlayerGearState {
  const gear = ensurePlayerGear(playerId);
  applyStarterRod(gear);
  return saveGear(playerId, gear);
}

export function canStartFishingWithRod(
  playerId: string,
): { ok: true } | { ok: false; error: string } {
  const gear = ensurePlayerGear(playerId);
  if (!hasUsableRod(gear.ownedRods, gear.equippedRod)) {
    return { ok: false, error: noUsableRodError() };
  }
  return { ok: true };
}

export function addOwnedRod(playerId: string, rodId: string): PlayerGearState {
  const gear = ensurePlayerGear(playerId);
  if (!gear.ownedRods.includes(rodId) && getRodDef(rodId)) {
    gear.ownedRods.push(rodId);
    if (!gear.equippedRod) gear.equippedRod = rodId;
  }
  return saveGear(playerId, gear);
}

export function equipRod(playerId: string, rodId: string): PlayerGearState {
  const gear = ensurePlayerGear(playerId);
  if (gear.ownedRods.includes(rodId) && getRodDef(rodId)) {
    gear.equippedRod = rodId;
  }
  return saveGear(playerId, gear);
}

export function addOwnedVessel(playerId: string, vesselId: string): PlayerGearState {
  const gear = ensurePlayerGear(playerId);
  if (!gear.ownedVessels.includes(vesselId) && getVesselDef(vesselId)) {
    gear.ownedVessels.push(vesselId);
  }
  return saveGear(playerId, gear);
}

export function resolveBaitForBite(
  playerId: string,
  speciesId: string,
): { baitId: string; cost: number } {
  const gear = ensurePlayerGear(playerId);
  const coins = getPlayerCoins(playerId);
  return pickBaitForDiet(gear.unlockedBaits, speciesId, coins);
}

function getPlayerCoins(playerId: string): number {
  try {
    const row = db.prepare(`SELECT coins FROM players WHERE player_id = ?`).get(playerId) as
      | { coins: number }
      | undefined;
    return row?.coins ?? 0;
  } catch {
    return 0;
  }
}

export function chargeBaitUse(
  playerId: string,
  baitId: string,
  cost: number,
): { charged: boolean; coins: number } {
  if (cost <= 0) return { charged: false, coins: getPlayerCoins(playerId) };
  const spend = deductCoins(playerId, cost);
  if (!spend.ok) return { charged: false, coins: getPlayerCoins(playerId) };
  recordFishingMetric('bait_use', {
    playerId,
    payload: { baitId, cost, coinsAfter: spend.coins },
  });
  return { charged: true, coins: spend.coins };
}

export function noteRodOversizeLanding(
  playerId: string,
  sizeM: number,
): { gear: PlayerGearState; broke: boolean; rodId: string } {
  const gear = ensurePlayerGear(playerId);
  const rodId = gear.equippedRod;
  if (!rodId || !isOversizeForRod(rodId, sizeM)) {
    return { gear, broke: false, rodId };
  }
  const next = (gear.rodOversizeLandings[rodId] ?? 0) + 1;
  gear.rodOversizeLandings[rodId] = next;
  let broke = false;
  if (shouldDestroyRod(rodId, next)) {
    gear.ownedRods = gear.ownedRods.filter((id) => id !== rodId);
    delete gear.rodOversizeLandings[rodId];
    gear.equippedRod = gear.ownedRods[0] ?? '';
    broke = true;
    recordFishingMetric('rod_broke', {
      playerId,
      payload: { rodId, sizeM, oversizeLandings: next },
    });
  }
  return { gear: saveGear(playerId, gear), broke, rodId };
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

export function equipBait(playerId: string, baitId: string): PlayerGearState {
  const gear = ensurePlayerGear(playerId);
  gear.equippedBait = baitId as BaitId;
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
    gear.equippedBait = BASIC_BAIT_ID;
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
