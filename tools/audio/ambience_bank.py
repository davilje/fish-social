"""Merge independently reproducible natural ambience tracks into the runtime bank."""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
MANIFEST = ROOT/'fish-social-unity/Assets/StreamingAssets/Audio/ambience-manifest.json'
ORDER = ('water', 'wind', 'leaves')


def loops(bank):
    if 'loops' in bank:
        return bank['loops']
    runtime = bank['runtime']
    return runtime.get('tracks', [dict(id='water', **runtime)])


def merge_bank(bank, ids):
    previous = json.loads(MANIFEST.read_text()) if MANIFEST.exists() else {}
    old_loops = loops(previous) if previous else []
    incoming = loops(bank)
    assert {item['id'] for item in incoming} == set(ids)
    replaced_paths = {item['path'] for item in old_loops if item['id'] in ids}
    kept_assets = [item for item in previous.get('assets', []) if item['path'] not in replaced_paths]
    kept_sources = {key for item in kept_assets for key in item['source']}
    merged_loops = [item for item in old_loops if item['id'] not in ids] + incoming
    merged_loops.sort(key=lambda item: ORDER.index(item['id']))
    old_provenance = previous.get('provenance', {})
    builders = old_provenance.get('builders', [old_provenance] if old_provenance else [])
    provenance = bank['provenance']
    builders = [item for item in builders if item.get('script') != provenance['script']] + [provenance]
    builders.sort(key=lambda item: item['script'])
    result = dict(version=2, loops=merged_loops,
        sources=[item for item in previous.get('sources', []) if item['key'] in kept_sources] + bank['sources'],
        assets=kept_assets + bank['assets'], provenance=dict(builders=builders))
    paths = [item['path'] for item in result['assets']]
    assert len(paths) == len(set(paths)) and set(paths) == {item['path'] for item in merged_loops}
    MANIFEST.write_text(json.dumps(result, ensure_ascii=False, indent=2)+'\n')
    return result
