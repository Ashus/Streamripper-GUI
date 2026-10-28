# Live console

## Native output and width

The current fork flushes redirected output as UTF-8. Progress updates end in CR;
track transitions add LF. Redirected pipe/file output contains the full title
and does not emit terminal erase-to-end sequences. Identical updates may be
suppressed by the native implementation.

The GUI already redirects stdout/stderr to separate pipes. It does not send
the panel width to Streamripper. In the updated native output code, an unknown
terminal width means an unlimited title budget. A real terminal still uses its
own width and erase behavior; that branch is not used by this integration.

AvaloniaEdit does not wrap console lines and provides horizontal scrolling.
The GUI's row-length cap below is a memory limit, not terminal width.
A native binary predating the pipe-output change can still truncate titles.

## Byte parser

Core/Console/ConsoleBuffer accepts raw byte blocks through Append(channel, bytes)
and flushes each independent UTF-8 decoder through Complete(channel).
It is thread-safe and independent of Avalonia/processes.

- CR arms replacement of the entire active update when the next text arrives.
  This deliberately differs from a general terminal: shorter updates must not
  leave an old suffix.
- LF commits a row. CRLF commits exactly one row, even across byte blocks.
- A trailing CR preserves the visible update until more text arrives.
- Partial UTF-8 sequences survive block boundaries. Invalid/incomplete sequences
  become visible replacement characters, including at EOF.
- Truncation preserves Unicode surrogate pairs. The parser is not a complete
  grapheme-aware ANSI terminal.

stdout and stderr have separate active rows, so status updates cannot overwrite
diagnostics. Row ordering reflects first arrival; two independent pipes cannot
provide an exact global order.

## Memory and rendering

Retained output is bounded to 2,000 rows, 8,192 UTF-16 units per row and
1,048,576 UTF-16 units total. Both active rows count; completed rows are evicted
first. Oversized rows remain marked truncated until the next CR/new row.
The 2 MiB text payload budget excludes object/rendering overhead.

Snapshots expose stable row IDs and revisions. ConsoleViewModel refreshes on
the UI dispatcher every 34 ms. ConsoleTextProjection computes incremental
descending-offset edits, reuses unchanged rows and coalesces history evictions.

ConsoleView applies edits in one AvaloniaEdit document update instead of
replacing the whole transcript. Line rendering is virtualized; no list-item
control exists per row. Undo history and scrolling below the document end are
disabled. Following adjusts during layout before rendering.

## Interaction

Use continuous partial/multi-line selection, Ctrl+C, the Copy context menu and
Ctrl+A. There is no Copy all or simulator button. Clear removes history when
not recording, avoiding loss of an active partially decoded sequence.

Manual scrolling up pauses Follow output. Returning to the end, including a
document too short to scroll, enables following. Selecting text pauses display
updates while the bounded parser continues accepting output. Selection takes
precedence over automatic follow: it must not be cleared by new output or
history eviction. Escape clears selection; explicitly enabling Follow output
resumes presentation.

The header keeps the station title on the left and all controls on the right.
Console, rules and metadata-log views use the application theme. stderr is
contrasting red; selection uses readable theme colors. Theme changes apply
without restart.

## Verification

Automated tests cover CR/LF/CRLF, block splits, shorter/longer updates, UTF-8
boundaries and invalid sequences, EOF flushing, row/text limits, concurrency,
independent channels, reset, startup fixtures and incremental projection.

Windows UI checks during implementation covered cross-line selection/copy,
paused output, resuming follow, history eviction, light/dark colors and
large documents. Parser fragmentation tests replace the removed simulator.
Linux rendering/runtime remains unverified.

Run `dotnet test -c Release` from the repository root.
