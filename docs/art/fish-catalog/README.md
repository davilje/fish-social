# 图鉴鱼种与素材对照表

当前游戏鱼种表共 50 个条目。保持既有 ID 和玩法数据；每个 ID 对应一个独立 Sprite。

## 接入规则

- 运行时清单：`fish-social-unity/Assets/Resources/Desktop/Theme/Cozy/items/fish/catalog.json`。
- UI 通过 `CozyItemArt.Fish(speciesId)` 统一读取，覆盖图鉴、背包、展示柜与选鱼弹窗。
- 44 张新增 PNG 与 6 张既有图集切片组合，原图集不覆盖。
- 所有图片使用 Sprite、零九宫格边框、透明背景、等比缩放；鱼图不是 Sliced 底板。
- 历史 ID `bass`、`perch`、`largemouth_bass` 统一指向 `black_bass`；`trout` 和 `sturgeon` 使用既有兼容映射。
- 未知 ID 返回空，不随机展示其他鱼。图鉴未解锁时继续使用该鱼本身的轮廓遮罩。
- `团头鲂` 与 `武昌鱼`、`青鱼` 与 `巨青` 按游戏现有条目分别配图，不据此宣称它们是不同生物物种。

## 资源列表

Resources 路径不带扩展名；“图集切片”列为空时是同名独立 PNG。

