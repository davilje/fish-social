/** 每个鱼塘最多同时钓鱼人数 */
export const MAX_POND_USERS = 20;

/** 每人每天最多钓鱼时长（毫秒）= 8 小时 */
export const MAX_DAILY_FISHING_MS = 8 * 60 * 60 * 1000;

/** 世界地图尺寸（逻辑像素） */
export const WORLD_MAP_WIDTH = 800;
export const WORLD_MAP_HEIGHT = 600;

/** 单个鱼塘内部场景尺寸（斜 45° 正交视角） */
export const POND_SCENE_WIDTH = 400;
export const POND_SCENE_HEIGHT = 440;

/** 鱼塘聊天消息保留条数 */
export const MAX_CHAT_HISTORY = 100;

/** 钓鱼时随机上钩检测间隔（毫秒）— v0.3.1：每 300 秒（5 分钟）判定一次 */
export const FISH_BITE_CHECK_MS = 60_000;

/**
 * @deprecated A0 起使用指数咬钩模型（shared/fishing.ts），仅移动端演示模式保留
 */
export const FISH_BITE_CHANCE = 0.14;

/**
 * @deprecated A0 起脱钩由 calcEscapeRate 判定，仅移动端演示模式保留
 */
export const FISH_CATCH_SUCCESS_RATE = 0.7;

/** 鱼塘生态模拟间隔（毫秒） */
export const POND_ECOSYSTEM_TICK_MS = 30_000;

/** 钓点上钩权重刷新间隔 */
export const SPOT_BITE_WEIGHT_REFRESH_MS = 5 * 60 * 1000;

/** 钓鱼结果弹窗自动关闭时间（毫秒） */
export const FISHING_PROMPT_AUTO_CLOSE_MS = 5000;

/** 机器人最短停留时间（1 小时） */
export const BOT_MIN_STAY_MS = 60 * 60 * 1000;

/** 机器人最长停留时间（3 小时） */
export const BOT_MAX_STAY_MS = 3 * 60 * 60 * 1000;
