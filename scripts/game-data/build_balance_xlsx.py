#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Build root 钓鱼玩法固定数值表.xlsx (FEAT-PROG-01 balance pipeline)."""
from __future__ import annotations

from pathlib import Path

from openpyxl import Workbook
from openpyxl.comments import Comment
from openpyxl.styles import Alignment, Font, PatternFill
from openpyxl.utils import get_column_letter

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "钓鱼玩法固定数值表.xlsx"

HEADER_FILL = PatternFill("solid", fgColor="1F4E78")
HEADER_FONT = Font(bold=True, color="FFFFFF")

# ---------------------------------------------------------------------------
# Authoritative draft data (plan v0 / FEAT-PROG-01)
# ---------------------------------------------------------------------------

META_ROWS = [
    ("key", "value"),
    ("version", "1.0.0"),
    ("SIZE_EXP", 1.15),
    ("maxFeeChargesPerDayDefault", 4),
    ("schemaNote", "FEAT-PROG-01"),
]

PONDS_HEADER = [
    "pondId",
    "name",
    "pondCategory",
    "mapZoneId",
    "feePer2h",
    "maxFeeChargesPerDay",
    "unlock",
    "isOpen",
    "showOnWorldMap",
    "minPlayerLevel",
    "mapX",
    "mapY",
]

# (pondId, name, category, zone, fee, maxCharges, unlock, isOpen, showMap, minLv)
PONDS = [
    ("pond-calm", "静心湖", "advanced", "zone-advanced", 200, 4, "onboarding", True, True, 0),
    ("pond-mist", "云雾塘", "advanced", "zone-advanced", 200, 4, "onboarding", True, True, 0),
    ("pond-sunset", "夕阳湾", "advanced", "zone-advanced", 200, 4, "onboarding", True, True, 0),
    ("pond-bamboo", "竹林池", "advanced", "zone-advanced", 200, 4, "onboarding", True, True, 0),
    ("pond-reed", "芦苇荡", "advanced", "zone-advanced", 200, 4, "onboarding", True, True, 0),
    ("pond-crystal", "晶石潭", "veteran", "zone-veteran", 500, 4, "level:5", True, True, 5),
    ("pond-lotus", "荷香池", "veteran", "zone-veteran", 500, 4, "level:5", True, True, 5),
    ("pond-mirror", "镜面湖", "veteran", "zone-veteran", 500, 4, "level:5", True, True, 5),
    ("pond-willow", "柳荫湾", "veteran", "zone-veteran", 500, 4, "level:5", True, True, 5),
    ("pond-stone", "叠石矶", "wilderness", "zone-wilderness", 0, 0, "onboarding", True, True, 0),
    ("pond-spring", "清泉眼", "wilderness", "zone-wilderness", 0, 0, "onboarding", True, True, 0),
    ("pond-dusk", "暮色泊", "wilderness", "zone-wilderness", 0, 0, "onboarding", True, True, 0),
    ("pond-pine", "松风潭", "wilderness", "zone-wilderness", 0, 0, "onboarding", True, True, 0),
    ("pond-coral", "珊瑚浅", "reservoir", "zone-reservoir", 0, 0, "onboarding", True, True, 0),
    ("pond-moon", "月影池", "reservoir", "zone-reservoir", 0, 0, "onboarding", True, True, 0),
    ("pond-fern", "蕨影泽", "reservoir", "zone-reservoir", 0, 0, "onboarding", True, True, 0),
    ("pond-ridge", "岭下塘", "forbidden", "zone-forbidden", 0, 0, "onboarding", True, True, 0),
    ("pond-harbor", "渔港湾", "forbidden", "zone-forbidden", 0, 0, "onboarding", True, True, 0),
    ("pond-orchid", "兰汀", "forbidden", "zone-forbidden", 0, 0, "onboarding", True, True, 0),
    ("pond-frost", "霜华淀", "giant", "zone-giant", 2500, 4, "level:15", False, True, 15),
    ("pond-novice", "新手练习塘", "novice", "", 0, 0, "guide_only", True, False, 0),
]

