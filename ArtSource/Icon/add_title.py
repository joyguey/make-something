"""
Writes the title "Little Echoes" across the middle of an icon rendered by build_icons.py, using the
hand-lettered title from the owner's key art (lifted out by extract_title.py into ArtSource/Icon/lettering):
cream letters with a rounded deep-plum outline and a soft shadow, on two lines spaced so the "L" and the
"E" read as separate letters.

  python3 ArtSource/Icon/add_title.py <icon.png> ArtSource/Icon/lettering <out.png> [--one-line]
      [--foreground fg.png fg_out.png] [--title-layer title_out.png]
--title-layer also writes the title alone on transparency, without the soft shadow: the top layer of PICO's
layered launcher icon (the launcher adds its own shadows and highlights there).
Needs Pillow and numpy.
"""
import argparse

import os

import numpy as np
from PIL import Image, ImageFilter

OUTLINE = (59, 35, 80, 255)     # deep plum, as on the notes
SHADOW = (150, 60, 50, 110)     # coral shadow
CENTRE_Y = 0.385                # fraction of the height: the clear band between the ring top and the leaf
MAX_WIDTH = 0.5                 # fraction of the width, inside the echo ring


def lettering_layer(size, folder, one_line, shadowed=True):
    """The key-art lettering, outlined in plum with a soft shadow, as one line or two."""
    w = h = size
    pieces = [Image.open(os.path.join(folder, "little_echoes.png"))] if one_line else \
        [Image.open(os.path.join(folder, n)) for n in ("little.png", "echoes.png")]
    width = (0.62 if one_line else MAX_WIDTH) * w  # one line stays clear of the echoes on the ring
    scale = width / max(p.width for p in pieces)
    pieces = [p.resize((max(1, int(p.width * scale)), max(1, int(p.height * scale))), Image.LANCZOS) for p in pieces]
    stroke = max(4, int(w * 0.011))
    # Two lines sit apart far enough that their outlines don't meet: the "L"'s foot and the "E" read as
    # separate letters (owner, 2026-10-29).
    gap = 2 * stroke + int(0.004 * h) if not one_line else 0
    total = sum(p.height for p in pieces) + gap * (len(pieces) - 1) + 2 * stroke
    letters = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    y = int((CENTRE_Y + (0.03 if one_line else 0.0)) * h - total / 2) + stroke
    for p in pieces:
        letters.alpha_composite(p, ((w - p.width) // 2, y))
        y += p.height + gap
    alpha = letters.getchannel("A")
    # A rounded outline: blur the letters' shape and keep everything the blur reaches (a square max
    # filter gave the thin swoosh strokes blocky corners).
    # A small max filter first, so the swoosh's hairline strokes get an outline as even as the letters'.
    ring = alpha.filter(ImageFilter.MaxFilter(5)).filter(ImageFilter.GaussianBlur(stroke)).point(lambda v: 0 if v < 24 else min(255, (v - 24) * 18))
    ring = Image.fromarray(np.maximum(np.asarray(ring), np.asarray(alpha)))
    outline = Image.new("RGBA", (w, h), OUTLINE)
    outline.putalpha(ring)
    shadow = Image.new("RGBA", (w, h), SHADOW[:3] + (0,))
    shadow.putalpha(ring.point(lambda v: v * SHADOW[3] // 255))
    shadow = shadow.transform((w, h), Image.AFFINE, (1, 0, -stroke // 2, 0, 1, -stroke)).filter(ImageFilter.GaussianBlur(stroke * 0.8))
    lettered = Image.alpha_composite(outline, letters)
    return Image.alpha_composite(shadow, lettered) if shadowed else lettered


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("icon")
    parser.add_argument("lettering")
    parser.add_argument("out")
    parser.add_argument("--one-line", action="store_true")
    parser.add_argument("--title-layer")
    parser.add_argument("--foreground", nargs=2, metavar=("FG", "FG_OUT"))
    args = parser.parse_args()
    icon = Image.open(args.icon).convert("RGBA")
    title = lettering_layer(icon.width, args.lettering, args.one_line)
    Image.alpha_composite(icon, title).save(args.out)
    if args.foreground:
        fg = Image.open(args.foreground[0]).convert("RGBA")
        Image.alpha_composite(fg, title).save(args.foreground[1])
    if args.title_layer:
        lettering_layer(icon.width, args.lettering, args.one_line, shadowed=False).save(args.title_layer)
    print(args.out)


if __name__ == "__main__":
    main()
