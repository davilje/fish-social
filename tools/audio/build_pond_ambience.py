"""Build a reproducible natural shore ambience from one pinned CC0 stereo field recording.

Requires numpy/scipy/soundfile. Downloads only a public Freesound HQ preview on cache
miss; refuses different source bytes. No oscillators, noise synthesis, music, added
birds, pitch shifting, compression, saturation, or layered effects. Does not touch
legacy audio manifests, adoption records or cue configuration.
"""
from pathlib import Path
import argparse
import hashlib
import json
import uuid
import urllib.request

import numpy as np
import soundfile as sf
from scipy import signal
from ambience_bank import merge_bank

ROOT = Path(__file__).resolve().parents[2]
AUDIO = ROOT / 'fish-social-unity/Assets/StreamingAssets/Audio'
SOURCE = dict(key='hudson_shore_ceich93_318064', title='Water_Lapping_River.wav',
    author='ceich93', page='https://freesound.org/people/ceich93/sounds/318064/',
    url='https://cdn.freesound.org/previews/318/318064_4457609-hq.mp3',
    file='shore-318064.mp3',
    sha256='633f65aa915de7c83aca9d6100cb6a497000f4e39c36b6bef9cdefed06612313',
    license='CC0-1.0', licenseUrl='https://creativecommons.org/publicdomain/zero/1.0/',
    licenseEvidence=dict(officialPage='https://freesound.org/people/ceich93/sounds/318064/',
        inspectedDate='2026-10-30', authorPageLicenseLabel='Creative Commons 0',
        inspectedPageSha256='af00105b8876ec1890366d904542d1a68e55264bbde811302f487394165012fe'),
    sourceDescription='Hudson River water lapping a pebbly shore at night; Tascam DR-05 with Rode windscreen.',
    downloadFormat='Official public high-quality MP3 preview; decoded44100Hz stereo. Original upload WAV44100Hz16bit stereo.')
SR = 44100
TRIM_START = 1.5
INPUT_SECONDS = 56
OVERLAP_SECONDS = 8
HIGH_PASS = 80
LOW_PASS = 2600
TARGET_TRUE_PEAK = .26


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def ensure_meta(path, folder=False):
    """StreamingAssets uses DefaultImporter even for WAV; preserve any existing GUID."""
    meta = Path(str(path)+'.meta')
    if meta.exists():
        return
    guid = uuid.uuid5(uuid.NAMESPACE_URL, 'fish-social:'+str(path.relative_to(ROOT))).hex
    text = 'fileFormatVersion: 2\nguid: '+guid+'\n'
    if folder:
        text += 'folderAsset: yes\n'
    text += 'DefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
    meta.write_text(text)


def fetch_source(cache):
    path = cache/SOURCE['file']
    cache.mkdir(parents=True, exist_ok=True)
    if not path.exists():
        req = urllib.request.Request(SOURCE['url'], headers={'User-Agent':'FishSocial-Natural-Shore/1.0'})
        with urllib.request.urlopen(req, timeout=90) as response:
            raw = response.read()
        assert hashlib.sha256(raw).hexdigest() == SOURCE['sha256'], 'Downloaded source hash mismatch'
        path.write_bytes(raw)
    assert sha(path) == SOURCE['sha256'], 'Cached source hash mismatch'
    return path


