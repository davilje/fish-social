import type { Server } from 'socket.io';
import {
  BOT_MAX_STAY_MS,
  BOT_MIN_STAY_MS,
  MAX_POND_USERS,
  PONDS,
  getQualityInfo,
  getSpecies,
  isAnnounceQuality,
  isFishingActive,
  type ClientToServerEvents,
  type PondUser,
  type ServerToClientEvents,
  BAITS,
  TACKLES,
  type BaitId,
  type TackleId,
} from '@fish-social/shared';
import {
  addBotUser,
  getBotMeta,
  getBotPlayerId,
  listBotsInPond,
  listHumansInPond,
  listUsersInPond,
  postAnnouncement,
  removeBotUser,
  resolveBotFishingSpot,
  emitPondUserUpdated,
  startBotFishing,
  stopBotFishing,
} from './gameState.js';
import { areFriends, sendFriendRequest } from './friends.js';
import { addFishToInventory, isPondFishLocked } from './inventory.js';
import { ensurePlayer, getPlayer } from './players.js';
import { createPostFromFish } from './posts.js';
import { removePondFish } from './pondEcology.js';
import { ensurePlayerGear, equipBait, equipTackle, addBaitToInventory } from './gear.js';
import { getConfigNumber } from './gameConfig.js';
import { db } from './db.js';
import { initBotFishingPhase, processWaitingBiteTick } from './fishingStateMachine.js';
import { setBotHookCatchHandler } from './botHookCatch.js';
import {
  getBiteCheckMs,
  getRuntimeNumber,
  refreshRuntimeFromDb,
  scheduleRuntimeInterval,
} from './runtimeConfig.js';
import { resolveSocketByPlayer } from './sessionRegistry.js';

/** FISH-BOT-1：一人一名，均 ≤12 字（ensurePlayer 截断） */
export const BOT_NAMES = [
  '湖边小王', '静默钓手', '晨雾渔人', '夕阳老张', '竹林隐者',
  '微风钓客', '江湖渔翁', '浅水阿杰', '远投高手', '夜钓达人',
  '悠闲钓妹', '湖心居士', '云雾行者', '甩竿少年', '静待鱼儿',
  '碧波钓者', '芦苇边上', '细雨抛竿', '月下水影', '青石钓台',
  '南岸闲人', '北堤渔歌', '东亭小满', '西湾听涛', '柳梢钓客',
  '荷塘夜语', '溪口老李', '沙洲短笛', '烟波客', '渔火一点',
  '晚风收竿', '朝露入水', '浪花少年', '青笠钓叟', '蒲扇渔夫',
  '棠梨渡', '芙蓉汀', '白鹭矶', '鲤跃门', '一竿秋水',
  '半日闲钓', '满船清梦', '斜阳独钓', '疏星河岸', '薄雾船头',
  '小桥边钓', '古渡渔歌', '芦花浅水', '苔痕石矶', '青竹为竿',
  '丝线随风', '浮漂轻点', '提竿见月', '落水有声', '静坐听波',
  '淡水清欢', '河边小鱼', '塘口阿青', '湾里阿宁', '矶石上人',
  '晓风钓友', '暮色行舟', '雪后独钓', '夏夜追鲤', '春水初生',
  '秋湖望远', '冬堤温茶', '云起时', '雨歇后', '风定浪平',
  '回湾小憩', '浅滩寻踪', '深潭守候', '急流试竿', '缓水漫钓',
  '星湖钓影', '月湾渔火', '枫林临水', '桐荫钓台', '梅坞小驻',
  '菊径临塘', '兰汀抛丝', '竹篱外钓', '茅亭一角', '石埠钓踪',
  '渡口闲聊', '渔村阿成', '船家小妹', '堤上阿福', '水边阿珍',
  '青坪钓客', '碧潭客', '银浪手', '金鳞客', '玉珠浮漂',
  '翠羽湖', '琥珀湾', '琉璃汀', '翡翠矶', '玄青水',
  '淡墨河', '素简钓', '清音渡', '怀远客', '归雁汀',
];

export const BOT_POOL_SIZE = 100;
const BOT_POOL_ID_PREFIX = 'bot-pool-';

