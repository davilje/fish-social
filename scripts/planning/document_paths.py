"""Resolve document references after the 2026-10-26 topic migration."""
from __future__ import annotations

import json
import re
from functools import lru_cache
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


@lru_cache(maxsize=1)
def path_map() -> dict[str, str]:
    return json.loads((ROOT / 'docs/维护/path-map.json').read_text(encoding='utf-8'))


def resolve_document_path(relative: str) -> Path:
    normalized = relative.replace('\\', '/')
    return ROOT / path_map().get(normalized, normalized)


@lru_cache(maxsize=1)
def reference_pattern() -> re.Pattern:
    return re.compile('|'.join(re.escape(k) for k in sorted(path_map(), key=len, reverse=True)))


def rewrite_document_paths(value: str) -> str:
    # Longest first; replacement values are not scanned a second time.
    if 'docs/' not in value:
        return value
    mapping = path_map()
    return reference_pattern().sub(lambda match: mapping[match.group(0)], value)


def find_active_spec(filename: str) -> Path | None:
    matches = [p for p in (ROOT / 'docs/策划/规格').rglob('*.md') if p.name == filename]
    if len(matches) > 1:
        raise ValueError(f'Ambiguous spec name: {filename}')
    return matches[0] if matches else None
