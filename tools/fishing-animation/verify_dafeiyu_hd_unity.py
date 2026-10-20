import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import struct
import subprocess
import tempfile


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "fish-social-unity"
PROJECT = Path("/private/tmp/fish-dafeiyu-integration")
UNITY = Path("/Applications/Unity/Hub/Editor/2021.3.29f1c1/Unity.app/Contents/MacOS/Unity")
CHECK = Path(__file__).with_name("DafeiyuHdIntegrationCheck.cs")


def require(condition, message):
    if not condition:
        raise RuntimeError(message)


def plain_tree(directory):
    require(directory.is_dir() and not directory.is_symlink(), f"Expected isolated directory: {directory}")
    for parent, directories, files in os.walk(directory):
        for name in directories + files:
            require(not (Path(parent) / name).is_symlink(), f"Refusing symlink: {Path(parent) / name}")


def copy_file(source, destination):
    destination = Path(destination)
    require(PROJECT in destination.resolve().parents, f"Write outside isolated project: {destination}")
    with tempfile.NamedTemporaryFile(dir=destination.parent, delete=False) as temporary:
        temporary_path = Path(temporary.name)
    try:
        shutil.copy2(source, temporary_path)
        os.replace(temporary_path, destination)
    finally:
        temporary_path.unlink(missing_ok=True)
    return str(destination)


def png_size(file):
    with file.open("rb") as source:
        header = source.read(24)
    require(len(header) == 24 and header[:8] == b"\x89PNG\r\n\x1a\n" and header[12:16] == b"IHDR", f"Invalid PNG: {file}")
    return list(struct.unpack(">II", header[16:24]))


def installed_hd_manifest():
    fishing = SOURCE / "Assets/StreamingAssets/Fishing"
    manifest_file = fishing / "dafeiyu/manifest.json"
    manifest = json.loads(manifest_file.read_text())
    require(manifest.get("bodyTextureScale") == 2, "HD resources are not installed; do not run the Unity check yet")
    require(manifest["canvas"] == [640, 512], "Logical canvas changed")
    require(len(manifest["clips"]) == 16 and sum(len(clip["frames"]) for clip in manifest["clips"].values()) == 74, "Expected 16 clips / 74 slots")
    for clip, definition in manifest["clips"].items():
        for index in range(len(definition["frames"])):
            for layer in manifest["layers"]:
                body = layer in manifest["bodyLayers"]
                folder = fishing / "dafeiyu/body" if body else fishing / "shared"
                file = folder / clip / str(index) / (layer + ".png")
                require(png_size(file) == ([1280, 1024] if body else [640, 512]), f"Wrong source dimensions: {file}")
    portrait = SOURCE / "Assets/Resources/Desktop/Theme/Cozy/characters/avatars/dafeiyu.png"
    require(png_size(portrait) == [512, 512], "HD portrait is not installed")
    return hashlib.sha256(manifest_file.read_bytes()).hexdigest()


def prepare(run):
    require(PROJECT.resolve() == PROJECT and PROJECT != SOURCE.resolve(), "Unexpected integration project path")
    require((PROJECT / "ProjectSettings/ProjectVersion.txt").read_bytes() == (SOURCE / "ProjectSettings/ProjectVersion.txt").read_bytes(), "Project Unity versions differ")
    plain_tree(PROJECT / "Assets")
    require(not (PROJECT / "Temp/UnityLockfile").exists(), "The isolated Unity project is already open")
    manifest_hash = installed_hd_manifest() if run else None
    folders = ["Scripts", "Tests"] + (["Resources", "StreamingAssets/Fishing"] if run else [])
    for folder in folders:
        source = SOURCE / "Assets" / folder
        destination = PROJECT / "Assets" / folder
        plain_tree(source)
        shutil.copytree(source, destination, dirs_exist_ok=True, copy_function=copy_file)
    editor = PROJECT / "Assets/Editor"
    editor.mkdir(exist_ok=True)
    copy_file(CHECK, editor / CHECK.name)
    if run:
        copied = PROJECT / "Assets/StreamingAssets/Fishing/dafeiyu/manifest.json"
        require(hashlib.sha256(copied.read_bytes()).hexdigest() == manifest_hash, "Installed manifest changed during snapshot")
    return manifest_hash


def main():
    parser = argparse.ArgumentParser(description="Prepare the isolated HD check; launch Unity only with --run after installation is confirmed.")
    parser.add_argument("--run", action="store_true", help="Snapshot installed HD resources and run isolated Unity (wait for installation confirmation first)")
    arguments = parser.parse_args()
    manifest_hash = prepare(arguments.run)
    if not arguments.run:
        print(json.dumps({"result": "prepared", "project": str(PROJECT), "unityStarted": False, "resourceSnapshot": False}))
        return
    require(UNITY.is_file(), "Unity executable is unavailable")
    report_file = PROJECT / "hd-result.json"
    report_file.unlink(missing_ok=True)
    subprocess.run([str(UNITY), "-batchmode", "-nographics", "-projectPath", str(PROJECT),
                    "-executeMethod", "DafeiyuHdIntegrationCheck.Run", "-logFile", str(PROJECT / "hd-check.log")], check=True)
    report = json.loads(report_file.read_text())
    require(report.get("result") == "passed" and report.get("manifestSha256") == manifest_hash, "Missing or stale HD result")
    print(json.dumps(report, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
