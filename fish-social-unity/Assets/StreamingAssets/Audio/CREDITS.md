# Fish Social audio — accepted effects v3 / music v5

Updated 2026-10-24. The user adopted the current v5 music together with all 46 previously accepted v3 effects and cat voices: “将当前方案应用到项目内，剔除旧版无效资源”. All 47 production WAVs and cue settings retain their approved hashes. Acceptance: docs/工程/声音/audio-22i-adoption.json; earlier effects approval: docs/工程/声音/audio-22i-sfx-acceptance.json. Windows hardware audio-visual latency remains unmeasured.

## Current music: 日光下的闲谈 / Sunlit Conversation

Original 90 BPM G-major composition, 32 bars A/A-prime/B/varied return, 85.333333 seconds. The first eight bars, 21.333333 seconds, are an exact PCM prefix with the same master gain. Upbeats, dotted rhythms, stepwise links, neighbours and three prepared suspensions develop a recurring motif. All decorations are explicitly annotated in score.json; theoretical checks do not certify taste or perceived quality.

The melody uses nine actual Meta Open Sound Vel02Pick light-picked nylon-guitar recordings; accompaniment uses twelve Vel01Finger recordings. Original pitches, no pitch shifting. Lead attack 1ms; no added delay/reverb, high-frequency shelf, compression or limiter. Gentle high-pass at80Hz for lead/65Hz for comp; comp low-pass6500Hz. Per-recording balancing preserves real string attacks. Accompaniment RMS is12dB below the lead stem. Full-master 4x true peak0.134974.

For G4/B4/D5, the first300ms 2.5–6.2kHz energy fraction in new light-pick source recordings is respectively8.527/11.261/9.450dB above old fingerstyle at the same pitch. This level-independent source comparison establishes a real timbre change, not human listening approval. The source method, fixed Git tree/blob identities and full checks are in docs/美术/声音/v5/assets/music-manifest.json.

## Recorded sources — CC0-1.0

Freesound derivatives use public HQ MP3 previews; guitar uses publisher OGG recordings, not claimed lossless masters. Runtime playback is offline, 44.1kHz PCM16, mono short sounds/stereo music. Publisher license: https://creativecommons.org/publicdomain/zero/1.0/ . Meta collection: https://github.com/facebookincubator/Meta-open-sound . Each download is pinned by SHA-256; guitar Git blobs and the official CC0 LICENSE are independently verified.

