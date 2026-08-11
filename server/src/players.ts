import {
  SHOWCASE_SLOT_COUNT,
  isDefaultAvatarPath,
  type FishInventoryItem,
  type PlayerProfile,
  type PublicPlayerView,
  type ShareVisibility,
} from '@fish-social/shared';
import { db } from './db.js';
import { getFishById } from './inventory.js';
import { ensurePlayerGear } from './gear.js';
import { getPlayerPosts } from './posts.js';

const MAX_AVATAR_LEN = 280_000;
const MAX_BIO_LEN = 120;

interface PlayerRow {
  player_id: string;
  nickname: string;
  coins: number;
  share_visibility: string;
  avatar_url: string | null;
  bio: string;
  showcase_fish_ids: string;
  created_at: number;
}

function emptyShowcase(): (string | null)[] {
  return Array.from({ length: SHOWCASE_SLOT_COUNT }, () => null);
}

function normalizeShowcase(slots?: (string | null)[]): (string | null)[] {
  const base = emptyShowcase();
  if (!slots) return base;
  for (let i = 0; i < SHOWCASE_SLOT_COUNT; i++) {
    base[i] = slots[i] ?? null;
  }
  return base;
}

function rowToProfile(row: PlayerRow): PlayerProfile {
  let showcase: (string | null)[] = emptyShowcase();
  try {
    showcase = normalizeShowcase(JSON.parse(row.showcase_fish_ids));
  } catch {
    /* ignore */
  }
  return {
    playerId: row.player_id,
    nickname: row.nickname,
    coins: row.coins,
    shareVisibility: row.share_visibility as ShareVisibility,
    createdAt: row.created_at,
    bio: row.bio,
    showcaseFishIds: showcase,
    ...(row.avatar_url ? { avatarUrl: row.avatar_url } : {}),
  };
}

const getPlayerStmt = db.prepare('SELECT * FROM players WHERE player_id = ?');
const insertPlayerStmt = db.prepare(`
  INSERT INTO players (player_id, nickname, coins, share_visibility, avatar_url, bio, showcase_fish_ids, created_at)
  VALUES (@playerId, @nickname, @coins, @shareVisibility, @avatarUrl, @bio, @showcaseFishIds, @createdAt)
`);
const updatePlayerStmt = db.prepare(`
  UPDATE players SET
    nickname = @nickname,
    coins = @coins,
    share_visibility = @shareVisibility,
    avatar_url = @avatarUrl,
    bio = @bio,
    showcase_fish_ids = @showcaseFishIds
  WHERE player_id = @playerId
`);

function saveProfile(p: PlayerProfile): void {
  const payload = {
    playerId: p.playerId,
    nickname: p.nickname,
    coins: p.coins,
    shareVisibility: p.shareVisibility,
    avatarUrl: p.avatarUrl ?? null,
    bio: p.bio ?? '',
    showcaseFishIds: JSON.stringify(p.showcaseFishIds ?? emptyShowcase()),
    createdAt: p.createdAt,
  };
  const existing = getPlayerStmt.get(p.playerId);
  if (existing) updatePlayerStmt.run(payload);
  else insertPlayerStmt.run(payload);
}

export function ensurePlayer(playerId: string, nickname: string): PlayerProfile {
  const row = getPlayerStmt.get(playerId) as PlayerRow | undefined;
  if (row) {
    const profile = rowToProfile(row);
    if (nickname && nickname !== profile.nickname) {
      profile.nickname = nickname.slice(0, 12);
      saveProfile(profile);
    }
    ensurePlayerGear(playerId);
    return profile;
  }
  const profile: PlayerProfile = {
    playerId,
    nickname: nickname.slice(0, 12) || '钓友',
    coins: 0,
    shareVisibility: 'public',
    createdAt: Date.now(),
    bio: '',
    showcaseFishIds: emptyShowcase(),
  };
  saveProfile(profile);
  ensurePlayerGear(playerId);
  return profile;
}

export function getPlayer(playerId: string): PlayerProfile | undefined {
  const row = getPlayerStmt.get(playerId) as PlayerRow | undefined;
  return row ? rowToProfile(row) : undefined;
}

