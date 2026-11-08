# Label font

| File | Source | License | Changes | sha256 |
|---|---|---|---|---|
| LittleEchoesLabels.ttf | Noto Sans SC (Google Fonts, github.com/google/fonts/tree/main/ofl/notosanssc), Copyright 2014-2021 Adobe | SIL Open Font License 1.1 (OFL.txt beside this file). Commercial use, embedding and bundling in software are allowed. | Cut down to the 346 characters the controls use (printable ASCII + the Chinese terms in Core/UiText.cs), fixed at weight 700, renamed "Little Echoes Labels" (a modified version must not use the reserved name "Source"; it doesn't). Made by ArtSource/Fonts/subset_label_font.py. | 654f689064c700fbb1ef688405fc332bce31843f4177e6471d4eac2bf419300e |
| LittleEchoesLabels SDF.asset | generated from the .ttf above by Editor/LabelFont.cs (TextMesh Pro static atlas) | same as the .ttf | none beyond rasterizing | — |

The OFL requires the license to travel with the font: keep OFL.txt in the project and list the font in the app's credits/notices before release.

## Derived artwork (not shipped as fonts)
- **Echo Battle title** (`Art/Battle/BattleTitleMeshes.json`): the words "ECHO//BATTLE" traced into 3D shapes from **Sedgwick Ave Display** (Copyright 2017 The Sedgwick Ave Project Authors, SIL Open Font License 1.1; the font and its OFL.txt are in `ArtSource/Fonts/SedgwickAveDisplay/`, not in the app), and 回声对决 from Noto Sans SC (above). Made by `ArtSource/Battle/battle_title_mesh.py`.
- **2026-11-06:** regenerated for 确, 定 and 退 (the "Sure?" confirmation and Quit), from Noto Sans SC downloaded the same day.
- **Source note (2026-10-31):** the Noto Sans SC variable font was downloaded again from google/fonts (the scratch copy was cleared); Google's current file differs from the 2026-10 copy, so the label font's bytes changed along with the new Battle characters.
