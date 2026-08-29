"""A small drawing kit for building the Swapshelf app icon.

There is no ImageMagick, Inkscape or cairosvg on this machine, so the icon is drawn
with PIL and committed as its own generator rather than as an opaque binary.

Everything is drawn in a 1000x1000 unit square and scaled to whatever pixel size is
asked for, supersampled and downsampled with LANCZOS, so one concept renders sharp at
every size from 16 to 512.

Shapes go onto a `Layer` (an alpha mask), and a mask is then painted with a colour or a
gradient. Separating the two is what makes glows, shadows and gradient fills easy:

    C = Canvas(256)
    disc = C.layer()
    disc.ellipse(500, 500, 460, 460)
    C.paint(disc, Linear("#1B2A4A", "#0B1220", angle=90))
    C.paint(disc, "#000000", blur=24, offset=(0, 12), opacity=0.5, behind=True)
    img = C.image()
"""

import io
import struct

from PIL import Image, ImageDraw, ImageFilter, ImageFont

UNITS = 1000.0


# ---------------------------------------------------------------- colour ----

def parse_colour(value):
    """'#rgb', '#rrggbb', '#rrggbbaa' or an (r, g, b[, a]) tuple -> RGBA tuple."""
    if isinstance(value, (tuple, list)):
        c = tuple(int(round(v)) for v in value)
        return c if len(c) == 4 else c + (255,)
    s = str(value).strip().lstrip("#")
    if len(s) == 3:
        s = "".join(ch * 2 for ch in s)
    if len(s) == 6:
        s += "ff"
    if len(s) != 8:
        raise ValueError("bad colour %r" % (value,))
    return tuple(int(s[i:i + 2], 16) for i in (0, 2, 4, 6))


def mix(a, b, t):
    """Blend two colours in linear light, which keeps midpoints from going muddy."""
    ca, cb = parse_colour(a), parse_colour(b)
    out = []
    for i in range(3):
        la = (ca[i] / 255.0) ** 2.2
        lb = (cb[i] / 255.0) ** 2.2
        out.append(255.0 * ((la + (lb - la) * t) ** (1 / 2.2)))
    out.append(ca[3] + (cb[3] - ca[3]) * t)
    return tuple(int(round(v)) for v in out)


def luminance(value):
    """Relative luminance 0..1 (WCAG). The number that decides small-size legibility."""
    c = parse_colour(value)
    parts = []
    for v in c[:3]:
        v = v / 255.0
        parts.append(v / 12.92 if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4)
    return 0.2126 * parts[0] + 0.7152 * parts[1] + 0.0722 * parts[2]


def contrast_ratio(a, b):
    """WCAG contrast ratio, 1..21. Shape edges want 3:1 or better against their ground."""
    la, lb = luminance(a), luminance(b)
    hi, lo = max(la, lb), min(la, lb)
    return (hi + 0.05) / (lo + 0.05)


# ----------------------------------------------------------------- paints ----

class Linear:
    """A linear gradient. angle is degrees clockwise from 'left to right'."""

    def __init__(self, start, end, angle=90, stops=None):
        self.start, self.end, self.angle, self.stops = start, end, angle, stops

    def render(self, size):
        import math
        w = h = size
        img = Image.new("RGBA", (w, h))
        px = img.load()
        rad = math.radians(self.angle)
        dx, dy = math.cos(rad), math.sin(rad)
        # Project every pixel onto the gradient axis, normalised to 0..1.
        span = abs(dx) * w + abs(dy) * h
        ox = 0 if dx >= 0 else w
        oy = 0 if dy >= 0 else h
        stops = self.stops or [(0.0, self.start), (1.0, self.end)]
        stops = sorted(stops, key=lambda s: s[0])
        row = []
        for i in range(1024):
            t = i / 1023.0
            lo = stops[0]
            hi = stops[-1]
            for j in range(len(stops) - 1):
                if stops[j][0] <= t <= stops[j + 1][0]:
                    lo, hi = stops[j], stops[j + 1]
                    break
            spread = (hi[0] - lo[0]) or 1.0
            row.append(mix(lo[1], hi[1], (t - lo[0]) / spread))
        for y in range(h):
            for x in range(w):
                t = ((x - ox) * dx + (y - oy) * dy) / span
                t = 0.0 if t < 0 else (1.0 if t > 1 else t)
                px[x, y] = row[int(t * 1023)]
        return img


