"""
The tutorial's 3-2-1-Go count as solid 3D shapes (owner, 2026-10-30: "different coloured and large 3D text").
Each word is set in Noto Sans SC at its heaviest weight (SIL OFL 1.1, Fonts/LICENSES.md), traced from a large
render (OpenCV contours, earcut triangles, as ArtSource/Icon/lettering_mesh.py does for the title) and extruded:
a coloured solid with darker sides, and a rounded dark outline a little behind it, so the count reads against a
busy real room. Plain meshes: PICO Spatial draws them reliably where it did not draw textures.

  python3 ArtSource/Countdown/countdown_mesh.py NotoSansSC.ttf Assets/RoomOrchestra/Art/Countdown/CountdownMeshes.json
Writes {"pieces": [{"name", "text", "width", "height", "faces", "sides", "outline"}]}, each part
{"vertices": [x, y, z, ...], "triangles": [...]}: metres, x right, y up, z away from the player (front at -z),
every triangle wound so Unity's (left-handed) normal points out of the solid.
Needs Pillow, numpy, fonttools, opencv-python-headless and mapbox_earcut.
"""
import json
import os
import sys
import tempfile

import cv2
import mapbox_earcut as earcut
import numpy as np
from fontTools.ttLib import TTFont
from fontTools.varLib import instancer
from PIL import Image, ImageDraw, ImageFont

PIECES = [("Count3", "3"), ("Count2", "2"), ("Count1", "1"), ("CountGo", "GO!"), ("CountGoZh", "开始！"), ("CountThrow", "THROW!"), ("CountThrowZh", "扔！")]
HEIGHT_M = 0.30        # a digit's height
DEPTH_M = 0.06         # the coloured solid's depth
OUTLINE_M = 0.012      # the dark rim around it
OUTLINE_DEPTH_M = 0.04  # the rim sits behind the solid's front face, set back this deep
RENDER_PX = 600        # font size for tracing
SIMPLIFY_PX = 1.2


def heavy_font(path):
    font = TTFont(path)
    if "fvar" in font:
        font = instancer.instantiateVariableFont(font, {"wght": 900})
    out = os.path.join(tempfile.mkdtemp(), "heavy.ttf")
    font.save(out)
    return out


def render(font_path, text):
    font = ImageFont.truetype(font_path, RENDER_PX)
    left, top, right, bottom = font.getbbox(text)
    pad = RENDER_PX // 4
    img = Image.new("L", (right - left + 2 * pad, bottom - top + 2 * pad), 0)
    ImageDraw.Draw(img).text((pad - left, pad - top), text, font=font, fill=255)
    return (np.asarray(img) >= 128).astype(np.uint8) * 255


def rings_of(mask):
    contours, hierarchy = cv2.findContours(mask, cv2.RETR_CCOMP, cv2.CHAIN_APPROX_NONE)
    shapes = []
    if hierarchy is None:
        return shapes
    hierarchy = hierarchy[0]
    for i, contour in enumerate(contours):
        if hierarchy[i][3] != -1:
            continue
        outer = cv2.approxPolyDP(contour, SIMPLIFY_PX, True).reshape(-1, 2).astype(np.float64)
        if len(outer) < 3:
            continue
        rings = [outer]
        child = hierarchy[i][2]
        while child != -1:
            hole = cv2.approxPolyDP(contours[child], SIMPLIFY_PX, True).reshape(-1, 2).astype(np.float64)
            if len(hole) >= 3:
                rings.append(hole)
            child = hierarchy[child][0]
        shapes.append(rings)
    return shapes


class Part:
    def __init__(self):
        self.v, self.t = [], []

    def tri(self, a, b, c, outward):
        a, b, c = map(np.asarray, (a, b, c))
        n = np.cross(b - a, c - a)
        if np.dot(n, outward) < 0:
            b, c = c, b
        base = len(self.v)
        self.v += [a, b, c]
        self.t += [base, base + 1, base + 2]

    def json(self):
        return {"vertices": [round(float(x), 5) for p in self.v for x in p], "triangles": self.t}


