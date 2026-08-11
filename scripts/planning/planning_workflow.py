#!/usr/bin/env python3
"""
策划 → 开发 → 验收 → 结案 工作流 CLI（含多 Agent 路由）

Usage:
  python scripts/planning/planning_workflow.py handoff v0.5.2 --source data-analysis
  python scripts/planning/planning_workflow.py verify v0.5.2
  python scripts/planning/planning_workflow.py accept v0.5.2
  python scripts/planning/planning_workflow.py close v0.5.2
"""

from __future__ import annotations

import argparse
import json
import subprocess
import sys
from datetime import date
from pathlib import Path

from agent_routing import (
    agent_label,
    default_verify_scripts,
    extract_meta_fields,
    load_routing,
    resolve_targets,
    write_handoff_manifest,
    write_target_prompts,
)
from spec_status_utils import (
    extract_main_spec_from_handoff,
    read_spec_status,
    set_spec_status,
)

ROOT = Path(__file__).resolve().parents[2]
SPECS_DIR = ROOT / "docs/planning" / "specs"
PROMPTS_DIR = ROOT / "docs/planning/prompts"
HANDOFFS_DIR = ROOT / "docs/planning/handoffs"
OUT_XLSX = SPECS_DIR / "项目开发需求计划表.xlsx"


def normalize_version(v: str) -> str:
    v = v.strip()
    return v if v.startswith("v") else f"v{v}"


def handoff_md_path(version: str) -> Path:
    return SPECS_DIR / f"{version}-开发交接.md"


def manifest_path(version: str) -> Path:
    return HANDOFFS_DIR / f"{version}.json"


def run(cmd: list[str], *, cwd: Path = ROOT) -> None:
    print(f"$ {' '.join(cmd)}")
    result = subprocess.run(cmd, cwd=cwd)
    if result.returncode != 0:
        raise SystemExit(result.returncode)


def export_plan() -> bool:
    try:
        run([sys.executable, str(ROOT / "scripts/planning/export_specs_xlsx_refined.py")])
        return True
    except SystemExit:
        print("[WARN] 计划表 xlsx 写入失败（文件可能被 Excel 占用），其余 handoff 步骤继续")
        return False


def generate_prompt(version: str) -> Path:
    run(["node", str(ROOT / "scripts/planning/generate-handoff-prompt.mjs"), version])
    return PROMPTS_DIR / f"{version}-dev.prompt.md"


def load_manifest(version: str) -> dict:
    path = manifest_path(version)
    if not path.exists():
        raise SystemExit(
            f"缺少 handoff manifest: {path.relative_to(ROOT)}\n"
            f"先运行: npm run planning:handoff -- {version}"
        )
    return json.loads(path.read_text(encoding="utf-8"))


def save_manifest(manifest: dict) -> None:
    HANDOFFS_DIR.mkdir(parents=True, exist_ok=True)
    version = manifest["version"]
    manifest_path(version).write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )


def _confirm_core(version: str, force: bool) -> tuple[Path | None, Path]:
    handoff = handoff_md_path(version)
    if not handoff.exists():
        raise SystemExit(f"缺少开发交接文档: {handoff.relative_to(ROOT)}")

    main_spec = extract_main_spec_from_handoff(handoff)
    if main_spec:
        status = read_spec_status(main_spec)
        if status != "已确认" and not force:
            raise SystemExit(
                f"主 spec 状态为「{status}」，需为「已确认」。"
                f"\n  {main_spec.relative_to(ROOT)}"
                f"\n加 --force 可自动改为已确认。"
            )
        if force or status != "已确认":
            set_spec_status(main_spec, "已确认")
            print(f"[OK] 主 spec -> 已确认: {main_spec.name}")
    else:
        print("[WARN] 未从交接文档解析到 **策划文档** 路径")

    if force or read_spec_status(handoff) != "已确认":
        set_spec_status(handoff, "已确认", note="待开发")

    export_ok = export_plan()
    prompt_file = generate_prompt(version)
    return main_spec, prompt_file, export_ok


def cmd_export(_: argparse.Namespace) -> None:
    if export_plan():
        print(f"\n[OK] 已更新 {OUT_XLSX.relative_to(ROOT)}")


def cmd_confirm(args: argparse.Namespace) -> None:
    version = normalize_version(args.version)
    _, prompt_file, _ = _confirm_core(version, args.force)
    print("\n策划登记完成")
    print(f"计划表: {OUT_XLSX.relative_to(ROOT)}")
    print(f"基础 prompt: {prompt_file.relative_to(ROOT)}")
    print(f"多 Agent 路由: npm run planning:handoff -- {version}")


def cmd_handoff(args: argparse.Namespace) -> None:
    version = normalize_version(args.version)
    main_spec, base_prompt_path, export_ok = _confirm_core(version, args.force)

    handoff = handoff_md_path(version)
    spec_text = ""
    meta: dict = {"source": "", "target": "", "tags": []}
    if main_spec:
        spec_text = main_spec.read_text(encoding="utf-8")
        meta = extract_meta_fields(spec_text)

    source_agent = args.source or meta.get("source") or "planning"
    targets = resolve_targets(
        source_agent=str(source_agent),
        explicit_target=str(meta.get("target", "")),
        tags=list(meta.get("tags", [])),
        spec_text=spec_text + handoff.read_text(encoding="utf-8"),
    )

    base_prompt = base_prompt_path.read_text(encoding="utf-8")
    target_prompts = write_target_prompts(base_prompt, version, targets)
    verify_scripts = list(args.verify) if args.verify else default_verify_scripts(targets)

    manifest = write_handoff_manifest(
        version,
        source_agent=str(source_agent),
        targets=targets,
        spec_path=main_spec or handoff,
        handoff_path=handoff,
        prompt_path=base_prompt_path,
        verify_scripts=verify_scripts,
    )

    print("\n" + "=" * 60)
    print("多 Agent 移交包已生成")
    print("=" * 60)
    print(f"Manifest: {manifest.relative_to(ROOT)}")
    print(f"来源: {agent_label(str(source_agent))}")
    print(f"目标: {', '.join(agent_label(t) for t in targets)}")
    print("\n在新对话打开对应开发 Agent，@ 下列 prompt：")
    for t, p in zip(targets, target_prompts):
        rule = load_routing()["agents"].get(t, {}).get("cursor_rule", "")
        print(f"  - {agent_label(t)}: @{p.relative_to(ROOT)}")
        if rule:
            print(f"    规则: {rule}")
    if not export_ok:
        print("[WARN] 请关闭 Excel 后运行: npm run planning:export")
    if verify_scripts:
        print(f"\n验收: npm run planning:verify -- {version}")
    print(f"结案: npm run planning:accept -- {version} && npm run planning:close -- {version}")


