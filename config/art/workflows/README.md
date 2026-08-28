# ComfyUI workflows（Fish Social）

| 文件 | 用途 |
|------|------|
| [fish-social-gpt-image-2-t2i.json](./fish-social-gpt-image-2-t2i.json) | **ComfyUI UI 工作流**（拖进画布 / Load） |
| [gpt-image-2-t2i.api.json](./gpt-image-2-t2i.api.json) | **API Format**，供 `npm run art:generate` 调用 |

## UI 工作流怎么用

1. 打开 `http://127.0.0.1:8188`
2. 菜单 **Load**，选本文件；或把文件拖进画布  
3. 在 **OpenAI GPT-Image-2** 节点填 `api_key`（或用节点已保存的配置）  
4. 改 `prompt` / `size`，点 **Queue Prompt**  
5. 结果：右侧 Preview + `ComfyUI/output/fish-social-art_*.png`

本机也可在 ComfyUI 工作流列表打开：

`F:\ComfyUI_windows_portable\ComfyUI\user\default\workflows\FishSocial-gpt-image-2-t2i.json`

## 脚本调用

```bash
npm run art:generate -- --preset desktop-cat --prompt "your subject"
```

脚本读的是 `gpt-image-2-t2i.api.json`，密钥来自项目根 `.env` 的 `OPENAI_API_KEY`。

若在 UI 改了节点结构，请 **Save (API Format)** 覆盖 `gpt-image-2-t2i.api.json`，并核对 `config/art/direction.json` 的 `nodes` 映射。
