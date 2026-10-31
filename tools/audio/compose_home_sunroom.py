"""Render the original home BGM audition 晴窗小憩; never installs into Unity.

Recorded CC0 Meta nylon guitar and VSCO 2 CE piccolo/viola/cello samples.
All notes, timing, orchestration and room processing are written here; no reference
song audio, score or MIDI is used. Requires numpy, scipy and soundfile.
"""
from pathlib import Path
import argparse
import base64
import concurrent.futures
import hashlib
import json
import math
import urllib.parse
import urllib.request

import numpy as np
from scipy import signal
import soundfile as sf

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'docs/美术/声音/home-sunroom/assets'
META = '251c0be57d5d1b3242b91f53e0cde62ab1cb42d9'
VSCO = '440300901dfe9275fd84e0b7763af1f8443ae62e'
SR = 44100
NAMES = ['C', 'Cs', 'D', 'Ds', 'E', 'F', 'Fs', 'G', 'Gs', 'A', 'As', 'B']

# Fingerstyle bass and inner voices use playable six-string positions.
# Extra colours (major seventh/ninth/sixth) are gently voiced, never random notes.
VOICES = {
    'Gmaj7': [43, 50, 55, 59], 'D/F#': [42, 50, 57, 62],
    'Em7': [40, 50, 55, 59], 'Em7/D': [50, 55, 59, 64],
    'Cmaj7': [48, 52, 55, 59], 'G/B': [47, 50, 55, 59],
    'Am7': [45, 52, 55, 60], 'D7': [50, 54, 57, 60],
    'Bm7': [47, 54, 57, 62], 'E7': [40, 52, 56, 62],
    'D/C': [48, 54, 57, 62], 'G7': [43, 50, 55, 65],
    'G6': [43, 50, 55, 64],
}
CHORD_PCS = {
    'Gmaj7': {7, 11, 2, 6, 9}, 'D/F#': {2, 6, 9, 4},
    'Em7': {4, 7, 11, 2, 6}, 'Em7/D': {4, 7, 11, 2, 6},
    'Cmaj7': {0, 4, 7, 11, 2, 9}, 'G/B': {7, 11, 2, 9},
    'Am7': {9, 0, 4, 7, 11}, 'D7': {2, 6, 9, 0, 4},
    'Bm7': {11, 2, 6, 9, 4}, 'E7': {4, 8, 11, 2, 6},
    'D/C': {2, 6, 9, 0, 4}, 'G7': {7, 11, 2, 5, 9},
    'G6': {7, 11, 2, 4, 9},
}
PROGRESSION = (
    ['Gmaj7', 'D/F#', 'Em7', 'Em7/D', 'Cmaj7', 'G/B', 'Am7', 'D7']
    + ['Gmaj7', 'Bm7', 'Cmaj7', 'D7', 'Em7', 'E7', 'Am7', 'D7']
    + ['Cmaj7', 'D/C', 'Bm7', 'Em7', 'Am7', 'D7', 'Gmaj7', 'G7']
    + ['Cmaj7', 'G/B', 'Am7', 'D7', 'Gmaj7', 'Cmaj7', 'D7', 'G6']
)
# (beat, pitch, gate beats). A four-note descending idea opens each eight-bar
# sentence, answered by an upward gesture. Longer notes and rests are composed.
A = [
    [(0,71,.75),(.75,69,.25),(1,67,1),(2.25,62,.5),(2.75,64,.25),(3,67,.75)],
    [(0,69,1.25),(1.5,66,.5),(2.25,64,.5),(3,62,.75)],
    [(0,67,.5),(.5,71,.5),(1,74,1.5),(2.75,71,.25),(3,69,.5),(3.5,67,.5)],
    [(0,66,.5),(.5,67,.5),(1,71,1),(2.5,69,.5),(3,67,.75)],
    [(0,76,1),(1.25,74,.5),(1.75,71,.25),(2,72,1),(3.25,67,.5)],
    [(0,74,.75),(.75,71,.25),(1,69,.5),(1.5,67,1),(3,62,.75)],
    [(0,69,.5),(.5,71,.5),(1,72,1),(2.25,76,.5),(2.75,74,.25),(3,72,.5),(3.5,69,.5)],
    [(0,66,.5),(.5,69,.5),(1,72,.75),(1.75,71,.25),(2,69,1),(3.25,66,.5)],
]
AP = [
    [(0,71,.75),(.75,69,.25),(1,67,1),(2.25,69,.5),(2.75,71,.25),(3,74,.75)],
    [(0,74,1),(1.25,71,.5),(1.75,69,.25),(2,66,1),(3.25,69,.5)],
    [(0,76,1.25),(1.5,74,.5),(2.25,72,.5),(3,71,.75)],
    [(0,69,.75),(.75,66,.25),(1,64,.5),(1.5,66,.5),(2,62,1.25)],
    [(0,67,.5),(.5,71,.5),(1,74,1),(2.25,76,.5),(2.75,74,.25),(3,71,.75)],
    [(0,71,1.25),(1.5,68,.5),(2.25,66,.5),(3,64,.75)],
    [(0,69,.75),(.75,71,.25),(1,72,1),(2.25,71,.5),(2.75,69,.25),(3,67,.75)],
    [(0,66,.5),(.5,69,.5),(1,72,1),(2.25,69,.5),(3,66,.75)],
]
B = [
    [(0,72,1.5),(1.75,71,.25),(2,67,1),(3.25,69,.5)],
    [(0,69,1),(1.25,66,.5),(2,64,.75),(2.75,66,.25),(3,69,.75)],
    [(0,71,1.5),(1.75,69,.25),(2,66,1),(3.25,69,.5)],
    [(0,67,1),(1.25,71,.5),(2,74,1.25)],
    [(0,76,.75),(.75,74,.25),(1,72,1),(2.25,71,.5),(2.75,69,.25),(3,67,.75)],
    [(0,69,.5),(.5,66,.5),(1,64,.5),(1.5,66,.5),(2,69,1),(3.25,72,.5)],
    [(0,71,1),(1.25,69,.5),(2,67,1.5)],
    [(0,65,.75),(.75,67,.25),(1,71,1),(2.25,69,.5),(3,67,.75)],
]
RETURN = [
    [(0,76,.75),(.75,74,.25),(1,72,1),(2.25,67,.5),(2.75,69,.25),(3,72,.75)],
    A[5], AP[6], A[7],
    [(0,71,.75),(.75,69,.25),(1,67,.75),(2,62,.5),(2.5,67,.5),(3,71,.75)],
    [(0,72,1),(1.25,71,.5),(1.75,69,.25),(2,67,1),(3.25,64,.5)],
    [(0,66,.5),(.5,69,.5),(1,72,.75),(1.75,71,.25),(2,69,.75),(3,66,.75)],
    [(0,67,1),(1,71,.5),(1.5,69,.5),(2,67,1.2)],
]
MELODY = A + AP + B + RETURN
# Subtle phrase rubato, not a rigid click and not a shuffled jazz rhythm.
TEMPOS = ([86,86,87,84,86,85,86,82] + [86,87,86,84,87,86,85,82]
          + [84,85,84,83,85,86,85,82] + [86,85,86,83,86,85,84,80])
