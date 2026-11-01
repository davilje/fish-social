#!/usr/bin/env python3
"""Serve the unified preview on macOS and Windows using only Python 3."""
import argparse
import functools
import hashlib
import json
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
import threading
from urllib.request import urlopen
import webbrowser

ROOT = Path(__file__).resolve().parent.parent
IDENTITY = hashlib.sha256(str(ROOT).encode()).hexdigest()
HEALTH = "/__fish_preview_health__"


class PreviewHandler(SimpleHTTPRequestHandler):
    def end_headers(self):
        self.send_header("Cache-Control", "no-store")
        super().end_headers()

    def do_GET(self):
        if self.path.split("?", 1)[0] == HEALTH:
            body = json.dumps({"project": "fish-social-preview", "root": IDENTITY}).encode()
            self.send_response(200)
            self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)
            return
        super().do_GET()

    def log_message(self, fmt, *args):
        if len(args) > 1 and str(args[1]) in ("200", "304"):
            return
        super().log_message(fmt, *args)


def is_our_preview(port):
    try:
        with urlopen(f"http://127.0.0.1:{port}{HEALTH}", timeout=1) as response:
            data = json.load(response)
        return data.get("project") == "fish-social-preview" and data.get("root") == IDENTITY
    except (OSError, ValueError, AttributeError):
        return False


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--port", type=int, default=8772)
    parser.add_argument("--no-browser", action="store_true")
    args = parser.parse_args()
    if not 1 <= args.port <= 65535:
        parser.error("port must be between 1 and 65535")
    if not (ROOT / "现行预览.html").is_file():
        parser.error("现行预览.html is missing from the project root")
    handler = functools.partial(PreviewHandler, directory=str(ROOT))
    servers = []
    active_port = None
    # Keep historical ports usable without stopping or reusing another app.
    candidates = list(dict.fromkeys([args.port, 8769, 8772, *range(8773, 8783)]))
    for port in candidates:
        if is_our_preview(port):
            active_port = port
            break
        try:
            servers.append(ThreadingHTTPServer(("127.0.0.1", port), handler))
            active_port = port
            break
        except OSError:
            print(f"Port {port} is occupied; trying another port.")
    if active_port is None:
        parser.exit(1, "No preview port is available.\n")
    if servers:
        for port in (8769, 8772):
            if port == active_port or is_our_preview(port):
                continue
            try:
                servers.append(ThreadingHTTPServer(("127.0.0.1", port), handler))
            except OSError:
                print(f"Legacy port {port} is occupied; use the URL below.")
        for server in servers:
            threading.Thread(target=server.serve_forever, daemon=True).start()
    url = f"http://127.0.0.1:{active_port}/preview-current.html"
    print(f"Preview: {url}", flush=True)
    if not args.no_browser:
        webbrowser.open(url)
    if not servers:
        print("Reusing the preview already running for this project.")
        return
    print("Keep this window open. Press Ctrl+C to stop the preview.", flush=True)
    try:
        threading.Event().wait()
    except KeyboardInterrupt:
        pass
    finally:
        for server in servers:
            server.shutdown()
            server.server_close()


if __name__ == "__main__":
    main()
