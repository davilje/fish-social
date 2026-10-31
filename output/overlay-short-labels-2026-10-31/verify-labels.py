import hashlib
import io
import json
import subprocess
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path

root = Path('/Users/davilje/fish-social')
output = root / 'output/overlay-short-labels-2026-10-31'
labels = json.loads((output / 'labels.json').read_text())
rows = json.loads((output / 'label-rows.json').read_text())
ns = {'m': 'http://schemas.openxmlformats.org/spreadsheetml/2006/main'}

def cells(data):
    with zipfile.ZipFile(io.BytesIO(data)) as archive:
        strings = [''.join(x.itertext()) for x in ET.fromstring(archive.read('xl/sharedStrings.xml')).findall('m:si', ns)] if 'xl/sharedStrings.xml' in archive.namelist() else []
        result = {}
        for name in archive.namelist():
            if not (name.startswith('xl/worksheets/sheet') and name.endswith('.xml')):
                continue
            sheet = {}
            for cell in ET.fromstring(archive.read(name)).findall('m:sheetData/m:row/m:c', ns):
                value = cell.find('m:v', ns)
                text = value.text if value is not None and value.text else ''
                if cell.get('t') == 's':
                    text = strings[int(text)]
                elif cell.get('t') == 'inlineStr':
                    text = ''.join(cell.find('m:is', ns).itertext())
                formula = cell.find('m:f', ns)
                if text or formula is not None:
                    sheet[cell.get('r')] = (text, formula.text if formula is not None else None)
            result[name] = sheet
        return result

baseline = cells(subprocess.check_output(['git', 'show', 'HEAD:unity-overlay文本表.xlsx'], cwd=root))
actual = cells((root / 'unity-overlay文本表.xlsx').read_bytes())
for key, row in rows.items():
    baseline['xl/worksheets/sheet1.xml'][f'E{row}'] = (labels[key][0], None)
    baseline['xl/worksheets/sheet1.xml'][f'F{row}'] = (labels[key][1], None)
assert baseline == actual, 'Unexpected workbook cell changes'
source = json.loads((root / 'unity-overlay文本表.json').read_text())
entries = {entry['key']: entry for entry in source['entries']}
for key, (cn, en) in labels.items():
    assert entries[key]['zhCN'] == cn and entries[key]['enUS'] == en
paths = [
    'desktop-overlay/OverlayResources/Localization/text-catalog.json',
    'fish-social-unity/Assets/Resources/Localization/text-catalog.json',
    'fish-social-unity/FishSocialOverlay/OverlayResources/Localization/text-catalog.json',
]
hashes = {path: hashlib.sha256((root / path).read_bytes()).hexdigest() for path in paths}
assert len(set(hashes.values())) == 1
catalog = json.loads((root / paths[0]).read_text())
catalog_entries = {entry['key']: entry for entry in catalog['entries']}
for key, (cn, en) in labels.items():
    assert catalog_entries[key]['zhCN'] == cn and catalog_entries[key]['enUS'] == en
result = {'passed': True, 'labelEntries': len(labels), 'otherWorkbookCellsPreserved': True, 'catalogHashes': hashes}
(output / 'localization-check.json').write_text(json.dumps(result, ensure_ascii=False, indent=2))
print(json.dumps(result, ensure_ascii=False))
