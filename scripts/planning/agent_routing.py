"""Load agent routing config and resolve target dev agents for a spec."""

from __future__ import annotations

import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
ROUTING_PATH = ROOT / "docs/planning/agent-routing.json"
HANDOFFS_DIR = ROOT / "docs/planning/handoffs"


def load_routing() -> dict:
    return json.loads(ROUTING_PATH.read_text(encoding="utf-8"))


def extract_meta_fields(text: str) -> dict[str, str | list[str]]:
    fields: dict[str, str] = {}
    for key in ("来源 Agent", "目标开发 Agent", "来源", "目标开发", "标签"):
        m = re.search(rf"\|\s*{re.escape(key)}\s*\|\s*([^|\n]+)\|", text)
        if m:
            fields[key] = m.group(1).strip()
    tags_line = fields.get("标签", "")
    tags = [t.strip() for t in re.split(r"[,，、\s]+", tags_line) if t.strip()]
    source = fields.get("来源 Agent") or fields.get("来源") or ""
    target = fields.get("目标开发 Agent") or fields.get("目标开发") or ""
    return {"source": source, "target": target, "tags": tags, "raw": fields}


def infer_tags_from_text(text: str) -> list[str]:
    keywords = {
        "mobile": ["mobile", "客户端", "前端", "expo", "react native", "ui"],
        "server": ["server", "后端", "socket", "sqlite", "api", "鉴权"],
        "analytics": ["数据分析", "模拟", "analytics", "报表", "埋点"],
        "art": ["原画", "美术", "贴图", "sprite", "lottie", "资源"],
        "架构": ["架构", "运维", "性能", "checkpoint"],
    }
    lower = text.lower()
    tags: list[str] = []
    for tag, words in keywords.items():
        if any(w.lower() in lower for w in words):
            tags.append(tag)
    return tags


def resolve_targets(
    *,
    source_agent: str = "",
    explicit_target: str = "",
    tags: list[str] | None = None,
    spec_text: str = "",
) -> list[str]:
    cfg = load_routing()
    if explicit_target:
        if explicit_target in ("both", "全栈", "前后端"):
            return ["frontend-dev", "backend-dev"]
        mapping = {
            "前端开发": "frontend-dev",
            "前端": "frontend-dev",
            "后端开发": "backend-dev",
            "后端": "backend-dev",
            "原画": "art",
        }
        for k, v in mapping.items():
            if k in explicit_target:
                return [v]
        if explicit_target in cfg["agents"]:
            return [explicit_target]

    tags = tags or []
    if spec_text and not tags:
        tags = infer_tags_from_text(spec_text)

    source_key = ""
    for aid, meta in cfg["agents"].items():
        if meta.get("label") == source_agent or aid == source_agent:
            source_key = aid
            break

    matched: list[str] = []
    for rule in cfg["routing_rules"]:
        m = rule.get("match", {})
        rule_sources = m.get("source", [])
        rule_tags = m.get("tags", [])
        if rule_sources and source_key and source_key not in rule_sources:
            continue
        if rule_tags and not any(
            t in tags or any(rt in t for rt in rule_tags) for t in tags
        ):
            if not any(rt.lower() in spec_text.lower() for rt in rule_tags):
                continue
        target = rule.get("target")
        if target == "both":
            matched.extend(rule.get("targets", ["frontend-dev", "backend-dev"]))
        elif target:
            matched.append(target)

    if matched:
        seen: set[str] = set()
        out: list[str] = []
        for t in matched:
            if t not in seen:
                seen.add(t)
                out.append(t)
        return out

    if source_key:
        default = cfg["agents"].get(source_key, {}).get("default_target")
        if default:
            return [default]

    if "mobile" in tags or "ui" in tags:
        return ["frontend-dev"]
    if "server" in tags or "架构" in tags or "analytics" in tags:
        return ["backend-dev"]
    return ["backend-dev"]


def agent_label(agent_id: str) -> str:
    cfg = load_routing()
    return cfg["agents"].get(agent_id, {}).get("label", agent_id)


def default_verify_scripts(targets: list[str]) -> list[str]:
    cfg = load_routing()
    scripts: list[str] = []
    for t in targets:
        for s in cfg.get("default_verify_by_target", {}).get(t, []):
            if s and s not in scripts:
                scripts.append(s)
    return scripts


def write_handoff_manifest(
    version: str,
    *,
    source_agent: str,
    targets: list[str],
    spec_path: Path,
    handoff_path: Path,
    prompt_path: Path,
    verify_scripts: list[str],
) -> Path:
    HANDOFFS_DIR.mkdir(parents=True, exist_ok=True)
    manifest = {
        "version": version,
        "source_agent": source_agent,
        "target_agents": targets,
        "spec": str(spec_path.relative_to(ROOT)).replace("\\", "/"),
        "handoff_md": str(handoff_path.relative_to(ROOT)).replace("\\", "/"),
        "prompt": str(prompt_path.relative_to(ROOT)).replace("\\", "/"),
        "verify_scripts": verify_scripts,
        "pipeline_status": "handoff_ready",
    }
    out = HANDOFFS_DIR / f"{version}.json"
    out.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return out


def write_target_prompts(base_prompt: str, version: str, targets: list[str]) -> list[Path]:
    prompts_dir = ROOT / "docs/planning/prompts"
    prompts_dir.mkdir(parents=True, exist_ok=True)
    scope_blocks = {
        "backend-dev": "\n## 角色范围（后端开发 Agent）\n- 仅改 `server/`、`shared/`、`scripts/`\n- 禁止改 `mobile/`\n",
        "frontend-dev": "\n## 角色范围（前端开发 Agent）\n- 仅改 `mobile/`\n- 禁止改 `server/`、`shared/` 业务逻辑（可读类型）\n",
        "art": "\n## 角色范围（原画 Agent）\n- 产出 `mobile/assets/` 或 `docs/art/`\n- 不写服务端逻辑\n",
    }
    written: list[Path] = []
    for target in targets:
        suffix = scope_blocks.get(target, "")
        out = prompts_dir / f"{version}-{target}.prompt.md"
        header = f"<!-- Target agent: {target} | Auto from handoff pipeline -->\n\n"
        out.write_text(header + base_prompt + suffix, encoding="utf-8")
        written.append(out)
    return written
