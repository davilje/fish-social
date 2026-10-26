# Fishing rest / sleeping eyes

Completed 2026-10-24 for orange, calico, silver, brown, cream, blue, black, white, cow, dafeiyu and sangbiao. Orange received an internal visual review before the remaining batch. User acceptance is pending.

## Deliverables

- Native: `desktop-overlay/OverlayResources/fishing/{pet}/body/fishing/0/head-sleep.png`.
- Byte-identical mirror: `fish-social-unity/FishSocialOverlay/OverlayResources/fishing/{pet}/body/fishing/0/head-sleep.png`.
- Ten cat canvases are 640×512. Dafeiyu retains its actual 1280×1024 source canvas.
- Each PNG is a full-canvas optional replacement for the native waiting `head.png`, retaining its original alpha and pixel ownership. Do not interpret its name or RGB preview as a guarantee that it only contains a head. Hidden RGB is preserved too.
- Other layers, animations, runtime code, fishing tools, hats and accessories were not changed by this task. Runtime breathing, dozing, loading and cache behavior belong to the main implementation.

## Artwork and provenance

All new eye artwork came from the built-in `image_gen.imagegen` tool. The original waiting heads were inspected before editing. Orange and sangbiao used individual magnified face crops. Eight existing cat breeds used one registered 4×2 crop atlas, as authorized by the task. Dafeiyu used a dedicated magnified 200×100 eye crop; two earlier whole-face candidates were rejected and are retained for provenance only.

`provenance.json` lists all six calls, original tool save paths, exact saved prompts and selected/rejected status. The selected raw generations and input images are stored alongside this README. Original native PNG snapshots are in `originals/`. Generated artwork is never installed directly.

`tools/fishing-animation/build_sleep_faces.mjs` uses sharp to resize the generated crop back to its exact source rectangle and copy RGB only inside the two configured eye rectangles. A 3-pixel edge feather (4 for dafeiyu) blends the local seam. It does not draw eyes, repaint breeds, recolor, move body parts, resize the source canvas, or replace source alpha. All transformations and rectangles are explicit in `patches.json`; the final implementation uses no per-eye positional shift. Rebuild refuses changed source snapshots.

## Visual review and pixel checks

- `review-contact.png`: labelled original/sleep comparisons for all eleven.
- `{pet}-review.png`: 3× face comparisons (dafeiyu uses a full-face review crop).
- `patch-bounds-contact.png`: red QA rectangles show the exact allowed editing bounds. These guides are never part of delivered artwork.
- `verification.json`: per-pet source/generated/output SHA-256, dimensions, exact exclusive-max changed bounding box, changed pixel count, coverage percentage and mirror state.
- Final PNGs were decoded again and compared to originals. All eleven pass: exact canvas; exact alpha everywhere; zero changed RGBA pixels outside the two eye rectangles; output hashes and mirror bytes match.
- Visual review checked closed eyelids, removal of old open-eye glints, fur boundaries, facial marks, silhouette, mouth, head placement and patch seams. A small residual upper eyelid in the initial sangbiao composite was removed by widening its patch upward without touching the separate eyebrow fur mark.
- Coverage is approximately 0.53–0.80% of the full source canvas. Exact per-pet figures are in the verification report.

## Integration record: mirror bases

The mirror directory is confirmed as `FishSocialOverlay/OverlayResources`; neither `External/NativeOverlay` nor `Assets/StreamingAssets/DesktopOverlay` exists here.

At final build, orange and sangbiao mirror `head.png` matched native. Silver, brown, cream and blue mirror bases were older, visibly different art. Calico, black, white, cow and dafeiyu had no mirror base `head.png`. Their new sleep PNGs are correctly copied from the native-derived result, but the corresponding base packs must be synchronized by the runtime/integration owner before switching between old mirror art and these new variants. This task did not overwrite unrelated base assets. `mirrorSourceMatches` is true, false or null (absent) and captures this distinction.

Integration follow-up on 2026-10-24: the main implementation synchronized the existing ten native fishing packs and shared props into the Unity companion Overlay folder (2,828 missing or different files). The initial `mirrorSourceMatches` values above are historical artwork-build observations, not the final mirror state. Sangbiao is installed by its complete-pack exporter.

Unity Resources mapping and PNG metadata are assigned to chat `01a16c30-4e65-7360-b436-c9e75cdc2611`; that owner was notified to copy the final files and use matching native bases. Sangbiao's owner supplied the registered native waiting head before its edit.

## Reproduction

With Node and sharp available:

```sh
node tools/fishing-animation/build_sleep_faces.mjs build
node tools/fishing-animation/build_sleep_faces.mjs verify
```

If sharp is outside normal module resolution, set `SHARP_MODULE` to its absolute package directory. Build uses saved generated files and requires no network or image generation. The verify command is read-only.

