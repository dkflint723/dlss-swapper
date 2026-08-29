"""Swapshelf app icon.

Two identical hooks - a horizontal board with a stub dropping off one end - placed in
180 degree point symmetry about the centre, on a deep navy rounded square. Neither hook
points anywhere; they simply reach past each other, and the gap left between them is a
single Z-shaped channel of one constant width. That channel is the whole idea: two
pieces milled to swap places, a shelf board each, no arrows.

Built from two rounds of judged concepts. The geometry is the 'interlock' variant's,
which won on meaning; the pixel discipline is the 'sixteen' variant's, which won on
small sizes. What is new here is the palette and the edge, which were the two things
none of the variants solved.

The palette is chosen against measured constraints rather than by eye, because the
person this is for is red/green colour blind and the two hooks must not differ by hue
alone:

    white vs amber      3.37:1  they differ in VALUE, not just colour  (was 1.78:1)
    amber vs tile       3.48:1
    white vs tile      11.73:1
    tile vs dark bar    1.39:1  carried by the rim below, not by the fill
    tile vs light bar  10.57:1
    rim vs dark bar     5.55:1

Under simulation the two hooks stay apart at 3.07:1 in deuteranopia and 3.92:1 in
protanopia, where the drafts sat near 1.8:1 and depended on the reader knowing which
one was supposed to be orange.

The earlier drafts all tried to fix the vanishing dark-taskbar silhouette by lifting the
tile, which cost the hooks the contrast they need against it - every unit of lift on the
tile is a unit off the marks. The edge is cheaper: a crisp one-device-pixel rim, drawn
only from 40px up where there is a pixel to spend on it. Below that the mark carries
itself, which is what it was already doing.
"""

import iconkit as K

# ------------------------------------------------------------------ palette --
# Deep enough that both hooks have room above it, and it stays recognisably navy
# rather than drifting to the mid blue every other Windows tile already is.
TILE_HI = "#2A4A72"
TILE_LO = "#101F33"
RIM = "#6C9AD0"

COOL = "#EAF2FB"
COOL_HI = "#FFFFFF"
# Deliberately darker than the amber the drafts converged on. They all lightened it
# to pop against the navy, which is right for the tile and wrong for the eye this is
# for: it pushed the two hooks together in value, the one axis colour blindness reads.
WARM = "#C9791A"
WARM_HI = "#E08C1E"

SHADOW = "#02070D"

SNAP_BELOW = 57                     # below this every edge lands on a whole pixel
RIM_FROM = 40                       # no rim until there is a pixel to draw it with
SHADOW_FROM = 96                    # and no shadow until it can be seen rather than smeared
PROPORTIONS = (0.155, 0.18, 0.09)   # inset, board thickness, channel


def _hook(C, t, x0, x1, top, reach, r):
    """A board from x0 to x1 at `top`, thickness t, with a stub dropping at its right end."""
    L = C.layer()
    L.rect(x0, top, x1, top + t, radius=r)
    L.rect(x1 - t, top, x1, reach, radius=r)
    return L


def _turned(C, t, x0, x1, top, reach, r):
    """The same hook rotated 180 degrees about the centre."""
    L = C.layer()
    L.rect(1000.0 - x1, 1000.0 - top - t, 1000.0 - x0, 1000.0 - top, radius=r)
    L.rect(1000.0 - x1, 1000.0 - reach, 1000.0 - x1 + t, 1000.0 - top, radius=r)
    return L


def geometry(px):
    """Layout in units. The channel leads and every other edge is measured off it."""
    if px < SNAP_BELOW:
        u = 1000.0 / px
        f_top, f_t, f_gap = PROPORTIONS
        top = max(2, int(round(px * f_top)))
        t = max(2, int(round(px * f_t)))
        # The channel straddles the midline, so it only lands on pixel boundaries when
        # it shares the icon's parity - and never under two pixels, because one pixel
        # of dark centred on a boundary renders as two grey ones.
        gap = min((n for n in range(2, px) if (n - px) % 2 == 0),
                  key=lambda n: abs(n - px * f_gap))
        stag = int(px / 2 - top - gap / 2 - t)
        stag = max(1, stag - 1 if px <= 18 else stag)
        top, t, gap, stag = top * u, t * u, gap * u, stag * u
        r = 24.0 if px <= 20 else (30.0 if px <= 32 else 38.0)
    else:
        top, t, gap, stag, r = 150.0, 178.0, 92.0, 130.0, 42.0

    x1 = 500.0 + gap / 2.0 + t          # the stub's outer edge, half a channel off centre
    x0 = 1000.0 - x1 - stag
    reach = 1000.0 - top - t - gap      # the tip stops exactly one channel short
    return t, x0, x1, top, reach, r


def draw(C):
    px = C.px
    small = px <= 32
    t, x0, x1, top, reach, r = geometry(px)

    up = _hook(C, t, x0, x1, top, reach, r)
    lo = _turned(C, t, x0, x1, top, reach, r)

    # ---------------------------------------------------------------- container
    tile = C.layer()
    tile.rect(45, 45, 955, 955, radius=194 if small else 208)
    C.paint(tile, K.Linear(TILE_HI, TILE_LO, angle=115))

    # The silhouette on a near-black taskbar, bought with one device pixel rather than
    # by lifting the whole fill. A stroked outline, not a glow: the earlier attempt used
    # a blurred perimeter and read as a focus ring around a button.
    if px >= RIM_FROM:
        edge = C.layer()
        edge.rect(45, 45, 955, 955, radius=194 if small else 208,
                  fill=False, width=1000.0 / px)
        C.paint(edge, RIM, opacity=0.85)

    # Painted after the tile and NOT behind it. Every draft had this as behind=True,
    # which composites underneath everything already drawn - including the opaque tile -
    # so the shadow rendered exactly zero pixels in all of them.
    if px >= SHADOW_FROM:
        C.paint(up, SHADOW, blur=13, offset=(0, 9), opacity=0.42)
        C.paint(lo, SHADOW, blur=13, offset=(0, 9), opacity=0.42)

    if small:
        C.paint(up, COOL_HI)
        C.paint(lo, WARM_HI)
    else:
        C.paint(up, K.Linear(COOL_HI, COOL, angle=90))
        C.paint(lo, K.Linear(WARM_HI, WARM, angle=90))
