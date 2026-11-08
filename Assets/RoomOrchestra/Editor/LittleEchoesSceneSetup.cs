using System.Collections.Generic;
using System.Linq;
using ByteDance.PICO.SpatialAdapter.Component;
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
        static readonly Vector3 DispenserPosition = new Vector3(0.62f, 0f, 1.15f); // clear of every pointer line (strict ray test)
        // The leaf palette (owner sketch 2026-10-27): one green leaf, the three characters and Loudness
        // along the back, a row of small buttons along the front. Left of the chair sight line (x = 0)
        // where it is tall, at least 1 m out, and low enough that hand pointer rays to every tile pass over it.
        static readonly Vector3 LeafCenter = new Vector3(-0.2f, 0.49f, 1.17f);
        static readonly Vector3 LeafSize = new Vector3(1.02f, 0.03f, 0.42f); // back edge below the standing sight line to the chair
        static readonly Vector3 TrayCenter = new Vector3(-0.3f, 0.5f, 1.28f); // the back row
        const float ButtonRowZ = 1.1f, ButtonY = 0.5f, ButtonStep = 0.1f;
        static readonly Vector3 SmallSlab = new Vector3(0.09f, 0.03f, 0.07f);
        static readonly Vector3 SmallCollider = new Vector3(0.095f, 0.08f, 0.08f);
        // Low and in front of the slab: the label's dark plate sits below the button's icon, not over it.
        static readonly Vector3 SmallLabelOffset = new Vector3(0f, 0.004f, -0.075f);
        const float SmallLabelHeight = 0.017f;

        // Nine buttons centred on the leaf (Lesson, then Quit, joined the row).
        static Vector3 FrontRow(int slot) => new Vector3(LeafCenter.x + ButtonStep * (slot - 3.8f), ButtonY, ButtonRowZ);

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
            // A saved scene has the guided song's stations parked in the cloud: bring them home before rebuilding.
            foreach (var station in Object.FindObjectsByType<StationReveal>(FindObjectsSortMode.None)) station.ShowNow();
            FitFurnitureToRealSizes();

            var parent = GameObject.Find("RoomOrchestra");
            var old = parent != null ? parent.transform.Find(RootName) : null;
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var orbMaterials = OrbCatalog.All
                .Select(p => Glow(EnsureMaterial($"Orb_{p.Type}", "Ball", new Color(p.R, p.G, p.B)), new Color(p.R, p.G, p.B)))
                .ToArray();
            var motifMaterial = EnsureMaterial("OrbMotif", "Ball", new Color(0.12f, 0.14f, 0.18f));
            var focusMaterial = EnsureMaterial("FocusMarker", "AimRing", new Color(1f, 0.95f, 0.6f));
            var slabMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/Pedestal.mat");

            var root = new GameObject(RootName);
            if (parent != null) root.transform.SetParent(parent.transform, false);
            // Every tappable control lives under one holder that Height (Seated/Standing) lifts together.
            var controls = Holder("Controls", root.transform, Vector3.zero);
            controls.gameObject.AddComponent<ControlsRig>();

            // The leaf pad: tray, buttons and (via RoamingTray) the ball stand travel together after a
            // player who walks around. Pitch columns stay beside their instruments.
            var pad = Holder("LeafPad", controls, Vector3.zero);
            // The leaf and everything on it, under one holder the opening swoops in (IntroSequence).
            var swoop = Holder("LeafSwoop", pad, Vector3.zero);

            var veinMaterial = EnsureMaterial("CallLeafVein", "Ball", new Color(0.86f, 0.95f, 0.78f));
            var leafStalk = CreateLeaf(swoop, pad, veinMaterial);

            // The characters along the back of the leaf
            var tray = new GameObject("OrbTray").transform;
            tray.SetParent(swoop, false);
            tray.position = TrayCenter;

            var tokens = new OrbToken[OrbCatalog.Count];
            for (int i = 0; i < OrbCatalog.Count; i++)
            {
                var profile = OrbCatalog.All[i];
                tokens[i] = CreateToken(profile, tray, new Vector3(-0.17f + 0.17f * i, 0.06f, 0f), // resting on the leaf: hand rays to the udu volume tile pass over them
                    orbMaterials[i], motifMaterial, selected: profile.Type == OrbType.Flutter);
                var name = Label($"Label_{profile.Type}", profile.DisplayName, tray, new Vector3(-0.17f + 0.17f * i, 0.004f, -0.092f), 0.02f); // below the face, in front
                Localize(name, profile.Type switch { OrbType.Flutter => UiTerm.Flutter, OrbType.Bop => UiTerm.Bop, _ => UiTerm.Boom });
            }

            var tierButton = CreateTierButton(swoop, slabMaterial, motifMaterial);
            CreateSoundsButton(swoop, slabMaterial, motifMaterial);
            CreateViewButton(swoop, slabMaterial, motifMaterial);
            CreateCallButton(FrontRow(0), swoop, slabMaterial, veinMaterial);

            // Targets left to right; one focus marker floats above the focused one
            var targets = Object.FindObjectsByType<InstrumentTarget>(FindObjectsSortMode.None)
                .OrderBy(t => t.AimPosition.x).ToArray();
            // The furniture lights up under the pointer too (its colliders sit on its visible parts).
            foreach (var target in targets)
                foreach (var part in target.GetComponentsInChildren<Collider>())
                    if (part.TryGetComponent<MeshRenderer>(out _)) Hoverable(part.gameObject);
            const float markerLift = 0.3f;
            var marker = Primitive(PrimitiveType.Cylinder, "FocusMarker", root.transform, focusMaterial, keepCollider: false);
            marker.position = targets[StartTarget].AimPosition + Vector3.up * markerLift;
            marker.localScale = new Vector3(0.14f, 0.006f, 0.14f);

            // Pitch columns: five labeled tiles beside each target, low to high.
            var columns = targets.Select((t, i) => CreatePitchColumn(t, controls, slabMaterial, i == StartTarget)).ToArray();

            var launcher = Object.FindFirstObjectByType<BallLauncher>();
            PreSkinBalls(orbMaterials[(int)OrbType.Flutter]);

            // Echo orbits: one clock-face ring per target, all driven by one conductor.
            var tickMaterial = EnsureMaterial("OrbitTick", "Ball", new Color(0.85f, 0.87f, 0.9f));
            var orbits = targets.Select(t => CreateOrbit(t, root.transform, orbMaterials, tickMaterial, focusMaterial, motifMaterial)).ToArray();
            var conductor = root.AddComponent<EchoConductor>();
            conductor.Configure(orbits);
            EditorUtility.SetDirty(conductor);
            var director = root.AddComponent<CharacterDirector>();
            director.Configure(conductor, tokens);
            EditorUtility.SetDirty(director);
            // The front row of the leaf, left to right after Come here.
            CreateUndoButton(FrontRow(1), swoop, slabMaterial, motifMaterial);
            CreateMusicButton(MusicControlButton.Kind.Pause, FrontRow(2), swoop, slabMaterial, motifMaterial);
            CreateMusicButton(MusicControlButton.Kind.Clear, FrontRow(3), swoop, slabMaterial, motifMaterial);
            CreateRecenterButton(FrontRow(4), swoop, slabMaterial, motifMaterial);
            CreateHeightButton(FrontRow(5), swoop, slabMaterial, motifMaterial);
            CreateLanguageButton(FrontRow(6), swoop, slabMaterial);
            CreateLessonButton(FrontRow(7), swoop, slabMaterial, motifMaterial);
            CreateQuitButton(FrontRow(8), swoop, slabMaterial, motifMaterial);
            root.AddComponent<LanguageDirector>();
            // The ball stand sits right of and behind the front row (clear of every pointer line).
            var dispenser = parent != null ? parent.transform.Find("BallDispenser") : null;
            if (dispenser != null)
            {
                dispenser.position = DispenserPosition;
                CreateCloudStand(dispenser);
            }
            if (parent != null && !parent.TryGetComponent<StageAnchor>(out _)) parent.AddComponent<StageAnchor>();
            MarkGround();
            // Safety limiter on the final mix (dense songs never clip the output).
            var listener = Object.FindFirstObjectByType<AudioListener>();
            if (listener != null && !listener.TryGetComponent<MasterLimiter>(out _)) listener.gameObject.AddComponent<MasterLimiter>();
            CreatePoofClouds(root.transform);
            // The ball stand and the poof clouds waiting in it travel with the pad; thrown balls stay put.
            var carried = new List<Transform> { root.transform.Find("PoofPark") };
            if (dispenser != null) carried.Insert(0, dispenser);
            pad.gameObject.AddComponent<RoamingTray>().Configure(root.transform, carried.ToArray(),
                dispenser != null ? dispenser.Find("BallPool") : null, launcher);
            // Focused start (Version 1 feedback): the stand and what travels with it begin directly in front of the
            // player; the opening glides them home (IntroSequence). Moved before anything parks in the cloud.
            // The aim arc's dots rest in the stand's cloud too, so they start in front with it. The arc is not rebuilt,
            // so it goes back to its home (the stage origin) first, or every rebuild would shift it again.
            var aimArc = parent != null ? parent.transform.Find("AimArc") : null;
            if (aimArc != null) aimArc.localPosition = Vector3.zero;
            var standCarried = carried.Append(parent != null ? parent.transform.Find("AimArc") : null).Where(t => t != null).ToArray();
            var standHome = standCarried.Select(t => t.position).ToArray();
            foreach (var t in standCarried) t.position += StandFrontOffset;
            RainforestArt.Build(root.transform, targets);

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

            CreateInstrumentPacks(root, targets);
            for (int i = 0; i < standCarried.Length; i++) standCarried[i].position = standHome[i]; // home, to record it
            BackLabels(pad, lift: true); // while the leaf is still at home (the opening then poses it out of view)
            CreateEchoBattle(root, conductor);
            var intro = CreateIntro(root, swoop, tokens, leafStalk);
            intro.ConfigureStand(standCarried, StandFrontOffset, launcher); // and back in front for the first frame
            leafStalk.Apply(); // its closed hole waits in the cloud, wherever the cloud starts
            var poofPark = root.transform.Find("PoofPark");
            if (poofPark != null) // idle poof clouds wait in the stand's cloud from the first frame too
                foreach (var cloud in root.GetComponentsInChildren<PoofCloud>(true)) cloud.transform.position = poofPark.position;
            var battleCard = root.transform.Find("EchoBattleCard");
            var parkPoint = parent != null ? parent.transform.Find("BallDispenser/ParkPoint") : null;
            if (battleCard != null && parkPoint != null) battleCard.position = parkPoint.position; // the hidden card too
            // Version 1 feedback ("higher fidelity"): rounded edges on every control and tile, sized while everything is
            // still at full size (the lesson then tucks the lamp's and table's tiles into the cloud).
            RoundedShapes.RoundBlocks(controls, 0.012f);
            TuckForLesson(swoop); // after rounding: the rounded meshes are sized from the controls at home
            CreateTutorial(root, pad, controls, tokens[(int)OrbType.Flutter].transform, targets, columns, orbits, conductor, intro);
            BackLabels(pad.Find("TutorialBubble"), lift: false); // the Skip leaf's label (the bubble is built later)
            RoundedShapes.RoundBlocks(pad, 0.008f); // soft plates too
            CreateViewSettings(root, controls, pad, slabMaterial, motifMaterial);
            CreateConfirmCard(root.transform);
            EnsureRealWalls();
            CreateRealSpots(); // beside the real walls, after them
            RoundedShapes.RoundBlocks(root.transform.Find("TitleCard"), 0.02f); // softened like the controls
        }

        // The real room's walls for the balls (RealWalls): at the scene's top level, in the headset's own space, so
        // recentring the stage never moves the real walls.
        static void EnsureRealWalls()
        {
            var existing = Object.FindFirstObjectByType<RealWalls>();
            var go = existing != null ? existing.gameObject : new GameObject("RealWalls", typeof(RealWalls));
            go.transform.SetParent(null, false);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            go.transform.localScale = Vector3.one;
            if (!go.TryGetComponent<RoomScan>(out _)) go.AddComponent<RoomScan>(); // the labelled scan (furniture), same space
            if (!go.TryGetComponent<RealFurniture>(out _)) go.AddComponent<RealFurniture>(); // after the lesson, instruments onto it
        }

        // Owner (emulator): white words over the light green leaf were hard to read. Every label on the leaf gets a dark
        // rounded plate behind its words, sized for the longest thing it can say in either language, as a child of the
        // text so it grows with Large text. Flutter's bubble has its own dark card.
        static readonly Dictionary<string, string[]> LabelVariants = new()
        {
            { "HeightLabel", new[] { HeightButton.TextFor(PlayPosture.Seated, UiLanguage.English), HeightButton.TextFor(PlayPosture.Standing, UiLanguage.English), HeightButton.TextFor(PlayPosture.Seated, UiLanguage.Chinese), HeightButton.TextFor(PlayPosture.Standing, UiLanguage.Chinese) } },
            { "SoundsLabel", new[] { SoundPackButton.TextFor(UiTerm.PackClassic, UiLanguage.English), SoundPackButton.TextFor(UiTerm.PackEarthy, UiLanguage.English), SoundPackButton.TextFor(UiTerm.PackClassic, UiLanguage.Chinese), SoundPackButton.TextFor(UiTerm.PackEarthy, UiLanguage.Chinese) } },
            { "ViewLabel", System.Enum.GetValues(typeof(ViewMode)).Cast<ViewMode>().SelectMany(m => new[] { ViewButton.TextFor(m, UiLanguage.English), ViewButton.TextFor(m, UiLanguage.Chinese) }).ToArray() },
            { "PauseLabel", new[] { UiText.Get(UiTerm.Pause, UiLanguage.English), UiText.Get(UiTerm.Play, UiLanguage.English), UiText.Get(UiTerm.Pause, UiLanguage.Chinese), UiText.Get(UiTerm.Play, UiLanguage.Chinese) } },
        };

        static void BackLabels(Transform under, bool lift)
        {
            if (under == null) return;
            var bubbleLine = under.Find("BubbleLine") ?? under.Find("TutorialBubble/BubbleLine");
            var backing = EnsureMaterial("LabelBacking", "Ball", new Color(0.12f, 0.08f, 0.16f));
            // 2.4 cm deep: plates inside Flutter's bubble shrink with it into the cloud and must stay above 1 mm there.
            const float padX = 0.012f, padY = 0.006f, depth = 0.024f;
            // The leaf's top surface (at home, where the setup builds): a plate rests on it, never sinks into it.
            float leafTop = LeafCenter.y + LeafSize.y / 2f + 0.003f;

            foreach (var text in under.GetComponentsInChildren<TMP_Text>(true))
            {
                if (bubbleLine != null && text.transform.IsChildOf(bubbleLine)) continue;
                var holder = text.transform.parent;
                var texts = new List<string> { text.text };
                var localized = holder.GetComponent<LocalizedLabel>();
                if (localized != null) { texts.Add(localized.TextFor(UiLanguage.English)); texts.Add(localized.TextFor(UiLanguage.Chinese)); }
                if (LabelVariants.TryGetValue(holder.name, out var more)) texts.AddRange(more);
                string authored = text.text;
                var size = Vector2.zero;
                var centre = Vector3.zero;
                foreach (var t in texts.Where(t => !string.IsNullOrEmpty(t)).Distinct())
                {
                    text.text = t;
                    text.ForceMeshUpdate(true, true);
                    var b = text.textBounds;
                    if (b.size.x > size.x) { size.x = b.size.x; centre.x = b.center.x; }
                    if (b.size.y > size.y) { size.y = b.size.y; centre.y = b.center.y; }
                }
                text.text = authored;
                text.ForceMeshUpdate(true, true);
                if (size == Vector2.zero) continue;
                var old = text.transform.Find("LabelBacking");
                if (old != null) Object.DestroyImmediate(old.gameObject);
                // Just behind the words (text faces the player along -z): in the text's own space, so it scales with it.
                var plate = Box("LabelBacking", text.transform, new Vector3(centre.x, centre.y, depth / 2f + 0.002f),
                    new Vector3(size.x + padX * 2f, size.y + padY * 2f, depth), backing, keepCollider: false);
                plate.localRotation = Quaternion.identity;
                if (!lift) continue;
                // Lift the label until its plate's lowest point clears the leaf (two-line labels need more room).
                var lowest = plate.TransformPoint(new Vector3(0f, -0.5f, 0f)).y;
                if (lowest < leafTop) holder.position += Vector3.up * (leafTop - lowest);
            }
        }

        // A11Y-08: what the View setting changes. Large text grows the leaf's labels (the pitch columns' words are
        // already large and fit their tiles, and Flutter's bubble fits its lines); high contrast darkens every
        // control slab, whitens all control text and brightens the icons (the characters keep their colours).
        static void CreateViewSettings(GameObject root, Transform controls, Transform pad, Material slab, Material mark)
        {
            bool Skip(Component c) => c.GetComponentInParent<OrbToken>() != null || c.transform.IsChildOf(pad.Find("TutorialBubble"));
            var leafLabels = pad.GetComponentsInChildren<TMP_Text>(true).Where(t => !t.enableAutoSizing && !Skip(t)).ToArray();
            var slabs = controls.GetComponentsInChildren<Renderer>(true).Where(r => r.sharedMaterial == slab && !Skip(r)).ToArray();
            var icons = controls.GetComponentsInChildren<Renderer>(true).Where(r => r.sharedMaterial == mark && !Skip(r)).ToArray();
            var texts = controls.GetComponentsInChildren<TMP_Text>(true).Where(t => !Skip(t)).ToArray();
            var view = root.AddComponent<ViewSettings>();
            view.Configure(leafLabels, slabs, icons, texts);
            EditorUtility.SetDirty(view);
        }

        // The guided first song (owner, 2026-10-29): the chair alone at first; the lamp and the table wait in the
        // ball stand's cloud with their tiles, rings, vines and sparkles, and appear in a big poof when the song
        // needs them. Flutter talks in a speech bubble above it, with a Skip leaf; the wanted tile glows.
        // Every station waits in the cloud until the lesson needs it; the chair's mbira too (owner, headset 2026-11-08:
        // the chair and its note keys showed behind the title card).
        static readonly InstrumentType[] Introduced = { InstrumentType.Mbira, InstrumentType.Erhu, InstrumentType.Udu };
        static readonly Vector3 BubbleAt = new Vector3(-0.62f, 0.85f, 1.28f); // above Flutter, left of the sight lines
        // The virtual starting room (owner: start in a virtual room that tears down to reveal the real room): a
        // simple, cosy room of panels around the stage, the seat and everything the music grows. Sage walls in two
        // tones, a warm plank floor, a cream ceiling; thin seams hint that it comes apart. No colliders.
        public static readonly Vector3 RoomMin = new Vector3(-2.6f, 0f, -1.6f), RoomMax = new Vector3(2.6f, 2.7f, 3.8f);
        const int RoomColumns = 5, RoomRows = 2;
        const float PanelDepth = 0.04f, Seam = 0.02f;

        static VirtualRoom CreateVirtualRoom(Transform root, Transform park)
        {
            var room = Holder("VirtualRoom", root, Vector3.zero);
            Material Tone(string name, Color c) => EnsureMaterial(name, "Ball", c);
            var wallA = Tone("RoomWallA", new Color(0.78f, 0.87f, 0.76f));
            var wallB = Tone("RoomWallB", new Color(0.72f, 0.82f, 0.71f));
            var floorA = Tone("RoomFloorA", new Color(0.78f, 0.6f, 0.44f));
            var floorB = Tone("RoomFloorB", new Color(0.71f, 0.54f, 0.39f));
            // Textures: not used. On the PICO Emulator 6.1 a correctly exported base-colour texture on a mesh with UVs drew
            // flat (or as stripes for a 3-channel image); docs/AGENT_PROGRESS.md (2026-10-30, textures) has the evidence.
            foreach (var plank in new[] { floorA, floorB })
                if (plank.HasProperty("_BaseMap") && plank.GetTexture("_BaseMap") != null) { plank.SetTexture("_BaseMap", null); plank.SetTexture("_MainTex", null); EditorUtility.SetDirty(plank); }
            var ceiling = Glow(Tone("RoomCeiling", new Color(0.98f, 0.95f, 0.9f)), new Color(0.5f, 0.48f, 0.45f)); // lit from below too
            var panels = new List<Transform>();
            var size = RoomMax - RoomMin;

            // A wall: a grid of upright panels on the plane through 'at', spanning 'along' (one axis) and the height.
            void Wall(string name, Vector3 start, Vector3 along, float length)
            {
                for (int c = 0; c < RoomColumns; c++)
                    for (int r = 0; r < RoomRows; r++)
                    {
                        float w = length / RoomColumns, h = size.y / RoomRows;
                        var centre = start + along * (w * (c + 0.5f)) + Vector3.up * (h * (r + 0.5f));
                        var panel = Box($"{name}_{c}_{r}", room, centre, new Vector3(w - Seam, h - Seam, PanelDepth), (c + r) % 2 == 0 ? wallA : wallB, keepCollider: true);
                        panel.gameObject.AddComponent<BounceSurface>(); // thrown balls bounce off it with their own sound
                        panel.rotation = Quaternion.LookRotation(Vector3.Cross(along, Vector3.up), Vector3.up);
                        panels.Add(panel);
                    }
            }
            Wall("WallFar", new Vector3(RoomMin.x, 0f, RoomMax.z), Vector3.right, size.x);
            Wall("WallNear", new Vector3(RoomMax.x, 0f, RoomMin.z), Vector3.left, size.x);
            Wall("WallLeft", new Vector3(RoomMin.x, 0f, RoomMin.z), Vector3.forward, size.z);
            Wall("WallRight", new Vector3(RoomMax.x, 0f, RoomMax.z), Vector3.back, size.z);
            // Floor and ceiling: flat grids.
            for (int x = 0; x < RoomColumns; x++)
                for (int z = 0; z < RoomColumns; z++)
                {
                    float w = size.x / RoomColumns, d = size.z / RoomColumns;
                    var at = new Vector3(RoomMin.x + w * (x + 0.5f), 0f, RoomMin.z + d * (z + 0.5f));
                    panels.Add(Box($"Floor_{x}_{z}", room, at + Vector3.down * (PanelDepth / 2f), new Vector3(w - Seam, PanelDepth, d - Seam), (x + z) % 2 == 0 ? floorA : floorB, keepCollider: false));
                    var top = Box($"Ceiling_{x}_{z}", room, at + Vector3.up * (size.y + PanelDepth / 2f), new Vector3(w - Seam, PanelDepth, d - Seam), ceiling, keepCollider: true);
                    top.gameObject.AddComponent<BounceSurface>();
                    panels.Add(top);
                }
            // The ceiling must not shade the stage: the room casts no shadows.
            foreach (var panel in panels) panel.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var virtualRoom = room.gameObject.AddComponent<VirtualRoom>();
            var farMiddle = new Vector3(0f, size.y / 2f, RoomMax.z);
            virtualRoom.Configure(panels.ToArray(), room.InverseTransformPoint(farMiddle), room.InverseTransformPoint((RoomMin + RoomMax) / 2f), park);
            EditorUtility.SetDirty(virtualRoom);
            return virtualRoom;
        }

        // The Echo Battle's card (easter egg): left of the leaf, clear of the sight lines to the table, waiting in the cloud
        // until the secret (Flutter, Bop, Boom, twice) brings it out. A deep plum card on a cream rim, white words.
        // Up and a little left, above the leaf and Flutter's bubble, in view (owner, emulator: 45 degrees to the left it
        // opened out of sight); above the sight lines to the table and its tiles.
        static readonly Vector3 BattleCardAt = new Vector3(-0.62f, 1.3f, 1.3f); // its wall clear of the table's drum tiles too
        static readonly Vector3 BattleCardSize = new Vector3(0.5f, 0.36f, 0.03f);

        static void CreateEchoBattle(GameObject root, EchoConductor conductor)
        {
            // Street style (owner 2026-10-31: the battle should look like teens and adults play it, "street / graffiti";
            // the characters stay as approved): a poster taped to a little brick wall, the title as a chunky graffiti tag
            // with a white sticker outline and spray drips, bold colour blocks, sticker buttons. Shapes only (textures did
            // not draw on the emulator).
            var park = GameObject.Find("RoomOrchestra")?.transform.Find("BallDispenser/ParkPoint");
            var card = Holder("EchoBattleCard", root.transform, Vector3.zero);
            card.position = BattleCardAt;
            card.rotation = FacePlayer(BattleCardAt);
            Material Paint(string name, Color c) => EnsureMaterial(name, "Ball", c);
            var brickA = Paint("BattleBrickA", new Color(0.27f, 0.25f, 0.28f));
            var brickB = Paint("BattleBrickB", new Color(0.22f, 0.21f, 0.24f));
            var asphalt = Paint("BattleAsphalt", new Color(0.08f, 0.08f, 0.1f));
            var pink = Glow(Paint("BattlePink", new Color(1f, 0.18f, 0.55f)), new Color(1f, 0.18f, 0.55f) * 0.35f);
            var blue = Glow(Paint("BattleBlue", new Color(0.15f, 0.45f, 1f)), new Color(0.15f, 0.45f, 1f) * 0.35f);
            var orange = Paint("BattleOrange", new Color(1f, 0.45f, 0.08f));
            var yellow = Glow(Paint("BattleYellow", new Color(1f, 0.84f, 0.12f)), new Color(1f, 0.84f, 0.12f) * 0.3f);
            var white = Paint("BattleWhite", new Color(0.97f, 0.97f, 0.95f));
            var tape = Paint("BattleTape", new Color(0.93f, 0.9f, 0.78f));

            // The wall: rows of offset bricks behind the poster.
            var wall = Holder("BattleWall", card, new Vector3(0f, 0f, 0.035f));
            const int rows = 6, perRow = 5;
            float wallW = BattleCardSize.x + 0.04f, wallH = BattleCardSize.y + 0.06f; // just peeking out round the poster
            float bw = wallW / perRow, bh = wallH / rows;
            for (int r = 0; r < rows; r++)
                for (int c = 0; c <= perRow; c++)
                {
                    float x = -(wallW / 2f) + bw * (c + (r % 2 == 0 ? 0f : 0.5f)) - bw * 0.25f;
                    if (Mathf.Abs(x) > wallW / 2f - bw * 0.2f) continue;
                    float y = -(wallH / 2f) + bh * (r + 0.5f);
                    Box($"Brick{r}_{c}", wall, new Vector3(x, y, 0f), new Vector3(bw - 0.008f, bh - 0.008f, 0.03f), (r + c) % 3 == 0 ? brickB : brickA, keepCollider: false);
                }
            // The poster: dark asphalt with a white sticker edge, taped at two corners.
            Box("BattleRim", card, new Vector3(0f, 0f, 0.006f), BattleCardSize + new Vector3(0.016f, 0.016f, 0f), white, keepCollider: false);
            Box("BattlePanel", card, Vector3.zero, BattleCardSize, asphalt, keepCollider: false);
            foreach (var sx in new[] { -1f, 1f })
                Box($"BattleTape{(sx < 0 ? "L" : "R")}", card, new Vector3(sx * (BattleCardSize.x / 2f - 0.02f), BattleCardSize.y / 2f - 0.01f, -0.008f),
                    new Vector3(0.09f, 0.03f, 0.022f), tape, keepCollider: false).localRotation = Quaternion.Euler(0f, 0f, sx * -35f);
            float front = -BattleCardSize.z / 2f - 0.006f;
            // Decorations are 2 cm deep, set mostly into the poster (their faces sit 2-4 mm proud of it): shrunk into the
            // cloud with the hidden card they must stay above the 1 mm limit.
            const float decorDepth = 0.02f;
            float decorZ = -BattleCardSize.z / 2f - 0.002f + decorDepth / 2f;
            // Colour blocks behind the title and the score, a little askew.
            Box("BattleTitleBlock", card, new Vector3(0f, 0.125f, decorZ), new Vector3(BattleCardSize.x - 0.02f, 0.085f, decorDepth), pink, keepCollider: false).localRotation = Quaternion.Euler(0f, 0f, -2.5f);
            Box("BattleScoreBlock", card, new Vector3(0f, -0.06f, decorZ), new Vector3(BattleCardSize.x * 0.7f, 0.05f, decorDepth), blue, keepCollider: false).localRotation = Quaternion.Euler(0f, 0f, 1.5f);

            // The title tag (English and Chinese words; the one not in use waits behind the wall).
            var titleData = JsonUtility.FromJson<CountPieces>(System.IO.File.ReadAllText(BattleTitleMeshData));
            var titles = new Transform[titleData.pieces.Length];
            for (int i = 0; i < titles.Length; i++)
            {
                var piece = titleData.pieces[i];
                var word = Holder(piece.name, card, new Vector3(0f, 0.128f, front - 0.012f));
                var solid = MeshPart("Solid", word, SaveSolidMesh(piece.name, piece.faces, piece.sides), Vector3.zero, yellow);
                solid.GetComponent<MeshRenderer>().sharedMaterials = new[] { yellow, pink };
                MeshPart("Rim", word, SaveSolidMesh(piece.name + "Rim", piece.outline, null), Vector3.zero, white);
                if (piece.name.EndsWith("Zh")) word.localScale = Vector3.one * 0.62f; // the same height as the tag
                titles[i] = word;
            }
            // Spray drips under the tag.
            foreach (var (x, len) in new[] { (-0.15f, 0.014f), (0.04f, 0.018f), (0.17f, 0.011f) }) // short: clear of the status line
            {
                Box($"Drip{x:0.00}", card, new Vector3(x, 0.083f - len / 2f, decorZ - 0.002f), new Vector3(0.01f, len, decorDepth), yellow, keepCollider: false);
                var blob = Primitive(PrimitiveType.Sphere, $"DripEnd{x:0.00}", card, yellow, keepCollider: false);
                blob.localPosition = new Vector3(x, 0.083f - len, decorZ - 0.002f);
                blob.localScale = new Vector3(0.013f, 0.015f, decorDepth);
            }

            TMP_Text Words(string name, Vector3 at, float height, float width, float lines)
            {
                var holder = Label(name, "Ag", card, at, height);
                holder.localRotation = Quaternion.identity;
                var t = holder.GetComponentInChildren<TMP_Text>();
                t.textWrappingMode = TextWrappingModes.Normal;
                t.rectTransform.sizeDelta = new Vector2(width, height * 1.4f * lines);
                t.enableAutoSizing = true;
                t.fontSizeMax = t.fontSize;
                t.fontSizeMin = t.fontSize * 0.75f;
                t.text = "";
                return t;
            }
            var status = Words("BattleStatus", new Vector3(0f, 0.025f, front - 0.004f), 0.026f, BattleCardSize.x - 0.05f, 3f);
            status.text = UiText.Get(UiTerm.BattleFound, UiLanguage.English);
            var score = Words("BattleScore", new Vector3(0f, -0.06f, front - 0.01f), 0.026f, BattleCardSize.x * 0.68f, 1.5f);
            score.text = string.Format(UiText.Get(UiTerm.BattleScore, UiLanguage.English), 0, 0);

            // Sticker buttons: bold colour, white edge, each a little askew.
            var kinds = new[] { (EchoBattleButton.Kind.Host, UiTerm.BattleHost, pink, -3f), (EchoBattleButton.Kind.Join, UiTerm.BattleJoin, blue, 2f), (EchoBattleButton.Kind.Leave, UiTerm.BattleLeave, orange, -1.5f) };
            for (int i = 0; i < kinds.Length; i++)
            {
                var (kind, term, colour, tilt) = kinds[i];
                var button = Holder($"Battle{kind}", card, new Vector3(-0.155f + 0.155f * i, -0.13f, front - 0.004f));
                button.localRotation = Quaternion.Euler(0f, 0f, tilt);
                Box($"Battle{kind}Edge", button, new Vector3(0f, 0f, 0.004f), new Vector3(0.142f, 0.072f, 0.03f), white, keepCollider: false);
                var slab = Box($"Battle{kind}Slab", button, Vector3.zero, new Vector3(0.13f, 0.06f, 0.03f), colour, keepCollider: false);
                slab.gameObject.AddComponent<BoxCollider>();
                Hoverable(slab.gameObject);
                var label = Label($"Battle{kind}Label", UiText.Get(term, UiLanguage.English), button, new Vector3(0f, 0f, -0.02f), 0.026f);
                label.localRotation = Quaternion.identity;
                Localize(label, term);
                button.gameObject.AddComponent<EchoBattleButton>().Configure(kind);
            }
            RoundedShapes.RoundBlocks(card, 0.012f);
            var director = root.AddComponent<EchoBattleDirector>();
            director.Configure(card, park, status, score, conductor);
            director.ConfigureTitles(titles);
            // Saved hidden in the cloud, buttons off, until the secret.
            if (park != null)
            {
                card.position = park.position;
                card.localScale = Vector3.one * EchoBattleDirector.ParkedScale;
            }
            foreach (var c in card.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            EditorUtility.SetDirty(director);
        }

        const string BattleTitleMeshData = "Assets/RoomOrchestra/Art/Battle/BattleTitleMeshes.json";

        // A bright arrow that bobs above the ball on the cloud whenever the lesson wants a throw (owner: it wasn't clear
        // the player should throw the ball). Built from rounded bars; waits in the cloud when unused.
        static Transform CreateBallArrow(Transform root, Transform park)
        {
            var arrow = Holder("TutorialArrow", root, Vector3.zero);
            var yellow = Glow(EnsureMaterial("TutorialArrow", "Ball", new Color(1f, 0.85f, 0.2f)), new Color(1f, 0.85f, 0.2f) * 0.6f);
            Box("ArrowShaft", arrow, new Vector3(0f, 0.06f, 0f), new Vector3(0.025f, 0.09f, 0.025f), yellow, keepCollider: false);
            Box("ArrowHeadL", arrow, new Vector3(-0.018f, 0.012f, 0f), new Vector3(0.022f, 0.06f, 0.025f), yellow, keepCollider: false).localRotation = Quaternion.Euler(0f, 0f, 45f);
            Box("ArrowHeadR", arrow, new Vector3(0.018f, 0.012f, 0f), new Vector3(0.022f, 0.06f, 0.025f), yellow, keepCollider: false).localRotation = Quaternion.Euler(0f, 0f, -45f);
            RoundedShapes.RoundBlocks(arrow, 0.008f);
            if (park != null)
            {
                arrow.position = park.position;
                arrow.localScale = Vector3.one * TutorialDirector.ParkedScale;
            }
            return arrow;
        }

        static readonly Vector3 CountdownAt = new Vector3(0.45f, 1.72f, 2.45f); // between the chair and the lamp, above both
        const string CountdownMeshData = "Assets/RoomOrchestra/Art/Countdown/CountdownMeshes.json";

        [System.Serializable] class SolidPart { public float[] vertices; public int[] triangles; }
        [System.Serializable] class CountPiece { public string name, text; public float width, height; public SolidPart faces, sides, outline; }
        [System.Serializable] class CountPieces { public CountPiece[] pieces; }

        // Solid words traced and extruded by ArtSource/Countdown/countdown_mesh.py: a coloured solid with darker
        // sides (two sub-meshes) on a dark plum rim, like the title lettering.
        static (Transform holder, Transform[] pieces) CreateCountdown(Transform root, Transform park)
        {
            var holder = Holder("TutorialCountdown", root, Vector3.zero);
            holder.position = CountdownAt;
            holder.rotation = FacePlayer(CountdownAt);
            var data = JsonUtility.FromJson<CountPieces>(System.IO.File.ReadAllText(CountdownMeshData));
            var rim = EnsureMaterial("CountRim", "Ball", new Color(0.23f, 0.14f, 0.31f));
            var pieces = new Transform[data.pieces.Length];
            for (int i = 0; i < pieces.Length; i++)
            {
                var piece = data.pieces[i];
                var colour = TutorialDirector.CountColours[Mathf.Min(i, TutorialDirector.CountColours.Length - 1)];
                var face = Glow(EnsureMaterial($"Count{Mathf.Min(i, 3)}Face", "Ball", colour), colour * 0.5f);
                var side = EnsureMaterial($"Count{Mathf.Min(i, 3)}Side", "Ball", colour * 0.6f + new Color(0f, 0f, 0f, 0.4f));
                var word = Holder(piece.name, holder, Vector3.zero);
                var solid = MeshPart("Solid", word, SaveSolidMesh(piece.name, piece.faces, piece.sides), Vector3.zero, face);
                solid.GetComponent<MeshRenderer>().sharedMaterials = new[] { face, side };
                MeshPart("Rim", word, SaveSolidMesh(piece.name + "Rim", piece.outline, null), Vector3.zero, rim);
                if (park != null)
                {
                    word.position = park.position; // waits in the cloud
                    word.localScale = Vector3.one * TutorialDirector.ParkedScale;
                }
                pieces[i] = word;
            }
            // THROW! under GO (owner: it wasn't clear the count means "throw now"): a child of the GO word, so it shows
            // and hides with it.
            void Under(string go, string throwWord)
            {
                var goPiece = pieces.FirstOrDefault(p => p.name == go);
                var throwPiece = pieces.FirstOrDefault(p => p.name == throwWord);
                if (goPiece == null || throwPiece == null) return;
                throwPiece.SetParent(goPiece, false);
                throwPiece.localPosition = new Vector3(0f, -0.3f, 0f);
                throwPiece.localScale = Vector3.one * 0.45f;
            }
            Under("CountGo", "CountThrow");
            Under("CountGoZh", "CountThrowZh");
            return (holder, pieces.Where(p => !p.name.StartsWith("CountThrow")).ToArray());
        }

        // A solid from a traced shape: already wound outward (Unity's normals), one sub-mesh per part given.
        static Mesh SaveSolidMesh(string name, SolidPart main, SolidPart second)
        {
            string path = $"Assets/RoomOrchestra/Meshes/{name}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/RoomOrchestra/Meshes")) AssetDatabase.CreateFolder("Assets/RoomOrchestra", "Meshes");
                mesh = new Mesh { name = name };
                AssetDatabase.CreateAsset(mesh, path);
            }
            var parts = second != null ? new[] { main, second } : new[] { main };
            var vertices = new List<Vector3>();
            var offsets = new int[parts.Length];
            for (int p = 0; p < parts.Length; p++)
            {
                offsets[p] = vertices.Count;
                for (int i = 0; i < parts[p].vertices.Length; i += 3)
                    vertices.Add(new Vector3(parts[p].vertices[i], parts[p].vertices[i + 1], parts[p].vertices[i + 2]));
            }
            mesh.Clear();
            mesh.indexFormat = vertices.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.subMeshCount = parts.Length;
            for (int p = 0; p < parts.Length; p++)
                mesh.SetTriangles(parts[p].triangles.Select(t => t + offsets[p]).ToArray(), p);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        static readonly Vector3 BubbleSize = new Vector3(0.64f, 0.21f, 0.03f);  // 3 cm deep: parked, still above the near-zero limit

        static void CreateTutorial(GameObject root, Transform pad, Transform controls, Transform flutter, InstrumentTarget[] targets,
            PitchColumn[] columns, OrbitView[] orbits, EchoConductor conductor, IntroSequence intro)
        {
            var park = GameObject.Find("RoomOrchestra")?.transform.Find("BallDispenser/ParkPoint");
            var rainforest = root.transform.Find("Rainforest");
            var stationsHolder = Holder("Stations", root.transform, Vector3.zero);
            var stations = new List<StationReveal>();
            foreach (var type in Introduced)
            {
                int i = System.Array.FindIndex(targets, t => t.Instrument == type);
                if (i < 0) continue;
                var parts = new List<Transform> { targets[i].transform, columns[i].transform, orbits[i].transform };
                if (rainforest != null)
                {
                    var names = type switch
                    {
                        InstrumentType.Erhu => new[] { "Vine_Lamp", "Sparkles_Erhu", "Flock" },
                        InstrumentType.Udu => new[] { "Vine_TableL", "Vine_TableR", $"Sparkles_{type}" },
                        _ => new[] { $"Sparkles_{type}" },
                    };
                    foreach (var n in names)
                    {
                        var part = rainforest.Find(n);
                        if (part != null) parts.Add(part);
                    }
                }
                var holder = Holder($"Station_{type}", stationsHolder, Vector3.zero);
                var station = holder.gameObject.AddComponent<StationReveal>();
                station.Configure(type, parts.ToArray(), park);
                station.HideNow();
                EditorUtility.SetDirty(station);
                stations.Add(station);
            }

            // Flutter's speech bubble: a deep plum card on a cream border, a little tail toward Flutter, white words that wrap.
            // Owner (emulator): the words were white on a faint card, hard to read; PICO Spatial appears to draw label text
            // white whatever its colour, so the card is dark and the words white, which reads in any renderer and any room.
            var bubble = Holder("TutorialBubble", pad, Vector3.zero);
            bubble.position = BubbleAt;
            bubble.rotation = FacePlayer(BubbleAt);
            var cream = EnsureMaterial("SpeechBubbleRim", "Ball", new Color(1f, 0.96f, 0.88f));
            var plum = EnsureMaterial("SpeechBubble", "Ball", new Color(0.2f, 0.1f, 0.28f));
            Box("BubbleRim", bubble, new Vector3(0f, 0f, 0.006f), BubbleSize + new Vector3(0.024f, 0.024f, 0f), cream, keepCollider: false);
            Box("BubblePanel", bubble, Vector3.zero, BubbleSize, plum, keepCollider: false);
            var tailRim = Box("BubbleTailRim", bubble, new Vector3(0.06f, -BubbleSize.y / 2f - 0.004f, 0.006f), new Vector3(0.066f, 0.066f, BubbleSize.z), cream, keepCollider: false);
            tailRim.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var tail = Box("BubbleTail", bubble, new Vector3(0.06f, -BubbleSize.y / 2f, 0f), new Vector3(0.05f, 0.05f, BubbleSize.z), plum, keepCollider: false);
            tail.localRotation = Quaternion.Euler(0f, 0f, 45f);
            // Big and white on deep plum: about 3.4 cm letters, never under 2.9 cm.
            var words = Label("BubbleLine", "Ag", bubble, new Vector3(0.045f, 0f, -BubbleSize.z / 2f - 0.01f), 0.034f);
            words.localRotation = Quaternion.identity; // flat on the card: turned to face the eye, its left end went behind it
            var tmp = words.GetComponentInChildren<TMP_Text>();
            tmp.text = UiText.Get(UiTerm.TutorialHello, UiLanguage.English);
            tmp.color = Color.white;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.rectTransform.sizeDelta = new Vector2(BubbleSize.x - 0.15f, BubbleSize.y - 0.02f);
            tmp.enableAutoSizing = true; // the longest lines shrink a little to fit
            tmp.fontSizeMax = tmp.fontSize;
            tmp.fontSizeMin = tmp.fontSize * 0.85f;
            tmp.ForceMeshUpdate(true, true);
            RoundedShapes.RoundBlocks(bubble, 0.02f); // a soft card, before it is tucked away

            // The Skip leaf on the bubble's left end (left of every sight line to the tiles).
            var skip = Holder("SkipButton", bubble, new Vector3(-BubbleSize.x / 2f + 0.06f, 0f, -BubbleSize.z / 2f - 0.012f));
            var leaf = Primitive(PrimitiveType.Sphere, "SkipLeaf", skip, EnsureMaterial("CallLeaf", "Ball", new Color(0.36f, 0.72f, 0.38f)), keepCollider: false);
            leaf.localScale = new Vector3(0.1f, 0.07f, 0.03f);
            leaf.gameObject.AddComponent<BoxCollider>();
            Hoverable(leaf.gameObject);
            var skipLabel = Label("SkipLabel", UiText.Get(UiTerm.Skip, UiLanguage.English), skip, new Vector3(0f, 0f, -0.02f), 0.024f);
            skipLabel.localRotation = Quaternion.identity;
            Localize(skipLabel, UiTerm.Skip);
            skip.gameObject.AddComponent<TutorialSkipButton>();

            // The glow behind the wanted tile (the director sizes and places it).
            var glow = Box("TutorialGlow", controls, Vector3.zero, new Vector3(0.18f, 0.15f, 0.006f),
                Glow(EnsureMaterial("TutorialGlow", "AimRing", new Color(1f, 0.85f, 0.35f)), new Color(1f, 0.85f, 0.35f)), keepCollider: false);

            // The 3-2-1-Go count for the timed last note (owner: large, coloured 3D words): above and left of the
            // lamp, clear of its tiles. Each word waits in the cloud until its beat.
            var count = CreateCountdown(root.transform, park);

            var director = root.AddComponent<TutorialDirector>();
            director.Configure(conductor, stations.ToArray(), columns, bubble, tmp, flutter, glow, park);
            director.ConfigureCountdown(count.holder, count.pieces);
            director.ConfigureRoom(CreateVirtualRoom(root.transform, park));
            director.ConfigureArrow(CreateBallArrow(root.transform, park), Object.FindFirstObjectByType<BallLauncher>());
            director.ParkForSave();
            intro.ConfigureTutorial(director);
            EditorUtility.SetDirty(director);
            EditorUtility.SetDirty(intro);
        }

        // The opening (owner, 2026-10-29): the scene is saved in its first frame, the leaf out of view and the
        // characters sitting on the floor in front of the leaf's home, ready to bounce up onto it.
        const float IntroFloorZ = 1.0f;

        static IntroSequence CreateIntro(GameObject root, Transform swoop, OrbToken[] tokens, LeafStalk stalk)
        {
            var cast = tokens.Select(t => t.transform).ToArray();
            var floor = cast.Select(t =>
            {
                var body = t.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
                float aboveFeet = t.position.y - body.min.y;
                return new Vector3(t.position.x, aboveFeet + 0.003f, IntroFloorZ);
            }).ToArray();
            var intro = root.AddComponent<IntroSequence>();
            var title = CreateTitle(root, intro);
            intro.Configure(swoop, cast, floor, stalk, title);
            EditorUtility.SetDirty(intro);
            EditorUtility.SetDirty(title);
            return intro;
        }

        // The title card (Version 1 feedback: "boot into a proper title screen"): in front of the player, the
        // name in the key-art lettering, a line about what it is, and a leaf-shaped Start button.
        static readonly Vector3 TitleCardAt = new Vector3(0f, 1.4f, 1.8f); // just above eye height, behind the ball stand at launch
        // From the stand's home (right of the leaf) to directly in front of the player, under the title card.
        static readonly Vector3 StandFrontOffset = new Vector3(-0.62f, 0f, -0.1f);

        static TitleScreen CreateTitle(GameObject root, IntroSequence intro)
        {
            var card = Holder("TitleCard", root.transform, Vector3.zero);
            card.position = TitleCardAt;
            card.rotation = FacePlayer(TitleCardAt);
            var cream = new Color(1f, 0.96f, 0.92f);
            // 3 cm deep, so even parked at TitleScreen.ParkedScale nothing is under the near-zero scale limit.
            Box("TitleRim", card, new Vector3(0f, 0f, 0.02f), new Vector3(0.95f, 0.59f, 0.03f), EnsureMaterial("TitleRim", "Ball", cream), keepCollider: false);
            Box("TitlePanel", card, new Vector3(0f, 0f, 0.012f), new Vector3(0.92f, 0.56f, 0.03f), EnsureMaterial("TitlePanel", "Ball", new Color(1f, 0.56f, 0.48f)), keepCollider: false);

            // The key-art lettering as two flat meshes (cream letters on the icon's plum outline): PICO Spatial did
            // not draw it as a textured quad on the emulator (ArtSource/Icon/lettering_mesh.py explains).
            var (fill, outline) = TitleLetteringMeshes();
            var letters = Holder("TitleLettering", card, new Vector3(0f, 0.1f, 0f));
            MeshPart("LetteringOutline", letters, outline, new Vector3(0f, 0f, -0.005f), EnsureMaterial("TitleLetterOutline", "Ball", new Color(0.23f, 0.14f, 0.31f)));
            MeshPart("LetteringFill", letters, fill, new Vector3(0f, 0f, -0.007f), EnsureMaterial("TitleLetterFill", "Ball", new Color(1f, 0.965f, 0.92f)));

            Localize(Label("TitleSubtitle", UiText.Get(UiTerm.TitleSubtitle, UiLanguage.English), card, new Vector3(0f, -0.055f, -0.004f), 0.045f), UiTerm.TitleSubtitle);

            // Bottom right, off the centre line: PICO draws its own window handle (the round grabber that opens
            // Back / Close / Minimize) on the app's centre line, and centred it covered Start (owner, emulator).
            var button = Holder("StartButton", card, new Vector3(0.27f, -0.18f, -0.02f));
            var leaf = Primitive(PrimitiveType.Sphere, "StartLeaf", button, EnsureMaterial("CallLeaf", "Ball", new Color(0.36f, 0.72f, 0.38f)), keepCollider: false);
            leaf.localScale = new Vector3(0.26f, 0.1f, 0.035f); // its depth parks above the near-zero limit too
            leaf.gameObject.AddComponent<BoxCollider>(); // the unit box inside the scaled leaf: its tap area
            Hoverable(leaf.gameObject);
            // Well clear of the leaf's curved face: off-centre, the label turns to face the player and its near
            // edge dipped behind the leaf ("tart" on the emulator).
            Localize(Label("StartLabel", UiText.Get(UiTerm.Start, UiLanguage.English), button, new Vector3(0f, 0f, -0.045f), 0.04f), UiTerm.Start);
            button.gameObject.AddComponent<TitleStartButton>();

            // Quit, bottom left (George, 2026-11-03: "a main menu with the app's name and a Quit button"): smaller and
            // quieter than Start, mirrored across PICO's window handle. It asks "Sure?" before it closes the app.
            var quit = Holder("TitleQuitButton", card, new Vector3(-0.3f, -0.19f, -0.02f));
            var quitLeaf = Primitive(PrimitiveType.Sphere, "TitleQuitLeaf", quit, EnsureMaterial("QuitLeaf", "Ball", new Color(0.42f, 0.33f, 0.5f)), keepCollider: false);
            quitLeaf.localScale = new Vector3(0.19f, 0.08f, 0.035f);
            quitLeaf.gameObject.AddComponent<BoxCollider>();
            Hoverable(quitLeaf.gameObject);
            Localize(Label("TitleQuitLabel", UiText.Get(UiTerm.Quit, UiLanguage.English), quit, new Vector3(0f, 0f, -0.045f), 0.034f), UiTerm.Quit);
            quit.gameObject.AddComponent<QuitButton>();

            var title = root.AddComponent<TitleScreen>();
            title.Configure(card, GameObject.Find("RoomOrchestra")?.transform.Find("BallDispenser/ParkPoint"), intro);
            return title;
        }

        const string TitleLetteringMeshData = "Assets/RoomOrchestra/Art/Title/TitleLetteringMesh.json";

        [System.Serializable] class FlatShape { public float[] vertices; public int[] triangles; }
        [System.Serializable] class LetteringShapes { public float width, height; public FlatShape fill, outline; }

        // Mesh assets from the traced lettering, facing the player (-Z), saved beside the other generated meshes.
        static (Mesh fill, Mesh outline) TitleLetteringMeshes()
        {
            var data = JsonUtility.FromJson<LetteringShapes>(System.IO.File.ReadAllText(TitleLetteringMeshData));
            return (SaveFlatMesh("TitleLetteringFill", data.fill), SaveFlatMesh("TitleLetteringOutline", data.outline));
        }

        static Mesh SaveFlatMesh(string name, FlatShape shape)
        {
            string path = $"Assets/RoomOrchestra/Meshes/{name}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/RoomOrchestra/Meshes")) AssetDatabase.CreateFolder("Assets/RoomOrchestra", "Meshes");
                mesh = new Mesh { name = name };
                AssetDatabase.CreateAsset(mesh, path);
            }
            var vertices = new Vector3[shape.vertices.Length / 2];
            for (int i = 0; i < vertices.Length; i++) vertices[i] = new Vector3(shape.vertices[2 * i], shape.vertices[2 * i + 1], 0f);
            var triangles = (int[])shape.triangles.Clone();
            mesh.Clear();
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            if (mesh.normals.Length > 0 && mesh.normals[0].z > 0f) // wind every triangle to face the player
            {
                for (int i = 0; i < triangles.Length; i += 3) (triangles[i + 1], triangles[i + 2]) = (triangles[i + 2], triangles[i + 1]);
                mesh.triangles = triangles;
                mesh.RecalculateNormals();
            }
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        static Transform MeshPart(string name, Transform parent, Mesh mesh, Vector3 localPosition, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go.transform;
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
        /// Version 1 feedback: seated at a desk, the furniture felt uncomfortably large. It is real-world scale
        /// (the spatial camera is Unconstrained: one Unity metre is one metre), so it is set to standard sizes
        /// (a 0.75 m table, a chair back topping out at 0.90 m; the 1.54 m floor lamp was already standard)
        /// and set 0.2 m further out, around a seated player rather than over them (owner, 2026-10-29).
        /// Absolute values, so running the setup again changes nothing.
        /// </summary>
        public const float TableTop = 0.75f, ChairBackTop = 0.90f;
        public static readonly Vector3 TableAt = new Vector3(-1.1f, 0f, 2.5f), ChairAt = new Vector3(0f, 0f, 2.2f), LampAt = new Vector3(1.1f, 0f, 2.5f);

        // The furniture roots' authored scales (the table in sixths of a metre). Set explicitly so a station the
        // guided song parked in the cloud (StationReveal) always comes back at full size, even if the scene was
        // saved with its stations' script unresolved.
        const float TableScale = 6f, ChairScale = 5.5f, LampScale = 5.5f;

        static void FitFurnitureToRealSizes()
        {
            var furniture = GameObject.Find("RoomOrchestra")?.transform.Find("Furniture");
            if (furniture == null) return;
            // Every furniture collider is a tap and hit area (none is off by design); a hidden station turns them off.
            foreach (var c in furniture.GetComponentsInChildren<Collider>(true)) c.enabled = true;
            var table = furniture.Find("Table");
            if (table != null)
            {
                table.localPosition = TableAt;
                table.localScale = Vector3.one * TableScale;
                float s = table.localScale.y;                 // 6: the table is authored in sixths of a metre
                const float topThickness = 0.09f;
                float legTop = TableTop - topThickness;
                foreach (Transform part in table)
                {
                    if (part.name == "Top") part.localPosition = new Vector3(part.localPosition.x, (TableTop - topThickness / 2f) / s, part.localPosition.z);
                    if (part.name == "Leg")
                    {
                        part.localScale = new Vector3(part.localScale.x, legTop / s, part.localScale.z);
                        part.localPosition = new Vector3(part.localPosition.x, legTop / 2f / s, part.localPosition.z);
                    }
                    if (part.name == "AimPoint") part.localPosition = new Vector3(part.localPosition.x, (TableTop + 0.06f) / s, part.localPosition.z);
                }
            }
            var chair = furniture.Find("Chair");
            if (chair != null)
            {
                chair.localPosition = ChairAt;
                chair.localScale = Vector3.one * ChairScale;
                var back = chair.Find("Back");
                var seat = chair.Find("Seat");
                if (back != null && seat != null)
                {
                    float s = chair.localScale.y;
                    float seatTop = seat.GetComponent<Renderer>().bounds.max.y;
                    back.localScale = new Vector3(back.localScale.x, (ChairBackTop - seatTop) / s, back.localScale.z);
                    back.localPosition = new Vector3(back.localPosition.x, (ChairBackTop + seatTop) / 2f / s, back.localPosition.z);
                }
            }
            var lamp = furniture.Find("Lamp");
            var shade = lamp != null ? lamp.Find("Shade") : null;
            if (shade != null) shade.GetComponent<MeshFilter>().sharedMesh = RoundedShapes.LampShade(); // a shade, not a ball
            FurnitureModels.Apply(furniture); // the Blender models (ArtSource/Furniture/build_furniture.py)
            RoundedShapes.RoundBlocks(furniture, 0.02f); // anything still a plain block gets softened edges
            if (lamp != null)
            {
                lamp.localPosition = LampAt;
                lamp.localScale = Vector3.one * LampScale;
            }
            Physics.SyncTransforms();
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
            if (CharacterArt.Has(profile.Type))
                return CreateCharacterToken(profile, tray, localPosition, body, selected);

            var baseScale = new Vector3(profile.ScaleX, profile.ScaleY, profile.ScaleZ) * TokenDiameter;
            var t = Primitive(PrimitiveType.Sphere, $"Token_{profile.Type}", tray, body, keepCollider: true);
            Hoverable(t.gameObject);
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

        // The character itself (Blender mesh: body, rim, face, shine, motif), turned to face the player.
        // Its mesh carries the proportions, so it scales evenly.
        static OrbToken CreateCharacterToken(OrbProfile profile, Transform tray, Vector3 localPosition, Material body, bool selected)
        {
            var go = new GameObject($"Token_{profile.Type}");
            go.transform.SetParent(tray, false);
            go.transform.localPosition = localPosition;
            go.transform.rotation = FacePlayer(go.transform.position);
            var baseScale = Vector3.one * TokenDiameter;
            go.transform.localScale = selected ? baseScale * 1.3f : baseScale;
            CharacterArt.Dress(go, profile.Type, body);
            TapSphere(go.transform.Find("Visual"), 0.5f); // on the body, so the system's hover highlight shows

            var token = go.AddComponent<OrbToken>();
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
            go.transform.position = TrayCenter + new Vector3(0.4f, 0f, 0f); // right of Boom, clear of the chair sight line
            var tierSlab = Box("TierSlab", go.transform, Vector3.zero, new Vector3(0.11f, 0.03f, 0.08f), slab, keepCollider: false);
            TapBox(tierSlab, new Vector3(0f, 0.04f, 0f), new Vector3(0.11f, 0.12f, 0.09f));

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

            Localize(Label("TierLabel", "Loudness", go.transform, new Vector3(0f, 0.004f, -0.075f), SmallLabelHeight), UiTerm.Loudness);
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
        const float MiniDiameter = 0.06f; // character minis: faces readable on the ring

        // A clock face facing the player: lamp ring around the shade; chair/table rings float above.
        static OrbitView CreateOrbit(InstrumentTarget target, Transform root, Material[] orbMaterials,
            Material tick, Material marker, Material hub)
        {
            float lift = target.Instrument == InstrumentType.Erhu ? 0f : 0.4f;
            var go = new GameObject($"Orbit_{target.Instrument}");
            go.transform.SetParent(root, false);
            go.transform.position = target.AimPosition + Vector3.up * lift;
            go.transform.rotation = OrbitView.Facing(go.transform.position, PlayerEye, OrbitView.HaloTilt); // a halo tipped toward the seat

            for (int i = 0; i < MusicClock.StepsPerBar; i++)
            {
                float a = i / (float)MusicClock.StepsPerBar * Mathf.PI * 2f;
                var t = Primitive(PrimitiveType.Sphere, $"Tick{i:00}", go.transform, tick, keepCollider: false);
                t.localPosition = new Vector3(Mathf.Sin(a), Mathf.Cos(a), 0f) * OrbitRadius;
                t.localScale = Vector3.one * (i % MusicClock.StepsPerBeat == 0 ? 0.03f : 0.014f); // beats stand out
            }

            var mark = Primitive(PrimitiveType.Cube, "PlayMarker", go.transform, marker, keepCollider: false);
            mark.localPosition = new Vector3(0f, OrbitRadius + 0.055f, 0f);
            mark.localRotation = OrbitView.Upright(OrbitView.HaloTilt) * Quaternion.Euler(0f, 0f, 45f); // an upright diamond
            mark.localScale = new Vector3(0.035f, 0.035f, 0.012f);

            Transform park;
            if (lift > 0f)
            {
                park = Primitive(PrimitiveType.Sphere, "Hub", go.transform, hub, keepCollider: false);
                park.localScale = Vector3.one * 0.1f; // holds the widest unused mini (Boom) out of sight
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
                    Transform mini;
                    if (CharacterArt.Has(profile.Type))
                    {
                        // A tiny version of the character, facing the player like its ring.
                        mini = new GameObject($"Echo_{profile.Type}_{m}").transform;
                        mini.SetParent(go.transform, false);
                        CharacterArt.DressMini(mini.gameObject, profile.Type, orbMaterials[o]);
                        TapSphere(mini.Find("Visual"), 0.5f);
                        mini.localScale = Vector3.one * MiniDiameter;
                    }
                    else
                    {
                        mini = Primitive(PrimitiveType.Sphere, $"Echo_{profile.Type}_{m}", go.transform, orbMaterials[o], keepCollider: true);
                        Hoverable(mini.gameObject);
                        mini.localScale = new Vector3(profile.ScaleX, profile.ScaleY, profile.ScaleZ) * MiniDiameter;
                    }
                    mini.position = park.position;
                    mini.localRotation = OrbitView.Upright(OrbitView.HaloTilt); // characters stand up and face the player
                    mini.gameObject.AddComponent<EchoMini>().Configure(view);
                    EditorUtility.SetDirty(mini.GetComponent<EchoMini>());
                    pools[o][m] = mini;
                }
            }
            view.Configure(target, park, pools[0], pools[1], pools[2], OrbitRadius, mark);
            EditorUtility.SetDirty(view);
            return view;
        }

        // Undo, on the leaf's front row: a small slab with a hooked arrow.
        static void CreateUndoButton(Vector3 position, Transform root, Material slab, Material mark)
        {
            var go = SmallButton("UndoButton", position, root, slab, "UndoSlab");
            // Sized to sit within the small slab.
            Box("UndoArrowStem", go.transform, new Vector3(0.006f, 0.025f, 0f), new Vector3(0.046f, 0.01f, 0.01f), mark, keepCollider: false);
            Box("UndoArrowHook", go.transform, new Vector3(0.029f, 0.037f, 0f), new Vector3(0.01f, 0.024f, 0.01f), mark, keepCollider: false);
            var head = Box("UndoArrowHead", go.transform, new Vector3(-0.019f, 0.025f, 0f), new Vector3(0.018f, 0.018f, 0.01f), mark, keepCollider: false);
            head.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Localize(Label("UndoLabel", "Undo", go.transform, SmallLabelOffset, SmallLabelHeight), UiTerm.Undo);
            go.AddComponent<UndoButton>();
        }

        // Pause/Play and Clear in the front control row, right of Undo (clear of the chair sight line): a slab, an icon (pause bars / an X) and a label.
        static void CreateMusicButton(MusicControlButton.Kind kind, Vector3 position, Transform root, Material slab, Material mark)
        {
            var go = SmallButton($"{kind}Button", position, root, slab, $"{kind}Slab");
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
            var label = Label($"{kind}Label", kind == MusicControlButton.Kind.Pause ? "Pause" : "Clear", go.transform, SmallLabelOffset, SmallLabelHeight);
            var button = go.AddComponent<MusicControlButton>();
            button.Configure(kind, label.GetComponentInChildren<TMP_Text>(), pauseIcon, playIcon);
            if (kind == MusicControlButton.Kind.Clear) Localize(label, UiTerm.Clear); // Pause relabels itself (Pause / Play)
            EditorUtility.SetDirty(button);
        }

        // Recenter, at the end of the front row: a target mark (ring and dot) facing the player.
        static void CreateRecenterButton(Vector3 position, Transform root, Material slab, Material mark)
        {
            var go = SmallButton("RecenterButton", position, root, slab, "RecenterSlab");
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
            Localize(Label("RecenterLabel", "Recenter", go.transform, SmallLabelOffset, SmallLabelHeight), UiTerm.Recenter);
            go.AddComponent<RecenterButton>();
        }

        // Height (Seated / Standing), at the end of the front row: an up-down arrow, and a label with the setting.
        static void CreateHeightButton(Vector3 position, Transform root, Material slab, Material mark)
        {
            var go = SmallButton("HeightButton", position, root, slab, "HeightSlab");
            Box("HeightArrowStem", go.transform, new Vector3(0f, 0.035f, 0f), new Vector3(0.008f, 0.04f, 0.008f), mark, keepCollider: false);
            for (int i = 0; i < 2; i++)
            {
                float y = i == 0 ? 0.052f : 0.018f;
                var left = Box($"HeightArrowHead{i}L", go.transform, new Vector3(-0.006f, y, 0f), new Vector3(0.018f, 0.006f, 0.008f), mark, keepCollider: false);
                left.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? 40f : -40f);
                var right = Box($"HeightArrowHead{i}R", go.transform, new Vector3(0.006f, y, 0f), new Vector3(0.018f, 0.006f, 0.008f), mark, keepCollider: false);
                right.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? -40f : 40f);
            }
            var label = Label("HeightLabel", HeightButton.TextFor(PlayPosture.Seated, UiLanguage.English), go.transform, SmallLabelOffset, SmallLabelHeight * 2f);
            var button = go.AddComponent<HeightButton>();
            button.Configure(label.GetComponentInChildren<TMP_Text>());
            EditorUtility.SetDirty(button);
        }

        // English / 中文, at the end of the front row: the label names the language a tap switches to.
        // The back row, left of Flutter: View (A11Y-08), stepping Standard, Large text, High contrast, both.
        static void CreateViewButton(Transform root, Material slab, Material mark)
        {
            var go = new GameObject("ViewButton");
            go.transform.SetParent(root, false);
            go.transform.position = TrayCenter + new Vector3(-0.28f, 0f, 0f);
            go.transform.rotation = FacePlayer(go.transform.position);
            var slabPart = Box("ViewSlab", go.transform, Vector3.zero, new Vector3(0.1f, 0.03f, 0.08f), slab, keepCollider: false);
            TapBox(slabPart, new Vector3(0f, 0.04f, 0f), new Vector3(0.1f, 0.12f, 0.09f));
            // An eye: a flat lens with a dark pupil.
            var eyeWhite = Primitive(PrimitiveType.Sphere, "ViewEye", go.transform, EnsureMaterial("ViewEyeWhite", "Ball", new Color(0.95f, 0.95f, 0.92f)), keepCollider: false);
            eyeWhite.localPosition = new Vector3(0f, 0.032f, 0f);
            eyeWhite.localScale = new Vector3(0.05f, 0.026f, 0.012f);
            var pupil = Primitive(PrimitiveType.Sphere, "ViewPupil", go.transform, mark, keepCollider: false);
            pupil.localPosition = new Vector3(0f, 0.032f, -0.006f);
            pupil.localScale = new Vector3(0.018f, 0.018f, 0.006f);
            var label = Label("ViewLabel", ViewButton.TextFor(ViewMode.Standard, UiLanguage.English), go.transform, new Vector3(0f, 0.004f, -0.075f), 0.034f);
            go.AddComponent<ViewButton>().Configure(label.GetComponentInChildren<TMP_Text>());
        }

        // The back row, right of Loudness: Sounds, the next instrument pack (its label names the pack in use).
        static void CreateSoundsButton(Transform root, Material slab, Material mark)
        {
            var go = new GameObject("SoundsButton");
            go.transform.SetParent(root, false);
            go.transform.position = TrayCenter + new Vector3(0.515f, 0f, 0f);
            go.transform.rotation = FacePlayer(go.transform.position);
            var slabPart = Box("SoundsSlab", go.transform, Vector3.zero, new Vector3(0.1f, 0.03f, 0.08f), slab, keepCollider: false);
            TapBox(slabPart, new Vector3(0f, 0.04f, 0f), new Vector3(0.1f, 0.12f, 0.09f));
            // A little speaker with two sound waves (unlike Loudness's bars beside it).
            Box("SoundsSpeaker", go.transform, new Vector3(-0.026f, 0.03f, 0f), new Vector3(0.012f, 0.018f, 0.01f), mark, keepCollider: false);
            Box("SoundsHorn", go.transform, new Vector3(-0.014f, 0.03f, 0f), new Vector3(0.022f, 0.022f, 0.01f), mark, keepCollider: false).localRotation = Quaternion.Euler(0f, 0f, 45f);
            Box("SoundsWave0", go.transform, new Vector3(0.008f, 0.03f, 0f), new Vector3(0.005f, 0.018f, 0.008f), mark, keepCollider: false);
            Box("SoundsWave1", go.transform, new Vector3(0.022f, 0.03f, 0f), new Vector3(0.005f, 0.03f, 0.008f), mark, keepCollider: false);
            var label = Label("SoundsLabel", SoundPackButton.TextFor(UiTerm.PackClassic, UiLanguage.English), go.transform, new Vector3(0f, 0.004f, -0.075f), 0.034f);
            go.AddComponent<SoundPackButton>().Configure(label.GetComponentInChildren<TMP_Text>());
        }

        // The instrument packs, and the furniture they play on (left to right). Each pack's volumes are trimmed to
        // the approved set's loudness (SoundSets), so switching packs keeps the mix balance.
        static void CreateInstrumentPacks(GameObject root, InstrumentTarget[] targets)
        {
            InstrumentPacks.Pack Make(UiTerm name, string dir)
            {
                var pack = new InstrumentPacks.Pack { name = name };
                pack.udu = SoundSets.Clips(dir, "Udu");
                pack.mbira = SoundSets.Clips(dir, "Mbira");
                pack.erhu = SoundSets.Clips(dir, "Erhu");
                pack.volumes = new[] { InstrumentType.Udu, InstrumentType.Mbira, InstrumentType.Erhu }
                    .Select(t => SoundSets.VolumeFor(dir, t.ToString())).ToArray();
                return pack;
            }
            var packs = root.AddComponent<InstrumentPacks>();
            packs.Configure(new[] { Make(UiTerm.PackClassic, SoundSets.OriginalDir), Make(UiTerm.PackEarthy, SoundSets.SynthDir) }, targets);
            EditorUtility.SetDirty(packs);
        }

        // Last on the front row: plays the guided song again (it is remembered as done once finished or skipped).
        static void CreateLessonButton(Vector3 position, Transform root, Material slab, Material mark)
        {
            var go = SmallButton("LessonButton", position, root, slab, "LessonSlab");
            // A music note standing on the slab, like the other buttons' icons: a round head, a stem and a flag.
            var head = Primitive(PrimitiveType.Sphere, "LessonNoteHead", go.transform, mark, keepCollider: false);
            head.localPosition = new Vector3(-0.008f, 0.026f, 0f);
            head.localRotation = Quaternion.Euler(0f, 0f, 20f);
            head.localScale = new Vector3(0.026f, 0.018f, 0.012f);
            Box("LessonNoteStem", go.transform, new Vector3(0.004f, 0.045f, 0f), new Vector3(0.005f, 0.04f, 0.008f), mark, keepCollider: false);
            Box("LessonNoteFlag", go.transform, new Vector3(0.012f, 0.058f, 0f), new Vector3(0.016f, 0.006f, 0.008f), mark, keepCollider: false).localRotation = Quaternion.Euler(0f, 0f, -35f);
            Localize(Label("LessonLabel", UiText.Get(UiTerm.Lesson, UiLanguage.English), go.transform, SmallLabelOffset, SmallLabelHeight), UiTerm.Lesson);
            go.AddComponent<TutorialReplayButton>();
        }

        // The extras wait under the leaf during the guided song, so it starts with one thing to try (George,
        // 2026-11-03); saved tucked, like the stations, so nothing moves at startup when the lesson runs.
        static readonly string[] LessonTucked = { "CallButton", "ClearButton", "LessonButton", "SoundsButton", "TierButton" };

        static void TuckForLesson(Transform leafRoot)
        {
            foreach (var name in LessonTucked)
            {
                var control = leafRoot.Find(name);
                if (control == null) control = leafRoot.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
                if (control == null)
                {
                    Debug.LogWarning($"[RoomOrchestra] Nothing named {name} to tuck away during the lesson");
                    continue;
                }
                var tuck = control.gameObject.AddComponent<TuckAway>();
                tuck.Configure(LeafCenter);
                tuck.TuckNow();
                EditorUtility.SetDirty(tuck);
            }
        }

        // The confirm card (owner, headset 2026-11-07: "the double quit from the leaf is not self-explanatory"): a dark
        // card that pops up in front of a button that ends or wipes something, saying what will happen, with a big
        // coral "do it" button and a green Cancel. It waits shrunk in the ball stand's cloud (ConfirmCard moves it).
        static readonly Vector3 ConfirmSize = new(0.4f, 0.2f, 0.012f);

        static void CreateConfirmCard(Transform root)
        {
            var park = GameObject.Find("RoomOrchestra")?.transform.Find("BallDispenser/ParkPoint");
            var card = Holder("ConfirmCard", root, Vector3.zero);
            card.position = new Vector3(0f, 1.0f, 0.6f);
            card.rotation = Quaternion.identity; // faces +z, away from the seat; ConfirmCard turns it to the player
            var cream = EnsureMaterial("SpeechBubbleRim", "Ball", new Color(1f, 0.96f, 0.88f));
            var plum = EnsureMaterial("SpeechBubble", "Ball", new Color(0.2f, 0.1f, 0.28f));
            Box("ConfirmRim", card, new Vector3(0f, 0f, 0.006f), ConfirmSize + new Vector3(0.02f, 0.02f, 0f), cream, keepCollider: false);
            Box("ConfirmPanel", card, Vector3.zero, ConfirmSize, plum, keepCollider: false);
            var words = Label("ConfirmQuestion", "Ag", card, new Vector3(0f, 0.045f, -ConfirmSize.z / 2f - 0.006f), 0.028f);
            words.localRotation = Quaternion.identity;
            var question = words.GetComponentInChildren<TMP_Text>();
            question.text = UiText.Get(UiTerm.ConfirmLesson, UiLanguage.English); // the longest; sized for it
            question.textWrappingMode = TextWrappingModes.Normal;
            question.rectTransform.sizeDelta = new Vector2(ConfirmSize.x - 0.04f, 0.09f);
            question.enableAutoSizing = true;
            question.fontSizeMax = question.fontSize;
            question.fontSizeMin = question.fontSize * 0.7f;
            question.ForceMeshUpdate(true, true);

            var taps = new List<Collider>();
            TMP_Text Choice(string name, bool yes, float x, Color colour, UiTerm sample)
            {
                var button = Holder(name, card, new Vector3(x, -0.05f, -ConfirmSize.z / 2f - 0.01f));
                var slab = Box($"{name}Slab", button, Vector3.zero, new Vector3(0.16f, 0.07f, 0.02f), EnsureMaterial(name, "Ball", colour), keepCollider: false);
                TapBox(slab, Vector3.zero, new Vector3(0.17f, 0.08f, 0.04f));
                taps.Add(slab.GetComponent<Collider>());
                var label = Label($"{name}Label", UiText.Get(sample, UiLanguage.English), button, new Vector3(0f, 0f, -0.016f), 0.026f);
                label.localRotation = Quaternion.identity;
                button.gameObject.AddComponent<ConfirmChoice>().Configure(yes);
                return label.GetComponentInChildren<TMP_Text>();
            }
            var yesText = Choice("ConfirmYes", true, -0.09f, new Color(0.93f, 0.42f, 0.32f), UiTerm.ConfirmRestart);
            var noText = Choice("ConfirmNo", false, 0.09f, new Color(0.36f, 0.72f, 0.38f), UiTerm.ConfirmCancel);
            RoundedShapes.RoundBlocks(card, 0.015f);
            var confirm = card.gameObject.AddComponent<ConfirmCard>();
            confirm.Configure(question, yesText, noText, park, taps.ToArray());
            EditorUtility.SetDirty(confirm);
        }

        // Glowing spots for the real furniture the scan finds (owner, headset 2026-11-08: "show me visually where I can
        // assign the furniture to the actual furniture"). In the headset's own space beside the real walls (recentring
        // must not move them off the furniture); each waits shrunk in the ball stand's cloud until RealFurniture uses it.
        public const int RealSpotCount = 6;

        static void CreateRealSpots()
        {
            var walls = Object.FindFirstObjectByType<RealWalls>();
            var park = GameObject.Find("RoomOrchestra")?.transform.Find("BallDispenser/ParkPoint");
            if (walls == null || park == null) return;
            for (int i = walls.transform.childCount - 1; i >= 0; i--)
                if (walls.transform.GetChild(i).name.StartsWith("RealSpot")) Object.DestroyImmediate(walls.transform.GetChild(i).gameObject);
            var glow = Glow(EnsureMaterial("RealSpotPad", "Ball", new Color(1f, 0.86f, 0.35f)), new Color(1f, 0.86f, 0.35f) * 1.4f);
            var backing = EnsureMaterial("LabelBacking", "Ball", new Color(0.12f, 0.08f, 0.16f));
            for (int i = 0; i < RealSpotCount; i++)
            {
                var spot = new GameObject($"RealSpot{i}").transform;
                spot.SetParent(walls.transform, false);
                var pad = Primitive(PrimitiveType.Cylinder, "SpotPad", spot, glow, keepCollider: false);
                pad.localScale = new Vector3(0.4f, RealSpot.PadThickness, 0.4f);
                var tap = pad.gameObject.AddComponent<BoxCollider>();
                tap.size = new Vector3(1f, 6f, 1f); // about 7 cm deep: easy to point at, flat on the furniture
                tap.isTrigger = true; // takes taps and the hover highlight; balls go through to the real piece's hit box
                Hoverable(pad.gameObject);
                // read from across the room: 8 cm letters (measured on "Ag", like the other labels) on a big dark plate
                var label = Label("SpotLabel", "Ag", spot, Vector3.up * RealSpot.LabelHeight, 0.08f);
                label.localRotation = Quaternion.identity;
                label.GetComponentInChildren<TMP_Text>().text = "+";
                Box("SpotLabelBacking", label, new Vector3(0f, 0f, 0.008f), new Vector3(0.36f, 0.13f, 0.012f), backing, keepCollider: false);
                var view = spot.gameObject.AddComponent<RealSpot>();
                view.Configure(pad, label, label.GetComponentInChildren<TMP_Text>(), park, tap);
                EditorUtility.SetDirty(view);
            }
        }

        // Last on the front row: Quit, reachable during play (George, 2026-11-03), marked with a door ajar (Clear has
        // the X). Asks "Sure?" first.
        static void CreateQuitButton(Vector3 position, Transform root, Material slab, Material mark)
        {
            var go = SmallButton("QuitButton", position, root, slab, "QuitSlab");
            Box("QuitDoorFrame", go.transform, new Vector3(0f, 0.04f, 0.004f), new Vector3(0.03f, 0.046f, 0.004f), mark, keepCollider: false);
            var door = Box("QuitDoor", go.transform, new Vector3(-0.004f, 0.04f, -0.004f), new Vector3(0.024f, 0.04f, 0.006f), slab, keepCollider: false);
            door.localRotation = Quaternion.Euler(0f, 28f, 0f); // swung open toward the player
            var knob = Primitive(PrimitiveType.Sphere, "QuitDoorKnob", go.transform, mark, keepCollider: false);
            knob.localPosition = new Vector3(0.003f, 0.04f, -0.012f);
            knob.localScale = Vector3.one * 0.006f;
            Localize(Label("QuitLabel", UiText.Get(UiTerm.Quit, UiLanguage.English), go.transform, SmallLabelOffset, SmallLabelHeight), UiTerm.Quit);
            go.AddComponent<QuitButton>();
        }

        static void CreateLanguageButton(Vector3 position, Transform root, Material slab)
        {
            var go = SmallButton("LanguageButton", position, root, slab, "LanguageSlab");
            var label = Label("LanguageLabel", UiText.Get(UiTerm.SwitchLanguage, UiLanguage.English), go.transform, new Vector3(0f, 0.04f, 0f), 0.022f);
            Localize(label, UiTerm.SwitchLanguage);
            go.AddComponent<LanguageButton>();
        }

        // The leaf palette itself: a flat green leaf with a pale midrib, on a stalk. Decoration only: no colliders, so it never takes a tap from the buttons on it.
        static LeafStalk CreateLeaf(Transform leafHolder, Transform pad, Material vein)
        {
            var leafMaterial = EnsureMaterial("LeafPad", "Ball", new Color(0.42f, 0.74f, 0.4f));
            var leaf = Primitive(PrimitiveType.Sphere, "Leaf", leafHolder, leafMaterial, keepCollider: false);
            leaf.position = LeafCenter;
            leaf.localScale = LeafSize;
            // Over the thick middle only: the leaf thins to nothing at its tips.
            var rib = Box("LeafRib", leafHolder, Vector3.zero, new Vector3(LeafSize.x * 0.75f, 0.004f, 0.008f), vein, keepCollider: false);
            rib.position = new Vector3(LeafCenter.x, LeafCenter.y + LeafSize.y / 2f - 0.001f, (TrayCenter.z + ButtonRowZ) / 2f);
            // The stalk stands on the floor in a little hole (owner, 2026-10-29); in the opening the hole opens
            // and the stalk grows up to meet the swooping leaf. Saved closed: the opening's first frame.
            var stem = Holder("LeafStem", pad, Vector3.zero);
            var stalk = Box("LeafStalk", stem, Vector3.zero, Vector3.one, leafMaterial, keepCollider: false);
            // A black hole in the floor with jagged cracks running out from it, as if the stalk splits the real
            // floor open (owner: mixed reality). Once the stalk is up, the hole closes again (IntroSequence).
            var hole = Holder("LeafHole", stem, Vector3.zero);
            var black = EnsureMaterial("LeafHolePit", "Ball", new Color(0.02f, 0.02f, 0.02f));
            var pit = Primitive(PrimitiveType.Cylinder, "HolePit", hole, black, keepCollider: false);
            pit.localScale = new Vector3(0.11f, 0.002f, 0.11f);
            // Short, jagged cracks that zigzag and fork, wide at the hole and thinning out (not long, even
            // legs: a first try read as a spider).
            var random = new System.Random(29);
            void Crack(string name, Vector2 from, float angle, float reach, int segments, float width, int depth)
            {
                for (int k = 0; k < segments; k++)
                {
                    angle += (float)(random.NextDouble() - 0.5) * 1.4f; // sharp zigzags
                    var to = from + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (reach / segments);
                    var mid = (from + to) / 2f;
                    // Never under 0.001 m once the closed hole is shrunk to LeafStalk.Hidden (the near-zero scale rule).
                    float w = Mathf.Max(width * (1f - 0.7f * k / segments), 0.0012f / LeafStalk.Hidden);
                    var piece = Box($"{name}_{k}", hole, new Vector3(mid.x, 0.001f, mid.y),
                        new Vector3(w, 0.002f, (to - from).magnitude + w * 0.5f), black, keepCollider: false);
                    piece.localRotation = Quaternion.LookRotation(new Vector3(to.x - from.x, 0f, to.y - from.y), Vector3.up);
                    if (depth == 0 && k == 1) Crack($"{name}b", to, angle + (random.NextDouble() < 0.5 ? 0.9f : -0.9f), reach * 0.45f, 2, w * 0.7f, 1);
                    from = to;
                }
            }
            for (int c = 0; c < 6; c++)
            {
                float angle = c * Mathf.PI * 2f / 6f + (float)(random.NextDouble() - 0.5) * 0.7f;
                var from = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.048f;
                Crack($"Crack{c}", from, angle, 0.055f + 0.05f * (float)random.NextDouble(), 4, 0.012f, 0);
            }
            var leafStalk = stem.gameObject.AddComponent<LeafStalk>();
            var cloudCore = GameObject.Find("RoomOrchestra")?.transform.Find("BallDispenser/ParkPoint");
            leafStalk.Configure(stalk, hole, LeafCenter - new Vector3(0f, LeafSize.y / 2f, 0f), 0.03f, startGrown: 0f, startOpen: 0f, cloudCore);
            EditorUtility.SetDirty(leafStalk);
            return leafStalk;
        }

        // One small button on the leaf's front row: a slab facing the player and a tap box a little larger.
        static GameObject SmallButton(string name, Vector3 position, Transform root, Material slab, string slabName)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.position = position;
            go.transform.rotation = FacePlayer(position);
            var slabPart = Box(slabName, go.transform, Vector3.zero, SmallSlab, slab, keepCollider: false);
            TapBox(slabPart, new Vector3(0f, 0.03f, 0f), SmallCollider);
            return go;
        }

        // PICO's hover highlight (Version 1 feedback: nothing showed what you were pointing at) appears on an
        // object that carries both the tap collider and a visible mesh, so each tap volume sits on a visible
        // part. Taps still reach the control: input looks the owner up from the hit collider's parents.
        static void Hoverable(GameObject visible)
        {
            if (!visible.TryGetComponent<SpatialAdapterHoverEffect>(out _)) visible.AddComponent<SpatialAdapterHoverEffect>();
        }

        // The tap box, given in its control's space, placed on the control's visible part (an unrotated child),
        // so it keeps exactly the size and place it had on the control itself.
        static void TapBox(Transform visible, Vector3 center, Vector3 size)
        {
            var s = visible.localScale;
            var box = visible.gameObject.AddComponent<BoxCollider>();
            box.center = Vector3.Scale(center - visible.localPosition, new Vector3(1f / s.x, 1f / s.y, 1f / s.z));
            box.size = Vector3.Scale(size, new Vector3(1f / s.x, 1f / s.y, 1f / s.z));
            Hoverable(visible.gameObject);
        }

        // The ball's physics collider is on its root, but PICO's hover highlight needs a collider and a mesh on the
        // same object (George, 2026-11-03: highlight what I can pinch). So the visible body gets a trigger sphere of
        // its own: it shows the highlight and takes the pinch (the launcher finds the ball from its parents), while
        // triggers never touch the ball's bounces, mass or landing checks (those raycasts ignore triggers).
        static void HoverShell(Renderer body)
        {
            if (body == null) return;
            if (!body.TryGetComponent<SphereCollider>(out var shell)) shell = body.gameObject.AddComponent<SphereCollider>();
            var bounds = body.GetComponent<MeshFilter>().sharedMesh.bounds;
            shell.center = bounds.center;
            shell.radius = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
            shell.isTrigger = true;
            Hoverable(body.gameObject);
        }

        // A character's tap sphere on its body mesh (the "Visual" child, at the root's origin and scale).
        static void TapSphere(Transform body, float radius)
        {
            body.gameObject.AddComponent<SphereCollider>().radius = radius;
            Hoverable(body.gameObject);
        }

        // The call (roaming option C), first on the front row: a small green leaf, "Come here".
        static void CreateCallButton(Vector3 position, Transform root, Material slab, Material vein)
        {
            var go = SmallButton("CallButton", position, root, slab, "CallSlab");
            var leaf = Primitive(PrimitiveType.Sphere, "CallLeaf", go.transform, EnsureMaterial("CallLeaf", "Ball", new Color(0.36f, 0.72f, 0.38f)), keepCollider: false);
            leaf.localPosition = new Vector3(0f, 0.03f, 0f);
            leaf.localRotation = Quaternion.Euler(0f, 30f, 0f);
            leaf.localScale = new Vector3(0.06f, 0.012f, 0.032f);
            var rib = Box("CallLeafVein", go.transform, new Vector3(0f, 0.036f, 0f), new Vector3(0.045f, 0.002f, 0.004f), vein, keepCollider: false);
            rib.localRotation = Quaternion.Euler(0f, 30f, 0f);
            Localize(Label("CallLabel", UiText.Get(UiTerm.CallTray, UiLanguage.English), go.transform, SmallLabelOffset, SmallLabelHeight), UiTerm.CallTray);
            go.AddComponent<TrayCallButton>();
        }

        static void Localize(Transform labelHolder, UiTerm term) =>
            labelHolder.gameObject.AddComponent<LocalizedLabel>().Configure(labelHolder.GetComponentInChildren<TMP_Text>(), term);

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
                Hoverable(tile.gameObject);
                tile.position = new Vector3(go.transform.position.x, ColumnBottom + ColumnStep * i, go.transform.position.z);
                tile.rotation = FacePlayer(tile.position);
                tile.localScale = focused && i == StartPetal ? TileScale * 1.2f : TileScale;
                var note = new MusicalNote(target.Instrument, i);
                string text = UiText.Tile(note, UiLanguage.English);
                // Label on the tile's player-facing side, sized in world units (not stretched by the tile).
                // Sized for a note name; any word (a drum sound, Chinese) shrinks to fit the tile, since the furniture
                // can play any instrument.
                var label = Label($"TileLabel{i}", "D", go.transform, Vector3.zero, 0.045f);
                label.position = tile.position - tile.forward * 0.02f;
                label.rotation = tile.rotation;
                var tileText = label.GetComponentInChildren<TMP_Text>();
                tileText.rectTransform.sizeDelta = new Vector2(TileScale.x - 0.015f, TileScale.y);
                tileText.enableAutoSizing = true;
                tileText.fontSizeMax = tileText.fontSize;
                tileText.fontSizeMin = tileText.fontSize * 0.5f;
                tileText.text = text;
                tileText.ForceMeshUpdate(true, true);
                label.gameObject.AddComponent<LocalizedLabel>().ConfigureTile(tileText, target, i);
                tiles[i] = tile.gameObject.AddComponent<PitchTile>();
                tiles[i].Configure(target, i);
                EditorUtility.SetDirty(tiles[i]);
            }
            // Jianpu shows its key above the music: "1=D" over the note tiles, in Chinese only, and only while the
            // furniture plays a melodic instrument (every column has one: instruments can change places).
            {
                // Sized from its Chinese text, then authored blank like every label's English start.
                var key = Label("KeyLabel", UiText.Get(UiTerm.KeyOfD, UiLanguage.Chinese), go.transform, Vector3.zero, 0.045f); // as tall as the tile numbers
                key.GetComponentInChildren<TMP_Text>().text = UiText.Get(UiTerm.KeyOfD, UiLanguage.English);
                key.position = new Vector3(go.transform.position.x, ColumnBottom + ColumnStep * (NoteMapping.Count - 1) + 0.11f, go.transform.position.z);
                key.rotation = FacePlayer(key.position);
                Localize(key, UiTerm.KeyOfD);
                key.GetComponent<LocalizedLabel>().ConfigureMelodicOnly(target);
                if (new MusicalNote(target.Instrument, 0).IsPercussion) key.GetComponentInChildren<TMP_Text>().text = "";
                // A small dark plate behind it (white text alone may vanish against a light wall).
                // In English it waits inside the top tile.
                var plate = Primitive(PrimitiveType.Cube, "KeyBacking", go.transform, slab, keepCollider: false);
                plate.rotation = key.rotation;
                plate.localScale = new Vector3(0.09f, 0.06f, 0.015f); // deep enough to stay above the near-zero limit while its station is hidden
                var shown = go.transform.InverseTransformPoint(key.position + key.forward * 0.008f);
                var parked = go.transform.InverseTransformPoint(tiles[NoteMapping.Count - 1].transform.position);
                plate.localPosition = parked;
                var backing = key.gameObject.AddComponent<ChineseOnlyBacking>();
                backing.Configure(plate, shown, parked);
                backing.ConfigureOwner(target);
            }
            // The instrument's name tile is also its volume button (tap steps 100/60/30%/Muted).
            var mixTile = Primitive(PrimitiveType.Cube, "MixTile", go.transform, slab, keepCollider: true);
            Hoverable(mixTile.gameObject);
            mixTile.position = new Vector3(go.transform.position.x, ColumnBottom - 0.14f, go.transform.position.z);
            mixTile.rotation = FacePlayer(mixTile.position);
            mixTile.localScale = new Vector3(0.16f, 0.1f, 0.03f);
            var caption = Label("InstrumentLabel", MixButton.TextFor(target.Instrument, 0, UiLanguage.English), go.transform, Vector3.zero, 0.07f); // two lines, ~3 cm each
            caption.position = mixTile.position - mixTile.forward * 0.02f;
            caption.rotation = mixTile.rotation;
            var mix = mixTile.gameObject.AddComponent<MixButton>();
            mix.Configure(target, caption.GetComponentInChildren<TMP_Text>());
            EditorUtility.SetDirty(mix);

            // Under the name: Change gives this furniture the next instrument (InstrumentPacks).
            var changeTile = Primitive(PrimitiveType.Cube, "ChangeTile", go.transform, slab, keepCollider: true);
            Hoverable(changeTile.gameObject);
            changeTile.position = new Vector3(go.transform.position.x, ColumnBottom - 0.26f, go.transform.position.z);
            changeTile.rotation = FacePlayer(changeTile.position);
            changeTile.localScale = new Vector3(0.14f, 0.07f, 0.03f);
            var changeLabel = Label("ChangeLabel", UiText.Get(UiTerm.ChangeInstrument, UiLanguage.English), go.transform, Vector3.zero, 0.03f);
            changeLabel.position = changeTile.position - changeTile.forward * 0.02f;
            changeLabel.rotation = changeTile.rotation;
            Localize(changeLabel, UiTerm.ChangeInstrument);
            changeTile.gameObject.AddComponent<ChangeInstrumentButton>().Configure(target);

            var column = go.AddComponent<PitchColumn>();
            column.Configure(target, tiles, TileScale);
            EditorUtility.SetDirty(column);
            return column;
        }

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
            tmp.font = LabelFont.Ensure(); // English and Chinese
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

        // The character materials the leaf's trio already wears (CharacterArt), in OrbType order.
        static Material[] Skins(string prefix) => System.Enum.GetValues(typeof(OrbType)).Cast<OrbType>()
            .Select(t => AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/{prefix}_{t}.mat")).ToArray();

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
                if (CharacterArt.Has(OrbType.Bop))
                {
                    var (look, rim, face) = CharacterArt.DressBall(ball.gameObject, OrbType.Flutter);
                    ball.ConfigureCharacter(rim, face, look);
                    ball.ConfigureSkins(Skins("Orb"), Skins("Rim"), Skins("Face"));
                    HoverShell(look);
                    EditorUtility.SetDirty(ball);
                }
                else if (ball.TryGetComponent<Renderer>(out var renderer))
                {
                    renderer.sharedMaterial = flutter;
                }
                var body = ball.GetComponent<Rigidbody>();
                if (body != null) body.mass = profile.Mass;
                // Its own bounce off walls: a quiet 3D voice on the ball itself.
                if (!ball.TryGetComponent<AudioSource>(out var voice)) voice = ball.gameObject.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                voice.spatialBlend = 1f;
                voice.rolloffMode = AudioRolloffMode.Linear;
                voice.minDistance = 0.5f;
                voice.maxDistance = 8f;
                ball.ConfigureBounce(voice, BounceClips());
                EditorUtility.SetDirty(ball);
            }
        }

        // Each character's own bounce (ArtSource/Audio/gen_bounce.py), in OrbType order.
        static AudioClip[] BounceClips()
        {
            var clips = new[] { OrbType.Flutter, OrbType.Bop, OrbType.Boom }
                .Select(o => AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/RoomOrchestra/Audio/Effects/Bounce_{o}.wav")).ToArray();
            if (clips.Any(c => c == null)) Debug.LogWarning("[RoomOrchestra] Bounce sounds missing: run ArtSource/Audio/gen_bounce.py");
            return clips;
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

        // The room floor: thrown characters that land on it vanish in a poof.
        static void MarkGround()
        {
            var floor = GameObject.Find("FloorCollider");
            if (floor != null && !floor.TryGetComponent<GroundSurface>(out _)) floor.AddComponent<GroundSurface>();
        }

        // Poof clouds of seven puffs, waiting inside the ball stand's cloud until a character lands.
        const int PoofClouds = 16, PuffsPerCloud = 7; // landings plus Clear's smoke over vines, toadstools and butterflies
        const float PuffSize = 0.035f;
        const string SmokeSoundPath = "Assets/RoomOrchestra/Audio/Effects/Smoke.wav";
        const float PoofVolume = 0.45f;

        static void CreatePoofClouds(Transform root)
        {
            var puffMaterial = Glow(EnsureMaterial("Poof", "Ball", new Color(0.97f, 0.97f, 0.95f)), new Color(0.97f, 0.97f, 0.95f));
            var park = Holder("PoofPark", root, Vector3.zero);
            park.position = DispenserPosition + CloudCoreHeight; // inside the stand cloud's core, with the spare balls
            var smokeSound = AssetDatabase.LoadAssetAtPath<AudioClip>(SmokeSoundPath);
            if (smokeSound == null) Debug.LogWarning("[RoomOrchestra] Smoke sound missing: run ArtSource/Audio/gen_poof.py");
            for (int c = 0; c < PoofClouds; c++)
            {
                var cloud = Holder($"PoofCloud{c}", root, Vector3.zero);
                cloud.position = park.position;
                var puffs = new Transform[PuffsPerCloud];
                for (int i = 0; i < puffs.Length; i++)
                {
                    puffs[i] = Primitive(PrimitiveType.Sphere, $"Puff{i}", cloud, puffMaterial, keepCollider: false);
                    puffs[i].localScale = Vector3.one * PuffSize;
                }
                var poof = cloud.gameObject.AddComponent<PoofCloud>();
                poof.Configure(puffs, park);
                // Plain 2D like the instruments, so the limiter on the listener covers it too; quiet next to the music.
                var source = cloud.gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.volume = PoofVolume;
                poof.ConfigureSound(source, smokeSound);
                EditorUtility.SetDirty(poof);
            }
        }

        // The ball stand (owner 2026-10-28): the ready ball floats on a little cloud instead of a post.
        // The scene's Pedestal becomes the cloud's core, a solid egg that hides the spare balls and the
        // waiting poof clouds (and stops throws, as the post did); puffs around it, without colliders, make the shape.
        static readonly Vector3 CloudCoreHeight = new Vector3(0f, 0.915f, 0f); // just under the ready ball (spawn at 1.06 m)
        // As narrow as the old post (hand pointer rays to the erhu notes pass beside it), tall enough for a parked Flutter.
        static readonly Vector3 CloudCoreSize = new Vector3(0.12f, 0.16f, 0.12f);
        static readonly (Vector3 at, float size)[] CloudPuffs =
        {
            (new Vector3(-0.10f, -0.01f, 0f), 0.12f), (new Vector3(0.10f, -0.01f, 0f), 0.12f),
            (new Vector3(-0.05f, 0.02f, 0.05f), 0.12f), (new Vector3(0.05f, 0.02f, -0.05f), 0.12f),
            (new Vector3(0.04f, 0.02f, 0.06f), 0.11f), (new Vector3(-0.04f, 0.02f, -0.06f), 0.11f),
            (new Vector3(-0.16f, -0.025f, 0.01f), 0.08f), (new Vector3(0.16f, -0.025f, -0.01f), 0.08f),
        };

        static void CreateCloudStand(Transform dispenser)
        {
            var white = new Color(0.95f, 0.97f, 1f);
            var cloudMaterial = Glow(EnsureMaterial("StandCloud", "Ball", white), white * 0.6f);
            var core = dispenser.Find("Pedestal");
            if (core != null)
            {
                core.localPosition = CloudCoreHeight;
                core.localScale = CloudCoreSize;
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                core.GetComponent<MeshFilter>().sharedMesh = sphere.GetComponent<MeshFilter>().sharedMesh;
                Object.DestroyImmediate(sphere);
                if (core.TryGetComponent<SphereCollider>(out var round)) Object.DestroyImmediate(round);
                if (!core.TryGetComponent<CapsuleCollider>(out var capsule)) capsule = core.gameObject.AddComponent<CapsuleCollider>();
                capsule.center = Vector3.zero;
                capsule.direction = 1;
                capsule.radius = 0.5f;
                capsule.height = 1f; // a short upright capsule inside the egg-shaped core
                core.GetComponent<Renderer>().sharedMaterial = cloudMaterial;
            }
            var park = dispenser.Find("ParkPoint");
            if (park != null) park.localPosition = CloudCoreHeight;
            // Spare balls are authored where they wait (PICO Spatial keeps a pose moved before its renderer
            // connects): inside the cloud, not at the old post height. The ready ball stays on the spawn point.
            var spawn = dispenser.Find("SpawnPoint");
            var pool = dispenser.Find("BallPool");
            if (pool != null)
                foreach (var ball in pool.GetComponentsInChildren<Ball>(true))
                    if (spawn == null || (ball.transform.position - spawn.position).sqrMagnitude > 1e-6f)
                        ball.transform.position = dispenser.TransformPoint(CloudCoreHeight);

            var old = dispenser.Find("CloudPuffs");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var puffs = Holder("CloudPuffs", dispenser, CloudCoreHeight);
            for (int i = 0; i < CloudPuffs.Length; i++)
            {
                var puff = Primitive(PrimitiveType.Sphere, $"CloudPuff{i}", puffs, cloudMaterial, keepCollider: false);
                puff.localPosition = CloudPuffs[i].at;
                puff.localScale = new Vector3(1f, 0.8f, 1f) * CloudPuffs[i].size; // flat-bottomed, like a cartoon cloud
            }
        }

        // A soft self-glow in the body colour so characters stay bright in a dim room (owner: "brighter").
        static Material Glow(Material material, Color color)
        {
            if (!material.HasProperty("_EmissionColor")) return material;
            material.EnableKeyword("_EMISSION");
            // URP re-validates materials and keeps _EMISSION only when the GI flags say emissive.
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            material.SetColor("_EmissionColor", color * Ball.GlowStrength);
            // Matte, like the reference: a glossy finish mirrored the grey room and washed the colour out.
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.1f);
            if (material.HasProperty("_EnvironmentReflections")) material.SetFloat("_EnvironmentReflections", 0f);
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
