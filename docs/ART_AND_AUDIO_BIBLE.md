# APPROVED THREE-CHARACTER ART & AUDIO BIBLE

## Golden reference
The owner-approved image `Flutter_Bop_Boom_USER_APPROVED_REFERENCE.png` is an immutable visual design reference. Copy exactly; do NOT overwrite, restyle, remove, replace with downloads, or reinterpret into different face/proportions without design approval. Make derivative editable 3D meshes separately under `Assets/RoomOrchestra/Art/Characters/`. Keep source `*.blend`/procedural scripts and document provenance.

## Silhouettes and personalities
- **Flutter**: upright droplet/blob; cool turquoise fill; dark teal simple oval eyes and smile; bright sparkle motif, slight asymmetry, airy weight. Playful narrow bounce, feather float. Use a small sparkle glyph on selectable label for non-color ID.
- **Bop**: symmetrical rounded lavender orb; deep violet eyes and smiling mouth; subtle top-left highlight; springy elastic squish. Dot motif on UI label/selected glow.
- **Boom**: wider flatter amber dumpling/blob; brown eyes/smile; slow squash, warm big resonance. Three little arcs/ripple motif on UI label/selected outline.
- Visual features: matte-to-soft-satin shapes, thick enough face silhouette and gentle reflections. Avoid human skin texture, detailed costumes, culture-caricature faces or distracting particles. Facial expressions can animate via mesh blend shapes or simple 2D material swaps. Preserve body bounding ratios from original user reference.
- Mini echo versions are identifiable heads/faces only or simplified ghost echoes, not dense clones needing many skinned meshes; use pooled low-cost meshes and material property blocks.

## Emotional performance rules
Idle breathe/blink slowly. On first collision react in surprise (not fear). A second echo of the same type at the same target yields social interaction: two glance and smile. Denser rhythms yield restrained happy bounce at significant beat transitions. Changed note -> puzzled then satisfied nod. Reduced-motion mode substitutes static friendly face changes and selected highlights.

## Furniture and sound
Table/udu: stable terracotta drum object/soft rings with African instrument provenance text **only** if accurate; don't visually claim table literally turns into an udu. Chair/mbira: floating tine shimmer / plucked arcs. Lamp/erhu: floating bow ribbon/string tremble. Use non-literal friendly silhouettes rather than stereotyping cultural objects.
MVP audio targets: three distinctive instrument-inspired timbres, never chiptune-only unless clearly a placeholder. Prefer licensed samples with source author/license/proof in `docs/AUDIO_ASSET_LICENSES.md`. If authentic sample unavailable, clearly call it 'udu-inspired synth', 'mbira-inspired synth', 'erhu-inspired synth'. Don't download automatically from questionable sources. Audio levels headroom and no jump-scares.

## Blender pipeline
If Blender MCP is available and trusted: read available tools and versions; ask no credential unless essential; block out precisely one ball, test import pipeline, then batch three with single shared character material style. If not available use Blender CLI headless (check binary) or procedural Unity meshes. Do not block functional prototype on MCP. Export FBX/glTF with supported Unity import path; `*.blend` sources retained. Separate collision roots from art geometry; measured collider size stable. Test faces import and audio mapping before full asset rollout.

## Performance and asset bounds (targets, not results)
Simple meshes, pooled mini echoes, limited transparent materials, unlit/URP-compatible shaders where possible. Avoid 1.4 million-triangle GenAI objects, expensive physics on tiny orbit ghosts, skeletal meshes for faces if texture/material swaps suffice. Profile on target device. No hard claimed tri budget until target hardware is confirmed.

## Accessibility in art
Shapes and symbols always differentiate orbs. UI labels optional but scalable. Pitch higher/lower cues need large enough readable layout. Avoid color-only feedback or animation-only timing. Reduced-motion visuals retain functional beat progress / note ID. Face meaning reinforced by readable effect + optional short text.
