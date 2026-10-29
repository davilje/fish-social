"""Verify the user's close-sound selection without rewriting historical adoption locks."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
RECORD = ROOT/'docs/工程/声音/2026-10-30-close-cue-selection.json'


def verify_selection(raw, baseline_hash):
    selection = json.loads(RECORD.read_text())
    assert selection['baselineCueConfigSha256'] == baseline_hash
    assert hashlib.sha256(raw).hexdigest() == selection['cueConfigSha256']
    config = json.loads(raw)
    close = next(cue for cue in config['cues'] if cue['cue'] == selection['cue'])
    assert close['paths'] == selection['paths'] == ['sfx/ui_close_01.wav']
    inactive = set(selection['inactivePaths'])
    assert inactive == {'sfx/ui_close_02.wav'}
    assert not inactive.intersection(path for cue in config['cues'] for path in cue['paths'])
    close['paths'] = selection['previousPaths']
    restored = (json.dumps(config, ensure_ascii=False, indent=2)+'\n').encode()
    assert hashlib.sha256(restored).hexdigest() == baseline_hash, 'Unrelated cue settings changed'
    return inactive
