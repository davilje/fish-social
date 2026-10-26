# One-shot: rename docs taxonomy folders to Chinese and rewrite path references.
import json
import os
import re
import subprocess
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DOCS = ROOT / "docs"

NAMES = {
    "analytics": "数据分析",
    "archive": "归档",
    "art": "美术",
    "compliance": "合规",
    "design": "设计",
    "engineering": "工程",
    "maintenance": "维护",
    "monitoring": "监控",
    "ops": "运维",
    "planning": "策划",
    "architecture": "架构",
    "handoffs": "交接",
    "product": "产品",
    "prompts": "提示词",
    "reports": "报告",
    "reviews": "评审",
    "specs": "规格",
    "templates": "模板",
    "workflow": "流程",
    "ui": "界面",
    "current": "现行",
    "home": "家园",
    "journal": "手账",
    "overlay": "浮层",
    "previews": "预览",
    "profile": "资料",
    "prototype": "原型",
    "references": "参考",
    "animation-interaction": "动画交互",
    "audio": "声音",
    "performance": "性能",
    "scenes": "场景",
    "water-effects": "水面特效",
    "bite-escape-calibration": "咬钩脱钩校准",
    "daily": "日报",
    "growth": "增长",
    "pond-day-simulation": "鱼塘日模拟",
    "pond-ecology-initial": "鱼塘生态初值",
    "runs": "运行记录",
    "warehouse": "数仓",
    "alert-rules": "告警规则",
    "grafana-dashboards": "监控面板",
    "animations": "动画",
    "audio-22i": "声音",
    "cat-skins": "猫咪形象",
    "cozy-journal": "手账",
    "dafeiyu": "大肥鱼",
    "direction": "风格",
    "fish-catalog": "鱼图",
    "fishing-motion": "钓鱼动作",
    "fishing-rest": "睡眼",
    "generated": "生成",
    "login-pastel-2026-10-26": "登录",
    "water-effects-22h": "水面特效",
    "evidence": "证据",
    "indexes": "索引",
    "retired": "废弃",
    "superseded": "替代",
    "versions": "版本",
    "appendix": "附录",
    "backend": "后端",
    "bugs": "缺陷",
    "data-ops": "数据运维",
    "desktop": "桌面端",
    "gameplay": "玩法",
    "roadmaps": "路线图",
    "world": "世界观",
    "journal-workspace": "手账工作台",
    "social-refinement": "钓友来信",
    "steam-desktop-22hij": "鱼塘表现",
    "home-room-animation-unity": "家园动画",
    "home-room-unity": "家园房间",
    "latest": "最新",
    "audio-22i-adoption": "声音采用",
    "audio-22i-v5": "声音第五版",
}

# Descendants of these folders are asset packs or version trees, not taxonomy.
CONTAINERS = {
    "cat-skins", "cozy-journal", "fish-catalog", "water-effects-22h", "dafeiyu",
    "fishing-motion", "fishing-rest", "login-pastel-2026-10-26", "audio-22i",
    "animations", "generated", "direction", "sangbiao", "breeds", "frames",
    "painted", "pack", "hd-v2", "full-v1", "assets", "magic-v4", "cats-v4",
    "template-study-v1", "character-study-2026-10-24", "realistic-v1",
    "cartoon-study-v1", "orange", "v5",
    "猫咪形象", "鱼图", "大肥鱼", "钓鱼动作", "睡眼", "登录", "动画", "生成", "风格",
    "手账", "声音", "水面特效",
}

TEXT_EXT = {
    ".md", ".html", ".json", ".js", ".mjs", ".cjs", ".py", ".cs", ".txt", ".csv",
    ".yml", ".yaml", ".toml", ".xml", ".css", ".bat", ".ps1", ".mdc", ".prompt",
    ".svg",
}
SKIP_DIR = {".git", "node_modules", "Library", "Temp", "obj", "bin", "Logs"}


def rel(path: Path) -> str:
    return path.relative_to(DOCS).as_posix()


