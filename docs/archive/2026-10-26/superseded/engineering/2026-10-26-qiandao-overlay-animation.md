# 千岛湖 Overlay 环境动效

千岛湖（pond-calm）现使用 8 个背景景层。云缓慢水平漂移，树叶按权重轻晃，下部主干与树根固定。其他 20 个钓鱼布局保持原样。

## 接入

美术来源：`/Users/red/Desktop/fish/output/scene-revision/qiandao-animated-v2/`。Unity 权威素材位于 `Assets/Desktop/OverlayArt/Ponds/layered/qiandao-animated-v2/`，catalog 的 pond-calm 绑定、保存的 pond-calm.prefab、导出的布局和运行目录均已更新。

背景旁的 motion.json 指定云层 ID、树层 ID、原图尺寸、权重纹理和位移参数。OverlayLayoutExporter 导出背景时自动携带 motion.json、权重、图层布局与预览，日常“更新钓鱼场景到运行目录”不会丢失动效依赖。

PondScenePresenter 原子发布装饰层，并按保存的顺序替换树层为固定根部与活动树冠组合。所有环境景层仍位于 DecorLayer（Z=2）；角色、座位、鱼线的层级和几何不变。场景切换取消旧素材加载并释放旧动效；没有 motion.json 的场景保持静态。

## WPF 实现

OverlaySceneMotion 复用全局动画时钟，24 Hz 更新。云使用 TranslateTransform；树叶使用与网页预览等价的局部位移、双线性采样和预乘 BGRA，避免透明边缘黑边。原图坐标统一映射至 1680 × 560 场景，不随窗口大小更换运动幅度。

树冠计算放在后台工作项中，最多一个任务在途，复用像素缓冲区，不堆积过期帧；UI 线程仅发布局部 WriteableBitmap。窗口隐藏停止订阅，恢复从暂停时间继续，切换或销毁后旧任务不再写入场景。动效加载失败时保留静态景层。

云参数：±12 原图像素，160 秒周期。叶动参数：最大 2.2 原图像素，垂直比例 0.26，周期 7.5 / 4.2 / 9.3 秒；Y≥300 权重为零。移动区域包含左右边缘余量，下部树干用原图静态显示。

## 验证与发布

- Unity 批处理导入、保存、导出、渲染及同步成功。
- verify-pond-art.py 通过：21 个布局、420 个钓位，座位/入场/水面坐标不变；另 20 个布局与更新前 JSON 一致。
- 3969 个源资源与 Unity 本地 Overlay 运行目录逐文件一致。
- 动效内核 8 项、座位运动 10 组、调度 10 组、水面接触 13 组检查通过。
- 使用真实左树素材在 t=1.8 秒对比网页 WebGL：预乘通道最大差异 1/255。macOS .NET 8 下左树计算约 1.54 ms/帧；该数字不是 Windows 端性能结论。
- WPF 主程序与原生性能测试项目编译成功；原生测试增加几何、层级、隐藏/恢复、销毁、取消与静态场景检查。当前 macOS 无法执行 WPF，Windows 实际合成及原生测试尚待确认。
- 已发布 win-x64 self-contained Release 到 `fish-social-unity/FishSocialOverlay/`。需重新打包，或将整个该目录同步到 Windows 后重启 Overlay；只复制新 PNG 不会启用代码动效。

详细安装记录：`/Users/red/Desktop/fish/output/scene-revision/qiandao-animated-v2/overlay-install-validation.json`。背景补绘与原图存在已说明的差异，沿用已确认的 v2 预览素材，本次没有重新生成图像。
