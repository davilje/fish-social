"""Original singable nylon-guitar theme v5. Only writes docs/美术/声音/v5/assets.

Requires numpy scipy soundfile. --score-only prints the auditable score without downloading or writing assets.
Lead uses actual CC0 Meta Vel02Pick recordings at original pitch; comp uses Vel01Finger.
"""
from pathlib import Path
import argparse, base64, hashlib, json, math, urllib.request
import numpy as np
import soundfile as sf
from scipy import signal

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'docs/美术/声音/v5/assets'
SR,BPM=44100,90
BEAT=60/BPM
FRAMES=round(32*4*BEAT*SR)
TREE='251c0be57d5d1b3242b91f53e0cde62ab1cb42d9'
TREE_PINS_SHA256='ccc9df9bbabb4eff7c8b5c1db86fb136ea3df6c8753530a0166f4b7290c713f9'
REPO='https://api.github.com/repos/facebookincubator/Meta-open-sound'
NYLON='systems/instruments/meta-nylon-guitar/'
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--sources',type=Path,default=Path.home()/'.cache/fish-social-audio-v3')
parser.add_argument('--score-only',action='store_true')
args=parser.parse_args();CACHE=args.sources
OUT.mkdir(parents=True,exist_ok=True)
NAMES=['C','C#','D','D#','E','F','F#','G','G#','A','A#','B']
CHORDS={'G':{7,11,2},'G/B':{7,11,2},'Cadd9':{0,4,7,2},'D7':{2,6,9,0},'Em7':{4,7,11,2},'Am7':{9,0,4,7}}
FUNCTIONS={'G':'I','G/B':'I6','Cadd9':'IV(add9)','D7':'V7','Em7':'vi7','Am7':'ii7'}
VOICINGS={
    'G':[('StringLowE',43),('StringG',55),('StringB',59),('StringD',50)],
    'G/B':[('StringA',47),('StringG',55),('StringB',59),('StringD',50)],
    'Cadd9':[('StringA',48),('StringD',52),('StringB',62),('StringG',55)],
    'D7':[('StringD',50),('StringD',54),('StringB',60),('StringA',45)],
    'Em7':[('StringLowE',40),('StringG',55),('StringB',62),('StringA',47)],
    'Am7':[('StringA',45),('StringG',55),('StringB',60),('StringD',52)],
}
# onset, MIDI, duration in beats. Gates INCLUDE release, and intentional suspensions cross a barline.
A=[
    [(.5,74,.5),(1,71,1),(2,69,.5),(2.5,71,.5),(3,67,.5),(3.5,69,.5)],
    [(0,71,1.5),(1.5,69,.5),(2,67,1)],
    [(0,76,1),(1,74,.5),(1.5,72,.5),(2,71,.5),(2.5,72,.5),(3,76,1.5)],
    [(.5,74,.5),(1,72,.5),(1.5,69,.5),(2,66,1),(3,69,.5)],
    [(0,71,1),(1,74,.5),(1.5,76,.5),(2,74,1),(3,71,.5),(3.5,69,.5)],
    [(0,67,1),(1,69,.5),(1.5,72,.5),(2,71,.5),(2.5,72,.5),(3,76,1)],
    [(0,74,.5),(.5,72,.5),(1,69,1),(2,67,.5),(2.5,66,.5),(3,64,.5),(3.5,66,.5)],
    [(0,67,1),(1,71,.5),(1.5,69,.5),(2,67,1.5)],
]
AP=[
    A[0],
    [(0,71,1),(1,74,.5),(1.5,71,.5),(2,69,.5),(2.5,67,.5)],
    [(0,76,1.5),(1.5,74,.5),(2,72,.5),(2.5,71,.5),(3,72,1)],
    [(0,74,.5),(.5,72,.5),(1,69,1),(2,67,.5),(2.5,66,.5),(3,69,.5)],
    [(0,71,.5),(.5,74,.5),(1,76,1),(2,74,1),(3,71,.5),(3.5,67,.5)],
    [(0,72,.5),(.5,71,.5),(1,69,1),(2,67,.5),(2.5,69,.5),(3,72,1)],
    [(0,69,1),(1,72,.5),(1.5,74,.5),(2,72,1),(3,69,.5),(3.5,66,.5)],
    A[7],
]
B=[
    [(0,76,1.5),(1.5,74,.5),(2,71,.5),(2.5,69,.5),(3,67,.5),(3.5,71,.5)],
    [(0,74,1),(1,76,.5),(1.5,78,.5),(2,76,1.5)],
    [(0,76,.5),(.5,74,.5),(1,72,1),(2,74,.5),(2.5,76,.5),(3,74,1)],
    [(0,72,1.5),(1.5,71,.5),(2,72,1),(3,76,.5),(3.5,74,1)],
    [(.5,72,.5),(1,71,.5),(1.5,69,.5),(2,67,1),(3,69,.5),(3.5,72,.5)],
    [(0,76,1.5),(1.5,74,.5),(2,72,1),(3,71,.5),(3.5,69,.5)],
    [(0,69,1),(1,72,.5),(1.5,74,.5),(2,72,.5),(2.5,69,.5),(3,67,.5),(3.5,66,.5)],
    [(0,64,.5),(.5,66,.5),(1,69,1),(2,67,.5),(2.5,66,.5),(3,64,.5),(3.5,66,.5)],
]
RETURN=[
    [(0,67,.5),(.5,74,.5),(1,71,1),(2,69,.5),(2.5,71,.5),(3,67,.5),(3.5,69,.5)],
    [(0,71,1),(1,74,.5),(1.5,71,.5),(2,69,.5),(2.5,67,.5),(3,69,.5),(3.5,71,.5)],
    A[2],A[3],
    [(0,71,.5),(.5,74,.5),(1,76,1),(2,74,.5),(2.5,71,.5),(3,69,.5),(3.5,67,.5)],
    [(0,72,1),(1,71,.5),(1.5,69,.5),(2,67,.5),(2.5,69,.5),(3,72,1)],
    [(0,74,.5),(.5,72,.5),(1,69,1),(2,67,.5),(2.5,66,.5),(3,69,.5),(3.5,66,.5)],
    A[7],
]
THEME=A+AP+B+RETURN
PROGRESSION=['G','G/B','Cadd9','D7','Em7','Am7','D7','G']*2+['Em7','Em7','Cadd9','Cadd9','Am7','Am7','D7','D7']+['G','G/B','Cadd9','D7','Em7','Am7','D7','G']
SECTIONS=['A']*8+['A-prime']*8+['B']*8+['A-varied-return']*8


