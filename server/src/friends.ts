import { randomUUID } from 'crypto';
import type { FriendInfo, FriendRequest, FriendRequestStatus } from '@fish-social/shared';
import { db } from './db.js';
import { ensurePlayer, getPlayer } from './players.js';

interface RequestRow {
  id: string;
  from_player_id: string;
  from_nickname: string;
  to_player_id: string;
  to_nickname: string;
  status: string;
  created_at: number;
}

interface FriendLinkRow {
  player_id: string;
  friend_id: string;
  since: number;
}

function rowToRequest(row: RequestRow): FriendRequest {
  return {
    id: row.id,
    fromPlayerId: row.from_player_id,
    fromNickname: row.from_nickname,
    toPlayerId: row.to_player_id,
    toNickname: row.to_nickname,
    status: row.status as FriendRequestStatus,
    createdAt: row.created_at,
  };
}

const getRequestStmt = db.prepare('SELECT * FROM friend_requests WHERE id = ?');
const insertRequestStmt = db.prepare(`
  INSERT INTO friend_requests (id, from_player_id, from_nickname, to_player_id, to_nickname, status, created_at)
  VALUES (@id, @fromPlayerId, @fromNickname, @toPlayerId, @toNickname, @status, @createdAt)
`);
const updateRequestStatusStmt = db.prepare(
  'UPDATE friend_requests SET status = ? WHERE id = ?',
);
const listIncomingStmt = db.prepare(
  "SELECT * FROM friend_requests WHERE to_player_id = ? AND status = 'pending' ORDER BY created_at DESC",
);
const listOutgoingStmt = db.prepare(
  "SELECT * FROM friend_requests WHERE from_player_id = ? AND status = 'pending' ORDER BY created_at DESC",
);
const findPendingBetweenStmt = db.prepare(`
  SELECT * FROM friend_requests
  WHERE status = 'pending'
    AND ((from_player_id = @a AND to_player_id = @b) OR (from_player_id = @b AND to_player_id = @a))
  LIMIT 1
`);
const insertFriendLinkStmt = db.prepare(`
  INSERT OR IGNORE INTO friend_links (player_id, friend_id, since) VALUES (?, ?, ?)
`);
const areFriendsStmt = db.prepare(
  'SELECT 1 FROM friend_links WHERE player_id = ? AND friend_id = ? LIMIT 1',
);
const listFriendsStmt = db.prepare(
  'SELECT friend_id, since FROM friend_links WHERE player_id = ? ORDER BY since DESC',
);

export function isBotPlayerId(playerId: string): boolean {
  return playerId.startsWith('bot-');
}

export function areFriends(a: string, b: string): boolean {
  return !!areFriendsStmt.get(a, b);
}

export function getFriends(playerId: string): FriendInfo[] {
  const rows = listFriendsStmt.all(playerId) as { friend_id: string; since: number }[];
  return rows.map((row) => {
    const p = getPlayer(row.friend_id);
    return {
      playerId: row.friend_id,
      nickname: p?.nickname ?? '钓友',
      avatarUrl: p?.avatarUrl,
      since: row.since,
    };
  });
}

export function getFriendRequest(requestId: string): FriendRequest | undefined {
  const row = getRequestStmt.get(requestId) as RequestRow | undefined;
  return row ? rowToRequest(row) : undefined;
}

export function sendFriendRequest(
  fromPlayerId: string,
  fromNickname: string,
  toPlayerId: string,
): { ok: true; request: FriendRequest } | { ok: false; error: string } {
  if (fromPlayerId === toPlayerId) {
    return { ok: false, error: '不能添加自己为好友' };
  }
  ensurePlayer(fromPlayerId, fromNickname);
  const toPlayer = getPlayer(toPlayerId);
  if (!toPlayer) return { ok: false, error: '对方玩家不存在，请确认 ID' };

  if (areFriends(fromPlayerId, toPlayerId)) {
    return { ok: false, error: '已经是好友了' };
  }

  const pending = findPendingBetweenStmt.get({ a: fromPlayerId, b: toPlayerId }) as
    | RequestRow
    | undefined;
  if (pending) return { ok: false, error: '已有待处理的好友申请' };

  const request: FriendRequest = {
    id: randomUUID(),
    fromPlayerId,
    fromNickname: fromNickname.slice(0, 12),
    toPlayerId,
    toNickname: toPlayer.nickname,
    status: 'pending',
    createdAt: Date.now(),
  };
  insertRequestStmt.run({
    id: request.id,
    fromPlayerId: request.fromPlayerId,
    fromNickname: request.fromNickname,
    toPlayerId: request.toPlayerId,
    toNickname: request.toNickname,
    status: request.status,
    createdAt: request.createdAt,
  });
  return { ok: true, request };
}

export function getIncomingRequests(playerId: string): FriendRequest[] {
  return (listIncomingStmt.all(playerId) as RequestRow[]).map(rowToRequest);
}

export function getOutgoingRequests(playerId: string): FriendRequest[] {
  return (listOutgoingStmt.all(playerId) as RequestRow[]).map(rowToRequest);
}

function linkFriends(a: string, b: string, since: number): void {
  insertFriendLinkStmt.run(a, b, since);
  insertFriendLinkStmt.run(b, a, since);
}

export function acceptFriendRequest(
  playerId: string,
  requestId: string,
): { ok: true } | { ok: false; error: string } {
  const req = getRequestStmt.get(requestId) as RequestRow | undefined;
  if (!req || req.to_player_id !== playerId) {
    return { ok: false, error: '申请不存在' };
  }
  if (req.status !== 'pending') {
    return { ok: false, error: '申请已处理' };
  }
  const since = Date.now();
  const tx = db.transaction(() => {
    updateRequestStatusStmt.run('accepted', requestId);
    linkFriends(req.from_player_id, req.to_player_id, since);
  });
  tx();
  return { ok: true };
}

export function rejectFriendRequest(
  playerId: string,
  requestId: string,
): { ok: true } | { ok: false; error: string } {
  const req = getRequestStmt.get(requestId) as RequestRow | undefined;
  if (!req || req.to_player_id !== playerId) {
    return { ok: false, error: '申请不存在' };
  }
  updateRequestStatusStmt.run('rejected', requestId);
  return { ok: true };
}

const deleteFriendLinkStmt = db.prepare(
  'DELETE FROM friend_links WHERE player_id = ? AND friend_id = ?',
);

export function removeFriend(
  playerId: string,
  friendPlayerId: string,
): { ok: true } | { ok: false; error: string } {
  if (playerId === friendPlayerId) {
    return { ok: false, error: '不能删除自己' };
  }
  if (!areFriends(playerId, friendPlayerId)) {
    return { ok: false, error: '不是好友' };
  }
  const tx = db.transaction(() => {
    deleteFriendLinkStmt.run(playerId, friendPlayerId);
    deleteFriendLinkStmt.run(friendPlayerId, playerId);
  });
  tx();
  return { ok: true };
}

export function clearAllFriendData(): void {
  db.exec('DELETE FROM friend_links');
  db.exec('DELETE FROM friend_requests');
}
