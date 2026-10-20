export const DEFAULT_AVATAR_DIR = '/image/profile';
const LEGACY_SKINS = ['calico', 'orange', 'orange', 'orange', 'orange', 'white'] as const;
const RETIRED_SKINS = ['gray', 'siamese', 'tuxedo'];

export const DEFAULT_AVATARS = [
  { id: 'calico', filename: 'cat_avatar_calico.png', label: '三花猫' },
  { id: 'orange', filename: 'cat_avatar_orange.png', label: '橘猫' },
  { id: 'white', filename: 'cat_avatar_white.png', label: '白猫' },
  { id: 'silver', filename: 'cat_avatar_silver.png', label: '银渐层' },
  { id: 'brown', filename: 'cat_avatar_brown.png', label: '棕虎斑' },
  { id: 'cream', filename: 'cat_avatar_cream.png', label: '奶油猫' },
  { id: 'blue', filename: 'cat_avatar_blue.png', label: '英短蓝猫' },
  { id: 'black', filename: 'cat_avatar_black.png', label: '黑猫' },
  { id: 'cow', filename: 'cat_avatar_cow.png', label: '奶牛猫' },
  { id: 'dafeiyu', filename: 'cat_avatar_dafeiyu.png', label: '大肥鱼' },
] as const;

export function defaultAvatarPath(filename: string): string {
  return normalizeCatAssetPath(`${DEFAULT_AVATAR_DIR}/${filename}`);
}

export function isDefaultAvatarPath(url: string): boolean {
  url = normalizeCatAssetPath(url);
  if (!url.startsWith(`${DEFAULT_AVATAR_DIR}/`)) return false;
  const filename = url.slice(DEFAULT_AVATAR_DIR.length + 1);
  return DEFAULT_AVATARS.some((a) => a.filename === filename);
}

export function normalizeCatAssetPath(url: string): string {
  for (const id of RETIRED_SKINS) {
    if (url === `${DEFAULT_AVATAR_DIR}/cat_avatar_${id}.png`)
      return `${DEFAULT_AVATAR_DIR}/cat_avatar_orange.png`;
    if (url === `/image/fishing_photos/cat_fishing_${id}.png`)
      return '/image/fishing_photos/cat_fishing_orange.png';
  }
  return url;
}

export function resolveCatSkinId(avatarUrl?: string, playerId?: string): string {
  const url = normalizeCatAssetPath(avatarUrl ?? '');
  const matched = DEFAULT_AVATARS.find(avatar =>
    url === `${DEFAULT_AVATAR_DIR}/${avatar.filename}` ||
    url === `/image/fishing_photos/cat_fishing_${avatar.id}.png`);
  if (matched) return matched.id;
  let hash = 0;
  for (const character of (playerId ?? '').split(''))
    hash = ((hash << 5) - hash + character.charCodeAt(0)) | 0;
  return LEGACY_SKINS[Math.abs(hash) % LEGACY_SKINS.length];
}
