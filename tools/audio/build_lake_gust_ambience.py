"""Build two independent CC0 lake recordings and the adopted seamless outdoor wind bed.

No synthesized noise, stereo fabrication, compression, limiting, music or source
separation. Mono field material stays dual-mono. The quiet lake has a conservative
8x gain ceiling rather than lifting its noise floor to a peak normalization target.
The shared assembler alone installs scene configuration and retires the old wind.
"""
from pathlib import Path
import argparse
import hashlib
import json
import math
import urllib.request
import uuid

import numpy as np
import soundfile as sf
from scipy import signal

ROOT = Path(__file__).resolve().parents[2]
AUDIO = ROOT/'fish-social-unity/Assets/StreamingAssets/Audio'
SR = 44100
SOURCES = [
    dict(key='lake_calm_mick_705034', author='Mick_', title='2023-10-03 lake shore ambience.wav',
        page='https://freesound.org/people/Mick_/sounds/705034/',
        url='https://cdn.freesound.org/previews/705/705034_8356325-hq.mp3', file='source-705034.mp3',
        sha256='b37260bc8581be7210a1b64e5b0fe87086ad1ddcfdddd97e237106891ee8ca2b',
        pageSha256='690d0d8acda19f5f143f449694e29717c46cd32923ff05588ada05118c7b60c9',
        sourceDescription='Very soft waves and some wildlife at a calm lake; Lake Pymatuning, PA,2023-10-03.',
        originalFormat='Mono44100Hz32bit WAV; official HQ MP3 preview used',
        fieldSoundCaveat='Original field source includes wildlife; no claim of perfect isolated water-only stem.'),
    dict(key='lake_ripples_trp_568819', author='TRP', title='200906 Water waves lapping, lake, gentle, close, PEC9am.flac',
        page='https://freesound.org/people/TRP/sounds/568819/',
        url='https://cdn.freesound.org/previews/568/568819_97550-hq.mp3', file='source-568819.mp3',
        sha256='256c55aff55fed78e4ad2c5251f2a5d2ec91ea20b6d729010ee10fc15e1a7a3f',
        pageSha256='1b2d57e5ac02480ebdb3d9e629dfc4c1ddea139b8afd6a220c1ef4780bc3623f',
        sourceDescription='Small lake waves lapping a rocky shore; homemade parabolic with EM172s.',
        originalFormat='Stereo48000Hz24bit FLAC; official HQ MP3 preview used',
        fieldSoundCaveat='Close natural lake-wave recording; soft lowpass controls small splash transients.'),
    dict(key='wind_open_uganda_38976', author='Uganda', title='WIND, Wind through Wheatfield.wav',
        page='https://freesound.org/people/Uganda/sounds/38976/',
        url='https://cdn.freesound.org/previews/38/38976_226484-hq.mp3', file='source-38976.mp3',
        sha256='6490b47fcd0bd6daa477a8eb3362af380b1a933209d6aa33328c13eaa8f6619e',
        pageSha256='3204cfd63c8841a8bcd58f3220fe55f4f47a7b93282dd0696fb9d4f709c77705',
        sourceDescription='Wind through a wheatfield, soft vegetation crackling; Haywards Heath, West Sussex.',
        originalFormat='Stereo48000Hz24bit WAV; official HQ MP3 preview used',
        fieldSoundCaveat='Creator notes a few background birds in full recording; selected gesture avoids stronger high-frequency sections. '
            'Technical spectral checks cannot certify absence of every distant call.'),
]
for source in SOURCES:
    source.update(license='CC0-1.0', licenseUrl='https://creativecommons.org/publicdomain/zero/1.0/',
        licenseEvidence=dict(officialPage=source['page'], inspectedDate='2026-10-30',
            authorPageLicenseLabel='Creative Commons 0', inspectedPageSha256=source.pop('pageSha256')))
RECIPES = [
    dict(id='calm', path='ambience/pond_lake_calm.wav', source=SOURCES[0]['key'], start=110,
        seconds=56, overlap=8, highPass=50, lowPass=3000, targetPeak=.11, maxGain=8, loop=True,
        description='平静湖面现场录音，极细微水纹与安静湖岸；保留原始单声场，双通道封装'),
    dict(id='ripples', path='ambience/pond_lake_ripples.wav', source=SOURCES[1]['key'], start=72,
        seconds=56, overlap=8, highPass=45, lowPass=2000, targetPeak=.11, maxGain=1, loop=True,
        description='独立湖岸轻晃水面；选用较轻的后段，保留小波起伏并柔化近水滴响'),
    dict(id='wind', path='ambience/pond_wind_open_loop.wav', source=SOURCES[2]['key'], start=48,
        seconds=16, overlap=0, highPass=80, lowPass=8000, targetPeak=.11, maxGain=4, loop=False,
        description='已采用的户外风声连续底床；保留原风源，去边缘并2秒互补交叠为12秒循环'),
]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def meta(path):
    target = Path(str(path)+'.meta')
    if target.exists():
        return
    guid = uuid.uuid5(uuid.NAMESPACE_URL, 'fish-social:'+str(path.relative_to(ROOT))).hex
    target.write_text('fileFormatVersion: 2\nguid: '+guid+'\nDefaultImporter:\n  externalObjects: {}\n'
        '  userData: \n  assetBundleName: \n  assetBundleVariant: \n')


