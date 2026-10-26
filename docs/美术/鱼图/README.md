# 鱼图轻 Q 版 · 全量交付

50 个游戏鱼种条目统一使用已确认的轻 Q 版风格：深棕描边、简洁色块、少量鳞片提示、黑色高光眼睛，同时保留各自体形和花纹。

## 文件与接入

- 正式图片：`fish-social-unity/Assets/Resources/Desktop/Theme/Cozy/items/fish/<speciesId>.png`。
- 正式映射：同目录 `catalog.json`。50 张均为独立 Sprite，不再引用旧写实图集切片。
- 图鉴、背包、展示柜、选鱼界面统一经 `CozyItemArt.Fish(speciesId)` 读取；不修改鱼种 ID、数值和解锁记录。
- 历史名称 bass/perch/largemouth_bass 等继续使用现有兼容映射。
- 已确认的六张直接沿用；其余 44 张使用内置 image_gen，以已确认鱼图为共同风格参考生成。完整提示词见 `cartoon-production-prompts.json`。
- 旧写实图片在 [realistic-v1](../../归档/2026-10-26/替代/美术/鱼图/realistic-v1/)；六张样稿对照页继续引用这份归档，不会出现新旧相同的问题。

## 浏览和检查

- `index.html`：当前 50 种鱼图，可搜索、切换深色底。
- `fish-art-mapping.csv`：鱼名、ID、资源路径一一对应。
- `previews/`：Unity 渲染总览与图鉴页面。
- `tools/fish-catalog/verify.py`：检查游戏条目覆盖、名称、文件、重复内容与透明通道。
- Unity `CozyFishCatalogTests`：检查 50 种 Sprite 读取、独立性和旧 ID 映射。

## 本次验证（2026-10-18）

- 静态校验通过：50 个条目、50 张独立素材，名称、映射、透明通道及重复检查均通过。
- Unity EditMode：`CozyFishCatalogTests` 9/9 通过，结果保存于 `cartoon-tests.xml`。
- Unity 实际渲染：五页鱼种总览及图鉴页面已更新到 `previews/`。
- 浏览器检查：50/50 图片加载成功，中文搜索及深色底切换正常。
- 本次未执行 Windows Player 构建或联网游戏验收。

## 逐条映射

| 鱼名 | speciesId | 图片 |
|---|---|---|
| 鲫鱼 | crucian | crucian.png |
| 鲤鱼 | carp | carp.png |
| 草鱼 | grass_carp | grass_carp.png |
| 鲢鱼 | silver_carp | silver_carp.png |
| 鳙鱼 | bighead_carp | bighead_carp.png |
| 青鱼 | black_carp | black_carp.png |
| 罗非鱼 | tilapia | tilapia.png |
| 泥鳅 | loach | loach.png |
| 鳊鱼 | bream | bream.png |
| 团头鲂 | bluntnose_bream | bluntnose_bream.png |
| 长春鳊 | white_amur_bream | white_amur_bream.png |
| 鳜鱼 | mandarin | mandarin.png |
| 翘嘴鲌 | topmouth | topmouth.png |
| 大口黑鲈 | black_bass | black_bass.png |
| 乌鳢 | snakehead | snakehead.png |
| 鲶鱼 | catfish | catfish.png |
| 黄颡鱼 | yellow_catfish | yellow_catfish.png |
| 银鲴 | silver_bream | silver_bream.png |
| 红鳍鲌 | redfin | redfin.png |
| 马口鱼 | river_chub | river_chub.png |
| 鳡鱼 | chinese_sabre | chinese_sabre.png |
| 赤眼鳟 | redeye | redeye.png |
| 长吻鮠 | longsnout_cat | longsnout_cat.png |
| 武昌鱼 | wuchang_bream | wuchang_bream.png |
| 光唇鱼 | cn_mahseer | cn_mahseer.png |
| 岩原鲤 | rock_carp | rock_carp.png |
| 花鲴 | barbel | barbel.png |
| 裂腹鱼 | snow_trout | snow_trout.png |
| 金线鲃 | golden_line | golden_line.png |
| 铜鱼 | bronze_carp | bronze_carp.png |
| 黑鲷 | black_porgy | black_porgy.png |
| 真鲷 | red_porgy | red_porgy.png |
| 大黄鱼 | yellow_croaker | yellow_croaker.png |
| 带鱼 | hairtail | hairtail.png |
| 鲐鱼 | chinese_mackerel | chinese_mackerel.png |
| 牙鲆 | flounder | flounder.png |
| 花鲈 | sea_bass | sea_bass.png |
| 银鲳 | pomfret | pomfret.png |
| 石斑鱼 | grouper | grouper.png |
| 鲻鱼 | mullet | mullet.png |
| 虹鳟 | rainbow_trout | rainbow_trout.png |
| 细鳞鲑 | lenok | lenok.png |
| 哲罗鲑 | taimen | taimen.png |
| 雅罗鱼 | whitefish | whitefish.png |
| 中华鲟 | chinese_sturgeon | chinese_sturgeon.png |
| 达氏鲟 | dabry_sturgeon | dabry_sturgeon.png |
| 鳗鲡 | eel | eel.png |
| 黄鳝 | ricefield_eel | ricefield_eel.png |
| 锦鲤 | koi | koi.png |
| 巨青 | giant_black_carp | giant_black_carp.png |