def collect():
    all_dirs = [p for p in DOCS.rglob("*") if p.is_dir()]
    rename = []
    skipped = []
    for path in all_dirs:
        if path.name not in NAMES:
            continue
        ancestors = set(path.relative_to(DOCS).parts[:-1])
        if ancestors & CONTAINERS:
            skipped.append(path)
            continue
        rename.append(path)
    return rename, skipped


def git_mv_path(rel_src, rel_dst):
    result = subprocess.run(
        ["git", "mv", "--", rel_src, rel_dst],
        cwd=ROOT, capture_output=True,
    )
    if result.returncode == 0:
        return ""
    return result.stderr.decode("utf-8", "replace")


def move_tree(src: Path, dst: Path):
    if not src.exists():
        return
    dst.parent.mkdir(parents=True, exist_ok=True)
    error = ""
    for attempt in range(5):
        error = git_mv_path(src.relative_to(ROOT).as_posix(), dst.relative_to(ROOT).as_posix())
        if not error:
            return
        if "Permission denied" not in error and "bad source" not in error:
            raise SystemExit(error)
        time.sleep(0.4 * (attempt + 1))
    if not src.is_dir():
        raise SystemExit(error)
    dst.mkdir(parents=True, exist_ok=True)
    for child in list(src.iterdir()):
        move_tree(child, dst / child.name)
    try:
        src.rmdir()
    except OSError:
        if not only_empty(src):
            raise SystemExit(error)


def only_empty(path: Path) -> bool:
    if not path.is_dir():
        return False
    return all(only_empty(child) for child in path.iterdir())


def git_mv(pairs):
    for src, dst in pairs:
        if not src.exists():
            continue
        if dst.exists():
            for child in list(src.iterdir()):
                target = dst / child.name
                if target.exists():
                    raise SystemExit(f"both sides have {target}")
                move_tree(child, target)
            try:
                src.rmdir()
            except OSError:
                if not only_empty(src):
                    raise SystemExit(f"could not remove {src}")
            continue
        move_tree(src, dst)


def replace_path(text, old, new, paths_only_with_docs_prefix):
    text = text.replace("docs/" + old, "docs/" + new)
    old_win = old.replace("/", "\\")
    new_win = new.replace("/", "\\")
    text = text.replace("docs\\" + old_win, "docs\\" + new_win)
    if paths_only_with_docs_prefix:
        return text
    if "/" in old:
        text = re.sub(
            r"(?<![A-Za-z0-9_\-])" + re.escape(old) + r"(?![A-Za-z0-9_\-])",
            lambda _match: new,
            text,
        )
        text = re.sub(
            r"(?<![A-Za-z0-9_\-])" + re.escape(old_win) + r"(?![A-Za-z0-9_\-])",
            lambda _match: new_win,
            text,
        )
    else:
        text = re.sub(
            r"(?<=[/\\(\"'])" + re.escape(old) + r"(?=[/\\)\"'\s#?]|$)",
            lambda _match: new,
            text,
        )
        text = re.sub(
            r"(?<=[/\\(\"'])" + re.escape(old_win) + r"(?=[/\\)\"'\s#?]|$)",
            lambda _match: new_win,
            text,
        )
    return text


def apply_renames(text, full_paths, paths_only_with_docs_prefix=False):
    for old, new in full_paths:
        text = replace_path(text, old, new, paths_only_with_docs_prefix)
    return text


def protect(text, keeps):
    tokens = []
    for index, keep in enumerate(keeps):
        parent, child = keep.rsplit("/", 1)
        token = f"__KEEP_DIR_{index}__"
        text = text.replace(f"{parent}/{child}", f"{parent}/{token}")
        text = text.replace(f"{parent}\\{child}", f"{parent}\\{token}")
        tokens.append((token, child))
    return text, tokens


def restore(text, tokens):
    for token, name in tokens:
        text = text.replace(token, name)
    return text


