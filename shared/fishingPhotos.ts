import { DEFAULT_AVATARS, normalizeCatAssetPath, resolveCatSkinId } from './defaultAvatars';

export const FISHING_PHOTO_DIR = '/image/fishing_photos';

/** 与 profile 默认头像一一对应的钓鱼纪念照 */
export const FISHING_PHOTOS = DEFAULT_AVATARS.map((avatar) => ({
  id: avatar.id,
  filename: `cat_fishing_${avatar.id}.png`,
  avatarFilename: avatar.filename,
  label: avatar.label,
}));

export function fishingPhotoPath(filename: string): string {
  return normalizeCatAssetPath(`${FISHING_PHOTO_DIR}/${filename}`);
}

/** 根据 profile 头像路径解析钓鱼纪念照；无匹配时按 playerId 稳定分配 */
export function resolveFishingPhotoPath(avatarUrl?: string, playerId?: string): string {
  if (avatarUrl?.startsWith(`${FISHING_PHOTO_DIR}/`)) {
    return normalizeCatAssetPath(avatarUrl);
  }

  return fishingPhotoPath(`cat_fishing_${resolveCatSkinId(avatarUrl, playerId)}.png`);
}

export function isFishingPhotoPath(url: string): boolean {
  url = normalizeCatAssetPath(url);
  if (!url.startsWith(`${FISHING_PHOTO_DIR}/`)) return false;
  const filename = url.slice(FISHING_PHOTO_DIR.length + 1);
  return FISHING_PHOTOS.some((p) => p.filename === filename);
}
