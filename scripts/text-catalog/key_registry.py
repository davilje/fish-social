"""Stable public localization keys with legacy source-identity compatibility.

Approved names live in key-registry.json. Source fingerprints remain internal
to extraction/review rules and never decide an approved key's spelling. New,
unreviewed sources receive a valid provisional name until explicitly named.
"""
from copy import deepcopy
from functools import lru_cache
import json
from pathlib import Path
import re

REGISTRY_PATH = Path(__file__).with_name('key-registry.json')
KEY_PATTERN = re.compile(r'^[a-z][a-z0-9_]*(?:\.[a-z][a-z0-9_]*){2,6}$')
KEY_POLICY = ('全小写英文点分层、段内snake_case：ui|game|server.模块.页面或实体.用途。'
              '正式Key由key-registry.json登记，独立于中英文措辞与行号；旧Key仅作迁移别名。'
              '新增未命名来源使用unreviewed命名空间，审查后登记正式Key。')
MODULES = {
    '通用界面': 'common', '鱼塘与地图': 'map', '新手引导': 'onboarding',
    '图鉴与成就': 'codex', '商店与装备': 'shop', '个人中心': 'profile',
    '家园与桌宠': 'home', '设置': 'settings', '登录与账号': 'auth',
    '好友与社区': 'social', '排行榜': 'leaderboard', '鱼获与结算': 'catch',
    '钓鱼与悬浮窗': 'fishing',
}


@lru_cache(maxsize=1)
def registry():
    data = json.loads(REGISTRY_PATH.read_text(encoding='utf-8'))
    names = data['legacyToSemantic']
    if len(names.values()) != len(set(names.values())):
        raise ValueError('语义Key注册表有重复名称')
    for old, new in names.items():
        if not re.fullmatch(r'txt_[0-9a-f]{16}', old) or not KEY_PATTERN.fullmatch(new):
            raise ValueError(f'无效Key映射: {old} → {new}')
    return data


def legacy_key(key):
    return reverse_registry().get(key, key)


@lru_cache(maxsize=1)
def reverse_registry():
    return {new: old for old, new in registry()['legacyToSemantic'].items()}


def restore_source_keys(catalog):
    """Use original source identities while extracting and applying filters."""
    result = deepcopy(catalog)
    for entry in result['entries'] + result.get('retiredEntries', []):
        entry['key'] = entry.pop('sourceKey', None) or legacy_key(entry['key'])
    return result


def _segment(value, fallback):
    value = re.sub(r'([a-z0-9])([A-Z])', r'\1_\2', value)
    value = re.sub(r'[^a-z0-9]+', '_', value.lower()).strip('_')
    value = '_'.join(value.split('_')[:6])[:60].rstrip('_')
    if not value or not value[0].isalpha():
        value = fallback + ('_' + value if value else '')
    return value


def apply_semantic_keys(catalog, previous=None):
    result = deepcopy(catalog)
    names = registry()['legacyToSemantic']
    prior_names = {}
    for entry in (previous or {}).get('entries', []) + (previous or {}).get('retiredEntries', []):
        source_key = entry.get('sourceKey') or legacy_key(entry['key'])
        if KEY_PATTERN.fullmatch(entry['key']):
            prior_names[source_key] = entry['key']
    used = set(names.values()) | set(prior_names.values())
    provisional = []
    all_entries = result['entries'] + result.get('retiredEntries', [])
    for entry in sorted(all_entries, key=lambda e: e['key']):
        old = entry.pop('sourceKey', None) or legacy_key(entry['key'])
        new = names.get(old) or prior_names.get(old)
        if new is None and KEY_PATTERN.fullmatch(entry['key']):
            new = entry['key']
        if new is None:
            source = entry['sources'][0]
            file = _segment(Path(source['file']).stem, 'source')
            location = _segment(source['location'], 'text')
            base = f"unreviewed.{MODULES.get(entry['module'], 'common')}.{file}.{location}.text"
            new = base
            ordinal = 2
            while new in used:
                new = f'{base}_{ordinal}'
                ordinal += 1
        used.add(new)
        entry['key'] = new
        if old not in names:
            entry['sourceKey'] = old
            provisional.append(new)
    public_keys = [e['key'] for e in all_entries]
    if len(public_keys) != len(set(public_keys)):
        raise ValueError('Key迁移产生重复名称，拒绝写入')
    if not all(KEY_PATTERN.fullmatch(key) for key in public_keys):
        raise ValueError('Key迁移产生不规范名称，拒绝写入')
    result['keyPolicy'] = KEY_POLICY
    result['keyMigration'] = {
        'registryFile': 'scripts/text-catalog/key-registry.json',
        'registryVersion': registry()['version'],
        'registeredEntries': sum(e['key'] in reverse_registry() for e in result['entries']),
        'legacyAliasesSupported': True,
        'unreviewedKeys': sorted(provisional),
    }
    return result
