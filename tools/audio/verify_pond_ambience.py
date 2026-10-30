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
from cue_config_selection import verify_selection
from verify_ambience_config import verify_config

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
    verify_selection((AUDIO/'cue-config.json').read_bytes(),adoption['cueConfigSha256'])
    assert len(adoption['assets'])==47
    for asset in adoption['assets']:
        assert sha(AUDIO/asset['path'])==asset['sha256'],asset['path']
    verify_config(ambience)
    runtime=ambience['loops']
    assert {track['id'] for track in runtime}=={'water','wind','leaves'}
    measured=[]
    for asset in ambience['assets']:
        item=metrics(AUDIO/asset['path'])
        item['path']=asset['path']
        assert item['sha256']==asset['sha256']
        assert item['channels']==2 and item['sampleRate']==44100 and item['format']=='PCM_16'
        if asset['path']=='ambience/pond_wind_open_loop.wav':
            assert item['duration']==12
            assert item['seamDifference']<=item['naturalInteriorStep99Percentile']
        else:
            assert 40<=item['duration']<=90 and item['seamDifference']==0
            assert item['seamAdjacentStep']<item['naturalInteriorStep99Percentile']
        assert item['truePeak4x']<=(.3 if asset['path']=='ambience/pond_shore.wav' else .12)
        assert item['rms']>.001, 'Ambience must preserve an audible recording, not silence'
        measured.append(item)
    for builder in ambience['provenance']['builders']:
        assert sha(ROOT/builder['script'])==builder['scriptSha256']
    configuration=ambience['provenance']['configuration']
    assert sha(ROOT/configuration['script'])==configuration['scriptSha256']
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
    ambient_peaks={item['path']:item['truePeak4x'] for item in measured}
    # Include two overlapping water sources even without assuming a perfectly complementary fade.
    water_peak=max(ambient_peaks[profile['path']]*profile['gain'] for profile in ambience['waterProfiles'])
    w=ambience['weather']
    maxima=dict(wind=w['windMaxGain'],leaves=max(w['initialLeavesGain'],w['leavesResponseGain']*(w['windMaxGain']/w['leavesReferenceWindGain'])**w['leavesResponseExponent']))
    gust_peak=sum(ambient_peaks[track['path']]*track['gain']*maxima[track['id']] for track in runtime if track['id']!='water')
    bound=2*groups['fishing']+groups['voice']+2*water_peak+gust_peak
    db=float(20*np.log10(bound))
    assert db<=-1,'Pond ambience plus all action/cat voices exceeds mix headroom'
    return dict(status='PASS',date='2026-10-30',original47WavHashesUnchanged=True,
        cueConfigChanges='Only ui_close selects the approved page-close variant; all other cue settings match adoption',ambience=measured,pondMusicEnabled=False,pondButtonsEnabled=False,
        runtime=runtime,waterProfiles=ambience['waterProfiles'],sceneCount=len(ambience['scenes']),weather=ambience['weather'],weatherPeakBounds=maxima,productionMonitorGain=1,pondFullVolumeMixUpperBoundDbTP=db,
        method='4x oversampling; sum2 max fishing voices+max cat+2 max water sources during crossfade+overlapping continuous wind/leaves at theoretical weather maxima and full sliders; production excludes preview monitor gain; no BGM/UI in pond',
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
