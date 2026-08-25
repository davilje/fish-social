# -*- coding: utf-8 -*-
"""FEAT-FISH-CN-01 authoritative species / habitat / pond rename / pool rows."""
from __future__ import annotations

# speciesId, name, diet, catchGroup, typicalMinM, typicalMaxM, rarityTier, nationwide
FISH_SPECIES_CN = [
    # nationwide (8)
    ("crucian", "鲫鱼", "herbivore", "still_bait", 0.03, 0.35, "common", True),
    ("carp", "鲤鱼", "herbivore", "still_bait", 0.08, 0.90, "common", True),
    ("grass_carp", "草鱼", "herbivore", "still_bait", 0.15, 1.20, "common", True),
    ("silver_carp", "鲢鱼", "omnivore", "still_bait", 0.20, 1.00, "common", True),
    ("bighead_carp", "鳙鱼", "omnivore", "still_bait", 0.25, 1.20, "common", True),
    ("black_carp", "青鱼", "omnivore", "cast_heavy", 0.30, 1.50, "uncommon", True),
    ("tilapia", "罗非鱼", "herbivore", "still_bait", 0.10, 0.50, "common", True),
    ("loach", "泥鳅", "omnivore", "still_bait", 0.08, 0.25, "common", True),
    # plain lakes (12)
    ("bream", "鳊鱼", "herbivore", "still_bait", 0.12, 0.50, "common", False),
    ("bluntnose_bream", "团头鲂", "herbivore", "still_bait", 0.15, 0.55, "common", False),
    ("white_amur_bream", "长春鳊", "herbivore", "still_bait", 0.12, 0.45, "common", False),
    ("mandarin", "鳜鱼", "carnivore", "lure_predator", 0.15, 0.70, "uncommon", False),
    ("topmouth", "翘嘴鲌", "carnivore", "lure_predator", 0.20, 1.00, "uncommon", False),
    ("black_bass", "大口黑鲈", "carnivore", "lure_predator", 0.15, 0.75, "uncommon", False),
    ("snakehead", "乌鳢", "carnivore", "lure_predator", 0.20, 0.90, "uncommon", False),
    ("catfish", "鲶鱼", "omnivore", "cast_heavy", 0.20, 1.20, "common", False),
    ("yellow_catfish", "黄颡鱼", "carnivore", "still_bait", 0.10, 0.40, "common", False),
    ("silver_bream", "银鲴", "omnivore", "stream_light", 0.10, 0.35, "common", False),
    ("redfin", "红鳍鲌", "carnivore", "lure_predator", 0.15, 0.60, "uncommon", False),
    ("river_chub", "马口鱼", "carnivore", "stream_light", 0.08, 0.25, "common", False),
    # river / reservoir (10)
    ("chinese_sabre", "鳡鱼", "carnivore", "lure_predator", 0.40, 1.50, "rare", False),
    ("redeye", "赤眼鳟", "omnivore", "still_bait", 0.15, 0.50, "uncommon", False),
    ("longsnout_cat", "长吻鮠", "carnivore", "cast_heavy", 0.25, 0.90, "rare", False),
    ("wuchang_bream", "武昌鱼", "herbivore", "still_bait", 0.15, 0.55, "uncommon", False),
    ("cn_mahseer", "光唇鱼", "omnivore", "stream_light", 0.15, 0.60, "uncommon", False),
    ("rock_carp", "岩原鲤", "omnivore", "still_bait", 0.20, 0.80, "rare", False),
    ("barbel", "花鲴", "omnivore", "stream_light", 0.12, 0.40, "uncommon", False),
    ("snow_trout", "裂腹鱼", "omnivore", "stream_light", 0.15, 0.50, "rare", False),
    ("golden_line", "金线鲃", "omnivore", "stream_light", 0.08, 0.25, "rare", False),
    ("bronze_carp", "铜鱼", "omnivore", "still_bait", 0.20, 0.70, "uncommon", False),
    # coastal (10)
    ("black_porgy", "黑鲷", "omnivore", "cast_heavy", 0.15, 0.50, "uncommon", False),
    ("red_porgy", "真鲷", "carnivore", "cast_heavy", 0.20, 0.70, "rare", False),
    ("yellow_croaker", "大黄鱼", "carnivore", "cast_heavy", 0.20, 0.60, "uncommon", False),
    ("hairtail", "带鱼", "carnivore", "cast_heavy", 0.40, 1.20, "uncommon", False),
    ("chinese_mackerel", "鲐鱼", "carnivore", "lure_predator", 0.20, 0.55, "common", False),
    ("flounder", "牙鲆", "carnivore", "cast_heavy", 0.20, 0.70, "uncommon", False),
    ("sea_bass", "花鲈", "carnivore", "lure_predator", 0.25, 0.80, "uncommon", False),
    ("pomfret", "银鲳", "omnivore", "cast_heavy", 0.15, 0.45, "uncommon", False),
    ("grouper", "石斑鱼", "carnivore", "giant_game", 0.30, 1.00, "rare", False),
    ("mullet", "鲻鱼", "omnivore", "still_bait", 0.20, 0.60, "common", False),
    # cold / rare / giant (10)
    ("rainbow_trout", "虹鳟", "carnivore", "lure_predator", 0.20, 0.70, "uncommon", False),
    ("lenok", "细鳞鲑", "carnivore", "lure_predator", 0.25, 0.70, "rare", False),
    ("taimen", "哲罗鲑", "carnivore", "giant_game", 0.50, 1.80, "legendary", False),
    ("whitefish", "雅罗鱼", "omnivore", "stream_light", 0.15, 0.45, "uncommon", False),
    ("chinese_sturgeon", "中华鲟", "omnivore", "giant_game", 0.80, 3.50, "legendary", False),
    ("dabry_sturgeon", "达氏鲟", "omnivore", "giant_game", 0.50, 2.00, "legendary", False),
    ("eel", "鳗鲡", "carnivore", "cast_heavy", 0.20, 1.00, "uncommon", False),
    ("ricefield_eel", "黄鳝", "carnivore", "still_bait", 0.20, 0.70, "common", False),
    ("koi", "锦鲤", "omnivore", "still_bait", 0.15, 0.80, "uncommon", False),
    ("giant_black_carp", "巨青", "omnivore", "giant_game", 0.80, 2.00, "rare", False),
]

