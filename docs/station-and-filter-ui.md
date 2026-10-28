# Station and filter interactions

## Main window and stations

Stations, Profiles and Filters are the main tabs. Global Settings is a separate
fixed-size modal opened from the footer gear. There is no top menu or toolbar.
The plus icon sits beside the station-list heading.

Search filters inactive station names on every text change. Active stations have
a separate pinned list above the scrollable inactive group, a green indicator
and their status. Newly pinned stations become visible immediately. Inactive
rows omit Ready and remain compact. Full statuses are available as tooltips.

Stations, profiles and filters use current-culture alphabetical ordering,
ignoring case. Default profile and None filter remain first. Punctuation order
can differ from another application's ordinal or language-specific comparer.

A double-click toggles recording. Right-click anywhere on a row selects it and
opens Start, Stop and Remove actions for that station. Selecting another station
while recording is allowed and switches its detail and console. A search hiding
an inactive selection does not discard its detail or console.

The detail starts with Recording: controls, destination, and the current relay
link if available. Station configuration follows below. Neighboring fields have
a gap; short profile options use two columns.

Station name, URL and profile/filter selection save on blur. There are no
Save station/Cancel changes buttons. Invalid drafts remain visible with an error.
The selected station's own fields are disabled while it records; profile catalogs
can still be edited for future recordings.

Removing a station requires confirmation with primary/default Yes. Removal
deletes only its station entry, never audio, logs, profiles or filters. Stop an
active recording before removal.

## Output paths and relay

A session uses:

```text
global recording root / safe station name / profile filename pattern
```

The root is an application setting. Profiles store only the filename pattern.
The station directory sanitizes invalid/control characters, reserved Windows
names, trailing dots/spaces and traversal components, preserving usable Unicode.
Names are unique ignoring case/outer whitespace. Sanitized-directory collisions
rename the later station with (2), (3), etc., and persist that visible name.

The GUI prevents directory injection through names/patterns.
Percent tokens are passed literally; they are not CMD-style doubled.
Streamripper chooses the extension. Existing recordings are not moved on rename.
The destination is shown with a folder icon opening the system file manager.
Selecting an idle station checks its current target directory and shows the path
only if that directory already exists, even before the first recording in this
GUI session. Saving a station rename or recording-root change repeats this check.
These checks do not create directories and do not change a running session's
destination. After stopping, the recording destination remains visible until
the next check.

Relay is disabled by default. When active output reports a listener, show
`Relay: http://.../` and a copy icon. Use the observed address/port, not the
requested profile value or cached observation. IPv4 wildcard/port-only output
uses localhost; IPv6 wildcard uses ::1; concrete addresses are retained and IPv6
uses brackets. Hide the link on stop/reset. The station format description
does not repeat relay information.

## Profiles and deletion

Default is editable except for its name and cannot be deleted. Stations selecting
it use its saved settings on their next start. Named profiles are complete
independent copies with stable IDs, not live inheritance from Default.

New profile immediately creates and saves a uniquely named copy of the selected
saved profile, selects it, and leaves station assignments unchanged.
Create profile on a station copies its current effective saved settings, names
the copy after the station, assigns it and opens Profiles. It is available only
while the station selects Default. The equivalent Create filter action is
available only with None selected. These are reusable entries; UI text does not
emphasize shared ownership.

Save profile is enabled/accented only when its name/options have changed.
Normalization after save resets the dirty state. Non-default values and their
groups get a colored star; notes are not emphasized. For named profiles the
baseline is the saved default profile; for Default it is the factory defaults.

Renaming a profile/filter retains its ID and updates selectors without rewriting
station references. Named entries can be removed even when assigned.
Confirmation names the item, uses a profile/filter-specific question, and lists
affected stations alphabetically in an automatically scrollable area.
An unused item gets a smaller nonresizable modal without detach wording.

Confirmed deletion changes profile assignments to Default and filter assignments
to None. Filter deletion also deletes its TXT file; profile deletion does not
delete recordings. Running sessions are not stopped or reconfigured. The UI
rechecks affected assignments after confirmation and reports changes/errors.

## Native filter editor

Selecting a station on the Profiles or Filters tab selects that station’s assigned
profile or filter in the editor. Stations without an assignment use the default
profile or show None for filters. Opening either tab also synchronizes its editor
with the currently selected station.

Switching an editor selection or starting a new profile/filter with an unsaved
draft opens a modal with Save (primary and Enter), Discard changes, and Return to
editing (Escape and window close). Save uses the regular profile persistence or
filter validation/save path. A validation or write failure keeps the draft and
cancels switching. Cancel also restores the previous selection. When station
selection requires switching a dirty editor, cancellation keeps the previous
station selected as well. Switching between tabs alone preserves inactive drafts;
they are checked when their editor would load another item.

