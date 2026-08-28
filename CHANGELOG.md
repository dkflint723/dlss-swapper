# Changelog

Changes made in [dkflint723/dlss-swapper](https://github.com/dkflint723/dlss-swapper), a personal
fork of [beeradmoore/dlss-swapper](https://github.com/beeradmoore/dlss-swapper). Entries describe
what changed for someone using the app, and why — the reasoning in full is in the commit messages.

Nothing here has been offered upstream. Builds from this fork are **unsigned**; the original
project is the signed, official one.

**Versions from 2.0.0.0 onward are this fork's own count** and have nothing to do with upstream's
number. Before that the scheme was upstream's version with this fork's count in the fourth part —
1.2.5.1 being the first build on top of upstream's 1.2.5 — which stopped working the day upstream
released a 1.2.6.0 of its own: two different builds wearing one number, and this fork's reading as
*older* than an upstream release it already contained all of. Starting a separate line at 2 keeps
them apart without anyone having to remember a rule. It stays four plain numbers because the
updater packs them into 16 bits each, so a suffix like `-fork.3` would silently stop update checks
working.

## v2.2.0.0 — the app closes the loop

A feature release, and the first with features of this fork's own rather than repairs and
refinements of what was there. One thesis runs through all five: the app's job used to end at the
file write, while the user's job ends when the game runs well — these close the gap.

**Restore originals, for the whole library.** The sidebar has always said "Originals kept for 24 of
24 games"; acting on that promise meant opening every game one at a time. One toolbar button now
puts everything back — scoped to the games shown like every bulk action, skipping games marked
leave alone like every bulk write, and confirming with every dll named: what each is now, what it
goes back to, in a list that scrolls. For the moments that call for a clean slate: a driver
rollback, a support thread, handing the machine on.

**The app says so when a game throws a swap away.** A game update overwrites the dll you swapped
in, and the scan that notices also deletes the stranded backup — so the swap and the way back both
vanished silently, recorded only in a history dialog nobody opens. A warning bar on the games page
now names each game and dll it happened to. Closing the bar acknowledges exactly what it showed;
a swap undone next month reopens it.

**A dll can be pinned where it is, with the reason written down.** You roll a game back because the
newest build ghosts, and nothing remembers — so next month's update-all offers the bad version
again. "Never update this game" existed but is all-or-nothing. A pin holds one dll: no batch moves
it — not an update run, not a restore run — while the picker on the game's own page always can.
The row says it in words: *v310.1 installed and pinned there — v310.7.129 exists — newer builds
ghost in this game*, the last clause being whatever you wrote when you pinned it.

**The version wall gained a curated lane.** A hundred near-identical numbers, and the knowledge of
which two matter lived on forums. A Recommended group now leads the list on the Upscalers page and
in the picker, each entry carrying its why — the 310.x transformer line, and 3.8.10 as the last
CNN build and the fallback when the transformer misbehaves. Every claim names an exact build, and
a test fails the build if an entry points at a version the shipped manifest does not carry.

**Launch, then restore originals.** The swap you do not want to still be there next week: play the
session swapped, and the shipped versions return the moment the game closes. The confirmation
names every dll before anything is agreed to; a strip narrates the watch in words; and every
uncertain path — the app closing, the game never starting, the watch stopped — leaves the files
exactly as they are, which is where every game is without this feature.

Also: the revert summary now has a sentence for every count. "Restored 4 dlls across 1 games" is
gone, and the one-dll case — which resolved a resource key nothing defined and showed an error in
the exact case singled out for better wording — reads properly, with tests holding every
concatenated key to the resources for good.

## v2.1.0.0 — the app meets you halfway

A user-experience release: two adversarial review passes over the whole app, every finding either
implemented and verified on screen, or declined with the reason recorded in the commit.

**Flows finish what the press asked for.** Swapping a not-yet-downloaded version downloads and then
swaps, in one motion. The dll picker gets the search box and release-line groupings the Upscalers
page already had, and a selection the filter hides disarms Swap. Reset all lists every dll it will
touch — what each is now and what it goes back to — before asking for a yes. Version rows on the
Upscalers page offer Download in the dialog the row opens, instead of hiding it in an overflow menu.
Exports end with "Show in folder".

**First launch is honest.** The page says "Looking for your games…" while the first scan runs instead
of offering to start it; empty filter tabs state their truth instead of a blank canvas; the anti-cheat
note appears before your first swap rather than over the loading screen; adding a game by hand shows
one note, not two stacked ones.

**Settings speak one language.** The SteamGridDB key is validated before it is saved, with the API's
own answer under the box — a mistyped key used to be saved silently and fail every later search. The
DLSS developer options got sentences; ignored paths say what ignoring means, with Remove visible; the
dll-list toggles sit together; titles share one style.

**It is faster where you feel it.** The game list appears before cover art finishes loading; grid
covers decode at the size they are drawn; unchanged covers are a cache hit across the session instead
of a fresh disk read on every scroll; Refresh walks your dlls without re-downloading every cover.

**And it stops guessing.** A failed update check says it could not reach GitHub instead of "no new
updates"; one malformed Xbox config no longer removes every other Xbox game from the list; a failed
scan no longer hides the game it failed on.

## v2.0.0.0 — a version number of its own

A small release. Its reason for existing is the version number, and one thing that number was
getting wrong.

**The installer was naming the wrong release.** The version is written in four files by hand and
only two of them were ever bumped, so the 1.2.6.0 build stamped **1.2.5.1** into its file
properties and into the entry it writes to *Add or remove programs* — the place you look to see
what you have installed. Fixed, and a test now reads all four files and fails when they disagree,
so it cannot happen quietly again.

**This fork counts its own versions now**, starting here. It used to carry upstream's version with
its own count in the fourth part, which collided the day upstream released a 1.2.6.0 of its own —
two different builds wearing one number, and this fork's reading as *older* than an upstream
release it already contained all of. Nothing about the app changes; the number just stops being
ambiguous.

Also in this build:

- **DLSS 310.7.129**, in both the served manifest and the copy bundled with the app. Upstream
  updated only the first of those in their own release, so a fresh install here starts with the
  newer list rather than waiting to fetch it.
- **Upstream's Japanese retranslation**, minus the sixteen strings it carries for UI this fork
  removed. Better wording throughout, and one real correction: the "NVIDIA recommended" preset had
  been labelled "always use the latest version".
- **A zip that tries to write outside the import folder is refused on any operating system**, not
  only on Windows. The check asked the running platform what counted as a path separator, which
  meant the rule was true where the app ships and quietly false everywhere it was tested.

## v1.2.6.0 — keeping your original dlls

Forked at upstream v1.2.5. Takes upstream's DLSS 310.7.128 entries and its DLSS D Preset F.

Most of this release is one theme: several ways the app could destroy the copy of a dll your game
shipped with — the file that lets you put a game back the way it came. None of them announced
themselves, and most needed nothing unusual to happen.

### Your saved originals

- **Ignoring a folder no longer deletes what is inside it.** Every launcher's scan skips a game in an
  ignored path, and every launcher then deletes the games its scan did not return, assuming they were
  uninstalled — which deletes their saved originals. So adding an ignored path destroyed the original
  dll of every game underneath it on the very next refresh. The game still leaves the app; the files
  stay, and un-ignoring the path finds them again.
- **A scan no longer invents an original.** When an installed dll stopped matching the version on
  record, the scan deleted the saved original and then immediately wrote a new one from whatever was
  installed at that moment — which could be a dll the app itself had swapped in and failed to record.
  Reverting would then have restored the swapped dll and reported success. The row now says plainly
  that there is no saved original, and "Save a copy" takes a fresh one when you ask.
- **"Save a copy" covers every location of a dll.** A game shipping the same dll in two folders had
  the first copied, the second skipped, and success reported — and the row then read as protected
  while that second location had nothing saved anywhere. The same wrong question was being asked in
  four places, so the list, the row and the sidebar all agreed.
- **Deleting a saved original is written down before the file goes**, so an interrupted scan can no
  longer destroy the copy and the note that it did.
- **A failed swap removes a dll it created.** When a target file was missing, the swap wrote one and
  could not undo that, so a swap that failed part way left a dll in your game folder while reporting
  that nothing had changed.

### Your settings and your imported dlls

- **An interrupted write no longer costs you either.** Settings, the imported dll list and the cached
  update check were each written by emptying the real file first, so a crash, a power cut or a full
  disk left nothing behind. Settings were then silently replaced with defaults — api key, ignored
  paths, library order, language, theme. The imported dll list disabled importing permanently, with
  the dlls still on disk and nothing left saying what they were. All three are now written beside the
  real file and moved over it, so it is either entirely the old one or entirely the new one.
- **A settings file that cannot be read is left alone.** A file held open for a moment by an
  antivirus used to read as "no settings yet" and get overwritten. Being unreadable and being absent
  are now different answers.
- **Imported dlls survive upgrading.** A dll that existed only in your imported list was skipped by
  the upgrade to the current layout, then read as missing and removed — it disappeared from the
  library on the first launch after updating, silently. Only genuinely custom dlls could hit this,
  which are the ones that cannot be downloaded again.
- **A zip that writes outside the import folder is refused.** Entry names inside a zip are chosen by
  whoever made it, and are not always a plain file name.

### Things that were not true

- **A game with no upscalers says so** instead of "Up to date", which was a claim about DLSS being
  current in a game that has no DLSS.
- **The update count clears when the batch finishes.** "Review 13 updates" stayed on screen over rows
  that all read up to date, and then did nothing when pressed.
- **Searching survives clicking a filter tab.** The box kept your text over a list that had stopped
  honouring it.
- **Update prompts come back.** Closing one update dialog suppressed every future release
  permanently — the check asked whether it had prompted before and answered the question backwards.
- **Two games from different launchers that share an id are two games.** Owning a Steam game and a
  Ubisoft game with the same number made one silently overwrite the other's name and install path.

### Cover art

- **Apply cannot run twice.** Pressing it again re-ran games already done, overwriting the backup of
  your own cover with the one the scan had just written — and undo then deleted your cover outright.
- **Stopping part way keeps undo**, and says how many were applied. It used to say nothing at all and
  hide the button, then delete the backups when the dialog closed.
- **Undo says how many it actually put back** rather than always claiming success.
- **A stalled search gives up** instead of sitting silent, and a scan that is stopped keeps what it
  found rather than throwing it away.

### Interface

- **A game opens over the list rather than instead of it.** Closing it leaves the list exactly where
  it was — same scroll position, same search, same tab — so looking at several games in a row is no
  longer a round trip through a list that resets each time. Escape, the back button and clicking away
  all close it.

### Under it

- The library is no longer rewritten to the database on every launch — measured at 25 writes per
  start before, none now unless something actually changed.
- Setting a game's title during a scan could throw and take the rest of that launcher's scan with it,
  so a game renamed or moved to another drive stopped the whole library being scanned.
- The installed build walked its own dll cache on every launch to refresh a number in Windows' Apps
  list. Once a week now.

## v1.2.5.1 — interface redesign

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

- **The updater points at this fork**, so it offers this fork's releases rather than the original's
  — which would otherwise replace this build with one that does not have any of these changes.
- **About names both repositories** — this fork for the code and for interface reports, the original
  for everything else and for credit.