class Radial:
    """A radial gradient centred at (cx, cy) in units, reaching `radius` units."""

    def __init__(self, inner, outer, cx=500, cy=500, radius=500):
        self.inner, self.outer = inner, outer
        self.cx, self.cy, self.radius = cx, cy, radius

    def render(self, size):
        import math
        img = Image.new("RGBA", (size, size))
        px = img.load()
        k = size / UNITS
        cx, cy, r = self.cx * k, self.cy * k, max(self.radius * k, 1e-6)
        row = [mix(self.inner, self.outer, i / 1023.0) for i in range(1024)]
        for y in range(size):
            for x in range(size):
                d = math.hypot(x - cx, y - cy) / r
                d = 0.0 if d < 0 else (1.0 if d > 1 else d)
                px[x, y] = row[int(d * 1023)]
        return img


# ------------------------------------------------------------------ layer ----

class Layer:
    """An alpha mask you draw shapes onto. All coordinates are in 0..1000 units."""

    def __init__(self, canvas):
        self._c = canvas
        self.img = Image.new("L", (canvas.res, canvas.res), 0)
        self.d = ImageDraw.Draw(self.img)

    def _s(self, v):
        return v * self._c.k

    def _pts(self, points):
        return [(self._s(x), self._s(y)) for x, y in points]

    def rect(self, x0, y0, x1, y1, radius=0, fill=True, width=0):
        # PIL fills the end coordinate inclusively, so a 0..500 rect comes out one
        # device pixel wide of 500 units. Pull the far edge back so a rect spans
        # exactly what it says and abutting rects do not overlap by a supersample.
        box = [self._s(x0), self._s(y0), self._s(x1) - 1, self._s(y1) - 1]
        args = dict(fill=255 if fill else None,
                    outline=255 if width else None,
                    width=int(round(self._s(width))))
        if radius:
            self.d.rounded_rectangle(box, radius=self._s(radius), **args)
        else:
            self.d.rectangle(box, **args)
        return self

    def ellipse(self, cx, cy, rx, ry=None, fill=True, width=0):
        ry = rx if ry is None else ry
        box = [self._s(cx - rx), self._s(cy - ry),
               self._s(cx + rx) - 1, self._s(cy + ry) - 1]
        self.d.ellipse(box, fill=255 if fill else None,
                       outline=255 if width else None,
                       width=int(round(self._s(width))))
        return self

    def polygon(self, points, fill=True, width=0):
        self.d.polygon(self._pts(points), fill=255 if fill else None,
                       outline=255 if width else None,
                       width=int(round(self._s(width))))
        return self

    def line(self, points, width=10, round_ends=True):
        w = max(int(round(self._s(width))), 1)
        self.d.line(self._pts(points), fill=255, width=w, joint="curve")
        if round_ends:
            r = w / 2.0
            for x, y in self._pts(points):
                self.d.ellipse([x - r, y - r, x + r, y + r], fill=255)
        return self

    def arc(self, cx, cy, rx, start, end, width=10, ry=None):
        ry = rx if ry is None else ry
        box = [self._s(cx - rx), self._s(cy - ry), self._s(cx + rx), self._s(cy + ry)]
        self.d.arc(box, start, end, fill=255, width=max(int(round(self._s(width))), 1))
        return self

    def pieslice(self, cx, cy, r, start, end):
        box = [self._s(cx - r), self._s(cy - r), self._s(cx + r), self._s(cy + r)]
        self.d.pieslice(box, start, end, fill=255)
        return self

    def text(self, x, y, string, size, font="segoeuib.ttf", anchor="mm"):
        f = ImageFont.truetype("C:/Windows/Fonts/" + font, int(round(self._s(size))))
        self.d.text((self._s(x), self._s(y)), string, font=f, fill=255, anchor=anchor)
        return self

    def erase(self, other):
        """Punch another layer out of this one. Good for cut-through highlights."""
        from PIL import ImageChops
        self.img = ImageChops.subtract(self.img, other.img)
        self.d = ImageDraw.Draw(self.img)
        return self

    def intersect(self, other):
        from PIL import ImageChops
        self.img = ImageChops.multiply(self.img, other.img)
        self.d = ImageDraw.Draw(self.img)
        return self

    def copy(self):
        new = Layer(self._c)
        new.img = self.img.copy()
        new.d = ImageDraw.Draw(new.img)
        return new

    def grow(self, units):
        """Dilate (positive) or shrink (negative) the mask, for outlines and insets."""
        r = abs(int(round(self._s(units))))
        if r:
            f = ImageFilter.MaxFilter if units > 0 else ImageFilter.MinFilter
            step = 9
            done = 0
            while done < r:
                self.img = self.img.filter(f(step))
                done += (step - 1) // 2
        self.d = ImageDraw.Draw(self.img)
        return self


