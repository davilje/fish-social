"""Inventory Unity/Overlay copy without changing runtime sources.

Install requirements.txt into a venv, then run this file from any directory.
The JSON zhCN overrides survive regeneration when the source key is unchanged.
"""
from __future__ import annotations

import argparse
from collections import Counter
from datetime import datetime
import hashlib
import json
from pathlib import Path
import re
import sys
from zoneinfo import ZoneInfo
import xml.parsers.expat

import yaml
from tree_sitter import Language, Parser
import tree_sitter_c_sharp
import tree_sitter_typescript

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "unity-overlay文本表.json"
HAN = re.compile(r"[\u3400-\u9fff]")
PLACEHOLDER = re.compile(r"(?<!\{)\{\d+(?:,[^{}:]+)?(?::[^{}]+)?\}(?!\})")
DISPLAY = re.compile(r"text|label|title|caption|content|tooltip|placeholder|message|header|hint|status|toast|prompt|subtitle|description|displayname", re.I)
TECH = re.compile(r"https?://|^#[0-9a-fA-F]{3,8}$|\.(?:png|jpg|jpeg|gif|svg|json|ttf|otf|wav|mp3|ogg|asset|prefab|unity|exe|dll|cs|log|xlsx|html|shader|mat|meta)$", re.I)
ENGLISH_UI = {"Debug", "NEW", "NEW!", "XP", "Lv.", "Lv", "MAX", "MAX!", "ON", "OFF", "OK", "Steam", "cm", "m", "kg", "s", "%", "+", "-", "—", "…", "···", "×", "✓", "✕", "←", "→", "<", ">", "?", "!", "♥", "★", "☆"}
SKIP_DIRS = {"Library", "Temp", "Logs", "obj", "bin", "dist", "node_modules", "Packages", "Builds", ".git", "tests", "Tests"}
PARSERS = {
    ".cs": Parser(Language(tree_sitter_c_sharp.language())),
    ".ts": Parser(Language(tree_sitter_typescript.language_typescript())),
}
entries = {}
scanned = []
skipped = Counter()
parse_errors = []


def text(node):
    return node.text.decode("utf-8")


def walk(node):
    yield node
    for child in node.named_children:
        yield from walk(child)


def context(node):
    current = node
    for _ in range(6):
        if current.parent is None:
            break
        current = current.parent
        if current.type in {"expression_statement", "return_statement", "field_declaration", "local_declaration_statement", "pair", "lexical_declaration"}:
            break
    return text(current)


def module(path):
    p = str(path).lower().replace("fish-social-unity/", "").replace("fishsocialoverlay/", "").replace("desktop-overlay/", "")
    choices = [
        (r"onboarding|guide|tutorial", "新手引导"),
        (r"auth/|login|steam|authenticated", "登录与账号"),
        (r"groundbait|bait|rod|vessel|shop|equipment|fishing_formula", "商店与装备"),
        (r"settlement|returnfee|returnfish|catch|inventory|bag", "鱼获与结算"),
        (r"gallery|species|fishcatalog|fish\.ts|achievements|quality", "图鉴与成就"),
        (r"worldmap|pondselect|ponddetail|ponds\.json|ponds\.ts|pondaccess|clue|spot_tag", "鱼塘与地图"),
        (r"rank|leaderboard", "排行榜"),
        (r"profile|avatar|player.level", "个人中心"),
        (r"social|friend|chat|post|feed|inbox|comment|compose", "好友与社区"),
        (r"setting|notification|audio|effect", "设置"),
        (r"home|pet|room", "家园与桌宠"),
        (r"overlay|fishing|pond|hook|seat|mainwindow", "钓鱼与悬浮窗"),
    ]
    for regex, value in choices:
        if re.search(regex, p):
            return value
    return "通用界面"


def category(path, ctx, kind, value):
    p = str(path)
    if "/Editor/" in p or ctx.startswith("attribute "):
        return "编辑器与界面构建"
    if "/JournalSamples/" in p or "/ReferenceAssets/" in p:
        return "界面预览样例"
    if re.search(r"Debug|Inspect|debug|玩法调试", p + " " + ctx[:400]):
        return "玩法调试"
    if re.search(r"Debug\.(?:Log|LogError|LogWarning)|console\.(?:log|warn|error)|(?:throw|new)\s+\w*Exception|logger\.|LatencyTrace\.Write|Trace\.Write", ctx):
        return "日志与诊断"
    if kind == "Unity序列化":
        return "预制体与场景初始文本"
    if p.startswith("server/"):
        return "关联服务端提示"
    if "/GameData/" in p or "/generated/game-data/" in p:
        return "游戏数据文案"
    if re.search(r"manifest|catalog|design-tokens|motion\.json|layout\.json|effects\.json|markers|README|/OverlayArt/", p, re.I):
        return "资源配置与说明"
    if value in {"txt_error", "txt_groundbait", "哈哈"} and "overlay-hud" in p:
        return "HUD预览占位"
    if p.endswith(".json") and "hud" in p.lower():
        return "HUD配置"
    return "运行时界面文本"


