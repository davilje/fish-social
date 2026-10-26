#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Build 埋点表清单.xlsx — events catalog per metrics schema (root + docs copy)."""
import shutil
from pathlib import Path

from openpyxl import Workbook
from openpyxl.styles import Alignment, Font, PatternFill

ROOT = Path(__file__).resolve().parents[1]
REPORTS = ROOT / "docs/策划/报告"
# 仓库根目录为权威交付物；docs/策划/报告/ 为同步副本（兼容旧链接）
OUT_NAME = "v0.4.4-埋点表清单.xlsx"
OUT = ROOT / OUT_NAME
OUT_DOCS = REPORTS / OUT_NAME

# 类别, 事件名, 类型, 触发时机, 必填字段, payload示例, 优先级, 状态, 落点文件
# 必填字段 = metrics-schema requiredFields（playerId/pondId 在列上，校验时合并）。
# 示例 payload = fishing_metrics.payload 的当前默认 JSON，不含列字段，除非写入方把身份抄进 JSON。
EVENTS = [
    ("A连接会话", "socket_connect", "metrics+日志", "socket 建连成功，此时尚未绑定玩家", "", '{"socketId":"sock-1","reason":"socket_connected","correlationId":"corr-1"}', "P0", "已实现", "socketLifecycle.ts"),
    ("A连接会话", "socket_connect_error", "metrics+日志", "引擎 connection_error", "", '{"socketId":"sock-1","reason":"xhr poll error","code":1}', "P0", "已实现", "socketLifecycle.ts"),
    ("A连接会话", "socket_disconnect", "metrics+日志", "在塘玩家 socket 断开，进入宽限", "playerId\npondId", '{"userId":"u1","spotId":"spot-1","fishingPhase":"waiting","reason":"socket_disconnect"}', "P0", "已实现", "fishingStateMachine.ts"),
    ("A连接会话", "disconnect", "metrics", "旧断线名。生产不再写，测试与历史查询仍认", "playerId\npondId", '{"socketId":"sock-1","reason":"transport"}', "P2", "已废弃", "fishingStateMachine.ts"),
    ("A连接会话", "reconnect", "metrics+日志", "宽限期内重连回塘", "playerId\npondId", '{"userId":"u1","spotId":"spot-1","fishingPhase":"waiting","reason":"socket_reconnect"}', "P0", "已实现", "fishingStateMachine.ts"),
    ("A连接会话", "disconnect_timeout", "metrics+日志", "断线宽限超时清场", "playerId\npondId", '{"userId":"u1","spotId":"spot-1","fishingPhase":"disconnected","reason":"disconnect_timeout"}', "P0", "已实现", "fishingStateMachine.ts"),
    ("A连接会话", "join_pond_attempt", "metrics+日志", "收到 join_pond。身份会再抄进 payload", "playerId\npondId", '{"socketId":"sock-1","reason":"join_pond_attempt","playerId":"p1","pondId":"pond-calm"}', "P0", "已实现", "socketPondHandlers.ts"),
    ("A连接会话", "join_pond_success", "metrics+日志", "进塘或重连成功。joinKind=fresh|reconnect", "playerId\npondId", '{"userId":"u1","socketId":"sock-1","spotId":null,"fishingPhase":"idle","reason":"join_pond_success","joinKind":"fresh","playerId":"p1","pondId":"pond-calm"}', "P0", "已实现", "socketPondHandlers.ts"),
    ("A连接会话", "join_pond_fail", "metrics+日志", "进塘失败。reason=return_fee_mode|pond_access|police_ban|pond_full 等", "playerId\npondId", '{"socketId":"sock-1","reason":"pond_full","ackError":"鱼塘已满（最多 20 人）"}', "P0", "已实现", "socketPondHandlers.ts"),
    ("A连接会话", "leave_pond", "metrics", "主动离塘带会话计数；巡警罚款离塘 reason=police_fine", "playerId\npondId", '{"reason":"user_explicit","spotId":"spot-1","fishingPhase":"seated","sessionHooks":1,"sessionEscapes":0,"sessionMissTicks":4,"missTicksSinceLastHook":4}', "P0", "已实现", "socketPondHandlers.ts"),
    ("A连接会话", "session_rebound", "日志", "同一 playerId 绑到新 socket。不进 fishing_metrics", "playerId\nsocketId", '{"playerId":"p1","socketId":"sock-2","previousSocketId":"sock-1","reason":"session_registry_bind_player","correlationId":"corr-1"}', "P1", "已实现", "sessionRegistry.ts"),
    ("A连接会话", "server_start", "metrics", "HTTP 开始监听", "", '{"pid":1234,"startedAt":1710000000000,"reason":"unknown"}', "P0", "已实现", "index.ts"),
    ("A连接会话", "server_stop", "metrics", "收到 SIGINT/SIGTERM 或致命错误，开始停机", "", '{"pid":1234,"uptimeSec":3600,"reason":"SIGTERM","startedAt":1710000000000}', "P0", "已实现", "index.ts"),
    ("B钓位鱼塘", "spot_take_success", "metrics", "占座成功", "playerId\npondId\nspotId", '{"userId":"u1","socketId":"sock-1","spotId":"spot-1","fishingPhase":"seated","eventId":"spot_take:u1:spot-1"}', "P1", "已实现", "pondSession.ts"),
    ("B钓位鱼塘", "spot_take_fail", "metrics", "钓位已被占用", "playerId\npondId\nspotId", '{"userId":"u1","spotId":"spot-1","reason":"occupied_by_human","occupantUserId":"u2","eventId":"spot_fail:u1:spot-1"}', "P1", "已实现", "pondSession.ts"),
    ("B钓位鱼塘", "spot_release", "metrics", "离席或离塘释放钓位。reason=leave_spot|leave_pond", "playerId\npondId\nspotId", '{"userId":"u1","spotId":"spot-1","fishingPhase":"idle","reason":"leave_spot","eventId":"spot_release:u1:spot-1:leave_spot"}', "P1", "已实现", "pondSession.ts"),
    ("B钓位鱼塘", "pond_full_reject", "metrics", "人数达到上限", "playerId\npondId", '{"socketId":"sock-1","reason":"max_pond_users","maxPondUsers":20,"eventId":"pond_full:p1:1710000000000"}', "P1", "已实现", "pondSession.ts"),
    ("B钓位鱼塘", "bot_evicted_for_human", "metrics", "给人腾位。player_id 列是被挤走的 bot", "pondId", '{"userId":"bot-user","reason":"pond_full_or_spot_pressure","eventId":"bot_evict:bot-user:1710000000000"}', "P1", "已实现", "pondUserManager.ts"),
    ("B钓位鱼塘", "pond_ecology_catchup", "日志", "空鱼塘唤醒补算。只打日志，不进 fishing_metrics", "pondId", '{"eventType":"pond_ecology_catchup","pondId":"pond-calm","offlineMs":60000,"replaySteps":2,"migrated":0,"supplemented":1,"durationMs":12,"catchupCompacted":false}', "P1", "已实现", "pondEcology.ts"),
    ("B钓位鱼塘", "pond_ecology_catchup_failed", "日志", "进塘前补算失败。只打日志", "pondId", '{"eventType":"pond_ecology_catchup_failed","pondId":"pond-calm","playerId":"p1"}', "P1", "已实现", "socketPondHandlers.ts"),
    ("C状态机", "fishing_phase_transition", "metrics+日志", "相位变更。库内仅 f/t/c；日志用全名。默认 bot 不落库（METRICS_BOT_PHASE=1 恢复）。f=-1 无来源；0 idle 1 seated 2 baiting 3 casting 4 waiting 5 hooked 6 resolving 7 stopping 8 disconnected 9 groundbaiting", "playerId\npondId\nf\nt\nc", '{"f":4,"t":5,"c":"bite_hook"}', "P0", "已实现", "fishingObservability.ts"),
    ("C状态机", "phase_transition_invalid", "metrics+日志", "非法相位跳转。库内仅 f/t/c；日志保留全名。默认 bot 不落库", "playerId\npondId\nf\nt\nc", '{"f":0,"t":5,"c":"bite_hook"}', "P1", "已实现", "fishingStateMachine.ts"),
    ("D咬钩产出", "bite_tick_miss", "metrics", "waiting 未咬钩。默认不落库；METRICS_BITE_TICK_PERSIST=1 恢复", "playerId\npondId", '{"userId":"u1","spotId":"spot-1","reason":"empty"}', "P1", "已废弃默认", "fishingStateMachine.ts"),
    ("D咬钩产出", "bite_tick_hit", "metrics", "waiting 咬钩 tick。默认不落库，语义并入 bite_hook；METRICS_BITE_TICK_PERSIST=1 恢复", "playerId\npondId", '{"userId":"u1","spotId":"spot-1","speciesId":"crucian","quality":"green","sizeM":0.2,"hookDurationMs":8000}', "P1", "已废弃默认", "fishingStateMachine.ts"),
    ("D咬钩产出", "bite_hook", "metrics", "上钩，并写入会话累计", "playerId\npondId", '{"speciesId":"crucian","quality":"green","sessionHooks":1,"sessionEscapes":0,"sessionMissTicks":4,"missTicksSinceLastHook":4,"waitingMsSinceLastHook":180000}', "P0", "已实现", "fishingStateMachine.ts"),
    ("D咬钩产出", "escape", "metrics", "脱钩，并写入会话累计", "playerId\npondId", '{"sessionHooks":1,"sessionEscapes":1,"sessionMissTicks":4}', "P0", "已实现", "fishingStateMachine.ts"),
    ("D咬钩产出", "escape_streak", "metrics", "同一玩家连续脱钩达到 3 次时另写一行", "playerId", '{"streak":3,"eventId":"escape_streak:p1:1710000000000"}', "P1", "已实现", "fishingMetrics.ts"),
    ("D咬钩产出", "fishing_start", "metrics", "开始一局。payload 为空", "playerId\npondId", "{}", "P1", "已实现", "fishingStateMachine.ts"),
    ("D咬钩产出", "fishing_stop", "metrics", "收杆。payload 为会话计数", "playerId\npondId", '{"sessionHooks":2,"sessionEscapes":1,"sessionMissTicks":6,"missTicksSinceLastHook":2}', "P1", "已实现", "fishingStateMachine.ts"),
    ("D咬钩产出", "abandon_fishing", "metrics", "fishing_start 后 30 秒内 fishing_stop。payload 为空", "playerId", "{}", "P1", "已实现", "fishingMetrics.ts"),
    ("D咬钩产出", "pending_catch_created", "metrics", "锁定待结算的咬钩鱼", "playerId\npondId", '{"userId":"u1","pondFishId":"fish-1","speciesId":"crucian","quality":"green","sizeM":0.2,"hookDurationMs":8000}', "P1", "已实现", "inventory.ts"),
    ("D咬钩产出", "pending_catch_accept", "metrics", "收鱼入包。dedup_key=pending_catch_accept:{eventId}", "playerId\npondId", '{"speciesId":"crucian","quality":"green","sizeM":0.2,"eventId":"catch-1","auto":false}', "P1", "已实现", "catchSettlement.ts"),
    ("D咬钩产出", "pending_catch_expired", "metrics", "待结算超时释放", "playerId\npondId", '{"userId":"u1","pondFishId":"fish-1","speciesId":"crucian","quality":"green","sizeM":0.2}', "P1", "已实现", "inventory.ts"),
    ("D咬钩产出", "catch_accept", "metrics", "旧收鱼名。生产改写 pending_catch_accept；历史榜与测试仍认", "playerId\npondId", '{"fishId":"fish-1","speciesId":"crucian","quality":"green","sizeM":0.2}', "P2", "已废弃", "catchSettlement.ts"),
    ("D咬钩产出", "bait_depleted", "metrics", "饵尽。reason=auto_switch_after_depletion|bait_insufficient", "playerId\npondId", '{"userId":"u1","socketId":"sock-1","previousBaitId":"bait-worm","reason":"auto_switch_after_depletion"}', "P1", "已实现", "fishingStateMachine.ts"),
    ("D成长进度", "admission_fee_charged", "metrics", "收费塘满 2 小时扣费成功", "playerId\npondId", '{"feePer2h":200,"returnFeeMode":"sell_only","chargeIndex":1,"progressMs":7200000,"coinsAfter":800}', "P0", "已实现", "playerProgress.ts"),
    ("D成长进度", "fishing_stopped_insufficient_gold", "metrics", "金币不够支付下一档入场费", "playerId\npondId", '{"feePer2h":200,"returnFeeMode":"sell_only","charges":1,"progressMs":14400000,"coins":0}', "P0", "已实现", "playerProgress.ts"),
    ("D成长进度", "onboarding_completed", "metrics", "新手引导完成。pond_id 列固定 pond-novice", "playerId", '{"completedAt":1710000000000}', "P0", "已实现", "playerProgress.ts"),
    ("D成长进度", "pond_proficiency_capped", "metrics", "塘熟练度封顶。source=catch|return|duration", "playerId\npondId", '{"pondLevel":10,"playerLevel":8,"source":"catch"}', "P1", "已实现", "playerProgress.ts"),
    ("D成长进度", "gameplay_debug_action", "metrics", "玩法 Debug 菜单", "playerId\naction", '{"action":"level_up","ok":true,"message":"ok"}', "P1", "已实现", "gameplayDebug.ts"),
    ("D成长进度", "fish_returned_to_pond", "metrics", "回鱼入塘。自动回鱼时再写 fish_auto_returned", "playerId\npondId", '{"speciesId":"crucian","sizeM":0.2,"gold":56,"sizeGainM":0.03,"quality":"green","newSizeM":0.23,"sellGold":40,"spawned":true,"auto":false}', "P0", "已实现", "returnFish.ts"),
    ("D成长进度", "fish_auto_returned", "metrics", "达标自动回塘。payload 与 fish_returned_to_pond 相同且 auto=true", "playerId\npondId", '{"speciesId":"crucian","sizeM":0.28,"gold":120,"sizeGainM":0.04,"quality":"green","newSizeM":0.32,"sellGold":80,"spawned":true,"auto":true}', "P1", "已实现", "returnFish.ts"),
    ("D成长进度", "return_fee_mode_selected", "metrics", "进塘选定卖鱼或自动回鱼", "playerId\npondId", '{"mode":"auto_return","feePer2h":350}', "P1", "已实现", "socketPondHandlers.ts"),
    ("D成长进度", "gold_earn", "metrics", "卖鱼 source=fish_sell；回鱼 source=fish_return", "playerId", '{"amount":56,"source":"fish_sell","fishId":"inv-1","quality":"green","sizeM":0.2}', "P0", "已实现", "socialRoutes.ts"),
    ("D成长进度", "achievement_unlocked", "metrics", "成就解锁", "playerId", '{"achievementId":"ach-first-catch"}', "P1", "已实现", "achievements.ts"),
    ("D成长进度", "album_pin_changed", "metrics", "相册钉选。action=pin|unpin|set", "playerId", '{"pinCount":3,"action":"pin"}', "P1", "已实现", "socialRoutes.ts"),
    ("D成长进度", "profile_hub_opened", "metrics", "打开自己的个人中心。tab 默认 profile", "playerId", '{"tab":"profile"}', "P1", "已实现", "socialRoutes.ts"),
    ("D装备", "bait_use", "metrics", "咬钩时进阶饵扣金", "playerId", '{"baitId":"bait-veg","cost":15,"coinsAfter":985}', "P0", "已实现", "gear.ts"),
    ("D装备", "bait_buy", "metrics", "旧买饵事件。接口已拒绝，无生产写入", "playerId", '{"baitId":"worm","quantity":1,"cost":500}', "P2", "已废弃", "shop.ts"),
    ("D装备", "tackle_buy", "metrics", "旧买渔具事件。生产改写 rod_buy", "playerId", '{"tackleId":"rod-basic","cost":100}', "P2", "已废弃", "shop.ts"),
    ("D装备", "tackle_repair", "metrics", "修理渔具", "playerId", '{"tackleId":"pro","cost":40}', "P1", "已实现", "gear.ts"),
    ("D装备", "rod_buy", "metrics", "购买钓竿", "playerId", '{"rodId":"rod-tai","cost":2500}', "P0", "已实现", "shop.ts"),
    ("D装备", "rod_broke", "metrics", "超规格次数达到销毁阈值", "playerId", '{"rodId":"rod-tai","sizeM":0.5,"oversizeLandings":3}', "P0", "已实现", "gear.ts"),
    ("D装备", "vessel_buy", "metrics", "购买船具（不可使用）", "playerId", '{"vesselId":"vessel-raft","cost":15000}', "P1", "已实现", "shop.ts"),
    ("D装备", "groundbait_cast_started", "metrics", "开始打窝扣金。不写 pond_id", "playerId", '{"groundbaitId":"gb-basic","costGold":30,"stackBefore":0}', "P1", "已实现", "groundbait.ts"),
    ("D装备", "groundbait_applied", "metrics", "打窝完成叠层", "playerId\npondId", '{"groundbaitId":"gb-basic","stackAfter":1,"biteBonus":0.006,"sizeBonus":0.003}', "P1", "已实现", "groundbait.ts"),
    ("D装备", "groundbait_rejected", "metrics", "打窝被拒绝", "playerId", '{"reason":"gold","code":"INSUFFICIENT_GOLD"}', "P1", "已实现", "groundbait.ts"),
    ("D风险", "forbidden_pond_fine", "metrics", "巡警超时罚款并当日禁钓", "playerId\npondId", '{"charged":800,"coinsAfter":0,"fineGold":800,"raidId":"raid-1"}', "P0", "已实现", "forbiddenPolice.ts"),
    ("D风险", "forbidden_pond_escaped", "metrics", "时限内离塘免罚，2 小时禁入", "playerId\npondId", '{"untilMs":1710007200000,"raidId":"raid-1"}', "P0", "已实现", "forbiddenPolice.ts"),
    ("E性能", "tick_fishing_phases_duration_ms", "聚合日志", "相位 tick 耗时。慢于 PERF_LOG_SLOW_MS 才打，默认不进 fishing_metrics", "durationMs", '{"eventType":"tick_fishing_phases_duration_ms","durationMs":12,"pondCount":4,"activeUsers":3,"snapshotCalls":0}', "P2", "已实现", "serverLoops.ts"),
    ("E性能", "bite_check_loop_duration_ms", "聚合日志", "咬钩循环耗时。慢日志，不进 fishing_metrics", "durationMs", '{"eventType":"bite_check_loop_duration_ms","durationMs":20,"pondCount":4,"candidates":2,"triggered":1,"snapshotCalls":0}', "P2", "已实现", "serverLoops.ts"),
    ("E性能", "ecology_tick_duration_ms", "聚合日志", "生态 tick 耗时。慢日志，不进 fishing_metrics", "durationMs", '{"eventType":"ecology_tick_duration_ms","durationMs":30,"pondCount":4}', "P2", "已实现", "serverLoops.ts"),
    ("E性能", "snapshot_build_duration_ms", "聚合日志", "buildSnapshot 耗时。慢日志，不进 fishing_metrics", "durationMs\npondId", '{"eventType":"snapshot_build_duration_ms","durationMs":8,"pondId":"pond-calm","userCount":3}', "P2", "已实现", "pondUserManager.ts"),
    ("E性能", "socket_broadcast_fanout", "指标+可选日志", "广播 fanout。Prometheus 常计；FANOUT_LOG_INFO=1 才打 info", "fanoutCount\nchannel\npondId", '{"eventType":"socket_broadcast_fanout","channel":"pond_user_joined","pondId":"pond-calm","fanoutCount":3}', "P2", "已实现", "serverLoops.ts"),
    ("E性能", "sqlite_query_slow", "日志", "SQL 慢于 SLOW_QUERY_MS（默认 30）。不进 fishing_metrics", "queryName\ndurationMs", '{"eventType":"sqlite_query_slow","queryName":"metrics_player_timeline","durationMs":42,"thresholdMs":30,"rows":20,"ts":1710000000000}', "P2", "已实现", "db.ts"),
    ("E性能", "admin_route_duration_ms", "日志", "每条 Admin 请求结束时 console.log，不是 logStructuredEvent", "path\ndurationMs", '{"eventType":"admin_route_duration_ms","method":"GET","path":"/players","status":200,"durationMs":15,"ts":1710000000000}', "P2", "已实现", "admin.ts"),
    ("E性能", "admin_route_slow", "日志", "Admin 耗时达到 ADMIN_ROUTE_SLOW_MS 时另打一行 console.log", "path\ndurationMs\nthresholdMs", '{"eventType":"admin_route_slow","method":"GET","path":"/players","status":200,"durationMs":800,"thresholdMs":200,"ts":1710000000000}', "P2", "已实现", "admin.ts"),
]

