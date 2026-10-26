# Fish Social audio credits

## Original music and interaction sounds

- **池畔晴日 / Pond Daylight** — original instrumental composition created for Fish Social, 72 BPM, C major, 24 bars, 80 seconds, stereo seamless loop. Synthesized piano/mallet tones and a quiet sustained accompaniment; no vocals or drums.
- UI click/open/close/toggle/tick/success/error, cast, splash and catch — original procedural sound design created for Fish Social.
- Rebuild recipe: `tools/声音/build_desktop_audio.py` (NumPy, SciPy, SoundFile). Game playback is entirely offline; Python is not required to run the game.

## Cat voice recordings (CC0 / public domain)

All three source pages were checked on 2026-10-23 and mark their sound as Creative Commons 0. Derivatives are mono 44.1 kHz PCM WAV, with DC/high-pass cleanup, level adjustment and short fades. The first two use the complete short recording; the third is a trimmed individual call. Freesound public HQ previews were used as the source audio.

| File | Recording / author | Source | Processing |
| --- | --- | --- | --- |
| `sfx/meow_01.wav` | Cute Kitten Meow — Breviceps | https://freesound.org/people/Breviceps/sounds/448084/ | Full 0.604 second vocal cat imitation (the author identifies it as a vocal performance). |
| `sfx/meow_02.wav` | Cat Meow — qubodup, from Cat Call (Meow) by Jofae | https://freesound.org/people/qubodup/sounds/813119/ | Full short real cat call. The selected sound page marks it CC0. |
| `sfx/meow_03.wav` | Kitten meows — Luke100000 | https://freesound.org/people/Luke100000/sounds/476918/ | Real eight-week kitten; source interval 10.94–11.57 seconds. |

License: https://creativecommons.org/publicdomain/zero/1.0/

`manifest.json` records durations, channels, signal levels, sources and SHA-256 hashes of the shipped WAVs. Source MP3 downloads are regeneration inputs only and are not required in a release.
