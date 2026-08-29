# Overlay 场景布局表

`STEAM-DESKTOP-ART-02`：Unity Prefab 导出的像素布局。Overlay 按 `pondId` 读 `<pondId>.json`，有表则猫和钓位走像素坐标，无表回退 `MapToScene`。

坐标系：左上原点，Y 向下，画布 960×560。`kind=spot` 的 `x,y` 为猫脚底中心（`anchor=bottom-center`）。猫咪 Overlay **显示 64×64**；源图 256×256。`kind=pet-size` 默认 `w/h=64`（与显示槽一致）。

不要把 HUD 控件写进本目录（HUD 在 `../hud/overlay-hud.json`）。