# speciesId -> list of bioRegion
HABITAT_BY_SPECIES: dict[str, list[str]] = {
    "bream": ["east_plain_lake", "yangtze_mid"],
    "bluntnose_bream": ["yangtze_mid"],
    "white_amur_bream": ["east_plain_lake"],
    "mandarin": ["east_plain_lake", "yangtze_mid"],
    "topmouth": ["east_plain_lake", "yangtze_mid"],
    "black_bass": ["east_plain_lake", "south_reservoir"],
    "snakehead": ["yangtze_mid", "south_reservoir"],
    "catfish": ["east_plain_lake", "yangtze_mid"],
    "yellow_catfish": ["east_plain_lake", "yangtze_mid"],
    "silver_bream": ["yangtze_mid"],
    "redfin": ["yangtze_mid"],
    "river_chub": ["yangtze_mid", "south_reservoir"],
    "chinese_sabre": ["yangtze_mid"],
    "redeye": ["yangtze_mid", "south_reservoir"],
    "longsnout_cat": ["yangtze_mid"],
    "wuchang_bream": ["yangtze_mid"],
    "cn_mahseer": ["south_reservoir", "southwest_plateau"],
    "rock_carp": ["southwest_plateau"],
    "barbel": ["yangtze_mid"],
    "snow_trout": ["southwest_plateau"],
    "golden_line": ["southwest_plateau"],
    "bronze_carp": ["yangtze_mid"],
    "black_porgy": ["southeast_coast", "north_bohai"],
    "red_porgy": ["southeast_coast", "north_bohai"],
    "yellow_croaker": ["southeast_coast"],
    "hairtail": ["southeast_coast", "north_bohai"],
    "chinese_mackerel": ["southeast_coast", "north_bohai"],
    "flounder": ["north_bohai"],
    "sea_bass": ["southeast_coast", "north_bohai"],
    "pomfret": ["southeast_coast", "south_sea"],
    "grouper": ["south_sea", "southeast_coast"],
    "mullet": ["southeast_coast", "south_sea"],
    "rainbow_trout": ["northeast_cold", "southwest_plateau"],
    "lenok": ["northeast_cold"],
    "taimen": ["northeast_cold"],
    "whitefish": ["northeast_cold"],
    "chinese_sturgeon": ["yangtze_mid"],
    "dabry_sturgeon": ["yangtze_mid"],
    "eel": ["east_plain_lake", "southeast_coast"],
    "ricefield_eel": ["east_plain_lake", "yangtze_mid"],
    "koi": ["east_plain_lake"],
    "giant_black_carp": ["yangtze_mid"],
}

