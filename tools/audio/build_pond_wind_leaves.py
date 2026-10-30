"""Rebuild the retained CC0 leaf recording; the legacy filename is kept for reproduction.

The rejected continuous/window-like wind is retired. New lake beds and outdoor gusts
are built by build_lake_gust_ambience.py. This command only refreshes the leaf asset.

Requires numpy scipy soundfile. Each natural field recording is downloaded from its
creator's official public Freesound preview and pinned by SHA256. Uses only excerpt,
identical stereo filtering, resampling, long overlap and one constant gain. No noise
synthesis, music, added birds, compressors, limiters or time stretching. Writes only
pond_leaves WAV+meta and its independent report, merging only owned leaf track ID
into the shared runtime ambience bank through ambience_bank.merge_bank.
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
TARGET_PEAK = .115
SOURCES = [
    dict(key='leaves_ecfike_183496', title='Wind and Leaves.wav', author='ecfike',
        page='https://freesound.org/people/ecfike/sounds/183496/',
        url='https://cdn.freesound.org/previews/183/183496_2337290-hq.mp3',
        file='source-183496.mp3',
        sha256='a804e75f0c57acd77488a63b8452d8de8593a7f8ed1c0a5b546d0b41d770b8fa',
        sourceDescription='Wind in a small clump of broad-leaf trees; original stereo44100Hz16bit WAV field recording. '
            'The leaves track retains this recording\'s own light breeze; it is not an artificially isolated dry stem.',
        license='CC0-1.0', licenseUrl='https://creativecommons.org/publicdomain/zero/1.0/',
        licenseEvidence=dict(officialPage='https://freesound.org/people/ecfike/sounds/183496/',
            inspectedDate='2026-10-30', authorPageLicenseLabel='Creative Commons 0',
            inspectedPageSha256='21569182632d445e8f958e497c8f26f83580b9b9fb38df2316cafeb0464caabd'),
        downloadFormat='Official public HQ MP3 preview; decoded44100Hz stereo'),
]
RECIPES = [
    dict(id='leaves', path='ambience/pond_leaves.wav', source=SOURCES[0]['key'], start=128, seconds=56,
        overlap=8, highPass=450, lowPass=6500, gain=.65,
        description='近景树叶细碎沙沙；削减低频风底，保留较完整叶片摩擦细节；独立现场录音'),
]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def ensure_meta(path):
    meta = Path(str(path)+'.meta')
    if meta.exists():
        return
    guid = uuid.uuid5(uuid.NAMESPACE_URL, 'fish-social:'+str(path.relative_to(ROOT))).hex
    meta.write_text('fileFormatVersion: 2\nguid: '+guid+'\nDefaultImporter:\n'
        '  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')


def fetch_source(source, cache):
    path = cache/source['file']
    cache.mkdir(parents=True, exist_ok=True)
    if not path.exists():
        req = urllib.request.Request(source['url'], headers={'User-Agent':'FishSocial-Natural-Wind-Leaves/1.0'})
        with urllib.request.urlopen(req, timeout=90) as response:
            raw = response.read()
        assert hashlib.sha256(raw).hexdigest() == source['sha256'], 'Downloaded source hash mismatch'
        path.write_bytes(raw)
    assert sha(path) == source['sha256'], 'Cached source hash mismatch: '+source['key']
    return path


def rotate_at_natural_stereo_seam(pcm):
    # Select actual equal adjacent stereo vectors, so no endpoint sample needs repair.
    candidates = np.flatnonzero(np.all(pcm[:-1] == pcm[1:], axis=1))+1
    candidates = candidates[(candidates>2)&(candidates<len(pcm)-2)]
    assert len(candidates), 'No exact natural stereo PCM seam available'
    steps = np.max(np.abs(pcm[candidates+1].astype(int)-pcm[candidates].astype(int)), axis=1) + \
        np.max(np.abs(pcm[candidates-1].astype(int)-pcm[candidates-2].astype(int)), axis=1)
    rotation = int(candidates[np.argmin(steps)])
    return np.concatenate([pcm[rotation:], pcm[:rotation]]), rotation, len(candidates)


def metrics(path):
    x, rate = sf.read(path, dtype='float64')
    info = sf.info(path)
    peak = float(np.max(np.abs(x)))
    rms = float(np.sqrt(np.mean(x*x)))
    window = round(.5*rate)
    windows = x[:len(x)//window*window].reshape(-1, window, 2)
    wrms = np.sqrt(np.mean(windows*windows, axis=(1, 2)))
    freq, psd = signal.welch(x, fs=rate, nperseg=8192, axis=0)
    power = psd.sum(axis=1)
    return dict(path=str(path.relative_to(AUDIO)), version=2, duration=len(x)/rate,
        frames=len(x), channels=info.channels, sampleRate=rate, format=info.subtype,
        peak=peak, truePeak4x=float(np.max(np.abs(signal.resample_poly(x, 4, 1, axis=0)))),
        rms=rms, rmsDbfs=float(20*np.log10(rms)), crestDb=float(20*np.log10(peak/rms)),
        seamDifference=float(np.max(np.abs(x[0]-x[-1]))),
        seamAdjacentStep=float(max(np.max(np.abs(x[1]-x[0])), np.max(np.abs(x[-1]-x[-2])))),
        halfSecondRmsPercentiles={str(p):float(np.percentile(wrms,p)) for p in [5, 50, 95, 100]},
        lowFrequencyEnergyBelow80Hz=float(power[freq<80].sum()/power.sum()),
        highFrequencyEnergyAbove6000Hz=float(power[freq>6000].sum()/power.sum()),
        stereoCorrelation=float(np.corrcoef(x.T)[0, 1]), sha256=sha(path))


def build_track(recipe, source, cache):
    raw, rate = sf.read(fetch_source(source, cache), dtype='float64')
    assert raw.ndim == 2 and raw.shape[1] == 2, 'Expected original twochannel source'
    identical_source_channels = bool(np.array_equal(raw[:,0], raw[:,1]))
    left = round(recipe['start']*rate)
    x = raw[left:left+round(recipe['seconds']*rate)]
    assert len(x) == round(recipe['seconds']*rate)
    source_rms = float(np.sqrt(np.mean(x*x)))
    source_peak = float(np.max(np.abs(x)))
    sos = signal.butter(2, [recipe['highPass'], recipe['lowPass']], btype='bandpass', fs=rate, output='sos')
    x = signal.sosfiltfilt(sos, x, axis=0)
    if rate != SR:
        divisor = math.gcd(rate, SR)
        x = signal.resample_poly(x, SR//divisor, rate//divisor, axis=0)
    overlap = round(recipe['overlap']*SR)
    weight = np.sin(np.linspace(0, np.pi/2, overlap))**2
    blend = x[-overlap:]*(1-weight[:,None]) + x[:overlap]*weight[:,None]
    loop = np.concatenate([x[overlap:-overlap], blend])
    expected_frames = round((recipe['seconds']-recipe['overlap'])*SR)
    assert len(loop) == expected_frames
    peak = float(np.max(np.abs(signal.resample_poly(loop, 4, 1, axis=0))))
    master_gain = TARGET_PEAK/peak
    pcm = np.rint(loop*master_gain*32768).astype(np.int16)
    pcm, rotation, candidate_count = rotate_at_natural_stereo_seam(pcm)
    assert np.array_equal(pcm[0], pcm[-1])
    path = AUDIO/recipe['path']
    path.parent.mkdir(parents=True, exist_ok=True)
    sf.write(path, pcm, SR, subtype='PCM_16')
    ensure_meta(path)
    output = metrics(path)
    assert output['channels'] == 2 and output['format'] == 'PCM_16' and output['sampleRate'] == SR
    assert 40 <= output['duration'] <= 64 and output['seamDifference'] == 0
    assert output['truePeak4x'] <= .12
    processing = dict(sourceStartSeconds=recipe['start'], sourceDurationSeconds=recipe['seconds'],
        sourceExcerptPeak=source_peak, sourceExcerptRms=source_rms,
        sourceChannels=2, sourceSampleRate=rate, resampleTo=SR,
        sourceSpatialMode='dual-mono (source channels identical)' if identical_source_channels else 'original distinct stereo channels',
        sourceChannelsIdentical=identical_source_channels, syntheticStereo=False,
        highPassHz=recipe['highPass'], lowPassHz=recipe['lowPass'],
        filter='2nd-order Butterworth bandpass forward/backward, zero phase, identical for both channels',
        overlapSeconds=recipe['overlap'], loopMethod='Complementary sin-squared tail-to-head overlap',
        seamMethod='Circular rotation at existing equal adjacent stereo PCM16 vectors; no sample rewrite or silent dip',
        circularRotationFrames=rotation, equalStereoSeamCandidates=candidate_count,
        normalization='One constant linear gain per complete loop', masterGain=master_gain,
        targetTruePeak4x=TARGET_PEAK, compression=False, limiting=False, saturation=False,
        syntheticNoise=False, music=False, addedVoices=False, addedBirds=False,
        pitchShiftSemitones=0, timeStretch=False, layeredRecordings=1)
    return dict(output, description=recipe['description'], source=[source['key']], processing=processing)


def build(cache, report_path):
    assets = [build_track(recipe, source, cache) for recipe, source in zip(RECIPES, SOURCES)]
    runtime = dict(tracks=[dict(id=recipe['id'], path=recipe['path'], gain=recipe['gain'], fadeSeconds=2.0,
        defaultMix=1.0) for recipe in RECIPES])
    provenance = dict(script='tools/audio/build_pond_wind_leaves.py', scriptSha256=sha(Path(__file__)),
        reproduction='python tools/audio/build_pond_wind_leaves.py --sources <cache>',
        independentRecordedSources=True,
        scope='Only wind/leaves WAVs and report; merge only owned leaves metadata into runtime bank, preserving water.')
    manifest = dict(version=1, runtime=runtime, sources=SOURCES, assets=assets, provenance=provenance)
    from ambience_bank import merge_bank
    merge_bank(manifest, ['leaves'])
    report = dict(status='pass', date='2026-10-30', assets=assets, sources=SOURCES,
        runtime=runtime, provenance=provenance,
        validation=['Pinned official CC0 natural leaf recording; original stereo preserved',
            'Stereo PCM16 44100Hz,48seconds', '8second natural overlap, exact zero stereo endpoint difference',
            '4x truepeak below.12', 'Only constant gain/filter/resample/overlap; no compressors or generated noise'],
        sourceNote='Leaves recording has distinct stereo channels and naturally includes its own light breeze; '
            'not a perfectly isolated leaf-only stem. Runtime uses intermittent excerpts, not a continuous loop.',
        inspectionScope='Creator descriptions and licensing plus full-track spectral/transient measurements; '
            'technical checks do not constitute human listening approval or prove absence of every background field sound.',
        actualWindowsListening='Not performed by asset builder')
    report_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2)+'\n')
    print(json.dumps(dict(assets=[{k:v for k,v in a.items() if k not in ['source','processing']} for a in assets],
        runtime=runtime, report=str(report_path)), ensure_ascii=False, indent=2))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--sources', type=Path, default=Path.home()/'.cache/fish-social-pond-wind-leaves')
    parser.add_argument('--report', type=Path,
        default=ROOT/'docs/工程/声音/2026-10-30-retained-leaves-assets.json')
    args = parser.parse_args()
    build(args.sources, args.report)


if __name__ == '__main__':
    main()
