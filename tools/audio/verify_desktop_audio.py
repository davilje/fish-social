"""Verify shipped audio without third-party dependencies: python tools/audio/verify_desktop_audio.py."""
from pathlib import Path
import array
import hashlib
import json
import math
import sys
import wave

root = Path(__file__).resolve().parents[2] / "fish-social-unity/Assets/StreamingAssets/Audio"
manifest = json.loads((root / "manifest.json").read_text())
expected = {"ui_click", "ui_open", "ui_close", "ui_toggle", "ui_tick", "ui_success", "ui_error", "cast", "splash", "catch", "meow_01", "meow_02", "meow_03"}
assert expected == {Path(a["path"]).stem for a in manifest["assets"] if a["path"].startswith("sfx/")}
for item in manifest["assets"]:
    path = root / item["path"]
    assert hashlib.sha256(path.read_bytes()).hexdigest() == item["sha256"], path
    with wave.open(str(path)) as audio:
        assert audio.getframerate() == 44100 and audio.getsampwidth() == 2, path
        channels = audio.getnchannels()
        assert channels == item["channels"], path
        duration = audio.getnframes() / audio.getframerate()
        assert abs(duration - item["duration"]) < .0001, path
        samples = array.array("h", audio.readframes(audio.getnframes()))
        if sys.byteorder != "little":
            samples.byteswap()
    peak = max(abs(v) for v in samples) / 32768
    rms = math.sqrt(sum(v*v for v in samples) / len(samples)) / 32768
    assert .01 < peak < .98 and rms > .005, (path, peak, rms)
    if item["path"].startswith("music/"):
        assert channels == 2 and duration == 80, path
        assert max(abs(samples[i] - samples[-channels+i]) for i in range(channels)) <= 3, "Loop seam"
    print("PASS", item["path"], str(round(duration, 3)) + "s")
print("PASS", len(manifest["assets"]), "audio files, PCM format, manifest hashes, levels and loop seam")
