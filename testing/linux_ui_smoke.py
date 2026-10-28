#!/usr/bin/env python3
"""Exercise the installed Linux GUI against local ICY MP3 streams in Xvfb."""

import argparse
import http.server
import json
import os
import signal
from pathlib import Path
import subprocess
import tempfile
import threading
import time
import urllib.parse

ROOT = Path(__file__).resolve().parents[1]


def wait_until(predicate, message, timeout=30):
    end = time.monotonic() + timeout
    while time.monotonic() < end:
        if predicate():
            return
        time.sleep(0.2)
    raise AssertionError(message)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--sample", type=Path, required=True, help="An existing valid MP3 fixture; never downloaded by this test")
    parser.add_argument("--executable", default="/usr/bin/streamripper-gui")
    args = parser.parse_args()
    sample = args.sample.read_bytes()
    if len(sample) < 16000:
        parser.error("The MP3 sample must contain at least 16000 bytes")
    work = Path(tempfile.mkdtemp(prefix="gui-smoke-", dir=ROOT / "artifacts/linux"))
    environment = os.environ.copy()
    display = next(number for number in range(93, 113) if not Path(f"/tmp/.X11-unix/X{number}").exists())
    environment.update(DISPLAY=f":{display}", XDG_CONFIG_HOME=str(work / "config"),
                       XDG_DATA_HOME=str(work / "data"), XDG_CACHE_HOME=str(work / "cache"))
    environment.pop("WAYLAND_DISPLAY", None)
    environment.pop("DOTNET_ROOT", None)
    recordings = work / "recordings"
    config = work / "config/StreamripperGui"
    config.mkdir(parents=True)
    (config / "rules").mkdir()
    applications = work / "data/applications"
    applications.mkdir(parents=True)
    folder_event = work / "opened-folder.txt"
    handler = work / "folder-handler.py"
    handler.write_text("import sys\nfrom pathlib import Path\n" +
                       f"Path({str(folder_event)!r}).write_text(sys.argv[1])\n")
    (applications / "smoke-folder.desktop").write_text(
        "[Desktop Entry]\nType=Application\nName=Isolated folder handler\n"
        f"Exec=/usr/bin/python3 {handler} %u\nMimeType=inode/directory;\nNoDisplay=true\n")
    rules = config / "rules/test.txt"
    rules.write_text("m/(.*) - (.*)/A1T2\n")

    class Radio(http.server.BaseHTTPRequestHandler):
        def log_message(self, *unused):
            pass

        def do_GET(self):
            self.send_response(200)
            for key, value in {"Content-Type": "audio/mpeg", "icy-name": "Local test radio",
                               "icy-metaint": "16000", "icy-br": "128"}.items():
                self.send_header(key, value)
            self.end_headers()
            index = 0
            audio = (sample * 2)[:16000]
            try:
                while True:
                    title = ("Artist - Song", "Artist - Advert", "Artist - Another song")[index // 3 % 3]
                    metadata = f"StreamTitle='{title}';".encode()
                    blocks = (len(metadata) + 15) // 16
                    self.wfile.write(audio + bytes([blocks]) + metadata.ljust(blocks * 16, b"\0"))
                    self.wfile.flush()
                    index += 1
                    time.sleep(1)
            except (BrokenPipeError, ConnectionResetError):
                pass

    server = http.server.ThreadingHTTPServer(("127.0.0.1", 0), Radio)
    threading.Thread(target=server.serve_forever, daemon=True).start()
    port = server.server_address[1]
    (config / "config.json").write_text(json.dumps({"schemaVersion": 1,
        "application": {"language": "en", "theme": "light", "streamripperPath": "/usr/bin/streamripper"},
        "recording": {"directory": str(recordings), "defaults": {"incompleteMinDurationSeconds": 0,
                      "keepIncomplete": True, "skipInitialTracks": 0, "recordingMode": "both"}, "profiles": []},
        "metadataRuleSets": [{"id": "test", "name": "Test", "file": "rules/test.txt"}]}))
    (config / "stations.json").write_text(json.dumps({"schemaVersion": 1, "stations": [
        {"id": "one", "name": "Local One", "url": f"http://127.0.0.1:{port}/one", "metadataRuleSetId": "test"},
        {"id": "two", "name": "Local Two", "url": f"http://127.0.0.1:{port}/two"}]}))
    processes = []
    logs = []

    def start(name, arguments):
        log = open(work / f"{name}.log", "w")
        logs.append(log)
        process = subprocess.Popen(arguments, env=environment, stdout=log, stderr=subprocess.STDOUT, start_new_session=True)
        processes.append(process)
        return process

    def command(*arguments, check=True):
        return subprocess.run(arguments, env=environment, check=check, capture_output=True, text=True, timeout=15)

    def window_id(process):
        result = command("xdotool", "search", "--onlyvisible", "--pid", str(process.pid), check=False)
        return result.stdout.strip().splitlines()[0] if result.returncode == 0 else None

    def screenshot(name):
        command("scrot", str(work / f"{name}.png"))

    try:
        command("xdg-mime", "default", "smoke-folder.desktop", "inode/directory")
        start("xvfb", ["Xvfb", f":{display}", "-screen", "0", "1600x1000x24", "-nolisten", "tcp", "-pn"])
        wait_until(lambda: command("xdotool", "getdisplaygeometry", check=False).returncode == 0, "Xvfb did not start")
        start("window-manager", ["openbox", "--sm-disable"])
        time.sleep(1)
        gui = start("gui", [args.executable])
        wait_until(lambda: window_id(gui), "The GUI did not open", timeout=60)
        window = window_id(gui)
        command("xdotool", "windowsize", window, "1130", "840")
        command("xdotool", "windowmove", window, "20", "20")
        command("xdotool", "windowactivate", "--sync", window)
        time.sleep(1)
        def click(x, y):
            command("xdotool", "mousemove", "--window", window, str(x), str(y), "click", "1")
            time.sleep(0.3)

        def console_text():
            click(500, 620)
            command("xdotool", "key", "ctrl+a", "ctrl+c")
            text = command("xclip", "-selection", "clipboard", "-o").stdout
            command("xdotool", "key", "Escape")
            return text

        screenshot("startup")
        click(376, 125)
        first = recordings / "Local One/incomplete/metadata.log"
        second = recordings / "Local Two/incomplete/metadata.log"
        wait_until(first.exists, "The first recording did not produce metadata")
        screenshot("first-recording")
        click(40, 165)
        click(376, 125)
        wait_until(second.exists, "The second recording did not produce metadata")
        screenshot("concurrent")
        click(40, 112)
        rules.write_text("m/Advert/x\nm/(.*) - (.*)/A1T2\n")
        time.sleep(8)
        transcript = console_text()
        (work / "reload-console.txt").write_text(transcript)
        assert "reload" in transcript.lower(), "The GUI console did not report native rule reload"
        click(435, 125)
        wait_until(lambda: not (recordings / "Local One/incomplete/.lock").exists(), "The first recorder did not stop")
        before = second.stat().st_size
        wait_until(lambda: second.stat().st_size > before, "Stopping one recorder also stopped the other")
        click(40, 165)
        click(376, 125)
        wait_until(lambda: (recordings / "Local One/incomplete/.lock").exists(), "Recording did not restart")
        command("xdotool", "windowactivate", "--sync", window)
        command("xdotool", "key", "alt+F4")
        assert gui.wait(timeout=25) == 0, "Closing the GUI failed"
        assert not list(recordings.rglob(".lock")), "Close-all left a recorder lock"
        assert list((recordings / "Local One").rglob("*.mp3")), "First recording produced no audio"
        assert list((recordings / "Local Two").rglob("*.mp3")), "Second recording produced no audio"
        stations = json.loads((config / "stations.json").read_text())["stations"]
        assert all(station.get("lastObservedStream", {}).get("audioFormat") == "MP3" for station in stations)
        reopened = start("reopened", [args.executable])
        wait_until(lambda: window_id(reopened), "The GUI did not reopen")
        second_instance = start("activation", [args.executable])
        assert second_instance.wait(timeout=10) == 0, "Second-instance activation did not exit"
        assert reopened.poll() is None, "Activation terminated the owning instance"
        window = window_id(reopened)
        click(1078, 196)
        wait_until(folder_event.exists, "The folder button did not invoke the desktop file-manager association")
        opened = folder_event.read_text()
        opened = urllib.parse.unquote(urllib.parse.urlsplit(opened).path) if opened.startswith("file:") else opened
        assert Path(opened) == recordings / "Local One", opened
        screenshot("reopened")
        command("xdotool", "windowactivate", "--sync", window_id(reopened))
        command("xdotool", "key", "alt+F4")
        assert reopened.wait(timeout=15) == 0
        (work / "result.json").write_text(json.dumps({"passed": True, "checks": [
            "installed framework-dependent startup", "concurrent ICY MP3 recordings", "native rule reload",
            "independent graceful stop", "recording restart", "close-all finalization",
            "format persistence", "restart", "single-instance activation", "desktop folder association"]}, indent=2))
        print(f"Linux GUI smoke passed; evidence: {work}")
    finally:
        try:
            screenshot("final")
        except subprocess.SubprocessError:
            pass
        for process in reversed(processes):
            # Each test child owns its session; include recorders if an assertion interrupted the GUI.
            try:
                os.killpg(process.pid, signal.SIGTERM)
            except ProcessLookupError:
                pass
            if process.poll() is None:
                try:
                    process.wait(timeout=5)
                except subprocess.TimeoutExpired:
                    os.killpg(process.pid, signal.SIGKILL)
                    process.wait()
        for log in logs:
            log.close()
        server.shutdown()
        server.server_close()


if __name__ == "__main__":
    main()
