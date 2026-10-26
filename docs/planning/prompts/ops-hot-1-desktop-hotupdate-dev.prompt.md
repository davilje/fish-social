# 开发提示词：桌面资源与数值热更（OPS-HOT-1）

你是 Fish Social 后端和桌面客户端开发。按规格做热更通道，不要在这张票里改公式或世界观文案。

## 必读

1. `docs/planning/specs/桌面资源与数值热更.md`（**已确认** / **OPS-HOT-1**）
2. `docs/planning/specs/发版与热更策略.md`（分类已定，本项不重写 runbook）
3. 现有 `game_config` 继续只管原有开关，不要把公式表并进去

## 顺序

1. 数值包：校验、切换、单独回滚，不重启进程。非法字段整包拒绝。
2. Overlay 资源包：布局、场景图、特效和音效带版本清单。缺文件整包拒绝。
3. 客户端缓存失败时回退安装包内资源，并给出可见原因。
4. 不热更 exe，不改 `mobile/`，不恢复 FEAT-05。

## 验收

对照 spec §5。完成后把 spec 改为 **已实现**，并 `npm run planning:master-xlsx`。

## 派发

```text
@docs/planning/prompts/ops-hot-1-desktop-hotupdate-dev.prompt.md 按此实现 OPS-HOT-1
```
