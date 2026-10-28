# Implementation and release checklist

This checklist describes the current code, not historical implementation stages.
Checked implementation items are not a claim of Linux support or release readiness.

**Overall status: incomplete.** Windows application work and Debian 13 amd64
recording/DEB packaging are implemented. Linux builds and X11 GUI checks pass under
WSL; broader platform coverage and final release verification remain open.

## Implemented

- [x] Buildable .NET 10 solution and Rider run target with one artifacts layout.
- [x] Main station list, pinned active group, live search, tabs, splitters and global Settings.
- [x] Station CRUD, automatic field saves, directory-safe unique names and confirmation.
- [x] Name-based OPML preview/import, merge/replacement, duplicate precedence and repeat-import aliases.
- [x] Editable default/named recording profiles, independent filters and stable assignments.
- [x] Validated typed CLI mapping, defaults, dependent controls and saved command preview.
- [x] Concurrent Windows sessions, private-console graceful stop, explicit force fallback and close-all.
- [x] Independent live UTF-8/CR/LF consoles, bounded history, incremental rendering and multi-line selection.
- [x] Actual format/capability observations, URL invalidation and observed relay link/copy.
- [x] Native-rule editor, GLib validation, insertion helpers and explicit valid saves.
- [x] Actual-session rule-reload feedback and native current-title re-evaluation.
- [x] Metadata-log tail, decoded selection helpers and explicit in-place clearing.
- [x] Startup-only local TXT discovery, folder import and confirmed filter/file deletion.
- [x] Atomic JSON persistence, JSON backups, rule writes without backups and load-error protection.
- [x] Portable/user paths, immediate committed saves and single-instance activation.
- [x] Window size/position/maximization and console-height persistence.
- [x] Application icon, VERSION-based metadata and repository URL outside the process description.
- [x] EN/CZ selection, automatic language fallback and light/dark/system appearance.
- [x] Shared recording-process contract with Windows private-console control and Linux targeted SIGINT.
- [x] Linux executable/configuration/recording defaults, system GLib and stable lock/activation.
- [x] Framework-dependent Debian 13 amd64 GUI packaging with compatible recorder dependency and SHA-256.
- [x] Automatic DEB payload, permissions, checksums, launcher, runtime and native-library validation.

## Final code and documentation cleanup

- [x] Replace hashed translation keys with semantic names matching final UI text.
- [x] Route application-owned UI labels/messages through catalogs; retain original user/native text.
- [x] Remove duplicate/unused resources and obsolete simulator/manual-save remnants.
- [x] Add catalog/placeholder/fallback/nested-error regression tests.
- [x] Verify live language switching on existing windows and dialogs.
- [x] Review direct/transitive dependencies; retain only the existing required direct UI packages.
- [x] Keep AvaloniaEdit as the sole document editor/console renderer.
- [x] Remove temporary verification outputs from the normal artifacts tree.
- [x] Review regression tests against the final behavior; remove the unused caret-based
      rule insertion API and replace its obsolete test assertions with current helper behavior.
- [x] Remove ignored one-off prototype projects, generators and diagnostics from build/.
- [x] Rewrite maintained documentation in English and separate product, UI, storage and integration topics.
- [x] Remove superseded claims about simulator controls, future OPML/JSON stages,
      rules backups, retained deleted filters, settings-only-on-close and GUI relay allocation.

## Verification evidence

The automated suite currently has 140 passing tests on Debian 13 amd64 covering console parsing and
projection, station validation, JSON preservation/recovery boundaries, recording
arguments, OPML planning, native-rule parsing, log handling, endpoint conversion
and localization, draft navigation, compiled XAML, Linux process isolation/signals,
fast exits, native GLib and lock/activation/restart. Windows has 135 passing tests
and skips the five Linux-only checks. Run `dotnet test -c Release` from the root.

Windows UI/process checks during implementation covered concurrent local
MP3/ICY recordings, independent stop/console switching, pinned/search behavior,
real native rule reload/current-title filtering, imports and deletion, automatic
saving, single-instance activation, geometry persistence and console interactions.

The final localization check exercised live EN/CZ tabs, an already-open Settings
dialog, stored nested errors, unchanged station names, default actions, profile
dirty state, Czech autodetection and English fallback. It used an isolated
configuration directory and did not modify personal configuration.

Linux verification used the actual shared checkout over `ssh wsl-ai-sandbox`,
with separate managed outputs/cache under `artifacts/linux`. The installed
framework-dependent DEB uses the system .NET 10 APT package and depends directly
on `streamripper-ng`. Joint APT installation replaced the old `streamripper`
package and confirmed that `/usr/bin/streamripper` belongs to `streamripper-ng`.
The recorder's isolated APT transition test also replaced Debian's original
`streamripper 1.64.6-2` in a disposable Debian 13 container. An isolated Xvfb/X11
GUI test exercised concurrent local ICY MP3 recordings, native rule reload,
independent graceful stop, recording restart, close-all audio finalization,
format persistence, reopening, second-instance activation and the folder button
through an isolated XDG directory association. Evidence is recorded by
`testing/linux_ui_smoke.py` under `artifacts/linux/gui-smoke-*`.

The DEB validator checks root ownership and exact permissions, complete MD5 payload
and SHA-256 archive checksums, absence of bundled runtime files, compiled XAML,
desktop launcher syntax and required native library resolution.

Version 1.0 packaging was rebuilt on Windows and Debian 13/WSL with the same test
counts above. The Windows portable archive and Inno Setup installer include the
compatible recorder, license notices and SHA-256 sidecars. Neither package bundles
.NET. BUILDING documents stay in the repository. The Windows installer lifecycle
check passed silent installation, in-place reinstallation, payload checksums,
compiled XAML, silent removal and preservation of a user-owned file. The optional
runtime download and UAC cancellation still require a clean-machine interactive check.

## Before a first release

- [x] Extended real-radio and long-duration concurrent-recording soak test.
- [x] Broader AAC/Ogg/FLAC/HLS and reconnect/error-path coverage with the shipping CLI.
- [ ] Physical Windows DPI/monitor changes and reduced-work-area placement checks.
- [x] Clean Windows x64 runtime-only machine test of published output.
- [x] Assemble the requested portable archive with compatible Streamripper DLLs,
      dependency license notices and a SHA-256 checksum.
- [x] Review all shipping assets/notices and user documentation against that exact package.
- [x] Include the GUI project license (`LICENSE`, GPL v3) in Windows and Debian packages.
- [x] Verify interactive installation, optional runtime download, silent installation,
      upgrade and silent uninstallation on a clean Windows x64 machine.

## Separate platform work

- [x] Implement and verify Linux process launch, graceful signal delivery and activation.
- [x] Verify GLib/native libraries, paths, XDG folder integration and X11 UI on Debian 13 amd64/WSL.
- [x] Produce and validate a framework-dependent Debian GUI package and install it with APT.
- [x] Verify joint APT installation/migration with the renamed streamripper-ng recorder DEB;
      system-package replacement also passed in an isolated Debian 13 container.
- [ ] Native Wayland, physical Linux desktop/DPI/monitor changes and other distributions/architectures.

## Next verification order

1. Extend Linux real-radio/long-duration and AAC/Ogg/FLAC/HLS/reconnect coverage.
2. Verify native desktop/Wayland and physical monitor/DPI behavior beyond WSL/X11.
3. Complete dependency/asset notice review against the included GUI license.
4. Complete the remaining Windows/release verification and assemble release artifacts
   only when a release is requested.

Version changes, release tags and publication are outside this cleanup.