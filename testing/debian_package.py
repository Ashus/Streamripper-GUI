#!/usr/bin/env python3
"""Validate the GUI DEB payload, permissions, checksums and system-runtime launch."""

import argparse
import hashlib
import io
import os
from pathlib import Path
import subprocess
import struct
import tarfile
import tempfile

ROOT = Path(__file__).resolve().parents[1]


def output(*arguments):
    return subprocess.check_output(list(map(str, arguments)))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("package", type=Path)
    args = parser.parse_args()
    package = args.package.resolve()
    fields = output("dpkg-deb", "--field", package).decode()
    expected = (ROOT / "VERSION").read_text().strip() + "-1"
    assert "Package: streamripper-gui\n" in fields
    assert f"Version: {expected}\n" in fields
    assert "Architecture: amd64\n" in fields
    assert "streamripper-ng (>= 1.68.3)" in fields
    assert "dotnet-runtime-10.0 (>= 10.0.0)" in fields
    assert package.with_suffix(".deb.sha256").read_text().split()[0] == hashlib.sha256(package.read_bytes()).hexdigest()
    control = output("dpkg-deb", "--ctrl-tarfile", package)
    with tarfile.open(fileobj=io.BytesIO(control)) as archive:
        member = next(member for member in archive if member.name.lstrip("./") == "md5sums")
        md5sums = dict((line[34:], line[:32]) for line in archive.extractfile(member).read().decode().splitlines())
    executables = {"usr/bin/streamripper-gui", "usr/lib/streamripper-gui/StreamripperGui", "usr/lib/streamripper-gui/createdump"}
    found = set()
    process = subprocess.Popen(["dpkg-deb", "--fsys-tarfile", str(package)], stdout=subprocess.PIPE)
    try:
        with tarfile.open(fileobj=process.stdout, mode="r|") as archive:
            for member in archive:
                name = member.name.removeprefix("./")
                assert member.uid == member.gid == 0, f"Non-root package ownership: {name}"
                assert member.isdir() or member.isfile(), f"Unexpected payload type: {name}"
                if member.isdir():
                    assert member.mode == 0o755, f"Incorrect directory permissions: {name}"
                    continue
                found.add(name)
                assert member.mode == (0o755 if name in executables else 0o644), f"Incorrect file permissions: {name}"
                assert not name.endswith(".pdb")
                assert Path(name).name not in {"portable.flag", "config.json", "stations.json", "Avalonia.DesignerSupport.dll"}
                payload = archive.extractfile(member).read()
                assert md5sums[name] == hashlib.md5(payload).hexdigest(), name
                assert not name.endswith(".exe"), f"Windows executable in Linux package: {name}"
                if name.endswith(".dll"):
                    # Managed .NET assemblies use PE/CLI on Linux too; reject native Windows DLLs.
                    assert payload[:2] == b"MZ", name
                    pe = struct.unpack_from("<I", payload, 0x3c)[0]
                    assert payload[pe:pe + 4] == bytes((80, 69, 0, 0)), name
                    magic = struct.unpack_from("<H", payload, pe + 24)[0]
                    assert magic in (0x10b, 0x20b), name
                    directories = pe + 24 + (112 if magic == 0x20b else 96)
                    cli_rva, cli_size = struct.unpack_from("<II", payload, directories + 14 * 8)
                    assert cli_rva and cli_size, f"Native Windows DLL in Linux package: {name}"
        assert process.wait() == 0
    finally:
        process.stdout.close()
        if process.poll() is None:
            process.terminate()
            process.wait()
    for name in ["usr/bin/streamripper-gui", "usr/lib/streamripper-gui/StreamripperGui",
                 "usr/lib/streamripper-gui/StreamripperGui.dll",
                 "usr/lib/streamripper-gui/StreamripperGui.runtimeconfig.json",
                 "usr/share/applications/streamripper-gui.desktop", "usr/share/doc/streamripper-gui/THIRD-PARTY-NOTICES.txt"]:
        assert name in found, name
    assert any(name.startswith("usr/share/icons/hicolor/") and name.endswith(".png") for name in found)
    assert "usr/bin/streamripper" not in found
    for name in ("libcoreclr.so", "libhostfxr.so", "libhostpolicy.so", "System.Private.CoreLib.dll"):
        assert f"usr/lib/streamripper-gui/{name}" not in found, f"Bundled runtime file: {name}"
    assert len(md5sums) == len(found)
    with tempfile.TemporaryDirectory(prefix="deb-check-", dir=ROOT / "artifacts/linux") as temporary:
        root = Path(temporary)
        subprocess.run(["dpkg-deb", "--extract", str(package), str(root)], check=True)
        application = root / "usr/lib/streamripper-gui"
        environment = os.environ.copy()
        environment.update(DOTNET_ROOT="/usr/share/dotnet", DOTNET_MULTILEVEL_LOOKUP="0",
                           LD_LIBRARY_PATH=str(application), PATH="/usr/bin:/bin")
        subprocess.run([str(application / "StreamripperGui"), "--verify-xaml"], env=environment, check=True)
        subprocess.run(["desktop-file-validate", str(root / "usr/share/applications/streamripper-gui.desktop")], check=True)
        for library in application.glob("*.so"):
            result = subprocess.run(["ldd", str(library)], env=environment, capture_output=True, text=True)
            assert result.returncode == 0 and "not found" not in result.stdout, f"{library.name}: {result.stdout} {result.stderr}"
    print(f"DEB validation passed: {len(found)} payload files; package and native dependencies verified")


if __name__ == "__main__":
    main()