None is immutable and supplies no custom rules file; Streamripper's built-in
parsing remains enabled. Named filters refer to real UTF-8 files under `rules/`.
New filter starts a draft with this template:

```text
# Optional: exclude titles containing the standalone word advert.
# m/\badvert\b/ix

# Optional: remove the trailing text mp3pro.
# s/\s*-?\s*mp3pro$//i

# Artist/title extraction must remain last.
m/^\s*(.*?[^-\s])\s+-\s+(.*)\s*$/A1T2
```

The final extraction matters because custom files replace the built-in rules.
Without extraction the full text becomes the title.

The editor highlights native commands, comments, delimiters, regexes,
replacements and flags. Draft validation is debounced and uses GLib beside the
selected CLI for regex compilation. Native action/extraction flags such as
`/x` and `A1T2` are parsed separately. Highlighting alone is not validation.

The regex editor provides Cut, Copy and Paste in its context menu. Cut and Copy
require a selection; Cut and Paste are disabled for an immutable editor. Windows
Ctrl+Insert, Shift+Delete and Shift+Insert shortcuts perform the same operations.
Right-clicking moves the caret to the pointer before opening the context menu.
A click inside the current selection preserves it; a click outside clears it,
so Paste inserts at the clicked position rather than replacing an old selection.

Global validator failures have no invented line/column and do not paint the
first comment red. Actual syntax errors identify their location; stale
diagnostics are cleared when a draft is replaced.

Validate and save changes is enabled/accented only for changed drafts, including
name-only changes. It validates the complete file before an atomic write.
Errors preserve the draft and last valid disk contents. Blur never saves.
Rule-save feedback checks whether a running session actually started with that
file; it does not promise a fixed reload time. Native console diagnostics remain
authoritative for reload acceptance and filtered status.

Add rule offers a full-width literal-text field, At start of title, At end of
title, Replace text and Exclude track. Helpers escape literal regex punctuation
and the native slash delimiter. The start/end options add `^`/`$` to both
substitution and exclusion patterns; selecting both matches the whole title.

- Exclusions (`m/.../x`) are inserted before the first active substitution (`s/`).
  If there is no substitution, they go before the first active rule. If there
  are no active rules, they are appended after any comments. Leading comments
  and blank lines remain at the top; commented-out examples do not count as rules.
- Substitutions (`s/.../.../`) are inserted before the **last** active match
  rule (`m/`), or appended if there is no active match rule. This is intended
  to keep the final artist/title extraction last; the insertion code does not
  check whether that last match rule actually performs extraction.

Helpers insert whole rules without replacing or reordering existing content,
preserve LF/CRLF line endings and move the caret after the inserted rule.

## Metadata log and console

The metadata pane reads the selected station/session's
`incomplete/metadata.log`. The path is not shown in the UI.
Display timestamps/raw log text; Prepare selected line for adding decodes the
original StreamTitle and its escaped characters for literal-rule input.

Read a bounded tail (262,144 bytes), ignoring a partial final record. Handle
missing, truncated/recreated files and I/O errors without fabricating a log.
Following pauses for selection/manual scrolling. Scrolling back to the bottom
automatically enables Follow changes again, as in the console, unless text is
still selected. Manually enabling it clears the selection and jumps to the end.
Programmatic refreshes do not turn a manually disabled checkbox back on.

Clear is enabled only if the log exists, including while following is paused.
It truncates that existing file in place, with bounded retries for transient
sharing locks; it does not delete/replace/create it. Streamripper appends future
changed titles normally. Its current-title deduplication means the same title
need not immediately reappear.

The independent process console supports continuous partial/multi-line text
selection and keyboard/context-menu copying. Controls stay right-aligned while
the station title changes. It has no Copy all or simulator controls.
See [console-design.md](console-design.md) for parser/rendering details.

## Settings and modal behavior

Settings contains the recording directory first, then two import columns
(stations/filters), language and appearance side by side, executable path and
a grey read-only configuration directory. Paths have native picker icons.
There is no portable-mode technical explanation or duplicate global heading.

Settings saves valid changes as they occur and on closure. One primary/default
Close action supports Enter; Escape also closes. A missing executable is
allowed so a deleted/unavailable installation cannot trap the dialog. Empty
or syntactically invalid paths and write failures show an error and prevent
successful closure.

Every modal has one accent-colored Enter/default action; Escape cancels/closes.
Import commits only with its primary Import action. Deletion uses Yes.