def extrude(mask, to_m, z_front, z_back, faces, sides):
    inside = lambda p: 0 <= int(p[1]) < mask.shape[0] and 0 <= int(p[0]) < mask.shape[1] and mask[int(p[1]), int(p[0])] > 0
    for rings in rings_of(mask):
        points = np.concatenate(rings)
        ends = np.cumsum([len(r) for r in rings]).astype(np.uint32)
        tris = earcut.triangulate_float64(points, ends).reshape(-1, 3)
        m = [to_m(p) for p in points]
        for i, j, k in tris:
            faces.tri((*m[i], z_front), (*m[j], z_front), (*m[k], z_front), (0, 0, -1))
            faces.tri((*m[i], z_back), (*m[j], z_back), (*m[k], z_back), (0, 0, 1))
        for ring in rings:
            for a in range(len(ring)):
                p, q = ring[a], ring[(a + 1) % len(ring)]
                edge = q - p
                length = np.hypot(*edge)
                if length < 1e-6:
                    continue
                normal_px = np.array([edge[1], -edge[0]]) / length  # one side of the edge, in image space
                if inside((p + q) / 2 + normal_px * 2.5):
                    normal_px = -normal_px                          # pointed into the solid: turn it out
                outward = (normal_px[0], -normal_px[1], 0.0)        # image y runs down
                P, Q = to_m(p), to_m(q)
                a3, b3, c3, d3 = (*P, z_front), (*Q, z_front), (*Q, z_back), (*P, z_back)
                sides.tri(a3, b3, c3, outward)
                sides.tri(a3, c3, d3, outward)


def piece(font_path, name, text, px_per_m):
    fill = render(font_path, text)
    r = max(1, int(round(OUTLINE_M * px_per_m)))
    kernel = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (2 * r + 1, 2 * r + 1))
    outline = cv2.dilate(fill, kernel)
    ys, xs = np.nonzero(outline)
    cx, cy = (xs.min() + xs.max()) / 2.0, (ys.min() + ys.max()) / 2.0
    to_m = lambda p: ((p[0] - cx) / px_per_m, (cy - p[1]) / px_per_m)
    faces, sides, rim, rim_sides = Part(), Part(), Part(), Part()
    extrude(fill, to_m, -DEPTH_M / 2, DEPTH_M / 2, faces, sides)
    front_rim = -DEPTH_M / 2 + 0.004  # just behind the solid's face, so only the rim around it shows
    extrude(outline, to_m, front_rim, front_rim + OUTLINE_DEPTH_M, rim, rim_sides)
    rim.v += rim_sides.v
    rim.t += [i + len(rim.v) - len(rim_sides.v) for i in rim_sides.t]
    return {
        "name": name, "text": text,
        "width": round(float(xs.max() - xs.min()) / px_per_m, 4), "height": round(float(ys.max() - ys.min()) / px_per_m, 4),
        "faces": faces.json(), "sides": sides.json(), "outline": rim.json(),
    }


def main(font, out):
    heavy = heavy_font(font)
    digit = render(heavy, "3")
    ys, _ = np.nonzero(digit)
    px_per_m = (ys.max() - ys.min()) / HEIGHT_M  # every word at the digits' scale
    pieces = [piece(heavy, name, text, px_per_m) for name, text in PIECES]
    os.makedirs(os.path.dirname(out), exist_ok=True)
    with open(out, "w") as f:
        json.dump({"pieces": pieces}, f, separators=(",", ":"))
    for p in pieces:
        print(p["name"], p["text"], f'{p["width"]} x {p["height"]} m', len(p["faces"]["triangles"]) // 3, len(p["sides"]["triangles"]) // 3, len(p["outline"]["triangles"]) // 3)


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