PLAYER_LEVELS = [
    # level, xpToNext, pondXpPerHour, maxPondLevel
    (1, 100, 12, 1),
    (2, 150, 14, 2),
    (3, 220, 16, 3),
    (4, 300, 18, 4),
    (5, 400, 22, 5),
    (6, 520, 26, 5),
    (7, 650, 30, 6),
    (8, 800, 34, 6),
    (9, 980, 38, 7),
    (10, 1200, 42, 7),
    (11, 1450, 48, 8),
    (12, 1750, 54, 8),
    (13, 2100, 60, 9),
    (14, 2500, 66, 9),
    (15, 3000, 72, 10),
    (16, 3600, 80, 10),
    (17, 4300, 88, 10),
    (18, 5100, 96, 10),
    (19, 6000, 104, 10),
    (20, 0, 112, 10),
]

POND_LEVEL_XP = [80, 120, 180, 260, 360, 480, 620, 800, 1000, 0]

FISH_SPECIES = [
    # speciesId, name, diet, catchGroup, typicalMinM, typicalMaxM
    ("crucian", "鲫鱼", "herbivore", "still_bait", 0.03, 0.35),
    ("carp", "鲤鱼", "herbivore", "still_bait", 0.08, 0.9),
    ("tilapia", "罗非鱼", "herbivore", "still_bait", 0.1, 0.5),
    ("koi", "锦鲤", "omnivore", "still_bait", 0.15, 0.8),
    ("perch", "河鲈", "omnivore", "stream_light", 0.08, 0.45),
    ("herring", "鲱鱼", "omnivore", "stream_light", 0.08, 0.4),
    ("mackerel", "鲭鱼", "omnivore", "stream_light", 0.12, 0.55),
    ("bass", "大口黑鲈", "carnivore", "lure_predator", 0.15, 0.75),
    ("topmouth", "翘嘴", "carnivore", "lure_predator", 0.2, 1.0),
    ("pike", "狗鱼", "carnivore", "lure_predator", 0.25, 1.0),
    ("trout", "鳟鱼", "carnivore", "lure_predator", 0.12, 0.65),
    ("mandarin", "桂鱼", "carnivore", "lure_predator", 0.15, 0.7),
    ("catfish", "鲶鱼", "omnivore", "cast_heavy", 0.2, 1.2),
    ("eel", "鳗鱼", "carnivore", "cast_heavy", 0.2, 1.0),
    ("cod", "鳕鱼", "omnivore", "cast_heavy", 0.2, 1.1),
    ("snapper", "鲷鱼", "omnivore", "cast_heavy", 0.15, 0.85),
    ("salmon", "三文鱼", "carnivore", "cast_heavy", 0.4, 1.8),
    ("tuna", "黄鳍金枪鱼", "carnivore", "cast_heavy", 0.5, 2.3),
    ("marlin", "蓝旗鱼", "carnivore", "giant_game", 1.0, 5.0),
    ("sturgeon", "鲟鱼", "carnivore", "giant_game", 0.8, 3.5),
]

XP_QUALITY_BASE = {
    "gray": 12,
    "green": 20,
    "blue": 35,
    "purple": 55,
    "red": 90,
    "orange": 140,
    "gold": 220,
}
CATCH_GROUP_COEFF = {
    "still_bait": 0.9,
    "stream_light": 0.85,
    "lure_predator": 1.1,
    "cast_heavy": 1.25,
    "giant_game": 1.6,
}
QUALITIES = list(XP_QUALITY_BASE.keys())

FISH_SELL_QUALITY = [
    # quality, QUALITY_BASE, SIZE_REF, MIN_SELL
    ("gray", 80, 0.20, 40),
    ("green", 160, 0.35, 80),
    ("blue", 360, 0.60, 160),
    ("purple", 900, 1.00, 400),
    ("red", 2200, 1.80, 900),
    ("orange", 5500, 3.00, 2200),
    ("gold", 14000, 5.00, 6000),
]
SPECIES_MULT = [
    ("still_bait", 1.0),
    ("stream_light", 0.95),
    ("lure_predator", 1.05),
    ("cast_heavy", 1.10),
    ("giant_game", 1.25),
]

POND_MODIFIERS = [
    # category, biteRateMul, escapeRateMul, infoRevealMul, qualityWeightSkew, sizeCapMul, pondXpMul,
    # fineChancePerHour, fineGold, policeWarningMs
    ("advanced", 1, 1, 1, 1, 1, 1, 0, 0, 0),
    ("novice", 1, 1, 1, 1, 1, 1, 0, 0, 0),
    ("veteran", 1, 1, 1, 1, 1, 1, 0, 0, 0),
    ("wilderness", 0.75, 1.10, 0.70, 0.85, 0.95, 1.10, 0, 0, 0),
    ("reservoir", 0.65, 1.0, 0.55, 0.85, 1.0, 1.15, 0, 0, 0),
    ("forbidden", 0.90, 1, 1, 1, 1, 1, 0.15, 800, 10000),
    ("giant", 1, 1, 1, 1, 1, 1, 0, 0, 0),
]

