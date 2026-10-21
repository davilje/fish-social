# Cozy UI 持久化资源

所有尺寸、GUID、fileID、Sprite rect、border和实际导入尺寸以本目录`asset-manifest.json`为准，该文件由Unity AssetDatabase重建。旧文档中的按钮border10、纸页border20已经过时，不再作为导入依据。

`journal/`是原版手账UI新增资源：90张共用切图、60张角色补充姿势、21张真实鱼塘场景图。原有137张PNG继续保留供现有引用使用。共用切图参数见`journal/ui-manifest.json`，角色与鱼塘分别有独立manifest。

新切图需要同时读取`contentInsets`（设计坐标的透明外延）、border（源像素）、PPU与imageType。原版纸纹使用PPU 50、Repeat、alpha .32和Multiply材质；其余新UI主要为2×/PPU 200。不要把纸纹作为普通九宫格或随面板拉伸。

10页面/11状态的Unity参考prefab位于`Assets/Desktop/JournalSamples/`；12个组合组件位于其`Components/`。这些是独立资源验证与布局参考，没有替换正式游戏页面，也没有接入其业务事件。新角色姿势为补充素材，默认参考仍使用已有orange-ready。

查看`docs/art/cozy-journal/README.md`、`compare.html`和`index.html`获取校正状态、预览与重建步骤。50项共用UI素材已按原版校正；补充素材与跨引擎视觉差异仍需分别验收。
