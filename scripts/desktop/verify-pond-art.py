"""Check saved Unity pond art against the exported desktop resources."""
from collections import Counter
from pathlib import Path
import hashlib
import json
import re
import struct

ROOT = Path(__file__).resolve().parents[2]
ASSETS = ROOT / 'fish-social-unity/Assets'
ART = ASSETS / 'Desktop/OverlayArt/Ponds'
RESOURCES = ROOT / 'desktop-overlay/OverlayResources'


def read(path):
    return json.loads(path.read_text())


def verify():
    catalog = read(ART / 'catalog.json')
    ponds = read(ASSETS / 'Resources/GameData/ponds.json')
    world_map = read(ASSETS / 'Resources/Desktop/WorldMapPonds.json')['ponds']
    tags = read(ASSETS / 'Resources/GameData/pond_spot_tags.json')
    if isinstance(tags, dict):
        tags = tags.get('items', tags.get('tags', []))
    profiles = {p['theme']: p for p in catalog['profiles']}
    bindings = {p['pondId']: p for p in catalog['bindings']}
    assert len(world_map) == 20
    assert len(bindings) == len(ponds) == 21
    assert set(bindings) == {p['pondId'] for p in ponds}
    assert set(profiles) == {'pond', 'lake', 'reservoir', 'river', 'coastal', 'estuary', 'wilderness'}
    assert len(catalog['seats']) == 4
    seat_specs = {s['name']: s for s in catalog['seats']}
    pet_place = read(RESOURCES / 'fishing/orange/manifest.json')['place']
    for manifest in (RESOURCES / 'fishing').glob('*/manifest.json'):
        place = read(manifest)['place']
        unity_place = read(ASSETS / 'StreamingAssets/Fishing' / manifest.parent.name / 'manifest.json')['place']
        assert place == unity_place, ('Animation placement differs between clients', manifest)
        assert place['referenceFoot'] == pet_place['referenceFoot'], ('Skin seat contact differs', manifest)
    pngs = list(ART.rglob('*.png'))
    for path in pngs:
        data = path.read_bytes()
        assert data[:8] == b'\x89PNG\r\n\x1a\n', path
        assert data[25] == 6, ('PNG must preserve RGBA', path)
        width, height = struct.unpack('>II', data[16:24])
        assert max(width, height) <= 4096
        assert path.with_name(path.name + '.meta').exists(), ('Missing import settings', path)
    total_spots = 0
    for pond in ponds:
        binding = bindings[pond['pondId']]
        theme = binding['theme']
        profile = profiles[theme]
        assert theme == ('wilderness' if pond['pondId'] == 'pond-ridge' else pond['waterType'])
        document = read(RESOURCES / 'layouts' / (pond['pondId'] + '.json'))
        assert document['canvas']['width'] == document['pond']['width'] == 1680
        assert document['canvas']['height'] == document['pond']['height'] == 560
        objects = document['objects']
        backgrounds = [o for o in objects if o['id'] == 'pond-bg']
        assert len(backgrounds) == 1
        assert backgrounds[0]['sprite'] == 'ponds/' + profile['image']
        if profile.get('layerLayout'):
            template = read(ART / profile['layerLayout'])
            assert read(RESOURCES / 'ponds' / profile['layerLayout']) == template
            assert template['width'] == 1680 and template['height'] == 560
            assert template['groundY'] == profile['shoreY'] - 8
            scene_objects = {o['id']: o for o in objects if o['kind'] == 'sprite'}
            assert set(scene_objects) == {o['id'] for o in template['layers']}, ('Stale or missing layer', pond['pondId'])
            for expected in template['layers']:
                actual = scene_objects[expected['id']]
                assert actual['anchor'] == 'top-left'
                assert actual['sprite'] == 'ponds/' + expected['image']
                assert actual['z'] == expected['z']
                for key in ['x', 'y', 'w', 'h']:
                    assert abs(actual[key] - expected[key]) <= 0.002, (pond['pondId'], expected['id'], key)
        spots = [o for o in objects if o['kind'] == 'spot']
        expected_spots = {row['spotId'] for row in tags if row['pondId'] == pond['pondId']}
        assert len(spots) == len(expected_spots)
        assert {o['spotId'] for o in spots} == expected_spots
        seats = [o for o in objects if o['kind'] == 'actor-seat']
        assert len(seats) == len(spots)
        spec = seat_specs[binding['seat']]
        pets = {o['spotId']: o for o in objects if o['kind'] == 'actor-pet'}
        for seat in seats:
            assert seat['sprite'] == 'seats/' + binding['seat'] + '.png'
            assert abs(seat['y'] + seat['h'] * spec.get('groundY', 1) - (profile['shoreY'] - 8)) <= 1.5
            assert 0 <= seat['x'] < seat['x'] + seat['w'] <= 1681
            if spec.get('supportY', 0) > 0:
                pet = pets[seat['spotId']]
                uniform = min(pet['w'], pet['h']) / pet_place['reference']
                foot_x = (pet['w'] - uniform * pet_place['reference']) / 2 + uniform * pet_place['referenceFoot'][0]
                foot_y = (pet['h'] - uniform * pet_place['reference']) / 2 + uniform * pet_place['referenceFoot'][1]
                # Preserve the shared contact point through fractional layout export.
                assert abs(seat['x'] + seat['w'] * spec['supportX'] - (pet['x'] + foot_x)) <= 0.01
                assert abs(seat['y'] + seat['h'] * spec['supportY'] - (pet['y'] + foot_y)) <= 0.01
        for obj in objects:
            sprite = obj.get('sprite')
            assert sprite != 'null'
            if sprite:
                assert (RESOURCES / sprite).is_file(), (pond['pondId'], sprite)
        total_spots += len(spots)
    for path in pngs:
        relative = path.relative_to(ART)
        target = RESOURCES / ('seats' if relative.parts[0] == 'seats' else 'ponds') / (relative.name if relative.parts[0] == 'seats' else relative)
        assert target.is_file(), target
        assert hashlib.sha256(path.read_bytes()).digest() == hashlib.sha256(target.read_bytes()).digest(), target
    assert (RESOURCES / 'ponds/_default.png').read_bytes() == (ART / 'maps/map-lake.png').read_bytes()
    assert (RESOURCES / 'seats/_default.png').read_bytes() == (ART / 'seats' / (catalog['defaultSeat'] + '.png')).read_bytes()
    # Sprite GUIDs in all saved pond and module prefabs must resolve to real image assets.
    image_guids = set()
    for meta in ASSETS.rglob('*.png.meta'):
        match = re.search(r'^guid: (\w+)', meta.read_text(), re.M)
        if match and meta.with_suffix('').is_file():
            image_guids.add(match[1])
    prefabs = list((ASSETS / 'Desktop/OverlayLayouts').glob('pond-*.prefab')) + list((ART / 'Prefabs').glob('*.prefab'))
    for path in prefabs:
        for guid in re.findall(r'm_Sprite: \{fileID: \d+, guid: (\w+)', path.read_text()):
            assert guid in image_guids, (path, guid)
    return {'status': 'PASS', 'worldMapPonds': len(world_map), 'tutorialPonds': 1, 'layouts': len(bindings),
            'spotsPreserved': total_spots, 'backgrounds': len(profiles), 'modules': len(catalog['modules']),
            'seatVariants': len(catalog['seats']), 'artPNGs': len(pngs),
            'themeAssignments': dict(Counter(b['theme'] for b in bindings.values())),
            'seatAssignments': dict(Counter(b['seat'] for b in bindings.values()))}


if __name__ == '__main__':
    print(json.dumps(verify(), ensure_ascii=False, indent=2))