# rods: fitStillBait / fitStreamLight / fitLurePredator / fitCastHeavy / fitGiantGame
# + quality fits (weak ramp by tier)
RODS = [
    # rodId, name, subType, priceGold, biteBonus, escapeReduction, breakSizeM, breakMaxLandings,
    # fitGray..fitGold, fitStill, fitStream, fitLure, fitCast, fitGiant
    (
        "rod-bamboo", "竹制手竿", "手竿入门", 0, 0, 0, 0.45, 3,
        1.1, 1.05, 1.0, 0.7, 0.5, 0.4, 0.3,
        1.0, 0.7, 0.7, 0.7, 0.7,
    ),
    (
        "rod-tai", "台钓碳素竿", "台钓", 2500, 0.03, 0.04, 0.8, 5,
        1.05, 1.05, 1.05, 0.9, 0.7, 0.55, 0.4,
        1.15, 0.75, 0.75, 0.75, 0.7,
    ),
    (
        "rod-stream", "溪流竿", "溪流", 3200, 0.04, 0.03, 0.55, 4,
        1.05, 1.05, 1.0, 0.85, 0.65, 0.5, 0.35,
        0.75, 1.2, 0.8, 0.75, 0.7,
    ),
    (
        "rod-iso", "矶钓竿", "矶钓", 5500, 0.03, 0.06, 1.2, 5,
        0.95, 1.0, 1.05, 1.0, 0.85, 0.7, 0.55,
        0.75, 0.8, 0.85, 1.1, 0.75,
    ),
    (
        "rod-surf", "海竿", "抛竿", 4800, 0.02, 0.07, 1.5, 6,
        0.9, 1.0, 1.05, 1.05, 0.9, 0.75, 0.6,
        0.7, 0.75, 0.8, 1.15, 0.8,
    ),
    (
        "rod-lure", "路亚竿", "路亚", 9000, 0.05, 0.05, 1.0, 5,
        0.85, 0.95, 1.05, 1.1, 0.95, 0.8, 0.65,
        0.7, 0.8, 1.2, 0.9, 0.75,
    ),
    (
        "rod-heavy", "重型路亚竿", "重路亚", 22000, 0.04, 0.10, 2.5, 8,
        0.75, 0.85, 0.95, 1.1, 1.15, 1.05, 0.9,
        0.7, 0.75, 1.1, 1.1, 0.9,
    ),
    (
        "rod-giant", "巨物竿", "巨物", 45000, 0.03, 0.12, 4.0, 10,
        0.7, 0.8, 0.9, 1.05, 1.15, 1.2, 1.25,
        0.7, 0.7, 0.85, 0.95, 1.25,
    ),
]

BAITS = [
    # baitId, name, diet, unlockPlayerLevel, costGoldPerUse,
    # biteBonusHerbivore, biteBonusOmnivore, biteBonusCarnivore, isDefaultInfinite
    ("bait-basic", "基础杂饵", "any", 0, 0, 0, 0, 0, True),
    ("bait-veg", "谷物饵", "herbivore", 2, 15, 0.06, 0.01, 0.01, False),
    ("bait-mix", "腥香饵", "omnivore", 3, 20, 0.01, 0.06, 0.01, False),
    ("bait-meat", "荤腥饵", "carnivore", 4, 25, 0.01, 0.01, 0.07, False),
]

VESSELS = [
    ("vessel-raft", "筏钓日票", 12, 15000, 8, False),
    ("vessel-boat", "路亚小艇", 14, 35000, 12, False),
    ("vessel-trawler", "捕捞船", 18, 80000, 20, False),
]

# Advanced calm stock template (STOCK_TEMPLATES[0] → pond-calm)
POND_FISH_POOL = [
    ("pond-calm", "crucian", "common", 1.0, True),
    ("pond-calm", "carp", "common", 1.0, True),
    ("pond-calm", "tilapia", "common", 1.0, True),
    ("pond-calm", "perch", "common", 1.0, True),
    ("pond-calm", "mandarin", "rare", 0.1, True),
    ("pond-calm", "trout", "rare", 0.1, True),
]

