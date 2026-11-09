"""
The title card's "Little Echoes" as geometry (2026-10-29). PICO Spatial on the PICO Emulator 6.1 did not draw
the lettering as a textured quad: Unlit and Lit, transparent and cut-out, compressed and RGBA32 readable,
_BaseMap and _MainTex, power-of-two or not, it showed grey or another texture's pixels (docs/AGENT_PROGRESS.md).
Plain coloured meshes draw reliably, so the key-art lettering is traced into two flat shapes: the cream letters
and the icon's rounded plum outline behind them. Edges are traced from the lettering's alpha (OpenCV contours,
lightly simplified) and filled with triangles (earcut), so they stay smooth at any distance.

  python3 ArtSource/Icon/lettering_mesh.py ArtSource/Icon/lettering/little_echoes.png \
      Assets/RoomOrchestra/Art/Title/TitleLetteringMesh.json
Writes {"width": metres, "fill": {"vertices": [x, y, ...], "triangles": [...]}, "outline": {...}};
x right, y up, centred, in metres. Needs Pillow, numpy, opencv-python-headless and mapbox_earcut.
"""
import json
import sys

import cv2
import mapbox_earcut as earcut
import numpy as np
from PIL import Image, ImageFilter

WIDTH_M = 0.8          # the letters across the title card
TRACE_PX = 1600        # the letters' width when traced
STROKE = 0.0177        # outline thickness as a share of the letters' width (as on the icon)
SIMPLIFY_PX = 0.9      # contour simplification tolerance


def masks(path):
    art = Image.open(path).convert("RGBA")
    scale = TRACE_PX / art.width
    art = art.resize((TRACE_PX, round(art.height * scale)), Image.LANCZOS)
    pad = int(TRACE_PX * STROKE * 2)
    canvas = Image.new("RGBA", (art.width + 2 * pad, art.height + 2 * pad), (0, 0, 0, 0))
    canvas.alpha_composite(art, (pad, pad))
    alpha = canvas.getchannel("A")
    stroke = TRACE_PX * STROKE
    # The icon's outline: grow, blur, keep what the blur reaches (add_title.lettering_layer, at this size).
    ring = alpha.filter(ImageFilter.MaxFilter(5)).filter(ImageFilter.GaussianBlur(stroke)).point(lambda v: 0 if v < 24 else 255)
    fill = (np.asarray(alpha) >= 128).astype(np.uint8) * 255
    outline = np.maximum(np.asarray(ring), fill)
    return fill, outline


def shape(mask, px_to_m, centre):
    contours, hierarchy = cv2.findContours(mask, cv2.RETR_CCOMP, cv2.CHAIN_APPROX_NONE)
    vertices, triangles = [], []
    if hierarchy is None:
        return {"vertices": vertices, "triangles": triangles}
    hierarchy = hierarchy[0]
    for i, contour in enumerate(contours):
        if hierarchy[i][3] != -1:
            continue  # a hole; filled in with its outer edge below
        rings = [cv2.approxPolyDP(contour, SIMPLIFY_PX, True).reshape(-1, 2)]
        child = hierarchy[i][2]
        while child != -1:
            hole = cv2.approxPolyDP(contours[child], SIMPLIFY_PX, True).reshape(-1, 2)
            if len(hole) >= 3:
                rings.append(hole)
            child = hierarchy[child][0]
        if len(rings[0]) < 3:
            continue
        points = np.concatenate(rings).astype(np.float64)
        ends = np.cumsum([len(r) for r in rings]).astype(np.uint32)
        tris = earcut.triangulate_float64(points, ends)
        base = len(vertices) // 2
        for x, y in points:
            vertices += [round((x - centre[0]) * px_to_m, 5), round((centre[1] - y) * px_to_m, 5)]
        triangles += [int(base + t) for t in tris]
    return {"vertices": vertices, "triangles": triangles}


def main(lettering, out):
    fill, outline = masks(lettering)
    ys, xs = np.nonzero(fill)
    px_to_m = WIDTH_M / (xs.max() - xs.min())
    oys, oxs = np.nonzero(outline)
    centre = ((oxs.min() + oxs.max()) / 2.0, (oys.min() + oys.max()) / 2.0)
    data = {
        "width": round((oxs.max() - oxs.min()) * px_to_m, 4),
        "height": round((oys.max() - oys.min()) * px_to_m, 4),
        "fill": shape(fill, px_to_m, centre),
        "outline": shape(outline, px_to_m, centre),
    }
    with open(out, "w") as f:
        json.dump(data, f, separators=(",", ":"))
    print(out, {k: (len(v["triangles"]) // 3 if isinstance(v, dict) else v) for k, v in data.items()})


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
