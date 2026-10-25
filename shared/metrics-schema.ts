// 事件字典 — 新增 event 须同步更新 xlsx（npm run planning:metrics-xlsx 维护流程）

export interface MetricEventSchema {
  eventType: string;
  requiredFields: string[];
  optionalFields: string[];
  description: string;
}

export const FISHING_METRIC_EVENTS: MetricEventSchema[] = [
  { eventType: 'fishing_start', requiredFields: ['playerId', 'pondId'], optionalFields: [], description: '开始钓鱼。身份在列上，payload 为 {}' },
  { eventType: 'fishing_stop', requiredFields: ['playerId', 'pondId'], optionalFields: ['sessionHooks', 'sessionEscapes', 'sessionMissTicks', 'missTicksSinceLastHook'], description: '停止钓鱼，payload 为会话咬钩计数' },
  { eventType: 'bite_hook', requiredFields: ['playerId', 'pondId'], optionalFields: ['speciesId', 'quality', 'sessionHooks', 'sessionEscapes', 'sessionMissTicks', 'missTicksSinceLastHook', 'waitingMsSinceLastHook'], description: '鱼咬钩（含会话累计计数；D-L2-15）' },
  { eventType: 'catch_accept', requiredFields: ['playerId', 'pondId'], optionalFields: ['fishId', 'speciesId', 'quality', 'sizeM'], description: '已废弃：生产改写 pending_catch_accept。历史行与测试仍认' },
  { eventType: 'pending_catch_accept', requiredFields: ['playerId', 'pondId'], optionalFields: ['speciesId', 'quality', 'sizeM', 'eventId', 'auto'], description: '收鱼入包。dedup_key = pending_catch_accept:{eventId}' },
  { eventType: 'escape', requiredFields: ['playerId', 'pondId'], optionalFields: ['sessionHooks', 'sessionEscapes', 'sessionMissTicks'], description: '鱼脱钩（含会话累计计数；D-L2-15）' },
  { eventType: 'bait_buy', requiredFields: ['playerId'], optionalFields: ['baitId', 'quantity', 'cost'], description: '已废弃：买饵接口拒绝，无生产写入' },
  { eventType: 'tackle_buy', requiredFields: ['playerId'], optionalFields: ['tackleId', 'cost'], description: '已废弃：生产改写 rod_buy' },
  { eventType: 'tackle_repair', requiredFields: ['playerId'], optionalFields: ['tackleId', 'cost'], description: '修理渔具' },
  { eventType: 'gold_earn', requiredFields: ['playerId'], optionalFields: ['amount', 'source', 'fishId', 'quality', 'sizeM'], description: '金币获得。source 为 fish_sell 或 fish_return' },
  { eventType: 'abandon_fishing', requiredFields: ['playerId'], optionalFields: [], description: '开钓后 30 秒内停止。身份在列上，payload 为 {}' },
  { eventType: 'escape_streak', requiredFields: ['playerId'], optionalFields: ['streak', 'eventId'], description: '同一玩家连续脱钩达到 3 次' },
  { eventType: 'socket_connect', requiredFields: [], optionalFields: ['socketId', 'reason', 'correlationId', 'playerId'], description: 'Socket 建连。发生在绑定玩家之前，默认无 playerId' },
  { eventType: 'socket_connect_error', requiredFields: [], optionalFields: ['socketId', 'reason', 'code'], description: 'Socket 建连失败' },
  { eventType: 'join_pond_attempt', requiredFields: ['playerId', 'pondId'], optionalFields: ['socketId', 'reason'], description: '收到 join_pond。recordStructuredMetric 会把身份再写入 payload' },
  { eventType: 'join_pond_success', requiredFields: ['playerId', 'pondId'], optionalFields: ['userId', 'socketId', 'spotId', 'fishingPhase', 'reason', 'joinKind', 'disconnectDurationMs'], description: '加入鱼塘成功。joinKind 为 fresh 或 reconnect' },
  { eventType: 'join_pond_fail', requiredFields: ['playerId', 'pondId'], optionalFields: ['socketId', 'reason', 'ackError', 'eventId'], description: '加入鱼塘失败' },
  { eventType: 'spot_take_success', requiredFields: ['playerId', 'pondId', 'spotId'], optionalFields: ['userId', 'socketId', 'fishingPhase', 'eventId'], description: '占位成功' },
  { eventType: 'spot_take_fail', requiredFields: ['playerId', 'pondId', 'spotId'], optionalFields: ['userId', 'reason', 'occupantUserId', 'eventId'], description: '占位失败' },
  { eventType: 'spot_release', requiredFields: ['playerId', 'pondId', 'spotId'], optionalFields: ['userId', 'fishingPhase', 'reason', 'eventId'], description: '释放钓位。reason 为 leave_pond 或 leave_spot' },
  { eventType: 'pond_full_reject', requiredFields: ['playerId', 'pondId'], optionalFields: ['socketId', 'reason', 'maxPondUsers', 'eventId'], description: '鱼塘满员拒绝' },
  { eventType: 'bot_evicted_for_human', requiredFields: ['pondId'], optionalFields: ['userId', 'reason', 'eventId'], description: 'Bot 被挤走。player_id 列是被挤走的 bot' },
  { eventType: 'disconnect', requiredFields: ['playerId', 'pondId'], optionalFields: ['socketId', 'reason'], description: '已废弃：生产改写 socket_disconnect。测试仍可写入' },
  { eventType: 'socket_disconnect', requiredFields: ['playerId', 'pondId'], optionalFields: ['userId', 'spotId', 'fishingPhase', 'reason'], description: 'Socket 断开，进入 60 秒宽限' },
  { eventType: 'reconnect', requiredFields: ['playerId', 'pondId'], optionalFields: ['userId', 'spotId', 'fishingPhase', 'reason'], description: '宽限期内重连' },
  { eventType: 'disconnect_timeout', requiredFields: ['playerId', 'pondId'], optionalFields: ['userId', 'spotId', 'fishingPhase', 'reason'], description: '断线宽限超时清场' },
  { eventType: 'leave_pond', requiredFields: ['playerId', 'pondId'], optionalFields: ['reason', 'spotId', 'fishingPhase', 'sessionHooks', 'sessionEscapes', 'sessionMissTicks', 'missTicksSinceLastHook', 'raidId'], description: '离开鱼塘。主动离塘带会话计数；巡警罚款离塘带 raidId' },
  { eventType: 'fishing_phase_transition', requiredFields: ['playerId', 'pondId', 'f', 't', 'c'], optionalFields: [], description: '阶段转换。payload 仅 f/t/c。f=-1 无来源；0 idle 1 seated 2 baiting 3 casting 4 waiting 5 hooked 6 resolving 7 stopping 8 disconnected 9 groundbaiting。默认 bot 不落库，METRICS_BOT_PHASE=1 恢复' },
  { eventType: 'phase_transition_invalid', requiredFields: ['playerId', 'pondId', 'f', 't', 'c'], optionalFields: [], description: '非法阶段转换。payload 仅 f/t/c。默认 bot 不落库，METRICS_BOT_PHASE=1 恢复' },
  { eventType: 'bite_tick_miss', requiredFields: ['playerId', 'pondId'], optionalFields: ['userId', 'spotId', 'reason', 'sampledFishId', 'sampledSpeciesId'], description: '已废弃默认：waiting 未咬钩。METRICS_BITE_TICK_PERSIST=1 才落库' },
  { eventType: 'bite_tick_hit', requiredFields: ['playerId', 'pondId'], optionalFields: ['userId', 'spotId', 'speciesId', 'quality', 'sizeM', 'hookDurationMs'], description: '已废弃默认：waiting 咬钩 tick。语义并入 bite_hook；METRICS_BITE_TICK_PERSIST=1 才落库' },
  { eventType: 'pending_catch_created', requiredFields: ['playerId', 'pondId'], optionalFields: ['userId', 'pondFishId', 'speciesId', 'quality', 'sizeM', 'hookDurationMs'], description: '锁定待结算咬钩鱼' },
  { eventType: 'pending_catch_expired', requiredFields: ['playerId', 'pondId'], optionalFields: ['userId', 'pondFishId', 'speciesId', 'quality', 'sizeM'], description: '待结算咬钩超时释放' },
  { eventType: 'bait_depleted', requiredFields: ['playerId', 'pondId'], optionalFields: ['userId', 'socketId', 'previousBaitId', 'equippedBaitId', 'reason'], description: '鱼饵耗尽。reason 为 auto_switch_after_depletion 或 bait_insufficient' },
  { eventType: 'server_start', requiredFields: [], optionalFields: ['pid', 'startedAt', 'reason'], description: '服务进程开始监听' },
  { eventType: 'server_stop', requiredFields: [], optionalFields: ['pid', 'uptimeSec', 'reason', 'startedAt'], description: '服务进程开始优雅停机' },
  { eventType: 'pond_ecology_catchup', requiredFields: ['pondId'], optionalFields: ['offlineMs', 'replaySteps', 'migrated', 'supplemented', 'durationMs', 'catchupCompacted'], description: '仅结构化日志，不进 fishing_metrics。空鱼塘唤醒时补算生态' },
  { eventType: 'admission_fee_charged', requiredFields: ['playerId', 'pondId'], optionalFields: ['feePer2h', 'returnFeeMode', 'chargeIndex', 'progressMs', 'coinsAfter'], description: '收费塘满 2 小时扣费成功' },
  { eventType: 'fishing_stopped_insufficient_gold', requiredFields: ['playerId', 'pondId'], optionalFields: ['feePer2h', 'returnFeeMode', 'charges', 'progressMs', 'coins'], description: '金币不足，停止继续扣费钓鱼' },
  { eventType: 'onboarding_completed', requiredFields: ['playerId'], optionalFields: ['completedAt'], description: '新手引导完成。pond_id 列固定 pond-novice' },
  { eventType: 'pond_proficiency_capped', requiredFields: ['playerId', 'pondId'], optionalFields: ['pondLevel', 'playerLevel', 'source', 'fishingMs'], description: '塘熟练度满或锁满，停发塘 XP。source 为 catch、return 或 duration' },
  { eventType: 'bait_use', requiredFields: ['playerId'], optionalFields: ['baitId', 'cost', 'coinsAfter'], description: '咬钩时使用进阶饵扣金' },
  { eventType: 'rod_buy', requiredFields: ['playerId'], optionalFields: ['rodId', 'cost'], description: '购买钓竿' },
  { eventType: 'rod_broke', requiredFields: ['playerId'], optionalFields: ['rodId', 'sizeM', 'oversizeLandings'], description: '超规格满 N 次后销毁钓竿' },
  { eventType: 'vessel_buy', requiredFields: ['playerId'], optionalFields: ['vesselId', 'cost'], description: '购买船具（不可使用）' },
  { eventType: 'forbidden_pond_fine', requiredFields: ['playerId', 'pondId'], optionalFields: ['charged', 'coinsAfter', 'fineGold', 'raidId'], description: '禁止塘巡警超时罚款并当日禁钓' },
  { eventType: 'forbidden_pond_escaped', requiredFields: ['playerId', 'pondId'], optionalFields: ['untilMs', 'raidId'], description: '禁止塘巡警时限内离塘免罚，2 小时禁入' },
  { eventType: 'gameplay_debug_action', requiredFields: ['playerId', 'action'], optionalFields: ['fishId', 'ok', 'message'], description: '玩法 Debug 菜单操作。pond_id 列在有塘时写入' },
  { eventType: 'fish_returned_to_pond', requiredFields: ['playerId', 'pondId'], optionalFields: ['speciesId', 'sizeM', 'gold', 'sizeGainM', 'quality', 'newSizeM', 'sellGold', 'spawned', 'auto'], description: '回鱼入塘增重并发奖' },
  { eventType: 'return_fee_mode_selected', requiredFields: ['playerId', 'pondId'], optionalFields: ['mode', 'feePer2h'], description: '进塘时选定卖鱼或自动回鱼收费模式' },
  { eventType: 'fish_auto_returned', requiredFields: ['playerId', 'pondId'], optionalFields: ['speciesId', 'sizeM', 'gold', 'sizeGainM', 'quality', 'newSizeM', 'sellGold', 'spawned', 'auto'], description: '达标鱼自动回塘。与 fish_returned_to_pond 各写一行' },
  { eventType: 'groundbait_cast_started', requiredFields: ['playerId'], optionalFields: ['groundbaitId', 'costGold', 'stackBefore'], description: '开始打窝并扣金。当前不写 pond_id' },
  { eventType: 'groundbait_applied', requiredFields: ['playerId', 'pondId'], optionalFields: ['groundbaitId', 'stackAfter', 'biteBonus', 'sizeBonus'], description: '打窝完成并叠层' },
  { eventType: 'groundbait_rejected', requiredFields: ['playerId'], optionalFields: ['reason', 'code'], description: '打窝被拒绝。pond_id 列在有塘时写入' },
  { eventType: 'achievement_unlocked', requiredFields: ['playerId'], optionalFields: ['achievementId'], description: '成就解锁' },
  { eventType: 'album_pin_changed', requiredFields: ['playerId'], optionalFields: ['pinCount', 'action'], description: '相册钉选变更' },
  { eventType: 'profile_hub_opened', requiredFields: ['playerId'], optionalFields: ['tab'], description: '打开自己的个人中心' },
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
