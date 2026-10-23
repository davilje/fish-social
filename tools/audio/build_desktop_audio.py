"""Build Fish Social's original audio palette and prepare CC0 cat recordings.

Dependencies: numpy scipy soundfile. Run: python tools/audio/build_desktop_audio.py
Source recordings are public CC0 previews, downloaded only when regenerating assets.
"""
from pathlib import Path
import hashlib
import json
import wave

import numpy as np
from scipy import signal
import soundfile as sf

HERE = Path(__file__).resolve().parent
OUT = HERE.parents[1] / 'fish-social-unity/Assets/StreamingAssets/Audio'
SR = 44100
RNG = np.random.default_rng(231026)
MANIFEST = []


def save(name, audio, description, source='original'):
    path = OUT / name
    path.parent.mkdir(parents=True, exist_ok=True)
    audio = np.nan_to_num(audio)
    assert np.max(np.abs(audio)) < 1, name
    sf.write(path, audio, SR, subtype='PCM_16')
    MANIFEST.append(dict(path=name, description=description, source=source,
                         duration=round(len(audio) / SR, 4),
                         channels=1 if audio.ndim == 1 else audio.shape[1],
                         peak=round(float(np.max(np.abs(audio))), 5),
                         rms=round(float(np.sqrt(np.mean(audio ** 2))), 5),
                         sha256=hashlib.sha256(path.read_bytes()).hexdigest()))


