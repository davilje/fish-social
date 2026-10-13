# Fishing 分层动画包 v2

来源为 animation-studio v18，640×512 RGBA；1550 张 PNG，5 个同形象配色、14 个运行时片段，全部 51 张源动作帧。

四个角色层：body、head、far_hand、hands。五个公共层：rod、line、fish、bobber、bait。必须按照 manifest 内每帧顺序绘制，按 durationsMs 计时；旧 cat.png 不是新播放器的输入，不与 body/head 重叠。

`hooked` 前四帧只播一次，后六帧循环；`effort` 从零帧循环。成功收杆和空军收杆均为六帧、600ms。pending catch 展示最后帧保持，领取后复位。

源 JSON 保存在 source-animation-v18.json，导出清单为 inventory.json。

Unity 预览菜单：Window → Fish Social → Fishing Animation Preview。

完整接入、状态映射、换肤限制和运行验收说明见仓库 `docs/art/animations/2026-10-13-fishing-animation-port.md`。五个配色不代表五个不同花纹品种；帽子围巾仍保持移除。

Windows 原生桌宠需要重新编译加载器，不能只复制资源给旧 EXE。
