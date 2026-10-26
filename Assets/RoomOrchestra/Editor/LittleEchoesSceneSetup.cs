using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RoomOrchestra.EditorTools
{
    /// <summary>
    /// Adds the Little Echoes select-and-place controls to the RoomOrchestra scene: an orb tray
    /// (Flutter / Bop / Boom tokens with shape + motif cues), an intensity button, focus markers over
    /// each target, and pre-skins the ball pool as Flutter. Everything is authored in the scene in its
    /// initial state, because PICO Spatial mishandles objects that change during startup.
    /// Safe to re-run: it only replaces the "LittleEchoes" object it created.
    /// Batch: Unity -batchmode -executeMethod RoomOrchestra.EditorTools.LittleEchoesSceneSetup.ApplyAndSave
    /// </summary>
    public static class LittleEchoesSceneSetup
    {
        public const string ScenePath = "Assets/RoomOrchestra/Scenes/RoomOrchestra.unity";
        const string MaterialDir = "Assets/RoomOrchestra/Materials";
        const string RootName = "LittleEchoes";
        const float TokenDiameter = 0.1f;
        const int StartTarget = 1; // chair (middle)
        const int StartPetal = 2;  // middle note

        // Front-left, where the old loop station stood: inside the default view, and seated/standing
        // sight lines to all three targets pass clear of it (checked by SceneLayoutTests).
        static readonly Vector3 DispenserPosition = new Vector3(0.72f, 0f, 1.25f); // clear of every pointer line (strict ray test)
        static readonly Vector3 TrayCenter = new Vector3(-0.55f, 0.5f, 1.1f); // low enough that hand pointer rays to every tile pass over the front row

        [MenuItem("RoomOrchestra/Little Echoes/Set Up Select-and-Place")]
        public static void ApplyAndSave()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            Apply();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[RoomOrchestra] Little Echoes scene setup saved");
        }

        public static void Apply()
        {
            KeepExportAlignedWithBuild();
            RemoveRetiredLoopStation();

            var parent = GameObject.Find("RoomOrchestra");
            var old = parent != null ? parent.transform.Find(RootName) : null;
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var orbMaterials = OrbCatalog.All
                .Select(p => EnsureMaterial($"Orb_{p.Type}", "Ball", new Color(p.R, p.G, p.B)))
                .ToArray();
            var motifMaterial = EnsureMaterial("OrbMotif", "Ball", new Color(0.12f, 0.14f, 0.18f));
            var focusMaterial = EnsureMaterial("FocusMarker", "AimRing", new Color(1f, 0.95f, 0.6f));
            var slabMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/Pedestal.mat");

            var root = new GameObject(RootName);
            if (parent != null) root.transform.SetParent(parent.transform, false);

            // Orb tray
            var tray = new GameObject("OrbTray").transform;
            tray.SetParent(root.transform, false);
            tray.position = TrayCenter;
            Box("TraySlab", tray, Vector3.zero, new Vector3(0.56f, 0.03f, 0.18f), slabMaterial, keepCollider: false);
            Box("TrayPost", tray, new Vector3(0f, -TrayCenter.y / 2f, 0f), new Vector3(0.04f, TrayCenter.y, 0.04f), slabMaterial, keepCollider: false);

            var tokens = new OrbToken[OrbCatalog.Count];
            for (int i = 0; i < OrbCatalog.Count; i++)
            {
                var profile = OrbCatalog.All[i];
                tokens[i] = CreateToken(profile, tray, new Vector3(-0.19f + 0.19f * i, 0.09f, 0f),
                    orbMaterials[i], motifMaterial, selected: profile.Type == OrbType.Flutter);
                Label($"Label_{profile.Type}", profile.DisplayName, tray, new Vector3(-0.19f + 0.19f * i, 0.025f, -0.095f), 0.022f);
            }

            var tierButton = CreateTierButton(root.transform, slabMaterial, motifMaterial);

            // Targets left to right; one focus marker floats above the focused one
            var targets = Object.FindObjectsByType<InstrumentTarget>(FindObjectsSortMode.None)
                .OrderBy(t => t.AimPosition.x).ToArray();
            const float markerLift = 0.3f;
            var marker = Primitive(PrimitiveType.Cylinder, "FocusMarker", root.transform, focusMaterial, keepCollider: false);
            marker.position = targets[StartTarget].AimPosition + Vector3.up * markerLift;
            marker.localScale = new Vector3(0.14f, 0.006f, 0.14f);

            // Pitch columns: five labeled tiles beside each target, low to high.
            var columns = targets.Select((t, i) => CreatePitchColumn(t, root.transform, slabMaterial, i == StartTarget)).ToArray();

            var launcher = Object.FindFirstObjectByType<BallLauncher>();
            PreSkinBalls(orbMaterials[(int)OrbType.Flutter]);

            // Echo orbits: one clock-face ring per target, all driven by one conductor.
            var tickMaterial = EnsureMaterial("OrbitTick", "Ball", new Color(0.85f, 0.87f, 0.9f));
            var orbits = targets.Select(t => CreateOrbit(t, root.transform, orbMaterials, tickMaterial, focusMaterial, motifMaterial)).ToArray();
            var conductor = root.AddComponent<EchoConductor>();
            conductor.Configure(orbits);
            EditorUtility.SetDirty(conductor);
            CreateUndoButton(root.transform, slabMaterial, motifMaterial);
            CreateMusicButton(MusicControlButton.Kind.Pause, new Vector3(0.13f, TrayCenter.y, 1.0f), root.transform, slabMaterial, motifMaterial);
            CreateMusicButton(MusicControlButton.Kind.Clear, new Vector3(0.28f, TrayCenter.y, 1.0f), root.transform, slabMaterial, motifMaterial);
            CreateRecenterButton(new Vector3(0.43f, TrayCenter.y, 1.0f), root.transform, slabMaterial, motifMaterial);
            CreateMotionButton(new Vector3(0.58f, TrayCenter.y, 1.0f), root.transform, slabMaterial, motifMaterial);
            // The ball stand steps right and back to make room for the Motion button (still seated reach).
            var dispenser = parent != null ? parent.transform.Find("BallDispenser") : null;
            if (dispenser != null) dispenser.position = DispenserPosition;
            if (parent != null && !parent.TryGetComponent<StageAnchor>(out _)) parent.AddComponent<StageAnchor>();

            var selector = root.AddComponent<OrbSelector>();
            var so = new SerializedObject(selector);
            SetArray(so.FindProperty("targets"), targets);
            so.FindProperty("focusMarker").objectReferenceValue = marker;
            so.FindProperty("focusMarkerLift").floatValue = markerLift;
            SetArray(so.FindProperty("tokens"), tokens);
            so.FindProperty("tierButton").objectReferenceValue = tierButton;
            SetArray(so.FindProperty("pitchColumns"), columns);
            so.FindProperty("startPetal").intValue = StartPetal;
            so.FindProperty("launcher").objectReferenceValue = launcher;
            so.FindProperty("startOrb").enumValueIndex = (int)OrbType.Flutter;
            so.FindProperty("startTarget").intValue = StartTarget;
            so.FindProperty("startTier").enumValueIndex = (int)IntensityTier.Medium;
            so.ApplyModifiedPropertiesWithoutUndo();

            var controller = root.AddComponent<ControllerButtonInput>();
            var cso = new SerializedObject(controller);
            cso.FindProperty("selector").objectReferenceValue = selector;
            cso.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// The SpatialAdapter exporter writes EditorOnly objects that the player build strips, so every later
        /// object renders with its neighbour's mesh/material/pose on device. Untag them; switch cameras off instead.
        /// </summary>
        static void KeepExportAlignedWithBuild()
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!t.CompareTag("EditorOnly")) continue;
                t.gameObject.tag = "Untagged";
                if (t.TryGetComponent<Camera>(out _) && !t.TryGetComponent<EditorPreviewOnly>(out _))
                    t.gameObject.AddComponent<EditorPreviewOnly>();
                Debug.Log($"[RoomOrchestra] {t.name}: EditorOnly tag removed so the Spatial export matches the build");
            }
        }

        /// <summary>
        /// The RoomOrchestra loop recorder is replaced by Little Echoes orbits (owner approved 2026-10-22).
        /// Only the scene object goes; its scripts stay in the project.
        /// </summary>
        static void RemoveRetiredLoopStation()
        {
            foreach (var station in Object.FindObjectsByType<LoopStation>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Debug.Log($"[RoomOrchestra] Removed retired loop station '{station.name}' from the scene");
                Object.DestroyImmediate(station.gameObject);
            }
        }

        static OrbToken CreateToken(OrbProfile profile, Transform tray, Vector3 localPosition,
            Material body, Material motif, bool selected)
        {
            var baseScale = new Vector3(profile.ScaleX, profile.ScaleY, profile.ScaleZ) * TokenDiameter;
            var t = Primitive(PrimitiveType.Sphere, $"Token_{profile.Type}", tray, body, keepCollider: true);
            t.localPosition = localPosition;
            t.localScale = selected ? baseScale * 1.3f : baseScale;

            // Motif on the side facing the player (-Z), so orbs differ by symbol as well as shape.
            switch (profile.Type)
            {
                case OrbType.Flutter: // sparkle: a small diamond
                    var sparkle = Primitive(PrimitiveType.Cube, "Motif_Sparkle", t, motif, keepCollider: false);
                    sparkle.localPosition = new Vector3(0f, 0.12f, -0.47f);
                    sparkle.localRotation = Quaternion.Euler(0f, 0f, 45f);
                    sparkle.localScale = new Vector3(0.3f, 0.3f, 0.1f);
                    break;
                case OrbType.Bop: // single dot
                    var dot = Primitive(PrimitiveType.Sphere, "Motif_Dot", t, motif, keepCollider: false);
                    dot.localPosition = new Vector3(0f, 0.1f, -0.45f);
                    dot.localScale = Vector3.one * 0.24f;
                    break;
                case OrbType.Boom: // ripple: three stacked arcs
                    for (int i = 0; i < 3; i++)
                    {
                        var arc = Primitive(PrimitiveType.Cube, $"Motif_Ripple{i}", t, motif, keepCollider: false);
                        arc.localPosition = new Vector3(0f, 0.22f - 0.14f * i, -0.46f);
                        arc.localScale = new Vector3(0.6f - 0.12f * i, 0.06f, 0.1f);
                    }
                    break;
            }

            var token = t.gameObject.AddComponent<OrbToken>();
            var so = new SerializedObject(token);
            so.FindProperty("orb").enumValueIndex = (int)profile.Type;
            so.FindProperty("baseScale").vector3Value = baseScale;
            so.ApplyModifiedPropertiesWithoutUndo();
            return token;
        }

        static TierButton CreateTierButton(Transform root, Material slab, Material bars)
        {
            var go = new GameObject("TierButton");
            go.transform.SetParent(root, false);
            go.transform.position = TrayCenter + new Vector3(0.36f, 0f, 0f);
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.04f, 0f);
            box.size = new Vector3(0.14f, 0.12f, 0.1f);
            Box("TierSlab", go.transform, Vector3.zero, new Vector3(0.14f, 0.03f, 0.1f), slab, keepCollider: false);

            var raised = new Vector3(0.03f, 0.1f, 0.03f);
            var lowered = new Vector3(0.03f, 0.01f, 0.03f);
            var barTransforms = new Transform[3];
            for (int i = 0; i < 3; i++)
            {
                var bar = Primitive(PrimitiveType.Cube, $"TierBar{i}", go.transform, bars, keepCollider: false);
                bar.localPosition = new Vector3(-0.04f + 0.04f * i, 0.015f, 0f);
                bar.localScale = i <= (int)IntensityTier.Medium ? raised : lowered;
                barTransforms[i] = bar;
            }

            Label("TierLabel", "Loudness", go.transform, new Vector3(0f, 0.025f, -0.065f), 0.02f);
            var tier = go.AddComponent<TierButton>();
            var so = new SerializedObject(tier);
            SetArray(so.FindProperty("bars"), barTransforms);
            so.FindProperty("raisedScale").vector3Value = raised;
            so.FindProperty("loweredScale").vector3Value = lowered;
            so.ApplyModifiedPropertiesWithoutUndo();
            return tier;
        }

        static readonly Vector3 PlayerEye = new Vector3(0f, 1.15f, 0f); // seated eye height at the start position
        const float OrbitRadius = 0.3f;
        const float MiniDiameter = 0.05f;

        // A clock face facing the player: lamp ring around the shade; chair/table rings float above.
        static OrbitView CreateOrbit(InstrumentTarget target, Transform root, Material[] orbMaterials,
            Material tick, Material marker, Material hub)
        {
            float lift = target.Instrument == InstrumentType.Erhu ? 0f : 0.4f;
            var go = new GameObject($"Orbit_{target.Instrument}");
            go.transform.SetParent(root, false);
            go.transform.position = target.AimPosition + Vector3.up * lift;
            go.transform.rotation = Quaternion.LookRotation(go.transform.position - PlayerEye, Vector3.up);

            for (int i = 0; i < MusicClock.StepsPerBar; i++)
            {
                float a = i / (float)MusicClock.StepsPerBar * Mathf.PI * 2f;
                var t = Primitive(PrimitiveType.Sphere, $"Tick{i:00}", go.transform, tick, keepCollider: false);
                t.localPosition = new Vector3(Mathf.Sin(a), Mathf.Cos(a), 0f) * OrbitRadius;
                t.localScale = Vector3.one * (i % MusicClock.StepsPerBeat == 0 ? 0.03f : 0.014f); // beats stand out
            }

            var mark = Primitive(PrimitiveType.Cube, "PlayMarker", go.transform, marker, keepCollider: false);
            mark.localPosition = new Vector3(0f, OrbitRadius + 0.055f, 0f);
            mark.localRotation = Quaternion.Euler(0f, 0f, 45f);
            mark.localScale = new Vector3(0.035f, 0.035f, 0.012f);

            Transform park;
            if (lift > 0f)
            {
                park = Primitive(PrimitiveType.Sphere, "Hub", go.transform, hub, keepCollider: false);
                park.localScale = Vector3.one * 0.09f;
            }
            else
            {
                park = new GameObject("Park").transform; // inside the lamp shade, hidden
                park.SetParent(go.transform, false);
            }

            var view = go.AddComponent<OrbitView>();
            var pools = new Transform[OrbCatalog.Count][];
            for (int o = 0; o < OrbCatalog.Count; o++)
            {
                var profile = OrbCatalog.All[o];
                pools[o] = new Transform[EchoSequencer.MaxPerTarget];
                for (int m = 0; m < pools[o].Length; m++)
                {
                    var mini = Primitive(PrimitiveType.Sphere, $"Echo_{profile.Type}_{m}", go.transform, orbMaterials[o], keepCollider: true);
                    mini.position = park.position;
                    mini.localScale = new Vector3(profile.ScaleX, profile.ScaleY, profile.ScaleZ) * MiniDiameter;
                    mini.gameObject.AddComponent<EchoMini>().Configure(view);
                    EditorUtility.SetDirty(mini.GetComponent<EchoMini>());
                    pools[o][m] = mini;
                }
            }
            view.Configure(target, park, pools[0], pools[1], pools[2], OrbitRadius, mark);
            EditorUtility.SetDirty(view);
            return view;
        }

        // Undo, front row between Loudness and Pause: a slab with a hooked arrow.
        static void CreateUndoButton(Transform root, Material slab, Material mark)
        {
            var go = new GameObject("UndoButton");
            go.transform.SetParent(root, false);
            go.transform.position = TrayCenter + new Vector3(0.53f, 0f, -0.1f); // beside, not in front of, Loudness
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.03f, 0f);
            box.size = new Vector3(0.12f, 0.08f, 0.08f);
            Box("UndoSlab", go.transform, Vector3.zero, new Vector3(0.12f, 0.03f, 0.07f), slab, keepCollider: false);
            Box("UndoArrowStem", go.transform, new Vector3(0.01f, 0.025f, 0f), new Vector3(0.06f, 0.012f, 0.012f), mark, keepCollider: false);
            Box("UndoArrowHook", go.transform, new Vector3(0.04f, 0.04f, 0f), new Vector3(0.012f, 0.03f, 0.012f), mark, keepCollider: false);
            var head = Box("UndoArrowHead", go.transform, new Vector3(-0.025f, 0.025f, 0f), new Vector3(0.022f, 0.022f, 0.012f), mark, keepCollider: false);
            head.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Label("UndoLabel", "Undo", go.transform, new Vector3(0f, 0.02f, -0.05f), 0.02f);
            go.AddComponent<UndoButton>();
        }

        // Pause/Play and Clear in the front control row, right of Undo (clear of the chair sight line): a slab, an icon (pause bars / an X) and a label.
        static void CreateMusicButton(MusicControlButton.Kind kind, Vector3 position, Transform root, Material slab, Material mark)
        {
            var go = new GameObject($"{kind}Button");
            go.transform.SetParent(root, false);
            go.transform.position = position;
            go.transform.rotation = FacePlayer(position);
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.03f, 0f);
            box.size = new Vector3(0.13f, 0.08f, 0.1f);
            Box($"{kind}Slab", go.transform, Vector3.zero, new Vector3(0.13f, 0.03f, 0.09f), slab, keepCollider: false);
            Transform pauseIcon = null, playIcon = null;
            if (kind == MusicControlButton.Kind.Pause)
            {
                // Icons are 0.028 m tall so the parked one fits wholly inside the 0.03 m slab.
                pauseIcon = Holder("PauseIcon", go.transform, MusicControlButton.DefaultIconShown);
                Box("PauseBarLeft", pauseIcon, new Vector3(-0.012f, 0f, 0f), new Vector3(0.012f, 0.028f, 0.012f), mark, keepCollider: false);
                Box("PauseBarRight", pauseIcon, new Vector3(0.012f, 0f, 0f), new Vector3(0.012f, 0.028f, 0.012f), mark, keepCollider: false);
                playIcon = Holder("PlayIcon", go.transform, MusicControlButton.DefaultIconParked);
                var triangle = new GameObject("PlayTriangle");
                triangle.transform.SetParent(playIcon, false);
                triangle.transform.localScale = new Vector3(0.026f, 0.028f, 0.012f);
                triangle.AddComponent<MeshFilter>().sharedMesh = EnsurePlayTriangleMesh();
                triangle.AddComponent<MeshRenderer>().sharedMaterial = mark;
            }
            else
            {
                var a = Box("ClearCrossA", go.transform, new Vector3(0f, 0.03f, 0f), new Vector3(0.045f, 0.01f, 0.01f), mark, keepCollider: false);
                a.localRotation = Quaternion.Euler(0f, 0f, 45f);
                var c = Box("ClearCrossB", go.transform, new Vector3(0f, 0.03f, 0f), new Vector3(0.045f, 0.01f, 0.01f), mark, keepCollider: false);
                c.localRotation = Quaternion.Euler(0f, 0f, -45f);
            }
            var label = Label($"{kind}Label", kind == MusicControlButton.Kind.Pause ? "Pause" : "Clear", go.transform, new Vector3(0f, 0.02f, -0.06f), 0.02f);
            var button = go.AddComponent<MusicControlButton>();
            button.Configure(kind, label.GetComponentInChildren<TMP_Text>(), pauseIcon, playIcon);
            EditorUtility.SetDirty(button);
        }

        // Recenter, at the end of the front row: a target mark (ring and dot) facing the player.
        static void CreateRecenterButton(Vector3 position, Transform root, Material slab, Material mark)
        {
            var go = new GameObject("RecenterButton");
            go.transform.SetParent(root, false);
            go.transform.position = position;
            go.transform.rotation = FacePlayer(position);
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.03f, 0f);
            box.size = new Vector3(0.13f, 0.08f, 0.1f);
            Box("RecenterSlab", go.transform, Vector3.zero, new Vector3(0.13f, 0.03f, 0.09f), slab, keepCollider: false);
            var ring = Primitive(PrimitiveType.Cylinder, "RecenterRing", go.transform, mark, keepCollider: false);
            ring.localPosition = new Vector3(0f, 0.035f, 0f);
            ring.localRotation = Quaternion.Euler(90f, 0f, 0f);
            ring.localScale = new Vector3(0.036f, 0.004f, 0.036f);
            var gap = Primitive(PrimitiveType.Cylinder, "RecenterRingGap", go.transform, slab, keepCollider: false);
            gap.localPosition = new Vector3(0f, 0.035f, -0.0045f);
            gap.localRotation = Quaternion.Euler(90f, 0f, 0f);
            gap.localScale = new Vector3(0.024f, 0.001f, 0.024f);
            var dot = Primitive(PrimitiveType.Cylinder, "RecenterDot", go.transform, mark, keepCollider: false);
            dot.localPosition = new Vector3(0f, 0.035f, -0.006f);
            dot.localRotation = Quaternion.Euler(90f, 0f, 0f);
            dot.localScale = new Vector3(0.01f, 0.001f, 0.01f);
            Label("RecenterLabel", "Recenter", go.transform, new Vector3(0f, 0.02f, -0.06f), 0.02f);
            go.AddComponent<RecenterButton>();
        }

        // Motion (Full / Calm), at the end of the front row: three still dots, and a label with the mode.
        static void CreateMotionButton(Vector3 position, Transform root, Material slab, Material mark)
        {
            var go = new GameObject("MotionButton");
            go.transform.SetParent(root, false);
            go.transform.position = position;
            go.transform.rotation = FacePlayer(position);
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.03f, 0f);
            box.size = new Vector3(0.13f, 0.08f, 0.1f);
            Box("MotionSlab", go.transform, Vector3.zero, new Vector3(0.13f, 0.03f, 0.09f), slab, keepCollider: false);
            for (int i = -1; i <= 1; i++)
            {
                var dot = Primitive(PrimitiveType.Sphere, $"MotionDot{i + 1}", go.transform, mark, keepCollider: false);
                dot.localPosition = new Vector3(0.022f * i, 0.025f, 0f);
                dot.localScale = Vector3.one * 0.014f;
            }
            var label = Label("MotionLabel", MotionToggleButton.TextFor(MotionMode.Full), go.transform, new Vector3(0f, 0.02f, -0.06f), 0.04f);
            var button = go.AddComponent<MotionToggleButton>();
            button.Configure(label.GetComponentInChildren<TMP_Text>());
            EditorUtility.SetDirty(button);
        }

        // Beside each target, to its right as the player sees it: D E F# A B (or the five udu sounds)
        // from the lowest tile to the highest, so height still means pitch.
        static readonly Dictionary<InstrumentType, float> ColumnX = new()
        {
            { InstrumentType.Udu, -0.45f },   // in the gap right of the table
            { InstrumentType.Mbira, 0.47f },  // right of the chair, clear of its orbit ring
            { InstrumentType.Erhu, 1.53f },   // right of the lamp, clear of its orbit ring
        };
        const float ColumnBottom = 0.55f, ColumnStep = 0.15f;
        static readonly Vector3 TileScale = new Vector3(0.14f, 0.11f, 0.03f);

        static PitchColumn CreatePitchColumn(InstrumentTarget target, Transform root, Material slab, bool focused)
        {
            var go = new GameObject($"PitchColumn_{target.Instrument}");
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(ColumnX[target.Instrument], 0f, target.AimPosition.z);
            var tiles = new PitchTile[NoteMapping.Count];
            for (int i = 0; i < tiles.Length; i++)
            {
                var tile = Primitive(PrimitiveType.Cube, $"PitchTile{i}", go.transform, slab, keepCollider: true);
                tile.position = new Vector3(go.transform.position.x, ColumnBottom + ColumnStep * i, go.transform.position.z);
                tile.rotation = FacePlayer(tile.position);
                tile.localScale = focused && i == StartPetal ? TileScale * 1.2f : TileScale;
                var note = new MusicalNote(target.Instrument, i);
                string text = note.IsPercussion ? note.Articulation.ToString() : NoteMapping.NoteName(i);
                // Label on the tile's player-facing side, sized in world units (not stretched by the tile).
                var label = Label($"TileLabel{i}", text, go.transform, Vector3.zero, note.IsPercussion ? 0.034f : 0.045f);
                label.position = tile.position - tile.forward * 0.02f;
                label.rotation = tile.rotation;
                tiles[i] = tile.gameObject.AddComponent<PitchTile>();
                tiles[i].Configure(target, i);
                EditorUtility.SetDirty(tiles[i]);
            }
            // The instrument's name tile is also its volume button (tap steps 100/60/30%/Muted).
            var mixTile = Primitive(PrimitiveType.Cube, "MixTile", go.transform, slab, keepCollider: true);
            mixTile.position = new Vector3(go.transform.position.x, ColumnBottom - 0.14f, go.transform.position.z);
            mixTile.rotation = FacePlayer(mixTile.position);
            mixTile.localScale = new Vector3(0.16f, 0.1f, 0.03f);
            string name = InstrumentCaption(target.Instrument);
            var caption = Label("InstrumentLabel", MixButton.TextFor(name, 0), go.transform, Vector3.zero, 0.07f); // two lines, ~3 cm each
            caption.position = mixTile.position - mixTile.forward * 0.02f;
            caption.rotation = mixTile.rotation;
            var mix = mixTile.gameObject.AddComponent<MixButton>();
            mix.Configure(target, caption.GetComponentInChildren<TMP_Text>(), name);
            EditorUtility.SetDirty(mix);

            var column = go.AddComponent<PitchColumn>();
            column.Configure(target, tiles, TileScale);
            EditorUtility.SetDirty(column);
            return column;
        }

        static string InstrumentCaption(InstrumentType type) => type switch
        {
            InstrumentType.Udu => "udu",
            InstrumentType.Mbira => "mbira",
            _ => "erhu",
        };

        static Quaternion FacePlayer(Vector3 position)
        {
            var away = position - PlayerEye;
            away.y = 0f;
            return Quaternion.LookRotation(away.sqrMagnitude > 1e-6f ? away : Vector3.forward, Vector3.up);
        }

        /// <summary>
        /// Clear text label facing the player, white, centred. TextMesh Pro, because PICO Spatial's
        /// runtime syncs TMP text (plain TextMesh with a dynamic font rendered nothing on the emulator).
        /// <paramref name="height"/> is the measured text height in metres.
        /// </summary>
        static Transform Label(string name, string text, Transform parent, Vector3 localPosition, float height)
        {
            // A plain holder carries the pose; the TMP text sits at its origin. TMP turns its object into
            // a RectTransform, which (with no UI parent) did not keep its saved height in the scene.
            var holder = new GameObject(name).transform;
            holder.SetParent(parent, false);
            holder.localPosition = localPosition;
            holder.rotation = FacePlayer(holder.position);
            var go = new GameObject("Text");
            go.transform.SetParent(holder, false);
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.font = TMP_Settings.defaultFontAsset;
            tmp.text = text;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.color = Color.white;
            tmp.rectTransform.sizeDelta = new Vector2(1f, 0.25f);
            // Measure, then scale the font so the rendered text is exactly the requested height.
            tmp.fontSize = 1f;
            tmp.ForceMeshUpdate(true, true);
            float measured = tmp.textBounds.size.y;
            if (measured > 1e-5f) tmp.fontSize = height / measured;
            tmp.ForceMeshUpdate(true, true);
            return holder;
        }

        static void PreSkinBalls(Material flutter)
        {
            var profile = OrbCatalog.Get(OrbType.Flutter);
            foreach (var ball in Object.FindObjectsByType<Ball>(FindObjectsSortMode.None))
            {
                var so = new SerializedObject(ball);
                so.FindProperty("orb").enumValueIndex = (int)OrbType.Flutter;
                float diameter = so.FindProperty("diameter").floatValue;
                so.ApplyModifiedPropertiesWithoutUndo();
                ball.transform.localScale = new Vector3(profile.ScaleX, profile.ScaleY, profile.ScaleZ) * diameter;
                var renderer = ball.GetComponent<Renderer>();
                if (renderer != null) renderer.sharedMaterial = flutter;
                var body = ball.GetComponent<Rigidbody>();
                if (body != null) body.mass = profile.Mass;
            }
        }

        static Material EnsureMaterial(string name, string template, Color color)
        {
            string path = $"{MaterialDir}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var source = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/{template}.mat");
                material = new Material(source);
                AssetDatabase.CreateAsset(material, path);
            }
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            EditorUtility.SetDirty(material);
            return material;
        }

        static Transform Holder(string name, Transform parent, Vector3 localPosition)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPosition;
            return t;
        }

        const string PlayTrianglePath = "Assets/RoomOrchestra/Meshes/PlayTriangle.asset";

        /// <summary>Unit triangular prism pointing +x (a play symbol), flat-shaded, saved as an asset.</summary>
        static Mesh EnsurePlayTriangleMesh()
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(PlayTrianglePath);
            if (mesh == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/RoomOrchestra/Meshes"))
                    AssetDatabase.CreateFolder("Assets/RoomOrchestra", "Meshes");
                mesh = new Mesh { name = "PlayTriangle" };
                AssetDatabase.CreateAsset(mesh, PlayTrianglePath);
            }
            Vector3 a = new(-0.5f, -0.5f, 0f), b = new(-0.5f, 0.5f, 0f), c = new(0.5f, 0f, 0f), d = new(0f, 0f, 0.5f);
            Vector3 front = -d, back = d;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            void Face(params Vector3[] corners) // corners wound clockwise as seen from outside
            {
                int start = vertices.Count;
                vertices.AddRange(corners);
                for (int i = 1; i < corners.Length - 1; i++) triangles.AddRange(new[] { start, start + i, start + i + 1 });
            }
            Face(a + front, b + front, c + front);
            Face(a + back, c + back, b + back);
            Face(a + front, a + back, b + back, b + front); // flat left side
            Face(b + front, b + back, c + back, c + front);
            Face(c + front, c + back, a + back, a + front);
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        static Transform Box(string name, Transform parent, Vector3 localPosition, Vector3 scale, Material material, bool keepCollider)
        {
            var t = Primitive(PrimitiveType.Cube, name, parent, material, keepCollider);
            t.localPosition = localPosition;
            t.localScale = scale;
            return t;
        }

        static Transform Primitive(PrimitiveType type, string name, Transform parent, Material material, bool keepCollider)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            if (material != null) go.GetComponent<Renderer>().sharedMaterial = material;
            return go.transform;
        }

        static void SetArray<T>(SerializedProperty property, T[] values) where T : Object
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}
