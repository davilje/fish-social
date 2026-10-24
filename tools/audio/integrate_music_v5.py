"""Install the adopted v5 music; preserve the independent approved-effects lock.

--check-only validates inputs and prints the planned result without writing any file.
A different music hash requires a new explicit user adoption record first.
"""
from pathlib import Path
import argparse
import copy
import hashlib
import json

R = Path(__file__).resolve().parents[2]
A = R / 'fish-social-unity/Assets/StreamingAssets/Audio'
P = R / 'docs/art/audio-22i/v5/assets'
LOCK = Path(__file__).with_name('accepted-effects.json')
ADOPTION = R / 'docs/engineering/audio-22i-adoption.json'


def read(path):
    return json.loads(path.read_text())


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def prepare(current, music, lock, adoption):
    assert current['version'] == music['version'] == adoption['version'] == 5
    assert adoption['status'] == lock['status'] == 'adopted'
    approved = {a['path']: a for a in lock['assets']}
    shorts = [a for a in current['assets'] if not a['path'].startswith('music/')]
    assert len(approved) == len(shorts) == 46
    assert {a['path'] for a in shorts} == set(approved)
    assert sha(A / 'cue-config.json') == lock['cueConfigSha256'] == adoption['cueConfigSha256']
    for asset in shorts:
        assert asset == approved[asset['path']], ('Approved effect metadata changed', asset['path'])
        assert sha(A / asset['path']) == asset['sha256'], asset['path']
    current_sources = {s['key']: s for s in current['sources']}
    for source in lock['sources']:
        assert current_sources[source['key']] == source, ('Approved effect source changed', source['key'])
    adopted = {a['path']: a for a in adoption['assets']}
    assert len(adopted) == len(current['assets']) == 47
    assert set(adopted) == {a['path'] for a in current['assets']}
    for asset in current['assets']:
        assert asset['version'] == adopted[asset['path']]['version']
        assert asset['sha256'] == adopted[asset['path']]['sha256'] == sha(A / asset['path']), asset['path']
    assert sha(P / 'score.json') == music['score']['sha256']
    assert sha(R / music['provenance']['scoreScript']) == music['provenance']['scoreScriptSha256']
    for asset in music['assets']:
        assert asset['version'] == 5 and sha(P / asset['path']) == asset['sha256'], asset['path']
    full = copy.deepcopy(next(a for a in music['assets'] if a['path'] == 'sunlight-theme.wav'))
    assert full['channels'] == 2 and 60 <= full['duration'] <= 90
    assert full['sha256'] == adoption['music']['sha256'], 'Music requires an explicit new adoption record'
    assert adoption['music']['path'] == 'music/pond_daylight.wav'
    full['path'] = adoption['music']['path']
    full['processing']['score'] = 'docs/art/audio-22i/v5/assets/score.json'
    locked_sources = {s['key']: s for s in lock['sources']}
    for source in music['sources']:
        if source['key'] in locked_sources:
            assert source == locked_sources[source['key']], ('Music would overwrite an approved effect source', source['key'])
    sources = list({s['key']: s for s in lock['sources'] + music['sources']}.values())
    result = copy.deepcopy(current)
    acceptance = dict(status='adopted', date=adoption['date'],
        effects='All 46 v3 effects accepted and retained', music='v5 accepted for project use by user',
        record=str(ADOPTION.relative_to(R)), windowsAvMeasurement='Pending real Windows measurement')
    result.update(version=5, sources=sources, assets=shorts + [full], acceptance=acceptance)
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check-only', action='store_true')
    args = parser.parse_args()
    current, music, lock, adoption = read(A/'manifest.json'), read(P/'music-manifest.json'), read(LOCK), read(ADOPTION)
    result = prepare(current, music, lock, adoption)
    if not args.check_only:
        # Preserve the adopted audio bytes; repeat integration does not rewrite identical WAVs.
        if sha(A/'music/pond_daylight.wav') != result['assets'][-1]['sha256']:
            (A/'music/pond_daylight.wav').write_bytes((P/'sunlight-theme.wav').read_bytes())
        (A/'manifest.json').write_text(json.dumps(result, ensure_ascii=False, indent=2)+'\n')
        (P/'manifest.json').write_text(json.dumps(music, ensure_ascii=False, indent=2)+'\n')
    print(('Validated' if args.check_only else 'Applied') +
        ' adopted music v5; all 47 approved WAV hashes, 46 effect metadata records and cue configuration unchanged.')


if __name__ == '__main__':
    main()
