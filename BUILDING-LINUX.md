# Building and packaging on Linux

The Linux package targets Debian 13 amd64, including WSL. Use a Linux .NET 10 SDK,
Python 3, dpkg-dev, and the native dependencies listed below. Windows and Linux
must use separate intermediate/output directories, even in a shared checkout.

## WSL workspace

The development sandbox is reached with `ssh wsl-ai-sandbox`. The shared checkout
is `/mnt/projekty/Streamripper-GUI` and the recorder checkout is the sibling
`/mnt/projekty/Streamripper`. Run commands from the GUI repository root.

```sh
sudo apt-get update
sudo apt-get install -y python3 dpkg-dev curl desktop-file-utils ca-certificates libicu76 libssl3t64 \
  libx11-6 libice6 libsm6 libfontconfig1 libxcb1 libxrandr2 libxi6 libxcursor1 \
  libxrender1 libxext6 libglib2.0-0t64 fonts-dejavu-core xdg-utils
```

Install the .NET 10 SDK using the official Microsoft install script or package
manager. A project-local SDK can be installed under
`artifacts/toolchains/linux-dotnet`; pass its executable with `--dotnet` below.
The DEB uses the system .NET 10 runtime; an SDK is needed only for building.
For a private build SDK, install the system runtime too so package validation tests
exercise the runtime used after installation.

## Build the compatible recorder

The GUI depends on the separate **streamripper-ng >= 1.68.3** package. The old
distribution package named `streamripper` does not satisfy this dependency.
The fork keeps `/usr/bin/streamripper` and declares `Conflicts: streamripper`
and `Replaces: streamripper` because both packages own that command. A version-only
dependency on `streamripper` would exclude today's old distribution version but
would not distinguish this fork from another package with a high enough version.
The fork's DEB metadata is maintained in the recorder repository.
Build it using the recorder repository’s documented Linux workflow. To keep the
build/cache separate from Windows and put both DEBs together:

```sh
python3 ../Streamripper/package-linux.py \
  --build-dir "$PWD/artifacts/linux/native-build" \
  --output-dir "$PWD/artifacts/packages"
```

The native recorder keeps system libraries as package dependencies; it is not
copied into the GUI payload. Neither command publishes or changes versions.

## Build the GUI DEB

```sh
python3 package-linux.py --dotnet "$PWD/artifacts/toolchains/linux-dotnet/dotnet"
```

The helper runs the Release tests and a complete Linux x64 framework-dependent
publish, including compiled-XAML verification. It writes all managed intermediates
to `artifacts/linux/managed` and keeps the Linux NuGet cache under
`artifacts/linux/nuget`. It does not touch Windows build outputs.
Use `--skip-tests` only after testing the exact same sources.

The version is read from `VERSION`; the Debian packaging revision is `-1`:

- `artifacts/packages/streamripper-gui_<version>-1_amd64.deb`
- `artifacts/packages/streamripper-gui_<version>-1_amd64.deb.sha256`

The payload includes managed application DLLs, native Avalonia libraries, dependency
license notices, a desktop launcher, and the application icon. It excludes debug
symbols, DesignerSupport, personal configuration, recordings and toolchains.
Before replacing a previous artifact, the helper validates archive ownership,
permissions, complete payload checksums, launcher syntax, execution with the system
runtime and native library resolution using `testing/debian_package.py`. The validator
rejects bundled .NET runtime files. Managed .NET assemblies keep their `.dll`
extension on Linux; Linux native libraries use `.so`.
No service is installed. Package creation uses root ownership without requiring
root privileges. Staging is temporary and a failed build preserves prior DEBs.

This checkout has no project license file yet. The package records that fact;
dependency license notices do not grant a license for the GUI itself. Establish
the GUI license before public distribution.

## Install and run

Register Microsoft's Debian 13 repository once, following the
[official .NET instructions](https://learn.microsoft.com/en-us/dotnet/core/install/linux-debian):

```sh
curl -fsSLo packages-microsoft-prod.deb https://packages.microsoft.com/config/debian/13/packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
sudo apt update
sudo apt install dotnet-runtime-10.0
```

The plain runtime is sufficient; neither an SDK nor ASP.NET/Windows Desktop runtime
is required on the target computer. After registering the repository, APT can also
install the runtime automatically through the GUI package's dependency.
Install both locally built DEBs with APT, which resolves their dependencies:

```sh
recorder_version=$(cat ../Streamripper/VERSION)
sudo apt install "./artifacts/packages/streamripper-ng_${recorder_version}-1_amd64.deb" \
  "./artifacts/packages/streamripper-gui_$(cat VERSION)-1_amd64.deb"
streamripper-gui
```

APT removes an installed `streamripper` package and installs `streamripper-ng`
in the same transaction. The executable path remains `/usr/bin/streamripper`;
existing GUI settings do not need a path change. Both recorder packages cannot
be installed together. Do not use `--force-overwrite` or `--force-depends`.
The GUI depends directly on `streamripper-ng`, so a distribution package named
`streamripper` cannot satisfy its dependency, regardless of that package's version.

`dpkg -i` alone does not download dependencies. Prefer `apt install ./file.deb`;
if already installed with `dpkg -i`, `sudo apt --fix-broken install` can resolve
missing dependencies after the required repository has been configured.

The launcher runs `/usr/lib/streamripper-gui/StreamripperGui`. The recorder defaults
to `/usr/bin/streamripper`. Settings live under `$XDG_CONFIG_HOME/StreamripperGui`
(normally `~/.config/StreamripperGui`); a local `portable.flag` still takes priority.
Recordings default to the user’s Music/Streamripper directory, with a home-based
fallback if the Music special folder is unavailable. Package removal leaves user
configuration and recordings intact.

```sh
/usr/lib/streamripper-gui/StreamripperGui --verify-xaml
sudo apt remove streamripper-gui
```

## Validation

Linux process tests exercise independent SIGINT stop, explicit forced termination,
UTF-8/argument preservation, fast exits, system GLib regex compilation, and the
single-instance lock/activation/restart. Windows tests and builds must still pass.

Use an isolated Xvfb display/configuration for GUI testing in a noninteractive WSL
session. GUI and real local-radio recording checks are separate from compiled-XAML
checks. Remaining platform/release boundaries are tracked in
[docs/checklist.md](docs/checklist.md).

```sh
sudo apt-get install -y xvfb openbox xdotool xclip scrot
python3 testing/linux_ui_smoke.py --sample ../Streamripper/build/sample-5s.mp3
python3 testing/debian_package.py "artifacts/packages/streamripper-gui_$(cat VERSION)-1_amd64.deb"
```

The GUI smoke test requires the installed recorder/GUI and an existing valid MP3
fixture. It downloads no media and uses local HTTP only. It verifies concurrent
recordings, rule reload, independent stop, close-all, restart and activation.
Logs, screenshots and `result.json` remain under `artifacts/linux/gui-smoke-*`.