SPOT_CLUES = [
    ("pond-calm", "spot-1", 1, 1, "这片水很清澈"),
    ("pond-calm", "spot-2", 3, 2, "这边水草很多"),
]


# sheet, field, 中文名, 说明 — exported JSON still uses English keys in row 1.
FIELD_DOCS: list[tuple[str, str, str, str]] = [
    ("_meta", "key", "键", "全局常量名，如 SIZE_EXP、version。"),
    ("_meta", "value", "值", "对应常量的数值或备注。"),
    ("ponds", "pondId", "鱼塘ID", "程序主键，进塘/扣费/地图都用这个。"),
    ("ponds", "name", "鱼塘名", "中文显示名。"),
    ("ponds", "pondCategory", "鱼塘分级", "novice/advanced/veteran/wilderness/reservoir/forbidden/giant。"),
    ("ponds", "mapZoneId", "地图分区", "世界地图分区 ID。"),
    ("ponds", "feePer2h", "每2小时入场费", "金币；0 表示不收费。"),
    ("ponds", "maxFeeChargesPerDay", "每日扣费次数上限", "按 2 小时切片累计。"),
    ("ponds", "unlock", "解锁条件", "onboarding / level:N / guide_only。"),
    ("ponds", "isOpen", "是否开放", "FALSE 的塘不能进（如巨物塘壳）。"),
    ("ponds", "showOnWorldMap", "是否上地图", "FALSE 则世界地图不画（新手练习塘）。"),
    ("ponds", "minPlayerLevel", "最低钓鱼等级", "低于此等级不能进塘。"),
    ("ponds", "mapX", "地图X", "世界地图坐标 0~1。"),
    ("ponds", "mapY", "地图Y", "世界地图坐标 0~1。"),
    ("player_levels", "level", "钓鱼等级", "1~20。"),
    ("player_levels", "xpToNext", "升到下级所需经验", "20 级为 0。"),
    ("player_levels", "pondXpPerHour", "每小时鱼塘熟练度", "挂机时长折算塘经验的参考。"),
    ("player_levels", "maxPondLevel", "可达到的最高塘等级", "玩家等级对塘升级的软上限。"),
    ("pond_levels", "level", "鱼塘等级", "1~10。"),
    ("pond_levels", "xpToNext", "升到下级所需塘经验", "10 级为 0。"),
    ("fish_species", "speciesId", "鱼种ID", "程序主键。"),
    ("fish_species", "name", "中文名", "商店/图鉴显示。"),
    ("fish_species", "diet", "食性", "herbivore 草食 / omnivore 杂食 / carnivore 肉食。"),
    ("fish_species", "catchGroup", "钓组", "still_bait 静水底钓 / stream_light 溪流轻口 / lure_predator 路亚掠食 / cast_heavy 重抛 / giant_game 巨物。"),
    ("fish_species", "typicalMinM", "典型最小体长m", "展示参考，不是硬帽。"),
    ("fish_species", "typicalMaxM", "典型最大体长m", "展示参考，不是硬帽。"),
    ("fish_xp", "speciesId", "鱼种ID", "与 fish_species 对齐。"),
    ("fish_xp", "speciesName", "中文名", "便于策划阅读。"),
    ("fish_xp", "quality", "品质", "gray/green/blue/purple/red/orange/gold。"),
    ("fish_xp", "playerXp", "玩家经验", "钓上该品质该种鱼给玩家的经验。"),
    ("fish_xp", "pondXp", "鱼塘经验", "同时给当前塘熟练度的经验。"),
    ("fish_sell", "quality", "品质", "卖价品质底表；空行表示下面是钓组系数。"),
    ("fish_sell", "QUALITY_BASE", "品质底价", "卖价公式底数。"),
    ("fish_sell", "SIZE_REF", "体长参考m", "卖价公式尺寸归一化。"),
    ("fish_sell", "MIN_SELL", "最低卖价", "向下取整后的保底。"),
    ("fish_sell", "catchGroup", "钓组", "SPECIES_MULT 所对应的钓组。"),
    ("fish_sell", "SPECIES_MULT", "钓组卖价系数", "乘在品质底价上。"),
    ("pond_modifiers", "category", "鱼塘分级", "与 ponds.pondCategory 对应。"),
    ("pond_modifiers", "biteRateMul", "咬钩倍率", "塘级对咬钩率的乘区。"),
    ("pond_modifiers", "escapeRateMul", "脱钩倍率", "塘级对脱钩率的乘区。"),
    ("pond_modifiers", "infoRevealMul", "信息揭示倍率", "线索/信息量。"),
    ("pond_modifiers", "qualityWeightSkew", "品质权重倾斜", ">1 偏向高品质。"),
    ("pond_modifiers", "sizeCapMul", "体长上限倍率", "乘在鱼种尺寸帽上。"),
    ("pond_modifiers", "pondXpMul", "塘经验倍率", "该分级塘熟练度获取。"),
    ("pond_modifiers", "fineChancePerHour", "每小时出警概率", "仅 forbidden 使用，初值 0.15。其它分级填 0。"),
    ("pond_modifiers", "fineGold", "超时罚款金币", "不足则归零。仅 forbidden 使用，初值 800。"),
    ("pond_modifiers", "policeWarningMs", "出警时限毫秒", "时限内离塘免罚。仅 forbidden 使用，初值 10000。"),
    ("pond_fish_pool", "pondId", "鱼塘ID", "该塘可出的鱼。"),
    ("pond_fish_pool", "speciesId", "鱼种ID", "库存模板条目。"),
    ("pond_fish_pool", "role", "角色", "common 常驻 / rare 稀有。"),
    ("pond_fish_pool", "spawnWeight", "刷新权重", "相对权重。"),
    ("pond_fish_pool", "enabled", "是否启用", "FALSE 则该条不参与刷新。"),
    ("pond_quality_cap", "pondId", "鱼塘ID", "品质软帽。"),
    ("pond_quality_cap", "minQuality", "最低品质", "该塘随机品质下限。"),
    ("pond_quality_cap", "maxQuality", "最高品质", "该塘随机品质上限。"),
    ("pond_quality_cap", "notes", "备注", "策划说明，不进规则。"),
    ("pond_fish_size_cap", "pondId", "鱼塘ID", "体长硬帽。"),
    ("pond_fish_size_cap", "speciesId", "鱼种ID", "该塘该种鱼。"),
    ("pond_fish_size_cap", "quality", "品质", "按品质分档的尺寸帽。"),
    ("pond_fish_size_cap", "minSizeM", "最小体长m", "随机体长下限。"),
    ("pond_fish_size_cap", "maxSizeM", "最大体长m", "随机体长上限。"),
    ("rods", "rodId", "钓竿ID", "程序主键，商店与装备用。"),
    ("rods", "name", "中文名", "商店显示名。"),
    ("rods", "subType", "竿型", "手竿入门/台钓/溪流/矶钓/路亚/海竿/重路亚/巨物。"),
    ("rods", "priceGold", "金币价格", "0 表示新手赠送，商店不可回购。"),
    ("rods", "biteBonus", "咬钩加成", "加在咬钩判定上的弱加成，如 0.03 = +3%。"),
    ("rods", "escapeReduction", "防脱", "降低脱钩率的弱减免，如 0.04 = -4%。"),
    ("rods", "breakSizeM", "超规格体长m", "成功钓上大于该体长的鱼计入超规格。"),
    ("rods", "breakMaxLandings", "超规格成功上限", "累计满 N 次后当前装备竿销毁，不能修理。"),
    ("rods", "fitGray", "灰品质适配", "乘区；>1 更合适，<1 不合适。"),
    ("rods", "fitGreen", "绿品质适配", "乘区。"),
    ("rods", "fitBlue", "蓝品质适配", "乘区。"),
    ("rods", "fitPurple", "紫品质适配", "乘区。"),
    ("rods", "fitRed", "红品质适配", "乘区。"),
    ("rods", "fitOrange", "橙品质适配", "乘区。"),
    ("rods", "fitGold", "金品质适配", "乘区。"),
    ("rods", "fitStillBait", "静水底钓适配", "对应 catchGroup=still_bait。"),
    ("rods", "fitStreamLight", "溪流轻口适配", "对应 catchGroup=stream_light。"),
    ("rods", "fitLurePredator", "路亚掠食适配", "对应 catchGroup=lure_predator。"),
    ("rods", "fitCastHeavy", "重抛适配", "对应 catchGroup=cast_heavy。"),
    ("rods", "fitGiantGame", "巨物适配", "对应 catchGroup=giant_game。"),
    ("baits", "baitId", "鱼饵ID", "程序主键。"),
    ("baits", "name", "中文名", "商店显示名。"),
    ("baits", "diet", "对口食性", "any 通用 / herbivore 草食 / omnivore 杂食 / carnivore 肉食。"),
    ("baits", "unlockPlayerLevel", "解锁钓鱼等级", "0 表示开局可用。"),
    ("baits", "costGoldPerUse", "每次扣金", "咬钩成功时扣除；0 表示不扣金。不进货、无库存。"),
    ("baits", "biteBonusHerbivore", "对草食咬钩加成", "如 0.06 = +6%。"),
    ("baits", "biteBonusOmnivore", "对杂食咬钩加成", "如 0.06 = +6%。"),
    ("baits", "biteBonusCarnivore", "对肉食咬钩加成", "如 0.07 = +7%。"),
    ("baits", "isDefaultInfinite", "是否无限基础饵", "TRUE 的饵永不短缺，也不扣金。"),
    ("vessels", "vesselId", "船具ID", "程序主键。"),
    ("vessels", "name", "中文名", "商店显示名。"),
    ("vessels", "unlockPlayerLevel", "解锁钓鱼等级", "低于此等级不能买。"),
    ("vessels", "priceGold", "金币价格", "一次性购入。"),
    ("vessels", "placeholderCatchCount", "占位捕捞次数", "表内预留，当前不生效。"),
    ("vessels", "enabledUse", "是否允许使用", "FALSE：可买但不可装备、不可开船。"),
    ("spot_clues", "pondId", "鱼塘ID", "线索所属塘。"),
    ("spot_clues", "spotId", "钓点ID", "点位主键。"),
    ("spot_clues", "minPlayerLevel", "最低钓鱼等级", "未达到不显示该线索。"),
    ("spot_clues", "minPondLevel", "最低塘等级", "未达到不显示该线索。"),
    ("spot_clues", "clueText", "线索文案", "悬停/靠近时显示的中文。"),
]