/** 默认=塘容量，允许 bot 填满鱼塘 */
const BOT_MAX_PER_POND_DEFAULT = MAX_POND_USERS;
const BOT_SPAWN_CHECK_MS_DEFAULT = 45_000;
const BOT_START_FISHING_CHANCE = 0.28;
const BOT_STOP_FISHING_CHANCE = 0.03;
const BOT_FRIEND_REQUEST_CHANCE = 0.15;
const BOT_FRIEND_COOLDOWN_MS = 4 * 60 * 1000;

function uniformInt(min: number, max: number): number {
  const lo = Math.min(min, max);
  const hi = Math.max(min, max);
  return lo + Math.floor(Math.random() * (hi - lo + 1));
}

function maxBotsPerPond(): number {
  return Math.max(0, Math.floor(getRuntimeNumber('MAX_BOTS_PER_POND', BOT_MAX_PER_POND_DEFAULT)));
}

/** 开钓并广播；elapsedMs 仅回拨内存锚点 */
function beginBotFishingSession(
  io: Server<ClientToServerEvents, ServerToClientEvents>,
  pondId: string,
  bot: PondUser,
  opts?: { elapsedMs?: number },
): boolean {
  const spotId = resolveBotFishingSpot(pondId, bot.id);
  if (!spotId) return false;
  const result = startBotFishing(pondId, bot.id, spotId, opts);
  if (!result.ok) return false;
  initBotFishingPhase(result.user);
  const playerId = getBotPlayerId(bot.id);
  const gear = playerId ? ensurePlayerGear(playerId) : null;
  if (gear) {
    result.user.equippedBaitId = gear.equippedBait;
    result.user.equippedTackleId = gear.equippedTackle;
  }
  emitPondUserUpdated(io, pondId, result.user);
  return true;
}

const botFriendCooldown = new Map<string, number>();

export function botPoolPlayerId(index0: number): string {
  return `${BOT_POOL_ID_PREFIX}${String(index0 + 1).padStart(3, '0')}`;
}

export function isBotPoolPlayerId(playerId: string): boolean {
  return playerId.startsWith(BOT_POOL_ID_PREFIX);
}

function randomStayMs(): number {
  return BOT_MIN_STAY_MS + Math.random() * (BOT_MAX_STAY_MS - BOT_MIN_STAY_MS);
}

function emitBotLeft(io: Server<ClientToServerEvents, ServerToClientEvents>, pondId: string, userId: string) {
  io.to(pondId).emit('pond_user_left', userId);
}

function removeBotAndNotify(
  io: Server<ClientToServerEvents, ServerToClientEvents>,
  pondId: string,
  userId: string,
): PondUser | null {
  const removed = removeBotUser(pondId, userId);
  if (removed) emitBotLeft(io, pondId, userId);
  return removed;
}

function randomizeBotGear(playerId: string): void {
  let gear = ensurePlayerGear(playerId);
  const baitChoices: BaitId[] = ['basic', 'basic', 'corn', 'corn', 'pellet'];
  const baitId = baitChoices[Math.floor(Math.random() * baitChoices.length)];
  if (baitId !== 'basic') {
    const bait = BAITS.find((b) => b.id === baitId);
    if (bait && bait.consumed) {
      gear = addBaitToInventory(playerId, baitId, 3 + Math.floor(Math.random() * 8));
    }
  }
  gear = equipBait(playerId, baitId);
  if (Math.random() < 0.25) {
    const tackle = TACKLES.filter((t) => t.id !== 'basic');
    const pick = tackle[Math.floor(Math.random() * tackle.length)];
    if (pick) gear = equipTackle(playerId, pick.id as TackleId);
  }
}

/** FISH-BOT-1：旧默认 6 → 塘容量 20（仅抬升库存默认，不覆盖人工改过的值） */
function ensureMaxBotsPerPondDefault(): void {
  const row = db
    .prepare(`SELECT config_value FROM game_config WHERE config_key = 'MAX_BOTS_PER_POND'`)
    .get() as { config_value: string } | undefined;
  if (row?.config_value === '6') {
    db.prepare(
      `UPDATE game_config SET config_value = ?, updated_at = ? WHERE config_key = 'MAX_BOTS_PER_POND'`,
    ).run(String(BOT_MAX_PER_POND_DEFAULT), Date.now());
    refreshRuntimeFromDb();
  }
}