def metrics(path):
    x, rate = sf.read(path, dtype='float64')
    info = sf.info(path)
    peak = float(np.max(np.abs(x)))
    rms = float(np.sqrt(np.mean(x*x)))
    first_diff = np.diff(x, axis=0)
    half_second = round(.5*rate)
    windows = x[:len(x)//half_second*half_second].reshape(-1, half_second, 2)
    window_rms = np.sqrt(np.mean(windows*windows, axis=(1, 2)))
    freq, psd = signal.welch(x, fs=rate, nperseg=8192, axis=0)
    psd = psd.sum(axis=1)
    return dict(path='ambience/pond_shore.wav', version=1, duration=len(x)/rate,
        frames=len(x), channels=info.channels, sampleRate=rate, format=info.subtype,
        peak=peak, truePeak4x=float(np.max(np.abs(signal.resample_poly(x, 4, 1, axis=0)))),
        rms=rms, rmsDbfs=float(20*np.log10(rms)), crestDb=float(20*np.log10(peak/rms)),
        seamDifference=float(np.max(np.abs(x[0]-x[-1]))),
        seamAdjacentStep=float(max(np.max(np.abs(x[1]-x[0])), np.max(np.abs(x[-1]-x[-2])))),
        naturalInteriorStep99Percentile=float(np.quantile(np.max(np.abs(first_diff), axis=1), .99)),
        halfSecondRmsPercentiles={str(p):float(np.percentile(window_rms,p)) for p in [5, 50, 95, 100]},
        lowFrequencyEnergyBelow80Hz=float(psd[freq<80].sum()/psd.sum()),
        highFrequencyEnergyAbove6000Hz=float(psd[freq>6000].sum()/psd.sum()),
        stereoCorrelation=float(np.corrcoef(x.T)[0, 1]), sha256=sha(path))


def build(cache, report_path):
    source_path = fetch_source(cache)
    raw, rate = sf.read(source_path, dtype='float64')
    assert rate == SR and raw.ndim == 2 and raw.shape[1] == 2
    left = round(TRIM_START*SR)
    excerpt = raw[left:left+round(INPUT_SECONDS*SR)]
    assert len(excerpt) == round(INPUT_SECONDS*SR)
    # Zero-phase gentle filtering removes handling/wind rumble and close-mic brittle fizz;
    # both original channels pass the same filter, preserving the recorded stereo image.
    sos = signal.butter(2, [HIGH_PASS, LOW_PASS], btype='bandpass', fs=SR, output='sos')
    x = signal.sosfiltfilt(sos, excerpt, axis=0)
    overlap = round(OVERLAP_SECONDS*SR)
    weight = np.sin(np.linspace(0, np.pi/2, overlap))**2
    transition = x[-overlap:]*(1-weight[:,None]) + x[:overlap]*weight[:,None]
    loop = np.concatenate([x[overlap:-overlap], transition])
    assert len(loop) == round((INPUT_SECONDS-OVERLAP_SECONDS)*SR)
    peak = float(np.max(np.abs(signal.resample_poly(loop, 4, 1, axis=0))))
    master_gain = TARGET_TRUE_PEAK/peak
    loop *= master_gain
    # Choose an existing stereo quantization-equal adjacent pair for the file boundary.
    # This is a circular rotation only: no seam patch, forced-zero sample or silent fade.
    pcm = np.rint(loop*32768).astype(np.int16)
    identical = np.all(pcm[:-1] == pcm[1:], axis=1)
    candidates = np.flatnonzero(identical)+1
    candidates = candidates[(candidates>2)&(candidates<len(pcm)-2)]
    assert len(candidates), 'No exact natural stereo PCM seam found'
    near_steps = np.max(np.abs(pcm[candidates+1].astype(int)-pcm[candidates].astype(int)), axis=1) + \
        np.max(np.abs(pcm[candidates-1].astype(int)-pcm[candidates-2].astype(int)), axis=1)
    rotation = int(candidates[np.argmin(near_steps)])
    pcm = np.concatenate([pcm[rotation:], pcm[:rotation]])
    assert np.all(pcm[0] == pcm[-1])
    folder = AUDIO/'ambience'
    folder.mkdir(parents=True, exist_ok=True)
    path = folder/'pond_shore.wav'
    sf.write(path, pcm, SR, subtype='PCM_16')
    output = metrics(path)
    assert output['channels'] == 2 and output['format'] == 'PCM_16'
    assert 48 <= output['duration'] <= 90 and output['seamDifference'] == 0
    assert output['truePeak4x'] <= .28 and .003 < output['rms'] < .07
    processing = dict(sourceStartSeconds=TRIM_START, sourceDurationSeconds=INPUT_SECONDS,
        highPassHz=HIGH_PASS, lowPassHz=LOW_PASS, filter='2nd-order Butterworth bandpass forward/backward (zero phase)',
        loopOverlapSeconds=OVERLAP_SECONDS, loopMethod='8second complementary sin-squared tail-to-head overlap; circular rotation only',
        seamMethod='Select existing adjacent stereo PCM16 vectors with exactly equal values; no sample rewrite or silence',
        circularRotationFrames=rotation, equalStereoSeamCandidates=len(candidates),
        normalization='One constant linear gain on the entire48second loop', masterGain=master_gain,
        targetTruePeak4x=TARGET_TRUE_PEAK, compression=False, limiting=False, saturation=False,
        syntheticNoise=False, music=False, addedVoices=False, addedBirds=False,
        pitchShiftSemitones=0, timeStretch=False, layeredRecordings=1)
    manifest = dict(version=1, runtime=dict(path='ambience/pond_shore.wav', gain=.65, fadeSeconds=2.0),
        sources=[SOURCE], assets=[dict(output,
            description='柔和河岸近水实录底床；保持自然轻拍与原始立体声，单条连续水景，无音乐或合成噪声',
            source=[SOURCE['key']], processing=processing)],
        provenance=dict(script='tools/audio/build_pond_ambience.py', scriptSha256=sha(Path(__file__)),
            reproduction='python tools/audio/build_pond_ambience.py --sources <cache>',
            scope='Independent ambience only; main manifest/cue config/music/effect adoption hashes unchanged'))
    manifest_path = AUDIO/'ambience-manifest.json'
    merge_bank(manifest, ['water'])
    ensure_meta(folder, folder=True)
    ensure_meta(path)
    ensure_meta(manifest_path)
    report = dict(status='pass', date='2026-10-30', asset=output, processing=processing,
        sourceLicense='CC0 verified on original creator Freesound page', sourceSha256=SOURCE['sha256'],
        validation=['Pinned download SHA256', 'True stereo source; stereo PCM16 44100Hz output',
            '48second loop with8second natural overlap', 'Zero PCM16 stereo endpoint difference without zeroing/fading to silence',
            '4x polyphase truepeak below.28', 'No music/synthesized noise/added voices/birds',
            'One constant gain; no compressor or limiter'],
        inspectionScope='Creator source description/license, full-track spectral and local transient analysis. '
            'These checks do not establish human headphone/speaker approval or exclude every background field sound.',
        levelNote='Natural transient crest retained: RMS is below the aspirational.05–.07 target; '
            'raising to that level would violate.28 peak without materially compressing the recording.',
        runtime=manifest['runtime'], actualWindowsListening='Not performed by asset builder')
    report_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2)+'\n')
    print(json.dumps(dict(asset=output, runtime=manifest['runtime'], report=str(report_path)), ensure_ascii=False, indent=2))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--sources', type=Path, default=Path.home()/'.cache/fish-social-pond-ambience')
    parser.add_argument('--report', type=Path,
        default=ROOT/'docs/工程/声音/2026-10-30-pond-ambience-assets.json')
    args = parser.parse_args()
    build(args.sources, args.report)


if __name__ == '__main__':
    main()
