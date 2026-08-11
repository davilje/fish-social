/**
 * FISH-BOT-1：清理历史 bot 账号与明细，重建 100 池。
 * 硬约束：不改写 docs/analytics/**、不 UPDATE daily_* 看板聚合表。
 *
 * 用法：
 *   node scripts/ops/cleanup-bot-pool.mjs --dry-run
 *   node scripts/ops/cleanup-bot-pool.mjs          # 需停服或可接受锁
 */
import Database from 'better-sqlite3';
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(__dirname, '../..');
const dbPath = process.env.DB_PATH ?? path.join(root, 'data', 'fish-social.db');
const dryRun = process.argv.includes('--dry-run');
const BOT_POOL_SIZE = 100;
const BOT_POOL_PREFIX = 'bot-pool-';

/** 与 bots.ts BOT_NAMES 保持一致（脚本独立运行，不 import server） */
const BOT_NAMES = [
  '湖边小王', '静默钓手', '晨雾渔人', '夕阳老张', '竹林隐者',
  '微风钓客', '江湖渔翁', '浅水阿杰', '远投高手', '夜钓达人',
  '悠闲钓妹', '湖心居士', '云雾行者', '甩竿少年', '静待鱼儿',
  '碧波钓者', '芦苇边上', '细雨抛竿', '月下水影', '青石钓台',
  '南岸闲人', '北堤渔歌', '东亭小满', '西湾听涛', '柳梢钓客',
  '荷塘夜语', '溪口老李', '沙洲短笛', '烟波客', '渔火一点',
  '晚风收竿', '朝露入水', '浪花少年', '青笠钓叟', '蒲扇渔夫',
  '棠梨渡', '芙蓉汀', '白鹭矶', '鲤跃门', '一竿秋水',
  '半日闲钓', '满船清梦', '斜阳独钓', '疏星河岸', '薄雾船头',
  '小桥边钓', '古渡渔歌', '芦花浅水', '苔痕石矶', '青竹为竿',
  '丝线随风', '浮漂轻点', '提竿见月', '落水有声', '静坐听波',
  '淡水清欢', '河边小鱼', '塘口阿青', '湾里阿宁', '矶石上人',
  '晓风钓友', '暮色行舟', '雪后独钓', '夏夜追鲤', '春水初生',
  '秋湖望远', '冬堤温茶', '云起时', '雨歇后', '风定浪平',
  '回湾小憩', '浅滩寻踪', '深潭守候', '急流试竿', '缓水漫钓',
  '星湖钓影', '月湾渔火', '枫林临水', '桐荫钓台', '梅坞小驻',
  '菊径临塘', '兰汀抛丝', '竹篱外钓', '茅亭一角', '石埠钓踪',
  '渡口闲聊', '渔村阿成', '船家小妹', '堤上阿福', '水边阿珍',
  '青坪钓客', '碧潭客', '银浪手', '金鳞客', '玉珠浮漂',
  '翠羽湖', '琥珀湾', '琉璃汀', '翡翠矶', '玄青水',
  '淡墨河', '素简钓', '清音渡', '怀远客', '归雁汀',
];

function botPoolPlayerId(i0) {
  return `${BOT_POOL_PREFIX}${String(i0 + 1).padStart(3, '0')}`;
}

function hasTable(db, name) {
  return !!db.prepare(`SELECT name FROM sqlite_master WHERE type='table' AND name=?`).get(name);
}

function count(db, sql, ...params) {
  return db.prepare(sql).get(...params)?.c ?? 0;
}

if (!fs.existsSync(dbPath)) {
  console.error('[cleanup-bot-pool] missing DB', dbPath);
  process.exit(1);
}

if (BOT_NAMES.length < BOT_POOL_SIZE) {
  console.error(`[cleanup-bot-pool] BOT_NAMES=${BOT_NAMES.length} < ${BOT_POOL_SIZE}`);
  process.exit(1);
}

const db = new Database(dbPath);
db.pragma('busy_timeout = 30000');
db.pragma('foreign_keys = ON');

const beforeBots = count(db, `SELECT COUNT(*) AS c FROM players WHERE player_id LIKE 'bot-%'`);
const beforeMetrics = count(db, `SELECT COUNT(*) AS c FROM fishing_metrics WHERE player_id LIKE 'bot-%'`);
const beforeInv = hasTable(db, 'inventory')
  ? count(db, `SELECT COUNT(*) AS c FROM inventory WHERE player_id LIKE 'bot-%'`)
  : 0;

console.log(`[cleanup-bot-pool] db=${dbPath}`);
console.log(`[cleanup-bot-pool] before: players(bot)=${beforeBots} metrics=${beforeMetrics} inventory=${beforeInv}`);
console.log(`[cleanup-bot-pool] mode=${dryRun ? 'DRY-RUN' : 'APPLY'} · will NOT touch analytics / daily_* aggregates`);

if (dryRun) {
  console.log(`[cleanup-bot-pool] would DELETE all bot-% detail rows then ensure ${BOT_POOL_SIZE} ${BOT_POOL_PREFIX}*`);
  db.close();
  process.exit(0);
}

const del = (sql) => db.prepare(sql).run().changes;

