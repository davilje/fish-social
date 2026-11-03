# 前景布局更新 v6

使用内置 image_gen，按用户连续调整逐步编辑完整背景。

## 初次前移

Use case: precise-object-edit.
Edit target: current approved 1672x941 pastel storybook cat home interior.
User request: move the CAT BED, CAT CLIMBING TREE and CAT FOOD/WATER BOWLS forward into the near-floor foreground area.

Change ONLY their placement and repair the floor behind them:
1. Left round mint/cream cat bed: move the entire bed approximately 115–125 pixels DOWN toward the viewer; its new bounding rectangle should be roughly x25–360, y650–790. Keep same design, size, silhouette, cushion and colors.
2. Left multi-level wood/sisal cat climbing frame (including its three green-cushion platforms, posts and bases): shift the WHOLE connected structure approximately 110–125 pixels DOWN. Keep it in the LEFT zone, behind the cat bed with physically logical overlap. New top platform around y490, all bases grounded around y750–780. No floating platforms, no detached post pieces. Keep same height, width and design; do not turn this into a different object.
3. Pair of bowls on small wooden feeding tray near right door: move approximately 110–125 pixels DOWN onto near right floor, around x1420–1585,y715–785. Preserve both bowls and tray designs/sizes.

All three are visibly in front of the back-wall furniture line, at comparable foreground ground depth around y775. They remain ABOVE the bottom UI safe band y840–941. Nothing cropped by canvas. Keep central oval green rug and central open cat activity area intact, preserve its existing location/scale. Avoid crowding central rug or blocking door access.
Reconstruct their former locations with matching clean floor/wall continuation, no ghost furniture, holes or pasted seams. Bed sits in FRONT of climbing posts, posts correctly meet their wooden bases, everything grounded with only the tiny soft contact shadows already used.

STRICT preserve all other contents: window/outdoor lake, curtain, left potted flowers, fishing rods/stand/hat, framed picture, aquarium/bench/toy basket, shelf/vine, door, rug, walls and floor. Same camera, same perspective, same exact 1672x941 framing, same quiet pastel mint/cream/warm wood palette and even ambient lighting. Same clean large color areas and sparse painterly texture as target. No new texture/noise, wood grain, mottled sunlight or strong cast shadows. No cats, UI, labels, new furniture, duplicates or border.
Output one complete background painting; furniture remains baked into it, not separate cutouts.

## 中央猫窝

Use case: precise-object-edit.
Edit the provided room painting with ONE layout correction requested by the user: CAT BED IN THE MIDDLE, CAT CLIMBING TREE ON THE LEFT.

Move ONLY the mint/cream round cat bed from lower-left floor onto the CENTRAL OVAL RUG, centered approximately x835,y748, with bounds about x665–1005,y675–815 on the 1672x941 canvas. Preserve the bed's exact design, approx 334x145 size, colors and soft cushion. It is naturally resting on the rug, facing the same way. Restore its previous lower-left position with clean matching floor. No duplicate bed.
Keep the entire existing cat climbing frame on the LEFT in its current foreground position, its posts and platforms connected, lower bases around y710. Do not move it to center or back against the wall.
Keep the existing bowl pair and tray on the RIGHT foreground at their current x1400–1605,y700–780 placement.
Keep existing oval rug shape, location and size. Central bed has enough visible rug around it and stays above bottom UI safe band y840–941. Preserve original room perspective and even indoor light with small soft contact shadows only.
All other objects and colors are unchanged: window, curtain, flowers, fishing rods and cabinet, aquarium and bench, toy basket, wall picture, hanging plant, green door, wooden floor. Same clean pastel style and low texture density; no added noisy detail or sunlight shadows. Same exact 1672x941 full background. No cat, UI, text, borders, extra props or duplicate bed. Furniture remains in ONE complete painting.

## 左侧纵向猫爬架并移除花盆（最终）

Use case: precise-object-edit.
Edit target: current 1672x941 pastel storybook cat room with round mint bed CENTERED ON RUG and bowl pair RIGHT FOREGROUND.
Latest user request: arrange the CAT CLIMBING FRAME VERTICALLY ON THE LEFT; REMOVE LEFT FLOWERPOT.

1. Completely remove the left floor potted flowering plant (all white flowers, leaves and ceramic pot), restore the original cream wall/wood floor/curtain edge cleanly.
2. Redesign ONLY the placement/arrangement of the existing left climbing frame into a NARROW VERTICAL ascending stack on the far-left floor: about x90–325, top around y390–430, bottom base y760–785. Three green-cushion platforms stagger upward one above another, with small left/right offsets within this narrow footprint. Highest platform is at top, middle platform midway, low step nearest floor. Connected warm wooden/sisal posts with credible support under each platform and a single stable low floor base. This is one coherent cat tree, no separate stool spread toward the middle. Approx width230, height360–390. Keep original warm wood, cream rope, sage cushion design language and same soft simplified brushwork. Avoid tall top-heavy/crooked/floating structure; stable base and grounded contact.
3. Preserve the centered mint/cream bed on rug (around x665–1005,y660–810), and right foreground bowls/tray x1400–1605,y700–780. Do not move them. Keep central rug and middle passage readable.

All other room contents and palette are preserved: window/lake scene, curtains, fishing rods and low rack, hat, wall picture, aquarium and toy bench, wall shelf/vine, right door, floorboards. Same camera, exact composition and 1672x941 dimensions. Keep original clean pastel rendering with broad continuous colors, even ambient indoor lighting and tiny contact shadows; no added detail, noise, color shift, wooden texture, sunlight patch or strong cast shadow. No cats, UI, labels, borders, duplicate furniture or new flowers. Output ONE complete painting.

