import { Platform } from 'react-native';
import { getAuthSession } from './auth';

/** Web 用 localhost；Android 模拟器用 10.0.2.2；真机改为电脑局域网 IP */
const DEV_HOST =
  Platform.OS === 'web'
    ? 'localhost'
    : Platform.OS === 'android'
      ? '10.0.2.2'
      : 'localhost';

export const API_BASE = `http://${DEV_HOST}:3001`;
export const SOCKET_URL = API_BASE;

export function formatDuration(ms: number): string {
  const totalSec = Math.floor(ms / 1000);
  const h = Math.floor(totalSec / 3600);
  const m = Math.floor((totalSec % 3600) / 60);
  const s = totalSec % 60;
  if (h > 0) return `${h}时${m}分`;
  if (m > 0) return `${m}分${s}秒`;
  return `${s}秒`;
}

/** 钓鱼相关展示：始终含秒 */
export function formatFishingDuration(ms: number): string {
  const totalSec = Math.max(0, Math.floor(ms / 1000));
  const h = Math.floor(totalSec / 3600);
  const m = Math.floor((totalSec % 3600) / 60);
  const s = totalSec % 60;
  if (h > 0) return `${h}小时${m}分${s}秒`;
  if (m > 0) return `${m}分${s}秒`;
  return `${s}秒`;
}

export function getNickname(): string {
  const auth = getAuthSession();
  if (auth?.nickname) return auth.nickname;
  return '钓友';
}
