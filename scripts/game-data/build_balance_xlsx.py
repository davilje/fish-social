#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Build root 钓鱼玩法固定数值表.xlsx (FEAT-PROG-01 balance pipeline)."""
from __future__ import annotations

from pathlib import Path

from openpyxl import Workbook
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
    # category, biteRateMul, escapeRateMul, infoRevealMul, qualityWeightSkew, sizeCapMul, pondXpMul
    ("advanced", 1, 1, 1, 1, 1, 1),
    ("novice", 1, 1, 1, 1, 1, 1),
    ("veteran", 1, 1, 1, 1, 1, 1),
    ("wilderness", 0.75, 1.10, 0.70, 0.85, 0.95, 1.10),
    ("reservoir", 0.65, 1.0, 0.55, 0.85, 1.0, 1.15),
    ("forbidden", 0.90, 1, 1, 1, 1, 1),
    ("giant", 1, 1, 1, 1, 1, 1),
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
    for row in rows:
        ws.append(list(row))
    style_header(ws)
    autosize(ws)


def build() -> Path:
    wb = Workbook()
    # Drop the empty default sheet so the first append is not preceded by a blank row.
    default = wb.active
    wb.remove(default)

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