# ----------------------------------------------------------------- canvas ----

class Canvas:
    """Draw at `px` pixels square, internally supersampled `ss` times."""

    def __init__(self, px, ss=4, background=None):
        self.px = px
        self.ss = ss
        self.res = px * ss
        self.k = self.res / UNITS
        self.out = Image.new("RGBA", (self.res, self.res), (0, 0, 0, 0))
        if background is not None:
            self.out = Image.new("RGBA", (self.res, self.res), parse_colour(background))

    def layer(self):
        return Layer(self)

    def paint(self, layer, paint, blur=0, offset=(0, 0), opacity=1.0, behind=False):
        """Composite a mask, filled with a colour or gradient, onto the canvas."""
        mask = layer.img
        if blur:
            mask = mask.filter(ImageFilter.GaussianBlur(self.k * blur))
        if offset != (0, 0):
            mask = mask.transform(
                mask.size, Image.AFFINE,
                (1, 0, -offset[0] * self.k, 0, 1, -offset[1] * self.k),
                resample=Image.BICUBIC)
        if opacity < 1.0:
            mask = mask.point(lambda v: int(v * opacity))

        if isinstance(paint, (Linear, Radial)):
            fill = paint.render(self.res)
        else:
            fill = Image.new("RGBA", (self.res, self.res), parse_colour(paint))

        piece = Image.new("RGBA", (self.res, self.res), (0, 0, 0, 0))
        piece.paste(fill, (0, 0), mask)
        if behind:
            piece.alpha_composite(self.out)
            self.out = piece
        else:
            self.out.alpha_composite(piece)
        return self

    def image(self):
        return self.out.resize((self.px, self.px), Image.LANCZOS)


def render(concept, px, ss=4):
    """Run a concept's draw(C) at one pixel size and hand back the image."""
    C = Canvas(px, ss=ss)
    concept(C)
    return C.image()


# ------------------------------------------------- colour vision checking ----

# Machado, Oliveira & Fernandes (2009), severity 1.0, applied in linear RGB.
CVD = {
    "protanopia": ((0.152286, 1.052583, -0.204868),
                   (0.114503, 0.786281, 0.099216),
                   (-0.003882, -0.048116, 1.051998)),
    "deuteranopia": ((0.367322, 0.860646, -0.227968),
                     (0.280085, 0.672501, 0.047413),
                     (-0.011820, 0.042940, 0.968881)),
    "tritanopia": ((1.255528, -0.076749, -0.178779),
                   (-0.078411, 0.930809, 0.147602),
                   (0.004733, 0.691367, 0.303900)),
}


def simulate_cvd(img, kind):
    """How the icon looks to someone with that form of colour blindness."""
    import numpy as np
    a = np.asarray(img.convert("RGBA")).astype(np.float64) / 255.0
    rgb, alpha = a[..., :3], a[..., 3:]
    lin = np.where(rgb <= 0.04045, rgb / 12.92, ((rgb + 0.055) / 1.055) ** 2.4)
    m = np.array(CVD[kind]).T
    sim = np.clip(lin @ m, 0.0, 1.0)
    srgb = np.where(sim <= 0.0031308, sim * 12.92, 1.055 * sim ** (1 / 2.4) - 0.055)
    out = np.concatenate([srgb, alpha], axis=-1) * 255.0
    return Image.fromarray(out.astype("uint8"), "RGBA")


def greyscale(img):
    """The harshest legibility test: does the shape survive with no colour at all?"""
    g = img.convert("RGBA")
    lum = g.convert("L").convert("RGBA")
    lum.putalpha(g.getchannel("A"))
    return lum


# -------------------------------------------------------------- contact ----

CHECKER_LIGHT = (238, 238, 238, 255)
CHECKER_DARK = (32, 32, 34, 255)


def on_ground(img, colour, pad=0):
    """Flatten onto a background so alpha reads honestly."""
    bg = Image.new("RGBA", (img.width + pad * 2, img.height + pad * 2), parse_colour(colour))
    bg.alpha_composite(img, (pad, pad))
    return bg


