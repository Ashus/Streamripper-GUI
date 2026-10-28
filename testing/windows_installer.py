"""Verify silent per-user install, in-place upgrade and uninstall on Windows."""

import argparse
from pathlib import Path
import subprocess
import tempfile
import time
import winreg

from packaging_common import manifest_files


ROOT = Path(__file__).resolve().parents[1]
UNINSTALL_KEY = r"Software\Microsoft\Windows\CurrentVersion\Uninstall\{DDF0F637-697A-4CB8-BB53-6589698B99A2}_is1"


def installed():
    try:
        with winreg.OpenKey(winreg.HKEY_CURRENT_USER, UNINSTALL_KEY):
            return True
    except FileNotFoundError:
        return False


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("installer", type=Path)
    args = parser.parse_args()
    if installed():
        parser.error("Uninstall the existing per-user application before running this isolated test")
    version = (ROOT / "VERSION").read_text(encoding="utf-8").strip()
    with tempfile.TemporaryDirectory(prefix="installer-check-", dir=ROOT / "artifacts") as temporary:
        work = Path(temporary)
        application = work / "application"
        switches = ["/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/SP-", "/NOICONS"]
        try:
            for attempt in ("install", "upgrade"):
                subprocess.run([str(args.installer.resolve()), *switches,
                                f"/DIR={application}", f"/LOG={work / (attempt + '.log')}"],
                               check=True, timeout=120)
                assert installed()
                with winreg.OpenKey(winreg.HKEY_CURRENT_USER, UNINSTALL_KEY) as key:
                    assert winreg.QueryValueEx(key, "DisplayVersion")[0] == version
                    assert winreg.QueryValueEx(key, "InstallLocation")[0].rstrip("\\") == str(application)
                assert not (application / "portable.flag").exists()
                assert (application / "LICENSE").is_file()
                assert not list(application.rglob("BUILDING*.md"))
                manifest_files(application)
                manifest_files(application / "streamripper")
                subprocess.run([str(application / "StreamripperGui.exe"), "--verify-xaml"],
                               check=True, timeout=30)
                if attempt == "install":
                    (application / "unrelated.txt").write_text("Preserve user-owned files", encoding="utf-8")
                assert (application / "unrelated.txt").read_text(encoding="utf-8") == "Preserve user-owned files"
        finally:
            uninstaller = application / "unins000.exe"
            if uninstaller.is_file():
                subprocess.run([str(uninstaller), "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART"],
                               check=True, timeout=120)
                for _ in range(100):
                    if not installed() and not (application / "StreamripperGui.exe").exists():
                        break
                    time.sleep(0.1)
        assert not installed()
        assert not (application / "StreamripperGui.exe").exists()
        assert (application / "unrelated.txt").is_file()
        print("Silent install, upgrade, payload verification and uninstall passed; user-owned file preserved")


if __name__ == "__main__":
    main()
