"""Verify shipped audio structure/hashes/budget; no third-party dependencies."""
from pathlib import Path
import array,hashlib,json,math,sys,wave
from cue_config_selection import verify_selection
root=Path(__file__).resolve().parents[2]/'fish-social-unity/Assets/StreamingAssets/Audio'
manifest=json.loads((root/'manifest.json').read_text());config=json.loads((root/'cue-config.json').read_text())
ambience=json.loads((root/'ambience-manifest.json').read_text())
assert ambience['version']==2
assert {t['id'] for t in ambience['loops']}=={'water','wind','leaves'}
for track in ambience['loops']:
 assert 0<track['gain']<=1 and .1<=track['fadeSeconds']<=5
ambient_paths={a['path'] for a in ambience['assets']}
assert len(ambient_paths)==3 and ambient_paths=={t['path'] for t in ambience['loops']}
assets=manifest['assets']+ambience['assets']
expected={p for c in config['cues'] for p in c['paths']}|{'music/pond_daylight.wav'}
adoption=json.loads((root.parents[3]/'docs/工程/声音/audio-22i-adoption.json').read_text())
expected|=verify_selection((root/'cue-config.json').read_bytes(),adoption['cueConfigSha256'])
assert expected=={a['path'] for a in manifest['assets']}
expected|=ambient_paths
assert len([p for p in expected if p.startswith('water/')])>=28
assert len({a['sha256'] for a in manifest['assets'] if a['path'].startswith('water/')})>=28
water=[a for a in manifest['assets'] if a['path'].startswith('water/')]
for i,a in enumerate(water):
 for b in water[i+1:]:
  x,y=a['processing'],b['processing']
  if x['source']==y['source']:
   assert x['startSeconds']+x['durationSeconds']<=y['startSeconds'] or y['startSeconds']+y['durationSeconds']<=x['startSeconds'],('Reused source event',a['path'],b['path'])
for source in manifest['sources']+ambience['sources']:
 assert source['license']=='CC0-1.0' and len(source['sha256'])==64 and source['page'].startswith('https://')
budget=0
for item in assets:
 path=root/item['path'];assert hashlib.sha256(path.read_bytes()).hexdigest()==item['sha256'],path
 with wave.open(str(path)) as w:
  assert w.getframerate()==44100 and w.getsampwidth()==2,path
  channels=w.getnchannels();frames=w.getnframes();duration=frames/44100
  assert channels==item['channels'] and abs(duration-item['duration'])<.00001,path
  samples=array.array('h',w.readframes(frames))
  if sys.byteorder!='little':samples.byteswap()
 peak=max(abs(v) for v in samples)/32768
 assert .005<peak<=10**(-3/20),path
 if channels==1:budget+=frames*4
 if item['path'].startswith('water/'):
  for tier,(lo,hi) in {'s':(.12,.26),'m':(.18,.36),'l':(.28,.50),'xl':(.38,.70)}.items():
   if '_'+tier+'_' in path.name:assert lo-.0001<=duration<=hi+.0001,path
 if item['path'].startswith(('music/','ambience/')):
  assert channels==2 and (40 if item['path'].startswith('ambience/') else 60)<=duration<=90,path
  assert max(abs(samples[i]-samples[-channels+i]) for i in range(channels))<=3,'Loop seam'
 else:assert channels==1,path
 print('PASS',item['path'],round(duration,3),'s')
assert budget<=4*1024*1024,budget
assert not (set(str(p.relative_to(root)) for p in root.rglob('*.wav'))-expected),'Unmanifested assets'
print('PASS',len(expected),'assets; decoded shorts',budget,'bytes / 4 MiB; formats/hashes/tier durations/loop seam')
