import { defaultAvatarPath } from '@fish-social/shared';
import { API_BASE } from './config';

export function resolveAvatarUrl(url?: string): string | undefined {
  if (!url) return undefined;
  if (url.startsWith('data:') || url.startsWith('http://') || url.startsWith('https://')) {
    return url;
  }
  const assetPath = url.startsWith('/') ? url : `/${url}`;
  return `${API_BASE}${assetPath}`;
}

/** 解析服务端静态资源路径（头像、钓鱼纪念照等） */
export function resolveAssetUrl(url?: string): string | undefined {
  return resolveAvatarUrl(url);
}

export function getDefaultAvatarUrl(filename: string): string {
  return resolveAvatarUrl(defaultAvatarPath(filename))!;
}