def fetch(source, cache):
    path = cache/source['file']
    cache.mkdir(parents=True, exist_ok=True)
    if not path.exists():
        req = urllib.request.Request(source['url'], headers={'User-Agent':'FishSocial-Lake-Gust/1.0'})
        with urllib.request.urlopen(req, timeout=90) as response:
            data = response.read()
        assert hashlib.sha256(data).hexdigest() == source['sha256'], 'Downloaded source hash mismatch'
        path.write_bytes(data)
    assert sha(path) == source['sha256'], 'Cached source hash mismatch'
    return path


def exact_seam(pcm):
    candidates = np.flatnonzero(np.all(pcm[:-1] == pcm[1:], axis=1))+1
    candidates = candidates[(candidates>2)&(candidates<len(pcm)-2)]
    assert len(candidates), 'No natural equal-stereo PCM seam'
    slopes = np.max(np.abs(pcm[candidates+1].astype(int)-pcm[candidates].astype(int)), axis=1) + \
        np.max(np.abs(pcm[candidates-1].astype(int)-pcm[candidates-2].astype(int)), axis=1)
    rotation = int(candidates[np.argmin(slopes)])
    return np.concatenate([pcm[rotation:], pcm[:rotation]]), rotation


def measure(path):
    x, rate = sf.read(path, dtype='float64')
    info = sf.info(path)
    freq, density = signal.welch(x, fs=rate, nperseg=8192, axis=0)
    power = density.sum(axis=1)
    rms = float(np.sqrt(np.mean(x*x)))
    return dict(path=str(path.relative_to(AUDIO)), version=1, frames=len(x), duration=len(x)/rate,
        channels=info.channels, sampleRate=rate, format=info.subtype,
        peak=float(np.max(np.abs(x))), truePeak4x=float(np.max(np.abs(signal.resample_poly(x,4,1,axis=0)))),
        rms=rms, rmsDbfs=float(20*np.log10(rms)),
        seamDifference=float(np.max(np.abs(x[0]-x[-1]))),
        sourceSpatialMode='dual-mono; source mono preserved' if np.array_equal(x[:,0],x[:,1]) else 'original distinct stereo',
        stereoCorrelation=float(np.corrcoef(x.T)[0,1]),
        frequencyEnergyRatios={'below80Hz':float(power[freq<80].sum()/power.sum()),
            'above2500Hz':float(power[freq>=2500].sum()/power.sum()),
            'above6000Hz':float(power[freq>=6000].sum()/power.sum())}, sha256=sha(path))


