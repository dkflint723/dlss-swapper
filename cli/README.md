# dlss-swapper-cli

A headless way in to the same swap the app performs, so other things — a Steam client plugin, a
script, a scheduled job — can change dlls without reimplementing what a swap is allowed to do.

It does no swapping of its own. It loads what the app loads and calls the same methods the buttons
call, so the rules hold automatically: the files a type owns, the transactional write and its
rollback, the per-path saved original that is never overwritten, pins, the version ranking FSR
breaks if you get it wrong. A second implementation of those in another language would drift, and
the way that shows up is wrong files written into game folders.

## Output contract

**stdout is always one JSON object, whatever happened** — including failures, which carry
`ok: false` and an `error` rather than only an exit code. Read stdout and check `ok`. The exit code
agrees with it, for shell use. Diagnostics and stack traces go to **stderr** and never to stdout.

Every response carries `contractVersion`. Callers should refuse a version they do not know rather
than guess at a field: this ships separately from anything reading it, so the two will be
mismatched eventually, and saying so plainly beats swapping something nobody asked for.

Current contract version: **1**.

## Commands

```
dlss-swapper-cli list
dlss-swapper-cli scan [--force]
dlss-swapper-cli swap --game <id> --type <type> --version <version> [--force]
dlss-swapper-cli restore --game <id> [--type <type>]
dlss-swapper-cli version
dlss-swapper-cli help
```

`--game` takes the id from `list`, or a title if it is unambiguous — an ambiguous title is refused
rather than guessed at, because the next thing this does is write into a game folder.

`--type` takes the manifest key (`dlss`, `dlss_g`, `dlss_d`, `dlss_nr`, `fsr_31_dx12`, `fsr_31_vk`,
`xess`, `xess_fg`, `xess_dx11`, `xell`) or the enum name (`DLSS_G`).

`restore` with no `--type` restores every dll in the game that has a saved original.

## Pins

A pin means no batch moves that dll. The picker inside the app may, because pressing it is a
deliberate act on one named file in front of you; a call arriving from a script or another process
is not, so **`swap` refuses a pinned dll** and says why, including the reason the pin was given.
`--force` overrides it.

`list` still reports a pinned dll as `behind` when a newer version exists — pinned is not the same
as current, and a caller deserves to know both.

## scan

`list` reports the library as the app last saw it. `scan` is what changes that: it looks at Steam
again and writes what it finds into the same database the app loads at startup, so a game found
here is a game the app has.

**Steam only, and that is deliberate.** Every other library scan reaches a different launcher's
files, and one of them — Rockstar — installs copies whose dlls must never be swapped at all,
because the launcher's integrity check stops the game launching if they change. A command that
quietly walked every library would be doing considerably more than its name says.

Games somebody added to Steam themselves are included, since Steam plays those like anything else
and they hold the same dlls. Steam never writes an `appmanifest_*.acf` for one, so they are read
from `shortcuts.vdf` instead, and they carry the same app id Steam's own library page uses — which
is what lets a caller looking at a game page match it to a game here.

`--force` re-reads every game's folder rather than only the ones that look changed. It is the answer
to "it should have found something and did not".

The reply names what changed, so a caller does not have to diff two lists:

```json
{
  "ok": true, "contractVersion": 1, "scanned": "steam", "forced": false, "incomplete": false,
  "games": 22,
  "added": [
    { "id": "steam_1245620", "title": "ELDEN RING", "installPath": "...",
      "nonSteamShortcut": false, "hasSwappableItems": true }
  ],
  "removed": []
}
```

`incomplete` is true when dll detection was still running when it gave up waiting. The games are
saved either way; some may not have had their dlls recorded yet. Detection runs on the thread pool
and the scan does not return until it finishes, because returning earlier would report a game with
none of its dlls found and exit mid-write.

## What it reads

`list` reports the library as the app last saw it. It loads games from the cache rather than
rescanning every install folder of every library — that walk is the app's job, not something a
caller asking one question should pay for. Run `scan` above, or a scan in the app, when you need
the freshest state.

## Building

```
dotnet build cli/DLSS.Swapper.Cli/DLSS.Swapper.Cli.csproj -c Debug -p:Platform=x64
```

It references the app project and needs its `resources.pri` beside the executable, which the
csproj copies after build — without it the first translated string throws rather than coming back
untranslated.

## Where it ships

Into the app's own install folder, beside `DLSS Swapper.exe`, by both packaging scripts — so an
installed app has `C:\Program Files\DLSS Swapper (dkflint723)\dlss-swapper-cli.exe` and a portable
one has it beside the app in the zip. The NSIS file list is generated by walking the publish folder,
so nothing in `Installer.nsi` names it, and the uninstall log picks it up the same way.

Both scripts publish the cli **first** and the app **second**, into the one folder. The cli
references the app project, so the two publishes very nearly produce the same set of files;
publishing the app second means anything they both produce — `resources.pri` above all — is the
app's copy rather than one built for the cli. What survives from the first step is
`dlss-swapper-cli.exe`, its `.dll`, and its two json files.

The portable script builds it `Release_Portable` like the app beside it, because that configuration
is what defines `PORTABLE` in the app project this references, and that is what decides where the
database is looked for. A `Release` cli in a portable build reads a different library than the app
it shipped with — which is visible immediately: a portable build reports an empty library where an
installed one reports the real thing.
