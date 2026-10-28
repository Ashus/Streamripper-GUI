# Building and distributing on Windows

## Prerequisites

Development requires the .NET 10 SDK. The published Windows x64 application is
framework-dependent: install **.NET Runtime 10 for Windows x64** on the target
computer. Avalonia does not require the Windows Desktop Runtime.

A portable archive means an extractable application without an installer; it
does not mean a bundled .NET runtime. Native Streamripper and its required DLLs
are separate dependencies.

## Development

From the repository root:

```bat
dotnet build -c Release
dotnet run --project src/StreamripperGui.Desktop
dotnet test -c Release
```

Open `Streamripper-GUI.slnx` in Rider and run `StreamripperGui.Desktop`.
The project name stays unchanged; its assembly and executable are named
`StreamripperGui`.

Both Rider and CLI use the SDK artifacts layout configured in
`Directory.Build.props`:

```text
artifacts/
  bin/StreamripperGui.Desktop/debug/StreamripperGui.exe
  bin/StreamripperGui.Desktop/release/StreamripperGui.exe
  bin/StreamripperGui.Desktop/release_win-x64/StreamripperGui.exe
  publish/StreamripperGui.Desktop/release_win-x64/StreamripperGui.exe
  obj/
```

Other projects have separate directories under bin/obj. Publish is the
distribution output; bin is a development output and can include native assets
for several platforms. Do not distribute bin or infer archive size from it.

If a running application locks Release files, close it before rebuilding that
configuration. Use the normal destinations above instead of creating alternate
output trees.

Use the complete build pipeline when validating the desktop application, not
MSBuild's standalone `Compile` target: Avalonia also needs its compiled XAML.
If startup reports `No precompiled XAML found`, rebuild the desktop project with
`dotnet build src/StreamripperGui.Desktop -c Release --no-incremental`, then verify
startup. A successful C# compilation alone does not verify an Avalonia build.

Desktop build and publish automatically run `--verify-xaml` on their resulting
assembly. This checks Avalonia’s compiled loader and the generated populate method
for App and every XAML-backed view; missing compiled XAML fails the pipeline.
The check runs before single-instance handling, without opening windows or
loading user configuration. It can also be run manually:

```bat
dotnet artifacts/bin/StreamripperGui.Desktop/release/StreamripperGui.dll --verify-xaml
```

This verifies compiled resources, not full UI startup. Keep the manual Windows
startup check when preparing a release.

## Windows x64 publish

```bat
dotnet publish src/StreamripperGui.Desktop/StreamripperGui.Desktop.csproj -c Release -r win-x64 --self-contained false
```

Use a clean publish destination for release preparation. The project excludes
all PDB files and `Avalonia.DesignerSupport.dll` from Release publish and removes
stale copies of those files after publishing into an existing directory.
Development builds retain their debugging/design support.

DesignerSupport was checked with Avalonia 12.1.4 and is not required by the
published application. Verify published startup again after dependency upgrades.
Do not remove other Avalonia/native DLLs merely because their names appear
platform-specific. No trimming, single-file publish or Native AOT is configured.

## Release preparation

`VERSION` supplies application/package versions. The icon comes from
`assets/radio.ico`; product/file descriptions identify Streamripper GUI, author
metadata identifies Ashus, and repository metadata points to GitHub without
adding the URL to the Task Manager process description.

Before an explicitly requested release:

1. Prepare the compatible recorder payload using its `package-windows.cmd`.
2. Run `package-windows.cmd` in this repository. It runs Release tests and publish,
   verifies compiled XAML, and assembles `dist/` with the recorder in `streamripper/`.
   Install Inno Setup 6.3 or newer first. The same pipeline also builds the per-user
   installer. Optional arguments: `--streamripper-dir PATH`, `--7zip PATH`,
   `--dotnet PATH`, `--iscc PATH`. The compiler is discovered on PATH or in standard
   Inno Setup 6 or 7 installation directories, including per-user installations.
   Use `--skip-tests` only after testing the exact checkout.
3. Verify startup on Windows x64 with the .NET Runtime installed and no SDK.
4. Review the generated payload and notices. The package includes `portable.flag`
   and per-file `SHA256SUMS`; personal configuration, logs and recordings are excluded.
5. Collect `artifacts/packages/streamripper-gui-<version>-win64-portable.7z` and its
    `.7z.sha256` sidecar, plus `streamripper-gui-<version>-win64-setup.exe` and its
    `.exe.sha256` sidecar. The version comes from `VERSION`.

The helper builds a fresh archive from staging, tests it with 7-Zip, extracts it,
verifies every file checksum and compiled XAML, and then updates `dist/` and the
archive. Obsolete files are removed only if listed in the previous generated
manifest; unrelated files in `dist/` are preserved and excluded from the archive.
A missing or modified recorder payload fails validation: regenerate it in the
recorder project first. Close the GUI and Rider XAML previews that lock build files
before packaging.

## Per-user installer

`packaging/windows/StreamripperGui.iss` consumes a separate, verified staging copy
without `portable.flag`. It uses a stable AppId for upgrades, installs into
`%LOCALAPPDATA%\Programs\Streamripper GUI`, registers a per-user uninstaller and
creates a Start menu shortcut. Configuration and recordings are not installer-owned.
Do not install over an extracted portable copy: its marker and settings belong to
that copy. The installer does not remove unrelated files during upgrades.

The runtime is not bundled. Interactive installation offers a task selected by default to
download and install .NET Runtime 10 x64 only when a stable system runtime is missing.
The download is verified with a pinned SHA-256 checksum before execution. Update
`RuntimeUrl` and `RuntimeSHA256` together in the ISS script when refreshing the
runtime; verify the download against Microsoft's release metadata first.
Only the optional Microsoft runtime installer requests administrator approval.
Cancelling or failing that step leaves Setup on the preparation page; retry or go
back and uncheck the task to install the GUI without the runtime.

For unattended installation, use `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-`;
use `/SILENT` instead of `/VERYSILENT` to keep progress visible. Both modes skip
the optional dependency download and application launch. Provision .NET Runtime
10 x64 separately before starting the application. The uninstaller accepts
`/VERYSILENT /SUPPRESSMSGBOXES /NORESTART`. Add `/LOG="path"` for diagnostics.
Run installation and uninstallation as the target user, without elevation.

Validate the generated installer on Windows with the runtime already installed:

```bat
python -m testing.windows_installer artifacts/packages/streamripper-gui-<version>-win64-setup.exe
```

The check refuses to overwrite an existing per-user installation. It verifies silent
install, in-place upgrade, registered version/path, installed payload checksums,
compiled XAML, silent uninstall and preservation of an unrelated user-owned file.
It uses a temporary directory under `artifacts`. Test the interactive missing-runtime
task and cancelled administrator approval separately on a clean machine.

The GUI license is included as `LICENSE`; dependency notices are shipped separately.
Release packages include `CHANGELOG.md`. `BUILDING*` documents stay in the repository.
 Do not commit binaries, toolchains,
`dist/` or archives. Version changes, tags, publishing and release uploads
require an explicit request.
