# One-shot: rename docs taxonomy folders to Chinese and rewrite path references.
import json
import os
import re
import subprocess
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


def git_mv(pairs):
    for src, dst in pairs:
        dst.parent.mkdir(parents=True, exist_ok=True)
        subprocess.run(["git", "mv", "--", src.as_posix(), dst.as_posix()], cwd=ROOT, check=True)


def replace_path(text, old, new):
    text = text.replace("docs/" + old, "docs/" + new)
    text = text.replace("docs\\" + old.replace("/", "\\"), "docs\\" + new.replace("/", "\\"))
    text = re.sub(
        r"(?<![A-Za-z0-9_\-])" + re.escape(old) + r"(?![A-Za-z0-9_\-])",
        new.replace("\\", r"\\"),
        text,
    )
    old_win = old.replace("/", "\\")
    new_win = new.replace("/", "\\")
    text = re.sub(
        r"(?<![A-Za-z0-9_\-])" + re.escape(old_win) + r"(?![A-Za-z0-9_\-])",
        new_win.replace("\\", r"\\"),
        text,
    )
    return text


def apply_renames(text, full_paths):
    for old, new in full_paths:
        text = replace_path(text, old, new)
    return text


def protect(text, keeps):
    tokens = []
    for index, keep in enumerate(keeps):
        token = f"__KEEP_DIR_{index}__"
        text = text.replace(keep, token)
        text = text.replace(keep.replace("/", "\\"), token)
        tokens.append((token, keep.split("/")[-1]))
    return text, tokens


def restore(text, tokens):
    for token, name in tokens:
        text = text.replace(token, name)
    return text


def basename_pass(text, safe_basenames):
    for name, chinese in sorted(safe_basenames.items(), key=lambda item: len(item[0]), reverse=True):
        text = re.sub(
            r"(?<![A-Za-z0-9_\-])" + re.escape(name) + r"(?![A-Za-z0-9_\-])",
            chinese,
            text,
        )
    return text


def iter_text_files():
    for dirpath, dirnames, filenames in os.walk(ROOT):
        dirnames[:] = [name for name in dirnames if name not in SKIP_DIR]
        for name in filenames:
            path = Path(dirpath) / name
            if path.suffix.lower() not in TEXT_EXT:
                continue
            if path.stat().st_size > 8_000_000:
                continue
            yield path


def main():
    rename, skipped = collect()
    by_name = {}
    for path in rename:
        by_name.setdefault(path.name, []).append(path)
    skipped_names = {path.name for path in skipped}
    safe_basenames = {
        name: NAMES[name]
        for name in by_name
        if name not in skipped_names
    }
    keeps = sorted({f"{path.parent.name}/{path.name}" for path in skipped}, key=len, reverse=True)
    pairs = sorted(rename, key=lambda path: len(path.relative_to(DOCS).parts), reverse=True)
    moves = []
    for src in pairs:
        dst = src.with_name(NAMES[src.name])
        if dst.exists():
            raise SystemExit(f"destination exists: {dst}")
        moves.append((src, dst))
    print(f"rename {len(moves)} folders; protect {len(keeps)} asset paths")
    git_mv(moves)

    full_paths = []
    for src, dst in moves:
        old = src.relative_to(DOCS).as_posix()
        new = dst.relative_to(DOCS).as_posix()
        # Parents are still the original names here; rewrite the whole chain.
        old_parts = old.split("/")
        new_parts = [NAMES.get(part, part) for part in old_parts]
        full_paths.append(("/".join(old_parts), "/".join(new_parts)))
    full_paths.sort(key=lambda item: len(item[0]), reverse=True)

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
        updated, tokens = protect(body, keeps)
        updated = apply_renames(updated, full_paths)
        updated = basename_pass(updated, safe_basenames)
        updated = restore(updated, tokens)
        if updated != body:
            path.write_bytes(updated.replace("\n", newline).encode("utf-8"))
            changed += 1
    print(f"rewrote {changed} files")

    if path_map_file and path_map_file.exists():
        data = json.loads(path_map_file.read_text(encoding="utf-8"))
        rewritten = {}
        for key, value in data.items():
            if isinstance(value, str):
                updated, tokens = protect(value, keeps)
                updated = apply_renames(updated, full_paths)
                updated = basename_pass(updated, safe_basenames)
                value = restore(updated, tokens)
            rewritten[key] = value
        for old, new in full_paths:
            rewritten.setdefault("docs/" + old, "docs/" + new)
        path_map_file.write_text(json.dumps(rewritten, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        print(f"path-map entries {len(rewritten)}")


if __name__ == "__main__":
    main()
