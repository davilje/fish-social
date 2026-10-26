# Fish Social audio — STEAM-DESKTOP-22I: approved effects v3 / music v4

Produced 2026-10-24 following listening feedback. Complete paper tails, clearer UI, gentle recorded water, and an original sunny nylon-guitar score with **no piano**. Offline playback; 44.1 kHz PCM16, mono effects and stereo music. `manifest.json` records output hashes, source IDs and processing.

## Acceptance and current music revision

The user accepted all 46 non-music v3 effects and cat calls on 2026-10-24. Their WAVs, asset metadata and cue settings remain byte-identical. The v3 music was rejected as disorganized; it is frozen in baseline-v3 for comparison. **Only music is revised to v4; music listening acceptance is pending.** See docs/工程/声音/audio-22i-sfx-acceptance.json for the original feedback and accepted hashes.

Music v4 is the original **池畔日光·简主题 / Sunlight on the Pond — simple theme**: 88 BPM, G major, 32 bars A/A-prime/B/A, 87.272721 seconds. Primary harmony G–C–D–G and contrasting Em–Am–D–G generally holds each chord for two bars. A fixed fingerstyle accompaniment sits 12 dB below the melody stem; one global melody voice has explicit rests and releases. Every note gate includes its tiny room reflection, so previous harmony cannot ring across the boundary. No piano or cello. The 21.818186-second preview is the exact first eight bars of the full master. Musical score, source pins and objective audit are in docs/美术/声音/v4/assets/music-manifest.json and score.json.

## Source licenses

