"""
Lifts the hand-lettered "Little Echoes" title out of the owner's key art (the design document cover,
"Little Echoes_ A Mixed-Reality Musical Playground.png"), so the icon uses that exact lettering (owner,
2026-10-29: "use this font as is"). The lettering is part of the artwork, not an installable font.

Cream letters are separated from the purple room by brightness and low saturation, edges are cleaned of
purple fringe, the sparkles and note around the title are dropped (yellow, or too small), the swoosh over
the "s" is dropped (owner, 2026-10-29), and the result is saved whole and split into "Little" / "Echoes"
for a two-line layout.

  python3 ArtSource/Icon/extract_title.py "<key art .png>" ArtSource/Icon/lettering
Needs Pillow and numpy.
"""
import os
import sys
from collections import deque

import numpy as np
from PIL import Image, ImageFilter

CROP = (915, 25, 1506, 156)   # the title in the 1536 x 1024 key art, above the subtitle
SCALE = 4                     # upscale before keying for smoother edges
CREAM = np.array([250, 240, 222], dtype=np.float32)
TAN = np.array([222, 194, 158], dtype=np.float32)   # the letters' own shading, without the room's purple


def smoothstep(edge0, edge1, x):
    t = np.clip((x - edge0) / (edge1 - edge0), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def components(mask):
    """Labels 4-connected regions of a boolean mask; returns (labels, sizes)."""
    h, w = mask.shape
    labels = np.zeros((h, w), dtype=np.int32)
    sizes = [0]
    for y, x in zip(*np.nonzero(mask)):
        if labels[y, x]:
            continue
        n = len(sizes)
        labels[y, x] = n
        queue, count = deque([(y, x)]), 0
        while queue:
            cy, cx = queue.popleft()
            count += 1
            for ny, nx in ((cy + 1, cx), (cy - 1, cx), (cy, cx + 1), (cy, cx - 1)):
                if 0 <= ny < h and 0 <= nx < w and mask[ny, nx] and not labels[ny, nx]:
                    labels[ny, nx] = n
                    queue.append((ny, nx))
        sizes.append(count)
    return labels, sizes


def main(src, out_dir):
    art = Image.open(src).convert("RGB").crop(CROP)
    art = art.resize((art.width * SCALE, art.height * SCALE), Image.LANCZOS)
    rgb = np.asarray(art).astype(np.float32)
    top = rgb.max(axis=2)
    sat = (top - rgb.min(axis=2)) / np.maximum(top, 1.0)
    alpha = smoothstep(130, 190, top) * (1.0 - smoothstep(0.25, 0.4, sat))

    labels, sizes = components(alpha > 0.5)
    h, w = alpha.shape
    keep = np.zeros(len(sizes), dtype=bool)
    for n in range(1, len(sizes)):
        ys, xs = np.nonzero(labels == n)
        cx = xs.mean() / w
        colour = rgb[ys, xs].mean(axis=0)
        # Everything cream is lettering: the letters and the i dots.
        # The little note left of the "L" (mint) and the sparkle right of the swoosh (pale blue) are not; specks are noise.
        cy = ys.mean() / h
        swoosh = cx > 0.9 and cy < 0.45  # the two dashes over the "s" (owner: remove), every piece of them
        # Letters only: the diamond inside the "o" (under 400 px here) reads as a speck at icon size.
        keep[n] = sizes[n] >= 400 and colour[1] <= colour[0] and colour[2] <= colour[0] and cx > 0.03 and not swoosh
    # Grow the kept regions by the soft edge so anti-aliasing survives, and nothing else does.
    near = np.isin(labels, np.nonzero(keep)[0])
    near_img = Image.fromarray((near * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(9))
    alpha *= np.asarray(near_img) / 255.0
    # Close the small gaps the darker, purple-tinted shading left at the letters' bottoms.
    solid = Image.fromarray((alpha * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(7)).filter(ImageFilter.MinFilter(7))
    alpha = np.maximum(alpha, np.asarray(solid) / 255.0 * near)

    # Keep the letters' light-to-dark shading, but in warm cream and tan instead of the room's purple.
    luminance = rgb @ np.array([0.299, 0.587, 0.114], dtype=np.float32)
    shade = smoothstep(150, 236, luminance)[..., None]
    colour = TAN + (CREAM - TAN) * shade
    rgba = np.dstack([np.clip(colour, 0, 255), alpha * 255]).astype(np.uint8)
    title = Image.fromarray(rgba, "RGBA")
    title = title.crop(title.getbbox())

    os.makedirs(out_dir, exist_ok=True)
    title.save(os.path.join(out_dir, "little_echoes.png"))
    # Split at the widest empty gap between the two words (in the middle third).
    cols = np.asarray(title)[..., 3].astype(np.float32).sum(axis=0)
    third = title.width // 3
    gap = int(np.argmin(cols[third:2 * third])) + third
    little = title.crop((0, 0, gap, title.height))
    echoes = title.crop((gap, 0, title.width, title.height))
    little.crop(little.getbbox()).save(os.path.join(out_dir, "little.png"))
    echoes.crop(echoes.getbbox()).save(os.path.join(out_dir, "echoes.png"))
    print(f"title {title.size}, split at x={gap}")


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
