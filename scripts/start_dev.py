#!/usr/bin/env python3
"""
Fish Social 本地开发一键启动

  python scripts/start_dev.py              # 前台运行，Ctrl+C 停止
  python scripts/start_dev.py --wait-key   # 启动后按任意键关闭（dev.bat 默认）
  python scripts/start_dev.py --check      # 仅检查端口
"""

from __future__ import annotations

import argparse
import atexit
import os
import shutil
import signal
import subprocess
import sys
import threading
import time
import urllib.error
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent

_proc: subprocess.Popen[object] | None = None
_server_only = False
_shutdown_done = False


def load_dotenv() -> dict[str, str]:
    merged = dict(os.environ)
    env_path = ROOT / ".env"
    if not env_path.is_file():
        return merged
    for raw in env_path.read_text(encoding="utf-8").splitlines():
        line = raw.strip()
        if not line or line.startswith("#") or "=" not in line:
            continue
        key, _, value = line.partition("=")
        key, value = key.strip(), value.strip().strip('"').strip("'")
        if key and key not in merged:
            merged[key] = value
    return merged


def apply_dev_auth_defaults(env: dict[str, str]) -> list[str]:
    """
    Make local dev startup resilient when auth env vars are absent.

    Server now requires JWT auth config by default. For local one-click startup,
    auto-enable development auth bypass only when safe:
    - NODE_ENV is missing -> set development
    - JWT_SECRET missing and AUTH_DISABLED missing under development -> set AUTH_DISABLED=1
    """
    notes: list[str] = []
    node_env = env.get("NODE_ENV")
    if not node_env:
        env["NODE_ENV"] = "development"
        notes.append("NODE_ENV 未设置，已自动使用 development")
        node_env = "development"

    if env.get("JWT_SECRET"):
        return notes

    if env.get("AUTH_DISABLED") == "1":
        notes.append("检测到 AUTH_DISABLED=1，开发模式将跳过 JWT 校验")
        return notes

    if node_env == "development":
        env["AUTH_DISABLED"] = "1"
        notes.append("未检测到 JWT_SECRET，已自动启用 AUTH_DISABLED=1（仅本次开发启动）")
    else:
        notes.append(
            "JWT_SECRET 未设置，且当前不是 development；服务端可能启动失败，请设置 JWT_SECRET"
        )
    return notes


def get_ports(env: dict[str, str]) -> tuple[int, int]:
    return int(env.get("PORT", "3001")), int(env.get("EXPO_WEB_PORT", "8082"))


def require_tool(name: str) -> None:
    if shutil.which(name) is None:
        print(f"错误: 未找到 {name}", file=sys.stderr)
        sys.exit(1)


def run_ports_mjs(command: str, *, force: bool = False, server_only: bool = False, quiet: bool = False) -> int:
    cmd = ["node", "scripts/ports.mjs", command]
    if force:
        cmd.append("--force")
    if server_only:
        cmd.append("--server-only")
    if quiet:
        cmd.append("--quiet")
    if not quiet:
        print(f">>> {' '.join(cmd)}")
    return subprocess.run(cmd, cwd=ROOT, env=os.environ.copy()).returncode


def shutdown_all(*, quiet: bool = False) -> None:
    global _shutdown_done
    if _shutdown_done:
        return
    _shutdown_done = True

    proc = _proc
    if proc is not None and proc.poll() is None:
        if not quiet:
            print("\n>>> 正在停止服务…")
        proc.terminate()
        try:
            proc.wait(timeout=8)
        except subprocess.TimeoutExpired:
            proc.kill()
            proc.wait(timeout=3)

    if not quiet:
        print(">>> 正在释放端口…")
    run_ports_mjs("free", force=True, server_only=_server_only, quiet=quiet)


def _install_windows_console_handler() -> None:
    if sys.platform != "win32":
        return
    import ctypes

    @ctypes.WINFUNCTYPE(ctypes.c_bool, ctypes.c_uint)
    def handler(ctrl_type: int) -> bool:
        if ctrl_type in (0, 1, 2, 5, 6):
            shutdown_all(quiet=ctrl_type == 2)
        return True

    ctypes.windll.kernel32.SetConsoleCtrlHandler(handler, True)


def wait_for_http(url: str, timeout_sec: float = 180.0) -> bool:
    deadline = time.time() + timeout_sec
    while time.time() < deadline:
        try:
            with urllib.request.urlopen(url, timeout=2) as resp:
                if resp.status < 500:
                    return True
        except (urllib.error.URLError, OSError, TimeoutError):
            pass
        time.sleep(1.5)
    return False