def render(recipe, source, cache):
    raw, rate = sf.read(fetch(source, cache), always_2d=True, dtype='float64')
    channels = raw.shape[1]
    assert channels in [1,2]
    x = raw[round(recipe['start']*rate):round((recipe['start']+recipe['seconds'])*rate)]
    assert len(x) == round(recipe['seconds']*rate)
    x = signal.sosfiltfilt(signal.butter(2,[recipe['highPass'],recipe['lowPass']],
        btype='bandpass',fs=rate,output='sos'), x, axis=0)
    if rate != SR:
        divisor = math.gcd(rate,SR)
        x = signal.resample_poly(x,SR//divisor,rate//divisor,axis=0)
    if channels == 1:
        x = np.repeat(x,2,axis=1)  # Exact mono compatibility, deliberately no fake spatialization.
    rotation = None
    if recipe['loop']:
        overlap = round(recipe['overlap']*SR)
        weight = np.sin(np.linspace(0,np.pi/2,overlap))**2
        x = np.concatenate([x[overlap:-overlap],x[-overlap:]*(1-weight[:,None])+x[:overlap]*weight[:,None]])
    else:
        fade = round(.75*SR)
        weight = np.sin(np.linspace(0,np.pi/2,fade))**2
        x[:fade] *= weight[:,None]
        x[-fade:] *= weight[::-1,None]
        x[0] = x[-1] = 0
    before_peak = float(np.max(np.abs(signal.resample_poly(x,4,1,axis=0))))
    gain = min(recipe['maxGain'], recipe['targetPeak']/before_peak)
    pcm = np.rint(x*gain*32768).astype(np.int16)
    if recipe['loop']:
        pcm, rotation = exact_seam(pcm)
    if recipe['id'] == 'wind':
        # Byte-equivalent input to the approved preview's windLoopData(): first
        # quantize the original16s gesture, crop1–15s, then overlap2s. Do not
        # renormalize or rotate: preserve the accepted preview's phase/level.
        raw_loop = pcm[SR:15*SR].astype(np.float64)/32768
        overlap = 2*SR
        weight = np.sin(np.linspace(0,np.pi/2,overlap))**2
        loop = np.concatenate([raw_loop[overlap:-overlap],
            raw_loop[-overlap:]*(1-weight[:,None])+raw_loop[:overlap]*weight[:,None]])
        pcm = np.rint(loop.astype(np.float32).astype(np.float64)*32768).astype(np.int16)
    path = AUDIO/recipe['path']
    path.parent.mkdir(parents=True,exist_ok=True)
    sf.write(path,pcm,SR,subtype='PCM_16')
    meta(path)
    item = measure(path)
    assert item['truePeak4x'] <= .12 and item['format'] == 'PCM_16' and item['channels'] == 2
    if recipe['loop']:
        assert 40 <= item['duration'] <= 64 and item['seamDifference'] == 0
    else:
        assert item['duration'] == 12
        natural_steps = np.max(np.abs(np.diff(pcm.astype(np.int32),axis=0)),axis=1)
        assert np.max(np.abs(pcm[0].astype(int)-pcm[-1].astype(int))) <= np.quantile(natural_steps,.99)
    return dict(item, description=recipe['description'], source=[source['key']],
        processing=dict(sourceStartSeconds=recipe['start'],sourceDurationSeconds=recipe['seconds'],
            sourceChannels=channels,sourceSampleRate=rate,resampleTo=SR,
            monoHandling='Exact duplicated channels; no stereo fabrication' if channels==1 else 'Original channel image retained',
            highPassHz=recipe['highPass'],lowPassHz=recipe['lowPass'],
            filter='2nd-order Butterworth forward/backward zero-phase bandpass',
            loop=True,loopOverlapSeconds=recipe['overlap'] if recipe['loop'] else 2,circularRotationFrames=rotation,
            seamMethod='8second complementary sin-squared overlap plus circular rotation at equal PCM stereo samples' if recipe['loop'] else 'Approved preview: original quantized16second gesture, crop1–15s,2second complementary sin-squared overlap; natural adjacent source samples at wrap, no silent dip',
            masterGain=gain,maxAllowedGain=recipe['maxGain'],targetTruePeak4x=recipe['targetPeak'],
            gainCappedToAvoidNoiseLift=bool(gain==recipe['maxGain']),
            normalization='One conservative constant gain; no framewise loudness normalization or dynamics processing',
            compression=False,limiting=False,saturation=False,syntheticNoise=False,syntheticStereo=False,
            music=False,addedVoices=False,addedBirds=False,pitchShiftSemitones=0,timeStretch=False))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--sources',type=Path,default=Path.home()/'.cache/fish-social-lake-gust')
    parser.add_argument('--incoming',type=Path,default=ROOT/'output/pond-scene-audio/incoming-bank.json')
    parser.add_argument('--report',type=Path,default=ROOT/'docs/工程/声音/2026-10-30-lake-gust-assets.json')
    parser.add_argument('--no-assemble',action='store_true',help='Only produce assets/incoming bank; do not invoke shared assembler')
    args = parser.parse_args()
    assets = [render(recipe,source,args.sources) for recipe,source in zip(RECIPES,SOURCES)]
    bank = dict(version=1,assets=assets,sources=SOURCES,
        provenance=dict(script='tools/audio/build_lake_gust_ambience.py',scriptSha256=sha(Path(__file__)),
            reproduction='python tools/audio/build_lake_gust_ambience.py --sources <cache>',
            sourceRecordingsIndependent=True,sourceScope='Three new independent creator files; original shore/leaves retained for comparison',
            humanListening='Not certified by automated signal/source verification'))
    args.incoming.parent.mkdir(parents=True,exist_ok=True)
    args.incoming.write_text(json.dumps(bank,ensure_ascii=False,indent=2)+'\n')
    if not args.no_assemble:
        from ambience_bank import assemble_scene_bank
        assemble_scene_bank(bank)
    report = dict(status='pass',date='2026-10-30',**bank,
        validation=['Pinned official source SHA256/CC0 page evidence',
            'PCM16 44100Hz twochannel compatibility, genuine source spatial image retained',
            'Two independent48second lake loops,8second overlaps, zero endpoint difference',
            'Broad80–8000Hz12second wind loop; same crop/overlap as user-approved preview',
            '4x truepeak at most.12, no compression/limiting/synthesis'],
        caveats=['Quiet lake normalization is capped at8x; target peak may not be reached rather than lift the background floor.',
            'Creator field recordings contain natural environmental detail; source descriptions and technical checks do not prove absence of all wildlife/human/engine sounds.',
            'The two lake files are independent recordings by different creators; the original shore comparison is not overwritten.'])
    args.report.parent.mkdir(parents=True,exist_ok=True)
    args.report.write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
    print(json.dumps([dict(path=a['path'],duration=a['duration'],truePeak4x=a['truePeak4x'],rms=a['rms'],
        sourceSpatialMode=a['sourceSpatialMode'],sha256=a['sha256']) for a in assets],indent=2))


if __name__ == '__main__':
    main()
