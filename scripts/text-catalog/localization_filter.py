"""Keep reviewed player-localizable copy, excluding individual non-display uses.

Most rules ignore source line/column shifts. When the same literal is both a
control identifier and a displayed label in one method, rules also check the
reviewed line contents and argument column, so a display use cannot be removed
by an identifier rule. Changed statements must be reviewed again.
"""
from collections import Counter
from copy import deepcopy
from datetime import datetime
import hashlib
import json
from pathlib import Path
import re
from zoneinfo import ZoneInfo

from inactive_filter import source_signature
from key_registry import legacy_key

RULES_PATH = Path(__file__).with_name('localization-sources.json')
ROOT = Path(__file__).resolve().parents[2]


def reviewed_category_origins():
    return json.loads(RULES_PATH.read_bytes()).get('categoryOrigins', {})


def localization_signature(key, source):
    signature = list(source_signature(key, source))
    if source['kind'] == 'Unity序列化' and 'GameObject=' in signature[3]:
        signature[3] = re.sub(r'document\[\d+\]', 'document[*]', signature[3])
    return tuple(signature)


def line_context(source, root=ROOT):
    lines = (root / source['file']).read_text(encoding='utf-8-sig').splitlines()
    index = source['line'] - 1
    if not 0 <= index < len(lines):
        return None
    return hashlib.sha256(lines[index].strip().encode('utf-8')).hexdigest()


def filter_localization(catalog):
    raw = RULES_PATH.read_bytes()
    rules_hash = hashlib.sha256(raw).hexdigest()
    manifest = json.loads(raw)
    rules = {}
    for rule in manifest['excludedSources']:
        signature = (rule['key'], rule['file'], rule['kind'],
                     rule['location'], rule['expressionSha256'])
        rules.setdefault(signature, []).append(rule)

    def exclusion(key, source, category=None):
        if '/Editor/' in source['file']:
            return 'Unity编辑器来源不进入玩家本地化表（含界面烘焙脚本）；实际显示文本保留运行时与Prefab来源。'
        reviewed_category = manifest['categoryOverrides'].get(legacy_key(key), category)
        if reviewed_category in {'编辑器与界面构建', '日志与诊断'}:
            return 'Unity编辑器属性、日志及开发诊断不进入玩家本地化表；已确认的玩家提示按审查分类保留。'
        for rule in rules.get(localization_signature(key, source), []):
            if 'lineContextSha256' in rule:
                if (source['column'] != rule['column']
                        or line_context(source) != rule['lineContextSha256']):
                    continue
            return rule['reason']
        return None

    result = deepcopy(catalog)
    removed = Counter()
    removed_entries = partial = recategorized = 0
    kept = []
    for entry in result['entries']:
        sources = []
        for source in entry['sources']:
            reason = exclusion(entry.get('sourceKey') or entry['key'], source, entry['category'])
            if reason:
                removed[reason] += 1
            else:
                sources.append(source)
        if not sources:
            removed_entries += 1
            continue
        partial += len(sources) != len(entry['sources'])
        entry['sources'] = sources
        category = manifest['categoryOverrides'].get(legacy_key(entry.get('sourceKey') or entry['key']), entry['category'])
        recategorized += category != entry['category']
        entry['category'] = category
        kept.append(entry)
    result['entries'] = kept
    retired = []
    for entry in result.get('retiredEntries', []):
        entry['sources'] = [s for s in entry['sources']
                            if not exclusion(entry.get('sourceKey') or entry['key'], s, entry['category'])]
        if entry['sources']:
            retired.append(entry)
    result['retiredEntries'] = retired
    if (not removed and not recategorized
            and retired == catalog.get('retiredEntries', [])
            and catalog.get('localizationFilter', {}).get('rulesSha256') == rules_hash):
        return catalog
    result['localizationFilter'] = {
        'filteredAt': datetime.now(ZoneInfo('Asia/Shanghai')).isoformat(timespec='seconds'),
        'rulesFile': 'scripts/text-catalog/localization-sources.json',
        'rulesSha256': rules_hash,
        'reviewDate': manifest['reviewDate'],
        'baseline': manifest['baseline'],
        'removedEntries': removed_entries,
        'removedSources': sum(removed.values()),
        'partiallyRetainedKeys': partial,
        'recategorizedEntries': recategorized,
        'reasons': dict(removed),
        'scope': '需要翻译给玩家的正式界面、通知、操作反馈与游戏显示数据；逐来源审查。',
    }
    counts = Counter(s['file'] for e in kept for s in e['sources'])
    result['files'] = [{**f, 'occurrences': counts[f['file']]} for f in result['files']]
    result['stats'] = {
        **result['stats'], 'entries': len(kept),
        'sourceOccurrences': sum(counts.values()),
        'categories': dict(Counter(e['category'] for e in kept)),
        'excludedNonLocalizableSources': sum(removed.values()),
    }
    return result