def contact_sheet(concept, path, title="", sizes=(16, 24, 32, 48, 64, 128, 256),
                  ss=4, label_colour=(120, 120, 120, 255)):
    """Every size on light and dark, plus colour-blind and greyscale views of the big one.

    This is the sheet to actually look at before calling an icon finished: an icon that
    only works at 256 on white is not an icon.
    """
    renders = {s: render(concept, s, ss=ss) for s in sizes}
    big = renders[max(sizes)]

    pad = 14
    row_h = max(sizes) + pad * 2
    strip_w = sum(s + pad * 2 for s in sizes)
    checks = [("deuteranopia", simulate_cvd(big, "deuteranopia")),
              ("protanopia", simulate_cvd(big, "protanopia")),
              ("greyscale", greyscale(big))]
    check_w = len(checks) * (256 + pad * 2)
    width = max(strip_w, check_w, 640)
    height = 30 + row_h * 2 + 30 + 256 + pad * 2 + 24

    sheet = Image.new("RGBA", (width, height), (250, 250, 250, 255))
    d = ImageDraw.Draw(sheet)
    try:
        f = ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf", 13)
        fb = ImageFont.truetype("C:/Windows/Fonts/segoeuib.ttf", 15)
    except OSError:
        f = fb = ImageFont.load_default()

    d.text((pad, 8), title or "icon", font=fb, fill=(20, 20, 20, 255))

    y = 30
    for ground in (CHECKER_LIGHT, CHECKER_DARK):
        d.rectangle([0, y, width, y + row_h], fill=ground)
        x = 0
        for s in sizes:
            cell = s + pad * 2
            sheet.alpha_composite(renders[s], (x + pad, y + (row_h - s) // 2))
            d.text((x + cell // 2, y + row_h - 11), str(s), font=f,
                   fill=label_colour, anchor="mm")
            x += cell
        y += row_h

    y += 8
    x = 0
    for name, im in checks:
        sheet.alpha_composite(on_ground(im, CHECKER_DARK), (x + pad, y + 18))
        d.text((x + pad + 128, y + 9), name, font=f, fill=(20, 20, 20, 255), anchor="mm")
        x += 256 + pad * 2
    sheet.convert("RGB").save(path)
    return path


# ------------------------------------------------------------------- ico ----

def write_ico(images, path):
    """Write a multi-size .ico, each size from its own render.

    Sizes up to 64 go in as 32-bit BMP with an AND mask, which every Windows shell
    surface reads; 128 and up go in PNG-compressed to keep the file small. Pillow's
    own ICO writer downsamples one source image for every entry, which throws away
    per-size tuning, so the container is assembled here.
    """
    images = sorted(images, key=lambda im: im.width)
    entries = []
    for im in images:
        im = im.convert("RGBA")
        if im.width > 64:
            buf = io.BytesIO()
            im.save(buf, format="PNG", optimize=True)
            entries.append((im.width, im.height, buf.getvalue()))
        else:
            entries.append((im.width, im.height, _dib(im)))

    out = bytearray(struct.pack("<HHH", 0, 1, len(entries)))
    offset = 6 + 16 * len(entries)
    for w, h, blob in entries:
        out += struct.pack("<BBBBHHII",
                           0 if w >= 256 else w, 0 if h >= 256 else h,
                           0, 0, 1, 32, len(blob), offset)
        offset += len(blob)
    for _, _, blob in entries:
        out += blob
    with open(path, "wb") as fh:
        fh.write(bytes(out))
    return path


def _dib(im):
    """32-bit bottom-up BGRA DIB with a 1bpp AND mask, as an .ico entry wants."""
    w, h = im.size
    header = struct.pack("<IiiHHIIiiII", 40, w, h * 2, 1, 32, 0, w * h * 4, 0, 0, 0, 0)
    px = im.load()
    body = bytearray()
    for y in range(h - 1, -1, -1):
        for x in range(w):
            r, g, b, a = px[x, y]
            body += bytes((b, g, r, a))
    stride = ((w + 31) // 32) * 4
    mask = bytearray()
    for y in range(h - 1, -1, -1):
        bits = bytearray(stride)
        for x in range(w):
            if px[x, y][3] == 0:
                bits[x // 8] |= 0x80 >> (x % 8)
        mask += bits
    return bytes(header) + bytes(body) + bytes(mask)