def fade(x, seconds=.009):
    n = min(int(seconds * SR), len(x) // 2)
    x = x.copy()
    x[:n] *= np.linspace(0, 1, n)
    x[-n:] *= np.linspace(1, 0, n)
    return x


def tone(freq, duration=.16, volume=.25, kind='bell'):
    t = np.arange(int(duration * SR)) / SR
    if kind == 'bell':
        x = (np.sin(2*np.pi*freq*t) * np.exp(-t*17) +
             .23*np.sin(2*np.pi*freq*2.005*t)*np.exp(-t*30))
    else:
        phase = 2*np.pi*np.cumsum(np.linspace(freq, freq*.65, len(t))) / SR
        x = np.sin(phase)*np.exp(-t*24)
    return fade(volume*x, .004)


def sequence(notes, spacing=.075, volume=.22):
    x = np.zeros(int((len(notes)*spacing+.24)*SR))
    for i, f in enumerate(notes):
        y = tone(f, .24, volume)
        start = int(i*spacing*SR)
        x[start:start+len(y)] += y
    return x


def sfx():
    save('sfx/ui_click.wav', tone(690, .11, .23, 'wood'), '柔和木质按钮点击')
    save('sfx/ui_open.wav', sequence([523.25, 783.99], .055, .14), '展开面板，轻盈上行双音')
    save('sfx/ui_close.wav', sequence([659.25, 440], .045, .12), '关闭面板，柔和下行双音')
    save('sfx/ui_toggle.wav', tone(880, .105, .18), '切换选项')
    save('sfx/ui_tick.wav', tone(1046.5, .07, .10, 'wood'), '拖动音量刻度')
    save('sfx/ui_success.wav', sequence([523.25, 659.25, 783.99], .09, .21), '操作成功，温暖大三和弦')
    save('sfx/ui_error.wav', sequence([311.13, 261.63], .12, .17), '操作未成功，克制的下行提示')
    duration=.42
    t=np.arange(int(SR*duration))/SR
    noise=signal.sosfilt(signal.butter(2,[700,4200],btype='bandpass',fs=SR,output='sos'),RNG.normal(0,1,len(t)))
    cast=fade(noise*.23*np.sin(np.pi*t/duration)**2)
    save('sfx/cast.wav',cast,'抛竿轻挥声')
    t=np.arange(int(SR*.56))/SR
    noise=signal.sosfilt(signal.butter(2,2600,fs=SR,output='sos'),RNG.normal(0,1,len(t)))
    phase=2*np.pi*np.cumsum(750*np.exp(-t*9)+210)/SR
    splash=fade((.12*noise+.18*np.sin(phase))*np.exp(-t*9),.004)
    splash+=np.pad(tone(1230,.14,.08),(int(.15*SR),len(t)-int(.15*SR)-int(.14*SR)))
    save('sfx/splash.wav',splash,'浮漂入水与咬钩水滴声')
    save('sfx/catch.wav',sequence([523.25,783.99,1046.5,1318.51],.095,.22),'收获鱼获的短庆祝声')


def music():
    # 24 bars in 4/4 at quarter-note=72: 80 seconds.
    # Cmaj9 / Am9 / Fmaj9 / G6; circular tails preserve the loop boundary.
    beat=60/72
    bars=24
    duration=bars*4*beat
    n=int(round(duration*SR))
    mix=np.zeros((n,2),dtype=np.float64)
    chords=[(48,[60,64,67,71,74]),(45,[57,60,64,67,71]),
            (41,[57,60,64,67,72]),(43,[55,59,62,67,69])]

    def add(y,start,pan=.0,gain=1):
        ids=(np.arange(len(y))+int(round(start*SR)))%n
        np.add.at(mix[:,0],ids,y*gain*np.sqrt((1-pan)/2))
        np.add.at(mix[:,1],ids,y*gain*np.sqrt((1+pan)/2))

    def note(midi,length=2.5,soft=False):
        freq=440*2**((midi-69)/12)
        t=np.arange(int(length*SR))/SR
        decay=1.25 if soft else 2.5
        y=(np.sin(2*np.pi*freq*t)*np.exp(-t*decay)+
           .26*np.sin(2*np.pi*freq*2.001*t)*np.exp(-t*decay*1.6)+
           .055*np.sin(2*np.pi*freq*3.997*t)*np.exp(-t*decay*3))
        return fade(y,.012 if soft else .004)

    motifs=[[0,2,1,4,2,1],[2,1,0,1,2,4],[4,2,1,0,2,1]]
    for bar in range(bars):
        root,chord=chords[bar%4]
        start=bar*4*beat
        # Gentle bass, a mellow broken chord, and a sparse upper melody.
        add(note(root,3.4,True),start,-.12,.068)
        for step,idx in enumerate([0,2,1,3,2,4,1,2]):
            add(note(chord[idx],2.2),start+step*beat/2,(-1 if step%2 else 1)*.33,.042)
        motif=motifs[(bar//4)%3]
        if bar%4 != 3:
            for step,idx in enumerate(motif[:4 if bar%2 else 6]):
                add(note(chord[idx]+12,2.8,True),start+(step*.5+.25)*beat,.22,.046)
        # Quiet sustained warmth, slowly modulated without drums or vocals.
        for pitch in chord[:3]:
            t=np.arange(int(4*beat*SR))/SR
            f=440*2**((pitch-69)/12)
            pad=np.sin(2*np.pi*f*t+.25*np.sin(2*np.pi*.16*t))*np.sin(np.pi*t/(4*beat))**2
            add(pad,start,-.25,.008)
    dry=mix.copy()
    for delay,gain in [(.173,.13),(.293,.09),(.431,.055),(.617,.03)]:
        mix+=np.roll(dry,int(delay*SR),axis=0)[:,::-1]*gain
    mix*=.48/np.max(np.abs(mix))
    # Remove the tiny sample discontinuity from floating-point note boundaries.
    k=128
    boundary=(mix[0]+mix[-1])/2
    mix[:k]+=(boundary-mix[0])*np.linspace(1,0,k)[:,None]
    mix[-k:]+=(boundary-mix[-1])*np.linspace(0,1,k)[:,None]
    save('music/pond_daylight.wav',mix,'池畔晴日：80秒原创钢琴、木琴与柔和铺底，无鼓点、无人声、无缝循环')


def cats():
    sources=[('kitten',None,'https://freesound.org/people/Breviceps/sounds/448084/'),
             ('cat',None,'https://freesound.org/people/qubodup/sounds/813119/'),
             ('kitten_calls',(10.94,11.57),'https://freesound.org/people/Luke100000/sounds/476918/')]
    for i,(name,trim,url) in enumerate(sources,1):
        x,sr=sf.read(HERE/'sources'/f'{name}.wav')
        assert sr==SR
        if trim:x=x[int(trim[0]*SR):int(trim[1]*SR)]
        x-=np.mean(x)
        x=signal.sosfilt(signal.butter(2,160,btype='highpass',fs=SR,output='sos'),x)
        x=fade(x,.012)
        x*=.48/max(np.max(np.abs(x)),.001)
        x=np.pad(x,(int(.018*SR),int(.045*SR)))
        save(f'sfx/meow_{i:02}.wav',x,['短促可爱的喵叫（人声拟猫）','成年猫轻喵','幼猫招呼声'][i-1],url)


if __name__=='__main__':
    import urllib.request
    (HERE/'sources').mkdir(exist_ok=True)
    # libsndfile reads MP3 previews directly; generated game assets are always WAV.
    for name,url in [('kitten','https://cdn.freesound.org/previews/448/448084_9159316-hq.mp3'),
                     ('cat','https://cdn.freesound.org/previews/813/813119_71257-hq.mp3'),
                     ('kitten_calls','https://cdn.freesound.org/previews/476/476918_3211895-hq.mp3')]:
        wav=HERE/'sources'/f'{name}.wav'
        if not wav.exists():
            mp3=wav.with_suffix('.mp3')
            urllib.request.urlretrieve(url,mp3)
            audio,rate=sf.read(mp3)
            if audio.ndim>1: audio=audio.mean(axis=1)
            if rate!=SR:
                import math
                divisor=math.gcd(rate,SR)
                audio=signal.resample_poly(audio,SR//divisor,rate//divisor)
            sf.write(wav,audio,SR,subtype='PCM_16')
    sfx()
    music()
    cats()
    (OUT/'manifest.json').write_text(json.dumps(dict(sampleRate=SR,format='PCM 16-bit WAV',
        musicTitle='池畔晴日 / Pond Daylight',musicBpm=72,musicBars=24,assets=MANIFEST),
        ensure_ascii=False,indent=2)+'\n')
    print(json.dumps(MANIFEST,ensure_ascii=False,indent=2))
