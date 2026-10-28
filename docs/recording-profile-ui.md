# Agreed recording-profile editor and CLI policy

Implemented parameter grouping and argument policy for the reviewed Streamripper
fork. Recheck its actual help/source when updating integration.
The editable default recording profile and all named profiles use the same editor.
Metadata filters are edited separately and assigned directly to stations.

## Outside recording profiles

Application settings owns language, appearance, executable path and the global recording root.
Stations own their name, URL, recording-profile reference, and independent filter
reference. Profiles own the final filename pattern, not the full destination path.

## Application-controlled arguments

- Always enable --metadata-log and --tls-info.
- Supply --metadata-rules only for an explicitly selected compatible station filter.
- Enable --reload-rules automatically with that filter; do not expose a separate
  profile option. Without a selected filter, omit both arguments.
- Compose --output-directory, --output-pattern and --no-stream-directory from the
  agreed root/station/filename layout.
- Do not use --quiet, --config, or --no-port-scan.
- Use native port scanning when relay is enabled; report the actually bound port.
- Pass the requested relay address/port unchanged. Do not inspect or coordinate
  collisions across stations in the GUI; read actual listener endpoints from output.
- Do not offer arbitrary shell commands or an unvalidated free-form argument field.

## Profile sections

| Section | Controls / arguments |
| --- | --- |
| Files | Filename pattern; recording mode: individual tracks / continuous / both (--continuous-file, --no-individual-tracks); original audio or MP3-to-WAV (--wav) |
| Incomplete files and duplicates | Initial incomplete track count (--skip-initial-tracks); preserve existing incomplete files (--keep-incomplete); minimum incomplete duration; overwrite policy always/never/larger/version |
| Connection | Stall timeout; reconnect on/off; User-Agent mode; automatic/IPv4/IPv6 |
| Relay | Enable checkbox; preferred port; maximum clients; advanced bind address |
| Recording limits | Optional duration and received-megabyte limits |
| Tags and metadata | ID3v1/ID3v2 switches; incoming metadata encoding, default AUTO |
| Split points (advanced) | Standard/alternative/off silence detector; boundary offset; previous/next padding; search window; silence length |
| Network (advanced) | Proxy; HTTP/1.0; custom CA file; explicit insecure TLS option; HLS best/worst/target bitrate |

## Initial GUI defaults

- Relay is OFF. Do not pass --relay or its dependent options while disabled.
  Preserve preferred port 8000 and client limit 10 for when the user enables it.
  Merely recording a station must not open a relay listener by default.
- Timeout: 8 seconds.
- User-Agent: compatibility mode, WinampMPEG/5.0.
- Preserve existing incomplete files: enabled.
- Minimum incomplete duration: 15 minutes.
- Filename pattern: %q - %A - %T.
- Other options retain the verified native defaults unless specified above.

## User-Agent selector

Provide three modes:
1. Streamripper default: omit --user-agent and let the selected executable supply
   its own Streamripper/version value. Never hardcode the current CLI version.
2. Winamp compatibility: pass --user-agent WinampMPEG/5.0. This is the GUI default.
3. Custom: show a text field and pass its validated nonempty value.

Persist the mode separately from the custom string; switching modes should not
discard previously entered custom text. Do not infer mode from a display label.
These modes belong to recording profiles and global recording defaults.

## Dependencies and mutually exclusive choices

- IP family is one selector; never send both --ipv4 and --ipv6.
- Recording mode is one selector; prevent disabling both individual and continuous
  output. HLS/FLAC capabilities still determine the actual supported recording mode.
- Derive ID3 flags from two tag switches; do not expose conflicting raw flags.
  WAV applies only to MP3 and disables ID3 output for that session without erasing
  the stored profile's tag preferences.
- Silence detection is one selector. The alternative detector applies to MP3;
  AAC uses the standard detector. Off disables searching, not necessarily every
  independent boundary-offset or padding option.
- Continuous-only recording and relay are not stripped of advertisement audio
  by individual-track metadata filtering; explain this in the UI.
- HLS/FLAC ignore relay with a warning and reject metadata rules. Native Ogg
  comments bypass ICY rules. Apply the capability policy from configuration.md.
- Duration and size limits may coexist; they are not mutually exclusive.
- Use numbering tokens in the custom filename pattern rather than adding an
  independent --sequence option for native default filenames.
- Preserving existing incomplete files does not exempt newly recorded incomplete
  files from the minimum-duration cleanup.
- Format-dependent applicability is based on observed format, never URL guesses;
  preserve the user's saved settings when a session cannot apply them.

## Deferred advanced options

Do not include external metadata commands, debug trace, relay-playlist export,
truncate-duplicates, or filesystem/ID3/relay encoding controls in the initial editor.
External metadata bypasses ICY rules and cannot be combined with --reload-rules.

The inspected CLI names are --no-port-scan and --relay-playlist.
Recheck argument support against the actual executable when updating integration.

## Implemented storage and lifecycle

RecordingOptions is the typed Core model; RecordingArguments validates it and
builds a URL-first argument array. The Windows launcher handles CRT quoting without
a shell. RecordingOptionsView is reused for default and named profiles. Save profile is
enabled and accented only for changed drafts; it validates before atomic JSON writes.
Create profile copies the station's saved effective options, assigns the new named
profile and opens the editor. New profile in the profile tab creates a saved copy
without assigning stations. Renaming preserves IDs.
The launch preview reflects saved options. In-progress recordings keep startup
snapshots even after saving profile changes.
StreamSummaryParser observes real startup bytes independently of the display,
including relay port and explicit IPv4/IPv6 listener forms.

Minimum incomplete duration offers s/m/h units, initially 15 m; storage and CLI
use seconds. HLS variant controls apply only to HLS streams. Non-default options
and their groups get colored stars, not bold notes. The launch preview uses
committed station/profile data, never unsaved profile drafts.
