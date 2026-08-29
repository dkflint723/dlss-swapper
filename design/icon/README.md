# Swapshelf icon

The app icon is generated, not drawn in an editor. The source of truth is
[`swapshelf_icon.py`](swapshelf_icon.py); every `.ico` and `.png` in `src/Assets` and
`docs` is rendered from it.

## Regenerating

```bash
python design/icon/build_assets.py swapshelf_icon
```

That rewrites all 64 assets — the exe icon, the MSIX tile set, the title bar image and
the docs favicons — at exactly the sizes and offsets they already had. Add `--dry-run`
to see what it would touch without writing anything.

Requires Pillow and numpy. There is no ImageMagick, Inkscape or cairosvg dependency;
the drawing kit in [`iconkit.py`](iconkit.py) does its own rasterising, its own colour
maths and writes the `.ico` container itself, because Pillow's ICO writer downsamples a
single source image for every frame and that throws away the per-size tuning this icon
depends on.

## Reviewing a change

```bash
python design/icon/review.py swapshelf_icon --out review.png
```

Produces a sheet with the mark down the size ladder on light and dark grounds, a
simulated taskbar, the 16px render magnified with no interpolation, and deuteranopia,
protanopia and greyscale panels. Look at it before committing an icon change. An icon
that only works at 256px on white has not been checked.

## The design, and the constraints it is under

Two identical hooks — a horizontal board with a stub dropping off one end — in 180
degree point symmetry about the centre. They reach past each other, and the gap between
them is a single Z-shaped channel of one constant width. That channel carries the idea:
two pieces milled to swap places, a shelf board each, and no arrows anywhere.

Three things constrain any change to it:

**It has to survive 16x16.** That is the taskbar, alt-tab and Explorer, and it is where
the icon actually lives. Below 57px `geometry()` snaps every edge to a whole device
pixel and holds the channel at a two pixel floor, because one pixel of dark centred on a
pixel boundary renders as two grey ones and the interlock closes up. The mark is drawn
slightly differently at small sizes on purpose; that is optical scaling, not a bug.

**The two hooks must not differ by hue alone.** The maintainer is red/green colour
blind. White against amber is 3.37:1 in greyscale, 3.07:1 under simulated deuteranopia
and 3.92:1 under protanopia, so the two pieces separate by *value* and not only by
colour. An earlier draft used a brighter amber that looked better against the navy and
measured 1.78:1 — it depended on the reader already knowing which piece was orange.
If you change the palette, re-measure with `iconkit.contrast_ratio` and the
`simulate_cvd` helper rather than trusting your eye.

**The tile has to hold an edge on a near-black taskbar.** It does that with a crisp
one-device-pixel rim from 40px up, not by lifting the fill. Lifting the fill was tried
and it costs the hooks the contrast they need against their own background — every unit
of lift on the tile is a unit off the marks.

## Provenance

Six concepts were drawn and judged on small-size legibility, colour vision and name fit;
the geometric direction was chosen. Five refinements were then drawn against named
defects and judged again. This mark takes its geometry from the `interlock` refinement,
which won on meaning, and its pixel discipline from `sixteen`, which won on small sizes.
The palette and the rim are new here — they were the two problems none of the
refinements solved, because each one optimised a single axis and slid sideways on
another.