# pondId, name, category, zone, fee, maxCharges, unlock, isOpen, showMap, minLv,
# bioRegion, waterType, realWorldRef
PONDS_CN = [
    ("pond-calm", "千岛湖", "advanced", "zone-advanced", 200, 4, "onboarding", True, True, 0,
     "east_plain_lake", "reservoir", "浙江淳安"),
    ("pond-mist", "太湖", "advanced", "zone-advanced", 200, 4, "onboarding", True, True, 0,
     "east_plain_lake", "lake", "江苏无锡/苏州"),
    ("pond-sunset", "洪泽湖", "advanced", "zone-advanced", 200, 4, "onboarding", True, True, 0,
     "east_plain_lake", "lake", "江苏淮安"),
    ("pond-bamboo", "鄱阳湖", "advanced", "zone-advanced", 200, 4, "onboarding", True, True, 0,
     "yangtze_mid", "lake", "江西九江"),
    ("pond-reed", "洞庭湖", "advanced", "zone-advanced", 200, 4, "onboarding", True, True, 0,
     "yangtze_mid", "lake", "湖南岳阳"),
    ("pond-crystal", "滇池", "veteran", "zone-veteran", 500, 4, "level:5", True, True, 5,
     "southwest_plateau", "lake", "云南昆明"),
    ("pond-lotus", "洱海", "veteran", "zone-veteran", 500, 4, "level:5", True, True, 5,
     "southwest_plateau", "lake", "云南大理"),
    ("pond-mirror", "镜泊湖", "veteran", "zone-veteran", 500, 4, "level:5", True, True, 5,
     "northeast_cold", "lake", "黑龙江牡丹江"),
    ("pond-willow", "查干湖", "veteran", "zone-veteran", 500, 4, "level:5", True, True, 5,
     "northeast_cold", "lake", "吉林松原"),
    ("pond-stone", "万绿湖", "wilderness", "zone-wilderness", 0, 0, "onboarding", True, True, 0,
     "south_reservoir", "reservoir", "广东河源"),
    ("pond-spring", "北江", "wilderness", "zone-wilderness", 0, 0, "onboarding", True, True, 0,
     "south_reservoir", "river", "广东韶关段"),
    ("pond-dusk", "丹江口水库", "wilderness", "zone-wilderness", 0, 0, "onboarding", True, True, 0,
     "yangtze_mid", "reservoir", "湖北十堰"),
    ("pond-pine", "清江", "wilderness", "zone-wilderness", 0, 0, "onboarding", True, True, 0,
     "yangtze_mid", "river", "湖北恩施"),
    ("pond-coral", "舟山近海", "reservoir", "zone-reservoir", 0, 0, "onboarding", True, True, 0,
     "southeast_coast", "coastal", "浙江舟山"),
    ("pond-moon", "厦门湾", "reservoir", "zone-reservoir", 0, 0, "onboarding", True, True, 0,
     "southeast_coast", "estuary", "福建厦门"),
    ("pond-fern", "南澳近海", "reservoir", "zone-reservoir", 0, 0, "onboarding", True, True, 0,
     "southeast_coast", "coastal", "广东汕头"),
    ("pond-ridge", "长江故道野塘", "forbidden", "zone-forbidden", 0, 0, "onboarding", True, True, 0,
     "yangtze_mid", "river", "禁钓叙事"),
    ("pond-harbor", "青岛近海", "forbidden", "zone-forbidden", 0, 0, "onboarding", True, True, 0,
     "north_bohai", "coastal", "山东青岛"),
    ("pond-orchid", "涠洲近海", "forbidden", "zone-forbidden", 0, 0, "onboarding", True, True, 0,
     "south_sea", "coastal", "广西北海"),
    ("pond-frost", "兴凯湖", "giant", "zone-giant", 2500, 4, "level:15", False, True, 15,
     "northeast_cold", "lake", "黑龙江界湖"),
    ("pond-novice", "城郊练杆塘", "novice", "", 0, 0, "guide_only", True, False, 0,
     "east_plain_lake", "pond", "新手"),
]

DELETED_FOREIGN_IDS = {
    "tuna", "marlin", "salmon", "cod", "herring", "snapper", "mackerel", "pike",
    "sturgeon", "perch", "bass", "trout",
}

