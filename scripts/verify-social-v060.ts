/**
 * v0.6.0 FEAT-SOC-01/02/03 验收
 * 运行: npm run verify:social-v060
 */
import fs from 'fs';
import path from 'path';
import http from 'http';
import { fileURLToPath } from 'url';
import type { AddressInfo } from 'net';
import { randomUUID } from 'crypto';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const tmpDir = path.join(ROOT, 'data', `verify-social-v060-${Date.now()}`);
fs.mkdirSync(tmpDir, { recursive: true });
process.env.DB_PATH = path.join(tmpDir, 'test.db');
process.env.NODE_ENV = 'development';
process.env.JWT_SECRET = process.env.JWT_SECRET ?? 'verify-social-v060-secret';
process.env.PLAYER_ERASE_PEPPER = process.env.PLAYER_ERASE_PEPPER ?? 'verify-pepper';
delete process.env.AUTH_DISABLED;

function assert(cond: boolean, msg: string): void {
  if (!cond) throw new Error(`FAIL: ${msg}`);
  console.log(`  OK: ${msg}`);
}

async function main(): Promise<void> {
  const { Server } = await import('socket.io');
  const { createApp } = await import('../server/src/createApp.js');
  const { signPlayerToken } = await import('../server/src/auth.js');
  const { ensurePlayer } = await import('../server/src/players.js');
  const { createPostFromFish } = await import('../server/src/posts.js');
  const { db } = await import('../server/src/db.js');
  const {
    clearLeaderboardCacheForTests,
    getLeaderboardComputeCountForTests,
    getDailyBiggestLeaderboard,
  } = await import('../server/src/leaderboard.js');
  const { clearCommentRateLimitForTests } = await import('../server/src/postEngagement.js');

  const alice = 'p_soc_alice';
  const bob = 'p_soc_bob';
  const bot = 'bot-soc-1';
  ensurePlayer(alice, 'Alice');
  ensurePlayer(bob, 'Bob');
  ensurePlayer(bot, 'Bot');

  const post = createPostFromFish(
    alice,
    'Alice',
    {
      id: randomUUID(),
      speciesId: 'carp',
      quality: 'purple',
      sizeM: 1.2,
      caughtAt: Date.now(),
    },
    'public',
  );

  const httpServer = http.createServer();
  const io = new Server(httpServer);
  const app = createApp(ROOT, io);
  httpServer.on('request', app);
  await new Promise<void>((resolve) => {
    httpServer.listen(0, '127.0.0.1', () => resolve());
  });
  const { port } = httpServer.address() as AddressInfo;
  const base = `http://127.0.0.1:${port}`;
  const tok = (id: string) => signPlayerToken(id);

  console.log('\n=== TC: like toggle ===');
  const like1 = await fetch(`${base}/api/posts/${post.id}/like`, {
    method: 'POST',
    headers: { Authorization: `Bearer ${tok(bob)}` },
  });
  const like1Body = (await like1.json()) as { liked: boolean; likeCount: number };
  assert(like1.ok && like1Body.liked === true && like1Body.likeCount === 1, 'like once');

  const wall = await fetch(`${base}/api/posts/wall`, {
    headers: { Authorization: `Bearer ${tok(bob)}` },
  });
  const wallBody = (await wall.json()) as {
    posts: Array<{ id: string; likeCount?: number; likedByMe?: boolean; commentCount?: number }>;
  };
  const wallPost = wallBody.posts.find((p) => p.id === post.id);
  assert(!!wallPost && wallPost.likeCount === 1 && wallPost.likedByMe === true, 'wall likedByMe');

  const like2 = await fetch(`${base}/api/posts/${post.id}/like`, {
    method: 'POST',
    headers: { Authorization: `Bearer ${tok(bob)}` },
  });
  const like2Body = (await like2.json()) as { liked: boolean; likeCount: number };
  assert(like2.ok && like2Body.liked === false && like2Body.likeCount === 0, 'unlike');

  const unauthLike = await fetch(`${base}/api/posts/${post.id}/like`, { method: 'POST' });
  assert(unauthLike.status === 401, 'like without auth → 401');

  console.log('\n=== TC: comments ===');
  clearCommentRateLimitForTests();
  const c1 = await fetch(`${base}/api/posts/${post.id}/comments`, {
    method: 'POST',
    headers: {
      Authorization: `Bearer ${tok(bob)}`,
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ text: '漂亮！' }),
  });
  const c1Body = (await c1.json()) as { comment: { id: string }; commentCount: number };
  assert(c1.ok && c1Body.commentCount === 1, 'comment create');

  const c2 = await fetch(`${base}/api/posts/${post.id}/comments`, {
    method: 'POST',
    headers: {
      Authorization: `Bearer ${tok(bob)}`,
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ text: '再来一条' }),
  });
  assert(c2.status === 429, 'comment rate limit 3s');

  clearCommentRateLimitForTests();
  const list = await fetch(`${base}/api/posts/${post.id}/comments`);
  const listBody = (await list.json()) as { comments: unknown[]; commentCount: number };
  assert(listBody.commentCount === 1 && listBody.comments.length === 1, 'comment list');

  const del = await fetch(`${base}/api/posts/${post.id}/comments/${c1Body.comment.id}`, {
    method: 'DELETE',
    headers: { Authorization: `Bearer ${tok(alice)}` },
  });
  const delBody = (await del.json()) as { commentCount: number };
  assert(del.ok && delBody.commentCount === 0, 'author can delete comment');

  console.log('\n=== TC: leaderboard from inventory (include bot) ===');
  const now = Date.now();
  const { addFishToInventory } = await import('../server/src/inventory.js');
  addFishToInventory(alice, {
    speciesId: 'carp',
    quality: 'gold',
    sizeM: 3.5,
    caughtAt: now,
    pondId: 'pond-calm',
  });
  addFishToInventory(bot, {
    speciesId: 'carp',
    quality: 'gold',
    sizeM: 9.9,
    caughtAt: now,
    pondId: 'pond-calm',
  });
  addFishToInventory(bob, {
    speciesId: 'carp',
    quality: 'blue',
    sizeM: 1.0,
    caughtAt: now,
    pondId: 'pond-calm',
  });
  // metrics-only catch must NOT rank (inventory 口径)
  db.prepare(
    `INSERT INTO fishing_metrics (id, event_type, player_id, pond_id, payload, created_at)
     VALUES (?, 'pending_catch_accept', ?, 'pond-calm', ?, ?)`,
  ).run(
    randomUUID(),
    bob,
    JSON.stringify({ speciesId: 'carp', quality: 'gold', sizeM: 99, eventId: 'e-metrics-only' }),
    now,
  );

  clearLeaderboardCacheForTests();
  const biggest = getDailyBiggestLeaderboard({ limit: 20 });
  assert(biggest[0]?.playerId === bot, 'daily biggest is bot (9.9m inventory)');
  assert(biggest.some((e) => e.playerId === bot), 'bot included on board');
  assert(biggest[0]?.value === 9.9, 'daily value is sizeM');
  assert(
    !biggest.some((e) => e.playerId === bob && e.value === 99),
    'metrics-only catch does not rank',
  );

  const weekRes = await fetch(`${base}/api/leaderboard/weekly-king`);
  const weekBody = (await weekRes.json()) as {
    entries: Array<{ playerId: string; value: number }>;
  };
  const botWeek = weekBody.entries.find((e) => e.playerId === bot);
  assert(!!botWeek && botWeek.value === 9.9, 'weekly king uses inventory max sizeM');

  const before = getLeaderboardComputeCountForTests();
  getDailyBiggestLeaderboard({ limit: 20 });
  getDailyBiggestLeaderboard({ limit: 20 });
  assert(
    getLeaderboardComputeCountForTests() === before,
    '5min cache avoids recompute',
  );

  const myRank = await fetch(
    `${base}/api/leaderboard/my-rank?boardType=daily_biggest`,
    { headers: { Authorization: `Bearer ${tok(alice)}` } },
  );
  const myRankBody = (await myRank.json()) as { rank: number | null; value: number };
  assert(myRank.ok && myRankBody.rank === 2 && myRankBody.value === 3.5, 'my-rank alice #2');

  const privacy = await import('../server/src/playerPrivacy.js');
  const exported = privacy.buildPlayerExport(bob);
  assert(
    exported?.social &&
      Array.isArray((exported.social as { postLikes?: unknown }).postLikes),
    'export includes postLikes',
  );

  io.close();
  await new Promise<void>((resolve, reject) => {
    httpServer.close((err) => (err ? reject(err) : resolve()));
  });

  try {
    fs.rmSync(tmpDir, { recursive: true, force: true });
  } catch {
    /* ignore locked db on windows */
  }

  console.log('\nALL PASS: verify:social-v060');
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
