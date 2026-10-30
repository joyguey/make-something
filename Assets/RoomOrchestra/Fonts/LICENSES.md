# Label font

| File | Source | License | Changes | sha256 |
|---|---|---|---|---|
| LittleEchoesLabels.ttf | Noto Sans SC (Google Fonts, github.com/google/fonts/tree/main/ofl/notosanssc), Copyright 2014-2021 Adobe | SIL Open Font License 1.1 (OFL.txt beside this file). Commercial use, embedding and bundling in software are allowed. | Cut down to the 230 characters the controls use (printable ASCII + the Chinese terms in Core/UiText.cs), fixed at weight 500, renamed "Little Echoes Labels" (a modified version must not use the reserved name "Source"; it doesn't). Made by ArtSource/Fonts/subset_label_font.py. | 52e906f8e520225ca837ece37e3ac115496333125a0017fa51cbea9347887cfb |
| LittleEchoesLabels SDF.asset | generated from the .ttf above by Editor/LabelFont.cs (TextMesh Pro static atlas) | same as the .ttf | none beyond rasterizing | — |

The OFL requires the license to travel with the font: keep OFL.txt in the project and list the font in the app's credits/notices before release.
