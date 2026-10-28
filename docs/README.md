# Documentation

Start with the [user guide](../README.md) for setup and everyday use.
Build, run, test and publishing instructions are maintained in
[BUILDING-WINDOWS.md](../BUILDING-WINDOWS.md) and
[BUILDING-LINUX.md](../BUILDING-LINUX.md).

Windows recording and Debian 13 amd64 recording/DEB packaging are implemented.
Linux verification uses WSL and an isolated X11 display; see the checklist for
remaining platform and release boundaries.

| Document | Responsibility |
| --- | --- |
| [Specification](specification.md) | Product scope, supported behavior and boundaries |
| [Architecture](architecture.md) | Projects, models, services and dependencies |
| [Configuration](configuration.md) | JSON schema, paths, persistence and OPML reconciliation |
| [Station and filter UI](station-and-filter-ui.md) | Interaction, editing and deletion |
| [Recording profiles](recording-profile-ui.md) | Controls, defaults and CLI mapping |
| [Streamripper integration](streamripper-integration.md) | Process lifecycle and native protocol |
| [Console design](console-design.md) | UTF-8/CR/LF parsing, rendering and limits |
| [Localization](localization.md) | Resource keys and translation maintenance |
| [Window state](window-state.md) | Geometry, placement and DPI |
| [Checklist](checklist.md) | Completed work and remaining verification |