CAT_SHEETS = {
    "A连接会话": "A-连接与会话",
    "B钓位鱼塘": "B-钓位与鱼塘",
    "C状态机": "C-状态机",
    "D咬钩产出": "D-咬钩与产出",
    "D成长进度": "D-成长与进度",
    "D装备": "D-装备",
    "D风险": "D-风险事件",
    "E性能": "E-系统性能",
}


def style_header(ws, ncol: int) -> None:
    for c in ws[1]:
        c.font = Font(bold=True, color="FFFFFF")
        c.fill = PatternFill("solid", fgColor="1F4E78")
        c.alignment = Alignment(horizontal="center", vertical="center", wrap_text=True)


def main() -> None:
    REPORTS.mkdir(parents=True, exist_ok=True)
    wb = Workbook()
    ws = wb.active
    ws.title = "总览"
    headers = ["类别", "事件名", "类型", "触发时机", "必填字段(核心)", "示例payload", "优先级", "状态", "推荐落点文件"]
    ws.append(headers)
    for e in EVENTS:
        ws.append(list(e))
    style_header(ws, len(headers))
    widths = {"A": 14, "B": 34, "C": 16, "D": 42, "E": 22, "F": 56, "G": 8, "H": 14, "I": 28}
    for col, w in widths.items():
        ws.column_dimensions[col].width = w
    for row in ws.iter_rows(min_row=2, max_row=ws.max_row):
        for c in row:
            c.alignment = Alignment(vertical="top", wrap_text=True)

    for cat, sheet in CAT_SHEETS.items():
        wsc = wb.create_sheet(sheet)
        wsc.append(headers)
        for e in EVENTS:
            if e[0] == cat:
                wsc.append(list(e))
        style_header(wsc, len(headers))
        for col, w in widths.items():
            wsc.column_dimensions[col].width = w
        for row in wsc.iter_rows(min_row=2, max_row=wsc.max_row):
            for c in row:
                c.alignment = Alignment(vertical="top", wrap_text=True)

    ws_fmt = wb.create_sheet("落库格式", 1)
    ws_fmt.append(["项", "内容"])
    for row in [
        ("表", "fishing_metrics（SQLite payload 为 TEXT；Postgres payload 为 JSONB）"),
        ("列", "id, event_type, player_id, pond_id, payload, correlation_id, dedup_key, created_at"),
        ("id", "randomUUID()"),
        ("event_type", "与埋点表「事件名」、metrics-schema eventType 相同"),
        ("player_id / pond_id", "recordFishingMetric 的顶层参数。不自动写入 payload"),
        ("payload", "只含调用时传入的 payload 对象。recordStructuredMetric 会把 playerId、pondId、correlationId 再抄进这份 JSON"),
        ("correlation_id", "若 payload.correlationId 是字符串，复制到本列；JSON 里仍保留该键"),
        ("dedup_key", "仅 catch_accept、pending_catch_accept，且 payload.eventId 为非空字符串时写入：{event_type}:{eventId}。冲突则跳过"),
        ("created_at", "写入时的 Date.now()，Unix 毫秒"),
        ("相位短码", "f/t/c。f=-1 无来源；0 idle；1 seated；2 baiting；3 casting；4 waiting；5 hooked；6 resolving；7 stopping；8 disconnected；9 groundbaiting"),
        ("整行示例 bite_hook", '{"id":"<uuid>","event_type":"bite_hook","player_id":"p1","pond_id":"pond-calm","payload":"{\\"speciesId\\":\\"crucian\\",\\"quality\\":\\"green\\",\\"sessionHooks\\":1,\\"sessionEscapes\\":0,\\"sessionMissTicks\\":4,\\"missTicksSinceLastHook\\":4,\\"waitingMsSinceLastHook\\":180000}","correlation_id":null,"dedup_key":null,"created_at":1710000000000}'),
        ("整行示例 fishing_phase_transition", '{"id":"<uuid>","event_type":"fishing_phase_transition","player_id":"p1","pond_id":"pond-calm","payload":"{\\"f\\":4,\\"t\\":5,\\"c\\":\\"bite_hook\\"}","correlation_id":null,"dedup_key":null,"created_at":1710000000000}'),
        ("整行示例 join_pond_success", '{"id":"<uuid>","event_type":"join_pond_success","player_id":"p1","pond_id":"pond-calm","payload":"{\\"userId\\":\\"u1\\",\\"socketId\\":\\"sock-1\\",\\"spotId\\":null,\\"fishingPhase\\":\\"idle\\",\\"reason\\":\\"join_pond_success\\",\\"joinKind\\":\\"fresh\\",\\"playerId\\":\\"p1\\",\\"pondId\\":\\"pond-calm\\"}","correlation_id":null,"dedup_key":null,"created_at":1710000000000}'),
        ("整行示例 pending_catch_accept", '{"id":"<uuid>","event_type":"pending_catch_accept","player_id":"p1","pond_id":"pond-calm","payload":"{\\"speciesId\\":\\"crucian\\",\\"quality\\":\\"green\\",\\"sizeM\\":0.2,\\"eventId\\":\\"catch-1\\",\\"auto\\":false}","correlation_id":null,"dedup_key":"pending_catch_accept:catch-1","created_at":1710000000000}'),
        ("不进此表", "类型为「日志」「聚合日志」「指标+可选日志」的事件只打结构化日志或 Prometheus，不写 fishing_metrics。pond_ecology_catchup 在契约中保留字段，生产只打日志"),
    ]:
        ws_fmt.append(list(row))
    style_header(ws_fmt, 2)
    ws_fmt.column_dimensions["A"].width = 36
    ws_fmt.column_dimensions["B"].width = 120
    for row in ws_fmt.iter_rows(min_row=2, max_row=ws_fmt.max_row):
        for c in row:
            c.alignment = Alignment(vertical="top", wrap_text=True)

    # 慢测验证 sheet
    ws_v = wb.create_sheet("慢测验证")
    ws_v.append(["用例", "脚本", "状态", "说明"])
    ws_v.append(["server observability", "npm run verify:server-observability", "已实现", "join/phase/timeline"])
    ws_v.append(["pending timeout", "npx tsx scripts/verify-pending-timeout.ts", "可选", "慢测 pending_catch_expired"])
    ws_v.append(["afk diag", "npx tsx scripts/verify-afk-diag.ts", "已实现", "断线宽限"])
    style_header(ws_v, 4)

    wb.save(OUT)
    print(f"Wrote {OUT} ({len(wb.sheetnames)} sheets)")
    try:
        REPORTS.mkdir(parents=True, exist_ok=True)
        shutil.copy2(OUT, OUT_DOCS)
        print(f"Synced copy: {OUT_DOCS}")
    except OSError as e:
        print(f"WARN: could not sync docs copy: {e}")


if __name__ == "__main__":
    main()
