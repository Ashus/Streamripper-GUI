"""Build Windows x64 portable and per-user installer packages with SHA-256 sums."""

import argparse
import hashlib
import json
import os
from pathlib import Path
import platform
import re
import shutil
import subprocess
import tempfile

from packaging_common import copy_notices, copy_readme_images, manifest_files, write_checksums

ROOT = Path(__file__).resolve().parent


def run(arguments, **kwargs):
    print("+", *map(str, arguments), flush=True)
    return subprocess.run(list(map(str, arguments)), cwd=ROOT, check=True, **kwargs)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dotnet", default="dotnet", help="Path to the .NET 10 SDK executable")
    parser.add_argument("--streamripper-dir", type=Path, default=ROOT.parent / "Streamripper/dist",
                        help="Verified runtime payload prepared by the recorder's package-windows.py")
    parser.add_argument("--7zip", dest="sevenzip", default=r"C:\Program Files\7-Zip\7z.exe")
    parser.add_argument("--skip-tests", action="store_true", help="Use only after testing this exact checkout")
    parser.add_argument("--iscc", help="Path to the Inno Setup 6.3+ compiler (ISCC.exe)")
    args = parser.parse_args()
    if platform.system() != "Windows":
        parser.error("Build and validate the Windows package on Windows")
    dotnet = shutil.which(args.dotnet)
    sevenzip = shutil.which(args.sevenzip)
    if not dotnet or not sevenzip:
        parser.error(".NET 10 SDK and 7-Zip are required; use --dotnet and --7zip if needed")
    iscc = shutil.which(args.iscc or "ISCC.exe")
    if not iscc and not args.iscc:
        for major in (7, 6):
            for variable in ("ProgramFiles", "ProgramFiles(x86)", "LOCALAPPDATA"):
                if not os.environ.get(variable):
                    continue
                base = Path(os.environ[variable])
                directory = f"Inno Setup {major}"
                candidate = base / (f"Programs/{directory}" if variable == "LOCALAPPDATA" else directory) / "ISCC.exe"
                if candidate.is_file():
                    iscc = str(candidate)
                    break
            if iscc:
                break
    if not iscc:
        parser.error("Inno Setup 6.3+ is required; install it or use --iscc PATH")
    version = (ROOT / "VERSION").read_text(encoding="utf-8").strip()
    if not re.fullmatch(r"\d+(?:\.\d+){1,3}", version):
        parser.error("VERSION must contain a numeric application version")
    recorder = args.streamripper_dir.resolve()
    if not (recorder / "SHA256SUMS").is_file():
        parser.error("Recorder SHA256SUMS is missing; run package-windows.cmd in the recorder project first")
    native_files = manifest_files(recorder)
    native_version = run([recorder / "streamripper.exe", "--version"], capture_output=True, text=True).stdout
    match = re.match(r"Streamripper (\d+)\.(\d+)\.(\d+)", native_version)
    if (not match or tuple(map(int, match.groups())) < (1, 68, 3)
            or "https://github.com/Ashus/streamripper" not in native_version.splitlines()):
        parser.error("The recorder payload must contain the compatible Ashus fork >= 1.68.3")
    if not args.skip_tests:
        run([dotnet, "test", ROOT / "Streamripper-GUI.slnx", "-c", "Release"])
    publish = ROOT / "artifacts/publish/StreamripperGui.Desktop/release_win-x64"
    if not publish.resolve().is_relative_to((ROOT / "artifacts").resolve()):
        raise RuntimeError("Publish directory escapes the artifacts directory")
    if publish.exists():
        shutil.rmtree(publish)
    run([dotnet, "publish", ROOT / "src/StreamripperGui.Desktop", "-c", "Release", "-r", "win-x64",
         "--self-contained", "false", "-p:PublishTrimmed=false"])
    run([publish / "StreamripperGui.exe", "--verify-xaml"])
    output = ROOT / "artifacts/packages"
    output.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="windows-", dir=output) as temporary:
        work = Path(temporary)
        stage = work / "payload"
        shutil.copytree(publish, stage)
        for source in native_files:
            if source.name == "BUILDING.md" or source.name.startswith("BUILDING-"):
                continue
            target = stage / "streamripper" / source.relative_to(recorder)
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(source, target)
        write_checksums(stage / "streamripper")
        (stage / "portable.flag").touch()
        for name in ("VERSION", "README.md", "LICENSE"):
            shutil.copyfile(ROOT / name, stage / name)
        copy_readme_images(ROOT, stage)
        assets = ROOT / "artifacts/obj/StreamripperGui.Desktop/project.assets.json"
        folders = json.loads(assets.read_text(encoding="utf-8"))["packageFolders"]
        copy_notices(Path(next(iter(folders))), publish / "StreamripperGui.deps.json", stage)
        (stage / "COPYRIGHT.txt").write_text(
            "Streamripper GUI - Ashus\nSee LICENSE for the GNU General Public License version 3.\n"
            "Dependency notices are in THIRD-PARTY-NOTICES.txt, licenses/ and streamripper/licenses/.\n",
            encoding="utf-8")
        (stage / "build-info.json").write_text(json.dumps({
            "version": version, "platform": "windows-x86_64", "runtime": "dotnet-runtime-10.0",
            "streamripper": native_version.strip(),
        }, indent=2) + "\n", encoding="utf-8")
        write_checksums(stage)
        manifest_files(stage)
        archive = work / f"streamripper-gui-{version}-win64-portable.7z"
        # Use the staging directory so unrelated files in dist cannot enter the archive.
        subprocess.run([sevenzip, "a", "-t7z", "-m0=lzma2", "-mx=9", str(archive), "."],
                       cwd=stage, check=True)
        run([sevenzip, "t", archive])
        extracted = work / "check"
        run([sevenzip, "x", archive, f"-o{extracted}", "-y"], stdout=subprocess.DEVNULL)
        manifest_files(extracted)
        run([extracted / "StreamripperGui.exe", "--verify-xaml"])
        run([extracted / "streamripper/streamripper.exe", "--version"], stdout=subprocess.DEVNULL)
        installer_payload = work / "installer-payload"
        shutil.copytree(stage, installer_payload)
        (installer_payload / "portable.flag").unlink()
        write_checksums(installer_payload)
        manifest_files(installer_payload)
        run([iscc, f"/DPayloadDir={installer_payload}", f"/DOutputDir={work}",
             ROOT / "packaging/windows/StreamripperGui.iss"])
        installer = work / f"streamripper-gui-{version}-win64-setup.exe"
        if not installer.is_file():
            raise RuntimeError("Inno Setup did not produce the expected installer")
        # Update only files owned by the previous generated manifest.
        destination = ROOT / "dist"
        old_manifest = destination / "SHA256SUMS"
        if old_manifest.exists():
            for line in old_manifest.read_text(encoding="utf-8").splitlines():
                _, relative = line.split("  ", 1)
                target = (destination / relative).resolve()
                if not target.is_relative_to(destination.resolve()):
                    raise ValueError(f"Unsafe previous manifest path: {relative}")
                if not (stage / relative).exists() and target.is_file():
                    target.unlink()
        shutil.copytree(stage, destination, dirs_exist_ok=True)
        target = output / archive.name
        pending = output / f".{archive.name}.tmp"
        try:
            shutil.copyfile(archive, pending)
            os.replace(pending, target)
        finally:
            pending.unlink(missing_ok=True)
        digest = hashlib.sha256(target.read_bytes()).hexdigest()
        target.with_suffix(".7z.sha256").write_text(f"{digest}  {target.name}\n", encoding="ascii")
        print(f"Payload: {destination}\nArchive: {target}\nSHA-256: {digest}")
        installer_target = output / installer.name
        installer_pending = output / f".{installer.name}.tmp"
        try:
            shutil.copyfile(installer, installer_pending)
            os.replace(installer_pending, installer_target)
        finally:
            installer_pending.unlink(missing_ok=True)
        installer_digest = hashlib.sha256(installer_target.read_bytes()).hexdigest()
        installer_target.with_suffix(".exe.sha256").write_text(
            f"{installer_digest}  {installer_target.name}\n", encoding="ascii")
        print(f"Installer: {installer_target}\nSHA-256: {installer_digest}")


if __name__ == "__main__":
    main()
