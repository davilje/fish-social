# Fish Social audio — STEAM-DESKTOP-22I v2

Produced 2026-10-24. Recorded-source palette; no synthetic bleeps or synthesized water. Runtime playback is entirely offline. All WAVs are 44.1 kHz PCM16; effects mono, music stereo. `manifest.json` records every output SHA-256, source ID, trim interval, processing and levels. `tools/声音/sources-v2.json` pins source downloads by SHA-256 and pinned Git blob/commit where applicable.

## Recorded sources (CC0-1.0)

Freesound public HQ MP3 previews are the actual derivative inputs (not claimed to be original lossless masters). Pages were checked for the CC0 license on 2026-10-24. The sample libraries identify their recordings as CC0. License: https://creativecommons.org/publicdomain/zero/1.0/

| Key | Creator | Original page | Input SHA-256 |
|---|---|---|---|
| water_bowls | NichelleMedia | https://freesound.org/people/NichelleMedia/sounds/614850/ | `9c08e48df9fcf47b65b99544035486a601e835d37ad868db6390c1873fa663c4` |
| water_stick | Phil25 | https://freesound.org/people/Phil25/sounds/207371/ | `7d1615959c99e55f2fac4804deb014edc8f6edf56e33375eba7ffefabd082e3e` |
| water_pour | discofield | https://freesound.org/people/discofield/sounds/793376/ | `f5babdc3b8fd9860b84226ff0a49e306dbc2ca7f24b6c3f307735ef7ccaf0327` |
| water_dash | codebolt7 | https://freesound.org/people/codebolt7/sounds/719970/ | `31cd7e8d3299bbb4dbf98ea58465b8044af88a5f3ab0465c26dd491852a66bd2` |
| water_lake | yurkobb | https://freesound.org/people/yurkobb/sounds/280219/ | `237a169a18640dc034a30048d6419d9ae6111e56b64ae5381f1357f9baa65f0e` |
| paper_a | music_is_wiggly_air | https://freesound.org/people/music_is_wiggly_air/sounds/783672/ | `f52f8b1a0f1dd8554672da65b9c971780fb4fa11953df6ab26c48df11a592b49` |
| paper_b | Beussa | https://freesound.org/people/Beussa/sounds/749880/ | `4b38b5c58abb206d5b86b9d5b6a3acde8eea90c288ef194b4a67503fe659b4a9` |
| paper_many | alonlubenfeld | https://freesound.org/people/alonlubenfeld/sounds/275304/ | `6041a8ed6f69e15a085a373b5a400af2f5f523c4010edc2506fcbad32566b3f6` |
| pencil | theshuggie | https://freesound.org/people/theshuggie/sounds/371026/ | `1ca51538d4f95b09569c2e5d561f70c65d9f948cc262e84288082e8132538b9a` |
| whoosh | OMogadime | https://freesound.org/people/OMogadime/sounds/766759/ | `41b49cf1dc5186003f604cfa084e6c192a0da3e75fc9e0da9c723a635b49c9fa` |
| piano_c3 | Versilian Studios / VCSL contributors | https://github.com/sgossner/VCSL | `eb52431464cc160cb7c3576d5087b6cc67326fd903b620088c0874f6bcff4a8e` |
| stamp | kermite607 | https://freesound.org/people/kermite607/sounds/362622/ | `31583ce19caf4f595b54565343d6267a5a311d736653bc25a9fe32c750305e58` |
| cello_c3 | Versilian Studios / VSCO-2-CE contributors | https://github.com/sgossner/VSCO-2-CE | `a614ee7ca821b44660236e47b89620cf914ec6ad27a99bc0d9764f47dbe512e1` |
| piano_c2 | Versilian Studios / VCSL contributors | https://github.com/sgossner/VCSL | `eba9adfb3977605a494830081d4e9e4ff0a4efd017439fe402a208aca9f11d7a` |
| reel | mwchristian95 | https://freesound.org/people/mwchristian95/sounds/725424/ | `074981efc743288e5ab0d7d155dd791fc51ce64068bcca00bd11b894d40cbf3b` |
| piano_c4 | Versilian Studios / VCSL contributors | https://github.com/sgossner/VCSL | `f921d4737ee847ecb55ce251a94e1841069b70e206b00342676954992ea7d0dc` |

## Processing and provenance

- Paper: independently recorded page movements cropped to 65–290 ms; the short click/tab cues are paper transients, not literal recordings of fingers tapping. `pencil` is pencil on paper; `stamp` is an actual stamp applied to paper. No electronic confirmation tones.
- Water: independent, non-overlapping source events; small bowl water movements, surface strikes with a stick, underwater gallon dashes, pouring, and a lake impact. 3 actions × 4 sizes × 2 variants plus 2 float contacts and 2 bait contacts = 28 assets. S/M/L/XL changes source event/body/duration, never water pitch. DC/high-pass cleanup, restrained low-pass, short fades and smooth transient compression where necessary; source interval and exact processing are in each manifest record. Bowl/pour/gallon recordings are physical foley, not recordings of actual fish.
- Cast: real stick swung through air (OMogadime); retrieve: real fishing reel (mwchristian95). Triggering is conditional on existing animation; files do not add visual effects.
- Music A / production `music/pond_daylight.wav`: original **池畔手记 / Pond Journal v2**, 68 BPM, C/Am/F/G, 24 bars, 84.7059 seconds. Sparse melody and bass using the softest Kawai grand-piano velocity samples from VCSL; mild low-pass softens attacks. This is sampled natural piano, not a recording of a felt piano. Circular note tails and short boundary blend make a continuous loop; no drums, bells or ambient loop.
- Music B preview: exactly the same melody and tempo, replaces piano bass with quiet sampled cello (VSCO-2-CE). Both previews are 7 bars / 24.7059 seconds. A is integrated as the quieter instrumentation choice; human sound-quality acceptance remains pending.
- VSCO 2 CE publisher license: https://versilian-studios.com/vsco-community/ ; VCSL license: https://github.com/sgossner/VCSL/blob/master/README.md . Credits: Sam Gossner / Versilian Studios, library contributors; cello recordings Sam Gossner & Simon Dalzell, sample cutting Elan Hickler / Soundemote.

## Retained cat voices

The same licensed calls from v1 are retained, with peak normalization from .48 to .30 to reserve full-volume mix headroom. The v1 originals and full earlier processing record remain in `docs/美术/声音/baseline-v1`.

| File | Source | Nature |
|---|---|---|
| meow_01 | https://freesound.org/people/Breviceps/sounds/448084/ | Human vocal cat imitation, explicitly identified as a performance by the author. |
| meow_02 | https://freesound.org/people/qubodup/sounds/813119/ | Real cat; derivative of Jofae's Cat Call (Meow), published CC0 by qubodup. |
| meow_03 | https://freesound.org/people/Luke100000/sounds/476918/ | Real kitten, original source 10.94–11.57 s. |

## Reproduction and verification

`python tools/声音/build_desktop_audio_v2.py --sources <cache>` (numpy, scipy, soundfile); downloads only on cache misses and rejects mismatched source hashes. The baseline cat WAVs are checked into the frozen v1 directory. `build_desktop_audio.py` is the historical v1 recipe and must not regenerate production v2.

`python tools/声音/verify_desktop_audio.py` checks hashes, formats, unique water variants, tier durations, decoded short-sound budget and loop seam. `python tools/声音/analyze_desktop_audio_v2.py` calculates 4× oversampled true-peak bounds at both full and default volumes and the effective-event RMS change. This is technical signal analysis, not headphone/speaker listening approval.
