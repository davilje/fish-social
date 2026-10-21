import { afterAll, beforeAll, describe, expect, it, vi } from 'vitest';
import express from 'express';
import type { AddressInfo } from 'node:net';
import type { Server } from 'node:http';

// Keep all route/SQLite fixtures in this isolated worker, never the user's database.
const originalEnv = vi.hoisted(() => {
  const previous = { DB_PATH: process.env.DB_PATH, AUTH_DISABLED: process.env.AUTH_DISABLED, JWT_SECRET: process.env.JWT_SECRET };
  process.env.DB_PATH = ':memory:';
  delete process.env.AUTH_DISABLED;
  process.env.JWT_SECRET = 'journal-own-feed-test-secret';
  return previous;
});

import { db } from '../db.js';
import { registerSocialRoutes } from '../socialRoutes.js';
import { signPlayerToken } from '../auth.js';
import { ensurePlayer } from '../players.js';

interface Post {
  id: string;
  playerId: string;
  visibility: string;
  likedByMe: boolean;
  likeCount: number;
  commentCount: number;
}

describe('journal own-post feed', () => {
  let server: Server;
  let baseUrl: string;
  const owner = 'journal-feed-owner';
  const other = 'journal-feed-other';
  const composer = 'journal-story-author';

  beforeAll(async () => {
    ensurePlayer(owner, '本人');
    ensurePlayer(other, '其他钓友');
    ensurePlayer(composer, '故事作者');
    const inventory = db.prepare('INSERT INTO inventory (id, player_id, species_id, quality, size_m, caught_at) VALUES (?, ?, ?, ?, ?, ?)');
    inventory.run('story-fish', composer, 'crucian', 'gray', 0.5, 100);
    inventory.run('someone-elses-fish', other, 'crucian', 'gray', 0.8, 100);
    const insert = db.prepare(`INSERT INTO social_posts
      (id, player_id, nickname, fish_json, text, visibility, created_at, like_count, comment_count)
      VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)`);
    const fish = JSON.stringify({ id: 'fish-a', speciesId: 'crucian', quality: 'gray', sizeM: 0.5, caughtAt: 100 });
    for (let index = 0; index < 55; index++) {
      // Identical timestamps exercise the stable secondary order across page boundaries.
      insert.run(`own-${String(index).padStart(2, '0')}`, owner, '本人', fish, '鱼获故事', ['public', 'friends', 'private'][index % 3], 1000, index === 54 ? 7 : 0, index === 54 ? 2 : 0);
    }
    insert.run('other-public', other, '其他钓友', fish, '其他人的公开动态', 'public', 2000, 0, 0);
    insert.run('other-private', other, '其他钓友', fish, '其他人的私密动态', 'private', 2000, 0, 0);
    db.prepare('INSERT INTO post_likes (post_id, player_id, created_at) VALUES (?, ?, ?)').run('own-54', owner, 2000);
    const app = express();
    app.use(express.json());
    registerSocialRoutes(app, {} as never);
    server = app.listen(0, '127.0.0.1');
    await new Promise<void>(resolve => server.once('listening', resolve));
    baseUrl = `http://127.0.0.1:${(server.address() as AddressInfo).port}`;
  });

  afterAll(async () => {
    if (server) await new Promise<void>((resolve, reject) => server.close(error => error ? reject(error) : resolve()));
    db.close();
    for (const [key, value] of Object.entries(originalEnv)) {
      if (value === undefined) delete process.env[key];
      else process.env[key] = value;
    }
  });

  const get = (path: string, identity?: string) => fetch(baseUrl + path, {
    headers: identity ? { Authorization: `Bearer ${signPlayerToken(identity)}` } : {},
  });
  const publish = (body: unknown, identity?: string) => fetch(baseUrl + '/api/posts', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', ...(identity ? { Authorization: `Bearer ${signPlayerToken(identity)}` } : {}) },
    body: JSON.stringify(body),
  });
  async function posts(query: string): Promise<Post[]> {
    const response = await get(`/api/posts/mine/${owner}${query}`, owner);
    expect(response.status).toBe(200);
    return ((await response.json()) as { posts: Post[] }).posts;
  }

  it('requires authentication and rejects another account, including a spoofed viewer query', async () => {
    expect((await get(`/api/posts/mine/${owner}`)).status).toBe(401);
    expect((await get(`/api/posts/mine/${owner}`, other)).status).toBe(403);
    expect((await get(`/api/posts/mine/${owner}?viewer=${owner}&playerId=${owner}`, other)).status).toBe(403);
  });

  it('paginates all of the owner’s visibility scopes without mixing in other authors', async () => {
    const first = await posts('?limit=50&offset=0');
    const second = await posts('?limit=50&offset=50');
    expect(first).toHaveLength(50);
    expect(second).toHaveLength(5);
    const combined = [...first, ...second];
    expect(new Set(combined.map(post => post.id)).size).toBe(55);
    expect(new Set(combined.map(post => post.visibility))).toEqual(new Set(['public', 'friends', 'private']));
    expect(combined.every(post => post.playerId === owner)).toBe(true);
    expect(combined.map(post => post.id)).toEqual(Array.from({ length: 55 }, (_, index) => `own-${String(54 - index).padStart(2, '0')}`));
    expect(await posts('?limit=50&offset=55')).toEqual([]);
  });

  it('normalizes malformed pagination and clamps page size to fifty', async () => {
    expect(await posts('?limit=500&offset=-4')).toHaveLength(50);
    expect(await posts('?limit=0&offset=0')).toHaveLength(1);
    expect(await posts('?limit=NaN&offset=NaN')).toHaveLength(50);
    expect((await posts('?limit=2.9&offset=1.9')).map(post => post.id)).toEqual(['own-53', 'own-52']);
  });

  it('returns server engagement counts and the owner’s actual like state', async () => {
    const [post] = await posts('?limit=1&offset=0');
    expect(post).toMatchObject({ id: 'own-54', likeCount: 7, commentCount: 2, likedByMe: true });
    const [unliked] = await posts('?limit=1&offset=1');
    expect(unliked?.likedByMe).toBe(false);
  });

  it('returns an empty feed for an authenticated player with no posts', async () => {
    const response = await get('/api/posts/mine/new-player', 'new-player');
    expect(response.status).toBe(200);
    expect(await response.json()).toEqual({ posts: [] });
  });

  it('publishes a real inventory fish with trimmed story text and chosen visibility without consuming it', async () => {
    const response = await publish({ fishId: 'story-fish', nickname: '故事作者', visibility: 'friends', text: '  今天在湖边遇见了你。  ' }, composer);
    expect(response.status).toBe(200);
    const result = await response.json() as { post: Post & { text: string; fish: { id: string } } };
    expect(result.post).toMatchObject({ playerId: composer, visibility: 'friends', text: '今天在湖边遇见了你。', fish: { id: 'story-fish' } });
    expect(db.prepare('SELECT id FROM inventory WHERE id = ? AND player_id = ?').get('story-fish', composer)).toBeTruthy();
    const ownResponse = await get(`/api/posts/mine/${composer}`, composer);
    const own = await ownResponse.json() as { posts: Post[] };
    expect(own.posts.some(post => post.id === result.post.id)).toBe(true);
    const wallResponse = await get('/api/posts/wall', other);
    const wall = await wallResponse.json() as { posts: Post[] };
    expect(wall.posts.some(post => post.id === result.post.id)).toBe(false);
  });

  it('rejects missing authentication, another player’s fish, malformed text, and invalid visibility', async () => {
    expect((await publish({ fishId: 'story-fish' })).status).toBe(401);
    expect((await publish({ fishId: 'someone-elses-fish' }, composer)).status).toBe(400);
    for (const invalid of [
      { fishId: 'story-fish', visibility: 'everyone-on-the-internet' },
      { fishId: 'story-fish', text: '鱼'.repeat(161) },
      { fishId: 'story-fish', text: { value: 'not text' } },
      { fishId: 123 },
      { text: '不能绕过真实库存鱼获' },
    ]) expect((await publish(invalid, composer)).status).toBe(400);
  });

  it('preserves legacy fish sharing when custom text and visibility are omitted', async () => {
    const response = await publish({ fishId: 'story-fish', nickname: '故事作者' }, composer);
    expect(response.status).toBe(200);
    const result = await response.json() as { post: { text: string; visibility: string } };
    expect(result.post.text.length).toBeGreaterThan(0);
    expect(result.post.visibility).toBe('public');
  });

});