/** 确保库内恰有固定 100 池账号（不足补齐；不在热路径建 UUID） */
export function ensureBotPool(size: number = BOT_POOL_SIZE): string[] {
  ensureMaxBotsPerPondDefault();
  const ids: string[] = [];
  for (let i = 0; i < size; i++) {
    const playerId = botPoolPlayerId(i);
    const nickname = BOT_NAMES[i] ?? `钓友${i + 1}`;
    ensurePlayer(playerId, nickname);
    ids.push(playerId);
  }
  return ids;
}

function listBusyBotPlayerIds(): Set<string> {
  const busy = new Set<string>();
  for (const pond of PONDS) {
    for (const bot of listBotsInPond(pond.id)) {
      if (bot.playerId) busy.add(bot.playerId);
    }
  }
  return busy;
}

export function listIdleBotPoolPlayerIds(size: number = BOT_POOL_SIZE): string[] {
  const busy = listBusyBotPlayerIds();
  const idle: string[] = [];
  for (let i = 0; i < size; i++) {
    const id = botPoolPlayerId(i);
    if (!busy.has(id)) idle.push(id);
  }
  return idle;
}

/** 从池抽空闲 bot 进塘；池空或塘已满则 null。禁止 randomUUID 新建账号。 */
export function enterPondFromPool(pondId: string): PondUser | null {
  const maxBotsPerPond = Math.max(
    0,
    Math.floor(getRuntimeNumber('MAX_BOTS_PER_POND', BOT_MAX_PER_POND_DEFAULT)),
  );
  const users = listUsersInPond(pondId);
  if (users.length >= MAX_POND_USERS) return null;
  if (listBotsInPond(pondId).length >= maxBotsPerPond) return null;

  const idle = listIdleBotPoolPlayerIds();
  if (idle.length === 0) return null;

  const playerId = idle[Math.floor(Math.random() * idle.length)]!;
  const profile = getPlayer(playerId);
  const nickname = profile?.nickname ?? BOT_NAMES[0]!;
  randomizeBotGear(playerId);
  const leaveAt = Date.now() + randomStayMs();
  return addBotUser(pondId, playerId, nickname, leaveAt);
}

function handleBotCatch(
  io: Server<ClientToServerEvents, ServerToClientEvents>,
  pondId: string,
  bot: PondUser,
  fish: { id: string; speciesId: string; quality: string; sizeM: number },
) {
  if (isPondFishLocked(fish.id)) return;

  const playerId = getBotPlayerId(bot.id) ?? bot.playerId;
  if (!playerId) return;

  // inventory.player_id → players：池账号若被删库/未 seed，直接 INSERT 会 FK 炸进程（STAB-03）
  ensurePlayer(playerId, bot.nickname);

  const removed = removePondFish(fish.id);
  if (!removed) return;

  let item;
  try {
    item = addFishToInventory(
      playerId,
      {
        speciesId: removed.speciesId,
        quality: removed.quality,
        sizeM: removed.sizeM,
        caughtAt: Date.now(),
        pondId,
      },
      { pondId },
    );
  } catch (err) {
    console.error('[bots] addFishToInventory failed', {
      playerId,
      pondId,
      fishId: fish.id,
      err: err instanceof Error ? err.message : err,
    });
    return;
  }

  try {
    // FEAT-UI-2: bot 仅史诗+发动态，去掉普通渔获随机分享
    if (isAnnounceQuality(item.quality)) {
      const species = getSpecies(item.speciesId);
      const qualityInfo = getQualityInfo(item.quality);
      const text = `${bot.nickname}钓到了【${qualityInfo.name}】的${species.name}！`;
      const msg = postAnnouncement(pondId, text);
      io.to(pondId).emit('chat_message', msg);
      createPostFromFish(playerId, bot.nickname, item, 'public', {
        authorAvatarUrl: bot.avatarUrl,
      });
    }
  } catch (err) {
    console.error('[bots] post-catch side effects failed', {
      playerId,
      pondId,
      err: err instanceof Error ? err.message : err,
    });
  }
}

