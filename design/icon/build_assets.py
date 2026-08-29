"""Render one icon concept into every asset the repo ships.

Usage:  python build_assets.py <concept_module> [--repo D:/git_projects/dlss-swapper] [--dry-run]

The geometry table below was measured off the assets already in the repo, so the new art
lands at exactly the size and offset the old art had. That keeps this change to "the
picture is different" rather than "the picture is different and also moved".
"""

import argparse
import importlib
import os
import sys

from PIL import Image

import iconkit as K

# (relative path, canvas w, canvas h, art size as a fraction of the shorter side)
# fraction 1.0 means full bleed.
SQUARE = [
    ("src/Assets/icon_256.png", 256, 256, 1.0),

    ("src/Assets/BadgeLogo.scale-100.png", 24, 24, 1.0),
    ("src/Assets/BadgeLogo.scale-125.png", 30, 30, 1.0),
    ("src/Assets/BadgeLogo.scale-150.png", 36, 36, 1.0),
    ("src/Assets/BadgeLogo.scale-200.png", 48, 48, 1.0),
    ("src/Assets/BadgeLogo.scale-400.png", 96, 96, 1.0),

    ("src/Assets/StoreLogo.scale-100.png", 50, 50, 1.0),
    ("src/Assets/StoreLogo.scale-125.png", 63, 63, 1.0),
    ("src/Assets/StoreLogo.scale-150.png", 75, 75, 1.0),
    ("src/Assets/StoreLogo.scale-200.png", 100, 100, 1.0),
    ("src/Assets/StoreLogo.scale-400.png", 200, 200, 1.0),

    ("src/Assets/LargeTile.scale-100.png", 310, 310, 0.33),
    ("src/Assets/LargeTile.scale-125.png", 388, 388, 0.33),
    ("src/Assets/LargeTile.scale-150.png", 465, 465, 0.33),
    ("src/Assets/LargeTile.scale-200.png", 620, 620, 0.33),
    ("src/Assets/LargeTile.scale-400.png", 1240, 1240, 0.33),

    ("src/Assets/SmallTile.scale-100.png", 71, 71, 0.50),
    ("src/Assets/SmallTile.scale-125.png", 89, 89, 0.50),
    ("src/Assets/SmallTile.scale-150.png", 107, 107, 0.50),
    ("src/Assets/SmallTile.scale-200.png", 142, 142, 0.50),
    ("src/Assets/SmallTile.scale-400.png", 284, 284, 0.50),

    ("src/Assets/Square150x150Logo.scale-100.png", 150, 150, 0.33),
    ("src/Assets/Square150x150Logo.scale-125.png", 188, 188, 0.33),
    ("src/Assets/Square150x150Logo.scale-150.png", 225, 225, 0.33),
    ("src/Assets/Square150x150Logo.scale-200.png", 300, 300, 0.33),
    ("src/Assets/Square150x150Logo.scale-400.png", 600, 600, 0.33),

    ("src/Assets/Square44x44Logo.scale-100.png", 44, 44, 0.75),
    ("src/Assets/Square44x44Logo.scale-125.png", 55, 55, 0.75),
    ("src/Assets/Square44x44Logo.scale-150.png", 66, 66, 0.75),
    ("src/Assets/Square44x44Logo.scale-200.png", 88, 88, 0.75),
    ("src/Assets/Square44x44Logo.scale-400.png", 176, 176, 0.75),

    ("src/Assets/Square44x44Logo.targetsize-16.png", 16, 16, 0.75),
    ("src/Assets/Square44x44Logo.targetsize-24.png", 24, 24, 0.75),
    ("src/Assets/Square44x44Logo.targetsize-32.png", 32, 32, 0.75),
    ("src/Assets/Square44x44Logo.targetsize-48.png", 48, 48, 0.75),
    ("src/Assets/Square44x44Logo.targetsize-256.png", 256, 256, 0.75),

    ("src/Assets/Square44x44Logo.altform-unplated_targetsize-16.png", 16, 16, 1.0),
    ("src/Assets/Square44x44Logo.altform-unplated_targetsize-24.png", 24, 24, 1.0),
    ("src/Assets/Square44x44Logo.altform-unplated_targetsize-32.png", 32, 32, 1.0),
    ("src/Assets/Square44x44Logo.altform-unplated_targetsize-48.png", 48, 48, 1.0),
    ("src/Assets/Square44x44Logo.altform-unplated_targetsize-256.png", 256, 256, 1.0),

    ("src/Assets/Square44x44Logo.altform-lightunplated_targetsize-16.png", 16, 16, 1.0),
    ("src/Assets/Square44x44Logo.altform-lightunplated_targetsize-24.png", 24, 24, 1.0),
    ("src/Assets/Square44x44Logo.altform-lightunplated_targetsize-32.png", 32, 32, 1.0),
    ("src/Assets/Square44x44Logo.altform-lightunplated_targetsize-48.png", 48, 48, 1.0),
    ("src/Assets/Square44x44Logo.altform-lightunplated_targetsize-256.png", 256, 256, 1.0),

    # The docs site. Favicons go full bleed - at 16px every pixel of padding is a
    # pixel of mark you do not get - while the touch icons keep their margin because
    # the platforms round the corners off them.
    ("docs/logo_250.png", 250, 250, 1.0),
    ("docs/favicon-16x16.png", 16, 16, 1.0),
    ("docs/favicon-32x32.png", 32, 32, 1.0),
    ("docs/apple-touch-icon.png", 180, 180, 0.90),
    ("docs/android-chrome-192x192.png", 192, 192, 0.90),
    ("docs/android-chrome-512x512.png", 512, 512, 0.90),
]

