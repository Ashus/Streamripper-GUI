# Main window state

The main window defaults to 1130 × 840 logical Avalonia units (DIPs), with
the existing minimum size of 960 × 540. On successful window closure,
`config.json` stores the last normal client size and the maximized flag:

```json
"application": {
  "window": {
    "width": 1130,
    "height": 840,
    "maximized": false,
    "centerXPercent": 50,
    "centerYPercent": 50,
    "consoleHeight": 260
  }
}
```

Existing application settings are preserved. Missing or invalid size values
fall back to the defaults. The normal window's outer-frame center is stored as
horizontal and vertical percentages of the full containing monitor bounds
(including the taskbar area), not the combined multi-monitor desktop. Missing
or invalid percentages default to 50. Startup uses the monitor selected for
the new window, falling back to the primary monitor; monitor identity is not
persisted. Placement is limited to the current work area so the title bar stays
accessible. Maximized or
minimized dimensions never replace the normal size; minimizing also preserves
the preceding maximized flag. Startup restores the normal size first, then
maximizes when requested. The application never starts minimized.

Sizes are not physical pixels and are not multiplied by a stored DPI factor.
Avalonia applies the current monitor scaling. At 125%, a logical size occupies
approximately 1.25 times as many physical pixels per axis, with rounding and
native window decorations handled by the platform. A lower-resolution screen
can have less logical workspace available; the existing minimum size still
applies.

Verified with a native Windows UI check: default size, resizing, maximization,
minimization, persistence, reopening maximized, and restoring the normal size.
Switching the Windows DPI setting itself was not part of that check.

The console panel height is saved alongside window geometry on close, in logical
Avalonia units, and restored on the next launch. A missing value retains the
original 5:3 panel ratio. A restored fixed height is constrained to leave the
upper panel's minimum height available, including after resizing the window.

The station sidebar starts at a fixed width of 300 logical Avalonia units, with
a minimum of 190. Maximizing or widening the window gives the extra space to the
editor instead of growing the sidebar. The vertical splitter still allows manual
resizing, including widths above 330; the chosen width is not persisted and resets
to 300 on the next launch.
