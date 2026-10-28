# Architecture

## Solution

All projects target net10.0.

| Project | Responsibility |
| --- | --- |
| StreamripperGui.Core | Station/catalog models, recording options/arguments, OPML reconciliation, native-rule parsing, stream-summary parsing and console buffering/projection |
| StreamripperGui.Infrastructure | Versioned JSON storage, OPML XML reading, GLib regex validation, native rule files and metadata-log I/O |
| StreamripperGui.Desktop | Avalonia views, view models, application composition, localization and Windows/Linux process/single-instance services |
| StreamripperGui.Core.Tests | Core/infrastructure tests and UI-independent translation-catalog tests |
| StreamripperGui.Desktop.Tests | Draft navigation, compiled XAML and platform process/activation integration tests |

Infrastructure references Core; Desktop references both. Core has no Avalonia
or process dependency. App creates the main view model and window directly;
there is no DI container or speculative service framework.

## Application state

MainWindowViewModel coordinates station editing, catalogs, persistence, imports
and shutdown. Partial files group these responsibilities. Each StationViewModel
owns its RecordingViewModel and ConsoleViewModel. Switching selection changes
the displayed session, never its lifetime.

StationCatalog validates names, HTTP(S) URLs and filesystem-safe directory
collisions. OPML planning produces a new station collection and change preview
without modifying the current data. Commit reconciles again and preserves
existing station/session objects.

RecordingOptions is a complete typed snapshot. RecordingArguments validates it
and constructs a URL-first argument array. The profile editor serves both the
default options and named profiles. Saved profiles and station assignments use
stable IDs; editor drafts are separate from committed data.

StreamSummaryParser reads native startup bytes independently of console refresh
and persists observations only when their source URL still matches the station.
The source URL is an in-memory guard, not a duplicate JSON field.

## Process and console

IRecordingProcess separates the platform process contract from the per-station
lifecycle. WindowsRecordingProcess owns native process/pipe handles and direct CRT
argument quoting. LinuxRecordingProcess uses ProcessStartInfo.ArgumentList, raw
redirected pipes and a targeted SIGINT, with explicit process-tree kill only for
Force stop. RecordingViewModel owns start, concurrent output draining, stop, fallback
after explicit native HLS/FLAC rules rejection, and finalization.

ConsoleBuffer owns independent UTF-8 decoders and active rows for stdout/stderr.
ConsoleViewModel batches bounded snapshots on the UI dispatcher.
ConsoleTextProjection turns stable row IDs into incremental AvaloniaEdit edits.
Views own selection, scrolling, colors and clipboard interaction.

SingleInstance acquires the active configuration lock before constructing the
view model. Later launches request activation over a current-user-only named
pipe and exit without loading or writing configuration.
Linux holds `flock` on a persistent lock-file inode; it does not unlink a live lock.

## Rules and storage

MetadataRules parses native commands and flags. GlibRegexValidator checks only
the embedded regex using GLib next to the selected Streamripper executable on
Windows. Linux uses a colocated GLib when present, otherwise the system soname;
.NET regex is not used as a compatibility substitute.

RulesEditorViewModel owns drafts, debounced validation and explicit saves.
MetadataRuleFiles restricts paths to the active rules directory and writes
atomically without backups. MetadataLogTail reads a bounded tail, decodes
logged metadata for insertion helpers and explicitly truncates existing logs.

JsonConfigurationStore owns storage-mode paths, schema checks and atomic
single-file replacement with JSON backups. Cross-file updates use safe ordering
rather than claiming a multi-file transaction; see [configuration.md](configuration.md).

## Localization and dependencies

Localizer provides observable language changes to XAML. TranslationCatalog has
no UI dependency and is also compiled into tests; embedded EN/CZ JSON catalogs
hold semantic keys. See [localization.md](localization.md).

The direct UI dependencies are Avalonia.Desktop, Avalonia.Themes.Fluent,
Avalonia.AvaloniaEdit and CommunityToolkit.Mvvm. Each remains in use.
AvaloniaEdit serves all three document views; no TextMate, grammar package,
browser or alternate console renderer is retained. Platform-native transitive
assets belong to Avalonia/Skia/HarfBuzz and are not individually pruned from
development builds. The test project uses xUnit v3 and the .NET 10
Microsoft.Testing.Platform runner.

## Platform boundary

Windows private-console control and Linux process/signals are isolated in
Desktop/Services. The Debian 13 amd64 DEB depends on system .NET 10 and the compatible
`streamripper-ng` native recorder package. Linux builds have separate artifacts and NuGet caches.
Actual Linux build, test, package-install and X11 GUI checks run under WSL;
unverified platform/release cases remain in [checklist.md](checklist.md).
