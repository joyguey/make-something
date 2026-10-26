# Flutter / Bop / Boom character meshes — provenance

- **Design:** owner-approved reference `docs/reference/Flutter_Bop_Boom_USER_APPROVED_REFERENCE.png` (unmodified; SHA-256 in `docs/reference/REFERENCE_ASSET_README.md`). Shapes, face layout and colours follow it; colours were sampled from it.
- **Made by:** procedural Blender script `ArtSource/Characters/build_characters.py` (Blender 4.2.1 LTS, headless, `--factory-startup`). Editable sources: `ArtSource/Characters/{Flutter,Bop,Boom}.blend`. Preview renders: `ArtSource/Characters/preview_character.py`.
- **External assets:** none. No downloaded models, textures, or fonts are used in these meshes.
- **Licence:** project-owned, same terms as the rest of this repository.
- Rebuild: `Blender --background --factory-startup --python ArtSource/Characters/build_characters.py -- Flutter Bop Boom`, then run `RoomOrchestra.EditorTools.LittleEchoesSceneSetup.ApplyAndSave()` in Unity.
