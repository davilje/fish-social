# 2026-10-25 水效可见度调整

用户反馈颜色太淡，水花／波纹难以辨认。正式M档PNG的非透明像素中位不透明度：波纹约0.647，水花约0.988；旧WPF／Unity又乘最高0.55的不透明度，并提前线性淡出，造成重复削弱。

三端统一为最高0.95：最初6%时间快速显现，保持到40%，之后用smoothstep曲线淡出至0。纹理自带半透明仍保留，水线裁剪、触水时机及落点沿用已确认规则。Web预览原先采用另一条快速淡出曲线，本轮同步相同显示强度曲线。

验证：Web原有253项通过，浅／深水截图已检查；Unity DesktopFishingWaterEffectsTests针对真实v4资源及池化渲染的1项综合回归通过，退出0；WPF正式程序及原生测试项目交叉编译0错误。Windows原生执行未进行。此前2026-10-24发布zip为历史版本，本轮源代码与预览已更新。

- before-light.png / after-light.png：同帧浅水咬钩波纹。
- before-dark.png / after-dark.png：同帧深水鱼出水。
- web.log / unity.xml / unity.log / wpf.log / bindings.log：本轮证据。
