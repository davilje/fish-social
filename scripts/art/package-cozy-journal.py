"""Package the prepared resources with original assets, sources, previews and rebuild scripts."""
from pathlib import Path
import json
import sys
import zipfile

root = Path(__file__).resolve().parents[2]
output = Path(sys.argv[1]).resolve()
output.parent.mkdir(parents=True, exist_ok=True)
folders = [
    'docs/美术/手账',
    'docs/设计/界面/原型/手账',
    'fish-social-unity/Assets/Resources/Desktop/Theme/Cozy',
    'fish-social-unity/Assets/Desktop/JournalSamples',
    'fish-social-unity/Assets/Shaders',
]
files = set()
for folder in folders:
    files.update(p for p in (root / folder).rglob('*') if p.is_file() and p.name != '.DS_Store' and '-draft.' not in p.name)
    meta = root / (folder + '.meta')
    if meta.exists():
        files.add(meta)
files.update((root / 'scripts/art').glob('*cozy-journal*'))
files.update((root / 'docs/设计/界面').glob('2026-10-21-cozy-journal-*'))
for name in ['CozyJournalResourcePreparation.cs', 'CozyPrefabRepair.cs']:
    file = root / 'fish-social-unity/Assets/Scripts/Desktop/Editor' / name
    files.add(file)
    if Path(str(file) + '.meta').exists():
        files.add(Path(str(file) + '.meta'))
with zipfile.ZipFile(output, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=4) as archive:
    for file in sorted(files):
        archive.write(file, file.relative_to(root))
    archive.writestr('START-HERE.txt', '请先阅读 docs/美术/手账/README.md。\n解压后在根目录启动 python3 -m http.server 8769，打开 docs/美术/手账/compare.html 查看原版与Unity对照。\n此包包含资源与参考样板，不是整个可独立运行的Unity项目；50项UI切图已按原版校正，补充素材仍需视觉验收，正式游戏业务未接入。\n')
with zipfile.ZipFile(output) as archive:
    bad = archive.testzip()
    if bad:
        raise RuntimeError('Archive CRC failure: ' + bad)
report = {'file': str(output), 'files': len(files) + 1, 'bytes': output.stat().st_size, 'crc': 'PASS'}
Path(str(output) + '.json').write_text(json.dumps(report, ensure_ascii=False, indent=2))
print(json.dumps(report, ensure_ascii=False))
