# Overlay 钓位座位图（STEAM-DESKTOP-14A）

```text
Assets/Resources/Desktop/Prefabs/OverlayPondActor.prefab   ← 座位唯一真相（改这里）
  └── actor-seat / actor-pet / actor-name / actor-status / actor-ring

Assets/Desktop/OverlayLayouts/<pondId>.prefab
  └── kind=spot（位置宿主）
        └── OverlayPondActor（预制体实例，联动模板）

OverlayResources/seats/_default.png        ← 缺图回退
OverlayResources/seats/<file>.png          ← 导出拷贝的椅图
```

## 美术流程

1. 编辑 `OverlayPondActor.prefab`：换椅图、把猫 / 名 / 状态 / 环对齐到椅面，保存。
2. Unity 菜单 **Fish Social → 补齐 Overlay 布局 Prefab**（把模板实例嵌到每个 spot；去掉旧的内联拷贝）。
3. 在塘 Prefab 里拖动每个 `spot` 到岸上（不要拆实例去改部件，除非该塘要单独覆盖）。
4. **Fish Social → Export Overlay Layout**。
5. 重启 Overlay 验收。

改模板后各塘实例会跟着变；仅 `spotId` / 物体名等由 Baker 写成实例覆盖。

## 运行时查找顺序（椅图）

1. 该钓位 `actor-seat.sprite`（或 spot.sprite）
2. `seats/_default.png`
3. 可选回退 `seats/<pondId>.png`
4. 黄色圆点（缺图打 Debug 日志，不崩）

猫落点优先 `actor-pet` 矩形中心。

空位 Opacity **0.40**；自己落座后空位隐藏；占用座位 Opacity **1.0**。

生成占位图：`python scripts/gen-overlay-seat-default.py`
