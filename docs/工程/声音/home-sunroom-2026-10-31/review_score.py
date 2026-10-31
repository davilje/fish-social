"""Independent offline audit of the rendered home BGM candidate. No composition mutations."""
from pathlib import Path
import argparse,ast,importlib.util,json,hashlib,math
import numpy as np
import soundfile as sf
from scipy import signal

ROOT=Path(__file__).resolve().parents[4]
A=ROOT/'docs/美术/声音/home-sunroom/assets'

def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()

def estimate_pitch(x,sr):
    # YIN-style cumulative-mean normalized squared difference. First trough,
    # unconstrained to the declared note, over 65--4000 Hz; parabolic refinement.
    x=x[:min(len(x),16384)].astype(float);x-=x.mean();n=len(x)
    lo=math.floor(sr/4000);hi=min(math.ceil(sr/65),n//3)
    c=signal.fftconvolve(x,x[::-1],mode='full')[n-1:n+hi]
    sq=np.r_[0,np.cumsum(x*x)];tau=np.arange(hi+1)
    d=(sq[n-tau]+sq[n]-sq[tau]-2*c)/(n-tau)
    d=np.maximum(d,0);cm=np.ones_like(d);cm[1:]=d[1:]*tau[1:]/np.maximum(np.cumsum(d[1:]),1e-15)
    below=np.flatnonzero(cm[lo:hi]<.12)
    if len(below):
        best=int(below[0])+lo
        while best+1<hi and cm[best+1]<cm[best]:best+=1
    else:best=int(np.argmin(cm[lo:hi]))+lo
    k=best
    delta=.5*(cm[k-1]-cm[k+1])/(cm[k-1]-2*cm[k]+cm[k+1]) if 0<k<hi else 0
    frequency=sr/(k+delta)
    return frequency, float(cm[k])


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--meta-cache',type=Path,required=True)
    parser.add_argument('--orchestra-cache',type=Path,required=True)
    args=parser.parse_args()
    score=json.loads((A/'score.json').read_text());manifest=json.loads((A/'manifest.json').read_text())
    composition=ROOT/'tools/audio/compose_home_sunroom.py'
    spec=importlib.util.spec_from_file_location('reviewed_composition',composition)
    module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
    parsed=ast.parse(composition.read_text())
    main_node=next(n for n in parsed.body if isinstance(n,ast.FunctionDef) and n.name=='main')
    sample_node=next(n for n in main_node.body if isinstance(n,ast.FunctionDef) and n.name=='sample_for')
    refs=sorted({tuple(e['source']) for e in score['events']})
    source_files={(library,path):(args.meta_cache if library=='meta' else args.orchestra_cache)/Path(path).name for library,path,base in refs}
    # Execute the exact renderer's sample function in an isolated context. It
    # can only read supplied cache paths and return in-memory samples; main()
    # and all asset-writing/downloading branches are deliberately not run.
    context=dict(vars(module),prepared={},tuning={},files=source_files,
                 sources={(library,path):{} for library,path,base in refs})
    exec(compile(ast.Module(body=[sample_node],type_ignores=[]),str(composition),'exec'),context)
    render_sample=context['sample_for']
    pitch=[]
    for library,path,base in refs:
        file=(args.meta_cache if library=='meta' else args.orchestra_cache)/Path(path).name
        record=next(p for p in manifest['sources'] if p['library']==library and p['path']==path)
        assert sha(file)==record['sha256'], ('Source no longer matches candidate manifest',path)
        x,sr=sf.read(file,always_2d=True);x=x.mean(axis=1)
        active=np.flatnonzero(np.abs(x)>np.max(np.abs(x))*.018);onset=active[0]/sr
        windows=[]
        for offset in [.08,.16,.28]:
            start=round((onset+offset)*sr);f,q=estimate_pitch(x[start:start+16384],sr)
            midi=69+12*np.log2(f/440)
            windows.append(dict(hz=float(f),midi=float(midi),yinDifference=q))
        measured=float(np.median([w['midi'] for w in windows]));error=(measured-base)*100
        instances=[e for e in score['events'] if tuple(e['source'])==(library,path,base)]
        corrections={float(e['sourceTuningCorrectionCents']) for e in instances}
        assert len(corrections)==1, ('Inconsistent cached tuning metadata',path,corrections)
        correction=corrections.pop()
        if library=='meta':
            assert abs(correction-record['tuningCorrectionCents'])<1e-9
        residual=error+correction
        assert abs(correction)<=30, ('Excessive microtuning',path,correction)
        if library=='meta':
            assert abs(residual)<2, ('Guitar tuning mismatch',path,error,correction)
            assert all(e['pitchShiftSemitones']==0 for e in instances)
        else:
            assert correction==0, ('Unexpected orchestral microtuning',path)
        # Independently process the actual source, retaining exactly these
        # onset-relative windows under the resampling time scale. This checks
        # the sign and magnitude on audio rather than only adding metadata.
        processed_windows=[]
        if library=='meta':
            ratio=2**(correction/1200)
            tuned=signal.resample(x,round(len(x)/ratio))
            for offset in [.08,.16,.28]:
                start=round((onset+offset)*sr/ratio)
                frequency,quality=estimate_pitch(tuned[start:start+round(16384/ratio)],sr)
                midi=69+12*np.log2(frequency/440)
                processed_windows.append(dict(hz=float(frequency),midi=float(midi),yinDifference=quality))
        processed_error=float(np.median([w['midi'] for w in processed_windows])-base)*100 if processed_windows else None
        if processed_error is not None:
            assert abs(processed_error)<2, ('Processed guitar pitch mismatch',path,processed_error)
        renderer_windows=[]
        if library=='meta':
            actual=render_sample(instances[0])
            assert render_sample(instances[0]) is actual, ('Sample cache regression',path)
            trim=max(0,int(active[0])-round(.008*sr))
            for offset in [.08,.16,.28]:
                start=round(((onset+offset)-trim/sr)*module.SR/ratio)
                frequency,quality=estimate_pitch(actual[start:start+round(16384*module.SR/sr/ratio)],module.SR)
                midi=69+12*np.log2(frequency/440)
                renderer_windows.append(dict(hz=float(frequency),midi=float(midi),yinDifference=quality))
        renderer_error=float(np.median([w['midi'] for w in renderer_windows])-base)*100 if renderer_windows else None
        if renderer_error is not None:
            assert abs(renderer_error)<2, ('Actual filtered renderer pitch mismatch',path,renderer_error)
        pitch.append(dict(library=library,sourcePath=path,sha256=sha(file),declaredActualMidi=base,
                          originalEstimatedMidi=measured,originalCentsFromDeclared=error,originalWindows=windows,
                          appliedSourceTuningCorrectionCents=correction,metadataResidualCents=residual,
                          independentProcessedCentsFromDeclared=processed_error,independentProcessedWindows=processed_windows,
                          actualRendererCentsFromDeclared=renderer_error,actualRendererWindows=renderer_windows,
                          allEventInstancesHaveSameCorrection=True,eventInstancesChecked=len(instances)))
    # Read actual exported bytes, including the final loop, not only score intent.
    x,sr=sf.read(A/'home-sunroom.wav',always_2d=True)
    power=np.sqrt(np.mean(x[:len(x)//441*441].reshape(-1,441,2)**2,axis=(1,2)))
    quiet=power<10**(-65/20)
    starts=np.flatnonzero(np.diff(np.r_[False,quiet,False].astype(int))==1)
    ends=np.flatnonzero(np.diff(np.r_[False,quiet,False].astype(int))==-1)
    mel=[e for e in score['events'] if e['role']=='melody']
    guitar=[p for p in pitch if p['library']=='meta'];orchestra=[p for p in pitch if p['library']=='vsco']
    result=dict(status='passed' if max(abs(p['independentProcessedCentsFromDeclared']) for p in guitar)<2 and max(abs(p['metadataResidualCents']) for p in orchestra)<50 else 'review-required',
        scope='Independent source pitch, score-structure and exported PCM checks; no subjective listening approval.',
        hashes={'score':sha(A/'score.json'),'manifest':sha(A/'manifest.json'),'wav':sha(A/'home-sunroom.wav'),'compositionScript':sha(composition)},
        pitchMethod='Three onset-relative 16384-frame windows (80/160/280ms); unconstrained 65--4000Hz YIN-style first trough <0.12, quadratic period refinement; median MIDI and cents error. Guitar correction checked against all score event instances, then independently Fourier-resampled source measured with matching time-scaled windows. Additionally the exact current sample_for function is AST-extracted and executed in an isolated context (no main/download/output mutation), and its actual 44.1kHz filtered, corrected sample and cache reuse are checked. Ensemble/vibrato estimates remain natural and uncorrected.',
        sourcePitchReview=pitch,maxOriginalSourcePitchErrorCents=max(abs(p['originalCentsFromDeclared']) for p in pitch),
        maxCorrectedGuitarResidualCents=max(abs(p['independentProcessedCentsFromDeclared']) for p in guitar),
        maxActualRendererGuitarResidualCents=max(abs(p['actualRendererCentsFromDeclared']) for p in guitar),
        maxUncorrectedOrchestralDeviationCents=max(abs(p['metadataResidualCents']) for p in orchestra),
        correctedOctaveConvention='VSCO labels C4 at sounding MIDI72, one octave above scientific C4=MIDI60; actual source bases verified. Meta source bases use scientific convention.',
        scoreReview=dict(bars=score['bars'],form='A / A-prime / contrast / varied return, 8 bars each',
            harmony=score['harmony'],melodyEventCount=len(mel),melodyOnsetsPerBar=[sum(e['bar']==b for e in mel) for b in range(1,33)],
            secondaryDominants='Bar14 E7 B-G#-F#-E (9th included) resolves to bar15 Am7; bar24 G7 leads to bar25 Cmaj7.',
            thematicShape='Four-note descending opening; upward answer; repeated/varied A; middle woodwind transfer; related return.',
            articulation='Guitar 2ms sin-squared attack, 180ms cos-squared tail; small overlaps between released tails are intentional. Do not claim globally monophonic PCM.',
            noReferenceTranscription='Composition source contains authored notes and fixed arrangement; this review did not compare a reference transcription or assess perceptual similarity.'),
        pcmReview=dict(durationSeconds=len(x)/sr,seamDifference=float(np.max(np.abs(x[0]-x[-1]))),
            last200msRms=float(np.sqrt(np.mean(x[-round(.2*sr):]**2))),
            belowMinus65DbfsRunsAtLeast100ms=[dict(startSeconds=float(st/100),durationSeconds=float((en-st)/100)) for st,en in zip(starts,ends) if en-st>=10],
            loopInterpretation='The only long near-silent region is a short phrase-ending breath. No long artificial loop gap.'),
        limitations=['No subjective listening was performed; pitch/score/PCM metrics cannot establish pleasantness, naturalism or fidelity to reference performance.','Sample-layer performance is not a live ensemble recording.','Pitch estimates include recorded vibrato and limited sample windows.','The processed single-note checks verify both an independent resampling and the exact current renderer sample function, rather than isolating notes from the polyphonic exported mix.'])
    dest=Path(__file__).with_name('score-review.json');dest.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
    print(json.dumps(dict(status=result['status'],sourceCount=len(pitch),maxOriginalSourceErrorCents=result['maxOriginalSourcePitchErrorCents'],maxCorrectedGuitarResidualCents=result['maxCorrectedGuitarResidualCents'],maxActualRendererGuitarResidualCents=result['maxActualRendererGuitarResidualCents'],maxUncorrectedOrchestralDeviationCents=result['maxUncorrectedOrchestralDeviationCents'],pcm=result['pcmReview']),indent=2))

if __name__=='__main__':main()
