import { DEFAULT_AVATARS, defaultAvatarPath } from './defaultAvatars';

export const FISHING_PHOTO_DIR = '/image/fishing_photos';

/** 与 profile 默认头像一一对应的钓鱼纪念照 */
export const FISHING_PHOTOS = DEFAULT_AVATARS.map((avatar) => ({
  id: avatar.id,
  filename: `cat_fishing_${avatar.id}.png`,
  avatarFilename: avatar.filename,
  label: avatar.label,
}));

const AVATAR_TO_FISHING = new Map<string, string>(
  FISHING_PHOTOS.map((p) => [p.avatarFilename, `${FISHING_PHOTO_DIR}/${p.filename}`]),
);

export function fishingPhotoPath(filename: string): string {
  return `${FISHING_PHOTO_DIR}/${filename}`;
}

/** 根据 profile 头像路径解析钓鱼纪念照；无匹配时按 playerId 稳定分配 */
export function resolveFishingPhotoPath(avatarUrl?: string, playerId?: string): string {
  if (avatarUrl?.startsWith(`${FISHING_PHOTO_DIR}/`)) {
    return avatarUrl;
  }

  if (avatarUrl?.startsWith('/image/profile/')) {
    const avatarFilename = avatarUrl.slice('/image/profile/'.length);
    const matched = AVATAR_TO_FISHING.get(avatarFilename);
    if (matched) return matched;
  }

  const fallbackIndex = playerId
    ? Math.abs(hashString(playerId)) % FISHING_PHOTOS.length
    : 0;
  return fishingPhotoPath(FISHING_PHOTOS[fallbackIndex].filename);
}

export function isFishingPhotoPath(url: string): boolean {
  if (!url.startsWith(`${FISHING_PHOTO_DIR}/`)) return false;
  const filename = url.slice(FISHING_PHOTO_DIR.length + 1);
  return FISHING_PHOTOS.some((p) => p.filename === filename);
}

function hashString(value: string): number {
  let hash = 0;
  for (let i = 0; i < value.length; i++) {
    hash = (hash << 5) - hash + value.charCodeAt(i);
    hash |= 0;
  }
  return hash;
}