def sha(data):return hashlib.sha256(data).hexdigest()
def name(midi):return NAMES[midi%12]+str(midi//12-1)
def key(string,midi,pick=False):return ('nylon_pick_' if pick else 'nylon_')+string+'_'+name(midi).replace('#','s')
def harmony_at(beat):return PROGRESSION[min(31,int(beat//4))]


def make_score():
    bars=[];all_events=[];ordinal=0
    for i,(notes,chord,section) in enumerate(zip(THEME,PROGRESSION,SECTIONS)):
        melody=[]
        for onset,midi,duration in notes:
            ordinal+=1;string='StringHighE' if midi>=64 else 'StringB'
            event=dict(id=f'm{ordinal:03}',startBeat=onset,durationBeats=duration,endBeat=onset+duration,
                absoluteStartBeat=i*4+onset,absoluteEndBeat=i*4+onset+duration,midi=midi,note=name(midi),
                string=string,sourceKey=key(string,midi,True),layer='Vel02Pick',ornaments=[])
            melody.append(event);all_events.append(event)
        bass,a,b,fifth=VOICINGS[chord]
        comp=[]
        for onset,voice,duration,velocity in [(0,bass,1.30,.75),(1.5,a,.40,.50),(1.5,b,.40,.50),
                                            (2,fifth,1.30,.65),(3.5,a,.35,.40),(3.5,b,.35,.40)]:
            string,midi=voice
            comp.append(dict(startBeat=onset,durationBeats=duration,endBeat=onset+duration,midi=midi,note=name(midi),
                string=string,sourceKey=key(string,midi),layer='Vel01Finger',velocity=velocity))
        bars.append(dict(bar=i+1,section=section,startBeat=i*4,harmony=chord,function=FUNCTIONS[chord],
            melodyNotes=melody,accompanimentNotes=comp,phrasePosition=i%8+1))
    for i,bar in enumerate(bars):
        start=i*4;end=start+4
        active=sorted((max(start,e['absoluteStartBeat']),min(end,e['absoluteEndBeat']))
            for e in all_events if e['absoluteStartBeat']<end and e['absoluteEndBeat']>start)
        cursor=start;rests=[]
        for left,right in active:
            if left>cursor:rests.append(dict(startBeat=cursor-start,endBeat=left-start,durationBeats=left-cursor))
            cursor=max(cursor,right)
        if cursor<end:rests.append(dict(startBeat=cursor-start,endBeat=4,durationBeats=end-cursor))
        bar['melodyRests']=rests
        bar['carryIn']=[dict(noteId=e['id'],note=e['note'],untilBeat=e['absoluteEndBeat']-start)
            for e in all_events if e['absoluteStartBeat']<start<e['absoluteEndBeat']]
    return dict(version=5,title='Sunlit Conversation / 日光下的闲谈',tempoBpm=BPM,timeSignature=[4,4],tonality='G major',
        bars=32,form=['A','A-prime','B','A-varied-return'],
        melodyDesign='Upbeat D-B neighbour motif; dotted-quarter answers, prepared9-8 and4-3 suspensions, '
            'stepwise links and contrasting B register; varied return and tonic cadence. No random pitch/timing.',
        strongBeats=[0,2],releaseIncludedInDuration=True,gatePolicy='Global mono lead across all strings; '
            'only explicitly prepared suspensions cross harmony boundaries. All comp releases end before a barline.',
        leadLayer='Vel02Pick (actual light picked recording, NOT brightEQ on fingerstyle)',
        accompanimentLayer='Vel01Finger',attackMs={'lead':1,'comp':4},releaseMs={'lead':35,'comp':40},
        room='Dry recorded room only; no added delay or reverb',
        dynamics='Four 2bar sentences within each8bar phrase; small planned rise and cadence relaxation, no random gain',
        score=bars)


def audit_score(score):
    events=[e for bar in score['score'] for e in bar['melodyNotes']]
    for a,b in zip(events,events[1:]):assert a['absoluteEndBeat']<=b['absoluteStartBeat'],('lead overlap',a['id'],b['id'])
    decorations=[]
    for i,event in enumerate(events):
        left,right=event['absoluteStartBeat'],event['absoluteEndBeat'];midi=event['midi']
        cuts=[left]+[b*4 for b in range(1,32) if left<b*4<right]+[right]
        for start,end in zip(cuts,cuts[1:]):
            harmony=harmony_at(start)
            if midi%12 in CHORDS[harmony]:continue
            assert i>0 and i+1<len(events),('unresolved outer note',event['id'])
            prior,nxt=events[i-1],events[i+1]
            if start>left:
                assert midi%12 in CHORDS[harmony_at(left)],('unprepared suspension',event['id'])
                assert 0<midi-nxt['midi']<=2 and nxt['absoluteStartBeat']==right
                assert nxt['midi']%12 in CHORDS[harmony_at(right)]
                kind='prepared-suspension'
                preparation=dict(noteId=event['id'],atBeat=left,harmony=harmony_at(left),midi=midi)
            else:
                a=midi-prior['midi'];b=nxt['midi']-midi
                assert 0<abs(a)<=2 and 0<abs(b)<=2,('nonstep ornament',event['id'],a,b)
                if prior['midi']==nxt['midi']:kind='neighbour'
                else:
                    assert a*b>0,('nonpassing contour',event['id'])
                    kind='passing'
                assert nxt['midi']%12 in CHORDS[harmony_at(nxt['absoluteStartBeat'])],('unresolved decoration',event['id'])
                preparation=dict(noteId=prior['id'],midi=prior['midi'],atBeat=prior['absoluteStartBeat'])
            accented=any(start<=bar*4+beat<end for bar in range(32) for beat in [0,2])
            record=dict(noteId=event['id'],kind=kind,accented=accented,harmony=harmony,
                fromBeat=start,toBeat=end,midi=midi,preparation=preparation,
                resolution=dict(noteId=nxt['id'],midi=nxt['midi'],atBeat=nxt['absoluteStartBeat'],harmony=harmony_at(nxt['absoluteStartBeat'])))
            decorations.append(record);event['ornaments'].append(record)
    strong_chord=strong_decorated=strong_rest=0
    for bar in score['score']:
        start=bar['startBeat'];chord=CHORDS[bar['harmony']]
        for beat in [start,start+2]:
            active=[e for e in events if e['absoluteStartBeat']<=beat<e['absoluteEndBeat']]
            assert len(active)<=1
            if not active:strong_rest+=1
            elif active[0]['midi']%12 in chord:strong_chord+=1
            else:
                assert any(d['noteId']==active[0]['id'] and d['fromBeat']<=beat<d['toBeat'] for d in decorations)
                strong_decorated+=1
        for e in bar['accompanimentNotes']:
            assert e['midi']%12 in chord and 0<=e['startBeat']<e['endBeat']<=3.85
        sounding=sum(min(start+4,e['absoluteEndBeat'])-max(start,e['absoluteStartBeat']) for e in events
            if e['absoluteStartBeat']<start+4 and e['absoluteEndBeat']>start)
        assert abs(sounding+sum(r['durationBeats'] for r in bar['melodyRests'])-4)<1e-9
    density=len(events)/32
    assert 4<=density<=6, density
    return dict(scope='Objective source/score/PCM validation; no listening approval',
        globalMelodyEvents=len(events),meanMelodyOnsetsPerBar=density,
        noteCountsPerBar=[len(b['melodyNotes']) for b in score['score']],
        strongBeats={'chordTone':strong_chord,'preparedOrResolvedDecoration':strong_decorated,'rest':strong_rest},
        preparedSuspensions=sum(d['kind']=='prepared-suspension' for d in decorations),
        passingTones=sum(d['kind']=='passing' for d in decorations),neighbours=sum(d['kind']=='neighbour' for d in decorations),
        ornaments=decorations,globalLeadMonophonic=True,unannotatedNonChordTones=0,
        accompanimentEvents=32*6,oldCompAcrossHarmonyBoundary=0,
        leadPitchRangeMidi=[min(e['midi'] for e in events),max(e['midi'] for e in events)],
        randomization=False,melodyLayer='Vel02Pick',compLayer='Vel01Finger')

score=make_score();score_audit=audit_score(score);score['audit']=score_audit
if args.score_only:
    print(json.dumps(score,indent=2,ensure_ascii=False));raise SystemExit(0)

# Every source is a pinned official Git blob; no generated oscillator or pitch-shift substitute.
tree_path=CACHE/'meta-tree.json'
if not tree_path.exists():
    CACHE.mkdir(parents=True,exist_ok=True)
    req=urllib.request.Request(REPO+'/git/trees/'+TREE+'?recursive=1',
        headers={'User-Agent':'FishSocial-CC0-Theme-v5'})
    with urllib.request.urlopen(req,timeout=60) as response:tree_bytes=response.read()
    tree=json.loads(tree_bytes)
else:
    tree_bytes=tree_path.read_bytes();tree=json.loads(tree_bytes)
assert tree['sha']==TREE and not tree['truncated'], 'Incorrect or incomplete pinned official tree'
# Pin the entire canonical path/mode/type/blob mapping, independent of JSON whitespace/API field order.
canonical=json.dumps(sorted((e['path'],e['mode'],e['type'],e['sha']) for e in tree['tree']),
    separators=(',',':')).encode()
assert sha(canonical)==TREE_PINS_SHA256, 'Official source tree mapping differs from approved pin'
if not tree_path.exists():tree_path.write_bytes(tree_bytes)
entries={e['path']:e for e in tree['tree'] if e['type']=='blob'}
source_records={};samples={}


def git_blob(raw):return hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()


def pinned_file(source_path,local_name=None):
    entry=entries[source_path]
    path=CACHE/(local_name or Path(source_path).name)
    if not path.exists():
        CACHE.mkdir(parents=True,exist_ok=True)
        req=urllib.request.Request(entry['url'],headers={'User-Agent':'FishSocial-CC0-Theme-v5'})
        with urllib.request.urlopen(req,timeout=60) as response:payload=json.load(response)
        assert payload['sha']==entry['sha']
        path.write_bytes(base64.b64decode(payload['content']))
    raw=path.read_bytes()
    assert git_blob(raw)==entry['sha'],('invalid pinned Git blob',path)
    return path,entry,raw


license_path,license_entry,license_raw=pinned_file(NYLON+'LICENSE','meta-nylon-LICENSE')
readme_path,readme_entry,readme_raw=pinned_file(NYLON+'README.md','meta-nylon-README.md')
assert b'CC0' in license_raw and b'Vel02' in readme_raw


def source_record(string,midi,pick):
    layer='Vel02Pick' if pick else 'Vel01Finger'
    filename=f'INSGuitarNylon_{string}_{layer}_{name(midi).replace("#","s")}_01.ogg'
    path,entry,raw=pinned_file(NYLON+'ogg/'+filename)
    return path,dict(key=key(string,midi,pick),author='Meta Creative Audio / Meta Platforms, Inc.',
        page=f'https://github.com/facebookincubator/Meta-open-sound/tree/{TREE}/{NYLON.rstrip("/")}',
        url=entry['url'],license='CC0-1.0',licenseFile=license_path.name,licenseSha256=sha(license_raw),
        licenseGitBlob=license_entry['sha'],file=filename,sha256=sha(raw),gitTree=TREE,gitBlob=entry['sha'],
        sourcePath=entry['path'],midi=midi,string=string,layer=layer,
        technique='Real light picked nylon guitar' if pick else 'Real soft fingerstyle nylon guitar',
        sourceFormat='Official mono OGG Vorbis, 48kHz; source collection recorded as24bit stereo WAV')


def raw_onset(path):
    x,rate=sf.read(path,dtype='float64')
    if x.ndim==2:x=x.mean(axis=1)
    active=np.flatnonzero(np.abs(x)>np.max(np.abs(x))*.025)
    trim=max(0,int(active[0])-round(.006*rate))
    return x[trim:],rate,trim


def clarity_evidence():
    evidence=[]
    for midi in [67,71,74]:
        pair={}
        for pick in [False,True]:
            path,record=source_record('StringHighE',midi,pick)
            x,rate,trim=raw_onset(path)
            first=x[:round(.300*rate)]
            freq,power=signal.welch(first,fs=rate,nperseg=4096,detrend='constant')
            total=float(np.sum(power));hf=float(np.sum(power[(freq>=2500)&(freq<=6200)]))
            pair[record['layer']]=dict(sourceKey=record['key'],file=record['file'],sha256=record['sha256'],
                gitBlob=record['gitBlob'],sampleRate=rate,trimFrames=trim,hfEnergyRatio=hf/total)
        evidence.append(dict(midi=midi,note=name(midi),sources=pair,
            pickVsFingerHfRatioDb=float(10*np.log10(pair['Vel02Pick']['hfEnergyRatio']/pair['Vel01Finger']['hfEnergyRatio']))))
    return dict(method='Original48kHz sources, same onset threshold2.5% peak minus6ms; '
        'first300ms Welch4096 detrendconstant. 2.5–6.2kHz PSD sum / total PSD sum, independent of level; no EQ.',
        frequencyBandHz=[2500,6200],samples=evidence,
        interpretation='Objective changed source layer/spectral evidence; not a listening-quality approval')


clarity=clarity_evidence()


def recorded_sample(string,midi,pick):
    sample_key=key(string,midi,pick)
    if sample_key in samples:return samples[sample_key]
    path,record=source_record(string,midi,pick)
    x,rate,trim=raw_onset(path)
    if rate!=SR:
        d=math.gcd(rate,SR);x=signal.resample_poly(x,SR//d,rate//d)
    x=x-np.mean(x)
    # Gentle rumble control only. Light pick's recorded definition is retained, without a treble shelf.
    highpass=80 if pick else 65
    x=signal.sosfilt(signal.butter(2,highpass,btype='highpass',fs=SR,output='sos'),x)
    if not pick:x=signal.sosfilt(signal.butter(2,6500,btype='lowpass',fs=SR,output='sos'),x)
    x/=max(np.max(np.abs(x)),1e-9)
    early_rms=float(np.sqrt(np.mean(x[:round(.300*SR)]**2)))
    balance=min(1.6,.26/max(early_rms,1e-9));x*=balance
    record['processing']=dict(pitchShiftSemitones=0,trimLeadInSeconds=trim/rate,
        highPassHz=highpass,lowPassHz=None if pick else 6500,trebleShelfDb=0,
        normalization='Per-source peak then first300ms RMS balance to.26, max1.6; no compression/limiter',
        balanceGain=balance,attackMs=1 if pick else 4,releaseMs=35 if pick else 40,
        room='No added room, delay or reverb; source recording only')
    source_records[sample_key]=record;samples[sample_key]=x
    return x


def gated_note(sample,length,pick):
    y=np.zeros(length);n=min(length,len(sample));y[:n]=sample[:n]
    attack=min(round((.001 if pick else .004)*SR),length//3)
    release=min(round((.035 if pick else .040)*SR),length//3)
    y[:attack]*=np.sin(np.linspace(0,np.pi/2,attack))**2
    y[-release:]*=np.cos(np.linspace(0,np.pi/2,release))**2
    y[0]=y[-1]=0
    return y


def phrase_gain(bar,note):
    # Planned 4+4 arc, one restrained contrast, relaxed cadence; never randomized.
    arc=[.96,.92,1.00,.92,1.02,1.00,.98,.88][(bar['bar']-1)%8]
    section={'A':1,'A-prime':1.015,'B':1.035,'A-varied-return':1.015}[bar['section']]
    beat=note['startBeat'];accent=1 if beat in [0,2] else .94 if beat%1==0 else .89
    return arc*section*accent


def render_stems(score):
    lead=np.zeros((FRAMES,2));comp=np.zeros_like(lead)
    occupancy=np.zeros(FRAMES,dtype=np.uint8);rendered=[];comp_end_checks=0
    for bar in score['score']:
        for group,dst,pan,pick in [('melodyNotes',lead,.035,True),('accompanimentNotes',comp,-.10,False)]:
            for note in bar[group]:
                # A held note remains ONE physical event across the barline: no perbar split/retrigger.
                start=round((bar['startBeat']+note['startBeat'])*BEAT*SR)
                end=round((bar['startBeat']+note['endBeat'])*BEAT*SR)
                y=gated_note(recorded_sample(note['string'],note['midi'],pick),end-start,pick)
                gain=phrase_gain(bar,note) if pick else note['velocity']
                note['renderGainBeforeStemMix']=gain
                note['pcmStartFrame']=start;note['pcmEndFrameExclusive']=end
                if pick:
                    occupancy[start:end]+=1
                    rendered.append(dict(noteId=note['id'],startFrame=start,endFrameExclusive=end,
                        sourceKey=note['sourceKey'],triggerCount=1,gateEndZero=bool(y[-1]==0)))
                else:
                    assert end<=round((bar['startBeat']+4)*BEAT*SR) and y[-1]==0
                    comp_end_checks+=1
                for channel,level in enumerate([math.sqrt((1-pan)/2),math.sqrt((1+pan)/2)]):
                    dst[start:end,channel]+=y*gain*level
    assert occupancy.max()==1
    lr=float(np.sqrt(np.mean(lead**2)));cr=float(np.sqrt(np.mean(comp**2)))
    comp*=lr*10**(-12/20)/max(cr,1e-12)
    measured=20*np.log10(np.sqrt(np.mean(comp**2))/lr)
    boundaries=[]
    for i in range(1,32):
        frame=round(i*4*BEAT*SR)
        assert np.max(np.abs(comp[frame-2:frame+1]))==0
        boundaries.append(frame)
    carries=[e for bar in score['score'] for e in bar['melodyNotes'] if e['endBeat']>4]
    for e in carries:
        frame=round((int(e['absoluteStartBeat']//4)+1)*4*BEAT*SR)
        assert e['pcmStartFrame']<frame<e['pcmEndFrameExclusive']
        # The physical waveform is alive through the harmony change; no secondattack/gate was inserted.
        assert np.sqrt(np.mean(lead[frame-100:frame+100]**2))>0
    return lead,comp,dict(leadMaxConcurrentPcmNotes=int(occupancy.max()),leadPhysicalTriggerCount=len(rendered),
        preparedSuspensionSingleTriggerCount=len(carries),globalLeadEvents=rendered,
        accompanimentGateEndZeroChecks=comp_end_checks,accompanimentBoundaryZeroFrames=boundaries,
        accompanimentBelowLeadDb=float(measured),noAddedRoomOrTailOutsideGate=True)


def metrics(path):
    x,rate=sf.read(path,dtype='float64');peak=float(np.max(np.abs(x)))
    active=np.max(np.abs(x),axis=1)>peak*.025
    return dict(duration=round(len(x)/rate,6),frames=len(x),channels=x.shape[1],sampleRate=rate,format='PCM_16',
        peak=peak,truePeak4x=float(np.max(np.abs(signal.resample_poly(x,4,1,axis=0)))),
        rms=float(np.sqrt(np.mean(x*x))),effectiveRms=float(np.sqrt(np.mean(x[active]**2))),
        seamDifference=float(np.max(np.abs(x[0]-x[-1]))),sha256=sha(path.read_bytes()))


lead,comp,pcm_audit=render_stems(score)
mix=lead+comp
truepeak=float(np.max(np.abs(signal.resample_poly(mix,4,1,axis=0))))
master_gain=.135/truepeak;mix*=master_gain
assert np.max(np.abs(mix[0]))==np.max(np.abs(mix[-1]))==0
score['audit']=dict(score_audit,pcm=pcm_audit,sourceClarity=clarity)
score_bytes=(json.dumps(score,indent=2,ensure_ascii=False)+'\n').encode()
(OUT/'score.json').write_bytes(score_bytes)
assets=[]
for filename,frames,description in [
    ('sunlight-theme.wav',FRAMES,'池畔日光·闲谈：90BPM G大调32小节，有呼吸的轻拨尼龙吉他主题与稳定指弹伴奏'),
    ('sunlight-theme-preview.wav',round(8*4*BEAT*SR),'完整主题前8小节：同一master gain，含弱起、附点、邻音与真实9–8延留'),
]:
    path=OUT/filename;sf.write(path,mix[:frames],SR,subtype='PCM_16')
    info=metrics(path)
    assert info['truePeak4x']<=.14 and info['seamDifference']==0
    assets.append(dict(path=filename,version=5,description=description,source=sorted(source_records),
        processing=dict(composition=score['title'],bpm=BPM,timeSignature='4/4',key='G major',
            bars=32 if frames==FRAMES else 8,form=score['form'] if frames==FRAMES else ['A'],
            score='score.json',scoreSha256=sha(score_bytes),musicalAudit=score_audit,pcmAudit=pcm_audit,
            instruments=['Real CC0 light picked nylon guitar lead Vel02Pick',
                         'Real CC0 soft fingerstyle nylon guitar comp Vel01Finger'],
            piano=False,cello=False,guitarPitchShiftSemitones=0,randomization=False,
            globalMonophonicMelody=True,releaseIncludedInDuration=True,accompanimentHarmonyBoundaryChoke=True,
            compBelowLeadDb=12,room={'addedRoom':False,'addedDelay':False,'addedReverb':False},
            softClipping=False,compression=False,limiter=False,truePeakLimit=.14,
            normalizationTruePeak=.135,masterGain=master_gain,
            loop='Exact32bar loop with explicit natural rests and note gates; zero boundary, no seam patch'),**info))
manifest=dict(version=5,assets=assets,sources=list(source_records.values()),
    score={'path':'score.json','sha256':sha(score_bytes)},audit=dict(score_audit,pcm=pcm_audit,sourceClarity=clarity),
    provenance=dict(sampleBased=True,piano=False,cello=False,
        officialReadme={'url':readme_entry['url'],'gitBlob':readme_entry['sha'],'sha256':sha(readme_raw),
                       'file':readme_path.name,'gitTree':TREE},
        officialLicense={'url':license_entry['url'],'gitBlob':license_entry['sha'],'sha256':sha(license_raw),
                         'file':license_path.name,'gitTree':TREE,'license':'CC0-1.0'},
        scoreScript='tools/audio/compose_sunlight_v5.py',scoreScriptSha256=sha(Path(__file__).read_bytes()),
        auditoryReview='Adopted by user for project use on2026-10-24; technical checks do not constitute Windows hardware verification',
        adoptionRecord='docs/工程/声音/audio-22i-adoption.json'))
(OUT/'music-manifest.json').write_text(json.dumps(manifest,indent=2,ensure_ascii=False)+'\n')
print(json.dumps(dict(assets=[{k:v for k,v in a.items() if k not in ['source','processing']} for a in assets],
    audit={k:v for k,v in score_audit.items() if k!='ornaments'},sourceCount=len(source_records),
    sourceClarity=clarity),indent=2,ensure_ascii=False))