PACK_MARKS = {
    "猫咪形象", "鱼图", "大肥鱼", "钓鱼动作", "睡眼", "登录", "动画", "生成", "风格",
}


def in_asset_pack(path: Path) -> bool:
    try:
        parts = path.relative_to(DOCS).parts
    except ValueError:
        return False
    for index, part in enumerate(parts):
        if part in CONTAINERS or part in PACK_MARKS:
            return True
        parent = parts[index - 1] if index else ""
        if part == "手账" and parent not in {"界面", "ui"}:
            return True
        if part == "声音" and parent in {"美术", "art", "替代", "superseded"}:
            return True
        if part == "水面特效" and parent in {"美术", "art", "替代", "superseded"}:
            return True
    return False


def basename_pass(text, safe_basenames):
    # Only path segments, so prose like "current art" stays English.
    for name, chinese in sorted(safe_basenames.items(), key=lambda item: len(item[0]), reverse=True):
        text = re.sub(
            r"(?<=[/\\])" + re.escape(name) + r"(?=[/\\)\"'\s#?]|$)",
            lambda _match, chinese=chinese: chinese,
            text,
        )
        text = re.sub(
            r"(?<=[(\"'])" + re.escape(name) + r"(?=/)",
            lambda _match, chinese=chinese: chinese,
            text,
        )
    return text


def iter_text_files():
    for dirpath, dirnames, filenames in os.walk(ROOT):
        dirnames[:] = [name for name in dirnames if name not in SKIP_DIR]
        for name in filenames:
            path = Path(dirpath) / name
            if path.name == "rename-docs-folders.py":
                continue
            if path.suffix.lower() not in TEXT_EXT:
                continue
            if path.stat().st_size > 8_000_000:
                continue
            yield path


def blob_paths(cached):
    if cached:
        result = subprocess.run(["git", "ls-files", "-s", "-z"], cwd=ROOT, capture_output=True)
    else:
        result = subprocess.run(["git", "ls-tree", "-r", "-z", "HEAD"], cwd=ROOT, capture_output=True)
    if result.returncode != 0:
        raise SystemExit(result.stderr.decode("utf-8", "replace"))
    found = {}
    for record in result.stdout.split(b"\0"):
        if not record:
            continue
        meta, path = record.split(b"\t", 1)
        pieces = meta.split()
        # ls-files: mode hash stage; ls-tree: mode type hash
        blob = pieces[1] if cached else pieces[2]
        found.setdefault(blob, []).append(path.decode("utf-8"))
    return found


def directory_renames():
    head = blob_paths(cached=False)
    index = blob_paths(cached=True)
    mapping = {}
    for blob, old_paths in head.items():
        new_paths = index.get(blob)
        if not new_paths or len(old_paths) != 1 or len(new_paths) != 1:
            continue
        old, new = old_paths[0], new_paths[0]
        if old == new or not old.startswith("docs/") or not new.startswith("docs/"):
            continue
        old_dir = old.split("/")[:-1]
        new_dir = new.split("/")[:-1]
        for length in range(1, min(len(old_dir), len(new_dir)) + 1):
            old_prefix = "/".join(old_dir[:length])
            new_prefix = "/".join(new_dir[:length])
            if old_prefix == new_prefix:
                continue
            old_rel = old_prefix[len("docs/"):]
            new_rel = new_prefix[len("docs/"):]
            if not old_rel:
                continue
            previous = mapping.get(old_rel)
            if previous and previous != new_rel:
                continue
            mapping[old_rel] = new_rel
    return sorted(mapping.items(), key=lambda item: len(item[0]), reverse=True)


PATH_TOKEN = re.compile(r"(?:docs[/\\])?(?:\.\.[/\\])*(?:[A-Za-z0-9_.-]+[/\\])+(?:[A-Za-z0-9_.-]+)?")
DOCS_PATH = re.compile(r"docs[/\\](?:[A-Za-z0-9_.-]+[/\\])*[A-Za-z0-9_.-]+")