export function addCoins(playerId: string, amount: number): number {
  const p = getPlayer(playerId);
  if (!p) return 0;
  p.coins += amount;
  saveProfile(p);
  return p.coins;
}

export function deductCoins(
  playerId: string,
  amount: number,
): { ok: true; coins: number } | { ok: false; code: 'INSUFFICIENT_GOLD' } {
  if (amount <= 0) {
    const p = getPlayer(playerId);
    return p ? { ok: true, coins: p.coins } : { ok: false, code: 'INSUFFICIENT_GOLD' };
  }
  const p = getPlayer(playerId);
  if (!p || p.coins < amount) {
    return { ok: false, code: 'INSUFFICIENT_GOLD' };
  }
  p.coins -= amount;
  saveProfile(p);
  return { ok: true, coins: p.coins };
}

export function setShareVisibility(
  playerId: string,
  visibility: ShareVisibility,
): PlayerProfile | null {
  const p = getPlayer(playerId);
  if (!p) return null;
  p.shareVisibility = visibility;
  saveProfile(p);
  return p;
}

export function updatePlayerProfile(
  playerId: string,
  patch: { nickname?: string; bio?: string; avatarUrl?: string | null },
): { ok: true; profile: PlayerProfile } | { ok: false; error: string } {
  const p = getPlayer(playerId);
  if (!p) return { ok: false, error: '玩家不存在' };

  if (patch.nickname !== undefined) {
    const nick = patch.nickname.trim().slice(0, 12);
    if (!nick) return { ok: false, error: '昵称不能为空' };
    p.nickname = nick;
  }

  if (patch.bio !== undefined) {
    p.bio = patch.bio.trim().slice(0, MAX_BIO_LEN);
  }

  if (patch.avatarUrl !== undefined) {
    if (patch.avatarUrl === null || patch.avatarUrl === '') {
      delete p.avatarUrl;
    } else if (isDefaultAvatarPath(patch.avatarUrl)) {
      p.avatarUrl = patch.avatarUrl;
    } else if (patch.avatarUrl.startsWith('data:image/') && patch.avatarUrl.length <= MAX_AVATAR_LEN) {
      p.avatarUrl = patch.avatarUrl;
    } else {
      return { ok: false, error: '无效的头像，请选择默认头像或上传图片' };
    }
  }

  saveProfile(p);
  return { ok: true, profile: p };
}

export function setShowcaseFish(
  playerId: string,
  slots: (string | null)[],
): { ok: true; profile: PlayerProfile } | { ok: false; error: string } {
  const p = getPlayer(playerId);
  if (!p) return { ok: false, error: '玩家不存在' };
  p.showcaseFishIds = normalizeShowcase(slots);
  saveProfile(p);
  return { ok: true, profile: p };
}

export function getPublicPlayerView(
  playerId: string,
  viewerPlayerId: string,
  limit = 20,
): PublicPlayerView | null {
  const p = getPlayer(playerId);
  if (!p) return null;

  const showcaseFish: (FishInventoryItem | null)[] = p.showcaseFishIds.map((fishId) =>
    fishId ? getFishById(playerId, fishId) : null,
  );

  return {
    profile: {
      playerId: p.playerId,
      nickname: p.nickname,
      avatarUrl: p.avatarUrl,
      bio: p.bio ?? '',
      showcaseFishIds: p.showcaseFishIds,
    },
    showcaseFish,
    posts: getPlayerPosts(playerId, viewerPlayerId, limit),
  };
}

export function searchPlayers(query: string, excludePlayerId?: string): PlayerProfile[] {
  const q = query.trim().toLowerCase();
  if (!q) return [];
  const rows = db.prepare('SELECT * FROM players').all() as PlayerRow[];
  return rows
    .map(rowToProfile)
    .filter((p) => p.playerId !== excludePlayerId)
    .filter(
      (p) =>
        p.playerId.toLowerCase().includes(q) ||
        p.nickname.toLowerCase().includes(q),
    )
    .slice(0, 20);
}
