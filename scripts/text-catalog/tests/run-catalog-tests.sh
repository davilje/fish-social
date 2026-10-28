#!/usr/bin/env bash
set -euo pipefail
repo_root="$(cd "$(dirname "$0")/../../.." && pwd)"
test_dir="$(mktemp -d "${TMPDIR:-/tmp}/fish-localization-tests.XXXXXX")"
trap 'rm -rf "$test_dir"' EXIT
unity_mono="${UNITY_MONO_ROOT:-/Applications/Unity/Hub/Editor/2021.3.29f1c1/Unity.app/Contents/MonoBleedingEdge}"
"$unity_mono/bin/mono" "$unity_mono/lib/mono/4.5/csc.exe" -nologo -out:"$test_dir/LocalizationCatalogTests.exe" \
  "$repo_root/fish-social-unity/Assets/Scripts/Desktop/Localization/LocalizationCatalog.cs" \
  "$repo_root/scripts/text-catalog/tests/LocalizationCatalogTests.cs"
"$unity_mono/bin/mono" "$test_dir/LocalizationCatalogTests.exe"
"$unity_mono/bin/mono" "$unity_mono/lib/mono/4.5/csc.exe" -nologo -r:System.Runtime.Serialization.dll \
  -out:"$test_dir/OverlayCatalogLoaderTests.exe" \
  "$repo_root/fish-social-unity/Assets/Scripts/Desktop/Localization/LocalizationCatalog.cs" \
  "$repo_root/desktop-overlay/OverlayLocalizationCatalogLoader.cs" \
  "$repo_root/scripts/text-catalog/tests/OverlayCatalogLoaderTests.cs"
"$unity_mono/bin/mono" "$test_dir/OverlayCatalogLoaderTests.exe"