# rarityTier → qualityMax rank (1=gray … 7=gold); qualityMin always 1
QUALITY_MAX_BY_TIER = {
    "common": 4,      # blue
    "uncommon": 5,    # purple
    "rare": 6,        # orange
    "legendary": 7,   # gold
}

SPECIES_BASE_WEIGHT = {
    "common": 10,
    "uncommon": 6,
    "rare": 3,
    "legendary": 1,
}

ECOLOGY_BY_CATEGORY = {
    "novice": (40, 8, 24),
    "advanced": (80, 12, 48),
    "veteran": (70, 10, 42),
    "wilderness": (60, 8, 36),
    "reservoir": (65, 8, 38),
    "forbidden": (75, 10, 45),
    "giant": (50, 6, 28),
}

# Global quality weights (1:1 from FISH_QUALITIES) for POOL-01 category sheet
DEFAULT_QUALITY_WEIGHTS = [
    ("gray", 38), ("green", 28), ("blue", 18), ("purple", 9),
    ("red", 4), ("orange", 2), ("gold", 1),
]

CATEGORIES = [
    "novice", "advanced", "veteran", "wilderness", "reservoir", "forbidden", "giant",
]

FISH_QUALITY_STATS = [
    # quality, sizeCapM, biteBaseAtMaxSize, displayName, QUALITY_BASE, SIZE_REF, MIN_SELL
    ("gray", 0.3, 0.05, "普通", 80, 0.20, 40),
    ("green", 0.8, 0.032, "优良", 160, 0.35, 80),
    ("blue", 2.0, 0.018, "稀有", 360, 0.60, 160),
    ("purple", 4.5, 0.008, "史诗", 900, 1.00, 400),
    ("red", 9.0, 0.0035, "传说", 2200, 1.80, 900),
    ("orange", 18.0, 0.0015, "神话", 5500, 3.00, 2200),
    ("gold", 40.0, 0.0006, "至尊", 14000, 5.00, 6000),
]

FISHING_FORMULA_CONSTANTS = [
    # key, value, notes
    ("BITE_BASE_SCALE", 0.05, "咬钩基数全局缩放（× biteBaseAtMaxSize）"),
    ("SIZE_BITE_K", 0.65, "体长对咬钩的影响系数"),
    ("MAX_FISH_SIZE_M", 50.0, "绝对体长硬帽（米）"),
    ("REFERENCE_SIZE_M", 40.0, "脱钩/收杆曲线参考体长"),
    ("ESCAPE_AT_40M", 0.985, "40m 脱钩率锚点（减免前）"),
    ("SIZE_ESCAPE_CURVE_EXPONENT", 1.8, "脱钩曲线指数"),
    ("JUVENILE_ESCAPE_SIZE_M", 0.35, "幼鱼脱钩段上限体长"),
    ("JUVENILE_SIZE_M_MIN", 0.08, "幼鱼出生/脱钩插值下限"),
    ("JUVENILE_SIZE_M_MAX", 0.20, "幼鱼出生体长上限"),
    ("ESCAPE_AT_JUVENILE_MIN", 0.22, "0.08m 幼鱼脱钩锚点"),
    ("ESCAPE_AT_JUVENILE_MAX", 0.08, "0.35m 幼鱼脱钩锚点"),
    ("SIZE_HOOK_CURVE_EXPONENT", 1.8, "收杆窗口曲线指数"),
    ("HOOK_MIN_MS", 2000, "最小收杆窗口（毫秒）"),
    ("HOOK_AT_40M_MS", 7200000, "40m 收杆窗口（毫秒）"),
    ("NEAR_MAX_SIZE_RATIO", 0.975, "近满尺寸判定比例"),
    ("NEAR_MAX_SPAWN_CHANCE", 0.006, "出生强制近满尺寸概率"),
    ("LENGTH_WEIGHT_A", 12.0, "体重 W(kg)=a×L(m)^b 的 a，展示用不落库"),
    ("LENGTH_WEIGHT_B", 3.0, "体重 W(kg)=a×L(m)^b 的 b，展示用不落库"),
    ("XP_SIZE_EXP", 0.85, "上鱼经验体长乘子指数：(sizeM/SIZE_REF)^exp"),
]


def species_rows_for_xlsx():
    """Rows for fish_species: + qualityMin(1) / qualityMax(by rarityTier)."""
    rows = []
    for sid, name, diet, group, mn, mx, tier, nationwide in FISH_SPECIES_CN:
        qmax = QUALITY_MAX_BY_TIER.get(tier, 7)
        rows.append((sid, name, diet, group, mn, mx, tier, nationwide, 1, qmax))
    return rows