| Key | Creator | Source page | Input SHA-256 |
|---|---|---|---|
| paper_a | music_is_wiggly_air | https://freesound.org/people/music_is_wiggly_air/sounds/783672/ | `f52f8b1a0f1dd8554672da65b9c971780fb4fa11953df6ab26c48df11a592b49` |
| paper_b | Beussa | https://freesound.org/people/Beussa/sounds/749880/ | `4b38b5c58abb206d5b86b9d5b6a3acde8eea90c288ef194b4a67503fe659b4a9` |
| paper_many | alonlubenfeld | https://freesound.org/people/alonlubenfeld/sounds/275304/ | `6041a8ed6f69e15a085a373b5a400af2f5f523c4010edc2506fcbad32566b3f6` |
| pencil | theshuggie | https://freesound.org/people/theshuggie/sounds/371026/ | `1ca51538d4f95b09569c2e5d561f70c65d9f948cc262e84288082e8132538b9a` |
| whoosh | OMogadime | https://freesound.org/people/OMogadime/sounds/766759/ | `41b49cf1dc5186003f604cfa084e6c192a0da3e75fc9e0da9c723a635b49c9fa` |
| stamp | kermite607 | https://freesound.org/people/kermite607/sounds/362622/ | `31583ce19caf4f595b54565343d6267a5a311d736653bc25a9fe32c750305e58` |
| reel | mwchristian95 | https://freesound.org/people/mwchristian95/sounds/725424/ | `074981efc743288e5ab0d7d155dd791fc51ce64068bcca00bd11b894d40cbf3b` |
| gentle_water | roisin.gleeson | https://freesound.org/people/roisin.gleeson/sounds/783808/ | `37ff0b25669ebd70f12c20cf9237e5304f4777286302f72ebc802878ca4b36c7` |
| small_water | JoeDoe22 | https://freesound.org/people/JoeDoe22/sounds/462578/ | `6a67fbfe80421d76a93b3c8a8854152633a5c7de088a6349f63a610896ff36bd` |
| nylon_pick_StringHighE_D5 | Meta Creative Audio / Meta Platforms, Inc. | https://github.com/facebookincubator/Meta-open-sound/tree/251c0be57d5d1b3242b91f53e0cde62ab1cb42d9/systems/instruments/meta-nylon-guitar | `e05a71362854c6f003cdf2334fd9d840304c7779130c2a4039ac60f144dcc0f1` |
| nylon_pick_StringHighE_B4 | Meta Creative Audio / Meta Platforms, Inc. | https://github.com/facebookincubator/Meta-open-sound/tree/251c0be57d5d1b3242b91f53e0cde62ab1cb42d9/systems/instruments/meta-nylon-guitar | `696991a367361b169688835484f794a9284e36c8cc51c3d919587becc354a31d` |
| nylon_pick_StringHighE_A4 | Meta Creative Audio / Meta Platforms, Inc. | https://github.com/facebookincubator/Meta-open-sound/tree/251c0be57d5d1b3242b91f53e0cde62ab1cb42d9/systems/instruments/meta-nylon-guitar | `a78ac99f02659ad30bd6d4db24ca323b318d56bd09c0cb3271229aaf9f32d9b4` |
| nylon_pick_StringHighE_G4 | Meta Creative Audio / Meta Platforms, Inc. | https://github.com/facebookincubator/Meta-open-sound/tree/251c0be57d5d1b3242b91f53e0cde62ab1cb42d9/systems/instruments/meta-nylon-guitar | `1abac816b9da39f145881bda26d064d8fff7a93b63032ca5db1702c8df30c4d7` |
| nylon_StringLowE_G2 | Meta Creative Audio / Meta Platforms, Inc. | https://github.com/facebookincubator/Meta-open-sound/tree/251c0be57d5d1b3242b91f53e0cde62ab1cb42d9/systems/instruments/meta-nylon-guitar | `ad7558174f93be08f3f99c56fdbbda1700156751357160ce623ac43ad7e7f088` |
| nylon_StringG_G3 | Meta Creative Audio / Meta Platforms, Inc. | https://github.com/facebookincubator/Meta-open-sound/tree/251c0be57d5d1b3242b91f53e0cde62ab1cb42d9/systems/instruments/meta-nylon-guitar | `f02c4b08753e037e03e7445414159ebe515401361fb2a85c185b90f554931d31` |
| nylon_StringB_B3 | Meta Creative Audio / Meta Platforms, Inc. | https://github.com/facebookincubator/Meta-open-sound/tree/251c0be57d5d1b3242b91f53e0cde62ab1cb42d9/systems/instruments/meta-nylon-guitar | `dda8784fa36a36decddd7b39e729258755c89f74817bdfc18c842e5b50f02d7c` |
| nylon_StringD_D3 | Meta Creative Audio / Meta Platforms, Inc. | https://github.com/facebookincubator/Meta-open-sound/tree/251c0be57d5d1b3242b91f53e0cde62ab1cb42d9/systems/instruments/meta-nylon-guitar | `ece6dc347e579a9f7c0686596075ee1a45ce425586c9841a597e9518b2ed5b97` |
| nylon_StringA_B2 | Meta Creative Audio / Meta Platforms, Inc. | https://github.com/facebookincubator/Meta-open-sound/tree/251c0be57d5d1b3242b91f53e0cde62ab1cb42d9/systems/instruments/meta-nylon-guitar | `ec56cfbdc0659aebb17c17cd3fa38f598f44e158fd7307a630e411398cd2677f` |
| nylon_pick_StringHighE_E5 | Meta Creative Audio / Meta Platforms, Inc. | https://github.com/facebookincubator/Meta-open-sound/tree/251c0be57d5d1b3242b91f53e0cde62ab1cb42d9/systems/instruments/meta-nylon-guitar | `997452f7cd2f6b3248801aeb041266bcad75e5455d36b11885aba6a489c6bd6c` |
| nylon_pick_StringHighE_C5 | Meta Creative Audio / Meta Platforms, Inc. | https://github.com/facebookincubator/Meta-open-sound/tree/251c0be57d5d1b3242b91f53e0cde62ab1cb42d9/systems/instruments/meta-nylon-guitar | `821972fb813dad74b53ecfe615c60877f9138fa7a0a6a1b68132bb374a684867` |
| nylon_StringA_C3 | Meta Creative Audio / Meta Platforms, Inc. | https://github.com/facebookincubator/Meta-open-sound/tree/251c0be57d5d1b3242b91f53e0cde62ab1cb42d9/systems/instruments/meta-nylon-guitar | `1d2fc1967e765530d1182f11896fef16a52427b81eae3a5e3386aa56828bc2a2` |
| nylon_StringD_E3 | Meta Creative Audio / Meta Platforms, Inc. | https://github.com/facebookincubator/Meta-open-sound/tree/251c0be57d5d1b3242b91f53e0cde62ab1cb42d9/systems/instruments/meta-nylon-guitar | `bb708d759c86ead11a307da883e07aa81adb99b2f6c63dd9ee9f146529cf5c53` |
| nylon_StringB_D4 | Meta Creative Audio / Meta Platforms, Inc. | https://github.com/facebookincubator/Meta-open-sound/tree/251c0be57d5d1b3242b91f53e0cde62ab1cb42d9/systems/instruments/meta-nylon-guitar | `8421ff65db84f4ab166fb95a9b44805245601e49f36f59cd64e99de905c0faee` |
| nylon_pick_StringHighE_Fs4 | Meta Creative Audio / Meta Platforms, Inc. | https://github.com/facebookincubator/Meta-open-sound/tree/251c0be57d5d1b3242b91f53e0cde62ab1cb42d9/systems/instruments/meta-nylon-guitar | `fe8de0b661d676caab284d415cc16fffca01b3d310ef3c79e73b4d2dc1bc3a86` |
| nylon_StringD_Fs3 | Meta Creative Audio / Meta Platforms, Inc. | https://github.com/facebookincubator/Meta-open-sound/tree/251c0be57d5d1b3242b91f53e0cde62ab1cb42d9/systems/instruments/meta-nylon-guitar | `02469d96231bb334bbc8d9bd1a036facae393736311d0e204bc0a638c48f3c31` |
| nylon_StringB_C4 | Meta Creative Audio / Meta Platforms, Inc. | https://github.com/facebookincubator/Meta-open-sound/tree/251c0be57d5d1b3242b91f53e0cde62ab1cb42d9/systems/instruments/meta-nylon-guitar | `61921298564aa209f0e228dcf48e47fb5cb612e048c88cd66d99a212a33ed55f` |
| nylon_StringA_A2 | Meta Creative Audio / Meta Platforms, Inc. | https://github.com/facebookincubator/Meta-open-sound/tree/251c0be57d5d1b3242b91f53e0cde62ab1cb42d9/systems/instruments/meta-nylon-guitar | `bdccb28f3c24422351d9f8aece2c53efbb8b8ef69df571457aadc6612a882d9d` |
| nylon_StringLowE_E2 | Meta Creative Audio / Meta Platforms, Inc. | https://github.com/facebookincubator/Meta-open-sound/tree/251c0be57d5d1b3242b91f53e0cde62ab1cb42d9/systems/instruments/meta-nylon-guitar | `145ce2503392ccf2d042e3f4cdbcebeec8acf45178850eae8ab1988e6fafb623` |
| nylon_pick_StringHighE_E4 | Meta Creative Audio / Meta Platforms, Inc. | https://github.com/facebookincubator/Meta-open-sound/tree/251c0be57d5d1b3242b91f53e0cde62ab1cb42d9/systems/instruments/meta-nylon-guitar | `e34a00254690b2a3f46dc8e38878b04997d3aec499cc589d0a5053ad8ea6c1cc` |
| nylon_pick_StringHighE_Fs5 | Meta Creative Audio / Meta Platforms, Inc. | https://github.com/facebookincubator/Meta-open-sound/tree/251c0be57d5d1b3242b91f53e0cde62ab1cb42d9/systems/instruments/meta-nylon-guitar | `cc787624845dda57cbe6141c289637b58ca728df086c85f3834fe91e27a46161` |