def cmd_verify(args: argparse.Namespace) -> None:
    version = normalize_version(args.version)
    manifest = load_manifest(version)
    scripts = list(args.verify) if args.verify else manifest.get("verify_scripts") or []
    if not scripts:
        print("[WARN] 未配置 verify_scripts，跳过")
    else:
        for script in scripts:
            run(["npm", "run", script])
    manifest["pipeline_status"] = "verified"
    save_manifest(manifest)
    print("\n[OK] pipeline_status=verified")
    print(f"下一步: npm run planning:accept -- {version}")


def cmd_accept(args: argparse.Namespace) -> None:
    version = normalize_version(args.version)
    handoff = handoff_md_path(version)
    mf = manifest_path(version)

    if mf.exists():
        m = load_manifest(version)
        if m.get("pipeline_status") != "verified":
            scripts = list(args.verify) if args.verify else m.get("verify_scripts") or []
            for script in scripts:
                run(["npm", "run", script])
    elif args.verify:
        for script in args.verify:
            run(["npm", "run", script])

    main_spec = extract_main_spec_from_handoff(handoff)
    if main_spec:
        set_spec_status(main_spec, "已实现")
        print(f"[OK] 主 spec -> 已实现: {main_spec.name}")
    set_spec_status(handoff, "已实现")
    export_plan()

    if mf.exists():
        m = load_manifest(version)
        m["pipeline_status"] = "accepted"
        save_manifest(m)

    print(f"[OK] 计划表已更新: {OUT_XLSX.relative_to(ROOT)}")
    print(f"策划结案: npm run planning:close -- {version}")


def cmd_close(args: argparse.Namespace) -> None:
    version = normalize_version(args.version)
    handoff = handoff_md_path(version)
    main_spec = extract_main_spec_from_handoff(handoff)

    if main_spec and read_spec_status(main_spec) != "已实现":
        raise SystemExit("请先运行 planning:accept")
    if read_spec_status(handoff) != "已实现":
        raise SystemExit("请先运行 planning:accept")

    export_plan()
    if manifest_path(version).exists():
        m = load_manifest(version)
        m["pipeline_status"] = "closed"
        save_manifest(m)

    print("\n策划 Agent 结案清单：")
    print("- [ ] CHANGELOG.md 追加实现节")
    print("- [ ] specs/README.md 更新索引")
    print("- [ ] spec §验收 勾选 [x]")
    print("- [x] 项目开发需求计划表.xlsx 已重建")


def cmd_status(args: argparse.Namespace) -> None:
    version = normalize_version(args.version)
    handoff = handoff_md_path(version)
    if not handoff.exists():
        raise SystemExit(f"未找到: {handoff.relative_to(ROOT)}")

    main_spec = extract_main_spec_from_handoff(handoff)
    prompt = PROMPTS_DIR / f"{version}-dev.prompt.md"
    mf = manifest_path(version)

    print(f"版本: {version}")
    print(f"交接: {read_spec_status(handoff)}")
    if main_spec:
        print(f"主 spec: {read_spec_status(main_spec)}")
    if mf.exists():
        m = json.loads(mf.read_text(encoding="utf-8"))
        print(f"pipeline: {m.get('pipeline_status')}  targets={m.get('target_agents')}")


def main() -> None:
    parser = argparse.ArgumentParser(description="策划→开发 多 Agent 工作流")
    sub = parser.add_subparsers(dest="cmd", required=True)

    sub.add_parser("export").set_defaults(func=cmd_export)

    p_confirm = sub.add_parser("confirm")
    p_confirm.add_argument("version")
    p_confirm.add_argument("--force", action="store_true")
    p_confirm.set_defaults(func=cmd_confirm)

    p_handoff = sub.add_parser("handoff")
    p_handoff.add_argument("version")
    p_handoff.add_argument("--source")
    p_handoff.add_argument("--force", action="store_true")
    p_handoff.add_argument("--verify", nargs="*")
    p_handoff.set_defaults(func=cmd_handoff)

    p_verify = sub.add_parser("verify")
    p_verify.add_argument("version")
    p_verify.add_argument("--verify", nargs="*")
    p_verify.set_defaults(func=cmd_verify)

    p_accept = sub.add_parser("accept")
    p_accept.add_argument("version")
    p_accept.add_argument("--verify", nargs="*")
    p_accept.set_defaults(func=cmd_accept)

    p_close = sub.add_parser("close")
    p_close.add_argument("version")
    p_close.set_defaults(func=cmd_close)

    p_status = sub.add_parser("status")
    p_status.add_argument("version")
    p_status.set_defaults(func=cmd_status)

    args = parser.parse_args()
    args.func(args)


if __name__ == "__main__":
    main()
