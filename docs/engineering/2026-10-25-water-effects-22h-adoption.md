# STEAM-DESKTOP-22H：正式采用参考鱼塘配色

> 后续2026-10-26已修复上钩浮漂与鱼线入水点、水花定位，见[修复报告](2026-10-26-hooked-bobber-water-contact.md)。本页验收包与hash保留为2026-10-25采用基线。

2026-10-25用户确认“应用该方案”，正式采用已认可的v4简约水魔法造型和v8鱼塘配色。实现状态为**已实现**，设计日期2026-10-24、实现完成日期2026-10-25；Windows专项验收继续单独记录。

## 应用内容

- 8水花、8波纹、2水滴统一使用深蓝暗部 `#1B5272`、浅青白水体 `#B5F0F5` 与奶白高光 `#FFF7E0`。保留原图alpha，预热时一次调色并缓存，三端使用同一映射。
- 等待波纹放大为原规格1.7倍，其他波纹1.2倍；播放峰值不透明度95%，前6%淡入，保持至40%后平滑淡出。
- WPF正式渲染器、Unity兜底渲染器与默认Web预览均采用本方案；18张PNG和配置在Overlay、Unity StreamingAssets与Unity随包Overlay中一致。调色由渲染器完成，不能仅替换PNG来升级旧程序。
- 七类触水效果继续使用已有事件：咬钩和水下挣扎仅波纹；实际鱼出水、抛竿、撒饵、收杆触水才溅起水花。鱼出水按可见鱼体尺寸取档，落点固定，完整水面波纹在水线下独立裁剪，水滴允许飞至水线上。

[采用记录与源码／资源哈希](water-effects-22h-adoption.json) · [参考水面预览](../art/water-effects-22h/index.html?style=magic&v=8-pond-palette&water=reference&cue=FX02) · [配色及验证证据](../art/water-effects-22h/magic-v4/palette-2026-10-25/README.md)。用户参考景图用于预览和配色对照；正式应用内容为水效表现。

## 验证

采用前同版渲染代码已通过357项Web检查（11角色、3槽位、3品质、3水面及触水／裁剪等），Unity水效1项综合回归通过且正常退出0，WPF程序与原生测试项目交叉编译0错误。见上述证据目录的 `web.log`、`unity.xml`、`unity.log`、`wpf.log`、`bindings.log`。本次未改渲染行为，复用这些结果。

本次重新发布win-x64自包含包，核对包内125项源码哈希、10项二进制哈希、20项正式资源哈希及采用记录，全部一致；ZIP完整性通过。[打包检查](../art/water-effects-22h/magic-v4/palette-2026-10-25/package-verification.json)。根目录计划表与看板和docs副本已重生，计划表内容仅22H行的状态、说明、完成日期改变。

## 当前Windows验收包

- [下载验收包](../../dist/overlay-water-effects-22h-v4-2026-10-25.zip)，250,707,245 bytes。
- SHA-256：`9b0e4d20112bc70f9ecf37bcccdfc0106f7a1cfa440a789f675aafeb1c73e146`。
- 解压后执行 `Run-Native-Tests.cmd` 获取 `native-tests.log`；`BUILD-INFO.json`及`ADOPTED-PRESENTATION.json`可核对版本与当前采用内容。
- 2026-10-24包保留为历史基线，不包含本次最终配色与波纹可见度修订；使用本包验收。包为Overlay及原生测试构建，完整联调还需同版Unity／服务端事件链。

## 待实机验收

当前环境是macOS，未运行Windows WPF程序或原生测试。Windows高DPI与所有场景落点、桌面点击穿透、20人/多个XL、10分钟稳定性、30次进出塘A/B性能，以及至少30次H/I音画采样仍待执行。编译、Web预览及Unity测试不替代这些项目，spec中对应复合验收项继续留待办。