## Approved effect processing

- Paper/pencil/stamp UI:145–540ms,49–120ms release,7ms onset;155–4300Hz bandpass and linear gain, no saturation. Distinct paper movements are foley, not literal fingertip recordings. The accepted v3 processing and source intervals remain in each asset record.
- Water:28 independent non-overlapping water events plus escape, sourced from gentle movements and small splashes/drips.170–3900Hz bandpass and smooth noise reduction capped at6dB, no hard gate/saturation/pitch shift. These are water foley, not real fish recordings.
- Cast and retrieve:real stick-air and fishing-reel recordings retained from v2.

## Approved cat voices

| File | Source | Nature |
|---|---|---|
| meow_01 | https://freesound.org/people/Breviceps/sounds/448084/ | Human vocal cat imitation, identified as performance by its author. |
| meow_02 | https://freesound.org/people/qubodup/sounds/813119/ | Real cat; derivative of Jofae Cat Call (Meow), published CC0 by qubodup. |
| meow_03 | https://freesound.org/people/Luke100000/sounds/476918/ | Real kitten, source interval10.94–11.57s. |

## Reproduction and history

1. `python tools/audio/compose_sunlight_v5.py --sources <cache>`
2. `python tools/audio/integrate_music_v5.py`
3. `python tools/audio/verify_desktop_audio.py`
4. `python tools/audio/verify_music_v5.py`

Python dependencies:numpy/scipy/soundfile. Downloads occur only on cache misses and verify pinned identities. The music-only installer checks all46 accepted WAVs, metadata and cue configuration before replacement. Obsolete installers have been removed. The installer checks the complete adoption hash lock before writing; --check-only validates without changes. Accepted effects and source metadata are self-contained in tools/audio/accepted-effects.json.

Latest preview:docs/美术/声音/v5/index.html. Superseded v1–v4 WAVs, baseline copies and preview implementations were removed at the user’s request. Old preview URLs redirect to v5. Historical source/credits/manifest records remain in docs/归档/2026-10-26/证据/工程/audio-22i-retired/; removed paths and hashes are inventoried in docs/工程/声音/audio-22i-cleanup.json. Current music is not the old piano or cello candidate. Score/source/PCM/browser checks are distinct from user BGM listening approval and Windows audio-visual latency measurement.