| 序号 | 游戏名称 | speciesId | Resources 路径 | 图集切片 |
|---|---|---|---|---|
| 1 | 鲫鱼 | `crucian` | `Desktop/Theme/Cozy/items/fish/crucian` | — |
| 2 | 鲤鱼 | `carp` | `Desktop/Theme/Cozy/items/fish-atlas` | `carp` |
| 3 | 草鱼 | `grass_carp` | `Desktop/Theme/Cozy/items/fish-atlas` | `grass_carp` |
| 4 | 鲢鱼 | `silver_carp` | `Desktop/Theme/Cozy/items/fish/silver_carp` | — |
| 5 | 鳙鱼 | `bighead_carp` | `Desktop/Theme/Cozy/items/fish/bighead_carp` | — |
| 6 | 青鱼 | `black_carp` | `Desktop/Theme/Cozy/items/fish-atlas` | `black_carp` |
| 7 | 罗非鱼 | `tilapia` | `Desktop/Theme/Cozy/items/fish/tilapia` | — |
| 8 | 泥鳅 | `loach` | `Desktop/Theme/Cozy/items/fish/loach` | — |
| 9 | 鳊鱼 | `bream` | `Desktop/Theme/Cozy/items/fish/bream` | — |
| 10 | 团头鲂 | `bluntnose_bream` | `Desktop/Theme/Cozy/items/fish/bluntnose_bream` | — |
| 11 | 长春鳊 | `white_amur_bream` | `Desktop/Theme/Cozy/items/fish/white_amur_bream` | — |
| 12 | 鳜鱼 | `mandarin` | `Desktop/Theme/Cozy/items/fish/mandarin` | — |
| 13 | 翘嘴鲌 | `topmouth` | `Desktop/Theme/Cozy/items/fish/topmouth` | — |
| 14 | 大口黑鲈 | `black_bass` | `Desktop/Theme/Cozy/items/fish-atlas` | `largemouth_bass` |
| 15 | 乌鳢 | `snakehead` | `Desktop/Theme/Cozy/items/fish-atlas` | `snakehead` |
| 16 | 鲶鱼 | `catfish` | `Desktop/Theme/Cozy/items/fish/catfish` | — |
| 17 | 黄颡鱼 | `yellow_catfish` | `Desktop/Theme/Cozy/items/fish/yellow_catfish` | — |
| 18 | 银鲴 | `silver_bream` | `Desktop/Theme/Cozy/items/fish/silver_bream` | — |
| 19 | 红鳍鲌 | `redfin` | `Desktop/Theme/Cozy/items/fish/redfin` | — |
| 20 | 马口鱼 | `river_chub` | `Desktop/Theme/Cozy/items/fish/river_chub` | — |
| 21 | 鳡鱼 | `chinese_sabre` | `Desktop/Theme/Cozy/items/fish/chinese_sabre` | — |
| 22 | 赤眼鳟 | `redeye` | `Desktop/Theme/Cozy/items/fish/redeye` | — |
| 23 | 长吻鮠 | `longsnout_cat` | `Desktop/Theme/Cozy/items/fish/longsnout_cat` | — |
| 24 | 武昌鱼 | `wuchang_bream` | `Desktop/Theme/Cozy/items/fish/wuchang_bream` | — |
| 25 | 光唇鱼 | `cn_mahseer` | `Desktop/Theme/Cozy/items/fish/cn_mahseer` | — |
| 26 | 岩原鲤 | `rock_carp` | `Desktop/Theme/Cozy/items/fish/rock_carp` | — |
| 27 | 花鲴 | `barbel` | `Desktop/Theme/Cozy/items/fish/barbel` | — |
| 28 | 裂腹鱼 | `snow_trout` | `Desktop/Theme/Cozy/items/fish/snow_trout` | — |
| 29 | 金线鲃 | `golden_line` | `Desktop/Theme/Cozy/items/fish/golden_line` | — |
| 30 | 铜鱼 | `bronze_carp` | `Desktop/Theme/Cozy/items/fish/bronze_carp` | — |
| 31 | 黑鲷 | `black_porgy` | `Desktop/Theme/Cozy/items/fish/black_porgy` | — |
| 32 | 真鲷 | `red_porgy` | `Desktop/Theme/Cozy/items/fish/red_porgy` | — |
| 33 | 大黄鱼 | `yellow_croaker` | `Desktop/Theme/Cozy/items/fish/yellow_croaker` | — |
| 34 | 带鱼 | `hairtail` | `Desktop/Theme/Cozy/items/fish/hairtail` | — |
| 35 | 鲐鱼 | `chinese_mackerel` | `Desktop/Theme/Cozy/items/fish/chinese_mackerel` | — |
| 36 | 牙鲆 | `flounder` | `Desktop/Theme/Cozy/items/fish/flounder` | — |
| 37 | 花鲈 | `sea_bass` | `Desktop/Theme/Cozy/items/fish/sea_bass` | — |
| 38 | 银鲳 | `pomfret` | `Desktop/Theme/Cozy/items/fish/pomfret` | — |
| 39 | 石斑鱼 | `grouper` | `Desktop/Theme/Cozy/items/fish/grouper` | — |
| 40 | 鲻鱼 | `mullet` | `Desktop/Theme/Cozy/items/fish/mullet` | — |
| 41 | 虹鳟 | `rainbow_trout` | `Desktop/Theme/Cozy/items/fish/rainbow_trout` | — |
| 42 | 细鳞鲑 | `lenok` | `Desktop/Theme/Cozy/items/fish/lenok` | — |
| 43 | 哲罗鲑 | `taimen` | `Desktop/Theme/Cozy/items/fish/taimen` | — |
| 44 | 雅罗鱼 | `whitefish` | `Desktop/Theme/Cozy/items/fish/whitefish` | — |
| 45 | 中华鲟 | `chinese_sturgeon` | `Desktop/Theme/Cozy/items/fish/chinese_sturgeon` | — |
| 46 | 达氏鲟 | `dabry_sturgeon` | `Desktop/Theme/Cozy/items/fish/dabry_sturgeon` | — |
| 47 | 鳗鲡 | `eel` | `Desktop/Theme/Cozy/items/fish/eel` | — |
| 48 | 黄鳝 | `ricefield_eel` | `Desktop/Theme/Cozy/items/fish/ricefield_eel` | — |
| 49 | 锦鲤 | `koi` | `Desktop/Theme/Cozy/items/fish-atlas` | `koi` |
| 50 | 巨青 | `giant_black_carp` | `Desktop/Theme/Cozy/items/fish/giant_black_carp` | — |

## 维护与验收

新增鱼种时必须同步该清单与对应素材；`CozyFishCatalogTests` 对比游戏鱼种全集、名字、图片唯一性及旧 ID 映射。
编辑器菜单 `Fish Social > Cozy Redesign > Render Complete Fish Catalog` 输出五张十鱼预览页到 `Library/CozyFishCatalog`。

## 本次交付检查（2026-10-18）

- `tools/fish-catalog/verify.py`：50 个游戏条目、50 个独立图片身份，无缺图、无重复映射，透明通道检查通过。
- Unity EditMode `CozyFishCatalogTests`：9/9 通过，覆盖全部鱼种加载、名称匹配、Sprite 唯一性、旧 ID 兼容与未知 ID 行为。
- 五张 Unity 实际渲染总览已经逐页检查，保存在 `previews/`。未进行 Windows Player 联网验收。
- `index.html` 是可搜索的素材总览，`fish-art-mapping.csv` 可用于表格检查；`validation.json` 保存每条素材的路径、尺寸和校验值。
- 此次补充的是图鉴与物品 UI 静态图片，不修改鱼种数值表、解锁记录或钓鱼动作序列。
