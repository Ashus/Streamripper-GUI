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
   Optional arguments: `--streamripper-dir PATH`, `--7zip PATH`, `--dotnet PATH`.
   Use `--skip-tests` only after testing the exact checkout.
3. Verify startup on Windows x64 with the .NET Runtime installed and no SDK.
4. Review the generated payload and notices. The package includes `portable.flag`
   and per-file `SHA256SUMS`; personal configuration, logs and recordings are excluded.
5. Collect `artifacts/packages/streamripper-gui-<version>-win64.7z` and its
   `.7z.sha256` sidecar. The version comes from `VERSION`.

The helper builds a fresh archive from staging, tests it with 7-Zip, extracts it,
verifies every file checksum and compiled XAML, and then updates `dist/` and the
archive. Obsolete files are removed only if listed in the previous generated
manifest; unrelated files in `dist/` are preserved and excluded from the archive.
A missing or modified recorder payload fails validation: regenerate it in the
recorder project first. Close the GUI and Rider XAML previews that lock build files
before packaging.

This checkout does not yet declare a GUI project license; establish it before
public distribution.
 Do not commit binaries, toolchains,
`dist/` or archives. Version changes, tags, publishing and release uploads
require an explicit request.
