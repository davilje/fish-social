"""Adopted scene sound design: lake beds and continuous shared wind/leaf weather."""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
WATER_PROFILES = [
    dict(id='calm', label='平静湖面', path='ambience/pond_lake_calm.wav', gain=.60, fadeSeconds=2),
    dict(id='ripples', label='轻晃水面', path='ambience/pond_lake_ripples.wav', gain=.65, fadeSeconds=2),
    dict(id='shore', label='原河岸水声（对照）', path='ambience/pond_shore.wav', gain=.45, fadeSeconds=2),
]
WEATHER = dict(seed=2201030, sampleIntervalSeconds=.05, blockSeconds=30,
    initialWindGain=.18, initialLeavesGain=.30,
    stepMinSeconds=2.4, stepMaxSeconds=4.8, windMinGain=.15, windMaxGain=.49,
    windAdjustmentThreshold=.045, windAdjustment=.08, windAdjustedMinimum=.13,
    firstLullMinSeconds=26, firstLullMaxSeconds=44, lullGain=.015,
    lullHoldMinSeconds=.8, lullHoldMaxSeconds=1.6, lullGapMinSeconds=28, lullGapMaxSeconds=46,
    leavesDelaySeconds=.28, leavesResponseGain=.73, leavesReferenceWindGain=.55,
    leavesResponseExponent=.85, leavesRiseSeconds=.65, leavesFallSeconds=1.35,
    fadeInSeconds=2, fadeOutSeconds=1.2, monitorGain=5,
    waterOffsetSeconds=10, windOffsetSeconds=3.2, leavesOffsetSeconds=19.7)
TRACK_GAINS = dict(wind=.18, leaves=.65)
CALM_PONDS = {'pond-calm', 'pond-mirror', 'pond-stone', 'pond-ridge', 'pond-novice'}


def scene_configuration():
    ponds = json.loads((ROOT/'shared/generated/game-data/ponds.json').read_text())
    return dict(defaultWaterProfile='calm', waterProfiles=WATER_PROFILES,
        scenes=[dict(pondId=pond['pondId'], label=pond['name'],
            waterProfile='calm' if pond['pondId'] in CALM_PONDS else 'ripples') for pond in ponds],
        weather=WEATHER)
