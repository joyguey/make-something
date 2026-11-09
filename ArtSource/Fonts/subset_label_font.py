"""
Builds the label font (Assets/RoomOrchestra/Fonts/LittleEchoesLabels.ttf): Noto Sans SC (SIL Open
Font License 1.1) cut down to the characters the controls use, at weight 700 (bold enough to read against a real room), renamed as the OFL asks
of modified versions. Run again whenever Core/UiText.cs gains a new Chinese character.

  python3 -m pip install fonttools
  curl -L -o NotoSansSC.ttf "https://github.com/google/fonts/raw/main/ofl/notosanssc/NotoSansSC%5Bwght%5D.ttf"
  python3 ArtSource/Fonts/subset_label_font.py NotoSansSC.ttf
"""
import os
import re
import sys

from fontTools import subset
from fontTools.ttLib import TTFont
from fontTools.varLib import instancer

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
TERMS = os.path.join(ROOT, "Assets", "RoomOrchestra", "Core", "UiText.cs")
OUT = os.path.join(ROOT, "Assets", "RoomOrchestra", "Fonts", "LittleEchoesLabels.ttf")
FAMILY = "Little Echoes Labels"


def characters():
    text = open(TERMS, encoding="utf-8").read()
    wide = {c for c in text if ord(c) > 0x7E}  # the Chinese terms (comments are ASCII)
    return "".join(sorted(wide | {chr(c) for c in range(0x20, 0x7F)}))


def main(source):
    font = TTFont(source)
    font = instancer.instantiateVariableFont(font, {"wght": 700})
    options = subset.Options()
    options.layout_features = ["*"]
    options.name_IDs = ["*"]
    subsetter = subset.Subsetter(options)
    subsetter.populate(text=characters())
    subsetter.subset(font)
    names = font["name"]
    for record in list(names.names):
        if record.nameID in (1, 16):
            names.setName(FAMILY, record.nameID, record.platformID, record.platEncID, record.langID)
        elif record.nameID in (3, 4):
            names.setName(f"{FAMILY} Medium", record.nameID, record.platformID, record.platEncID, record.langID)
        elif record.nameID == 6:
            names.setName("LittleEchoesLabels-Medium", record.nameID, record.platformID, record.platEncID, record.langID)
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    font.save(OUT)
    print(f"{os.path.relpath(OUT, ROOT)}: {len(characters())} characters, {os.path.getsize(OUT)} bytes")


if __name__ == "__main__":
    main(sys.argv[1])
