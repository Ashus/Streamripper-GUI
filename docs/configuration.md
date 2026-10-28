# Configuration and OPML

## Files and storage mode

The active configuration directory contains:

| Entry | Contents |
| --- | --- |
| `config.json` | Application settings, default/named recording profiles and filter catalog |
| `stations.json` | Stations, independent profile/filter references and observed stream information |
| `rules/*.txt` | Real UTF-8 native metadata-rule files |
| `.instance.lock` | OS-held single-instance lock; removed on Windows close, persistent inode on Linux |

On Windows, user mode uses `%LOCALAPPDATA%/StreamripperGui`.
On Linux it uses `$XDG_CONFIG_HOME/StreamripperGui`, normally
`~/.config/StreamripperGui`. The Linux lock file can remain after exit; its presence
alone does not mean another instance is running.
An existing `portable.flag` beside the GUI executable selects that executable
directory instead. There is no automatic copying between modes.

Executable, recording-root and additional-CA paths resolve from
`AppContext.BaseDirectory`, independent of the process working directory.
When saved, portable mode stores them relatively; user mode stores them
absolutely. A portable path on another drive is rejected because it cannot
remain relative. Existing absolute/relative paths are readable and normalized
on their next save. An empty initial recording root becomes `recordings` in
portable mode or the user's Music/Streamripper directory otherwise.

Rule-file paths resolve from the **active configuration directory**, not the
executable directory. They must stay inside its `rules/` subtree.

## Configuration schema

Both JSON files use `schemaVersion: 1`. The following is a valid compact example;
omitted recording-option properties use the defaults in RecordingOptions.

```json
{
  "schemaVersion": 1,
  "application": {
    "language": "auto",
    "theme": "auto",
    "streamripperPath": "streamripper/streamripper.exe"
  },
  "recording": {
    "directory": "recordings",
    "defaults": {
      "filenamePattern": "%q - %A - %T",
      "timeoutSeconds": 8,
      "keepIncomplete": true,
      "incompleteMinDurationSeconds": 900,
      "relayEnabled": false,
      "relayPort": 8000,
      "userAgentMode": "winamp"
    },
    "profiles": [
      {
        "id": "profile-long-recording",
        "name": "Long recording",
        "options": {
          "filenamePattern": "%q - %A - %T",
          "incompleteMinDurationSeconds": 0
        }
      }
    ]
  },
  "metadataRuleSets": [
    {
      "id": "rules-hotmix",
      "name": "Hotmix",
      "file": "rules/hotmix.txt"
    }
  ]
}
```

Profile options are complete independent settings in memory, not patches over
the current default profile. Saving writes their typed fields. Application
language is `auto/en/cs`; appearance is `auto/light/dark`.
[Window geometry](window-state.md) is stored at `application.window`.

```json
{
  "schemaVersion": 1,
  "stations": [
    {
      "id": "station-hotmix",
      "name": "Hotmix Lounge",
      "url": "https://example.com/lounge",
      "recordingProfileId": null,
      "metadataRuleSetId": "rules-hotmix",
      "importedName": null,
      "lastObservedStream": null
    }
  ]
}
```

IDs are stable strings. Null `recordingProfileId` selects the editable global
default; null `metadataRuleSetId` selects None and supplies no custom rules file.
A non-null unknown ID is an error. References belong to stations independently,
never to recording profiles.

`importedName` remembers an original import name after automatic collision
renaming, so subsequent imports can update its URL. A manual rename clears it.
Description and imported category are not stored.

An observed stream can contain:

```json
{
  "observedAt": "2026-10-27T12:00:00+00:00",
  "transport": "unknown",
  "audioFormat": "MP3",
  "container": "unknown",
  "trackTitles": "available",
  "metadataRules": "supported",
  "declaredBitrate": 320,
  "metadataInterval": 16000,
  "relayPort": 8000,
  "relayAddress": null,
  "nativeFormat": "MP3"
}
```

The observation does not duplicate URL. The running session retains its source
URL only in memory to reject stale observations. A station URL change clears
the cached observation. Format comes from native output, never from the URL,
OPML category or file extension. Cached relay information is not a live link;
the UI shows only a currently active session's actual listener.

