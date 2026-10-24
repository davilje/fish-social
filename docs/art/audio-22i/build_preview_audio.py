"""Generate proposed 22I cues in this preview only; requires numpy, scipy, soundfile."""
from pathlib import Path
import json
import numpy as np
from scipy import signal
import soundfile as sf

ROOT = Path(__file__).resolve().parent / 'assets'
SR = 44100
rng = np.random.default_rng(221)
ROOT.mkdir(exist_ok=True)

def finish(name, x, peak):
    n = min(441, len(x)//2)
    x[:n] *= np.linspace(0, 1, n)
    x[-n:] *= np.linspace(1, 0, n)
    x *= peak/max(abs(x).max(), .001)
    sf.write(ROOT/(name+'.wav'), x, SR, subtype='PCM_16')

# A clearer water ping for a bite, distinct from the ordinary cast splash.
t = np.arange(int(.42*SR))/SR
phase = 2*np.pi*np.cumsum(1350*np.exp(-t*10)+430)/SR
x = .7*np.sin(phase)*np.exp(-t*12)
x += .22*np.sin(2*np.pi*1046.5*t)*np.exp(-t*8)
finish('bite', x, .3)

# A brief quiet reel, not a continuous mechanical loop.
t = np.arange(int(.56*SR))/SR
noise = signal.sosfilt(signal.butter(2, [500, 2200], btype='bandpass', fs=SR, output='sos'), rng.normal(size=len(t)))
x = noise*(.3+.7*np.maximum(0,np.sin(2*np.pi*26*t))**10)*np.sin(np.pi*t/.56)**.7
x += .03*np.sin(2*np.pi*180*t)*np.sin(np.pi*t/.56)
finish('reel', x, .21)

# A short falling water note for a missed fish, without a failure alarm.
t = np.arange(int(.38*SR))/SR
phase = 2*np.pi*np.cumsum(620-330*t/.38)/SR
x = np.sin(phase)*np.exp(-t*11)+.14*np.sin(phase*2.03)*np.exp(-t*20)
finish('escape', x, .16)

# A handful of scattered bait hitting water once at the throw marker.
x = np.zeros(int(.46*SR))
for offset,f in [(0,680),(.038,1050),(.079,870),(.132,1230),(.185,740)]:
    t = np.arange(int(.16*SR))/SR
    phase = 2*np.pi*np.cumsum(f*np.exp(-t*10)+180)/SR
    drop = np.sin(phase)*np.exp(-t*35)
    drop[:90] *= np.linspace(0,1,90)
    i = int(offset*SR)
    x[i:i+len(drop)] += drop*.45
finish('groundbait', x, .19)

print(json.dumps({p.name: {'seconds':len(sf.read(p)[0])/SR, 'sampleRate':SR}
                  for p in ROOT.glob('*.wav')}, indent=2))
