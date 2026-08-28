# ComfyUI + gpt-image-2 本地对接

本机 ComfyUI 已在运行；仓库通过 HTTP API（默认 `http://127.0.0.1:8188`）提交工作流，调用 **gpt-image-2** 远程出图。

> 说明：当前实测进程路径为 `F:\ComfyUI_windows_portable`（非 G:）。请在 `.env` 里填真实路径；下面默认值已按实测路径写。

## 前置条件

| 项 | 要求 |
|----|------|
| ComfyUI 在线 | 浏览器打开 `http://127.0.0.1:8188` |
| Custom node | `ComfyUI-APIimage`（已克隆到 `custom_nodes/`） |
| OpenAI 密钥 | `.env` 中 `OPENAI_API_KEY`（勿提交 git） |

## 安装 / 确认节点

```powershell
cd F:\ComfyUI_windows_portable\ComfyUI\custom_nodes
git clone https://github.com/AyinMostima/ComfyUI-APIimage.git
```

**必须重启 ComfyUI**（关掉再跑 `run_nvidia_gpu.bat`），否则节点不会注册。

重启后在 Node Library 搜索 **OpenAI Image Generate**，确认 `model` 可选 `gpt-image-2`。

也可用仓库脚本探测：

```bash
npm run art:comfyui:health
```

应看到 `APIImage_OpenAIGenerate` 出现在节点列表中。

## 手跑验证（推荐一次）

仓库已带可导入的 UI 工作流：

- 仓库：[`config/art/workflows/fish-social-gpt-image-2-t2i.json`](../../../config/art/workflows/fish-social-gpt-image-2-t2i.json)
- 本机：`F:\ComfyUI_windows_portable\ComfyUI\user\default\workflows\FishSocial-gpt-image-2-t2i.json`

1. ComfyUI → **Load** 上述文件（或从工作流列表打开 `FishSocial-gpt-image-2-t2i`）
2. 在 **OpenAI GPT-Image-2** 节点填 `api_key`
3. Queue Prompt，确认 Preview 有图，且 `ComfyUI/output/fish-social-art_*.png` 落盘

结构：`APIImage_OpenAIGenerate` → `SaveImage` + `PreviewImage`。

## 导出 API 工作流（可选刷新）

脚本用的 API 图：[config/art/workflows/gpt-image-2-t2i.api.json](../../../config/art/workflows/gpt-image-2-t2i.api.json)。

若你在 UI 改了节点结构：

1. Settings → Enable Dev mode  
2. **Save (API Format)**  
3. 覆盖 `config/art/workflows/gpt-image-2-t2i.api.json`  
4. 更新 [config/art/direction.json](../../../config/art/direction.json) 里各 preset 的 `nodes` 映射（`prompt` / `size` / `api_key` / `base_url` 对应的 node id）

## `.env` 示例

```bash
COMFYUI_BASE_URL=http://127.0.0.1:8188
COMFYUI_ROOT=F:/ComfyUI_windows_portable/ComfyUI
COMFYUI_OUTPUT_DIR=F:/ComfyUI_windows_portable/ComfyUI/output
OPENAI_API_KEY=sk-...
# 可选代理 / 兼容网关
# OPENAI_BASE_URL=https://api.openai.com
```

## 出图命令

```bash
npm run art:comfyui:health
npm run art:generate -- --preset desktop-cat --prompt "orange tabby cat fishing, side view"
```

结果落在 `docs/art/generated/<date>/<preset>-<timestamp>/`，清单写入 `docs/art/generated/manifest.json`。

正式资源经美术审核后，再手动迁入 `fish-social-unity/Assets/` 或 `mobile/assets/`。
