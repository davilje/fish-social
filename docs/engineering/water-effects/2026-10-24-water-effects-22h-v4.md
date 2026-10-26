# STEAM-DESKTOP-22H v4：认可风格的正式接入

> 2026-10-25 用户确认“应用该方案”：本轮正式采用与最新Windows验收包见[采用记录](2026-10-25-water-effects-22h-adoption.md)。

> 2026-10-25 参考水面配色：三端改为浅青白主体、奶白高光与深蓝暗部，预览直接使用用户鱼塘图；[配色与357项检查记录](../../art/water-effects-22h/magic-v4/palette-2026-10-25/README.md)。

> 2026-10-25 追加波纹调整：等待波纹1.7倍、其他1.2倍并提亮为浅冰蓝，见[尺寸与颜色验证](../../art/water-effects-22h/magic-v4/ripples-2026-10-25/README.md)。

> 2026-10-25 可见度更新：三端提高显示强度并延后淡出，见[本轮对照与验证](../../art/water-effects-22h/magic-v4/visibility-2026-10-25/README.md)。下文2026-10-24 zip及测试数字保留为历史基线。

日期：2026-10-24；本轮代码基线 `fb6ce820`。用户确认原话：“该风格可以”。确认对象是简约水魔法的风格，完整实机验收仍单独记录。

## 资源与正式接入

原v3因细节过密被否定；v4以游戏猫咪为参考，保留低饱和湖蓝、圆润色块、少量高光和简洁卷曲水弧。此次从已确认的AI生成原图进行确定性切图、邻格碎片清理、抗锯齿边缘保留、尺寸归一与透明留边，未另绘一套风格。

- [猫咪同屏预览](../../art/water-effects-22h/index.html?style=magic)：默认v4，原版v2可切换对照；正式PNG／配置直接供预览消费。
- [认可风格、来源及最终提示词](../../art/water-effects-22h/magic-v4/README.md)。原始1774×887透明图集留在docs，运行时只加载规范化小图。
- 8水花＋8水纹＋2水滴，16张128×64＋2张64×64，解码 **557,056 bytes / 0.53125MiB**；逐图8px透明留边、坐标锚点与schema保持兼容。
- 正式资源目录 `desktop-overlay/OverlayResources/effects/fishing/v4/`；Unity `Assets/StreamingAssets/Effects/Fishing/v4/` 和随包Overlay同路径镜像逐项相同，包含Unity `.meta`。
- `OverlayWaterEffects.cs`、`UnityFishingWaterEffects.cs` 已切换加载v4；可移植水效测试也读取正式v4配置。
- 各档额外水滴最多2颗，标准S/M/L/XL分别4/5/6/6个绘制实例；保持全局24实例／8组及自己12实例／2组预留，数量均低于原上限。
- 来源为内置image_gen；v4不继承v2原创SVG的CC0标注。manifest记录原图与PNG hash、切图区域、处理方式、用户风格确认及尚未完成的实机验收。

构建入口：`scripts/art/build-water-effects-22h-v4.cjs`。v2目录与源稿保留，旧版构建脚本现在也保留v4作为预览默认，避免重建v2时静默退回旧配置。

## 用户追加的动效规则及落点修正

- 咬钩与水下挣扎只允许水面波纹，禁止水冠／飞溅水滴。水花只用于鱼真正出水及抛竿、撒饵、收杆真实触水。
- 波纹根据实际PNG可见上沿整体移到水线下，再独立裁剪兜底。水冠与水滴保留完整岸内裁剪，可以向上飞溅，不会被错误裁成水线下半圈。
- 抛竿取cast源3（420ms）真实浮漂触水点；撒饵源5的580ms仅为候选，需等正式D插值position5.333（620ms）首次触水。特效锚点固定，不跟随下一帧物件漂移。Web共用正式motion-samples矩阵、源alpha bounds与22B姿态。
- 收杆FX09只绑定reel/source0真实鱼线水线交点或reel_empty/source0浮漂；line交点由离线alpha逐行中心经实际变换求得，不取整条线bbox中心。每action occurrence0一次。
- 实际reel鱼身到position0.667（67ms）才跨过水线，才触发一次fish.surface。标准128布局本帧可见宽43.123px，对应L档；未来不同已显示鱼身尺寸依同规则取档，不能读取未公开鱼信息。
- FX05 surface在hooked/effort及真实resolving/stopping收杆中许可；fish.slap仍仅hooked/effort。FX06/07结果装饰继续撤销。主动stop只开放真实触水窗口，快照不补播，离席／断线／硬取消优先。
- Unity同一source frame内会重新查询尚未触水的provider，覆盖580→620ms；成功后由共享session去重，普通连续波纹仍按新发布帧门控。无真实水面provider的portrait保持静默。

