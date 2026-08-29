"""Before/after review sheet: the old icon and the new one under the same conditions.

    python review.py concept_slug [--out review.png]

Shows both marks down the size ladder on light and dark, a nearest-neighbour blow up of
the 16px render so you can count the pixels that actually survive, and the colour vision
panels. The point is to make a regression obvious rather than to flatter the new art.
"""

import argparse
import importlib
import os
import sys

from PIL import Image, ImageDraw, ImageFont

import iconkit as K

REPO = os.path.abspath(
    os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
SIZES = (16, 20, 24, 32, 48, 64, 128, 256)
LIGHT = (243, 243, 243, 255)
DARK = (32, 32, 34, 255)


def old_frames(path=None):
    """Pull each size out of the icon currently in the repo, nearest one up."""
    path = path or os.path.join(REPO, "src/Assets/icon.ico")
    have = sorted(s[0] for s in Image.open(path).info["sizes"])
    out = {}
    for want in SIZES:
        pick = next((h for h in have if h >= want), have[-1])
        im = Image.open(path)
        im.size = (pick, pick)
        im = im.convert("RGBA")
        out[want] = im if pick == want else im.resize((want, want), Image.LANCZOS)
    return out


def font(px, bold=False):
    try:
        return ImageFont.truetype("C:/Windows/Fonts/segoeui%s.ttf" % ("b" if bold else ""), px)
    except OSError:
        return ImageFont.load_default()


def ladder(sheet, d, y, images, ground, label):
    pad = 16
    row_h = max(SIZES) + pad * 2
    d.rectangle([0, y, sheet.width, y + row_h], fill=ground)
    ink = (30, 30, 30, 255) if ground == LIGHT else (215, 215, 215, 255)
    d.text((pad, y + 8), label, font=font(13, True), fill=ink)
    x = 150
    for s in SIZES:
        cell = s + pad * 2
        sheet.alpha_composite(images[s], (x + pad, y + (row_h - s) // 2))
        d.text((x + cell // 2, y + row_h - 12), str(s), font=font(11), fill=ink, anchor="mm")
        x += cell
    return y + row_h


def taskbar(sheet, d, y, new16, old16, ground):
    """A crude taskbar strip. Icons sit at 16px on a 40px bar, which is the real case."""
    bar_h = 44
    d.rectangle([0, y, sheet.width, y + bar_h], fill=ground)
    ink = (30, 30, 30, 255) if ground == LIGHT else (215, 215, 215, 255)
    d.text((16, y + bar_h // 2), "taskbar", font=font(12), fill=ink, anchor="lm")
    x = 150
    for label, im in (("old", old16), ("new", new16)):
        for _ in range(3):
            # neighbouring app icons, drawn as plain rounded blocks for scale reference
            d.rounded_rectangle([x, y + 14, x + 16, y + 30], radius=3,
                                fill=(120, 128, 140, 255))
            x += 30
        sheet.alpha_composite(im, (x, y + 14))
        d.text((x + 8, y + 38), label, font=font(9), fill=ink, anchor="mm")
        x += 30
        for _ in range(3):
            d.rounded_rectangle([x, y + 14, x + 16, y + 30], radius=3,
                                fill=(120, 128, 140, 255))
            x += 30
        x += 40
    return y + bar_h


def build(draw_fn, out, title, old_path=None):
    new = {s: K.render(draw_fn, s, ss=8 if s <= 64 else 4) for s in SIZES}
    old = old_frames(old_path)

    pad = 16
    row_h = max(SIZES) + pad * 2
    width = max(150 + sum(s + pad * 2 for s in SIZES), 1100)
    zoom = 176
    height = 34 + row_h * 4 + 44 * 2 + 30 + zoom + 46 + 30 + 256 + 40

    sheet = Image.new("RGBA", (width, height), (250, 250, 250, 255))
    d = ImageDraw.Draw(sheet)
    d.text((pad, 9), title, font=font(16, True), fill=(15, 15, 15, 255))

    y = 34
    y = ladder(sheet, d, y, old, LIGHT, "old  light")
    y = ladder(sheet, d, y, new, LIGHT, "NEW  light")
    y = ladder(sheet, d, y, old, DARK, "old  dark")
    y = ladder(sheet, d, y, new, DARK, "NEW  dark")
    y = taskbar(sheet, d, y, new[16], old[16], DARK)
    y = taskbar(sheet, d, y, new[16], old[16], LIGHT)

    # 16px, blown up with no interpolation: the honest small-size test.
    y += 22
    d.text((pad, y - 14), "16px actual pixels, magnified", font=font(13, True),
           fill=(15, 15, 15, 255))
    x = 150
    for label, im in (("old", old[16]), ("NEW", new[16])):
        for ground in (LIGHT, DARK):
            block = K.on_ground(im, ground).resize((zoom, zoom), Image.NEAREST)
            sheet.alpha_composite(block, (x, y))
            d.text((x + zoom // 2, y + zoom + 11), "%s %s" % (label, "light" if ground == LIGHT else "dark"),
                   font=font(11), fill=(40, 40, 40, 255), anchor="mm")
            x += zoom + 14
        x += 24

    # Colour vision, on the new mark only - the old one is being replaced anyway.
    y += zoom + 40
    d.text((pad, y - 14), "new mark, colour vision and greyscale", font=font(13, True),
           fill=(15, 15, 15, 255))
    x = 150
    panels = [("normal", new[256]),
              ("deuteranopia", K.simulate_cvd(new[256], "deuteranopia")),
              ("protanopia", K.simulate_cvd(new[256], "protanopia")),
              ("greyscale", K.greyscale(new[256]))]
    for label, im in panels:
        small = im.resize((190, 190), Image.LANCZOS)
        sheet.alpha_composite(K.on_ground(small, DARK), (x, y))
        d.text((x + 95, y + 202), label, font=font(11), fill=(40, 40, 40, 255), anchor="mm")
        x += 204

    sheet.convert("RGB").save(out)
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("concept")
    ap.add_argument("--out", default="review.png")
    ap.add_argument("--title", default=None)
    ap.add_argument("--old", default=None)
    args = ap.parse_args()
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    mod = importlib.import_module(args.concept)
    path = build(mod.draw, args.out, args.title or ("Swapshelf icon - " + args.concept),
                 old_path=args.old)
    print("wrote", path)


if __name__ == "__main__":
    main()