All listed sources are CC0-1.0. Freesound derivatives use public HQ MP3 previews; Meta nylon guitar uses publisher OGG recordings, not lossless masters. Cello is a VSCO 2 CE WAV. Source download URLs and SHA-256 are pinned in the manifest. License: https://creativecommons.org/publicdomain/zero/1.0/

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
| nylon_StringHighE_G4 | Meta Creative Audio / Meta Platforms, Inc. | https://github.com/facebookincubator/Meta-open-sound/tree/251c0be57d5d1b3242b91f53e0cde62ab1cb42d9/systems/instruments/meta-nylon-guitar | `fb75da29d5b76d90f617a9f5ba5813e0bed4cac8c087741b6f78d5b586d20a30` |
| nylon_StringHighE_B4 | Meta Creative Audio / Meta Platforms, Inc. | https://github.com/facebookincubator/Meta-open-sound/tree/251c0be57d5d1b3242b91f53e0cde62ab1cb42d9/systems/instruments/meta-nylon-guitar | `e432ba2c6f43464c964b254186827a3087c89661513eb306ddc8cbbdfc37f43c` |
| nylon_StringHighE_D5 | Meta Creative Audio / Meta Platforms, Inc. | https://github.com/facebookincubator/Meta-open-sound/tree/251c0be57d5d1b3242b91f53e0cde62ab1cb42d9/systems/instruments/meta-nylon-guitar | `ff4de1840951cf2e628622581b89d22cd926dbe983a8368abdbce8fdbf325edd` |
| nylon_StringLowE_G2 | Meta Creative Audio / Meta Platforms, Inc. | https://github.com/facebookincubator/Meta-open-sound/tree/251c0be57d5d1b3242b91f53e0cde62ab1cb42d9/systems/instruments/meta-nylon-guitar | `ad7558174f93be08f3f99c56fdbbda1700156751357160ce623ac43ad7e7f088` |
| nylon_StringG_G3 | Meta Creative Audio / Meta Platforms, Inc. | https://github.com/facebookincubator/Meta-open-sound/tree/251c0be57d5d1b3242b91f53e0cde62ab1cb42d9/systems/instruments/meta-nylon-guitar | `f02c4b08753e037e03e7445414159ebe515401361fb2a85c185b90f554931d31` |
| nylon_StringB_B3 | Meta Creative Audio / Meta Platforms, Inc. | https://github.com/facebookincubator/Meta-open-sound/tree/251c0be57d5d1b3242b91f53e0cde62ab1cb42d9/systems/instruments/meta-nylon-guitar | `dda8784fa36a36decddd7b39e729258755c89f74817bdfc18c842e5b50f02d7c` |
| nylon_StringD_D3 | Meta Creative Audio / Meta Platforms, Inc. | https://github.com/facebookincubator/Meta-open-sound/tree/251c0be57d5d1b3242b91f53e0cde62ab1cb42d9/systems/instruments/meta-nylon-guitar | `ece6dc347e579a9f7c0686596075ee1a45ce425586c9841a597e9518b2ed5b97` |
| nylon_StringHighE_A4 | Meta Creative Audio / Meta Platforms, Inc. | https://github.com/facebookincubator/Meta-open-sound/tree/251c0be57d5d1b3242b91f53e0cde62ab1cb42d9/systems/instruments/meta-nylon-guitar | `3fdd6e8979442633ed2093a8584df66b03109e3926714fa91b6eeeed062ee03c` |
| nylon_StringHighE_C5 | Meta Creative Audio / Meta Platforms, Inc. | https://github.com/facebookincubator/Meta-open-sound/tree/251c0be57d5d1b3242b91f53e0cde62ab1cb42d9/systems/instruments/meta-nylon-guitar | `df6ebe090857222e3f7679f696f1c54a69248211f9b3f7e561e617fbf4350d13` |
| nylon_StringHighE_E5 | Meta Creative Audio / Meta Platforms, Inc. | https://github.com/facebookincubator/Meta-open-sound/tree/251c0be57d5d1b3242b91f53e0cde62ab1cb42d9/systems/instruments/meta-nylon-guitar | `57f9c073cd1ee1365857151fb378c9fa452942eaf8a08de7d2a3a7beb12134a8` |
| nylon_StringA_C3 | Meta Creative Audio / Meta Platforms, Inc. | https://github.com/facebookincubator/Meta-open-sound/tree/251c0be57d5d1b3242b91f53e0cde62ab1cb42d9/systems/instruments/meta-nylon-guitar | `1d2fc1967e765530d1182f11896fef16a52427b81eae3a5e3386aa56828bc2a2` |
| nylon_StringD_E3 | Meta Creative Audio / Meta Platforms, Inc. | https://github.com/facebookincubator/Meta-open-sound/tree/251c0be57d5d1b3242b91f53e0cde62ab1cb42d9/systems/instruments/meta-nylon-guitar | `bb708d759c86ead11a307da883e07aa81adb99b2f6c63dd9ee9f146529cf5c53` |
| nylon_StringB_C4 | Meta Creative Audio / Meta Platforms, Inc. | https://github.com/facebookincubator/Meta-open-sound/tree/251c0be57d5d1b3242b91f53e0cde62ab1cb42d9/systems/instruments/meta-nylon-guitar | `61921298564aa209f0e228dcf48e47fb5cb612e048c88cd66d99a212a33ed55f` |
| nylon_StringHighE_Fs4 | Meta Creative Audio / Meta Platforms, Inc. | https://github.com/facebookincubator/Meta-open-sound/tree/251c0be57d5d1b3242b91f53e0cde62ab1cb42d9/systems/instruments/meta-nylon-guitar | `4af385c3178a8f091a9eca1deb01bb15b65b468ef156f5075338e8aeeb2ee039` |
| nylon_StringG_A3 | Meta Creative Audio / Meta Platforms, Inc. | https://github.com/facebookincubator/Meta-open-sound/tree/251c0be57d5d1b3242b91f53e0cde62ab1cb42d9/systems/instruments/meta-nylon-guitar | `b920b3aa7eb3597acd7a7f1498f082785feb537a548fcf20cb076aee1ebca96c` |
| nylon_StringD_Fs3 | Meta Creative Audio / Meta Platforms, Inc. | https://github.com/facebookincubator/Meta-open-sound/tree/251c0be57d5d1b3242b91f53e0cde62ab1cb42d9/systems/instruments/meta-nylon-guitar | `02469d96231bb334bbc8d9bd1a036facae393736311d0e204bc0a638c48f3c31` |
| nylon_StringA_A2 | Meta Creative Audio / Meta Platforms, Inc. | https://github.com/facebookincubator/Meta-open-sound/tree/251c0be57d5d1b3242b91f53e0cde62ab1cb42d9/systems/instruments/meta-nylon-guitar | `bdccb28f3c24422351d9f8aece2c53efbb8b8ef69df571457aadc6612a882d9d` |
| nylon_StringHighE_E4 | Meta Creative Audio / Meta Platforms, Inc. | https://github.com/facebookincubator/Meta-open-sound/tree/251c0be57d5d1b3242b91f53e0cde62ab1cb42d9/systems/instruments/meta-nylon-guitar | `7743bd063c273a2e9e01252d74352ab86e0fc6d14a8206e9f53d5d2f98db0e65` |
| nylon_StringLowE_E2 | Meta Creative Audio / Meta Platforms, Inc. | https://github.com/facebookincubator/Meta-open-sound/tree/251c0be57d5d1b3242b91f53e0cde62ab1cb42d9/systems/instruments/meta-nylon-guitar | `145ce2503392ccf2d042e3f4cdbcebeec8acf45178850eae8ab1988e6fafb623` |
| nylon_StringA_B2 | Meta Creative Audio / Meta Platforms, Inc. | https://github.com/facebookincubator/Meta-open-sound/tree/251c0be57d5d1b3242b91f53e0cde62ab1cb42d9/systems/instruments/meta-nylon-guitar | `ec56cfbdc0659aebb17c17cd3fa38f598f44e158fd7307a630e411398cd2677f` |

