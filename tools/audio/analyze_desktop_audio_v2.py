"""Offline true-peak bounds and v1 RMS comparison; not a human listening test."""
from pathlib import Path
import json,numpy as np,soundfile as sf
from scipy.signal import resample_poly
R=Path(__file__).resolve().parents[2];A=R/'fish-social-unity/Assets/StreamingAssets/Audio';B=R/'docs/art/audio-22i/baseline-v1'
c=json.loads((A/'cue-config.json').read_text());m=json.loads((A/'manifest.json').read_text());peaks={};rms={}
def db(x):return round(float(20*np.log10(max(x,1e-12))),3)
def effective(x):
 a=x[np.abs(x)>np.max(np.abs(x))*.025];return float(np.sqrt(np.mean(a*a)))
for item in m['assets']:
 x,sr=sf.read(A/item['path']);peaks[item['path']]=float(np.max(np.abs(resample_poly(x,4,1,axis=0))));rms[item['path']]=effective(x)
maxima={g:max(max(peaks[p] for p in cue['paths'])*cue['gain'] for cue in c['cues'] if cue['group']==g) for g in ['ui','fishing','voice']}
music=peaks['music/pond_daylight.wav'];bound=maxima['ui']+2*maxima['fishing']+maxima['voice']+music
comparisons={}
for cue in ['splash','groundbait']:
 x,_=sf.read(B/'sfx'/f'{cue}.wav');baseline=effective(x)
 comparisons[cue]=[dict(path=p,baselineEffectiveRms=baseline,effectiveRms=rms[p],deltaDb=db(rms[p]/baseline)) for p in sorted(rms) if p.startswith('water/'+cue+'_')]
report=dict(version=2,method='4x polyphase oversampling; sum of absolute per-channel maxima proves any phase/time alignment at 100% sliders is bounded. No limiter assumed.',individualTruePeaksDbTP={p:db(v) for p,v in peaks.items()},groupMaxima=maxima,worstCaseFullVolumeBound=bound,worstCaseFullVolumeDbTP=db(bound),defaultMixBound=.7*(bound-music)+.35*music,waterRmsComparison=comparisons,decodedShortBytes=sum(round(a['duration']*44100)*4 for a in m['assets'] if a['channels']==1),humanListening='NOT EXECUTED: headphones and speakers required',windowsAvMeasurement='NOT EXECUTED: requires >=30 actual displayed marker and output onset captures')
assert db(bound)<=-1,report
assert all(x['deltaDb']>=3 for v in comparisons.values() for x in v),comparisons
out=R/'docs/engineering/audio-22i-v2-measurements.json';out.write_text(json.dumps(report,indent=2,ensure_ascii=False)+'\n');print(json.dumps({k:v for k,v in report.items() if k not in ['individualTruePeaksDbTP']},indent=2))