def add(path, line, column, value, kind, ctx="", location="", variables=None, force=False, raw=""):
    if not value or not value.strip():
        skipped["空文本"] += 1
        return
    if not force and TECH.search(value):
        skipped["路径与技术标识"] += 1
        return
    if not force and (re.match(r"\s*(?:SELECT|INSERT\s+INTO|UPDATE\s+\w+\s+SET|DELETE\s+FROM|CREATE\s+TABLE|ALTER\s+TABLE|PRAGMA|DROP\s+TABLE)\b", value, re.I) or value.lstrip().startswith(('{"', '{{"'))):
        skipped["SQL与序列化模板"] += 1
        return
    if not force and (value in {"{}", "[]"} or re.search(r"\\[sSdDwWbB]|\[\^", value)):
        skipped["正则与序列化字串"] += 1
        return
    if not force and not HAN.search(value):
        # UI construction calls also contain hierarchy paths and GameObject names.
        # Keep visible English phrases, uppercase headings, counters and symbols.
        simplified = PLACEHOLDER.sub("", value).strip()
        if not simplified or (variables and re.fullmatch(r"[|:;._-]+", simplified)) or re.fullmatch(r"[a-zA-Z0-9_{}:.-]*:[a-zA-Z0-9_{}:.-]*", value):
            skipped["非显示技术字串"] += 1
            return
        identifier = bool(re.fullmatch(r"[A-Za-z_][A-Za-z_0-9./-]*", simplified))
        plausible = value in ENGLISH_UI or simplified in ENGLISH_UI or (not identifier and not re.search(r"[A-Za-z_]+/", simplified)) or bool(re.fullmatch(r"[A-Z]{2,14}", simplified))
        if not ((DISPLAY.search(ctx) or value in ENGLISH_UI) and plausible and not re.search(r"\.(?:Find|ToString|GetComponent|SetActive|Load|GetSprite)\s*\([^)]*$", ctx[:ctx.find(raw or '"' + value + '"')] if (raw or '"' + value + '"') in ctx else "")):
            skipped["非显示技术字串"] += 1
            return
    cat = category(path, ctx, kind, value)
    mod = module(path)
    key = "txt_" + hashlib.sha256((mod + "\0" + cat + "\0" + kind + "\0" + value).encode()).hexdigest()[:16]
    source = {"file": str(path), "line": line, "column": column, "location": location, "kind": kind}
    if variables:
        source["variables"] = variables
    if raw:
        source["expression"] = raw
    if key not in entries:
        entries[key] = {
            "key": key, "module": mod, "category": cat, "sourceText": value,
            "zhCN": value, "placeholders": PLACEHOLDER.findall(value), "note": "", "sources": []
        }
    entry = entries[key]
    if source not in entry["sources"]:
        entry["sources"].append(source)
    if kind == "拼接模板":
        entry["note"] = "由字符串拼接归一为编号模板；原表达式与参数见来源映射。"
    if cat == "HUD预览占位":
        entry["note"] = "HUD预览占位，会被运行时数据覆盖。"


def decode_escape(value):
    def replace(m):
        s = m.group(1)
        if s.startswith(("u", "U", "x")):
            return chr(int(s[1:], 16))
        return {"n": "\n", "r": "\r", "t": "\t", "0": "\0", "a": "\a", "b": "\b", "f": "\f", "v": "\v", "\\": "\\", '"': '"', "'": "'", "`": "`", "$": "$"}.get(s, s)
    return re.sub(r"\\(u[0-9a-fA-F]{4}|U[0-9a-fA-F]{8}|x[0-9a-fA-F]{1,4}|.)", replace, value, flags=re.S)


def literal(node):
    raw = text(node)
    if raw.startswith('@"'):
        return raw[2:-1].replace('""', '"')
    if raw.startswith('"""'):
        quotes = len(raw) - len(raw.lstrip('"'))
        return raw[quotes:-quotes]
    return decode_escape(raw[1:-1])


def plus(node):
    return node.type == "binary_expression" and any(c.type == "+" for c in node.children)