## 本轮验证

| 项目 | 结果 |
|---|---|
| 资源＋Web | [253项通过](../../art/water-effects-22h/verification-v4.json)：11角色、96/128/192槽位、三档品质、明暗水面、七类A/B、暂停逐帧；额外检查触水前零发射、触水几何、固定落点、波纹线上零像素及水冠可上线。无页面异常／404，最大9实例／4组。 |
| 正式配置绑定 | [通过](../../art/water-effects-22h/bindings-verification-v4.json)：两端加载v4；七类／八marker与实际policy匹配，slap阶段严格收窄，FX06/07无绑定。 |
| 水效策略 | [1382断言通过](../../art/water-effects-22h/magic-v4/evidence/water-policy.log)：含真实GB620ms、reel67ms矩阵／可见鱼尺寸／line交点、组件构成、快照与取消。 |
| 共享session | [2128检查通过](../../art/water-effects-22h/evidence/shared-session-tests.log)：真实收杆窗口、预测合并、surface阶段、一次性、取消与独立消费者。 |
| 原有源／资源保护 | [51/51通过](../../art/water-effects-22h/magic-v4/evidence/python-source.log)，无需修改旧hash快照。 |
| Unity | [208/208 EditMode通过](../../art/water-effects-22h/evidence/unity-v4-final-full.xml)，正常退出0，最终compile-only退出0。覆盖实际v4 PNG完整椭圆与独立水线mask、bite纯波纹、窝料水花中心、同源帧后触水、reel真实层／尺寸／取消及30次root重建。 |
| WPF | [win-x64自包含发布](../../art/water-effects-22h/magic-v4/evidence/windows-publish.log)，正式程序与33组原生测试交叉编译；新增RenderTargetBitmap波纹线上零像素／水滴可上线、真实收杆鱼出水检查。macOS未运行Windows测试。 |
| 审查包一致性 | [完整hash检查](../../art/water-effects-22h/magic-v4/evidence/package-verification.json)：源码与正式v4包内资源一致。 |

[Windows v4自包含包](../../../dist/overlay-water-effects-22h-v4-2026-10-24.zip)：250,704,510 bytes，SHA-256 `fdcb379006eaf1394551e4368cccf03a73b6aed8a8c4d8d3a5753528b93bf499`；122个源码hash无漂移。运行其中Run-Native-Tests.cmd执行33组原生回归；此包未部署或替换游戏安装，实机需配套Unity／server协议。

## 待验收与状态

风格确认已记录；Windows原生画面／高DPI／桌面点击穿透、20角色同条件A/B、30次进出＋10分钟稳定性、实际冷解码及至少30次H/I音画采样仍未执行。Unity Home portrait缺真实水面provider时继续静默，正式多人显示由WPF承担。

计划表已同步追加规则和本机验证范围，H状态仍为已确认，完成日期留空；没有把风格认可当作完整Windows验收。

## 复现

```sh
node scripts/art/build-water-effects-22h-v4.cjs
node scripts/art/verify-water-effects-22h.cjs --v4
node scripts/art/verify-water-effects-bindings-22h.cjs --v4
dotnet run --project desktop-overlay/tests/FishSocialOverlay.WaterEffectsTests.csproj
python3 -m unittest discover -s desktop-overlay/tests -p 'test_*.py'
python3 scripts/desktop/package-water-effects-22h.py --dotnet dotnet --output dist/overlay-water-effects-22h-v4-2026-10-24.zip
npm run planning:master-xlsx
```
