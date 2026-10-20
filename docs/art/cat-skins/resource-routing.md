# 当前猫咪资源调用

本次统一九种完整皮肤：orange、calico、silver、brown、cream、blue、black、white、cow。
三花猫继续使用已修复花斑连续性的 v3 绘制稿；本次没有重画角色或改变桌面动作时序。

共享鱼竿已恢复为完整杆身，取消橘猫头部预裁切。九只猫依各自头部和双手分层进行遮挡，Unity / Overlay 的每帧顺序与移动端合成图一致，详见 [鱼竿遮挡说明](rod-occlusion.md)。

## 身份与兼容

默认头像选择只显示这九种。共享端 `resolveCatSkinId` 与 Unity `DesktopDefaultAvatars` 使用相同解析规则，Unity 将解析后的 ID 通过 IPC 交给 Overlay。

历史 gray、siamese、tuxedo 未制作新版完整动作，统一兼容到默认橘猫，不以其他品种冒充。旧头像及纪念照 URL 由客户端归一化，服务端同时提供 302 跳转，已有玩家资料和动态无需改数据库。自定义头像保留原来的六槽哈希，只有退役槽位改为橘猫。

## 运行入口

| 客户端/用途 | 当前来源 |
| --- | --- |
| Unity 钓鱼、主界面角色、动作预览 | `Assets/StreamingAssets/Fishing/<skin>/manifest.json`、角色四层及 shared 五层 |
| Windows Overlay 本人及其他玩家 | `OverlayResources/fishing/<skin>/manifest.json`，布局、时序与 Unity 一致 |
| Unity 头像、排行榜前三名 | `Resources/Desktop/Theme/Cozy/characters/avatars/<skin>.png`，按玩家解析 |
| Overlay 缺动画时 | `OverlayResources/hud/cozy/characters/avatars/<skin>.png` |
| 移动端/Web 鱼塘角色 | `image/cats/<skin>/<clip>.png`，由同一分层包合成的 320×256 横向图集 |
| 头像与动态纪念照 | `image/profile/`、`image/fishing_photos/` |

移动端图集共 9×16 张，保留逐帧时长、循环起点和 nextClip，覆盖打窝、离席、成功及空军收杆与复位。角色完全加载后再推进帧；失败时显示当前猫咪头像。服务端 `/image/cats` 提供图片，Docker 已有的 `COPY image` 会打包该目录。

Unity/Overlay 的缺图回退现在只使用当前头像；不再读取旧 pets、StreamingAssets/Pet、旧整张 cat.png 序列，也不再绘制几何占位猫。Overlay 在使用动画包前检查所有片段的九层完整性，避免缺某一层时仅显示半只猫。

## 素材清理

已删除旧整张姿势目录、三种退役头像和纪念照、旧 Cozy 静态猫图、分层目录中 manifest 没有引用的 PNG 及对应 meta。清理脚本先检查 Unity 序列化 GUID 引用；主壳、主界面、鱼塘和排行榜预制体已改为当前资源。

`characters/poses/orange-ready.png` 是从当前分层包合成的主界面初始姿势，保留为预制体静态底图。绘制源稿、历史审核页和对照记录仍保留在 docs 中，不属于运行资源。

逐文件清单及释放字节数记录于 `resource-cleanup.json`。

## 后续更新

1. 完成绘制稿后，用现有 `build_calico_fishing_pack.mjs --skin=<id> --install` 更新两端分层包。
2. 运行 `install_breed_identity.mjs <id>` 同步头像及纪念照。
3. 运行 `export_cat_client_assets.mjs` 生成移动端图集、动作索引及橘猫身份资源。
4. 运行 `clean_retired_cat_assets.mjs` 预查引用，再加 `--apply` 清理并同步清单。
5. 执行 `verify_cat_resource_routing.mjs`，并在 Unity/Windows Overlay 内做运行验收。

图像导出工具使用 Sharp；通过 `SHARP_MODULE` 指定非标准安装路径。资源调用检查使用 TypeScript；通过 `TYPESCRIPT_MODULE` 指定其安装路径。

## 本次验证

- Unity 运行时和编辑器代码通过独立 C# 编译，含本次新增的编辑器测试代码；另执行 C# 身份解析检查。
- 共享 TypeScript 和移动端动作控制器通过类型检查。九种头像三端内容一致，144 张图集、666 帧槽位完整；抽查 27 个合成帧的图层顺序与可见像素一致（透明合成舍入容差 1）。
- 七种 v4 绘制包的完整性、两端哈希与原画来源检查通过。
- 原有 Overlay 源码约束测试仍有四处已有哈希差异：IpcProtocol.cs、PondScenePresenter.cs、OverlayHudWidgets.cs、overlay-hud.json。已确认这四个文件与本次修改前 HEAD 一致；没有用更新哈希掩盖这些已有差异。
- 本轮未运行 Windows Player/Native Overlay，也未运行完整 React Native 应用；这些平台的视觉、点击和网络验收仍需实机完成。
