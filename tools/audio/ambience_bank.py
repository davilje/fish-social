"""Merge independently reproducible natural ambience tracks into the runtime bank."""
import json
import hashlib
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
    if previous.get('version') in (3, 4):
        return merge_scene_assets(previous, bank)
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


def merge_scene_assets(previous, bank):
    """Older water/leaf builders can refresh owned assets without changing scene design."""
    configured = {item['path'] for item in previous['waterProfiles'] + previous['loops']}
    assert all(item['path'] in configured for item in bank['assets']), 'Retired ambience cannot replace the current scene bank'
    by_path = {item['path']: item for item in previous['assets']}
    by_path.update({item['path']: item for item in bank['assets']})
    previous['assets'] = [by_path[item['path']] for item in previous['assets']]
    source_keys = {key for item in previous['assets'] for key in item['source']}
    sources = {item['key']: item for item in previous['sources'] + bank['sources']}
    previous['sources'] = [sources[key] for key in sorted(source_keys)]
    incoming = bank['provenance']
    builders = previous['provenance']['builders']
    previous['provenance']['builders'] = sorted(
        [item for item in builders if item['script'] != incoming['script']] + [incoming],
        key=lambda item: item['script'])
    MANIFEST.write_text(json.dumps(previous, ensure_ascii=False, indent=2)+'\n')
    return previous


def assemble_scene_bank(bank):
    """Install new lakes/open wind, retaining the original shore and leaf recordings."""
    from pond_scene_config import scene_configuration, WATER_PROFILES, TRACK_GAINS
    previous = json.loads(MANIFEST.read_text())
    expected_new = {'ambience/pond_lake_calm.wav', 'ambience/pond_lake_ripples.wav', 'ambience/pond_wind_open_loop.wav'}
    assert {item['path'] for item in bank['assets']} == expected_new
    loops = [dict(id='water', path=WATER_PROFILES[0]['path'], gain=.60, fadeSeconds=2),
        dict(id='wind', path='ambience/pond_wind_open_loop.wav', gain=TRACK_GAINS['wind'], fadeSeconds=2),
        dict(id='leaves', path='ambience/pond_leaves.wav', gain=TRACK_GAINS['leaves'], fadeSeconds=2)]
    wanted = [item['path'] for item in WATER_PROFILES] + [item['path'] for item in loops[1:]]
    assets = {item['path']: item for item in previous['assets'] + bank['assets']}
    assert all(path in assets for path in wanted)
    assets = [assets[path] for path in wanted]
    keys = {key for item in assets for key in item['source']}
    sources = {item['key']: item for item in previous['sources'] + bank['sources']}
    builders = previous.get('provenance', {}).get('builders', [])
    builders = [item for item in builders if item['script'] != bank['provenance']['script']] + [bank['provenance']]
    script = ROOT/'tools/audio/pond_scene_config.py'
    result = dict(version=4, loops=loops, **scene_configuration(),
        assets=assets, sources=[sources[key] for key in sorted(keys)],
        provenance=dict(builders=sorted(builders, key=lambda item: item['script']),
            configuration=dict(script='tools/audio/pond_scene_config.py',
                scriptSha256=hashlib.sha256(script.read_bytes()).hexdigest())))
    MANIFEST.write_text(json.dumps(result, ensure_ascii=False, indent=2)+'\n')
    # The rejected window-like wind has been superseded, and must not ship as an orphan.
    for retired in ('ambience/pond_wind.wav', 'ambience/pond_wind_open.wav'):
        for suffix in ('', '.meta'):
            (MANIFEST.parent/(retired+suffix)).unlink(missing_ok=True)
    return result
