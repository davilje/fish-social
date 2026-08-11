export const DEFAULT_AVATAR_DIR = '/image/profile';

export const DEFAULT_AVATARS = [
  { id: 'calico', filename: 'cat_avatar_calico.png', label: '三花猫' },
  { id: 'gray', filename: 'cat_avatar_gray.png', label: '灰猫' },
  { id: 'orange', filename: 'cat_avatar_orange.png', label: '橘猫' },
  { id: 'siamese', filename: 'cat_avatar_siamese.png', label: '暹罗猫' },
  { id: 'tuxedo', filename: 'cat_avatar_tuxedo.png', label: '燕尾猫' },
  { id: 'white', filename: 'cat_avatar_white.png', label: '白猫' },
] as const;

export function defaultAvatarPath(filename: string): string {
  return `${DEFAULT_AVATAR_DIR}/${filename}`;
}

export function isDefaultAvatarPath(url: string): boolean {
  if (!url.startsWith(`${DEFAULT_AVATAR_DIR}/`)) return false;
  const filename = url.slice(DEFAULT_AVATAR_DIR.length + 1);
  return DEFAULT_AVATARS.some((a) => a.filename === filename);
}
