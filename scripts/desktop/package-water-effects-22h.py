#!/usr/bin/env python3
"""Publish a reviewable Windows bundle; this does not run or install the app."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import tempfile
import zipfile

ROOT = Path(__file__).resolve().parents[2]


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dotnet', default='dotnet')
    parser.add_argument('--output', default='dist/overlay-water-effects-22h-2026-10-24.zip')
    args = parser.parse_args()
    output = ROOT / args.output
    output.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='fish-water-22h-package-') as temporary:
        bundle = Path(temporary) / 'FishSocialOverlay'
        subprocess.run([
            args.dotnet, 'publish',
            'desktop-overlay/tests/FishSocialOverlay.PerformanceTests.csproj',
            '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true',
            '-p:EnableWindowsTargeting=true', '-o', str(bundle),
        ], cwd=ROOT, check=True)
        (bundle / 'Run-Native-Tests.cmd').write_bytes((
            '@echo off\r\ncd /d "%~dp0"\r\n'
            'FishSocialOverlay.PerformanceTests.exe > native-tests.log 2>&1\r\n'
            'set "TEST_EXIT=%ERRORLEVEL%"\r\ntype native-tests.log\r\n'
            'echo Native test exit code: %TEST_EXIT%\r\n'
            'echo Log: %CD%\\native-tests.log\r\npause\r\nexit /b %TEST_EXIT%\r\n'
        ).encode('ascii'))
        (bundle / 'README-22H.txt').write_text(
            'STEAM-DESKTOP-22H review bundle, 2026-10-24\n'
            'Run-Native-Tests.cmd runs the Windows native regression suite.\n'
            'The macOS publisher cannot execute WPF or validate desktop click-through.\n'
            'This bundle does not replace the installed game or deploy the server.\n'
            'The paired Unity/server protocol changes are required for live H events.\n'
            'Visual rules: ripples below waterline; splash droplets above allowed; actual contact only.\n'
            'Source report: docs/engineering/2026-10-24-water-effects-22h-v4.md\n'
            'Windows A/B, 10-minute stability and H/I audio capture remain separate acceptance.\n',
            encoding='utf-8')
        source_paths = list((ROOT / 'desktop-overlay').glob('*.cs'))
        source_paths += list((ROOT / 'desktop-overlay').glob('*.xaml'))
        source_paths += list((ROOT / 'desktop-overlay/tests').glob('*.cs'))
        source_paths += list((ROOT / 'desktop-overlay').glob('*.csproj'))
        source_paths += list((ROOT / 'desktop-overlay/tests').glob('*.csproj'))
        source_paths += list((ROOT / 'desktop-overlay/OverlayResources/effects/fishing/v4').glob('*'))
        source_paths += list((ROOT / 'desktop-overlay/OverlayResources/fishing').glob('*markers.json'))
        source_paths += list((ROOT / 'fish-social-unity/Assets/Scripts/Desktop').glob('*Presentation*.cs'))
        source_paths += list((ROOT / 'fish-social-unity/Assets/Scripts/Desktop/Pet').glob('*Water*.cs'))
        source_paths += [ROOT / name for name in [
            'fish-social-unity/Assets/Scripts/Desktop/DesktopAppBootstrap.cs',
            'fish-social-unity/Assets/Scripts/Desktop/NativeOverlayProcessController.cs',
            'fish-social-unity/Assets/Scripts/Desktop/NativeOverlayStateDto.cs',
            'fish-social-unity/Assets/Scripts/Desktop/Pet/SpriteFramePetRenderer.cs',
            'fish-social-unity/Assets/Scripts/Desktop/UI/DesktopFishingEffectsSettingsView.cs',
            'server/src/fishingPresentation.ts', 'server/src/fishingStateMachine.ts',
            'shared/types.ts',
            'desktop-overlay/OverlayResources/fishing/water-contact.json',
            'scripts/art/export-water-source-markers.py',
            'scripts/art/build-water-effects-22h-v4.cjs',
            'docs/art/water-effects-22h/preview.js',
        ]]
        info = {
            'task': 'STEAM-DESKTOP-22H', 'date': '2026-10-24',
            'artVersion': '22h-water-v4-approved-style',
            'behaviorRevision': '22h-contact-rules-2026-10-24',
            'baseCommit': '4ecc32482ff13a5529a25ba541e8b917ad18cc27',
            'packagedHead': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip(),
            'workingTreeChangesIncluded': True, 'runtime': 'win-x64 self-contained netcoreapp3.1',
            'windowsNativeExecution': 'NOT RUN: published on macOS',
            'windowsPerformanceAndAudioCapture': 'NOT RUN',
            'sourceFiles': {str(path.relative_to(ROOT)): sha256(path) for path in sorted(source_paths) if path.is_file()},
            'binaries': {path.name: sha256(path) for path in sorted(bundle.glob('FishSocialOverlay.*')) if path.is_file()},
        }
        (bundle / 'BUILD-INFO.json').write_text(json.dumps(info, indent=2) + '\n', encoding='utf-8')
        with zipfile.ZipFile(output, 'w', zipfile.ZIP_DEFLATED, compresslevel=5) as archive:
            for path in sorted(bundle.rglob('*')):
                if path.is_file():
                    archive.write(path, str(path.relative_to(bundle.parent)))
    print(json.dumps({'path': str(output), 'bytes': output.stat().st_size, 'sha256': sha256(output)}, indent=2))


if __name__ == '__main__':
    main()