def make_template(node):
    """Collapse only string additions; numeric/expression operands remain intact."""
    params = []
    def var(n, suffix=""):
        index = len(params)
        params.append({"index": index, "expression": text(n)})
        return "{" + str(index) + suffix + "}"
    def piece(n):
        if n.type in {"string_literal", "verbatim_string_literal", "raw_string_literal", "string"}:
            return literal(n).replace("{", "{{").replace("}", "}}")
        if n.type in {"interpolated_string_expression", "template_string"}:
            fragments = []
            verbatim = '@' in text(n)[:3]
            for c in n.named_children:
                if c.type in {"interpolation_start", "interpolation_quote"}:
                    continue
                if c.type in {"string_content", "string_fragment", "escape_sequence"}:
                    fragments.append(text(c).replace('""', '"') if verbatim else decode_escape(text(c)))
                elif c.type in {"interpolation", "template_substitution"}:
                    expressions = [x for x in c.named_children if x.type not in {"interpolation_brace", "interpolation_format_clause", "interpolation_alignment_clause"}]
                    if expressions:
                        suffix = "".join(text(x) for x in c.named_children if x.type in {"interpolation_format_clause", "interpolation_alignment_clause"})
                        fragments.append(var(expressions[0], suffix))
            return "".join(fragments)
        if plus(n) and any(x.type in {"string_literal", "verbatim_string_literal", "raw_string_literal", "string", "interpolated_string_expression", "template_string"} for x in walk(n)):
            return piece(n.child_by_field_name("left")) + piece(n.child_by_field_name("right"))
        return var(n)
    value = piece(node)
    if not params and plus(node):
        value = value.replace("{{", "{").replace("}}", "}")
    return value, params


def code_file(path, relative):
    data = path.read_bytes()
    lines = data.splitlines()
    tree = PARSERS[path.suffix].parse(data)
    if tree.root_node.has_error:
        parse_errors.append(str(relative))
    nodes = list(walk(tree.root_node))
    covered = set()
    string_types = {"string_literal", "verbatim_string_literal", "raw_string_literal", "interpolated_string_expression", "string", "template_string"}
    for n in nodes:
        if n.id in covered:
            continue
        kind = None
        if plus(n) and any(x.type in string_types for x in walk(n)) and not (n.parent and plus(n.parent)):
            # A large expression containing a conditional is retained as a parameter;
            # nested branch strings are inventoried independently below.
            value, variables = make_template(n)
            kind = "拼接模板"
        elif n.type in {"interpolated_string_expression", "template_string"}:
            value, variables = make_template(n)
            kind = "插值模板"
        elif n.type in string_types:
            value, variables, kind = literal(n), [], "代码常量"
        else:
            continue
        ctx = context(n)
        ancestor = n.parent
        attribute_literal = False
        module_import = False
        while ancestor:
            if ancestor.type == "attribute":
                attribute_literal = True
            if ancestor.type in {"import_statement", "import_declaration"} or (ancestor.type == "export_statement" and ancestor.child_by_field_name("source") is not None):
                module_import = True
                break
            ancestor = ancestor.parent
        if module_import:
            skipped["模块导入路径"] += 1
            continue
        # Only suppress operands consumed as literal pieces, not conditional branch literals.
        def consumed(x):
            if x.type in string_types:
                covered.add(x.id)
                return
            if plus(x):
                consumed(x.child_by_field_name("left"))
                consumed(x.child_by_field_name("right"))
        if kind == "拼接模板":
            consumed(n)
        elif kind == "插值模板":
            # Keep branch literals inside interpolation expressions for independent extraction.
            pass
        if attribute_literal:
            ctx = "attribute " + ctx
        member = ""
        ancestor = n.parent
        while ancestor:
            if ancestor.type in {"method_declaration", "constructor_declaration", "property_declaration", "function_declaration"}:
                name = ancestor.child_by_field_name("name")
                if name:
                    member = text(name)
                break
            ancestor = ancestor.parent
        character_column = len(lines[n.start_point.row][:n.start_point.column].decode("utf-8")) + 1
        add(relative, n.start_point.row + 1, character_column, value, kind, ctx, member, variables,
            raw=text(n) if kind != "代码常量" else "")


