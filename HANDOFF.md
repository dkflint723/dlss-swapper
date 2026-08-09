# DLSS Swapper — session handoff

**Repo:** personal fork (`dkflint723/dlss-swapper`) of `beeradmoore/dlss-swapper`. WinUI 3 / .NET 10,
x64, unpackaged. Treated as a **personal divergence, not upstream PRs** — aggressive refactoring is
fine.

**State:** `main`, 38 commits ahead of upstream, all pushed and CI-green. 272 tests (161 app, 111
core). Working tree clean except `src/Assets/static_manifest.json`, which predates the work and has
been deliberately excluded from every commit.

## Structure

- `core/DLSS.Swapper.Core` — pure `net10.0`, no WinUI. Swap executor, version ranking, `DllTypes`
  registry.
- `tests/DLSS.Swapper.Core.Tests` — 111 tests.
- `tests/DLSS.Swapper.App.Tests` — 161 tests; references the WinUI app directly. Needs
  `resources.pri` (a build target renames the app's `.pri`) or every string lookup throws.
- `TemporaryDatabase` fixture gives tests a **real SQLite database** in temp, via
  `Storage.OverrideStoragePath` + `Database.ResetInstanceAsync` (internal, test-only seams). Debug
  builds otherwise resolve storage to the developer's own library.
- CI: `.github/workflows/tests.yml`, runs both suites on push and PR.

## Conventions established

- **View models take data, not their control.** `DashboardPageModel`, `ShellSidebarModel`,
  `GameGridPageModel` follow this; `GameControlModel` predates it and cannot be built in a test host.
- **One rule, one place.** Counts and the lists they describe must come from the same function.
  `GameFilters.Matches` drives both tab counts and tab contents, with a test asserting they cannot
  diverge.
- **Colour is never load-bearing.** The user is red/green colourblind. Vendor-coloured badges were
  removed entirely; state is a glyph plus a sentence.
- Comments explain *why*, and record what was tried and rejected.

## Divergences from the design handoff

All deliberate, all reasoned in the relevant commit messages.

| Change | Reason |
| --- | --- |
| Radius 6/8, not 0 | User preference, after seeing the options rendered |
| No Downloads page | Downloads are sequential and take seconds, so the page would always be empty |
| Portrait 44x66 covers, not 96x54 | The cache holds 400x600 art; cropping a poster to 16:9 is unrecognisable |
| Light brand green `#0E8A4F` to `#0E874F` | The spec's own 4.5:1 contrast requirement was not met by its own value |

## Next, in order

1. **Collapse the duplicated grid/list templates.** Extract a `GameStatusView` control (glyph,
   sentence, engines, action button) rather than merging templates — they share meaning, not layout.
   This caused three separate misses in one session; do it before touching rows again.
2. **Collapsible launcher sections.** Click path is *proven* working
   (`sender=Button, tag=GameGroup`). Unsolved piece: empty groups. Do not flip `HidesIfEmpty` — hide
   rows instead, so "empty" keeps its original meaning.
3. Remaining handoff steps: preview sheet, undo strip, Upscalers page, Settings page, first-run
   state.

## Gotchas that cost real time

- **Five bugs in one session were the same shape:** a value computed once while something else later
  changed the truth. `GameManager.GamesChanged` exists so consumers observe rather than get told —
  prefer it.
- **Anything duplicated will get half-changed.** Three view predicates, two row templates, two header
  templates — each bit at least once.
- **Measure, do not infer.** Three wrong diagnoses: a "XAML parse error" that was an unsafe
  constructor call, a revert based on misread raw database bytes, and three wrong theories about a
  click handler. Each time, one bisect or one log line gave the answer in minutes.
- PowerShell string replacements **silently fail on CRLF**. Verify the edit applied before trusting a
  test result.
- Close the running app before building; it locks the exe.
- `Start-Process` succeeding is not the same as the app surviving. Wait on the process.
