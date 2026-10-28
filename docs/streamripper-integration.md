# Streamripper integration

The integration targets [Ashus/streamripper](https://github.com/Ashus/streamripper).
The reviewed source areas are `console/streamripper.c`,
`lib/console_output.c`, native metadata-rule parsing and the fork's
`METADATA-RULES.md`. The development executable/reference locations are recorded
in [AGENTS.md](../AGENTS.md); those locations remain read-only.

GUI argument policy is maintained in [recording-profile-ui.md](recording-profile-ui.md)
and RecordingArguments. Recheck the actual fork's help/source when changing CLI
support; the GUI does not expose every native option.

## Launch and lifetime

Syntax is `streamripper URL [OPTIONS]`; URL is the first argument.
Construct a typed argument array, then use Windows CRT quoting or Linux
ProcessStartInfo.ArgumentList with an explicit executable path. No shell, arbitrary argument field or implicit native INI
configuration is used. Relative GUI paths are resolved before launch.

WindowsRecordingProcess uses CreateProcessW with CREATE_NEW_CONSOLE and a hidden
window. Raw stdout and stderr are separate redirected pipes; stdin is also
redirected. The native process handle remains owned through completion,
including very fast exits. Drain both output streams concurrently until EOF
before finalizing the displayed recording.

Each station owns one process/session and console. Multiple recordings are
independent; selection and searches do not stop them. The native
`incomplete/.lock` additionally protects an output directory from two writers.

A session captures saved URL, destination, profile options and selected rules
path at startup. Later configuration edits do not reconfigure it. GUI status,
process lifetime and native stream diagnostics are distinct; PID is not shown
in the station status.

## Graceful stopping

A short-lived invocation of the GUI in `--internal-stop` mode attaches to the
recording's private console, broadcasts CTRL_BREAK, then detaches.
The main GUI never attaches, so Rider and unrelated consoles do not receive
the signal. Closing stdin is not a stop protocol.

Stop and main-window closure use the same finalization path. Wait up to
15 seconds before offering explicit Force stop; never automatically kill the
writer on timeout. The window remains available while a recording is still
finishing. Force termination may leave incomplete files.

The internal stop helper bypasses the normal single-instance guard.
Normal launches lock the configuration before loading JSON and activate an
existing owner when present.

On Linux, launch the recorder directly with separate raw stdout/stderr pipes.
Stop sends SIGINT only to that recorder PID; the GUI and sibling recordings do
not receive the signal. The native fork also handles SIGTERM. The same 15-second
timeout and explicit Force stop apply; force uses process-tree termination.
The default Linux executable is `/usr/bin/streamripper`, provided by the separate
compatible Debian package. System GLib validates rules when no colocated library
is present.

## Output and format observations

The startup summary contains Connecting, TLS diagnostics, host/server/stream,
`format:`, declared bitrate, metadata interval and optional relay listener.
The user-provided [startup fixture](fixtures/streamripper-hotmix-startup.txt)
preserves visible content rather than original byte framing.

StreamSummaryParser consumes raw stdout independently of display pauses.
It distinguishes audio format, container and transport, including MP3,
AAC/ADTS, Vorbis/Ogg, FLAC and HLS variants. Unknown values remain unknown.
Do not infer capabilities from the URL or imported category.

Observations are saved only while their source URL still matches the station.
Changing URL invalidates them. Reconnect format changes are a remaining
limitation when the native summary is not emitted again.

Current launch adaptations preserve the saved profile:

- WAV is omitted for a known non-MP3 audio format.
- Relay is omitted for known HLS/FLAC.
- AAC uses the standard detector when the alternative MP3 detector was selected.
- Known unsupported metadata rules are omitted, while their assignment remains.

A first connection can explicitly reject custom rules for HLS/FLAC. The GUI
recognizes that native diagnostic and retries once without the rules. It does
not guess this from a URL.

stdout progress uses carriage returns for buffering/ripping/incomplete/filtered
and reconnect updates. New tracks commit lines; shutdown diagnostics remain
visible. stderr carries warnings/errors. The console retains both streams
independently; see [console-design.md](console-design.md).

Runtime native errors can terminate with exit code 0. Zero alone does not prove
success. Preserve the transcript and distinguish requested stop from natural
exit. Nonzero exit is reported with its diagnostics.

## Relay

Pass the requested bind address and port unchanged. The GUI performs no port
reservation, collision checks, cross-station allocation or collision-driven
relaunch. Native Streamripper can search higher ports.

Parse the actual `relay port:` or `relay: address:port` output, including IPv4
and bracketed IPv6. The running session's observed endpoint supplies the link
and copy action. It does not overwrite the profile's preferred port.

## Native metadata rules

Files use UTF-8 native `m/regex/flags` and `s/regex/replacement/flags` syntax.
Action `x` excludes a track; `e` ignores a metadata update. Extraction flags
such as A1T2 map captures to artist/title. Rules are ordered metadata processing,
not an audio-content filter.

ICY MP3/AAC supports these title rules. Ogg comments bypass the ICY parser;
HLS/FLAC rejects custom rules. Relay and continuous recording retain audio
excluded from individual tracks.

The GUI always requests `--metadata-log` and `--tls-info`.
Only an explicitly selected, readable, validated filter enables both
`--metadata-rules` and `--reload-rules`. None omits both and leaves built-in
native parsing available.

Native reload checks file changes while processing audio, approximately every
five seconds in the inspected build. Stalled input can delay it; GUI save is
not a synchronous reload acknowledgement. Streamripper validates the whole
replacement, retains previous rules after rejection, and re-evaluates the last
ICY title after a successful reload. An ongoing track can switch from ripping
to filtered without a new metadata update. Existing audio is not retroactively
deleted. Keep console reload messages/errors visible despite local validation.

Metadata logging appends original changed ICY titles to
`incomplete/metadata.log`, opening/closing the file for each append in the
reviewed implementation. The GUI's explicit Clear truncates it in place and
retries transient sharing conflicts.

## Verification boundaries

Windows checks performed during implementation exercised real local HTTP
MP3/ICY streams, concurrent processes, Unicode destinations, pipe output,
graceful stop/close-all, native rule reload and current-title filtering.
Automated tests cover parsing, quoting-related argument construction, capability
observations and configuration behavior.

Debian 13 amd64/WSL checks also exercise system GLib, argument and UTF-8 preservation,
independent SIGINT/force stop, fast exits, stable lock/activation/restart, and the
installed GUI with concurrent local ICY MP3 sessions, rule reload, close-all and
persisted format observations. Long-duration real-radio behavior, broader format/
reconnect coverage and additional Linux environments remain on the
[checklist](checklist.md).
