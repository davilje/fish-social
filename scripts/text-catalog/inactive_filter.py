"""Exclude reviewed inactive/non-display sources from the player copy catalog.

The manifest deliberately matches individual sources, not whole text values or
heuristic categories. Line/column shifts do not restore reviewed sources. When a
retired UI is re-enabled, review and remove its corresponding manifest rules.
"""
from collections import Counter
from copy import deepcopy
from datetime import datetime
import hashlib
import json
from pathlib import Path
from zoneinfo import ZoneInfo
from key_registry import legacy_key

RULES_PATH = Path(__file__).with_name('inactive-sources.json')


def source_signature(key, source):
    return (legacy_key(key), source['file'], source['kind'], source['location'],
            hashlib.sha256(source.get('expression', '').encode('utf-8')).hexdigest())


def filter_catalog(catalog):
    raw_rules = RULES_PATH.read_bytes()
    manifest = json.loads(raw_rules)
    rules_hash = hashlib.sha256(raw_rules).hexdigest()
    excluded = {}
    for group in manifest['groups']:
        for entry in group['entries']:
            for source in entry['sources']:
                signature = (entry['key'], source['file'], source['kind'],
                             source['location'], source['expressionSha256'])
                excluded[signature] = group['id']

    result = deepcopy(catalog)
    removed_sources = Counter()
    removed_entries = 0
    partial_keys = 0

    def keep_sources(entry):
        return [s for s in entry['sources']
                if source_signature(entry.get('sourceKey') or entry['key'], s) not in excluded]

    kept = []
    for entry in result['entries']:
        sources = keep_sources(entry)
        for source in entry['sources']:
            reason = excluded.get(source_signature(entry.get('sourceKey') or entry['key'], source))
            if reason:
                removed_sources[reason] += 1
        if not sources:
            removed_entries += 1
            continue
        partial_keys += len(sources) != len(entry['sources'])
        entry['sources'] = sources
        kept.append(entry)
    result['entries'] = kept

    # Do not resurrect excluded text in the retired-edits archive.
    retired = []
    for entry in result.get('retiredEntries', []):
        sources = keep_sources(entry)
        if sources:
            entry['sources'] = sources
            retired.append(entry)
    result['retiredEntries'] = retired

    prior = catalog.get('inactiveTextFilter', {})
    if (not removed_sources and retired == catalog.get('retiredEntries', [])
            and prior.get('rulesSha256') == rules_hash):
        return catalog  # Repeated filter-only runs are byte-for-byte idempotent.

    result['inactiveTextFilter'] = {
        'filteredAt': datetime.now(ZoneInfo('Asia/Shanghai')).isoformat(timespec='seconds'),
        'rulesFile': 'scripts/text-catalog/inactive-sources.json',
        'rulesSha256': rules_hash,
        'reviewDate': manifest['reviewDate'],
        'baseline': manifest['baseline'],
        'removedEntries': removed_entries,
        'removedSources': sum(removed_sources.values()),
        'partiallyRetainedKeys': partial_keys,
        'reasons': dict(removed_sources),
    }
    per_file = Counter(s['file'] for e in kept for s in e['sources'])
    result['files'] = [{**f, 'occurrences': per_file[f['file']]}
                       for f in result['files']]
    result['stats'] = {
        **result['stats'],
        'entries': len(kept),
        'sourceOccurrences': sum(per_file.values()),
        'categories': dict(Counter(e['category'] for e in kept)),
        'excludedInactiveSources': sum(removed_sources.values()),
        'inactiveExclusionReasons': dict(removed_sources),
    }
    return result
