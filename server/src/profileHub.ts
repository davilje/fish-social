import type { FishInventoryItem, ProfileHubResponse } from '@fish-social/shared';
import { FISH_SPECIES, getAlbumPinCap, resolveFishingPhotoPath } from '@fish-social/shared';
import { getPlayer, getPublicPlayerView } from './players.js';
import { getFishById } from './inventory.js';
import { getPlayerCodex } from './codex.js';
import { listAlbumPins, seedAlbumPinsFromCandidatesIfEmpty } from './album.js';
import { buildAchievementViews } from './achievements.js';
import { areFriends } from './friends.js';
import { ensurePlayerProgress } from './playerProgress.js';

function canViewPrivateContent(
  ownerId: string,
  viewerId: string,
  shareVisibility: string,
): boolean {
  if (ownerId === viewerId) return true;
  if (shareVisibility === 'private') return false;
  if (shareVisibility === 'friends') return areFriends(ownerId, viewerId);
  return true; // public
}

export function getProfileHub(
  targetPlayerId: string,
  viewerPlayerId: string,
): ProfileHubResponse | null {
  const player = getPlayer(targetPlayerId);
  if (!player) return null;

  const isSelf = targetPlayerId === viewerPlayerId;
  const canSee = canViewPrivateContent(
    targetPlayerId,
    viewerPlayerId,
    player.shareVisibility,
  );

  if (!isSelf && !canSee) {
    // Minimal locked view
    return {
      isSelf: false,
      canEdit: false,
      profile: {
        playerId: player.playerId,
        nickname: player.nickname,
        avatarUrl: player.avatarUrl,
        bio: '',
        showcaseFishIds: [],
      },
      progress: null,
      showcaseFish: [],
      codexSummary: { unlockedCount: 0, totalSpecies: FISH_SPECIES.length },
      albumPins: [],
      albumCandidates: [],
      achievements: [],
      albumPinCap: getAlbumPinCap(),
    };
  }

  const showcaseFish: (FishInventoryItem | null)[] = player.showcaseFishIds.map((fishId) =>
    fishId ? getFishById(targetPlayerId, fishId) : null,
  );

  const codex = getPlayerCodex(targetPlayerId);
  const unlockedCount = codex.filter((e) => e.totalCaught > 0).length;
  const progress = isSelf ? ensurePlayerProgress(targetPlayerId) : ensurePlayerProgress(targetPlayerId);

  seedAlbumPinsFromCandidatesIfEmpty(targetPlayerId);

  const photoUrl = resolveFishingPhotoPath(player.avatarUrl, targetPlayerId);
  const albumPins = listAlbumPins(targetPlayerId).map((card) => ({
    ...card,
    photoUrl,
  }));

  return {
    isSelf,
    canEdit: isSelf,
    profile: {
      playerId: player.playerId,
      nickname: player.nickname,
      avatarUrl: player.avatarUrl,
      bio: player.bio ?? '',
      shareVisibility: isSelf ? player.shareVisibility : undefined,
      coins: isSelf ? player.coins : undefined,
      showcaseFishIds: player.showcaseFishIds,
    },
    progress: {
      level: progress.level,
      xp: progress.xp,
    },
    showcaseFish,
    codexSummary: {
      unlockedCount,
      totalSpecies: FISH_SPECIES.length,
    },
    albumPins,
    albumCandidates: [],
    achievements: buildAchievementViews(targetPlayerId, { forPublic: !isSelf }),
    albumPinCap: getAlbumPinCap(),
  };
}

/** Keep public-view posts path available; hub is separate aggregate. */
export function getProfileHubOrPublicFallback(
  targetPlayerId: string,
  viewerPlayerId: string,
): ProfileHubResponse | null {
  const hub = getProfileHub(targetPlayerId, viewerPlayerId);
  if (hub) return hub;
  const view = getPublicPlayerView(targetPlayerId, viewerPlayerId, 1);
  if (!view) return null;
  return null;
}
