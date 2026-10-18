import csv
import hashlib
import json
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[2]
RESOURCES = ROOT / "fish-social-unity/Assets/Resources"
OUTPUT = ROOT / "docs/art/fish-catalog"


def main():
    species = json.loads((ROOT / "shared/generated/game-data/fish_species.json").read_text())
    unity_species = json.loads((RESOURCES / "GameData/fish_species.json").read_text())
    entries = json.loads((RESOURCES / "Desktop/Theme/Cozy/items/fish/catalog.json").read_text())["items"]
    errors = []
    if species != unity_species:
        errors.append("Unity and shared fish species tables differ")
    expected = {entry["speciesId"]: entry["name"] for entry in species}
    if set(expected) != {entry["speciesId"] for entry in entries} or len(entries) != len(expected):
        errors.append("Catalog is not a one-to-one match for the species table")
    rows = []
    identities = set()
    for entry in entries:
        path = RESOURCES / (entry["resourcePath"] + ".png")
        row = dict(entry)
        row["assetPath"] = str(path.relative_to(ROOT))
        if entry["displayName"] != expected.get(entry["speciesId"]):
            errors.append("Name mismatch: " + entry["speciesId"])
        if not path.is_file():
            errors.append("Missing image: " + entry["speciesId"])
            rows.append(row)
            continue
        digest = hashlib.sha256(path.read_bytes()).hexdigest()
        identity = (digest, entry["spriteName"])
        if identity in identities:
            errors.append("Duplicate illustration: " + entry["speciesId"])
        identities.add(identity)
        with Image.open(path) as image:
            row["width"], row["height"] = image.size
            row["sha256"] = digest
            alpha = image.getchannel("A") if "A" in image.getbands() else None
            row["hasTransparency"] = alpha is not None and alpha.getextrema()[0] == 0
            if not row["hasTransparency"]:
                errors.append("Missing transparent background: " + entry["speciesId"])
        if entry["spriteName"]:
            meta = Path(str(path) + ".meta").read_text()
            if "name: " + entry["spriteName"] + "\n" not in meta:
                errors.append("Missing atlas slice: " + entry["speciesId"])
        rows.append(row)
    OUTPUT.mkdir(parents=True, exist_ok=True)
    report = {"status": "PASS" if not errors else "FAIL", "speciesCount": len(species),
              "distinctIllustrations": len(identities), "errors": errors, "items": rows}
    (OUTPUT / "validation.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n")
    with (OUTPUT / "fish-art-mapping.csv").open("w", encoding="utf-8-sig", newline="") as target:
        writer = csv.DictWriter(target, fieldnames=["speciesId", "displayName", "assetPath", "spriteName"])
        writer.writeheader()
        writer.writerows({key: row.get(key, "") for key in writer.fieldnames} for row in rows)
    print(json.dumps({key: value for key, value in report.items() if key != "items"}, ensure_ascii=False))
    return 1 if errors else 0


if __name__ == "__main__":
    raise SystemExit(main())
