import fs from 'node:fs';
import { createRequire } from 'node:module';
import { describe, expect, it } from 'vitest';
import {
  DEFAULT_AVATARS,
  isDefaultAvatarPath,
  normalizeCatAssetPath,
} from '@fish-social/shared';
import { assertDefaultAvatarCatalog } from '../defaultAvatarCatalog.js';

const CURRENT = [
  'calico',
  'orange',
  'white',
  'silver',
  'brown',
  'cream',
  'blue',
  'black',
  'cow',
  'dafeiyu',
  'sangbiao',
] as const;

describe('default avatar catalog (BUG-55)', () => {
  it('imports the dist module the server resolves', () => {
    const require = createRequire(import.meta.url);
    const resolved = require.resolve('@fish-social/shared');
    expect(resolved.replace(/\\/g, '/')).toMatch(/\/shared\/dist\/index\.js$/);
  });

  it('accepts the nine cats, Dafeiyu, and Sangbiao', () => {
    expect(DEFAULT_AVATARS.map((avatar) => avatar.id)).toEqual([...CURRENT]);
    for (const id of CURRENT) {
      expect(isDefaultAvatarPath(`/image/profile/cat_avatar_${id}.png`)).toBe(true);
    }
  });

  it('normalizes retired gray, siamese, and tuxedo to orange', () => {
    for (const id of ['gray', 'siamese', 'tuxedo']) {
      const normalized = normalizeCatAssetPath(`/image/profile/cat_avatar_${id}.png`);
      expect(normalized).toBe('/image/profile/cat_avatar_orange.png');
      expect(isDefaultAvatarPath(normalized)).toBe(true);
    }
  });

  it('rejects an unknown filename and keeps the profile error text', () => {
    expect(isDefaultAvatarPath('/image/profile/not-a-cat.png')).toBe(false);
    const players = fs.readFileSync(new URL('../players.ts', import.meta.url), 'utf8');
    expect(players).toContain('无效的头像，请选择默认头像或上传图片');
  });

  it('startup assert passes against the resolved catalog', () => {
    expect(() => assertDefaultAvatarCatalog()).not.toThrow();
  });
});