def open_browser(url: str) -> None:
    try:
        import webbrowser
        webbrowser.open(url)
    except Exception as exc:  # noqa: BLE001
        print(f"无法打开浏览器: {exc}")


def launch_npm(command: str, env: dict[str, str]) -> subprocess.Popen[object]:
    shell = sys.platform == "win32"
    npm = "npm.cmd" if shell and shutil.which("npm.cmd") else "npm"
    cmd = f"{npm} run {command}" if shell else [npm, "run", command]
    print(f">>> {cmd if isinstance(cmd, str) else ' '.join(cmd)}\n")
    return subprocess.Popen(cmd, cwd=ROOT, env=env, shell=shell)  # noqa: S603


def wait_for_any_key(prompt: str) -> None:
    print(prompt)
    if sys.platform == "win32":
        import msvcrt
        msvcrt.getch()
    else:
        input()


def run_wait_key_mode(npm_script: str, env: dict[str, str], web_url: str, server_port: int, open_browser_flag: bool) -> int:
    global _proc
    _proc = launch_npm(npm_script, env)

    print(f">>> 等待服务就绪（API :{server_port}，Web {web_url}）…")
    api_ok = wait_for_http(f"http://localhost:{server_port}/health", timeout_sec=120)
    web_ok = wait_for_http(web_url, timeout_sec=120) if npm_script == "dev" else True

    if api_ok and web_ok:
        print(f"\n[OK] 服务已启动")
        print(f"  API:  http://localhost:{server_port}")
        if npm_script == "dev":
            print(f"  Web:  {web_url}")
        if open_browser_flag and npm_script == "dev":
            open_browser(web_url)
    else:
        print("\n[WARN] 部分服务未在预期时间内就绪，仍可手动访问上述地址。", file=sys.stderr)

    wait_for_any_key("\n按任意键关闭服务并释放端口…")
    shutdown_all()
    return 0


def run_foreground_mode(npm_script: str, env: dict[str, str]) -> int:
    global _proc
    _proc = launch_npm(npm_script, env)
    print(">>> 按 Ctrl+C 停止（关闭窗口也会释放端口）\n")

    def handle_stop(_signum: int, _frame: object | None) -> None:
        shutdown_all()
        sys.exit(0)

    signal.signal(signal.SIGINT, handle_stop)
    signal.signal(signal.SIGTERM, handle_stop)

    try:
        return _proc.wait()
    finally:
        shutdown_all(quiet=True)


def main() -> None:
    global _server_only

    parser = argparse.ArgumentParser(description="Fish Social 本地开发一键启动")
    parser.add_argument("--check", action="store_true", help="仅检查端口")
    parser.add_argument("--no-free", action="store_true", help="启动前不释放端口")
    parser.add_argument("--server-only", action="store_true", help="仅启动 API")
    parser.add_argument("--no-browser", action="store_true", help="不自动打开浏览器")
    parser.add_argument(
        "--wait-key",
        action="store_true",
        help="服务就绪后按任意键关闭并释放端口（dev.bat 默认）",
    )
    args = parser.parse_args()
    _server_only = args.server_only

    require_tool("node")
    require_tool("npm")
    _install_windows_console_handler()
    atexit.register(lambda: shutdown_all(quiet=True))

    env = load_dotenv()
    auth_notes = apply_dev_auth_defaults(env)
    os.environ.update({k: v for k, v in env.items() if k not in os.environ})
    server_port, web_port = get_ports(env)
    for note in auth_notes:
        print(f">>> [dev-env] {note}")

    if args.check:
        sys.exit(run_ports_mjs("check", server_only=args.server_only))

    if not args.no_free:
        code = run_ports_mjs("free", force=True, server_only=args.server_only, quiet=True)
        if code != 0:
            sys.exit(code)
        time.sleep(0.8)

    web_url = f"http://localhost:{web_port}"
    npm_script = "server" if args.server_only else "dev"

    if not args.server_only and not args.no_browser and not args.wait_key:
        threading.Thread(
            target=lambda: wait_for_http(web_url) and open_browser(web_url),
            daemon=True,
        ).start()

    if args.wait_key:
        code = run_wait_key_mode(
            npm_script, env, web_url, server_port, open_browser_flag=not args.no_browser
        )
    else:
        code = run_foreground_mode(npm_script, env)

    if code != 0:
        print(f"\n启动失败 (exit {code})", file=sys.stderr)
    sys.exit(code)


if __name__ == "__main__":
    main()
