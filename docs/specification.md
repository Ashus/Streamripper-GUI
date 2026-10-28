# Product specification

Streamripper GUI manages internet-radio stations and controls the external
[Ashus/streamripper](https://github.com/Ashus/streamripper) CLI. The GUI does not
download or decode audio itself. It is a traditional desktop application without
a browser, WebView, database or external application service.

This document describes the implemented product scope. Detailed behavior belongs
to the linked documents; obsolete stage proposals do not override the code.

## Technology and platform scope

- C# / .NET 10, Avalonia UI and MVVM with CommunityToolkit.Mvvm.
- AvaloniaEdit for the live console, native-rule editor and metadata-log pane.
- System.Text.Json for versioned local configuration.
- JetBrains Rider and `dotnet build` as supported development workflows.
- Windows x64 recording and framework-dependent portable distribution.
- Debian 13 amd64 recording and framework-dependent DEB packaging, verified under WSL/X11.
- Other Linux distributions, native Wayland and physical Linux monitor/DPI changes remain unverified.

Streamripper remains a separately built executable. Integration follows the
current fork, not assumptions about historical upstream behavior.

## Functional scope

- Station creation, automatic saving, rename and confirmed removal.
- Name-based OPML import with a live preview and merge/replacement modes.
- Editable global default recording options and reusable named profiles.
- Independent station filter assignments to real native TXT rules.
- Native GLib validation, rule highlighting, literal-text helpers and a metadata log.
- Concurrent recordings with isolated controls, histories and pinned station rows.
- Live UTF-8 console output with carriage-return status replacement, selection,
  bounded memory and theme colors.
- Global language, appearance, executable and destination settings.
- Atomic configuration writes, portable/user storage, single-instance activation
  and persisted window geometry.

## UI contract

The main window has a station list, Stations/Profiles/Filters tabs, resizable
panels and a bottom console. Global Settings is a separate modal opened from
the footer. There is no menu/toolbar or simulator.

Active recordings stay visible above the filtered inactive list. Selection stays
available while recording and switches the console. Start/stop is available in
the station detail, on double-click and in the station context menu.

Every modal has one accent-colored default action activated by Enter.
Escape cancels or closes. Settings commits valid changes and offers Close.
Deletion confirmation uses Yes as its primary/default action.

## Data and process boundaries

Each station selects a recording profile and a metadata filter independently.
Default is an editable, undeletable recording profile; None is an immutable
absence of a custom rules file. Named entries keep stable IDs when renamed.

A recording captures saved settings at startup. Later profile changes apply to
future recordings. Successful edits of a rules file already used by a running
session are the deliberate live-reload exception.

Launch the CLI directly without a shell. Read stdout and stderr concurrently
as byte streams, retain diagnostics, and stop gracefully before offering force
termination. Do not treat a zero exit code as proof that audio was recorded.

## Detailed agreements

- [Configuration and OPML](configuration.md)
- [Station/filter interactions](station-and-filter-ui.md)
- [Recording-profile controls](recording-profile-ui.md)
- [Process integration](streamripper-integration.md)
- [Console implementation](console-design.md)
- [Window state](window-state.md)
- [Localization](localization.md)
- [Architecture](architecture.md)
- [Remaining work](checklist.md)

Do not expand platform or release claims until the corresponding checks pass.