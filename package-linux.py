#!/usr/bin/env python3
"""Build a framework-dependent Debian 13 amd64 package on Linux, including under WSL."""

import argparse
import hashlib
import os
from pathlib import Path
import platform
import re
import shutil
import struct
import subprocess
import tempfile
from packaging_common import copy_notices, copy_readme_images


ROOT = Path(__file__).resolve().parent
MIN_STREAMRIPPER = "1.68.3"


def run(arguments, environment, **kwargs):
    print("+", *map(str, arguments), flush=True)
    return subprocess.run(list(map(str, arguments)), cwd=ROOT, env=environment, check=True, **kwargs)


def install_icon(destination):
    data = (ROOT / "assets/radio.ico").read_bytes()
    reserved, kind, count = struct.unpack_from("<HHH", data)
    if reserved != 0 or kind != 1:
        raise RuntimeError("Invalid application icon")
    images = []
    for index in range(count):
        size, offset = struct.unpack_from("<II", data, 6 + index * 16 + 8)
        image = data[offset:offset + size]
        if image.startswith(b"\x89PNG\r\n\x1a\n"):
            width, height = struct.unpack_from(">II", image, 16)
            images.append((width * height, width, height, image))
    if not images:
        raise RuntimeError("The ICO must contain an embedded PNG for Linux desktop integration")
    _, width, height, image = max(images)
    target = destination / f"usr/share/icons/hicolor/{width}x{height}/apps/streamripper-gui.png"
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_bytes(image)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dotnet", default="dotnet", help="Path to the .NET 10 SDK executable")
    parser.add_argument("--output-dir", type=Path, default=ROOT / "artifacts/packages")
    parser.add_argument("--skip-tests", action="store_true", help="Use only after testing this exact checkout")
    args = parser.parse_args()
    if platform.system() != "Linux" or platform.machine() not in ("x86_64", "amd64"):
        parser.error("Build this package on Debian amd64, including WSL; do not cross-package on Windows")
    distribution = dict(line.split("=", 1) for line in Path("/etc/os-release").read_text().splitlines() if "=" in line)
    if distribution.get("ID", "").strip('"') != "debian" or distribution.get("VERSION_ID", "").strip('"') != "13":
        parser.error("This package targets Debian 13; build and validate it in that environment")
    version = (ROOT / "VERSION").read_text(encoding="utf-8").strip()
    if not re.fullmatch(r"\d+(?:\.\d+){1,3}", version):
        parser.error("VERSION must contain a numeric application version")
    dotnet = shutil.which(args.dotnet)
    if dotnet is None:
        parser.error(".NET 10 SDK was not found; use --dotnet /path/to/dotnet")
    artifacts = ROOT / "artifacts/linux"
    packages = artifacts / "nuget"
    environment = os.environ.copy()
    environment.update({
        "PATH": str(Path(dotnet).parent) + os.pathsep + environment.get("PATH", ""),
        "DOTNET_CLI_HOME": str(artifacts / "dotnet-home"),
        "NUGET_PACKAGES": str(packages),
        "DOTNET_CLI_TELEMETRY_OPTOUT": "1",
        "DOTNET_SKIP_FIRST_TIME_EXPERIENCE": "1",
    })
    properties = [f"-p:ArtifactsPath={artifacts / 'managed'}"]
    if not args.skip_tests:
        run([dotnet, "test", ROOT / "Streamripper-GUI.slnx", "-c", "Release", *properties], environment)
    publish = artifacts / "managed/publish/StreamripperGui.Desktop/release_linux-x64"
    # Remove only this generated publish directory; old self-contained files must not survive.
    if not publish.resolve().is_relative_to(artifacts.resolve()):
        raise RuntimeError("Publish directory escapes the Linux artifacts directory")
    if publish.exists():
        shutil.rmtree(publish)
    run([dotnet, "publish", ROOT / "src/StreamripperGui.Desktop", "-c", "Release", "-r", "linux-x64",
         "--self-contained", "false", "-p:PublishTrimmed=false", *properties], environment)
    run([publish / "StreamripperGui", "--verify-xaml"], environment)
    args.output_dir = args.output_dir.resolve()
    args.output_dir.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="deb-", dir=artifacts) as temporary:
        work = Path(temporary)
        stage = work / "root"
        application = stage / "usr/lib/streamripper-gui"
        shutil.copytree(publish, application)
        for symbol in application.rglob("*.pdb"):
            symbol.unlink()
        (application / "Avalonia.DesignerSupport.dll").unlink(missing_ok=True)
        for relative in ("usr/bin", "usr/share/applications", "usr/share/doc/streamripper-gui", "DEBIAN"):
            (stage / relative).mkdir(parents=True, exist_ok=True)
        # Windows checkouts may use CRLF; Linux launchers and desktop files require LF.
        for source, target in (("streamripper-gui", "usr/bin/streamripper-gui"),
                               ("streamripper-gui.desktop", "usr/share/applications/streamripper-gui.desktop")):
            (stage / target).write_text((ROOT / "packaging/linux" / source).read_text(encoding="utf-8"),
                                       encoding="utf-8", newline="\n")
        install_icon(stage)
        documentation = stage / "usr/share/doc/streamripper-gui"
        for name in ("README.md", "LICENSE"):
            shutil.copyfile(ROOT / name, documentation / name)
        copy_readme_images(ROOT, documentation)
        copy_notices(packages, publish / "StreamripperGui.deps.json", documentation)
        (documentation / "copyright").write_text(
            "Streamripper GUI\nAuthor: Ashus\nSource: https://github.com/Ashus/Streamripper-GUI\n"
            "See LICENSE for the GNU General Public License version 3.\n"
            "Bundled dependency licenses and notices are in THIRD-PARTY-NOTICES.txt and licenses/.\n"
            "Streamripper is a separate package with its own copyright and license files.\n", encoding="utf-8")
        # DrvFS permissions do not describe a Debian package; set every payload mode explicitly.
        stage.chmod(0o755)
        for path in stage.rglob("*"):
            path.chmod(0o755 if path.is_dir() else 0o644)
        for executable in (application / "StreamripperGui", application / "createdump", stage / "usr/bin/streamripper-gui"):
            if executable.exists():
                executable.chmod(0o755)
        installed_size = (sum(path.stat().st_size for path in stage.rglob("*") if path.is_file()) + 1023) // 1024
        dependencies = [f"streamripper-ng (>= {MIN_STREAMRIPPER})", "dotnet-runtime-10.0 (>= 10.0.0)", "libc6 (>= 2.38)",
                        "libgcc-s1", "libstdc++6",
                        "libglib2.0-0t64", "libx11-6", "libice6", "libsm6", "libfontconfig1", "libxcb1",
                        "libxrandr2", "libxi6", "libxcursor1", "libxrender1", "libxext6", "fonts-dejavu-core", "xdg-utils"]
        (stage / "DEBIAN/control").write_text(
            f"Package: streamripper-gui\nVersion: {version}-1\nArchitecture: amd64\nSection: sound\nPriority: optional\n"
            f"Maintainer: Ashus <github.com@ashus.net>\nInstalled-Size: {installed_size}\n"
            f"Depends: {', '.join(dependencies)}\nHomepage: https://github.com/Ashus/Streamripper-GUI\n"
            "Description: Internet radio recorder with station profiles and metadata filters\n"
            " Avalonia desktop interface for the Ashus Streamripper fork.\n"
            " Uses the system .NET 10 runtime and the compatible Streamripper package.\n", encoding="utf-8")
        checksums = []
        for path in sorted((stage / "usr").rglob("*")):
            if path.is_file():
                checksums.append(f"{hashlib.md5(path.read_bytes()).hexdigest()}  {path.relative_to(stage)}")
        (stage / "DEBIAN/md5sums").write_text("\n".join(checksums) + "\n", encoding="utf-8")
        package = work / f"streamripper-gui_{version}-1_amd64.deb"
        run(["dpkg-deb", "--root-owner-group", "--build", stage, package], environment)
        digest = hashlib.sha256(package.read_bytes()).hexdigest()
        package.with_suffix(".deb.sha256").write_text(f"{digest}  {package.name}\n", encoding="ascii")
        run(["python3", ROOT / "testing/debian_package.py", package], environment)
        target = args.output_dir / package.name
        # Validate before replacing an earlier artifact, including for custom output filesystems.
        pending = args.output_dir / f".{package.name}.tmp"
        try:
            shutil.copyfile(package, pending)
            os.replace(pending, target)
        finally:
            pending.unlink(missing_ok=True)
        target.with_suffix(".deb.sha256").write_text(f"{digest}  {target.name}\n", encoding="ascii")
        print(f"Package: {target}\nSHA-256: {digest}")


if __name__ == "__main__":
    main()
