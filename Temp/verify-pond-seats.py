#!/usr/bin/env python3
import json
import re
import shutil
from collections import Counter
from pathlib import Path

prefab_dir = Path(r"C:\Users\Administrator\Projects\fish-social\fish-social-unity\Assets\Desktop\OverlayLayouts")
json_dir = Path(r"C:\Users\Administrator\Projects\fish-social\desktop-overlay\OverlayResources\layouts")
unity_json = Path(r"C:\Users\Administrator\Projects\fish-social\fish-social-unity\FishSocialOverlay\OverlayResources\layouts")

prefabs = sorted(prefab_dir.glob("pond-*.prefab"))
print("Prefabs:", len(prefabs))
bad = []
for p in prefabs:
    t = p.read_text(encoding="utf-8")
    spots = set(re.findall(r"kind: spot\n\s*spotId: (\S+)", t))
    seats = set(re.findall(r"kind: actor-seat\n\s*spotId: (\S+)", t))
    pets = set(re.findall(r"kind: actor-pet\n\s*spotId: (\S+)", t))
    rings = set(re.findall(r"kind: actor-ring\n\s*spotId: (\S+)", t))
    statuses = set(re.findall(r"kind: actor-status\n\s*spotId: (\S+)", t))
    names = set(re.findall(r"kind: actor-name\n\s*spotId: (\S+)", t))
    views = len(re.findall(r"guid: 796169c93c28b8d43bad5812e79dd4a3", t))
    seat_count = len(re.findall(r"kind: actor-seat\n", t))
    ok = (
        spots
        and seats == spots
        and pets == spots
        and rings == spots
        and statuses == spots
        and names == spots
        and seat_count == len(spots)
    )
    if not ok:
        bad.append(
            {
                "pond": p.stem,
                "spots": len(spots),
                "seats": len(seats),
                "seat_docs": seat_count,
                "pets": len(pets),
            }
        )

    jpath = json_dir / f"{p.stem}.json"
    data = json.loads(jpath.read_text(encoding="utf-8"))
    kinds = Counter(o.get("kind") for o in data["objects"])
    spot_n = kinds.get("spot", 0)
    for k in ("actor-seat", "actor-pet", "actor-ring", "actor-status", "actor-name"):
        if kinds.get(k, 0) != spot_n:
            bad.append({"pond": p.stem, "issue": f"json {k}={kinds.get(k)} spot={spot_n}"})

    spot = next(o for o in data["objects"] if o.get("kind") == "spot")
    seat = next(o for o in data["objects"] if o.get("kind") == "actor-seat")
    pet = next(o for o in data["objects"] if o.get("kind") == "actor-pet")
    print(
        f"{p.stem}: prefab_spots={len(spots)} json_spot={spot_n} "
        f"host={spot['w']}x{spot['h']} seat={seat['w']}x{seat['h']} "
        f"sprite={seat.get('sprite')} pet=({pet['x']},{pet['y']}) views={views}"
    )

# Sync Unity FishSocialOverlay copy from desktop-overlay export
unity_json.mkdir(parents=True, exist_ok=True)
copied = 0
for j in json_dir.glob("pond-*.json"):
    dest = unity_json / j.name
    shutil.copy2(j, dest)
    copied += 1

print("---")
print("BAD:", bad if bad else "none")
print(f"Synced {copied} layout JSON -> FishSocialOverlay/OverlayResources/layouts/")
print("desktop layouts:", len(list(json_dir.glob('pond-*.json'))))
