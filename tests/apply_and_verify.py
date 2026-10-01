"""Explicitly enables Edge direct mode, exercises restore, and leaves it enabled.

Requires --apply. Changes only HKCU Edge ProxySettings and EdgeDirect's backup.
It checks real registry state and preserves unrelated Windows/Chrome settings.
"""
import argparse
import json
import subprocess
import winreg
from pathlib import Path
from browser_integration import ROOT, value_snapshot


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--apply", action="store_true", required=True)
    parser.parse_args()
    artifact_dir = ROOT / "artifacts"
    artifact_dir.mkdir(exist_ok=True)
    watched = [(winreg.HKEY_CURRENT_USER, r"Software\Microsoft\Windows\CurrentVersion\Internet Settings"),
               (winreg.HKEY_CURRENT_USER, r"Software\Policies\Google\Chrome"),
               (winreg.HKEY_LOCAL_MACHINE, r"Software\Policies\Google\Chrome"),
               (winreg.HKEY_LOCAL_MACHINE, r"Software\Policies\Microsoft\Edge")]
    before = [value_snapshot(root, key) for root, key in watched]
    policy_key = r"Software\Policies\Microsoft\Edge"

    def proxy_value():
        try:
            with winreg.OpenKey(winreg.HKEY_CURRENT_USER, policy_key) as key:
                return winreg.QueryValueEx(key, "ProxySettings")
        except FileNotFoundError:
            return None

    def run(option):
        output = artifact_dir / (option[2:] + ".txt")
        result = subprocess.run([str(ROOT / "EdgeDirect.exe"), option, "--result", str(output)],
                                timeout=300, creationflags=subprocess.CREATE_NO_WINDOW)
        if result.returncode:
            raise RuntimeError(output.read_text(encoding="utf-8"))
        return output.read_text(encoding="utf-8")

    initial = proxy_value()
    initial_status = json.loads(run("--status"))
    if initial_status["backupPresent"] and not initial_status["directConfigured"]:
        run("--restore")  # Recover a previous attempt that stopped before writing.
        initial_status = json.loads(run("--status"))
    if initial_status["backupPresent"] or initial_status["directConfigured"]:
        raise RuntimeError("This one-time round-trip check requires a fresh installation.")
    if initial_status["conflict"]:
        raise RuntimeError(initial_status["conflict"])
    run("--enable")
    assert proxy_value() == ('{"ProxyMode":"direct"}', winreg.REG_SZ)
    backup_path = Path(json.loads(run("--status"))["backupPath"])
    first_backup = backup_path.read_bytes()
    run("--enable")
    assert backup_path.read_bytes() == first_backup, "Repeated enable replaced original backup"
    run("--restore")
    assert proxy_value() == initial, "Restore changed original value"
    assert not backup_path.exists(), "Restore did not finish"
    run("--enable")
    assert proxy_value() == ('{"ProxyMode":"direct"}', winreg.REG_SZ)
    assert before == [value_snapshot(root, key) for root, key in watched], "An unrelated setting changed"
    report = {"passed": True, "actual_registry_round_trip": True, "repeated_enable_preserves_backup": True,
              "system_chrome_machine_policies_unchanged": True, "final_state": "Edge direct enabled", "backup_present": backup_path.exists()}
    (artifact_dir / "registry-verification.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
