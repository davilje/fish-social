"""Build the recorded v2 palette. Python: numpy scipy soundfile.
Usage: python tools/audio/build_desktop_audio_v2.py --sources /path/to/cache
Sources are pinned by SHA256 in sources-v2.json; release playback is offline.
"""
from pathlib import Path
import argparse,base64,hashlib,json,urllib.request
import numpy as np
import soundfile as sf
from scipy import signal
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'fish-social-unity/Assets/StreamingAssets/Audio'
PREVIEW=ROOT/'docs/art/audio-22i/v2/assets'
SR=44100
manifest=[]
preview_manifest=[]
source_meta=json.loads((Path(__file__).with_name('sources-v2.json')).read_text())
parser=argparse.ArgumentParser();parser.add_argument('--sources',default=str(Path.home()/'.cache/fish-social-audio-v2'));CACHE=Path(parser.parse_args().sources);CACHE.mkdir(parents=True,exist_ok=True)
source_audio={}
def source(key):
 if key in source_audio:return source_audio[key]
 m=next(x for x in source_meta if x['key']==key);p=CACHE/m['file']
 if not p.exists():
  raw=urllib.request.urlopen(urllib.request.Request(m['url'],headers={'User-Agent':'FishSocial-audio-build'}),timeout=90).read()
  if '/git/blobs/' in m['url']:raw=base64.b64decode(json.loads(raw)['content'])
  p.write_bytes(raw)
 assert hashlib.sha256(p.read_bytes()).hexdigest()==m['sha256'],p
 x,sr=sf.read(p);x=x.mean(axis=1) if x.ndim>1 else x
 if sr!=SR:x=signal.resample_poly(x,SR//np.gcd(sr,SR),sr//np.gcd(sr,SR))
 source_audio[key]=x;return x

def fade(x,attack=.003,release=.025):
 x=x.copy();a=min(int(SR*attack),len(x)//2);r=min(int(SR*release),len(x)//2)
 if a:x[:a]*=np.linspace(0,1,a).reshape((-1,)+(1,)*(x.ndim-1))
 if r:x[-r:]*=np.linspace(1,0,r).reshape((-1,)+(1,)*(x.ndim-1))
 return x

def active_rms(x):
 a=x[np.abs(x)>np.max(np.abs(x))*.025];return float(np.sqrt(np.mean(a*a)))
def write(path,x,description,sources,processing,preview=False):
 dst=(PREVIEW if preview else OUT)/path;dst.parent.mkdir(parents=True,exist_ok=True)
 sf.write(dst,x,SR,subtype='PCM_16');x,_=sf.read(dst)
 item=dict(path=path,version=2,description=description,source=sources,processing=processing,duration=round(len(x)/SR,6),channels=1 if x.ndim==1 else 2,peak=float(np.max(np.abs(x))),rms=float(np.sqrt(np.mean(x*x))),effectiveRms=active_rms(x),sha256=hashlib.sha256(dst.read_bytes()).hexdigest())
 if not preview:manifest.append(item)
 else:preview_manifest.append(item)
 return item

def cut(key,start,duration,peak=.16,rms=None,low=6500,high=100):
 x=source(key)[round(start*SR):round((start+duration)*SR)].copy();assert len(x)>duration*SR-2,(key,start)
 x-=np.mean(x);x=signal.sosfilt(signal.butter(2,[high,low],btype='bandpass',fs=SR,output='sos'),x)
 x=fade(x,.003,min(.04,duration*.22));x/=max(np.max(np.abs(x)),1e-9)
 # Oversampled soft peak control (not hard clipping), followed by low-pass;
 # each trial derives from the original waveform, never repeated saturation.
 drive=1.
 if rms:
  original=signal.resample_poly(x,4,1)
  for drive in [1.,1.5,2.,3.,4.,6.,8.,12.]:
   y=np.tanh(original*drive)
   y=signal.resample_poly(y,1,4)
   y=signal.sosfilt(signal.butter(3,5500,btype='lowpass',fs=SR,output='sos'),y)
   y=fade(y,.003,min(.035,duration*.20))
   true_peak=np.max(np.abs(signal.resample_poly(y,4,1)))
   y*=min(peak/true_peak,rms/max(active_rms(y),1e-9))
   x=y
   if active_rms(x)>=rms*.995:break
 else:x*=peak
 return x,dict(source=key,startSeconds=start,durationSeconds=duration,highPassHz=high,lowPassHz=low,softCompressionDrive=round(drive,2),pitchShift=0,fadeInMs=3,fadeOutMs=round(min(.04,duration*.22)*1000))

def effect(name,key,start,duration,desc,**kw):
 x,p=cut(key,start,duration,**kw);write(name,x,desc,[key],p)

# Separate physical page events, kept within UI duration budgets.
ui=[('ui_click_01','paper_many',1.30,.085),('ui_click_02','paper_many',5.36,.09),
 ('ui_open_01','paper_a',.39,.29),('ui_open_02','paper_b',.35,.28),
 ('ui_close_01','paper_many',17.37,.24),('ui_close_02','paper_many',25.78,.26),
 ('ui_toggle_01','paper_many',10.94,.13),('ui_toggle_02','paper_many',14.38,.13),
 ('ui_tick','pencil',2.14,.065),('ui_success','stamp',.015,.21),('ui_error','paper_many',23.75,.19),
 ('catch','stamp',.01,.26)]
for name,key,start,dur in ui:effect('sfx/'+name+'.wav',key,start,dur,'实录纸张／铅笔／轻印章反馈',peak=.16,low=5000)
effect('sfx/cast.wav','whoosh',.07,.23,'实录木杆挥动风切',peak=.16,low=5000)
effect('sfx/reel.wav','reel',.7,.34,'实录钓线轮短摩擦',peak=.16,low=3200)
effect('sfx/escape.wav','water_pour',19.16,.32,'实录落水短尾，仅真实脱钩接触可响',peak=.23,rms=.075)
# 24 non-overlapping recorded events, no water pitch shifting. Short light ripples
# use bowl/stick contacts; heavy bodies use separate gallon dashes/lake impact.
waters={
 'bite':{'s':[('water_bowls',6.20,.22),('water_bowls',9.00,.24)],'m':[('water_stick',1.17,.30),('water_stick',5.38,.32)],'l':[('water_dash',.14,.44),('water_dash',1.08,.46)],'xl':[('water_lake',.26,.68),('water_dash',3.70,.64)]},
 'water_struggle':{'s':[('water_bowls',18.24,.23),('water_bowls',22.00,.24)],'m':[('water_bowls',52.86,.33),('water_bowls',56.0,.34)],'l':[('water_dash',6.89,.47),('water_dash',8.20,.48)],'xl':[('water_dash',10.91,.66),('water_dash',12.52,.68)]},
 'water_slap':{'s':[('water_stick',3.24,.24),('water_stick',9.74,.25)],'m':[('water_stick',7.66,.34),('water_stick',14.78,.35)],'l':[('water_stick',19.65,.49),('water_stick',24.28,.50)],'xl':[('water_dash',16.24,.68),('water_dash',18.58,.69)]}}
for cue,tiers in waters.items():
 for tier,clips in tiers.items():
  for i,(key,start,dur) in enumerate(clips,1):effect(f'water/{cue}_{tier}_{i:02}.wav',key,start,dur,f'{cue} {tier.upper()} 实录独立变体{i}',peak=.245,rms={'s':.070,'m':.080,'l':.090,'xl':.100}[tier],high=80 if tier in ['l','xl'] else 150,low=7000)
for i,(key,start,dur) in enumerate([('water_pour',7.52,.30),('water_pour',11.55,.31)],1):effect(f'water/splash_{i:02}.wav',key,start,dur,'浮漂轻落水，尺寸独立于未来鱼获',peak=.245,rms=.096)
for i,start in enumerate([.67,4.15],1):effect(f'water/groundbait_{i:02}.wav','water_pour',start,.29,'多颗窝料实录水粒细响',peak=.245,rms=.091)
# Preserve the legally acquired calls, rebalance only; never synthesize a fake recording.
base=ROOT/'docs/art/audio-22i/baseline-v1'
old=json.loads((base/'manifest.json').read_text())
for i in range(1,4):
 name=f'sfx/meow_{i:02}.wav';x,_=sf.read(base/name);x*=.30/np.max(np.abs(x));m=next(a for a in old['assets'] if a['path']==name)
 write(name,x,m['description'],m['source'],dict(baselineSha256=m['sha256'],normalizationPeak=.30,originalProcessing='See baseline-v1/CREDITS.md; same licensed call'))

# Original sparse melody, 24 bars at 68 BPM, recorded soft Kawai piano.
# B uses exactly the same lead score plus a quiet cello line, A is production.
beat=60/68;n=round(24*4*beat*SR);a=np.zeros((n,2));b=np.zeros_like(a)
def note(key,base_midi,midi,duration,level):
 x=source(key);ratio=2**((midi-base_midi)/12);xp=np.arange(0,min(len(x),duration*SR*ratio),ratio);y=np.interp(xp,np.arange(len(x)),x)
 y=signal.sosfilt(signal.butter(2,2400,btype='lowpass',fs=SR,output='sos'),y);y=fade(y,.012,.15);return y*level/max(np.max(np.abs(y)),1e-9)
def add(dst,y,at,pan):
 ids=(np.arange(len(y))+round(at*SR))%n
 for c,g in enumerate([np.sqrt((1-pan)/2),np.sqrt((1+pan)/2)]):np.add.at(dst[:,c],ids,y*g)
roots=[48,45,41,43];motifs=[[60,64,67],[60,64,69],[60,65,64],[59,62,67]]
for bar in range(24):
 t=bar*4*beat;root=roots[bar%4];mel=motifs[bar%4]
 # At most two principal instrumental parts with generous silence between notes.
 for step,pitch in zip([.4,1.9,3.05],mel if bar%8!=7 else mel[:2]):
  y=note('piano_c4',60,pitch,2.4,.085 if step!=1.9 else .070);add(a,y,t+step*beat,.15);add(b,y,t+step*beat,.15)
 add(a,note('piano_c3',48,root,3.7,.060),t,-.18)
 add(b,note('cello_c3',48,root,3.1,.045),t,-.18)
for mix in [a,b]:
 dry=mix.copy()
 for delay,g in [(.143,.07),(.267,.04),(.399,.02)]:mix+=np.roll(dry,round(delay*SR),axis=0)[:,::-1]*g
 mix*=.10/np.max(np.abs(mix))
 k=128;boundary=(mix[0]+mix[-1])/2;mix[:k]+=(boundary-mix[0])*np.linspace(1,0,k)[:,None];mix[-k:]+=(boundary-mix[-1])*np.linspace(0,1,k)[:,None]
write('music/pond_daylight.wav',a,'池畔手记 v2：68BPM 柔音实采钢琴、24小节、稀疏双线条无缝循环',['piano_c3','piano_c4'],dict(bpm=68,bars=24,composition='Original Fish Social arrangement',sampleBased=True,lowPassHz=2400,loop='circular tails + 128 sample seam blend',selected='A'))
for name,mix,sources in [('v2-piano-a.wav',a,['piano_c3','piano_c4']),('v2-piano-cello-b.wav',b,['piano_c4','cello_c3'])]:
 x=mix[:round(7*4*beat*SR)].copy();x=fade(x,.03,.25);write(name,x,'同旋律配器比较 '+name,sources,dict(bpm=68,bars=7),preview=True)
config=json.loads((OUT/'cue-config.json').read_text());expected={p for c in config['cues'] for p in c['paths']}|{'music/pond_daylight.wav'}
assert expected=={m['path'] for m in manifest},expected^{m['path'] for m in manifest}
(PREVIEW/'manifest.json').write_text(json.dumps(dict(version=2,assets=preview_manifest,sources=source_meta),indent=2,ensure_ascii=False)+'\n')
# Remove only legacy v1 generated audio which was first frozen in baseline-v1.
for p in OUT.rglob('*.wav'):
 if str(p.relative_to(OUT)) not in expected:p.unlink()
(OUT/'manifest.json').write_text(json.dumps(dict(version=2,sampleRate=SR,format='PCM_16',sources=source_meta,assets=manifest),indent=2,ensure_ascii=False)+'\n')
print('BUILT',len(manifest),'assets; decoded shorts',sum(round(a['duration']*SR)*4 for a in manifest if a['channels']==1))
