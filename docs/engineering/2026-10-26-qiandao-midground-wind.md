# Qiandao: middle-ground wind and three repeating cloud layers

The pond-calm Overlay previously animated only the two near canopies, with one cloud layer oscillating left/right. It now uses six additional foliage cutouts, three separate cloud textures travelling right at 1.6/3.2/5.2 source pixels per second, and 7.2 source-pixel wind amplitude (3.27 times the previous 2.2).

## Resources and rendering

Canonical art: `fish-social-unity/Assets/Desktop/OverlayArt/Ponds/layered/qiandao-wind-v3/`. Catalog and pond-calm prefab/layout point to its 16 layers. The source canvas remains 2172×724, exported scene 1680×560. Middle bushes use their own wind-weight texture and offset patch rectangles; near canopies keep their existing root weights. Bush source pixels are extracted from the previous scene. Hidden land has a clean repair plate, and three transparent cloud strips are separately drawn. No global color adjustment is applied.

Motion manifest version 2 adds exactly three `clouds` entries (`id`, positive `speed`, optional `phase` in source pixels) and optional foliage `top`, `weightTexture`, `primaryPeriod`. Version 1 still loads. The exporter copies every referenced wind map. At runtime each cloud has two adjacent copies clipped to its original scene rectangle. Modulo travel always advances right; no sine reversal, fade-out, gap or new image allocation at wrap. The foliage renderer keeps roots outside each patch fixed and uses the existing 24 Hz visibility-aware clock with one in-flight worker and reusable buffers.

## Validation

- Pure motion tests passed: offset patch sampling, fixed roots, alpha edges, positive travel and wrap continuity for all three speeds, plus legacy behavior.
- Windows WPF app and native lifecycle tests compile. Native tests cover two-copy cloud geometry, per-group weight loading, vertical patch offsets, hide/resume/dispose and cancellation. These Windows tests cannot execute on this macOS host.
- Browser preview checked 8 phases from 0 to 1358 seconds: no alpha holes, fixed foreground/bank, no JS/WebGL errors. Each cloud returns to the same rendered placement after its cycle (maximum 1/255 rounding variation).
- Art verification checks 21 pond layouts and 420 seats. Final installation evidence records other pond layouts and gameplay coordinates unchanged, and matching source/bundled resources.
- Windows x64 self-contained output rebuilt in `fish-social-unity/FishSocialOverlay`; an already installed player must be repackaged or receive the updated runtime and resources.

Preview and detailed evidence: `/Users/red/Desktop/fish/output/scene-revision/qiandao-wind-v3/`.
