"""Shared release payload notices and checksums."""

import hashlib
import json
import shutil
import xml.etree.ElementTree as ET


def copy_notices(packages, dependencies, destination):
    lines = ["Third-party packages included in this build", ""]
    # Publish resolves native assets for the target RID. Restore metadata also
    # includes other platforms and build-only packages, which are not distributed.
    manifest = json.loads(dependencies.read_text(encoding="utf-8"))
    target = manifest["targets"][manifest["runtimeTarget"]["name"]]
    for identity in sorted(target):
        library = manifest["libraries"][identity]
        if library["type"] != "package":
            continue
        package = packages / library["path"]
        nuspec = next(package.glob("*.nuspec"), None)
        if nuspec is None:
            raise RuntimeError(f"Missing package metadata: {identity}")
        metadata = ET.parse(nuspec).getroot()
        fields = {element.tag.rsplit("}", 1)[-1]: element.text for element in metadata.iter()}
        lines.extend([identity, f"License: {fields.get('license', 'See package notices')}",
                      f"Source: {fields.get('projectUrl', 'https://www.nuget.org/packages/' + identity)}", ""])
        for source in package.rglob("*"):
            if source.is_file() and source.name.lower().startswith(("license", "copying", "notice", "thirdparty", "third-party")):
                notice = destination / "licenses" / identity.replace("/", "-") / source.relative_to(package)
                notice.parent.mkdir(parents=True, exist_ok=True)
                shutil.copyfile(source, notice)
    (destination / "THIRD-PARTY-NOTICES.txt").write_text("\n".join(lines), encoding="utf-8")


def write_checksums(directory):
    entries = []
    for path in sorted(directory.rglob("*")):
        if path.is_file() and path != directory / "SHA256SUMS":
            entries.append(f"{hashlib.sha256(path.read_bytes()).hexdigest()}  {path.relative_to(directory).as_posix()}")
    (directory / "SHA256SUMS").write_text("\n".join(entries) + "\n", encoding="utf-8", newline="\n")


def manifest_files(directory):
    directory = directory.resolve()
    result = []
    for line in (directory / "SHA256SUMS").read_text(encoding="utf-8").splitlines():
        digest, relative = line.split("  ", 1)
        path = (directory / relative).resolve()
        if not path.is_relative_to(directory) or path == directory:
            raise ValueError(f"Unsafe manifest path: {relative}")
        if hashlib.sha256(path.read_bytes()).hexdigest() != digest:
            raise ValueError(f"Checksum mismatch: {path}")
        result.append(path)
    return result

def copy_readme_images(root, destination):
    images = destination / "assets"
    images.mkdir(parents=True, exist_ok=True)
    for name in ("screenshot1.png", "screenshot2.png", "screenshot3.png"):
        shutil.copyfile(root / "assets" / name, images / name)