# Non square canvases: art is centred, sized as a fraction of the canvas HEIGHT.
WIDE = [
    ("src/Assets/Wide310x150Logo.scale-100.png", 310, 150, 0.33),
    ("src/Assets/Wide310x150Logo.scale-125.png", 388, 188, 0.33),
    ("src/Assets/Wide310x150Logo.scale-150.png", 465, 225, 0.33),
    ("src/Assets/Wide310x150Logo.scale-200.png", 620, 300, 0.33),
    ("src/Assets/Wide310x150Logo.scale-400.png", 1240, 600, 0.33),

    ("src/Assets/SplashScreen.scale-100.png", 620, 300, 0.33),
    ("src/Assets/SplashScreen.scale-125.png", 775, 375, 0.33),
    ("src/Assets/SplashScreen.scale-150.png", 930, 450, 0.33),
    ("src/Assets/SplashScreen.scale-200.png", 1240, 600, 0.33),
    ("src/Assets/SplashScreen.scale-400.png", 2480, 1200, 0.33),
]

# The exe icon. A standard Windows ladder rather than the odd set the old file carried
# (it had 28, 31, 42, 47, 60, 63 and 84 px frames, which no shell surface asks for).
ICO_SIZES = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256]
FAVICON_SIZES = [16, 24, 32, 48, 64]


def supersample_for(px):
    """Small icons need more supersampling to stay clean; huge ones cannot afford it."""
    if px <= 64:
        return 8
    if px <= 256:
        return 4
    if px <= 620:
        return 3
    return 2


def build(draw, repo, dry_run=False):
    written = []

    def place(canvas_w, canvas_h, fraction, path):
        art_px = int(round(min(canvas_w, canvas_h) * fraction))
        img = K.render(draw, art_px, ss=supersample_for(art_px))
        sheet = Image.new("RGBA", (canvas_w, canvas_h), (0, 0, 0, 0))
        sheet.alpha_composite(img, ((canvas_w - art_px) // 2, (canvas_h - art_px) // 2))
        full = os.path.join(repo, path)
        if not dry_run:
            os.makedirs(os.path.dirname(full), exist_ok=True)
            sheet.save(full, optimize=True)
        written.append((path, canvas_w, canvas_h, art_px))

    for path, w, h, frac in SQUARE:
        place(w, h, frac, path)
    for path, w, h, frac in WIDE:
        place(w, h, frac, path)

    for path, sizes in (("src/Assets/icon.ico", ICO_SIZES),
                        ("docs/favicon.ico", FAVICON_SIZES)):
        frames = [K.render(draw, s, ss=supersample_for(s)) for s in sizes]
        full = os.path.join(repo, path)
        if not dry_run:
            K.write_ico(frames, full)
        written.append((path, sizes[-1], sizes[-1], len(sizes)))

    return written


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("concept")
    ap.add_argument("--repo", default=os.path.abspath(
        os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..")),
        help="repo root; defaults to the one this script lives in")
    ap.add_argument("--dry-run", action="store_true")
    args = ap.parse_args()

    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    module = importlib.import_module(args.concept)

    written = build(module.draw, args.repo, dry_run=args.dry_run)
    for path, w, h, extra in written:
        print("  %-64s %dx%d" % (path, w, h))
    print("%s %d assets from %s" % ("would write" if args.dry_run else "wrote",
                                    len(written), args.concept))


if __name__ == "__main__":
    main()