## Persistence and recovery

- Station fields save on blur. Language/appearance choices save immediately;
  paths save on blur, picker selection and successful Settings closure.
- Profiles save explicitly; filters validate and save explicitly. Committed
  catalog/assignment changes write immediately, including startup rule discovery.
- Only window geometry is deferred until successful main-window closure.
- JSON uses a temporary file, flush-to-disk and atomic replacement, retaining
  the previous version as `.bak`. Native rule files use atomic replacement
  **without** `.bak`.
- Unknown JSON extension fields survive supported edits. Obsolete station
  description/category fields and the former observation URL are removed on save.
- Invalid structures, unsupported schemas or inconsistent references block
  saving to protect the existing files. Correct the files or restore a backup
  and restart. General schema migrations are not implemented.
- File writes are individually atomic, not a transaction across config/stations.
  Create catalog entries before assigning them; detach station references before
  deleting catalog entries. A later failure can leave an unused entry or detached
  stations and is reported. It must not create dangling references.

SingleInstance owns an exclusive lock before loading JSON. A second launch sends
activation through a current-user-only named pipe, then exits without reading
or saving configuration. The OS releases the lock, including on a crash;
DeleteOnClose removes its file.

## Rule catalogs and imports

At startup only, unreferenced top-level `.txt` files in the active `rules/`
directory are registered in the filter catalog. Names come from filenames
without extensions; collisions receive (2), (3), etc. Matching normalizes paths
and ignores case on Windows. `.TXT` is accepted. There is no periodic rescan.

Registration does not rewrite files or require valid regexes. Invalid discovered
files remain editable; recording start still validates selected rules.

Settings also supports importing from another folder: choose a folder, select
TXT files and optionally edit their proposed names. Validate all selections
before copying their original UTF-8 bytes into new uniquely named files in the
active `rules/`, then commit the catalog. Existing/source files are never
overwritten. Failed imports remove their new copies. Import does not assign
filters to stations or discard an existing editor draft.

Confirmed filter deletion detaches stations, removes its catalog entry and
deletes its TXT file. A temporary staged file allows rollback if the catalog
write fails. There is no ignored-file list and no retained deleted TXT.

A single Hotmix filter can be assigned to all matching Hotmix stations, and the
identical 1.FM rules can likewise be shared. The reference files were reviewed
read-only; the application does not automatically consolidate, migrate or rewrite
them. There is no wildcard/include extension to the native rules syntax.

## OPML reconciliation

Settings opens the native file picker first. After a valid `.opml` or `.xml`
file is parsed, show the selection/preview modal with actual entries.
Canceling the picker opens no modal; malformed input leaves stations unchanged.

OpmlReader accepts unnamespaced OPML 1.0, 1.1 and 2.0, traverses nested outlines,
and reads station `text` and HTTP(S) `url` attributes. Optional description
appears only in import selection. Category is ignored. This covers the reviewed
MusicBee export; arbitrary other OPML dialects are not claimed.
The committed sample is [fixtures/musicbee-stations.opml](fixtures/musicbee-stations.opml).

The preview updates automatically after any selection or replacement change.
Display entries alphabetically, while original source order determines duplicate
precedence. The last selected duplicate wins, ignoring name case and surrounding
whitespace. There is no duplicate-resolution prompt or URL-based matching.

- **Merge:** update URLs of matching names and preserve all other stations.
- **Replace:** reconcile every selected station first, then remove unmatched old
  stations. Existing IDs and profile/filter assignments survive matching.
- Exact current display names take priority over imported-name aliases.
- Automatic name/directory collisions receive predictable (2), (3), etc.
  Record the imported-name alias for repeat reconciliation.
- Changed URLs invalidate observations internally; the preview does not show
  a separate technical format-invalidation note.
- Removal of an actively recording station blocks import. Other saved URL
  updates leave the running session's startup URL unchanged.
- Import re-plans at commit, then atomically writes the station collection.
  Cancel/error leaves the previous collection unchanged.
- Profiles, filter catalogs/files and recorded audio survive either mode.
