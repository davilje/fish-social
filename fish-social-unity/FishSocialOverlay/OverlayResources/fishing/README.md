# Fishing 分层动画包 v2

当前为 640×512 RGBA，九种猫咪、16 个运行时片段，每种 74 个运行时帧。橘猫使用原始动作包，三花猫为连续性修正 v3，其余七种为独立绘制 v4。

四个角色层：body、head、far_hand、hands。五个公共层：rod、line、fish、bobber、bait。必须按照 manifest 内每帧顺序绘制，按 durationsMs 计时；旧 cat.png 不是新播放器的输入，不与 body/head 重叠。

`hooked` 前四帧只播一次，后六帧循环；`effort` 从零帧循环。成功收杆和空军收杆均为六帧、600ms。pending catch 展示最后帧保持，领取后复位。

当前动作源 JSON 为 source-animation-v19.json，导出清单为 inventory.json。v18 JSON 仅用于历史对照。

Unity 预览菜单：Window → Fish Social → Fishing Animation Preview。

当前资源调用、兼容与清理说明见仓库 `docs/art/cat-skins/resource-routing.md`。旧整张序列和旧程序绘制猫已退出运行路径；缺动画时显示当前头像。帽子围巾保持移除。

Windows 原生桌宠需要重新编译加载器，不能只复制资源给旧 EXE。