def style_header(ws) -> None:
    for cell in ws[1]:
        cell.font = HEADER_FONT
        cell.fill = HEADER_FILL
        cell.alignment = Alignment(horizontal="center", vertical="center", wrap_text=True)


def autosize(ws, max_width: int = 28) -> None:
    for idx, col in enumerate(ws.columns, start=1):
        width = 10
        for cell in col:
            if cell.value is not None:
                width = max(width, min(max_width, len(str(cell.value)) + 2))
        ws.column_dimensions[get_column_letter(idx)].width = width


def write_sheet(wb: Workbook, title: str, headers: list[str], rows: list[tuple]) -> None:
    ws = wb.create_sheet(title)
    ws.append(headers)
    zh_row = [
        next((zh for sheet, field, zh, _desc in FIELD_DOCS if sheet == title and field == h), "")
        for h in headers
    ]
    if any(zh_row):
        ws.append(zh_row)
        for cell in ws[2]:
            cell.font = Font(italic=True, color="1F4E78")
            cell.alignment = Alignment(wrap_text=True, vertical="center")
    comments_map = {
        field: (zh, desc)
        for sheet, field, zh, desc in FIELD_DOCS
        if sheet == title
    }
    for idx, header in enumerate(headers, start=1):
        meta = comments_map.get(header)
        if not meta:
            continue
        zh, desc = meta
        ws.cell(1, idx).comment = Comment(f"{zh}\n{desc}".strip(), "策划")
    for row in rows:
        ws.append(list(row))
    style_header(ws)
    autosize(ws)


