// 事件字典 — 新增 event 须同步更新 xlsx（npm run planning:metrics-xlsx 维护流程）

export interface MetricEventSchema {
  eventType: string;
  requiredFields: string[];
  optionalFields: string[];
  description: string;
}

export const FISHING_METRIC_EVENTS: MetricEventSchema[] = [
  { eventType: 'fishing_start', requiredFields: ['playerId', 'pondId'], optionalFields: ['spotId', 'baitId', 'tackleId'], description: '玩家开始钓鱼' },
  { eventType: 'fishing_stop', requiredFields: ['playerId', 'pondId'], optionalFields: ['reason', 'fishingMs', 'sessionHooks', 'sessionEscapes', 'sessionMissTicks', 'missTicksSinceLastHook'], description: '玩家停止钓鱼（可含会话咬钩摘要）' },
  { eventType: 'bite_hook', requiredFields: ['playerId', 'pondId'], optionalFields: ['fishId', 'speciesId', 'biteWeight', 'quality', 'sessionHooks', 'sessionEscapes', 'sessionMissTicks', 'missTicksSinceLastHook', 'waitingMsSinceLastHook'], description: '鱼咬钩（含会话累计计数；D-L2-15）' },
  { eventType: 'catch_accept', requiredFields: ['playerId', 'pondId'], optionalFields: ['fishId', 'speciesId', 'quality', 'sizeM'], description: '收鱼成功' },
  { eventType: 'pending_catch_accept', requiredFields: ['playerId', 'pondId'], optionalFields: ['pendingCatchId', 'fishId'], description: '挂机收鱼' },
  { eventType: 'escape', requiredFields: ['playerId', 'pondId'], optionalFields: ['fishId', 'speciesId', 'reason', 'sessionHooks', 'sessionEscapes', 'sessionMissTicks'], description: '鱼脱钩（含会话累计计数；D-L2-15）' },
  { eventType: 'bait_buy', requiredFields: ['playerId'], optionalFields: ['baitId', 'quantity', 'cost'], description: '购买鱼饵' },
  { eventType: 'tackle_buy', requiredFields: ['playerId'], optionalFields: ['tackleId', 'cost'], description: '购买渔具' },
  { eventType: 'tackle_repair', requiredFields: ['playerId'], optionalFields: ['tackleId', 'cost'], description: '修理渔具' },
  { eventType: 'gold_earn', requiredFields: ['playerId'], optionalFields: ['amount', 'source', 'fishId', 'quality', 'sizeM'], description: '金币获得' },
  { eventType: 'abandon_fishing', requiredFields: ['playerId'], optionalFields: ['pondId', 'fishingMs'], description: '弃钓' },
  { eventType: 'escape_streak', requiredFields: ['playerId'], optionalFields: ['streak'], description: '连续脱钩' },
  { eventType: 'socket_connect', requiredFields: ['playerId'], optionalFields: ['socketId'], description: 'Socket 连接' },
  { eventType: 'socket_connect_error', requiredFields: [], optionalFields: ['socketId', 'error'], description: 'Socket 连接失败' },
  { eventType: 'join_pond_attempt', requiredFields: ['playerId', 'pondId'], optionalFields: [], description: '尝试加入鱼塘' },
  { eventType: 'join_pond_success', requiredFields: ['playerId', 'pondId'], optionalFields: ['spotId'], description: '加入鱼塘成功' },
  { eventType: 'join_pond_fail', requiredFields: ['playerId', 'pondId'], optionalFields: ['reason'], description: '加入鱼塘失败' },
  { eventType: 'spot_take_success', requiredFields: ['playerId', 'pondId', 'spotId'], optionalFields: [], description: '占位成功' },
  { eventType: 'spot_take_fail', requiredFields: ['playerId', 'pondId', 'spotId'], optionalFields: ['reason'], description: '占位失败' },
  { eventType: 'spot_release', requiredFields: ['playerId', 'pondId', 'spotId'], optionalFields: ['reason'], description: '释放位置' },
  { eventType: 'pond_full_reject', requiredFields: ['playerId', 'pondId'], optionalFields: [], description: '鱼塘满员拒绝' },
  { eventType: 'bot_evicted_for_human', requiredFields: ['pondId'], optionalFields: ['botPlayerId'], description: 'Bot 被挤走' },
  { eventType: 'disconnect', requiredFields: ['playerId', 'pondId'], optionalFields: ['socketId', 'reason'], description: '断线' },
  { eventType: 'socket_disconnect', requiredFields: ['playerId'], optionalFields: ['socketId', 'reason'], description: 'Socket 断线（别名）' },
  { eventType: 'reconnect', requiredFields: ['playerId', 'pondId'], optionalFields: ['socketId'], description: '重连' },
  { eventType: 'disconnect_timeout', requiredFields: ['playerId', 'pondId'], optionalFields: ['timeoutMs'], description: '断线超时' },
  { eventType: 'leave_pond', requiredFields: ['playerId', 'pondId'], optionalFields: ['reason', 'sessionHooks', 'sessionEscapes', 'sessionMissTicks', 'missTicksSinceLastHook'], description: '离开鱼塘' },
  { eventType: 'fishing_phase_transition', requiredFields: ['playerId', 'pondId', 't', 'c'], optionalFields: ['f'], description: '阶段转换（D-L2-16：payload 仅 f/t/c；ADMIN-OBS-1.3：默认 bot 不落库，METRICS_BOT_PHASE=1 恢复）' },
  { eventType: 'phase_transition_invalid', requiredFields: ['playerId', 'pondId', 't', 'c'], optionalFields: ['f'], description: '无效阶段转换（短码 f/t/c；ADMIN-OBS-1.3：默认 bot 不落库，METRICS_BOT_PHASE=1 恢复）' },
  { eventType: 'bite_tick_miss', requiredFields: ['playerId', 'pondId'], optionalFields: ['tickIndex'], description: '咬钩 tick 未命中（deprecated：默认不落库；METRICS_BITE_TICK_PERSIST=1 时恢复）' },
  { eventType: 'bite_tick_hit', requiredFields: ['playerId', 'pondId'], optionalFields: ['tickIndex'], description: '咬钩 tick 命中（deprecated：默认不落库，语义并入 bite_hook；METRICS_BITE_TICK_PERSIST=1 时恢复）' },
  { eventType: 'pending_catch_created', requiredFields: ['playerId', 'pondId'], optionalFields: ['pendingCatchId', 'timeoutMs'], description: '挂机 catch 创建' },
  { eventType: 'pending_catch_expired', requiredFields: ['playerId', 'pondId'], optionalFields: ['pendingCatchId'], description: '挂机 catch 过期' },
  { eventType: 'bait_depleted', requiredFields: ['playerId', 'pondId'], optionalFields: ['baitId'], description: '鱼饵耗尽' },
  { eventType: 'server_start', requiredFields: [], optionalFields: ['pid', 'startedAt', 'reason'], description: '服务进程启动' },
  { eventType: 'server_stop', requiredFields: [], optionalFields: ['pid', 'uptimeSec', 'reason'], description: '服务进程优雅停机' },
  { eventType: 'pond_ecology_catchup', requiredFields: ['pondId'], optionalFields: ['offlineMs', 'replaySteps', 'migrated', 'supplemented', 'durationMs', 'catchupCompacted'], description: '空鱼塘唤醒离线生态补算' },
  { eventType: 'admission_fee_charged', requiredFields: ['playerId', 'pondId'], optionalFields: ['feePer2h', 'chargeIndex', 'progressMs', 'coinsAfter'], description: 'FEAT-PROG-01：收费塘满2h扣费成功' },
  { eventType: 'fishing_stopped_insufficient_gold', requiredFields: ['playerId', 'pondId'], optionalFields: ['feePer2h', 'charges', 'progressMs', 'coins'], description: 'FEAT-PROG-01：金币不足停钓' },
  { eventType: 'onboarding_completed', requiredFields: ['playerId'], optionalFields: ['pondId', 'completedAt'], description: 'FEAT-PROG-01：新手引导完成' },
  { eventType: 'pond_proficiency_capped', requiredFields: ['playerId', 'pondId'], optionalFields: ['pondLevel', 'playerLevel', 'source', 'fishingMs'], description: 'FEAT-PROG-01：塘熟练度满/锁满停发塘XP' },
  { eventType: 'bait_use', requiredFields: ['playerId'], optionalFields: ['baitId', 'cost', 'coinsAfter'], description: 'FEAT-GEAR-01：咬钩使用进阶饵扣金' },
  { eventType: 'rod_buy', requiredFields: ['playerId'], optionalFields: ['rodId', 'cost'], description: 'FEAT-GEAR-01：购买钓竿' },
  { eventType: 'rod_broke', requiredFields: ['playerId'], optionalFields: ['rodId', 'sizeM', 'oversizeLandings', 'pondId'], description: 'FEAT-GEAR-01：超规格满N次销毁钓竿' },
  { eventType: 'vessel_buy', requiredFields: ['playerId'], optionalFields: ['vesselId', 'cost'], description: 'FEAT-GEAR-01：购买船具（不可用）' },
  { eventType: 'forbidden_pond_fine', requiredFields: ['playerId', 'pondId'], optionalFields: ['charged', 'coinsAfter', 'fineGold', 'raidId'], description: 'FEAT-RISK-01：禁止塘巡警超时罚款+当日禁钓' },
  { eventType: 'forbidden_pond_escaped', requiredFields: ['playerId', 'pondId'], optionalFields: ['untilMs', 'raidId'], description: 'FEAT-RISK-01：禁止塘巡警时限内离塘免罚，2h禁入' },
  { eventType: 'gameplay_debug_action', requiredFields: ['playerId', 'action'], optionalFields: ['pondId', 'ok', 'message'], description: 'STEAM-DESKTOP-12：玩法 Debug 菜单操作' },
  { eventType: 'fish_returned_to_pond', requiredFields: ['playerId', 'pondId'], optionalFields: ['speciesId', 'sizeM', 'gold', 'sizeGainM', 'quality', 'newSizeM', 'sellGold', 'spawned'], description: 'FEAT-RETURN-01：回鱼入塘增重并发奖' },
];

export function validateMetricPayload(eventType: string, payload: Record<string, unknown>): string[] {
  const schema = FISHING_METRIC_EVENTS.find(e => e.eventType === eventType);
  if (!schema) return [`unknown event type: ${eventType}`];
  const warnings: string[] = [];
  for (const field of schema.requiredFields) {
    if (payload[field] === undefined || payload[field] === null) {
      warnings.push(`missing required field "${field}" for ${eventType}`);
    }
  }
  return warnings;
}