const tx = db.transaction(() => {
  const changes = {};

  // 明细（可关联 bot）
  if (hasTable(db, 'fishing_metrics')) {
    changes.fishing_metrics = del(`DELETE FROM fishing_metrics WHERE player_id LIKE 'bot-%'`);
  }
  if (hasTable(db, 'inventory')) {
    changes.inventory = del(`DELETE FROM inventory WHERE player_id LIKE 'bot-%'`);
  }
  if (hasTable(db, 'daily_fishing')) {
    changes.daily_fishing = del(`DELETE FROM daily_fishing WHERE user_id LIKE 'bot-%'`);
  }
  if (hasTable(db, 'fish_codex')) {
    changes.fish_codex = del(`DELETE FROM fish_codex WHERE player_id LIKE 'bot-%'`);
  }
  if (hasTable(db, 'player_gear')) {
    changes.player_gear = del(`DELETE FROM player_gear WHERE player_id LIKE 'bot-%'`);
  }
  if (hasTable(db, 'player_pond_session')) {
    changes.player_pond_session = del(`DELETE FROM player_pond_session WHERE player_id LIKE 'bot-%'`);
  }
  if (hasTable(db, 'pending_catch_locks')) {
    // user_id 未必是 bot- 前缀；按 player_id 列若存在再清
    const cols = db.prepare(`PRAGMA table_info(pending_catch_locks)`).all().map((c) => c.name);
    if (cols.includes('player_id')) {
      changes.pending_catch_locks = del(`DELETE FROM pending_catch_locks WHERE player_id LIKE 'bot-%'`);
    }
  }
  if (hasTable(db, 'social_posts')) {
    if (hasTable(db, 'post_likes')) {
      changes.post_likes = del(
        `DELETE FROM post_likes WHERE post_id IN (SELECT id FROM social_posts WHERE player_id LIKE 'bot-%')
         OR player_id LIKE 'bot-%'`,
      );
    }
    if (hasTable(db, 'post_comments')) {
      changes.post_comments = del(
        `DELETE FROM post_comments WHERE post_id IN (SELECT id FROM social_posts WHERE player_id LIKE 'bot-%')
         OR player_id LIKE 'bot-%'`,
      );
    }
    changes.social_posts = del(`DELETE FROM social_posts WHERE player_id LIKE 'bot-%'`);
  }
  if (hasTable(db, 'friend_links')) {
    changes.friend_links = del(
      `DELETE FROM friend_links WHERE player_id LIKE 'bot-%' OR friend_id LIKE 'bot-%'`,
    );
  }
  if (hasTable(db, 'friend_requests')) {
    changes.friend_requests = del(
      `DELETE FROM friend_requests WHERE from_player_id LIKE 'bot-%' OR to_player_id LIKE 'bot-%'`,
    );
  }
  if (hasTable(db, 'dm_messages')) {
    changes.dm_messages = del(
      `DELETE FROM dm_messages WHERE from_player_id LIKE 'bot-%' OR to_player_id LIKE 'bot-%'`,
    );
  }
  if (hasTable(db, 'dm_read_cursor')) {
    changes.dm_read_cursor = del(
      `DELETE FROM dm_read_cursor WHERE player_id LIKE 'bot-%' OR friend_player_id LIKE 'bot-%'`,
    );
  }

  // 账号本身
  changes.players = del(`DELETE FROM players WHERE player_id LIKE 'bot-%'`);

  // 重建 100 池
  const insert = db.prepare(
    `INSERT INTO players (player_id, nickname, coins, share_visibility, avatar_url, bio, showcase_fish_ids, created_at)
     VALUES (?, ?, 0, 'public', NULL, '', '[]', ?)`,
  );
  const now = Date.now();
  for (let i = 0; i < BOT_POOL_SIZE; i++) {
    insert.run(botPoolPlayerId(i), BOT_NAMES[i].slice(0, 12), now);
  }

  // 抬升默认 MAX_BOTS_PER_POND：仅当仍为旧 6
  if (hasTable(db, 'game_config')) {
    const row = db.prepare(`SELECT config_value FROM game_config WHERE config_key='MAX_BOTS_PER_POND'`).get();
    if (!row) {
      db.prepare(
        `INSERT INTO game_config (config_key, config_value, updated_at) VALUES ('MAX_BOTS_PER_POND', '20', ?)`,
      ).run(now);
    } else if (row.config_value === '6') {
      db.prepare(
        `UPDATE game_config SET config_value='20', updated_at=? WHERE config_key='MAX_BOTS_PER_POND'`,
      ).run(now);
    }
  }

  return changes;
});

const changes = tx();
const afterBots = count(db, `SELECT COUNT(*) AS c FROM players WHERE player_id LIKE 'bot-%'`);
const poolCount = count(db, `SELECT COUNT(*) AS c FROM players WHERE player_id LIKE 'bot-pool-%'`);
db.close();

console.log('[cleanup-bot-pool] deleted:', changes);
console.log(`[cleanup-bot-pool] after: bot players=${afterBots} pool=${poolCount}`);
console.log('[cleanup-bot-pool] DONE · analytics history left untouched');