function tickBotFriendRequests(io: Server<ClientToServerEvents, ServerToClientEvents>) {
  for (const pond of PONDS) {
    const humans = listHumansInPond(pond.id);
    if (humans.length === 0) continue;

    for (const bot of listBotsInPond(pond.id)) {
      const botPlayerId = getBotPlayerId(bot.id);
      if (!botPlayerId) continue;

      const last = botFriendCooldown.get(botPlayerId) ?? 0;
      if (Date.now() - last < BOT_FRIEND_COOLDOWN_MS) continue;
      if (Math.random() > BOT_FRIEND_REQUEST_CHANCE) continue;

      const candidates = humans.filter(
        (h) => h.playerId && !areFriends(botPlayerId, h.playerId),
      );
      if (candidates.length === 0) continue;

      const target = candidates[Math.floor(Math.random() * candidates.length)];
      const result = sendFriendRequest(botPlayerId, bot.nickname, target.playerId!);
      botFriendCooldown.set(botPlayerId, Date.now());

      if (result.ok) {
        const socket = resolveSocketByPlayer(target.playerId!);
        if (socket) io.to(socket).emit('friend_request', result.request);
      }
    }
  }
}

/**
 * FISH-BOT-2 Boot：每塘 3～6（可配），禁止 while 补满。
 * 热重载时若塘内已有 bot 则跳过，避免叠人。
 */
export function bootstrapBots(io: Server<ClientToServerEvents, ServerToClientEvents>): void {
  refreshRuntimeFromDb();
  const bootMin = Math.max(0, Math.floor(getRuntimeNumber('BOT_BOOT_MIN', 3)));
  const bootMax = Math.max(bootMin, Math.floor(getRuntimeNumber('BOT_BOOT_MAX', 6)));
  const fishingRatio = getRuntimeNumber('BOT_BOOT_FISHING_RATIO', 0.75);
  const elapsedMin = Math.max(0, Math.floor(getRuntimeNumber('BOT_BOOT_ELAPSED_MIN_MS', 5 * 60 * 1000)));
  const elapsedMax = Math.max(
    elapsedMin,
    Math.floor(getRuntimeNumber('BOT_BOOT_ELAPSED_MAX_MS', 75 * 60 * 1000)),
  );
  const hardCap = maxBotsPerPond();

  for (const pond of PONDS) {
    if (listBotsInPond(pond.id).length > 0) continue;

    const freeSlots = Math.max(0, MAX_POND_USERS - listUsersInPond(pond.id).length);
    const idlePool = listIdleBotPoolPlayerIds().length;
    let n = uniformInt(bootMin, bootMax);
    n = Math.min(n, hardCap, freeSlots, idlePool);
    if (n <= 0) continue;

    for (let i = 0; i < n; i++) {
      const bot = enterPondFromPool(pond.id);
      if (!bot) break;
      io.to(pond.id).emit('pond_user_joined', bot);

      if (Math.random() < fishingRatio) {
        const elapsedMs = uniformInt(elapsedMin, elapsedMax);
        beginBotFishingSession(io, pond.id, bot, { elapsedMs });
      }
    }
  }
}

/**
 * FISH-BOT-2 Steady：每塘每周期最多 +1，可缓慢补到 MAX_BOTS_PER_POND（满塘）。
 * 禁止 while 一次补满。
 */
export function tickSpawn(io: Server<ClientToServerEvents, ServerToClientEvents>): void {
  const hardCap = maxBotsPerPond();
  const spawnChance = getRuntimeNumber('BOT_SPAWN_CHANCE', 0.35);
  const joinFishChance = getRuntimeNumber('BOT_JOIN_FISHING_CHANCE', 0.4);
  const joinElapsedMax = Math.max(
    0,
    Math.floor(getRuntimeNumber('BOT_JOIN_ELAPSED_MAX_MS', 10 * 60 * 1000)),
  );

  for (const pond of PONDS) {
    const bots = listBotsInPond(pond.id).length;
    const humans = listHumansInPond(pond.id).length;
    if (bots + humans >= MAX_POND_USERS) continue;
    if (bots >= hardCap) continue;
    if (Math.random() >= spawnChance) continue;

    const bot = enterPondFromPool(pond.id);
    if (!bot) continue;
    io.to(pond.id).emit('pond_user_joined', bot);

    if (Math.random() < joinFishChance) {
      const elapsedMs = joinElapsedMax > 0 ? uniformInt(0, joinElapsedMax) : 0;
      beginBotFishingSession(io, pond.id, bot, { elapsedMs });
    }
  }
}