## Processing and score

- UI: distinct paper movements, pencil and stamp; 145–540 ms, 49–120 ms cosine-squared release, 7 ms onset. Bandpass 155–4300 Hz and linear gain only. Event RMS is 4.58–7.27 dB above frozen v2 at the same cue gains. No saturation or artificial confirmation tones.
- Water: 28 non-overlapping recorded water events plus escape, selected from gentle water movements (roisin.gleeson) and water splashes/drips (JoeDoe22). These are water foley, not recordings of real fish. 170–3900 Hz bandpass and smooth spectral floor subtraction capped at 6 dB; no hard gate, saturation, peak compression or pitch shifting. 12 ms onset and 90–120 ms release. S/M/L/XL uses distinct events and duration rather than amplified heavy impacts. Water no longer targets the superseded v2 RMS increase.
- Cast and retrieve remain real stick-air and fishing-reel recordings, unchanged from v2.
- Archived v3 music (comparison only): original **阳光落在鱼塘 / Sunlight on the Pond**, 86 BPM, G major with 6/add9 colors, 32 bars / 89.302336 seconds, A/B/C/A-prime form. Two parts use soft fingerstyle Meta Open Sound nylon guitar, 21 real sampled pitches without guitar pitch shifting; restrained room reflections, string choking and circular tails. No piano, bells, synthesized oscillator or heavy drum beat. Former v3 production was guitar A; current production uses v4 above.
- Archived v3 A/B previews are the same first 8 bars / 22.325578 seconds; B adds real VSCO 2 CE cello for warmth. These are historical candidates, not current production.
- Meta Open Sound publisher: https://github.com/facebookincubator/Meta-open-sound ; CC0 instrument license and exact Git blob provenance are retained in music-manifest.json. VSCO 2 CE: https://versilian-studios.com/vsco-community/ ; recordings Sam Gossner & Simon Dalzell, sample cutting Elan Hickler / Soundemote.

## Retained cat voices

Identical to v2 (peak .30); earlier provenance remains in baseline-v1 and baseline-v2.

| File | Source | Nature |
|---|---|---|
| meow_01 | https://freesound.org/people/Breviceps/sounds/448084/ | Human vocal cat imitation, identified as performance by the author. |
| meow_02 | https://freesound.org/people/qubodup/sounds/813119/ | Real cat; derivative of Jofae Cat Call (Meow), published CC0 by qubodup. |
| meow_03 | https://freesound.org/people/Luke100000/sounds/476918/ | Real kitten, original interval 10.94–11.57 seconds. |

## Reproduction

1. `python tools/声音/compose_sunlight_v4.py --sources <cache>`
2. `python tools/声音/integrate_music_v4.py`
3. `python tools/声音/verify_desktop_audio.py`
4. `python tools/声音/verify_music_v4.py`

Requires numpy, scipy and soundfile. Downloads only on cache misses and verifies hashes. Frozen v2/v3 assets are in docs/美术/声音/baseline-v2 and baseline-v3. Old v1/v2/v3 generators refuse to overwrite newer production. The music-only installer verifies every approved effect and never rebuilds those files. Preview: docs/归档/2026-10-26/替代/美术/声音/v4/index.html. Signal verification does not imply headphone/speaker sound-quality approval or Windows synchronization acceptance.
