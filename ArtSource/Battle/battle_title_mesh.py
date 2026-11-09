"""
The Echo Battle's street-style title as solid shapes (owner 2026-10-31: the battle should look like something teens and
adults play, "street / graffiti"): "ECHO//BATTLE" in Sedgwick Ave Display (a graffiti-tag face, SIL OFL 1.1,
ArtSource/Fonts/SedgwickAveDisplay/OFL.txt) and 回声对决 in Noto Sans SC Black (the label font's family; Sedgwick has no
Chinese), traced and extruded like the countdown (ArtSource/Countdown/countdown_mesh.py): a coloured face, darker sides,
and a thick white sticker outline behind. Shapes, not textures (PICO Spatial did not draw image textures on the emulator).

  python3 ArtSource/Battle/battle_title_mesh.py ArtSource/Fonts/SedgwickAveDisplay/SedgwickAveDisplay-Regular.ttf \
      NotoSansSC.ttf Assets/RoomOrchestra/Art/Battle/BattleTitleMeshes.json
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "Countdown"))
import countdown_mesh as cm  # noqa: E402

TITLE_WIDTH_M = 0.44   # across the battle card
cm.DEPTH_M = 0.016     # a chunky sticker, not a sign
cm.OUTLINE_M = 0.007   # the white sticker rim
cm.OUTLINE_DEPTH_M = 0.008


def main(tag_font, chinese_font, out):
    heavy = cm.heavy_font(chinese_font)
    pieces = []
    for name, text, font in (("BattleTitle", "ECHO//BATTLE", tag_font), ("BattleTitleZh", "回声对决", heavy)):
        mask = cm.render(font, text)
        import numpy as np
        ys, xs = np.nonzero(mask)
        px_per_m = (xs.max() - xs.min()) / TITLE_WIDTH_M  # every title the card's width
        pieces.append(cm.piece(font, name, text, px_per_m))
    os.makedirs(os.path.dirname(out), exist_ok=True)
    with open(out, "w") as f:
        json.dump({"pieces": pieces}, f, separators=(",", ":"))
    for p in pieces:
        print(p["name"], p["text"], f'{p["width"]} x {p["height"]} m')


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2], sys.argv[3])
