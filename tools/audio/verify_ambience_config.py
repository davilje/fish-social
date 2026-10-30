"""Shared read-only validation of the lake/scene and intermittent wind configuration."""
from pond_scene_config import scene_configuration, TRACK_GAINS


def verify_config(bank):
    assert bank['version'] == 4
    assert len(bank['loops']) == 3 and {t['id'] for t in bank['loops']} == {'water', 'wind', 'leaves'}
    expected = scene_configuration()
    for key, value in expected.items():
        assert bank[key] == value, ('Scene sound configuration changed', key)
    paths = {item['path'] for item in bank['assets']}
    used = {item['path'] for item in bank['loops'] + bank['waterProfiles']}
    assert len(paths) == len(bank['assets']) == 5 and paths == used
    assert not any(item['path'] in ('ambience/pond_wind.wav', 'ambience/pond_wind_open.wav') for item in bank['assets'])
    for track in bank['loops'] + bank['waterProfiles']:
        assert 0 < track['gain'] <= 1 and track['fadeSeconds'] == 2
    for track in bank['loops']:
        if track['id'] in TRACK_GAINS:
            assert track['gain'] == TRACK_GAINS[track['id']]
    assert bank['loops'][0]['path'] == bank['waterProfiles'][0]['path']
    assert len({scene['pondId'] for scene in bank['scenes']}) == len(bank['scenes'])
    assert {scene['waterProfile'] for scene in bank['scenes']} == {'calm', 'ripples'}
    w = bank['weather']
    assert 'gusts' not in bank
    assert 0 < w['sampleIntervalSeconds'] <= .1 < w['blockSeconds']
    assert 0 < w['windMinGain'] < w['windMaxGain'] < 1
    assert 0 < w['leavesDelaySeconds'] < w['stepMinSeconds']
    assert w['leavesFallSeconds'] > w['leavesRiseSeconds']
    assert w['lullGain'] < w['windMinGain']
    assert bank['loops'][1]['path'] == 'ambience/pond_wind_open_loop.wav'
    return paths
