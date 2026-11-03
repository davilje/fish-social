# 第八版：猫爬架原位水平翻转

工具：内置 image_gen；编辑输入：room-pastel-v7.png。

```text
Use case: precise-object-edit.
Input image is the edit target, a finished pastel illustrated room background. Do exactly one local edit: HORIZONTALLY MIRROR the existing cat climbing tree in the lower-left corner IN PLACE, about its own vertical centerline, not the room. The cat climbing tree currently has its highest platform on the right and its lowest platform on the left. After this edit, its highest platform must be on the LEFT, its middle platform in the middle, and its lowest platform on the RIGHT. All three posts, padded green platforms and wooden base must be mirrored as one coherent object. Keep the tree's overall bounding rectangle, scale and grounding unchanged, approximately x25..340,y489..854 on the 1672x941 original design canvas. Preserve its colors, construction, edges, texture, perspective and three levels; only reverse its left/right orientation. Carefully restore the tiny wall/floor areas newly exposed behind it.
Strictly preserve the rest of the entire room: window and curtain, central bed and rug, fishing rods and cabinet, aquarium and shelf, framed picture and hanging plant, door, food bowls on the right, toy mouse beside the tree, all floor planks, original lighting and palette. Do NOT mirror or reposition any other object. Do not add objects. No cats or UI. Same complete room and same wide canvas dimensions, no global repaint, no recoloring, no extra detail.
```
