import type { Server } from 'socket.io';
import {
  type ClientToServerEvents,
  type FishingFloatTextKind,
  type PondFishEntity,
  type ServerToClientEvents,
} from '@fish-social/shared';

/** 同塘广播咬钩/脱钩飘字（C6 状态机复用此签名） */
export function emitFishingFloatText(
  io: Server<ClientToServerEvents, ServerToClientEvents>,
  pondId: string,
  userId: string,
  kind: Extract<FishingFloatTextKind, 'hook' | 'escape'>,
  target: PondFishEntity,
): void {
  io.to(pondId).emit('fishing_float_text', {
    userId,
    pondId,
    kind,
    speciesId: target.speciesId,
    quality: target.quality,
    timestamp: Date.now(),
  });
}

/** v0.4.1 D12：检测未中（本点无鱼 / 单鱼判定失败） */
export function emitFishingMissFloatText(
  io: Server<ClientToServerEvents, ServerToClientEvents>,
  pondId: string,
  userId: string,
): void {
  io.to(pondId).emit('fishing_float_text', {
    userId,
    pondId,
    kind: 'miss',
    timestamp: Date.now(),
  });
}

/** FEAT-UI-1：抛竿瞬间同塘可见飘字 */
export function emitFishingCastFloatText(
  io: Server<ClientToServerEvents, ServerToClientEvents>,
  pondId: string,
  userId: string,
): void {
  io.to(pondId).emit('fishing_float_text', {
    userId,
    pondId,
    kind: 'cast',
    timestamp: Date.now(),
  });
}
