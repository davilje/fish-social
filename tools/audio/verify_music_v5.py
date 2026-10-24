"""Read-only verification of adopted v5 music, approved effects and full-volume mix.

Use --report PATH to explicitly save the measurement JSON. Adoption is user approval,
not evidence of an actual Windows audio/visual latency measurement.
"""
from pathlib import Path
import argparse
import json
import numpy as np
import soundfile as sf
from scipy.signal import resample_poly
from integrate_music_v5 import A, P, R, LOCK, ADOPTION, prepare, read, sha


def verify():
    manifest, music, lock, adoption = read(A/'manifest.json'), read(P/'music-manifest.json'), read(LOCK), read(ADOPTION)
    prepare(manifest, music, lock, adoption)
    assert manifest['acceptance']['status'] == 'adopted'
    assert music['provenance']['adoptionRecord'] == str(ADOPTION.relative_to(R))
    config = read(A/'cue-config.json')
    peaks = {}
    for item in manifest['assets']:
        path = A/item['path']
        info = sf.info(path)
        assert info.samplerate == 44100 and info.subtype == 'PCM_16' and info.channels == item['channels']
        x, rate = sf.read(path)
        assert abs(len(x)/rate-item['duration']) < .00001
        peaks[item['path']] = float(np.max(abs(resample_poly(x, 4, 1, axis=0))))
    groups = {group: max(max(peaks[p] for p in cue['paths'])*cue['gain']
        for cue in config['cues'] if cue['group'] == group) for group in ['ui', 'fishing', 'voice']}
    bound = groups['ui'] + 2*groups['fishing'] + groups['voice'] + peaks['music/pond_daylight.wav']
    db = float(20*np.log10(bound))
    assert db <= -1, 'Worst-case full-volume mix exceeds approved headroom'
    full, rate = sf.read(A/'music/pond_daylight.wav', dtype='int16')
    assert full.shape[1] == 2 and np.max(abs(full[0].astype(int)-full[-1].astype(int))) == 0
    assert 60 <= len(full)/rate <= 90 and peaks['music/pond_daylight.wav'] <= .14
    for item in music['assets']:
        data, preview_rate = sf.read(P/item['path'], dtype='int16')
        assert item['version'] == 5 and sha(P/item['path']) == item['sha256']
        assert preview_rate == rate and np.array_equal(full[:len(data)], data)
        if 'preview' in item['path']:
            assert 20 <= len(data)/rate <= 30
    return dict(version=5, status='adopted', acceptedEffectsUnchanged=len(lock['assets']),
        approvedAssetHashesUnchanged=len(adoption['assets']), cueConfigUnchanged=True,
        durationSeconds=len(full)/rate, musicTruePeak4x=peaks['music/pond_daylight.wav'],
        loopSeamDifference=0, fullVolumeMixUpperBoundDbTP=db,
        method='4x polyphase oversampling; sum max UI +2 fishing +voice +music, full sliders without duck',
        musicSha256=sha(A/'music/pond_daylight.wav'),
        humanBgmAcceptance='Adopted by user for project use; '+str(ADOPTION.relative_to(R)),
        windowsAvMeasurement=adoption['windowsAvMeasurement'])


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--report', type=Path)
    args = parser.parse_args()
    report = verify()
    payload = json.dumps(report, indent=2, ensure_ascii=False)+'\n'
    if args.report:
        args.report.parent.mkdir(parents=True, exist_ok=True)
        args.report.write_text(payload)
    print(payload, end='')


if __name__ == '__main__':
    main()
