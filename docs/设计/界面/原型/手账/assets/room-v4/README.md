# 小橘房间独立切图 v4

15 张透明 PNG + 空房间底图 PNG/JPEG。原型用 17 个实例组装房间，其中三支鱼竿复用一张图片。

移除了窗光斑、地板光照投影及物件外部投影；不添加猫咪脚底阴影。物件内部保留柔和明暗。底图仅保留墙地、窗户窗帘与门。

## 使用

- 物件 PNG 保留独立 Alpha 通道，已裁去周围透明空白。
- manifest.json 包含原生尺寸、透明裁切坐标、底部中心 pivot、布局坐标和层级。
- 布局坐标基于 1672 × 941；同一切图可作为多个独立实例使用。
- 默认在给定矩形内等比缩放、底部居中；地毯 fit: fill 按宽高适配地面透视。
- index.html 提供逐件下载、棋盘格／深色／米白透明边缘检查。
- 鱼缸内部鱼、水草、水体目前作为一个组件，未分别导出。
- 钓具收纳架包含箱子、帽子、毯子；壁架包含书；玩具篮包含篮内玩具。这些组合内部未继续拆分。
- 当前场景陈列使用预览素材，尚未绑定正式账号拥有物。

## 文件清单

| 组件 | 文件 | 原生尺寸 |
| --- | --- | --- |
| 猫窝 | cat-bed.png | 1586 × 657 |
| 猫爬架 | cat-climber.png | 1253 × 946 |
| 鱼缸 | aquarium.png | 1218 × 627 |
| 鱼竿 | fishing-rod.png | 278 × 1455 |
| 植物 | plant.png | 970 × 1100 |
| 地毯 | rug.png | 1950 × 515 |
| 玩具篮 | toy-basket.png | 1383 × 584 |
| 猫抓柱 | scratching-post.png | 765 × 1050 |
| 钓具收纳架 | tackle-rack.png | 1376 × 693 |
| 鱼缸底架 | aquarium-stand.png | 1909 × 511 |
| 食水碗 | food-bowls.png | 1393 × 454 |
| 钓鱼包与挂钩 | fishing-satchel.png | 751 × 1267 |
| 壁架与书 | wall-shelf.png | 1549 × 583 |
| 装饰画 | wall-picture.png | 1181 × 848 |
| 老鼠玩具 | mouse-toy.png | 1480 × 689 |
| 空房间底图 | room-base.png / room-base.jpg | 1672 × 941 |

## 来源

以 home-room-cat-furniture-v3.jpg 为参照，通过内置 image_gen 进行独立透明提取与遮挡边缘补全；完整提示词、参考图、生成路径见 generation-records.json。Pillow 导出步骤只裁透明空白、校验 Alpha、制作切图总览，不重画物件。