def json_file(path, relative):
    data = path.read_text(encoding="utf-8-sig")
    root = json.loads(data)
    def visit(value, pointer="", owner=""):
        if isinstance(value, dict):
            owner = str(next((value[k] for k in ["speciesId", "pondId", "itemId", "achievementId", "rodId", "baitId", "vesselId", "tagId", "clueId", "id"] if k in value), owner))
            for k, v in value.items():
                visit(v, pointer + "/" + str(k).replace("~", "~0").replace("/", "~1"), owner)
        elif isinstance(value, list):
            for i, item in enumerate(value):
                visit(item, pointer + "/" + str(i), owner)
        elif isinstance(value, str):
            field = pointer.rsplit("/", 1)[-1]
            force = bool(re.fullmatch(r"label|text|title|content|tooltip|placeholder|caption|displayName|nameZh|descriptionZh|clueText", field, re.I))
            # YAML's scanner also parses JSON and supplies exact marks; pointers are authoritative.
            add(relative, 0, 0, value, "JSON字段", field, pointer + (" [" + owner + "]" if owner else ""), force=force)
    visit(root)
    # Bind line and column to the corresponding JSON pointer rather than a text search.
    marks = {}
    def mark(node, pointer=""):
        if isinstance(node, yaml.MappingNode):
            for k, v in node.value:
                mark(v, pointer + "/" + k.value.replace("~", "~0").replace("/", "~1"))
        elif isinstance(node, yaml.SequenceNode):
            for i, v in enumerate(node.value):
                mark(v, pointer + "/" + str(i))
        elif isinstance(node, yaml.ScalarNode):
            marks[pointer] = (node.start_mark.line + 1, node.start_mark.column + 1)
    mark(yaml.compose(data, Loader=yaml.CSafeLoader))
    for e in entries.values():
        for src in e["sources"]:
            if src["file"] == str(relative) and src["kind"] == "JSON字段":
                src["line"], src["column"] = marks[src["location"].split(" [", 1)[0]]


def unity_file(path, relative):
    original = path.read_text(encoding="utf-8-sig")
    # Removing the Unity tags keeps every original line in place for source marks.
    data = re.sub(r"^%.*$", "", original, flags=re.M)
    data = re.sub(r"^--- !u!\d+ &(-?\d+).*$", r"--- # fileID=\1", data, flags=re.M)
    def visit(node, prefix="", object_id=""):
        if isinstance(node, yaml.MappingNode):
            mapping = {k.value: v for k, v in node.value}
            overridden_field = mapping.get("propertyPath")
            text_override = overridden_field is not None and overridden_field.value in {"m_Text", "m_text", "m_Title", "m_Label"}
            if "m_GameObject" in mapping:
                ref = mapping["m_GameObject"]
                if isinstance(ref, yaml.MappingNode):
                    object_id = next((v.value for k, v in ref.value if k.value == "fileID"), object_id)
            for k, v in node.value:
                if isinstance(v, yaml.ScalarNode) and (k.value in {"m_Text", "m_text", "m_Title", "m_Label", "m_Placeholder", "m_Tooltip", "m_ToolTip"} or (text_override and k.value == "value") or (v.tag.endswith(":str") and DISPLAY.fullmatch(k.value)) or (HAN.search(v.value) and k.value != "m_Name")):
                    add(relative, v.start_mark.line + 1, v.start_mark.column + 1, v.value,
                        "Unity序列化", k.value, prefix + "/" + k.value + (" [propertyPath=" + overridden_field.value + "]" if text_override else "") + (" GameObject=" + object_id if object_id else ""), force=True)
                else:
                    visit(v, prefix + "/" + k.value, object_id)
        elif isinstance(node, yaml.SequenceNode):
            for i, v in enumerate(node.value):
                visit(v, prefix + "/" + str(i), object_id)
    for index, node in enumerate(yaml.compose_all(data, Loader=yaml.CSafeLoader)):
        if node:
            visit(node, "document[" + str(index) + "]")


