/** FEAT-ALBUM-01：相册卡来源 */
export type AlbumCardSource = 'catch' | 'return' | 'first_codex';

export interface AlbumCard {
  id: string;
  speciesId: string;
  quality: string;
  sizeM: number;
  pondId: string | null;
  pondName: string | null;
  source: AlbumCardSource;
  eventAt: number;
  inventoryItemId?: string | null;
  /** 与社交动态一致的钓鱼纪念照路径（如 /image/fishing_photos/...） */
  photoUrl?: string | null;
}

import type { AchievementDef } from './gameDataTypes';

export type { AchievementDef };

export interface PlayerAchievementUnlock {
  achievementId: string;
  unlockedAt: number;
}

export interface AchievementView extends AchievementDef {
  unlocked: boolean;
  unlockedAt: number | null;
}

export interface ProfileHubResponse {
  isSelf: boolean;
  canEdit: boolean;
  profile: {
    playerId: string;
    nickname: string;
    avatarUrl?: string;
    bio?: string;
    shareVisibility?: string;
    coins?: number;
    showcaseFishIds: (string | null)[];
  };
  progress?: {
    level: number;
    xp: number;
  } | null;
  showcaseFish: unknown[];
  codexSummary: {
    unlockedCount: number;
    totalSpecies: number;
  };
  albumPins: AlbumCard[];
  albumCandidates: AlbumCard[];
  achievements: AchievementView[];
  albumPinCap: number;
}