function tickBots(io: Server<ClientToServerEvents, ServerToClientEvents>) {
  for (const pond of PONDS) {
    for (const bot of listBotsInPond(pond.id)) {
      const meta = getBotMeta(bot.id);
      if (!meta) continue;

      if (Date.now() >= meta.leaveAt) {
        removeBotAndNotify(io, pond.id, bot.id);
        continue;
      }

      // Heal stuck bots: waiting/垂钓中 but no timer anchor (pre-BUG-13 stop path)
      if (
        isFishingActive(bot.fishingPhase) &&
        (bot.fishingStartedAt == null || bot.status !== 'fishing')
      ) {
        bot.status = 'fishing';
        if (bot.fishingStartedAt == null) bot.fishingStartedAt = Date.now();
        emitPondUserUpdated(io, pond.id, bot);
      }

      if (bot.status === 'idle') {
        if (Math.random() < BOT_START_FISHING_CHANCE) {
          beginBotFishingSession(io, pond.id, bot);
        }
        continue;
      }

      if (bot.fishingPhase === 'waiting' && bot.spotId) {
        if (Math.random() < BOT_STOP_FISHING_CHANCE) {
          const result = stopBotFishing(pond.id, bot.id);
          if (result.ok) {
            emitPondUserUpdated(io, pond.id, result.user);
          }
          continue;
        }

        const cap = getConfigNumber('BOT_CATCH_SHARE_CAP', 0.4);
        const humans = listHumansInPond(pond.id).length;
        const botsFishing = listBotsInPond(pond.id).filter((b) => b.fishingPhase === 'waiting').length;
        const botShare = humans + botsFishing > 0 ? botsFishing / (humans + botsFishing) : 0;
        if (botShare > cap && Math.random() > 0.5) continue;

        const playerId = getBotPlayerId(bot.id);
        if (!playerId) continue;
        const hooked = processWaitingBiteTick(io, pond.id, bot.id, playerId, bot.spotId, null);
        void hooked;
      }
    }
  }
}

export function startBotLoop(io: Server<ClientToServerEvents, ServerToClientEvents>): () => void {
  setBotHookCatchHandler((ioSrv, pondId, bot, fish) => {
    handleBotCatch(ioSrv, pondId, bot, fish);
  });
  // 启动/热重载都重新 seed 100 池账号，避免 players 被清后 bot 仍在内存导致 FK
  const pool = ensureBotPool(BOT_POOL_SIZE);
  refreshRuntimeFromDb();
  const spawnCheckMs = Math.max(
    5_000,
    Math.floor(getRuntimeNumber('BOT_SPAWN_CHECK_MS', BOT_SPAWN_CHECK_MS_DEFAULT)),
  );
  console.log(
    `Bot loop started (pool=${pool.length}, maxBots/pond=${maxBotsPerPond()}, spawnCheckMs=${spawnCheckMs}, FISH-BOT-2 boot 3~6)`,
  );
  // FISH-BOT-2：稀疏 Boot，禁止启动 while 补满
  bootstrapBots(io);
  const spawnTimer = setInterval(() => tickSpawn(io), spawnCheckMs);
  const stopBiteLoop = scheduleRuntimeInterval(() => tickBots(io), getBiteCheckMs);
  const friendTimer = setInterval(() => tickBotFriendRequests(io), 60_000);
  return () => {
    clearInterval(spawnTimer);
    clearInterval(friendTimer);
    stopBiteLoop();
  };
}

export function emitEvictedBots(
  io: Server<ClientToServerEvents, ServerToClientEvents>,
  pondId: string,
  evictedUserIds: string[],
): void {
  for (const userId of evictedUserIds) {
    emitBotLeft(io, pondId, userId);
  }
}
