# DLSS Swapper — session handoff

**Repo:** personal fork (`dkflint723/dlss-swapper`) of `beeradmoore/dlss-swapper`. WinUI 3 / .NET 10,
x64, unpackaged. Treated as a **personal divergence, not upstream PRs** — aggressive refactoring is
fine.

**State:** `main`, all pushed and CI-green. 555 tests (391 app, 164 core). Working tree clean except
`src/Assets/static_manifest.json` and `docs/manifest.json`. Those two are now the **Manifest sync**
workflow's to change rather than a hand edit's (see below), but the staging rule still holds while a
pending copy sits in the tree — `git add -A` will sweep them in, so stage by path.

## Structure

- `core/DLSS.Swapper.Core` — pure `net10.0`, no WinUI. Swap executor, version ranking, `DllTypes`
  registry.
- `tests/DLSS.Swapper.Core.Tests` — 164 tests.
- `tests/DLSS.Swapper.App.Tests` — 391 tests; references the WinUI app directly. Needs
  `resources.pri` (a build target renames the app's `.pri`) or every string lookup throws.
- `TemporaryDatabase` fixture gives tests a **real SQLite database** in temp, via
  `Storage.OverrideStoragePath` + `Database.ResetInstanceAsync` (internal, test-only seams). Debug
  builds otherwise resolve storage to the developer's own library.
- CI: `.github/workflows/tests.yml`, runs both suites on push and PR.
  `.github/workflows/manifest-sync.yml` runs daily and opens a pull request when the dll manifest moves.

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
| Light brand green `#0E8A4F` to `#0C7545` | Twice: white on the spec's value is 4.41:1, and the accent is also used *as text*, where the spec's light values were never checked at all |
| Text tokens raised well above the spec's alphas | Measured, not judged. Secondary was 5.48:1 and tertiary 2.58:1 on dark; tertiary was below the 3:1 floor for anything while carrying counts, sizes and every setting's explanation. Every level is now checked against the lightest surface it can land on |

## Design source

The design lives in a Claude Design project, reachable through the `DesignSync` tool without any
extra login: project `bdbecbe5-df95-4a31-ba69-126debd66a1a`. `list_files` then `get_file`.
`design_handoff_games_page_redesign/SPEC-pages.md` is the page-by-page spec and is much cheaper to
read than the `.dc.html` mockup.

## Next, in order

**The per-game dialog rebuild is done as far as the user can see it:**

- ✅ `UpscalerRowStatus` / `UpscalerRows.For(game)` — the rule that says what each upscaler in a
  game is, and produces the rows *and* the "N upscalers not in this game" line from one split.
- ✅ The action row: eight unlabelled glyphs became three labelled buttons and a named `···` menu,
  with Launch and Update all dlls appearing only when they can act, and `Never update this game`
  reachable from the page it affects for the first time.
- ✅ The upscaler rows. `GameAssetPicker` is deleted along with its duplicate presence rule; the
  rows come from `UpscalerRows.For` and say what is installed, what is available, whether an
  original was kept and whether the dll was found twice.
- ✅ The preset rows, and with them the old "disabled with no reason" item: a preset that cannot be
  set now names which of the three reasons that is.
- ✅ **The leak.** `Hide` removes the control from the root grid.
- ✅ `Update all dlls` runs the preview sheet and the undo strip instead of the old modal
  confirm-progress-summary. `Reset all` deliberately still uses the prompt: it is a destructive
  revert, not a review-then-write.
- ✅ The name's save is a labelled button; the install path is selectable text rather than a
  disabled `TextBox` pretending to be a field.

**What is left is internal, not user-visible.** The surface now reads correctly; these are about
what it costs to change it next time.

1. **Dialog to page.** The only item left, and the only one that is a structural rewrite rather than
   an edit. `FakeContentDialog` exists only because a real `ContentDialog` cannot open over another
   on the same XamlRoot, and this surface opens six. (**The leak is already fixed** — `Hide` now
   removes the control from the root grid — so this is no longer urgent, only right.) As a page the
   workaround goes, every child dialog becomes an ordinary `EasyContentDialog` on `this.XamlRoot`,
   and the `OnApplyTemplate` footer injection disappears entirely.

   **The shape is already mapped**, so this is one pass rather than an exploration.
   `src/UserControls/GameControl.xaml` is 378 lines and splits cleanly:

   - lines 19–40 and 152–159 — resources to keep verbatim.
   - lines 41–140 — the left action row, currently wrapped in a `ControlTemplate`. Becomes the page
     footer's left half. Strip `{TemplateBinding Background}` and the `ContentDialogPadding`.
   - lines 142–150 — Remove / Close. Close becomes "← All games" in the page header, and keeps its
     Escape accelerator.
   - lines 162–376 — the whole content region, moved verbatim into a `ScrollViewer`.

   Then: `GameDetailPage` exposes `ViewModel` and sets `DataContext` to it (the footer uses
   `{Binding}`, the content uses `{x:Bind ViewModel…}`, and both must keep working); `GameControlModel.Close()`
   navigates back instead of hiding; `MainWindow` gains a `ShowGame(Game)` following
   `ShowGamesUsingDll`, constructing fresh per game and **not** caching, with `SectionForPageTag`
   returning `ShellSection.Games` so the sidebar stays put; `GameGridPage.xaml.cs:153` navigates
   instead of calling `ShowAsync`. `FakeContentDialog` itself stays — the manual-add dialog still
   uses it.
2. **Split the model.** `GameControlModel` holds a `WeakReference<GameControl>` read from 12 sites
   *and* reads `NVAPIHelper.Instance` directly — both must go for it to build in a test host.
   `NVAPIHelper` has a private constructor, no reset, and P/Invokes at construction, so it cannot be
   reached from a test at all; that seam is why none of this surface's behaviour is covered.
   **Mostly done**: `PresetAvailability` takes booleans instead of the singleton and is tested,
   and the driver write is one method instead of three copies inside `OnPropertyChanged`. What is
   left is the `WeakReference<GameControl>`, which the page move above removes the need for.


**Also still open, unrelated to the game page:**

- **The Manifest sync workflow needs one repository setting** before it can open anything: Settings
  → Actions → General → "Allow GitHub Actions to create and approve pull requests". Without it the
  fetch and the tests still run and still report; only the pull request step fails, and no token
  change fixes it.
- **Nothing stops dead keys coming back.** The sweep below was a one-off script, not a test. A test
   cannot easily do it: it would have to read the `.resw` from the source tree, and the test host
   only has the compiled `resources.pri`, where duplicates have already collapsed. Re-run the sweep
   by hand after any block of work that deletes a page. The method is in the commit message for the
   prune, and the four dynamic call sites it has to account for are `AccentPalette.NameResourceKey`,
   `GameFilterTab`'s label key, `DllTypeDefinition.DisplayNameResourceKey` and
   `DllUpdatePrompt`'s summary template — all four take literals from a table, so a plain text
   search over every file type does find them.

## Done since

- **Cover art from SteamGridDB**, per game and across the library. The rules live in
  `core/DLSS.Swapper.Core/CoverArt` and are tested there: what to ask for, how to read the answers,
  and when a name match is certain. Only portraits are ever requested — there is one 400x600 slot
  and the wide capsules, heroes, logos and icons have nowhere to go. Static only, because
  SteamGridDB's animated grids are webp and apng and a `BitmapImage` animates gif and nothing else,
  so the choice would have labelled two options that produce the same still. nsfw is fixed off in
  the request *and* on the way in.

  A key is the user's own, with the instructions for getting one in the settings row rather than
  just a box — SteamGridDB issues keys to people, and one shipped in an open source client is one
  anybody can read out of it.

  **"Best" is only ever "first in SteamGridDB's order".** Its `score`, `upvotes` and `downvotes`
  are zero on every result the api returns, and the `order` parameter is ignored — a nonsense value
  is accepted and changes nothing. There is no download count to show and none is claimed.

  The library scan proposes only certain matches and **names the rest**, which can be picked from
  inside the same dialog using the game page's own picker. `CoverArtMatch` is deliberately strict:
  `FINAL FANTASY VII (2013)` against `Final Fantasy VII` is a question, not a match, and this
  library proved the point on the first run.

- **A new dll type can no longer arrive unnoticed.** The swappable dll list is generated by a
  separate repository and was copied in by hand, so the bundled manifest went stale silently and a
  key the registry has no row for was carried verbatim and offered to nobody.
  `DllKeyedRecordsJson.ReadProperty` keeps such a key deliberately, so that saving a user's imported
  manifest cannot corrupt it — which also means nothing at runtime ever reports one.
  `DllTypesTests.EveryPopulatedManifestKey_IsHandledByTheRegistry` is the missing half of the test
  that was already there: the old one asserts every registry key is in the manifest, this one
  asserts every populated manifest key is in the registry, reading the records and the known dll
  hashes because a type can appear in one before the other. An empty key is not a failure —
  `directstorage` and `directstorage_core` have sat empty since before the fork — the first entry
  under one is. `extras/sync_manifest.ps1` fetches the manifest, writes both copies as raw bytes so
  the diff only ever shows real changes, and reports new types separately from new versions;
  `extras/update_manifest.cmd` now delegates to it rather than holding a second copy of the URL. The
  **Manifest sync** workflow runs it daily and force pushes one long-lived branch, so repeated runs
  update one pull request. It runs the core tests **itself** rather than leaving them to `tests.yml`
  on the pull request, because a pull request opened with `GITHUB_TOKEN` deliberately does not
  trigger other workflows and the checks would otherwise never run on it.
- **Grid/list duplication is gone.** `GameStatusView` and `GameActionButton` own the glyph,
  sentence, engines and button. Two controls, not the one the old note called for: the card floats
  the button over the cover and the row keeps it in line, so they cannot share a parent.
- **Launcher sections fold**, persisted per library in `GameLibrarySettings.IsCollapsed`. A search,
  or any tab but "All games", suspends folding so it can look inside folded sections. The heading is
  brought back into view after a toggle: folding removes items rather than hiding them, so the
  content gets shorter and the scroll viewer clamps, and unfolding put the games back above where
  the view now sat — they arrived off the top of the screen.
- **The update preview sheet** (README §3). `PendingDllUpdate.ForGames` builds the rows and
  `DllUpdateRunner.UpdateSelectedAsync` runs exactly those, so the list and the run cannot diverge.
  `UpdatePreviewModel` holds no controls, so its counts and copy are covered by tests.
- **The updating and done/undo strips** (README §4 and §5), replacing the modal progress and summary
  dialogs on the games page. `DllUpdateRunner` now works a flat list of `DllWorkItem` and reports
  which file of how many, and a result keeps the items it wrote so `UndoAsync` can put back that
  batch and nothing else. The game page still uses the old dialogs.
- **`See what changed`** lists what each written file was and what it became. The version a dll was
  *before* is only knowable before it is written over, so `RunAsync` reads it either side of each
  write and the result carries the list — no batch id, no schema change, no new view of the history
  table. It shares the strip's slot with `see what failed`, and the rule keeping them apart lives on
  the model: a partial batch is both, and failures win. An undo clears it, because the batch it
  described is no longer on disk.
- **The upscalers page's shape** (README §8): a column of the nine engines with version counts in
  place of the horizontally scrolling bar, versions as rows rather than cards, and `DllUsage`
  answering how many games have each file in place — the column that says whether it is safe to
  delete. See the unfinished list above for what is still missing from it.
- **The settings page in two columns** (spec §6): what the app does and how it looks on the left,
  what it found and what it is on the right, with `SectionRule` between blocks.
- **`SettingsRow`** — title, one line saying what the setting does, and the control. The page had
  ten copies of a heading, a control and an italic caption *below* it, so a setting could only be
  understood by reading past the thing you were about to change. **Every block on the page uses it
  now**, including the last two that did not: the three preset dropdowns, whose only explanation
  used to be one italic caption under all three; and the game libraries, which are still a
  reorderable `ListView` because dragging them sets the order the games page groups them in. A row
  can carry a leading glyph, which exists for those drag handles — inside the row, so the hairline
  runs its full width.
- **A library row says whether that library is on the machine.** Turning on a library that is not
  installed finds nothing, and there was no way to tell that apart from one that is installed and
  simply has no games with upscalers. `IsInstalled` is asked once per row, in the constructor, since
  it goes to the registry and the disk. `Manually Added` gets no such line — it is not installed
  anywhere, so neither answer is about anything.
- **27 dead resource keys are gone**, 212 entries across 24 translation files, along with a
  duplicate `LibraryPage_Importing` in en-US that predated all of this. Most were the deleted
  dashboard page's. Nine locales were only ever given the older nine of them, which is why the
  per-file counts differ.
- **`NewResourceStringTests`** asserts every resource key this work added resolves. A missing key
  renders as a sentinel string rather than throwing, so nothing else would notice.
- **The empty states** (README §6 and §7). `GamesEmptyState.For` decides between first run, a
  library with no upscaler games, and a search that matched nothing — three causes that a blank
  content area answered identically. An empty *filter tab* is deliberately left blank, since
  "no games with upscalers" would be a lie there and the tab already carries its own count.
- **About names both repositories**, this fork for the code and reports, the original for credit.
  The updater points at this fork, so it will offer this fork's releases and not the original's.
  **The fork now publishes unsigned releases.** `SHOULD_SIGN` in the distribute workflow requires
  `secrets.SIGNPATH_API_TOKEN` to be set as well as a tag, so a tag here takes the unsigned path
  that already existed for untagged builds, and upstream is unaffected. Signing properly is not
  simply a matter of applying to SignPath: the Foundation requires a project to already be released
  and documented, and a second signed binary called DLSS Swapper from a different certificate would
  be worse for users than an unsigned one.
- **Contrast is measured, not judged.** Every text level is checked against the lightest surface it
  can land on, and the accents are checked as text as well as under ink. `AccentPaletteTests` holds
  both rules. If a token is ever changed, re-measure rather than eyeball it.
- **"Which twelve games?" is now askable.** `DllFilter` carries a dll from the upscalers page to the
  games page, from the usage count itself and from the row menu, and both go through
  `MainWindow.ShowGamesUsingDll` so the page exists before it is filtered. It **narrows the tab
  rather than becoming a fifth one**, so the counts had to be narrowed too — left out, "All games"
  would have read 23 and opened onto 3. It lands on "All games" deliberately: arriving into whatever
  tab was last used, narrowed to one dll, is a page empty for two reasons and the user asked for
  one. The count is only a button when there are games behind it; "Not used" is plain text.
  **`GamesOnThePage` is the one place that answers "which games is this page about".** Every count
  and every button that acts on "the games" reads it, after a QA pass caught the review button
  counting the narrowed set and opening the whole library. That includes `Update all games`, which
  while a filter is on means all games *shown* — deliberate, and part of why the chip must stay
  visible.
- **Where each dll file is**, in words rather than only in which buttons a row shows. A row mid
  download says so, which it did not: it read `Not downloaded` for the whole download, and that is
  the one moment the words were false. The 2px bar runs along the row's own bottom edge, on the
  hairline it already draws, so nothing moves when it appears.
- **A row being written shows a bar where its button was** (README §4), built into
  `GameActionButton` because that control already *is* the action slot for both shapes. The card
  keeps a ring instead — its slot is a 28px button on cover art, where a hairline would not be seen.
  That is the only thing the two variants do differently, and it is written down in both files.
- **Versions grouped by release line** (`DllVersionLine`, `DllVersionGroup`), newest three lines
  separate and the rest rolled into one heading. Derived from the version, never stored, and the
  order the page is given is kept rather than re-sorted.
- **The accent picker** (spec §6.1). `AccentManager`, `AccentPalette` and `AccentResolver` already
  existed and were already tested; all that was missing was a way to choose one. Four named
  swatches plus `Match my desktop accent`, repainting live because the brushes the app binds to have
  their colour replaced in place. Theme is a segmented control on the same row shape.
  The swatches rebuild from the page's `ActualThemeChanged`, not from the theme buttons: "use system
  setting" does not say which theme it resolves to, and the effective theme has not settled when the
  command runs.

## Gotchas that cost real time

- **`x:Bind` is `OneTime` by default.** A row's text was rewritten after the row was built and the
  binding had already stopped listening, so it went on saying the opposite of what had happened.
  Anything that changes after first render needs `Mode=OneWay` spelled out.
- **A `ContentDialog` is 548x756 whatever its content asks for, and it clips rather than scrolls.**
  Both caps were hit in one session: at 548 wide a picker lost the two controls furthest right, so
  it rendered as a search box with no Search button; at 756 tall the grid pushed the button row off
  the bottom. Raise `ContentDialogMaxWidth` / `ContentDialogMaxHeight` on that dialog's own
  `Resources`. Size lists to whole rows too — half a row reads as content that failed to load.
- **Three bugs in one session compiled, passed the whole suite, and were only visible by driving
  the app.** The two above and the binding mode. The suites cannot see layout, and `x:Bind` being
  compile-checked only proves the path resolves, not that it updates.

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
- **An `ElementName` binding stops resolving once its element is placed in a `UserControl`'s content
  property**, because it is reparented out of the namescope the name lives in. It fails silently —
  the control renders and the click does nothing. Anything inside `SettingsRow.Control` has to carry
  its own command rather than reach back through the page.
- The XAML type generator emits an activator for every public-constructible type a dependency
  property can reach. A type with `required` members needs a private constructor or the build fails
  in generated code with no obvious link to the control that caused it.
- PowerShell string replacements **silently fail on CRLF**. Verify the edit applied before trusting a
  test result.
- The app can be driven and screenshotted from PowerShell (`SetForegroundWindow` + `CopyFromScreen`,
  `SetCursorPos` + `mouse_event`). Three visual bugs this session were only visible that way.
- **`-p:Platform=x64` builds to `src\bin\x64\Debug\...`, not `src\bin\Debug\...`.** Both exist, and
  the second one is stale. Launching it screenshots an old build and every conclusion drawn from
  that screenshot is wrong. Check the dll's timestamp against the source before believing a
  screenshot that shows a change missing.
- **An `x:Bind` to a function ignores `Converter` entirely**, and fails the build rather than at
  runtime: the function's return type has to be the target's type. So a visibility that comes from a
  computed answer needs a function returning `Visibility`, not a bool plus a converter.
- **A page-level `KeyboardAccelerator` advertises itself** by floating its key under whatever has
  focus. On the games page that put a stray "Esc" tip under the filter chip's dismiss button.
  `KeyboardAcceleratorPlacementMode="Hidden"` goes on the **`UIElement`**, not on the accelerator.
- **A `ToggleSwitch` reserves 154px** whether or not its on/off content needs it. In a 280px column
  that left every library name wrapping to three lines. Give it a `MinWidth` wide enough for the
  longer of the two words, so the row also does not reflow when it is toggled.
- Close the running app before building; it locks the exe.
- `Start-Process` succeeding is not the same as the app surviving. Wait on the process.
