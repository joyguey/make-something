"""
Subtle, light surface textures for Little Echoes, made from scratch (no photos, no downloads; deterministic). They are
near-white detail maps: the material's colour tints them, so the approved palette stays the same and only the grain shows.

  python3 ArtSource/Textures/gen_textures.py Assets/RoomOrchestra/Art/Textures
Writes Wood.png (soft wood grain with growth rings and a few knots, tiles seamlessly) and Weave.png (a fine fabric
weave), 512 x 512 RGBA (4 channels: PICO Spatial misread 3-channel textures as stripes). Needs numpy and Pillow.
"""
import sys

import numpy as np
from PIL import Image

N = 512


def wood(seed=7):
    rng = np.random.default_rng(seed)
    y, x = np.mgrid[0:N, 0:N] / N
    # Grain runs along x; seamless because every term is periodic in x and y.
    warp = 0.03 * np.sin(2 * np.pi * (y * 3 + 0.2 * np.sin(2 * np.pi * x))) + 0.015 * np.sin(2 * np.pi * (x * 2 + y * 5))
    rings = np.sin(2 * np.pi * (y * 18 + warp * 6))
    fine = np.sin(2 * np.pi * (y * 90 + warp * 20 + 0.3 * np.sin(2 * np.pi * x * 7)))
    v = 0.86 + 0.07 * rings + 0.03 * fine
    for _ in range(3):  # a few soft knots
        cx, cy = rng.random(2)
        dx = (x - cx + 0.5) % 1 - 0.5
        dy = ((y - cy + 0.5) % 1 - 0.5) * 2.5
        d = np.sqrt(dx * dx + dy * dy)
        v -= 0.10 * np.exp(-(d / 0.035) ** 2) * (0.6 + 0.4 * np.sin(2 * np.pi * d * 60))
    v = np.clip(v, 0.6, 1.0)
    rgb = np.stack([v, v * 0.985, v * 0.96], axis=-1)
    return Image.fromarray((rgb * 255).astype(np.uint8), "RGB").convert("RGBA")


def weave():
    y, x = np.mgrid[0:N, 0:N]
    cell = 16
    u, w = (x % cell) / cell, (y % cell) / cell
    over = ((x // cell + y // cell) % 2 == 0)
    thread = np.where(over, np.sin(np.pi * w), np.sin(np.pi * u))
    v = 0.84 + 0.12 * thread
    rgb = np.stack([v, v, v], axis=-1)
    return Image.fromarray((np.clip(rgb, 0, 1) * 255).astype(np.uint8), "RGB").convert("RGBA")


if __name__ == "__main__":
    out = sys.argv[1]
    wood().save(f"{out}/Wood.png")
    weave().save(f"{out}/Weave.png")
    print(out)