def habitat_rows_for_xlsx():
    """Build-time only helper (not exported). Prefer editing pond_fish_pool."""
    rows = []
    for sid, regions in sorted(HABITAT_BY_SPECIES.items()):
        for region in regions:
            rows.append((sid, region, True))
    return rows


def pond_base_tuples():
    """Compat with old PONDS shape (first 10 fields) for dual-fee builder."""
    return [p[:10] for p in PONDS_CN]


def pond_extra_fields(pond_id: str) -> tuple[str, str, str]:
    for p in PONDS_CN:
        if p[0] == pond_id:
            return p[10], p[11], p[12]
    return "", "", ""


def species_index() -> dict[str, tuple]:
    return {row[0]: row for row in FISH_SPECIES_CN}


def allowed_species_for_pond(pond) -> list[str]:
    pond_id, _name, category, *_rest = pond[:10]
    bio = pond[10]
    idx = species_index()

    if pond_id == "pond-novice":
        return ["crucian", "carp", "loach"]

    allowed: list[str] = []
    for sid, row in idx.items():
        nationwide = row[7]
        if nationwide:
            allowed.append(sid)
            continue
        regions = HABITAT_BY_SPECIES.get(sid, [])
        if bio in regions:
            # giant pond: prefer cold + nationwide; still allow habitat match
            if category == "giant" and bio != "northeast_cold" and sid not in (
                "rainbow_trout", "lenok", "taimen", "whitefish",
            ):
                # only northeast habitat species for frost
                pass
            allowed.append(sid)
    # frost giant: also ensure cold specialists present
    if pond_id == "pond-frost":
        for sid in ("rainbow_trout", "lenok", "taimen", "whitefish"):
            if sid not in allowed:
                allowed.append(sid)
        # drop coastal / south exclusives if any slipped
        allowed = [
            sid for sid in allowed
            if sid in idx and (
                idx[sid][7]
                or "northeast_cold" in HABITAT_BY_SPECIES.get(sid, [])
            )
        ]
    return allowed


def species_spawn_weight(sid: str) -> float:
    row = species_index()[sid]
    tier = row[6]
    base = float(SPECIES_BASE_WEIGHT[tier])
    if sid == "taimen":
        base = 0.4
    if sid in ("chinese_sturgeon", "dabry_sturgeon"):
        base = 0.5
    return base


def build_pond_fish_pool_rows() -> list[tuple]:
    """pondId, speciesId, speciesName, spawnWeight, enabled — 种池不含品质（品质走 category 表）。"""
    rows: list[tuple] = []
    idx = species_index()
    for pond in PONDS_CN:
        pond_id = pond[0]
        for sid in allowed_species_for_pond(pond):
            name = idx[sid][1]
            rows.append((pond_id, sid, name, round(species_spawn_weight(sid), 3), True))
    return rows


def ecology_fields_for_pond(pond_id: str) -> tuple[int, int, int]:
    for p in PONDS_CN:
        if p[0] == pond_id:
            category = p[2]
            return ECOLOGY_BY_CATEGORY.get(category, (70, 10, 40))
    return (70, 10, 40)


def build_pond_ecology_rows() -> list[tuple]:
    """Deprecated: population columns live on ponds sheet."""
    rows = []
    for pond in PONDS_CN:
        pond_id, name = pond[0], pond[1]
        mx, mn, init = ecology_fields_for_pond(pond_id)
        rows.append((pond_id, mx, mn, init, name))
    return rows


def build_category_quality_weight_rows() -> list[tuple]:
    rows = []
    for cat in CATEGORIES:
        skew = 1.0
        if cat in ("wilderness", "reservoir"):
            skew = 0.85
        if cat == "forbidden":
            skew = 1.05
        if cat == "giant":
            skew = 1.15
        for q, w in DEFAULT_QUALITY_WEIGHTS:
            # higher categories boost high quality slightly
            adj = w
            if cat in ("veteran", "forbidden", "giant") and q in ("purple", "red", "orange", "gold"):
                adj = int(round(w * 1.2 * skew))
            elif cat == "novice" and q not in ("gray", "green"):
                adj = 0
            else:
                adj = int(round(w * skew))
            if adj > 0:
                rows.append((cat, q, adj))
    return rows
