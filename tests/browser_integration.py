"""Real Edge network test. Does not change Windows proxy settings or browser policy.

Run baseline before enabling EdgeDirect; run protected-live/protected-dead after.
Each run uses a fresh disposable profile, never the user's normal browser profile.
"""
import argparse
import http.server
import json
import os
from pathlib import Path
import subprocess
import threading
import uuid
import winreg

ROOT = Path(__file__).resolve().parents[1]
RUNTIME = ROOT / "tests" / "runtime"
EDGE = Path(os.environ["ProgramFiles(x86)"]) / "Microsoft/Edge/Application/msedge.exe"
TOKEN = "EDGE_DIRECT_OK_" + uuid.uuid4().hex


def value_snapshot(root, path):
    try:
        with winreg.OpenKey(root, path) as key:
            result = []
            for i in range(winreg.QueryInfoKey(key)[1]):
                name, value, kind = winreg.EnumValue(key, i)
                result.append((name, repr(value), kind))
            return sorted(result)
    except FileNotFoundError:
        return None


class Handler(http.server.BaseHTTPRequestHandler):
    def do_GET(self):
        self.server.requests.append(self.path)
        body = ("<html><body>" + self.server.marker + "</body></html>").encode()
        self.send_response(200)
        self.send_header("Content-Type", "text/html; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        try:
            self.wfile.write(body)
        except (BrokenPipeError, ConnectionResetError):
            pass

    def do_CONNECT(self):
        self.server.requests.append(self.path)
        try:
            self.send_error(502)
        except (BrokenPipeError, ConnectionResetError, ConnectionAbortedError):
            pass

    def log_message(self, *args):
        pass


def serve(marker):
    server = http.server.ThreadingHTTPServer(("127.0.0.1", 0), Handler)
    server.daemon_threads = True
    server.marker = marker
    server.requests = []
    threading.Thread(target=server.serve_forever, daemon=True).start()
    return server


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("mode", choices=["baseline", "protected-live", "protected-dead"])
    args = parser.parse_args()
    RUNTIME.mkdir(parents=True, exist_ok=True)
    profile = RUNTIME / (args.mode + "-" + uuid.uuid4().hex)
    system_before = value_snapshot(winreg.HKEY_CURRENT_USER, r"Software\Microsoft\Windows\CurrentVersion\Internet Settings")
    chrome_before = value_snapshot(winreg.HKEY_CURRENT_USER, r"Software\Policies\Google\Chrome")
    target = serve(TOKEN)
    proxy = serve("PROXY_WAS_USED")
    proxy_port = proxy.server_port
    if args.mode == "protected-dead":
        proxy.shutdown()
        proxy.server_close()
    url = f"http://127.0.0.1:{target.server_port}/direct-proof"
    command = [str(EDGE), "--headless=new", "--no-first-run", "--no-default-browser-check",
               "--disable-background-networking", "--disable-component-update", "--disable-sync",
               "--disable-extensions", "--disable-quic", "--disable-breakpad",
               "--user-data-dir=" + str(profile), "--proxy-server=http://127.0.0.1:" + str(proxy_port),
               "--proxy-bypass-list=<-loopback>", "--dump-dom", url]
    try:
        result = subprocess.run(command, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                                timeout=45, creationflags=subprocess.CREATE_NO_WINDOW)
        dom = result.stdout.decode("utf-8", errors="replace")
        (RUNTIME / (args.mode + ".html")).write_text(dom, encoding="utf-8")
        (RUNTIME / (args.mode + ".stderr.txt")).write_bytes(result.stderr)
        proxy_used = any("/direct-proof" in path for path in proxy.requests)
        direct_used = any("/direct-proof" in path for path in target.requests)
        if args.mode == "baseline":
            passed = proxy_used and not direct_used and "PROXY_WAS_USED" in dom
        else:
            passed = direct_used and not proxy_used and TOKEN in dom
        unchanged = system_before == value_snapshot(winreg.HKEY_CURRENT_USER, r"Software\Microsoft\Windows\CurrentVersion\Internet Settings") and chrome_before == value_snapshot(winreg.HKEY_CURRENT_USER, r"Software\Policies\Google\Chrome")
        report = {"mode": args.mode, "passed": passed and unchanged, "exit_code": result.returncode,
                  "direct_target_requested": direct_used, "target_requested_through_proxy": proxy_used,
                  "proxy_request_count": len(proxy.requests), "system_and_chrome_unchanged": unchanged,
                  "profile": str(profile), "configured_proxy": "127.0.0.1:" + str(proxy_port)}
        (RUNTIME / (args.mode + ".json")).write_text(json.dumps(report, indent=2), encoding="utf-8")
        print(json.dumps(report, indent=2))
        if not report["passed"]:
            raise SystemExit(1)
    finally:
        target.shutdown()
        target.server_close()
        if args.mode != "protected-dead":
            proxy.shutdown()
            proxy.server_close()


if __name__ == "__main__":
    main()