def write_field_docs(wb: Workbook) -> None:
    ws = wb.create_sheet("字段说明", 0)
    ws.append(["表名", "字段名", "中文名", "说明"])
    for row in FIELD_DOCS:
        ws.append(list(row))
    style_header(ws)
    autosize(ws, 56)
    ws.column_dimensions["D"].width = 64
    ws.freeze_panes = "A2"


def build() -> Path:
    wb = Workbook()
    # Drop the empty default sheet so the first append is not preceded by a blank row.
    default = wb.active
    wb.remove(default)

    write_field_docs(wb)
    write_sheet(wb, "_meta", list(META_ROWS[0]), META_ROWS[1:])

    pond_rows = [(*p, 0, 0) for p in PONDS]
    write_sheet(wb, "ponds", PONDS_HEADER, pond_rows)

    write_sheet(
        wb,
        "player_levels",
        ["level", "xpToNext", "pondXpPerHour", "maxPondLevel"],
        PLAYER_LEVELS,
    )

    write_sheet(
        wb,
        "pond_levels",
        ["level", "xpToNext"],
        [(i + 1, xp) for i, xp in enumerate(POND_LEVEL_XP)],
    )

    write_sheet(
        wb,
        "fish_species",
        ["speciesId", "name", "diet", "catchGroup", "typicalMinM", "typicalMaxM"],
        FISH_SPECIES,
    )

    fish_xp_rows = []
    for sid, sname, _diet, group, _mn, _mx in FISH_SPECIES:
        coeff = CATCH_GROUP_COEFF[group]
        for q in QUALITIES:
            player_xp = round(XP_QUALITY_BASE[q] * coeff)
            pond_xp = round(player_xp * 0.5)
            fish_xp_rows.append((sid, sname, q, player_xp, pond_xp))
    write_sheet(
        wb,
        "fish_xp",
        ["speciesId", "speciesName", "quality", "playerXp", "pondXp"],
        fish_xp_rows,
    )

    # quality rows + SPECIES_MULT rows (same sheet; filter by filled columns)
    sell_headers = [
        "quality",
        "QUALITY_BASE",
        "SIZE_REF",
        "MIN_SELL",
        "catchGroup",
        "SPECIES_MULT",
    ]
    sell_rows = [(q, base, ref, mn, "", "") for q, base, ref, mn in FISH_SELL_QUALITY]
    sell_rows += [("", "", "", "", g, m) for g, m in SPECIES_MULT]
    write_sheet(wb, "fish_sell", sell_headers, sell_rows)

    write_sheet(
        wb,
        "pond_modifiers",
        [
            "category",
            "biteRateMul",
            "escapeRateMul",
            "infoRevealMul",
            "qualityWeightSkew",
            "sizeCapMul",
            "pondXpMul",
            "fineChancePerHour",
            "fineGold",
            "policeWarningMs",
        ],
        POND_MODIFIERS,
    )

    write_sheet(
        wb,
        "pond_fish_pool",
        ["pondId", "speciesId", "role", "spawnWeight", "enabled"],
        POND_FISH_POOL,
    )

    write_sheet(
        wb,
        "pond_quality_cap",
        ["pondId", "minQuality", "maxQuality", "notes"],
        [
            ("pond-calm", "gray", "gold", "stub: advanced calm full range"),
            ("pond-novice", "gray", "green", "stub: novice soft cap"),
        ],
    )

    write_sheet(
        wb,
        "pond_fish_size_cap",
        ["pondId", "speciesId", "quality", "minSizeM", "maxSizeM"],
        [
            ("pond-calm", "crucian", "gray", 0.03, 0.25),
            ("pond-calm", "carp", "green", 0.08, 0.6),
        ],
    )

    rod_headers = [
        "rodId",
        "name",
        "subType",
        "priceGold",
        "biteBonus",
        "escapeReduction",
        "breakSizeM",
        "breakMaxLandings",
        "fitGray",
        "fitGreen",
        "fitBlue",
        "fitPurple",
        "fitRed",
        "fitOrange",
        "fitGold",
        "fitStillBait",
        "fitStreamLight",
        "fitLurePredator",
        "fitCastHeavy",
        "fitGiantGame",
    ]
    write_sheet(wb, "rods", rod_headers, RODS)

    write_sheet(
        wb,
        "baits",
        [
            "baitId",
            "name",
            "diet",
            "unlockPlayerLevel",
            "costGoldPerUse",
            "biteBonusHerbivore",
            "biteBonusOmnivore",
            "biteBonusCarnivore",
            "isDefaultInfinite",
        ],
        BAITS,
    )

    write_sheet(
        wb,
        "vessels",
        [
            "vesselId",
            "name",
            "unlockPlayerLevel",
            "priceGold",
            "placeholderCatchCount",
            "enabledUse",
        ],
        VESSELS,
    )

    write_sheet(
        wb,
        "spot_clues",
        ["pondId", "spotId", "minPlayerLevel", "minPondLevel", "clueText"],
        SPOT_CLUES,
    )

    wb.save(OUT)
    print(f"Wrote {OUT}")
    print(f"Sheets: {', '.join(wb.sheetnames)}")
    return OUT


def main() -> None:
    build()


if __name__ == "__main__":
    main()
