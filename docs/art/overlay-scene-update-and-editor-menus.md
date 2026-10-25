# 钓鱼 Overlay 场景更新与 Unity 菜单

## 日常更新

保存场景 Prefab 后，执行 **Fish Social → Overlay → 更新钓鱼场景到运行目录**。

该命令导出所有已保存的钓鱼布局，将 `desktop-overlay/OverlayResources` 的配套文件同步到 `fish-social-unity/FishSocialOverlay/OverlayResources`，逐字节确认复制结果。不会调用布局生成器重建已保存的场景；未保存的 Prefab 编辑会被提示先保存。重新启动 Overlay 后加载新资源。

当前新版分层池塘对应 `pond-novice`（城郊练杆塘）。10 个图层沿用同一坐标表，猫咪和座位保持已保存的对齐关系。2026-10-25 已替换为原版「柔软粉彩绘本」配色与纹理，见[替换记录](pond-soft-pastel-overlay.md)。本次更新为静态分层资源，环境动画仍是浏览器预览。

Windows 打包流程也会先导出已保存的场景布局，再复制资源，避免新 Prefab 搭配旧 JSON。该操作更新的是资源，不会重新编译本地 Overlay 可执行文件。使用其他安装目录时，需将同一份 OverlayResources 复制到实际执行文件旁。

## 菜单整理

当前共 23 个项目菜单命令，按用途归类：

- **UI Prefab 管理**：页面与组件的管理窗口。
- **Overlay**：更新运行目录、导出选中/全部已保存布局、导出 HUD、应用池塘模板和座椅资源。
- **预览**：钓鱼动画、UI 页面、鱼图鉴、小屋场景与动画、日记页面。
- **校验**：钓鱼场景与座位、HUD 导出、UI 页面、日记页面与素材。
- **资源**：宠物美术同步。
- **维护**：补齐钓鱼布局、修复 Overlay HUD。
- **打包**：Debug 与 Release Windows 构建。

14 个历史整页重建、旧版本应用、一次性素材准备及细节修补入口已从菜单隐藏。对应方法保留供现有调用和批处理使用。原来分散在 Window 下的钓鱼动画预览已归入项目预览菜单。

## 本次验证（2026-10-25）

- Unity 2021.3.29f1c1 编译及 UpdateFishingScene 批处理成功。
- 已更新 156 个资源文件；3,894 个源资源在运行目录存在且 SHA-256 一致。
- 21 关、420 个钓位、分层切图坐标和猫咪座位接触点检查通过。
- 菜单路径无重复，旧菜单分组已移除。
- Windows WPF Overlay 未在本机 macOS 上运行，需在 Windows 重启客户端查看。

详细同步日志与菜单变更记录位于 `/Users/red/Desktop/fish/output/overlay-menu-cleanup/`。
