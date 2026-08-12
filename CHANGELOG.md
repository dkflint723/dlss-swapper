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
- **452 tests** across the two suites, run in CI on every push. The rules that decide what a page
  shows are tested rather than the pages themselves — counts and the lists they describe come from
  one function, and a test asserts they cannot diverge.
- Contrast is **measured** against the lightest surface each text level can land on, including the
  accents used as text. Several tokens are well above the values the design specified because those
  values did not pass.

### Not carried over

- **The updater points at this fork** and is inert, because this fork publishes no releases.
- **About names both repositories** — this fork for the code and for interface reports, the original
  for everything else and for credit.
