import { Platform } from 'react-native';
import * as SecureStore from 'expo-secure-store';

export const TOKEN_STORAGE_KEY = 'fish_social_jwt';
export const REFRESH_LEAD_SEC = 5 * 60;

export interface StoredTokenRecord {
  token: string;
  playerId: string;
  expSec: number;
}

function base64UrlDecode(data: string): string {
  const padded = data.replace(/-/g, '+').replace(/_/g, '/');
  const pad = padded.length % 4;
  const base64 = pad ? padded + '='.repeat(4 - pad) : padded;
  if (typeof globalThis.atob === 'function') {
    return globalThis.atob(base64);
  }
  return Buffer.from(base64, 'base64').toString('utf8');
}

export function decodeJwtExpSec(token: string): number | null {
  const parts = token.split('.');
  if (parts.length !== 3) return null;
  try {
    const payload = JSON.parse(base64UrlDecode(parts[1])) as { exp?: number };
    return typeof payload.exp === 'number' ? payload.exp : null;
  } catch {
    return null;
  }
}

export function shouldRefreshToken(expSec: number, nowSec = Math.floor(Date.now() / 1000)): boolean {
  return expSec - nowSec <= REFRESH_LEAD_SEC;
}

async function readRawToken(): Promise<string | null> {
  if (Platform.OS === 'web') {
    if (typeof localStorage === 'undefined') return null;
    return localStorage.getItem(TOKEN_STORAGE_KEY);
  }
  return SecureStore.getItemAsync(TOKEN_STORAGE_KEY);
}

async function writeRawToken(token: string | null): Promise<void> {
  if (Platform.OS === 'web') {
    if (typeof localStorage === 'undefined') return;
    if (!token) localStorage.removeItem(TOKEN_STORAGE_KEY);
    else localStorage.setItem(TOKEN_STORAGE_KEY, token);
    return;
  }
  if (!token) await SecureStore.deleteItemAsync(TOKEN_STORAGE_KEY);
  else await SecureStore.setItemAsync(TOKEN_STORAGE_KEY, token);
}

const memoryToken: { record: StoredTokenRecord | null } = { record: null };

function parseStoredRecord(raw: string | null): StoredTokenRecord | null {
  if (!raw) return null;
  try {
    const parsed = JSON.parse(raw) as StoredTokenRecord;
    if (!parsed.token || !parsed.playerId || typeof parsed.expSec !== 'number') return null;
    return parsed;
  } catch {
    const expSec = decodeJwtExpSec(raw);
    if (!expSec) return null;
    return { token: raw, playerId: '', expSec };
  }
}

export async function getStoredTokenRecord(): Promise<StoredTokenRecord | null> {
  if (memoryToken.record) return memoryToken.record;
  const raw = await readRawToken();
  const record = parseStoredRecord(raw);
  memoryToken.record = record;
  return record;
}

export async function setStoredToken(token: string, playerId: string): Promise<void> {
  const expSec = decodeJwtExpSec(token);
  if (!expSec) throw new Error('invalid token: missing exp');
  const record: StoredTokenRecord = { token, playerId, expSec };
  memoryToken.record = record;
  await writeRawToken(JSON.stringify(record));
}

export async function clearStoredToken(): Promise<void> {
  memoryToken.record = null;
  await writeRawToken(null);
}
