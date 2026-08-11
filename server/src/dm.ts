import { randomUUID } from 'crypto';
import type { DirectMessage, DmConversation } from '@fish-social/shared';
import { areFriends } from './friends.js';
import { getPlayer } from './players.js';
import { db } from './db.js';

const MAX_DM = 1000;

interface DmRow {
  id: string;
  from_player_id: string;
  from_nickname: string;
  to_player_id: string;
  text: string;
  created_at: number;
}

const insertMsgStmt = db.prepare(`
  INSERT INTO dm_messages (id, from_player_id, from_nickname, to_player_id, text, created_at)
  VALUES (@id, @fromPlayerId, @fromNickname, @toPlayerId, @text, @createdAt)
`);
const listConversationStmt = db.prepare(`
  SELECT * FROM dm_messages
  WHERE (from_player_id = @a AND to_player_id = @b) OR (from_player_id = @b AND to_player_id = @a)
  ORDER BY created_at ASC
`);
const upsertReadCursorStmt = db.prepare(`
  INSERT INTO dm_read_cursor (player_id, friend_player_id, last_read_at)
  VALUES (@playerId, @friendId, @lastReadAt)
  ON CONFLICT(player_id, friend_player_id) DO UPDATE SET last_read_at = excluded.last_read_at
`);
const getReadCursorStmt = db.prepare(
  'SELECT last_read_at FROM dm_read_cursor WHERE player_id = ? AND friend_player_id = ?',
);
const trimDmStmt = db.prepare(`
  DELETE FROM dm_messages WHERE id NOT IN (
    SELECT id FROM dm_messages ORDER BY created_at DESC LIMIT ${MAX_DM}
  )
`);

function rowToMessage(row: DmRow): DirectMessage {
  return {
    id: row.id,
    fromPlayerId: row.from_player_id,
    fromNickname: row.from_nickname,
    toPlayerId: row.to_player_id,
    text: row.text,
    createdAt: row.created_at,
  };
}

export function sendDirectMessage(
  fromPlayerId: string,
  fromNickname: string,
  toPlayerId: string,
  text: string,
): { ok: true; message: DirectMessage } | { ok: false; error: string } {
  if (!areFriends(fromPlayerId, toPlayerId)) {
    return { ok: false, error: '只能给好友发私信' };
  }
  const trimmed = text.trim();
  if (!trimmed) return { ok: false, error: '消息不能为空' };
  if (trimmed.length > 300) return { ok: false, error: '消息过长' };

  const msg: DirectMessage = {
    id: randomUUID(),
    fromPlayerId,
    fromNickname: fromNickname.slice(0, 12),
    toPlayerId,
    text: trimmed,
    createdAt: Date.now(),
  };
  insertMsgStmt.run({
    id: msg.id,
    fromPlayerId: msg.fromPlayerId,
    fromNickname: msg.fromNickname,
    toPlayerId: msg.toPlayerId,
    text: msg.text,
    createdAt: msg.createdAt,
  });
  trimDmStmt.run();
  return { ok: true, message: msg };
}

export function getConversation(
  playerId: string,
  friendPlayerId: string,
): DirectMessage[] {
  return (listConversationStmt.all({ a: playerId, b: friendPlayerId }) as DmRow[]).map(
    rowToMessage,
  );
}

export function markConversationRead(playerId: string, friendPlayerId: string): void {
  upsertReadCursorStmt.run({
    playerId,
    friendId: friendPlayerId,
    lastReadAt: Date.now(),
  });
}

export function listConversations(playerId: string): DmConversation[] {
  const rows = db
    .prepare(
      `
    SELECT
      CASE WHEN from_player_id = @playerId THEN to_player_id ELSE from_player_id END AS friend_id,
      MAX(created_at) AS last_at
    FROM dm_messages
    WHERE from_player_id = @playerId OR to_player_id = @playerId
    GROUP BY friend_id
    ORDER BY last_at DESC
  `,
    )
    .all({ playerId }) as { friend_id: string; last_at: number }[];

  const result: DmConversation[] = [];
  for (const row of rows) {
    const conv = getConversation(playerId, row.friend_id);
    if (conv.length === 0) continue;
    const last = conv[conv.length - 1];
    const friend = getPlayer(row.friend_id);
    const cursor = getReadCursorStmt.get(playerId, row.friend_id) as
      | { last_read_at: number }
      | undefined;
    const unread =
      last.fromPlayerId !== playerId &&
      (!cursor || last.createdAt > cursor.last_read_at)
        ? 1
        : 0;
    result.push({
      friendPlayerId: row.friend_id,
      friendNickname: friend?.nickname ?? '钓友',
      lastMessage: last.text,
      lastAt: row.last_at,
      unread,
    });
  }
  return result;
}

export function clearAllDmData(): void {
  db.exec('DELETE FROM dm_messages');
  db.exec('DELETE FROM dm_read_cursor');
}
