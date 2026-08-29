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

## What it reads

`list` reports the library as the app last saw it. It loads games from the cache rather than
rescanning every install folder of every library — that walk is the app's job, not something a
caller asking one question should pay for. Run a scan in the app if you need the freshest state.

## Building

```
dotnet build cli/DLSS.Swapper.Cli/DLSS.Swapper.Cli.csproj -c Debug -p:Platform=x64
```

It references the app project and needs its `resources.pri` beside the executable, which the
csproj copies after build — without it the first translated string throws rather than coming back
untranslated.
