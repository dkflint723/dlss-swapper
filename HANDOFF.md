# DLSS Swapper — session handoff

**Repo:** personal fork (`dkflint723/dlss-swapper`) of `beeradmoore/dlss-swapper`. WinUI 3 / .NET 10,
x64, unpackaged. Treated as a **personal divergence, not upstream PRs** — aggressive refactoring is
fine.

**State:** `main`, all pushed and CI-green. 322 tests (211 app, 111 core). Working tree clean except
`src/Assets/static_manifest.json` and `docs/manifest.json`, which predate the work and have been
deliberately excluded from every commit — `git add -A` will sweep them in, so stage by path.

## Structure

- `core/DLSS.Swapper.Core` — pure `net10.0`, no WinUI. Swap executor, version ranking, `DllTypes`
  registry.
- `tests/DLSS.Swapper.Core.Tests` — 111 tests.
- `tests/DLSS.Swapper.App.Tests` — 211 tests; references the WinUI app directly. Needs
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

## Design source

The design lives in a Claude Design project, reachable through the `DesignSync` tool without any
extra login: project `bdbecbe5-df95-4a31-ba69-126debd66a1a`. `list_files` then `get_file`.
`design_handoff_games_page_redesign/SPEC-pages.md` is the page-by-page spec and is much cheaper to
read than the `.dc.html` mockup.

## Next, in order

1. `src/Pages/InitialLoadingPage.xaml` is orphaned — nothing navigates to it, and the first-run
   state it was supposed to become now lives on the games page. Safe to delete.
2. **Settings is half done.** Every repeated row is now a `SettingsRow` with its explanation beside
   the name. Still to do from spec §6: the two column layout (560px main, 280px side), sections
   under uppercase rules, theme as a segmented control rather than radio buttons, the accent swatch
   row and `Match my desktop accent`, and moving Game libraries and About into the side column.
   `GameLibrarySelectorControl`, the ignored paths list and the DLSS preset block are untouched and
   still use the old shape.
3. **The upscalers page is not finished.** Still missing from spec §4.3: versions grouped under
   uppercase rules by major line (`DLSS 310`, `DLSS 3.7`, `DLSS 3.5 and older`), the state column
   (`On disk` / `Not downloaded` as words beside their glyph — right now state is only implied by
   which buttons a row shows, which is icon-only), the size column, and the per-row overflow menu.
   Clicking a usage count should filter Games to those titles.
4. **Two pieces of the update flow are deliberately not built.** The per-row progress bar in the
   action slot (README §4 says a row being written shows a 150px bar where its button was; it still
   shows the sentence and a spinner), and `See what changed` on the done strip, which needs a
   history view filtered to a batch and there is no such view yet.
5. The strip overlays the last of the content rather than shortening it, so the final row can sit
   behind it until you scroll. Docking it properly means the content row giving up 52px while it is
   there.
6. The grid card still diverges from spec §2.6, which puts the caption *below* the art with a 2px
   accent rule down its left edge, rather than in a gradient over it. Not yet reasoned about either
   way.

## Done since

- **Grid/list duplication is gone.** `GameStatusView` and `GameActionButton` own the glyph,
  sentence, engines and button. Two controls, not the one the old note called for: the card floats
  the button over the cover and the row keeps it in line, so they cannot share a parent.
- **Launcher sections fold**, persisted per library in `GameLibrarySettings.IsCollapsed`. A search,
  or any tab but "All games", suspends folding so it can look inside folded sections.
- **The update preview sheet** (README §3). `PendingDllUpdate.ForGames` builds the rows and
  `DllUpdateRunner.UpdateSelectedAsync` runs exactly those, so the list and the run cannot diverge.
  `UpdatePreviewModel` holds no controls, so its counts and copy are covered by tests.
- **The updating and done/undo strips** (README §4 and §5), replacing the modal progress and summary
  dialogs on the games page. `DllUpdateRunner` now works a flat list of `DllWorkItem` and reports
  which file of how many, and a result keeps the items it wrote so `UndoAsync` can put back that
  batch and nothing else. The game page still uses the old dialogs.
- **The upscalers page's shape** (README §8): a column of the nine engines with version counts in
  place of the horizontally scrolling bar, versions as rows rather than cards, and `DllUsage`
  answering how many games have each file in place — the column that says whether it is safe to
  delete. See the unfinished list above for what is still missing from it.
- **`SettingsRow`** — title, one line saying what the setting does, and the control. The page had
  ten copies of a heading, a control and an italic caption *below* it, so a setting could only be
  understood by reading past the thing you were about to change.
- **`NewResourceStringTests`** asserts every resource key this work added resolves. A missing key
  renders as a sentinel string rather than throwing, so nothing else would notice.
- **The empty states** (README §6 and §7). `GamesEmptyState.For` decides between first run, a
  library with no upscaler games, and a search that matched nothing — three causes that a blank
  content area answered identically. An empty *filter tab* is deliberately left blank, since
  "no games with upscalers" would be a lie there and the tab already carries its own count.
- **About names both repositories**, this fork for the code and reports, the original for credit.
  The updater points at this fork, and is inert until the fork publishes a release.

## Gotchas that cost real time

- **Five bugs in one session were the same shape:** a value computed once while something else later
  changed the truth. `GameManager.GamesChanged` exists so consumers observe rather than get told —
  prefer it.
- **Anything duplicated will get half-changed.** Three view predicates, two row templates, two header
  templates — each bit at least once.
- **Measure, do not infer.** Three wrong diagnoses: a "XAML parse error" that was an unsafe
  constructor call, a revert based on misread raw database bytes, and three wrong theories about a
  click handler. Each time, one bisect or one log line gave the answer in minutes.
- **A `GridView` sizes every cell from the first item it measures.** Collapse the first card and the
  whole grid goes to nothing — every section, not just that one. A `ListView` is fine with it. Any
  "hide the row" idea has to be checked in the grid before it is believed.
- `ContainerContentChanging`'s `ItemIndex` counts through all groups in order, so it maps back to a
  group by walking `CollectionGroups` sizes. `ContainerFromIndex` does **not** agree with it, and
  the realised panel holds headers among the rows, so its child order is not the item order either.
- **A group heading that hides while its group is empty never comes back.** It returns only when the
  group's membership changes, and rebuilding the whole content control does not help. So any rule
  for hiding a heading has to be one where the heading only ever reappears alongside its items.
- The XAML type generator emits an activator for every public-constructible type a dependency
  property can reach. A type with `required` members needs a private constructor or the build fails
  in generated code with no obvious link to the control that caused it.
- PowerShell string replacements **silently fail on CRLF**. Verify the edit applied before trusting a
  test result.
- The app can be driven and screenshotted from PowerShell (`SetForegroundWindow` + `CopyFromScreen`,
  `SetCursorPos` + `mouse_event`). Three visual bugs this session were only visible that way.
- Close the running app before building; it locks the exe.
- `Start-Process` succeeding is not the same as the app surviving. Wait on the process.