ROOTS = {
    "docs",
    "analytics", "archive", "art", "compliance", "design", "engineering",
    "maintenance", "monitoring", "ops", "planning",
    "数据分析", "归档", "美术", "合规", "设计", "工程", "维护", "监控", "运维", "策划",
}


def rewrite_token(token: str) -> str:
    slash = "\\" if "\\" in token and "/" not in token else "/"
    parts = re.split(r"[/\\]", token)
    rewritten = []
    armed = False
    blocked = False
    for part in parts:
        if part in {"", ".", ".."}:
            rewritten.append(part)
            continue
        if blocked:
            rewritten.append(part)
            continue
        if part in CONTAINERS:
            rewritten.append(NAMES.get(part, part))
            blocked = True
            continue
        if armed or part in ROOTS:
            rewritten.append(NAMES.get(part, part))
            armed = True
            continue
        rewritten.append(part)
    return slash.join(rewritten)


def rewrite_body(text: str, file_in_pack: bool, docs_prefix_only: bool) -> str:
    text = DOCS_PATH.sub(lambda match: rewrite_token(match.group(0)), text)
    if docs_prefix_only:
        return text

    def replace(match):
        token = match.group(0)
        if token.startswith("docs/") or token.startswith("docs\\"):
            return token
        if file_in_pack and not token.startswith(".."):
            return token
        return rewrite_token(token)

    return PATH_TOKEN.sub(replace, text)


def main():
    rename, skipped = collect()
    skipped_names = {path.name for path in skipped}
    safe_basenames = {
        name: chinese
        for name, chinese in NAMES.items()
        if name not in skipped_names
    }
    inverse = {}
    for english, chinese in NAMES.items():
        inverse.setdefault(chinese, set()).add(english)
    keep_set = set()
    for path in skipped:
        parents = {path.parent.name}
        parents.update(inverse.get(path.parent.name, ()))
        for parent in parents:
            keep_set.add(f"{parent}/{path.name}")
    keeps = sorted(keep_set, key=len, reverse=True)
    pairs = sorted(rename, key=lambda path: len(path.relative_to(DOCS).parts), reverse=True)
    moves = []
    for src in pairs:
        dst = src.with_name(NAMES[src.name])
        moves.append((src, dst))
    print(f"rename {len(moves)} folders; protect {len(keeps)} asset paths", flush=True)
    git_mv(moves)

    path_map_file = None
    changed = 0
    for path in iter_text_files():
        if path.name == "path-map.json" and path.parent.name in {"maintenance", "维护"}:
            path_map_file = path
            continue
        raw = path.read_bytes()
        try:
            text = raw.decode("utf-8")
        except UnicodeDecodeError:
            continue
        newline = "\r\n" if "\r\n" in text else "\n"
        body = text.replace("\r\n", "\n")
        inside_docs = DOCS in path.parents or path.parent == DOCS
        if not inside_docs and "docs/" not in body and "docs\\" not in body:
            continue
        updated = rewrite_body(body, in_asset_pack(path) if inside_docs else False, docs_prefix_only=not inside_docs)
        if updated != body:
            path.write_bytes(updated.replace("\n", newline).encode("utf-8"))
            changed += 1
    print(f"rewrote {changed} files", flush=True)

    if path_map_file and path_map_file.exists():
        data = json.loads(path_map_file.read_text(encoding="utf-8"))
        rewritten = {}
        for key, value in data.items():
            if isinstance(value, str):
                value = rewrite_body(value, False, docs_prefix_only=False)
            rewritten[key] = value
            if isinstance(key, str) and key.startswith("docs/"):
                mapped = rewrite_body(key, False, docs_prefix_only=False)
                if mapped != key:
                    rewritten.setdefault(key, mapped)
        path_map_file.write_text(json.dumps(rewritten, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        print(f"path-map entries {len(rewritten)}", flush=True)


if __name__ == "__main__":
    main()
