import { randomUUID } from 'crypto';
import { MAX_CHAT_HISTORY, type ChatMessage } from '@fish-social/shared';

const pondChats = new Map<string, ChatMessage[]>();

export function ensurePondChat(pondId: string): ChatMessage[] {
  if (!pondChats.has(pondId)) {
    pondChats.set(pondId, []);
  }
  return pondChats.get(pondId)!;
}

export function getPondMessages(pondId: string): ChatMessage[] {
  return [...ensurePondChat(pondId)];
}

export function appendChatMessage(pondId: string, message: ChatMessage): ChatMessage {
  const history = ensurePondChat(pondId);
  history.push(message);
  if (history.length > MAX_CHAT_HISTORY) {
    history.splice(0, history.length - MAX_CHAT_HISTORY);
  }
  return message;
}

export function postAnnouncement(pondId: string, text: string): ChatMessage {
  return appendChatMessage(pondId, {
    id: randomUUID(),
    pondId,
    userId: 'system',
    nickname: '系统',
    text,
    createdAt: Date.now(),
    type: 'announcement',
  });
}
