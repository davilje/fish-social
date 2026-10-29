"""Read-only asset, loop and full-volume pond-ASMR mix verification.

This verifies the independent ambience bank and existing action/cat recordings;
it does not require regenerating a music score that is disabled in the pond.
"""
from pathlib import Path
import argparse
import hashlib
import json
import numpy as np
import soundfile as sf
from scipy.signal import resample_poly
from build_pond_ambience import metrics

ROOT=Path(__file__).resolve().parents[2]
AUDIO=ROOT/'fish-social-unity/Assets/StreamingAssets/Audio'


def read(path):
    return json.loads(path.read_text())


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def verify():
    config=read(AUDIO/'cue-config.json')
    bank=read(AUDIO/'manifest.json')
    ambience=read(AUDIO/'ambience-manifest.json')
    adoption=read(ROOT/'docs/工程/声音/audio-22i-adoption.json')
    assert sha(AUDIO/'cue-config.json')==adoption['cueConfigSha256']
    assert len(adoption['assets'])==47
    for asset in adoption['assets']:
        assert sha(AUDIO/asset['path'])==asset['sha256'],asset['path']
    assert ambience['version']==2 and len(ambience['assets'])==3
    runtime=ambience['loops']
    assert {track['id'] for track in runtime}=={'water','wind','leaves'}
    measured=[]
    for track in runtime:
        assert 0<track['gain']<=1 and track['fadeSeconds']==2
        asset=next(item for item in ambience['assets'] if item['path']==track['path'])
        item=metrics(AUDIO/track['path'])
        item['path']=track['path']
        assert item['sha256']==asset['sha256']
        assert item['channels']==2 and item['sampleRate']==44100 and item['format']=='PCM_16'
        assert 40<=item['duration']<=90 and item['seamDifference']==0
        assert item['seamAdjacentStep']<item['naturalInteriorStep99Percentile']
        assert item['truePeak4x']<=(.3 if track['id']=='water' else .12) and item['rms']>.005
        measured.append(item)
    for builder in ambience['provenance']['builders']:
        assert sha(ROOT/builder['script'])==builder['scriptSha256']
    for source in ambience['sources']:
        assert source['license']=='CC0-1.0' and len(source['sha256'])==64
        assert source['page'].startswith('https://freesound.org/')
    peaks={}
    for item in bank['assets']:
        if item['path'].startswith('music/'):
            continue
        pcm,rate=sf.read(AUDIO/item['path'])
        assert rate==44100
        peaks[item['path']]=float(np.max(abs(resample_poly(pcm,4,1,axis=0))))
    groups={group:max(max(peaks[path] for path in cue['paths'])*cue['gain']
        for cue in config['cues'] if cue['group']==group) for group in ['fishing','voice']}
    bound=2*groups['fishing']+groups['voice']+sum(item['truePeak4x']*track['gain'] for item,track in zip(measured,runtime))
    db=float(20*np.log10(bound))
    assert db<=-1,'Pond ambience plus all action/cat voices exceeds mix headroom'
    return dict(status='PASS',date='2026-10-30',original47WavHashesUnchanged=True,
        cueConfigUnchanged=True,ambience=measured,pondMusicEnabled=False,pondButtonsEnabled=False,
        runtime=runtime,pondFullVolumeMixUpperBoundDbTP=db,
        method='4x oversampling; sum2 max fishing voices+max cat+all3 ambience tracks at full per-track and effects sliders; no BGM/UI in pond',
        runtimeBehaviourEvidence='Unity EditMode DesktopAudioTests; see accompanying report',
        windowsHardwareMeasurement='Not run')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--report',type=Path)
    args=parser.parse_args()
    payload=json.dumps(verify(),ensure_ascii=False,indent=2)+'\n'
    if args.report:
        args.report.parent.mkdir(parents=True,exist_ok=True)
        args.report.write_text(payload)
    print(payload,end='')