PICCOLO_BARS = {10,13,15,16,17,18,19}
SECTION_NAMES = ['吉他 · 阳光落窗', '木管 · 轻声应答', '弦乐 · 暖意展开', '回归 · 安心小憩']


def sha(data):
    return hashlib.sha256(data).hexdigest()


def blob_sha(data):
    return hashlib.sha1(b'blob ' + str(len(data)).encode() + b'\0' + data).hexdigest()


def read_url(url):
    request = urllib.request.Request(url, headers={'User-Agent': 'FishSocial-OriginalHomeBGM'})
    with urllib.request.urlopen(request, timeout=60) as response:
        return response.read()


def guitar_tuning_cents(x, rate, onset, base):
    """Median of three unconstrained YIN estimates; retain pluck pitch movement."""
    estimates = []
    for offset in [.08, .16, .28]:
        start = round(onset + offset*rate)
        window = x[start:start+16384].copy()
        window -= window.mean()
        n = len(window)
        lo, hi = math.floor(rate/4000), min(math.ceil(rate/65), n//3)
        correlation = signal.fftconvolve(window,window[::-1],mode='full')[n-1:n+hi]
        squares = np.r_[0,np.cumsum(window*window)]
        lag = np.arange(hi+1)
        difference = np.maximum((squares[n-lag]+squares[n]-squares[lag]-2*correlation)/(n-lag),0)
        normalized = np.ones_like(difference)
        normalized[1:] = difference[1:]*lag[1:]/np.maximum(np.cumsum(difference[1:]),1e-15)
        candidates = np.flatnonzero(normalized[lo:hi] < .12)
        best = int(candidates[0])+lo if len(candidates) else int(np.argmin(normalized[lo:hi]))+lo
        while best+1 < hi and normalized[best+1] < normalized[best]:
            best += 1
        delta = .5*(normalized[best-1]-normalized[best+1])/(normalized[best-1]-2*normalized[best]+normalized[best+1])
        estimates.append(69+12*math.log2((rate/(best+delta))/440))
    cents = (float(np.median(estimates))-base)*100
    assert abs(cents) < 35, f'Unexpected guitar source pitch: {cents} cents'
    return cents


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--sources', type=Path, required=True, help='Reusable original Meta sample cache')
    parser.add_argument('--orchestra-cache', type=Path, required=True)
    args = parser.parse_args()
    OUT.mkdir(parents=True, exist_ok=True)
    args.orchestra_cache.mkdir(parents=True, exist_ok=True)
    trees = {}
    for library, repo, commit, cache in [
        ('meta', 'facebookincubator/Meta-open-sound', META, args.sources),
        ('vsco', 'sgossner/VSCO-2-CE', VSCO, args.orchestra_cache),
    ]:
        tree_file = cache / ('meta-tree-' + commit + '.json' if library == 'meta' else 'tree.json')
        if not tree_file.exists():
            tree_file.write_bytes(read_url(f'https://api.github.com/repos/{repo}/git/trees/{commit}?recursive=1'))
        tree = json.loads(tree_file.read_text())
        assert tree['sha'] == commit and not tree['truncated']
        trees[library] = {e['path']: e for e in tree['tree'] if e['type'] == 'blob'}

    sources = {}

    def get_source(library, path):
        repo, commit, cache = ('facebookincubator/Meta-open-sound', META, args.sources) if library == 'meta' else ('sgossner/VSCO-2-CE', VSCO, args.orchestra_cache)
        file = cache / Path(path).name
        entry = trees[library][path]
        url = f'https://raw.githubusercontent.com/{repo}/{commit}/' + urllib.parse.quote(path)
        if not file.exists():
            payload = json.loads(read_url(entry['url']))
            assert payload['sha'] == entry['sha'] and payload['encoding'] == 'base64'
            file.write_bytes(base64.b64decode(payload['content']))
        data = file.read_bytes()
        assert blob_sha(data) == entry['sha'], f'Pinned sample mismatch: {path}'
        sources[(library, path)] = dict(library=library, path=path, url=url, commit=commit,
                                     downloadUrl=entry['url'],gitBlob=entry['sha'], sha256=sha(data), license='CC0-1.0')
        return file

    # Keep the full license/README material beside the audition, not only a URL.
    for library, paths in [('meta', ['systems/instruments/meta-nylon-guitar/LICENSE', 'systems/instruments/meta-nylon-guitar/README.md']),
                           ('vsco', ['LICENSE', 'README.md', 'Readme.txt'])]:
        for path in paths:
            file = get_source(library, path)
            (OUT / (library + '-' + Path(path).name)).write_bytes(file.read_bytes())
    assert b'CC0' in (OUT/'meta-LICENSE').read_bytes() and b'CC0' in (OUT/'vsco-LICENSE').read_bytes()

    def guitar_source(midi, picked):
        # Prefer the resonant higher frets of the lower string only in the bass.
        string = 'StringHighE' if midi >= 64 else 'StringB' if midi >= 59 else 'StringG' if midi >= 55 else 'StringD' if midi >= 50 else 'StringA' if midi >= 45 else 'StringLowE'
        layer = 'Vel02Pick' if picked else 'Vel01Finger'
        name = NAMES[midi % 12] + str(midi // 12 - 1)
        return 'meta', f'systems/instruments/meta-nylon-guitar/ogg/INSGuitarNylon_{string}_{layer}_{name}_01.ogg', midi

    orchestra = {
        # VSCO uses C4 for MIDI72, one octave below scientific file naming.
        # These are ACTUAL sounding MIDI pitches, verified from the recordings.
        'piccolo': [(79, 'Woodwinds/Piccolo/Sus/piccolo_G4_sustain1.wav'),
                    (84, 'Woodwinds/Piccolo/Sus/piccolo_C5_sustain1.wav'),
                    (91, 'Woodwinds/Piccolo/Sus/piccolo_G5_sustain1.wav')],
        'viola': [(55, 'Strings/Viola Section/susvib/ViolaEns_susvib_G2_v1_1.wav'),
                  (59, 'Strings/Viola Section/susvib/ViolaEns_susvib_B2_v1_1.wav'),
                  (62, 'Strings/Viola Section/susvib/ViolaEns_susvib_D3_v1_1.wav'),
                  (69, 'Strings/Viola Section/susvib/ViolaEns_susvib_A3_v1_1.wav')],
        'cello': [(47, 'Strings/Cello Section/susvib/susvib_B1_v1_1.wav'),
                  (50, 'Strings/Cello Section/susvib/susvib_D2_v1_1.wav'),
                  (53, 'Strings/Cello Section/susvib/susvib_F2_v1_1.wav'),
                  (57, 'Strings/Cello Section/susvib/susvib_A2_v1_1.wav'),
                  (60, 'Strings/Cello Section/susvib/susvib_C3_v1_1.wav')],
    }
    events = []
    starts = np.concatenate(([0.0], np.cumsum([240/b for b in TEMPOS])))
    frames = round(starts[-1] * SR)

    def note(bar, beat, pitch, duration, instrument, gain, pan, role, late=0):
        unit = 60/TEMPOS[bar]
        start = starts[bar] + beat*unit + late
        event = dict(bar=bar+1, beat=beat, midi=pitch, durationBeats=duration, instrument=instrument,
                     gain=gain, pan=pan, role=role, startSeconds=float(start), gateSeconds=float(duration*unit))
        if instrument == 'guitar':
            event['source'] = guitar_source(pitch, role == 'melody')
        else:
            base, path = min(orchestra[instrument], key=lambda p: abs(p[0]-pitch))
            event['source'] = ('vsco', path, base)
        events.append(event)

    arc = [1.0, .92, 1.02, .88, 1.05, .94, .99, .82]
    for bar, (chord, line) in enumerate(zip(PROGRESSION, MELODY)):
        owner = 'piccolo' if bar in PICCOLO_BARS else 'guitar'
        # Leave air in each sentence; only one instrument carries the melody.
        for j, (beat, pitch, duration) in enumerate(line):
            note(bar, beat, pitch + (12 if owner == 'piccolo' else 0), duration, owner,
                 arc[bar%8] * (1 if beat in (0,2) else .87) * (.64 if owner == 'piccolo' else 1),
                 .2 if owner == 'piccolo' else -.055, 'melody', late=[0,.006,.009,.003][j%4])
        bass, low, mid, high = VOICES[chord]
        # A thumb bass and rolling, lightly displaced inner notes. Two patterns
        # alternate at phrase level, rather than dense identical strummed chords.
        pattern = [(0,bass,1.65,.61),(.52,mid,.85,.29),(1.02,high,.8,.34),
                   (1.53,low,.9,.26),(2,bass,1.45,.49),(2.52,mid,.85,.29),(3.02,high,.75,.28)]
        if bar%4 in (2,3):
            pattern = [(0,bass,1.7,.58),(.53,low,.9,.28),(1.03,mid,.9,.32),(1.54,high,.8,.27),
                       (2,low,1.3,.40),(2.53,mid,.8,.28),(3.04,high,.72,.25)]
        if bar == 31:
            pattern = [(0,bass,2.4,.55),(.035,low,2.3,.27),(.07,mid,2.2,.25),(.105,high,2.1,.24)]
        for beat,pitch,duration,gain in pattern:
            note(bar,beat,pitch,duration,'guitar',gain*arc[bar%8],-.12,'accompaniment')
        # Warm chamber strings emerge only in the middle; no full-time synth pad.
        if 16 <= bar <= 29:
            v_pitch = high if high >= 57 else mid+12
            if bar%2 == 0:
                note(bar,.25,v_pitch,3.55,'viola',.23,.3,'harmony')
            note(bar,.12,bass+12 if bass < 48 else bass,3.65,'cello',.18,-.27,'harmony')

    requested = sorted({(e['source'][0],e['source'][1]) for e in events})
    with concurrent.futures.ThreadPoolExecutor(max_workers=6) as pool:
        files = dict(zip(requested, pool.map(lambda p:get_source(*p), requested)))
    prepared, tuning = {}, {}

    def sample_for(event):
        library, path, base = event['source']
        key = (library,path,event['midi'])
        if key in prepared:
            return prepared[key]
        x, rate = sf.read(files[(library,path)], dtype='float64', always_2d=True)
        x = x.mean(axis=1)
        activity = np.flatnonzero(np.abs(x) > np.max(np.abs(x))*.018)
        cents = guitar_tuning_cents(x,rate,int(activity[0]),base) if library=='meta' else 0
        tuning[key] = -cents
        if library=='meta':
            sources[(library,path)]['originalPitchErrorCents'] = cents
            sources[(library,path)]['tuningCorrectionCents'] = -cents
        trim = max(0,int(activity[0]) - round(.008*rate))
        x = x[trim:]
        # Equal-tempered resampling for sparse orchestra samples; the guitar
        # uses its matching recorded note with a small measured tuning correction.
        # No granular/time-loop texture or synthetic pitch source is used.
        ratio = 2**((event['midi']-base-cents/100)/12)
        new_length = round(len(x)*SR/rate/ratio)
        x = signal.resample_poly(x, SR//math.gcd(SR,rate), rate//math.gcd(SR,rate))
        if ratio != 1:
            x = signal.resample(x,new_length)
        low,high = (72,7900) if library=='meta' else (180,6500) if event['instrument']=='piccolo' else (95,5200)
        x = signal.sosfilt(signal.butter(2,[low,high],btype='bandpass',fs=SR,output='sos'),x)
        # Recorded dynamic envelopes survive; balance the first stable window.
        a,b = (0,round(.35*SR)) if library=='meta' else (round(.12*SR),round(.65*SR))
        level = max(float(np.sqrt(np.mean(x[a:b]**2))), 1e-8)
        x *= .19/level
        prepared[key] = x
        return x

    stems = {k:np.zeros((frames,2),dtype=np.float64) for k in ['guitar','piccolo','viola','cello']}
    for event in events:
        sample = sample_for(event)
        gate = event['gateSeconds']
        instrument = event['instrument']
        release = .18 if instrument=='guitar' else .16 if instrument=='piccolo' else .34
        attack = .002 if instrument=='guitar' else .045 if instrument=='piccolo' else .28
        length = min(len(sample), round((gate+release)*SR))
        y = sample[:length].copy()
        attack_frames = min(round(attack*SR),length//3)
        y[:attack_frames] *= np.sin(np.linspace(0,np.pi/2,attack_frames))**2
        release_frames = min(round(release*SR),length//2)
        y[-release_frames:] *= np.cos(np.linspace(0,np.pi/2,release_frames))**2
        y[0] = y[-1] = 0
        if instrument != 'guitar':
            t = np.arange(length)/SR
            # A continuous restrained breath/bow swell, never retriggered noise.
            y *= .78+.22*np.sin(np.pi*np.minimum(t/max(gate,.1),1))
        start = round(event['startSeconds']*SR)
        index = (start+np.arange(length)) % frames
        pan = event['pan']
        dst = stems[instrument]
        for channel,gain in enumerate([math.sqrt((1-pan)/2),math.sqrt((1+pan)/2)]):
            np.add.at(dst[:,channel],index,y*event['gain']*gain)
        event['pcmStartFrame'] = start
        event['renderedFrames'] = length
        event['pitchShiftSemitones'] = event['midi']-event['source'][2]
        event['sourceTuningCorrectionCents'] = tuning[tuple(event['source'][:2])+(event['midi'],)]

    # Small, dark room reflections preserve pluck definition. Circular delays
    # carry the short tail across the musical loop without an inserted gap.
    dry = sum(stems.values())
    mix = dry.copy()
    for seconds,gain in [(.041,.065),(.067,.052),(.101,.038),(.149,.026),(.211,.017),(.293,.010)]:
        delay = round(seconds*SR)
        reflection = signal.sosfilt(signal.butter(1,4000,fs=SR,output='sos'),dry,axis=0)
        mix += np.roll(reflection,delay,axis=0)[:,::-1]*gain
    peak = float(np.max(np.abs(signal.resample_poly(mix,4,1,axis=0))))
    master = .33/peak
    mix *= master
    full = OUT/'home-sunroom.wav'
    sf.write(full,mix,SR,subtype='PCM_16')
    # The opening eight bars are an honest excerpt with a gentle ending, not
    # a separately mastered loud demo. Full track remains the loop candidate.
    excerpt = mix[:round(starts[8]*SR)].copy()
    n = round(.65*SR)
    excerpt[-n:] *= np.cos(np.linspace(0,np.pi/2,n))[:,None]**2
    sf.write(OUT/'excerpt.wav',excerpt,SR,subtype='PCM_16')

    chapters = [dict(title=title,startSeconds=float(starts[i*8]),endSeconds=float(starts[(i+1)*8]),
                     bars=[i*8+1,(i+1)*8]) for i,title in enumerate(SECTION_NAMES)]
    score = dict(title='晴窗小憩',englishTitle='A Little Rest by the Sunlit Window',version=1,
                 status='audition-not-adopted',date='2026-10-31',key='G major',timeSignature=[4,4],
                 nominalBpm=85,tempoRangeBpm=[min(TEMPOS),max(TEMPOS)],bars=32,
                 durationSeconds=frames/SR,temposBpm=TEMPOS,chapters=chapters,
                 harmony=PROGRESSION,barStartSeconds=starts.tolist(),events=events,
                 instruments=['classical nylon guitar','piccolo','viola section','cello section'],
                 piano=False,referenceUse='Instrument/performance direction only; original notes and arrangement; no reference audio or MIDI.',
                 musicalDesign='Descending four-note motif; varied answer, guitar/woodwind handover, '
                 'predominantly stepwise links, moving bass, secondary E7-Am7 and G7-Cmaj7; '
                 'four eight-bar phrases with deliberate breath and phrase rubato.',
                 room=dict(type='small dark room reflections',maxDelaySeconds=.293,wetSum=.208),
                 sampleMethod='Real recorded note samples; matching guitar notes with measured A440 tuning corrections below35cents; nearest-pitch orchestra resampling; no synthetic oscillators.')
    (OUT/'score.json').write_text(json.dumps(score,ensure_ascii=False,indent=2)+'\n')
    assets = []
    for path in [full,OUT/'excerpt.wav']:
        x,rate = sf.read(path,always_2d=True)
        tp = float(np.max(np.abs(signal.resample_poly(x,4,1,axis=0))))
        rms = float(np.sqrt(np.mean(x*x)))
        assets.append(dict(path=path.name,sha256=sha(path.read_bytes()),sampleRate=rate,channels=2,
                           subtype='PCM_16',frames=len(x),durationSeconds=len(x)/rate,
                           truePeakDbTP=20*math.log10(tp),rmsDbFS=20*math.log10(rms),
                           loop=path==full,seamStep=float(np.max(np.abs(x[0]-x[-1])))))
    manifest = dict(title=score['title'],status='audition-not-adopted',date='2026-10-31',
                    assets=assets,scoreSha256=sha((OUT/'score.json').read_bytes()),
                    compositionScript=str(Path(__file__).relative_to(ROOT)),
                    compositionScriptSha256=sha(Path(__file__).read_bytes()),
                    sources=sorted(sources.values(),key=lambda p:(p['library'],p['path'])),
                    masterGain=master,stemsRmsDbFS={k:20*math.log10(float(np.sqrt(np.mean(v*v)))*master) for k,v in stems.items()},
                    adoptionRequired=True,formalAssetsModified=False)
    (OUT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n')
    (OUT/'CREDITS.md').write_text('''# 《晴窗小憩》试听来源

原创旋律、和声、编配与时间表见 score.json；重现脚本见 tools/audio/compose_home_sunroom.py。
本曲为真实乐器单音采样编曲，非真人整曲演奏，非音频生成模型产物。

- 古典尼龙吉他：Meta Creative Audio / Meta Platforms, Inc.，Meta Nylon Guitar，CC0-1.0。
  https://github.com/facebookincubator/Meta-open-sound/tree/251c0be57d5d1b3242b91f53e0cde62ab1cb42d9/systems/instruments/meta-nylon-guitar
- 短笛、中提琴组、大提琴组：VSCO 2 Community Edition，Versilian Studios / Sam Gossner；录音署名 Sam Gossner & Simon Dalzell，切样 Elan Hickler / Soundemote，CC0-1.0。
  https://github.com/sgossner/VSCO-2-CE/tree/440300901dfe9275fd84e0b7763af1f8443ae62e

完整上游 LICENSE 与 README 随试听保留；每个实际输入的固定 commit、Git blob 和 SHA-256 见 manifest.json。
参考曲《将世事高枕》仅提供配器与演奏方向，没有使用其旋律、MIDI、录音或采样。
''')
    print(json.dumps(dict(assets=assets,chapters=chapters,events=len(events),sources=len(sources),
                          stemsRmsDbFS=manifest['stemsRmsDbFS']),ensure_ascii=False,indent=2))


if __name__ == '__main__':
    main()
