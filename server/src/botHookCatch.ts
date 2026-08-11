import type { Server } from 'socket.io';
import type { ClientToServerEvents, PondFishEntity, PondUser, ServerToClientEvents } from '@fish-social/shared';

export type BotHookCatchHandler = (
  io: Server<ClientToServerEvents, ServerToClientEvents>,
  pondId: string,
  bot: PondUser,
  fish: PondFishEntity,
) => void;

let handler: BotHookCatchHandler | null = null;

export function setBotHookCatchHandler(fn: BotHookCatchHandler): void {
  handler = fn;
}

export function notifyBotHookCatch(
  io: Server<ClientToServerEvents, ServerToClientEvents>,
  pondId: string,
  bot: PondUser,
  fish: PondFishEntity,
): void {
  if (!handler) return;
  try {
    handler(io, pondId, bot, fish);
  } catch (err) {
    // Bot 获鱼不得变成 uncaughtException（会触发致命停机）
    console.error('[botHookCatch] handler failed', {
      pondId,
      botId: bot.id,
      playerId: bot.playerId,
      fishId: fish.id,
      err: err instanceof Error ? err.message : err,
    });
  }
}