def xaml_file(path, relative):
    parser = xml.parsers.expat.ParserCreate()
    stack = []
    def start(name, attrs):
        stack.append(attrs.get("x:Name", attrs.get("Name", name)))
        for k, value in attrs.items():
            if k.rsplit(":", 1)[-1] in {"Text", "Content", "Header", "Title", "ToolTip", "Watermark", "PlaceholderText"} and not value.startswith("{"):
                add(relative, parser.CurrentLineNumber, parser.CurrentColumnNumber + 1, value,
                    "XAML属性", k, "/".join(stack) + "/@" + k, force=True)
    def chars(value):
        if value.strip() and HAN.search(value):
            add(relative, parser.CurrentLineNumber, parser.CurrentColumnNumber + 1, value.strip(),
                "XAML元素文本", "Text", "/".join(stack), force=True)
    parser.StartElementHandler = start
    parser.EndElementHandler = lambda name: stack.pop()
    parser.CharacterDataHandler = chars
    parser.Parse(path.read_bytes(), True)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", type=Path, default=OUTPUT)
    args = parser.parse_args()
    roots = [ROOT / "fish-social-unity/Assets", ROOT / "fish-social-unity/FishSocialOverlay", ROOT / "desktop-overlay", ROOT / "shared"]
    source_files = set()
    for base in roots:
        for p in base.rglob("*"):
            relative = p.relative_to(ROOT)
            if not p.is_file() or any(x in SKIP_DIRS for x in relative.parts) or p.suffix not in {".cs", ".ts", ".json", ".xaml", ".uxml", ".prefab", ".unity", ".asset"}:
                continue
            if p.name in {"package.json", "package-lock.json", "tsconfig.json"} or p.name.endswith(".d.ts"):
                continue
            source_files.add(p)
    # Client-visible responses are included as associated text, excluding ops/admin endpoints.
    for p in (ROOT / "server/src").rglob("*.ts"):
        if not re.search(r"ops|admin|metric|test|simulation|migration|seed", str(p.relative_to(ROOT / "server/src")), re.I):
            source_files.add(p)
    for path in sorted(source_files):
        relative = path.relative_to(ROOT)
        before = sum(len(e["sources"]) for e in entries.values())
        if path.suffix in PARSERS:
            code_file(path, relative)
        elif path.suffix == ".json":
            json_file(path, relative)
        elif path.suffix in {".xaml", ".uxml"}:
            xaml_file(path, relative)
        else:
            unity_file(path, relative)
        scanned.append({"file": str(relative), "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
                        "occurrences": sum(len(e["sources"]) for e in entries.values()) - before})
    previous = {}
    retired = {}
    if args.output.exists():
        previous_data = json.loads(args.output.read_text())
        previous = {e["key"]: e for e in previous_data["entries"]}
        retired = {e["key"]: e for e in previous_data.get("retiredEntries", [])}
    ordered = sorted(entries.values(), key=lambda e: (e["category"], e["module"], e["sources"][0]["file"], e["sources"][0]["line"], e["key"]))
    for e in ordered:
        if e["key"] in previous:
            e["zhCN"] = previous[e["key"]].get("zhCN", e["sourceText"])
            e["note"] = previous[e["key"]].get("note", e["note"])
        if Counter(PLACEHOLDER.findall(e["sourceText"])) != Counter(PLACEHOLDER.findall(e["zhCN"])):
            raise ValueError("占位符不一致: " + e["key"])
        assert e["sources"] and all(s["line"] > 0 for s in e["sources"])
    result = {
        "schemaVersion": 1, "project": "Fish Social Unity + Desktop Overlay", "locale": "zh-CN",
        "generatedAt": datetime.now(ZoneInfo("Asia/Shanghai")).isoformat(timespec="seconds"),
        "runtimeBinding": "inventory-only: 当前工程尚未通过 key 读取此表；修改表格不会自动修改运行时界面。",
        "keyPolicy": "txt_ + SHA256(module + NUL + category + NUL + kind + NUL + sourceText)前16位；不依赖行号。",
        "stats": {"filesScanned": len(scanned), "entries": len(ordered), "sourceOccurrences": sum(len(e["sources"]) for e in ordered),
                  "categories": dict(Counter(e["category"] for e in ordered)), "skipped": dict(skipped)},
        "parseWarnings": parse_errors,
        "externalText": [
            {"fields": "Nickname / Text / 玩家昵称、签名、聊天、动态和评论", "policy": "用户输入内容，不预填固定文案。"},
            {"fields": "ErrorMessage / GuideTip / OverlayPromptTitle / OverlayPromptBody / OverlayPromptButton", "policy": "主进程或服务端动态输入；已收录可扫描的本地/关联提示，变量内容由运行时提供。"},
            {"fields": "图片内嵌文字、第三方插件 UI、服务器数据库运营配置", "policy": "不属于本次源码与序列化字段文本抽取范围。"}
        ],
        "knownDifferences": [
            {"location": "Overlay HUD btn_catch_leave", "values": ["离开座位", "离席"], "note": "HUD JSON 初始配置与 XAML/运行时代码存在差异，分别保留来源。"}
        ],
        "files": scanned, "entries": ordered,
        "retiredEntries": [e for k, e in {**retired, **previous}.items() if k not in entries and e.get("zhCN") != e.get("sourceText")],
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"output": str(args.output), **result["stats"], "parseWarnings": parse_errors}, ensure_ascii=False))


if __name__ == "__main__":
    main()
