# Changelog

Changes made in [dkflint723/dlss-swapper](https://github.com/dkflint723/dlss-swapper), a personal
fork of [beeradmoore/dlss-swapper](https://github.com/beeradmoore/dlss-swapper). Entries describe
what changed for someone using the app, and why — the reasoning in full is in the commit messages.

Nothing here has been offered upstream. The fork has published no releases, so there are no version
numbers yet; the sections below are the redesign as it landed.

## Unreleased — interface redesign

Forked at upstream v1.2.5.

### The app says things in words

- **Vendor-coloured badges are gone entirely.** A game's state was carried by colour, which is
  unreadable to anyone who cannot separate red from green. Every state is now a glyph plus a
  sentence, and no meaning anywhere in the app depends on colour alone.
- **A dll version row says where the file is** — on disk, imported, not downloaded, or downloading —
  instead of leaving it to be worked out from which buttons the row happened to show.
- **Every setting says what it does.** Four rows had a title and a control and nothing in between;
  four more had a description that explained the subject without ever stating what the setting
  changed. "Allow Untrusted" described how signature checking works and recommended a value without
  ever saying what turning it on permits.
- **A game's page has one row per upscaler**, each naming the installed version, whether anything
  newer is available, whether the original was kept, and whether the dll was found in more than one
  folder. It was nine dropdowns with that information split between a label and nowhere.
- **A preset that cannot be set says which of the three reasons that is** — no NVIDIA driver, no
  driver profile for this game, or the driver refusing access. All three were collapsed into the
  single word "Not supported".

### Nothing is written without showing you what it will write

- **The update preview sheet** lists every file a run will replace, with its current and new version,
  and lets you deselect any of them.
- **The done strip keeps what it wrote**, so undo puts that batch back and nothing else.
- **"See what changed"** names what each replaced file was and what it became. The version a file was
  before is only knowable before it is overwritten, so it is recorded as the run happens.
- A game's own **Update all dlls** now goes through the same sheet and strip rather than a modal
  confirmation offering only a count.

### Finding things

- **The upscalers page is searchable** across 200-plus versions in nine engines, by version, label or
  file hash. The engine counts follow the search, so the column says where the matches are.
- **Versions are grouped by release line**, newest three lines separately and the rest rolled up,
  rather than a flat wall of near-identical numbers.
- **Each version says how many games use it**, and the count is a button: click it to see which
  games. That is the question worth answering before deleting a file, and the page could not answer
  it at all.
- **Launcher sections fold**, and unfolding one keeps its heading where it was rather than putting
  its games off the top of the screen.

### Cover art

- **A game's page can find its own cover.** It could always take a custom image you already had —
  a button and a drag target — but not help you find one. It now searches SteamGridDB: pick which
  game it is from the matches, then pick a cover from that game's art. Nothing is written until you
  choose one, so a search that finds nothing leaves the cover you had alone and says so, and
  choosing a file from your own disk stays available throughout.
- **The whole library at once.** "Find covers" scans every game shown and proposes one only where
  the name matches beyond doubt. Everything less certain is **listed by name with a reason** rather
  than counted, and can be picked from without leaving the dialog — clicking one opens the same
  picker a game's page uses and returns to the list once a cover is set, so the list can be worked
  down. A batch can be put back in one press.
- **Only the shape that fits.** The app draws one 400x600 portrait, so only portrait art is ever
  fetched. Art flagged as adult, and art flagged for epilepsy, is never offered.
- **A key is your own.** SteamGridDB issues keys to people rather than to applications, so the
  setting says where to get one and links to the page that makes it. Without a key nothing else in
  the app changes.

### Counts that were wrong

Each of these was a count disagreeing with the list it described.

- **"All games" counted hidden games** and then did not show them. Steam and Xbox mark their own
  non-game entries hidden on sight, so the gap was widest on the libraries most people have.
- **The engine counts included debug files the list was hiding** — DLSS read 107 over a list of 88 —
  and the sidebar's total read 213 over a column summing to 186.
- **"Review N updates" counted one set of games and opened onto another** once a dll filter was on.

### Fixed

- **The per-game window leaked.** It added itself to the main window and was only ever hidden, so
  every game opened left one behind, still holding its model, its game and its cover bitmap.
- **Menus opened outside the application window** — the View menu rendered on the desktop above the
  app — and, once constrained, were transparent enough to read the toolbar through. Both fixed.
- **"1 upscalers not in this game"** had no singular form.
- A duplicate resource key and 30 dead ones removed, 300-odd entries across 24 translation files.

Three more were found by running a real swap on a real game, and all three were introduced by the
work above rather than inherited — they are listed because the first is exactly the kind of thing
this redesign exists to prevent, and it survived until something actually wrote to a disk.

- **A game's rows went stale the moment a swap succeeded.** The file on disk changed and the row
  kept describing the version it had before, until the page was reopened.
- **"Update all dlls" on a game's page opened nothing.** It navigated back and then raised the
  preview sheet, but the sheet lives on the page it had just left.
- **"See what changed" and the sheet that offered the change disagreed on format** — `310.6.0.0 →
  310.7.0.0` against `310.6 → 310.7`. One fact in two shapes reads as two facts.

### Shape

- **Three sections in a sidebar**: Games, Upscalers, Settings, each with a count.
- **The games page is one filtering surface.** A separate "Filter" dialog held a worse copy of the
  Hidden tab and a view option; both are gone, grouping moved into the View menu, and the toolbar
  dropped from six buttons to four, none of which decides which games are shown.
- **A game is a page rather than a dialog**, with the game's name as its header. As a dialog it had
  no title at all, and it existed inside a workaround for the fact that a dialog cannot open another
  dialog — which it did six times.
- **Grid cards put their caption below the art** with an accent rule, instead of a gradient darkening
  the cover to keep white text legible over whatever the publisher shipped. Cards gained a title.
- **Settings is two columns**: what the app does and how it looks on the left, what it found and what
  it is on the right.
- **A theme and accent picker**, with the accent applied live.

### Under it

- The swap engine, version ranking and dll registry moved into a **pure `net10.0` core project** with
  no WinUI dependency.
- **A new kind of dll cannot arrive unnoticed.** The list of swappable dlls is generated by a
  separate repository and was copied into this one by hand. A key the app has no entry for is kept
  as-is rather than dropped, so that an imported manifest is never corrupted when it is saved —
  which also meant a newly supported upscaler could be carried along silently and offered to nobody.
  A test now asserts that every manifest key with anything under it is one the app handles, and a
  daily job refreshes the bundled copy and raises a pull request saying whether what arrived is new
  versions of something already supported or something new that needs the app taught about it.
- **555 tests** across the two suites, run in CI on every push. The rules that decide what a page
  shows are tested rather than the pages themselves — counts and the lists they describe come from
  one function, and a test asserts they cannot diverge.
- **The write path has been run end to end on a real game**, not only against forced state: swap
  down to an older version, watch the row and the games list both notice, update back through the
  preview sheet, and confirm the file on disk at each step. That run is what found the three bugs
  above. Tests over forced state had all passed.
- Contrast is **measured** against the lightest surface each text level can land on, including the
  accents used as text. Several tokens are well above the values the design specified because those
  values did not pass.

### Not carried over

- **The updater points at this fork** and is inert, because this fork publishes no releases.
- **About names both repositories** — this fork for the code and for interface reports, the original
  for everything else and for credit.
