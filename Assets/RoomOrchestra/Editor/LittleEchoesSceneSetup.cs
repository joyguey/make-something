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
        // The leaf's own row now (owner, 2026-11-08: too many buttons on the leaf): Come here, Menu, Undo, Pause, Clear,
        // spread out (owner, headset 2026-11-08: "Come here" belongs on the leaf, not buried in the menu).
        const float LeafRowStep = 0.15f;
        // Toy buttons stand 8 mm higher on the leaf, so their cream rings sit on its top instead of inside it (owner, 2026-11-19).
        const float LeafRowLift = 0.008f;
        static Vector3 LeafRow(int slot) => new Vector3(LeafCenter.x + LeafRowStep * (slot - 2f), ButtonY + LeafRowLift, ButtonRowZ);

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

            // Loudness was taken off (owner, headset 2026-11-08: "completely unnecessary"); taps play at the medium level
            // and a throw's strength still sets how loud it is.
            CreateSoundsButton(swoop, slabMaterial, motifMaterial);
            CreateViewButton(swoop, slabMaterial, motifMaterial);
            CreateCallButton(LeafRow(0), swoop, slabMaterial, veinMaterial); // first on the leaf's own row

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
            CreateMenuButton(LeafRow(1), swoop, slabMaterial, motifMaterial);
            CreateUndoButton(LeafRow(2), swoop, slabMaterial, motifMaterial);
            CreateMusicButton(MusicControlButton.Kind.Pause, LeafRow(3), swoop, slabMaterial, motifMaterial);
            CreateMusicButton(MusicControlButton.Kind.Clear, LeafRow(4), swoop, slabMaterial, motifMaterial);
            // the rest go onto the menu tray (CreateMenuTray)
            CreateRecenterButton(FrontRow(4), swoop, slabMaterial, motifMaterial);
            CreateHeightButton(FrontRow(5), swoop, slabMaterial, motifMaterial);
            CreateLanguageButton(FrontRow(6), swoop, slabMaterial);
            CreateLessonButton(FrontRow(7), swoop, slabMaterial, motifMaterial);
            CreateQuitButton(FrontRow(8), swoop, slabMaterial, motifMaterial);
            // Choose furniture on the leaf itself, right of Boom where Loudness was (owner, headset 2026-11-08)
            CreatePlacesButton(TrayCenter + new Vector3(0.4f, 0f, 0f), swoop, slabMaterial, motifMaterial);
            CreateRealHidesButton(FrontRow(8), swoop, slabMaterial, motifMaterial);
            CreateStartOverButton(FrontRow(8), swoop, slabMaterial, motifMaterial);
            CreateTogetherButton(FrontRow(8), swoop, slabMaterial, motifMaterial);
            var menuTray = CreateMenuTray(swoop, motifMaterial);
            root.AddComponent<LanguageDirector>();
            CreateScanProgressBar(root.transform);
            root.AddComponent<InstrumentIconLibrary>().Configure(
                ((InstrumentType[])System.Enum.GetValues(typeof(InstrumentType))).Select(InstrumentIconMesh).ToArray(), MeadowArt.Lit());
            // the meadow theme (V3 review: background music), on the title, the opening and while the song is empty
            var theme = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/RoomOrchestra/Audio/Music/MeadowTheme.wav");
            if (theme != null)
            {
                var music = new GameObject("BackgroundMusic");
                music.transform.SetParent(root.transform, false);
                music.AddComponent<AudioSource>();
                music.AddComponent<BackgroundMusic>().Configure(theme);
            }
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
            pad.gameObject.AddComponent<LeafGrab>(); // grip or fist at its edge moves it, the cloud along (owner, 2026-11-08)
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
            CreateHitBloom(root.transform, parent != null ? parent.transform.Find("BallDispenser/ParkPoint") : null);
            var intro = CreateIntro(root, swoop, tokens, leafStalk);
            intro.ConfigureStand(standCarried, StandFrontOffset, launcher, parent != null ? parent.transform : null); // and back in front for the first frame
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
            CreateXrRig();
            RoundedShapes.RoundBlocks(root.transform.Find("TitleCard"), 0.02f); // softened like the controls
            PicoXrMode.RoomFeatures(); // PICO's settings for the room features this scene uses (depth sensing too)
            menuTray.Configure(Vector3.one); // saved folded away, last: every label and rounded edge was sized open
            EditorUtility.SetDirty(menuTray);
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
            var walls = go.GetComponent<RealWalls>();
            walls.ConfigureScanLook(ScanLook(), Occluder());
            EditorUtility.SetDirty(walls);
        }

        // The scanned room while the player chooses places (owner, headset 2026-11-08: show me the room being mapped): a
        // see-through sky-blue glow over every real surface, unlit so it reads the same in any room light.
        // The scanned room in depth only (Shaders/RoomOccluder): real walls and furniture hide what is behind them.
        static Material Occluder()
        {
            string path = $"{MaterialDir}/RoomOccluder.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("RoomOrchestra/RoomOccluder");
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != shader) m.shader = shader;
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry - 10;
            EditorUtility.SetDirty(m);
            return m;
        }

        // PICO's environment depth written into the depth buffer (Shaders/EnvironmentDepthOccluder).
        static Material DepthOccluder()
        {
            string path = $"{MaterialDir}/EnvironmentDepthOccluder.mat";
            var shader = Shader.Find("RoomOrchestra/EnvironmentDepthOccluder");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != shader) m.shader = shader;
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry - 20;
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material ScanLook()
        {
            string path = $"{MaterialDir}/RoomScan.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", new Color(0.45f, 0.85f, 1f, 0.22f));
            m.SetFloat("_Surface", 1f); // transparent
            m.SetFloat("_Blend", 0f);   // alpha
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", 0f);    // both sides: scanned pieces face any way
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

        // Owner (emulator): white words over the light green leaf were hard to read. Every label on the leaf gets a dark
        // rounded plate behind its words, sized for the longest thing it can say in either language, as a child of the
        // text so it grows with Large text. Flutter's bubble has its own dark card.
        static readonly Dictionary<string, string[]> LabelVariants = new()
        {
            { "HeightLabel", new[] { HeightButton.TextFor(PlayPosture.Seated, UiLanguage.English), HeightButton.TextFor(PlayPosture.Standing, UiLanguage.English), HeightButton.TextFor(PlayPosture.Seated, UiLanguage.Chinese), HeightButton.TextFor(PlayPosture.Standing, UiLanguage.Chinese) } },
            { "SoundsLabel", new[] { SoundPackButton.TextFor(UiTerm.PackClassic, UiLanguage.English), SoundPackButton.TextFor(UiTerm.PackEarthy, UiLanguage.English), SoundPackButton.TextFor(UiTerm.PackClassic, UiLanguage.Chinese), SoundPackButton.TextFor(UiTerm.PackEarthy, UiLanguage.Chinese) } },
            { "ViewLabel", System.Enum.GetValues(typeof(ViewMode)).Cast<ViewMode>().SelectMany(m => new[] { ViewButton.TextFor(m, UiLanguage.English), ViewButton.TextFor(m, UiLanguage.Chinese) }).ToArray() },
            { "MenuLabel", new[] { UiText.Get(UiTerm.Menu, UiLanguage.English), UiText.Get(UiTerm.MenuClose, UiLanguage.English), UiText.Get(UiTerm.Menu, UiLanguage.Chinese), UiText.Get(UiTerm.MenuClose, UiLanguage.Chinese) } },
            { "RealHidesLabel", new[] { UiText.Get(UiTerm.RealHidesOn, UiLanguage.English), UiText.Get(UiTerm.RealHidesOff, UiLanguage.English), UiText.Get(UiTerm.RealHidesOn, UiLanguage.Chinese), UiText.Get(UiTerm.RealHidesOff, UiLanguage.Chinese) } },
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
            // the toy buttons' plum caps and the tinted tiles keep their colours (they clear 4.5:1 already: GameplayLookTests)
            var slabs = controls.GetComponentsInChildren<Renderer>(true).Where(r => r.sharedMaterial == slab && !Skip(r)).ToArray();
            var icons = controls.GetComponentsInChildren<Renderer>(true).Where(r => (r.sharedMaterial == mark || r.sharedMaterial == IconGold()) && !Skip(r)).ToArray();
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
            // V3 review: "the room environment is just a box, what about a little meadow". The meadow's pieces are the
            // panels now; the room's walls stay as invisible fences, so thrown balls still bounce back into play.
            bool meadow = MeadowArt.Available;
            if (meadow) panels.AddRange(MeadowArt.Build(room));
            // The painted look and the haze on the far meadow (owner, Fable refinement 2026-11-11, option B)
            if (!room.TryGetComponent<LookSettings>(out _)) room.gameObject.AddComponent<LookSettings>();
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = LookSettings.HazeStart;
            RenderSettings.fogEndDistance = LookSettings.HazeEnd;
            RenderSettings.fogColor = MeadowTimeOfDay.DayHaze;

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
                        if (meadow) Object.DestroyImmediate(panel.GetComponent<MeshRenderer>()); // an invisible fence round the clearing
                        panel.rotation = Quaternion.LookRotation(Vector3.Cross(along, Vector3.up), Vector3.up);
                        panels.Add(panel);
                    }
            }
            Wall("WallFar", new Vector3(RoomMin.x, 0f, RoomMax.z), Vector3.right, size.x);
            Wall("WallNear", new Vector3(RoomMax.x, 0f, RoomMin.z), Vector3.left, size.x);
            Wall("WallLeft", new Vector3(RoomMin.x, 0f, RoomMin.z), Vector3.forward, size.z);
            Wall("WallRight", new Vector3(RoomMax.x, 0f, RoomMax.z), Vector3.back, size.z);
            // Floor and ceiling: flat grids (the box room only; the meadow has grass and sky).
            for (int x = 0; x < RoomColumns && !meadow; x++)
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
            foreach (var panel in panels) if (panel.TryGetComponent<MeshRenderer>(out var look)) look.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
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
        public const float BattleButtonStep = 0.118f, BattleButtonWidth = 0.094f; // four stickers within the poster

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
            // SOLO (owner, 2026-11-22): a battle against the game on one headset; four stickers across the same row
            var kinds = new[] { (EchoBattleButton.Kind.Host, UiTerm.BattleHost, pink, -3f), (EchoBattleButton.Kind.Join, UiTerm.BattleJoin, blue, 2f), (EchoBattleButton.Kind.Solo, UiTerm.BattleSolo, yellow, -2f), (EchoBattleButton.Kind.Leave, UiTerm.BattleLeave, orange, 1.5f) };
            for (int i = 0; i < kinds.Length; i++)
            {
                var (kind, term, colour, tilt) = kinds[i];
                var button = Holder($"Battle{kind}", card, new Vector3(BattleButtonStep * (i - (kinds.Length - 1) / 2f), -0.13f, front - 0.004f));
                button.localRotation = Quaternion.Euler(0f, 0f, tilt);
                Box($"Battle{kind}Edge", button, new Vector3(0f, 0f, 0.004f), new Vector3(BattleButtonWidth + 0.012f, 0.072f, 0.03f), white, keepCollider: false);
                var slab = Box($"Battle{kind}Slab", button, Vector3.zero, new Vector3(BattleButtonWidth, 0.06f, 0.03f), colour, keepCollider: false);
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
        // A live hit blooms (owner, 2026-11-19): the furniture squashes and springs back, petals puff up and drift down,
        // a ring ripples out (HitBloom). A fixed pool, parked in the ball stand's cloud while idle; no shadows.
        const int BloomPetals = 24, BloomRipples = 3;

        static void CreateHitBloom(Transform root, Transform park)
        {
            var holder = Holder("HitBloom", root, Vector3.zero);
            var petalMesh = PetalMesh();
            var colours = new List<Material>
            {
                EnsureMaterial("Petal_Blossom", "Ball", new Color(0.961f, 0.608f, 0.722f)), // #F59BB8, the meadow's blossom
                EnsureMaterial("Petal_Cream", "Ball", new Color(1f, 0.949f, 0.961f)),       // #FFF2F5
            };
            colours.AddRange(OrbCatalog.All.Select(p => EnsureMaterial($"Petal_{p.Type}", "Ball", new Color(p.R, p.G, p.B))));
            var parkAt = park != null ? park.position : new Vector3(0f, -1f, 0f);
            var petals = new Transform[BloomPetals];
            for (int i = 0; i < petals.Length; i++)
            {
                var petal = new GameObject($"Petal{i}").transform;
                petal.SetParent(holder, false);
                petal.gameObject.AddComponent<MeshFilter>().sharedMesh = petalMesh;
                var r = petal.gameObject.AddComponent<MeshRenderer>();
                r.sharedMaterial = colours[i % 2];
                r.enabled = false; // drawn only while it flies (HitBloom)
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                petal.position = parkAt;
                petal.localScale = Vector3.one * 0.002f; // parked small, above the near-zero limit
                petals[i] = petal;
            }
            var ripples = new Transform[BloomRipples];
            for (int i = 0; i < ripples.Length; i++)
            {
                var ring = new GameObject($"Ripple{i}").transform;
                ring.SetParent(holder, false);
                ring.gameObject.AddComponent<MeshFilter>();
                var r = ring.gameObject.AddComponent<MeshRenderer>();
                r.sharedMaterial = colours[2];
                r.enabled = false;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                ring.position = parkAt;
                ripples[i] = ring;
            }
            var bloom = holder.gameObject.AddComponent<HitBloom>();
            bloom.Configure(petals, colours.ToArray(), ripples, park);
            EditorUtility.SetDirty(bloom);
        }

        // A petal: a little cupped oval, 4 cm by 2.4 cm, both sides drawn.
        static Mesh PetalMesh()
        {
            const string path = "Assets/RoomOrchestra/Meshes/Petal.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null) return mesh;
            const int n = 12;
            // two sheets of their own (so each side has its own normals), the back a hair under the front
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int side = 0; side < 2; side++)
            {
                int start = vertices.Count;
                float drop = side * 0.0004f;
                vertices.Add(new Vector3(0f, 0.003f - drop, 0f));
                for (int k = 0; k < n; k++)
                {
                    float a = k * Mathf.PI * 2f / n;
                    vertices.Add(new Vector3(Mathf.Cos(a) * 0.02f, -drop, Mathf.Sin(a) * 0.012f));
                }
                for (int k = 0; k < n; k++)
                {
                    int a = start + 1 + k, b = start + 1 + (k + 1) % n;
                    if (side == 0) triangles.AddRange(new[] { start, b, a });
                    else triangles.AddRange(new[] { start, a, b });
                }
            }
            mesh = new Mesh { name = "Petal", vertices = vertices.ToArray(), triangles = triangles.ToArray() };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

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
            var rim = EnsureMaterial("CountRim", "Ball", new Color(0.165f, 0.122f, 0.239f));
            var pieces = new Transform[data.pieces.Length];
            for (int i = 0; i < pieces.Length; i++)
            {
                var piece = data.pieces[i];
                var colour = TutorialDirector.CountColours[Mathf.Min(i, TutorialDirector.CountColours.Length - 1)];
                var face = Glow(EnsureMaterial($"Count{Mathf.Min(i, 3)}Face", "Ball", colour), colour * 0.3f);
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
            var cream = EnsureMaterial("SpeechBubbleRim", "Ball", new Color(0.949f, 0.910f, 0.847f));
            var plum = EnsureMaterial("SpeechBubble", "Ball", new Color(0.165f, 0.122f, 0.239f));
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
            var leaf = Primitive(PrimitiveType.Cube, "SkipLeaf", skip, EnsureMaterial("TitleStart", "Ball", new Color(0.184f, 0.490f, 0.290f)), keepCollider: false);
            leaf.localScale = new Vector3(0.1f, 0.07f, 0.03f);
            leaf.gameObject.AddComponent<BoxCollider>();
            Hoverable(leaf.gameObject);
            var skipLabel = Label("SkipLabel", UiText.Get(UiTerm.Skip, UiLanguage.English), skip, new Vector3(0f, 0f, -0.02f), 0.024f);
            skipLabel.localRotation = Quaternion.identity;
            Localize(skipLabel, UiTerm.Skip);
            skip.gameObject.AddComponent<TutorialSkipButton>();

            // The x at the bubble's top right (owner, headset 2026-11-22): closes Flutter's lines after the lesson. A
            // cream disc on the cream rim's corner, a plum X on it; hidden during the lesson (Skip is there then).
            var closeX = Holder("BubbleCloseButton", bubble, new Vector3(BubbleSize.x / 2f + 0.005f, BubbleSize.y / 2f + 0.005f, -BubbleSize.z / 2f - 0.014f));
            var disc = Primitive(PrimitiveType.Cylinder, "BubbleCloseDisc", closeX, cream, keepCollider: false);
            disc.localRotation = Quaternion.Euler(90f, 0f, 0f); // its round face to the player
            disc.localScale = new Vector3(0.06f, 0.012f, 0.06f); // a 2.4 cm-deep puck (cylinder height is 2x its y scale)
            var tapBox = closeX.gameObject.AddComponent<BoxCollider>();
            tapBox.size = new Vector3(0.07f, 0.07f, 0.03f); // a little bigger than it looks: easy to hit
            Hoverable(closeX.gameObject);
            var xMark = Holder("BubbleCloseIconHolder", closeX, new Vector3(0f, -0.03f * 0.75f, -0.011f));
            xMark.localScale = Vector3.one * 0.75f;
            UiIcon("Clear", xMark, plum);
            closeX.gameObject.AddComponent<BubbleCloseButton>();
            closeX.gameObject.SetActive(false);

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

        /// <summary>The bush the three friends hide in until Start (stage space), left of where the leaf lands.</summary>
        public static readonly Vector3 FriendsBushAt = new(-1.05f, 0f, 1.6f);

        static IntroSequence CreateIntro(GameObject root, Transform swoop, OrbToken[] tokens, LeafStalk stalk)
        {
            var cast = tokens.Select(t => t.transform).ToArray();
            // Owner, headset 2026-11-12: not waiting on the floor at the title; they hide in a bush beside the leaf and
            // pop out of it on Start, hopping up onto the leaf as it lands.
            var floor = cast.Select((t, i) =>
            {
                var body = t.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
                float aboveFeet = t.position.y - body.min.y;
                return FriendsBushAt + new Vector3(-0.1f + 0.1f * i, aboveFeet + 0.003f, 0.04f * (i % 2));
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
        // Close and still from the first frame (owner, headset 2026-11-14: "the sign needs to be closer for me to put my
        // hand on it"; then, of a sign that glided up to hands: "that was scary... it should be static but close at the
        // start"): at arm's reach, a little smaller than life so it fits the view, its feet on the ground. It never moves
        // until Start sends it into the cloud.
        const float TitleScale = 0.85f, TitleCardHeight = 1.4f; // the card's middle above the ground at full size (m)
        static readonly Vector3 TitleCardAt = new Vector3(0f, TitleCardHeight * TitleScale, 0.85f); // 0.65, then 0.74, were "a little too close" (owner; V4 review); 0.85 is still a straight-arm reach
        /// <summary>The sign's top rail with its vine, above the card's middle (m): the friends stand on it.</summary>
        const float TitleRailTop = 0.398f; // the rail top (1.535 m in Blender x 1.173) less 2 mm: flush, no gap
        /// <summary>The rail's middle in the card's space: the hanging card swings about it (1.5 m in Blender x 1.173,
        /// less the card's 1.4 m; the rail is 4.5 cm behind the card).</summary>
        static readonly Vector3 TitleHangFrom = new Vector3(0f, 0.3595f, 0.045f);
        const float TitleRingX = 0.2f, TitleRingRadius = 0.062f, TitleRingThickness = 0.0065f;
        static readonly Vector2 HandprintHalf = new Vector2(0.08f, 0.095f);
        // From the stand's home (right of the leaf) to beside the title sign's right post, where Flutter on the cloud
        // greets the player without standing between them and the sign.
        static readonly Vector3 StandFrontOffset = new Vector3(0.16f, 0f, -0.35f);

        static TitleScreen CreateTitle(GameObject root, IntroSequence intro)
        {
            var card = Holder("TitleCard", root.transform, Vector3.zero);
            card.position = TitleCardAt;
            card.rotation = FacePlayer(TitleCardAt);
            card.localScale = Vector3.one * TitleScale;
            var cream = new Color(1f, 0.96f, 0.92f);
            // The grown-up title (owner, Fable refinement 2026-11-11: "option A with the square buttons from option C
            // and change the start button to a green background with white text"): a deep plum card with a cream rim,
            // the key-art lettering unchanged, rounded square buttons. Start's green keeps white text at 5:1.
            var plum = new Color(0.165f, 0.122f, 0.239f);    // #2A1F3D, the menu plates' family
            var rimCream = new Color(0.949f, 0.910f, 0.847f); // #F2E8D8
            var startGreen = new Color(0.184f, 0.490f, 0.290f); // #2F7D4A
            var quitGrey = new Color(0.290f, 0.255f, 0.341f);   // #4A4157
            // The card hangs from the sign's top rail on two steel rings and rocks about the rail (owner, Fable
            // refinement 2026-11-11): everything on the card is under "Hanging", whose origin is the rail's middle.
            var hanging = Holder("Hanging", card, TitleHangFrom);
            Vector3 On(Vector3 cardLocal) => cardLocal - TitleHangFrom;
            var steel = EnsureMaterial("SteelRing", "Ball", new Color(0.655f, 0.678f, 0.71f)); // #A7ADB5
            if (steel.HasProperty("_Smoothness")) steel.SetFloat("_Smoothness", 0.8f);
            if (steel.HasProperty("_Metallic")) steel.SetFloat("_Metallic", 0.6f);
            foreach (float x in new[] { -TitleRingX, TitleRingX })
                MeshPart(x < 0f ? "RingLeft" : "RingRight", hanging, SteelRingMesh(), new Vector3(x, 0f, 0f), steel);
            // 3 cm deep, so even parked at TitleScreen.ParkedScale nothing is under the near-zero scale limit.
            Box("TitleRim", hanging, On(new Vector3(0f, 0f, 0.02f)), new Vector3(0.95f, 0.59f, 0.03f), EnsureMaterial("TitleRim", "Ball", rimCream), keepCollider: false);
            Box("TitlePanel", hanging, On(new Vector3(0f, 0f, 0.012f)), new Vector3(0.92f, 0.56f, 0.03f), EnsureMaterial("TitlePanel", "Ball", plum), keepCollider: false);
            hanging.gameObject.AddComponent<TitleSwing>().Configure(new Vector2(0.475f, 0.295f), TitleHangFrom.y, -TitleHangFrom.z);

            // The key-art lettering as two flat meshes (cream letters on the icon's plum outline): PICO Spatial did
            // not draw it as a textured quad on the emulator (ArtSource/Icon/lettering_mesh.py explains).
            var (fill, outline) = TitleLetteringMeshes();
            var letters = Holder("TitleLettering", hanging, On(new Vector3(0f, 0.1f, 0f)));
            MeshPart("LetteringOutline", letters, outline, new Vector3(0f, 0f, -0.005f), EnsureMaterial("TitleLetterOutline", "Ball", new Color(0.23f, 0.14f, 0.31f)));
            MeshPart("LetteringFill", letters, fill, new Vector3(0f, 0f, -0.007f), EnsureMaterial("TitleLetterFill", "Ball", new Color(1f, 0.965f, 0.92f)));

            // Every word lies flat on its plate or button, 2 mm proud (owner, headset 2026-11-14, with the sign now at arm's
            // reach: "the quit and start word are not sitting flush to the sign and button"; they stood 3 cm off, turned
            // toward the player).
            Transform Flat(Transform label) { label.localRotation = Quaternion.identity; return label; }
            const float OnButton = -0.0195f; // the buttons are 35 mm deep: their face is 17.5 mm in front of their middle
            Localize(Flat(Label("TitleSubtitle", UiText.Get(UiTerm.TitleSubtitle, UiLanguage.English), hanging, On(new Vector3(0f, -0.055f, -0.005f)), 0.036f)), UiTerm.TitleSubtitle);

            // Bottom right, off the centre line: PICO draws its own window handle (the round grabber that opens
            // Back / Close / Minimize) on the app's centre line, and centred it covered Start (owner, emulator).
            var button = Holder("StartButton", hanging, On(new Vector3(0.27f, -0.18f, -0.02f)));
            var buttonLook = Holder("ButtonLook", button, Vector3.zero);
            var leaf = Primitive(PrimitiveType.Cube, "StartLeaf", buttonLook, EnsureMaterial("TitleStart", "Ball", startGreen), keepCollider: false);
            leaf.localScale = new Vector3(0.26f, 0.1f, 0.035f); // its depth parks above the near-zero limit too
            leaf.gameObject.AddComponent<BoxCollider>(); // the unit box inside the scaled leaf: its tap area
            Hoverable(leaf.gameObject);
            // Well clear of the leaf's curved face: off-centre, the label turns to face the player and its near
            // edge dipped behind the leaf ("tart" on the emulator).
            Localize(Flat(Label("StartLabel", UiText.Get(UiTerm.Start, UiLanguage.English), buttonLook, new Vector3(0f, 0f, OnButton), 0.04f)), UiTerm.Start);
            // With bare hands, a handprint in a thick green dashed line instead, leaning right like a hand raised for a high five
            // (owner, Fable refinement 2026-11-11)
            var handLook = Holder("HandprintLook", button, new Vector3(0f, -0.004f, 0.015f)); // just in front of the card's face
            handLook.localRotation = Quaternion.Euler(0f, 0f, -15f); // leaning right: raised for a high five
            var printShape = MeshPart("HandprintDashes", handLook, HandprintMesh(), Vector3.zero, EnsureMaterial("HandprintDashes", "Ball", new Color(0.361f, 0.722f, 0.380f))); // #5CB861
            var printBox = printShape.gameObject.AddComponent<BoxCollider>(); // on the visible print: its tap area
            printBox.size = new Vector3(HandprintHalf.x * 2f, HandprintHalf.y * 2f, 0.03f);
            var printWord = Flat(Label("HandprintLabel", UiText.Get(UiTerm.Start, UiLanguage.English), handLook, new Vector3(0.002f, -0.034f, -0.0015f), 0.032f));
            printWord.localRotation = Quaternion.Euler(0f, 0f, 15f); // level on the card, though the print leans
            Localize(printWord, UiTerm.Start);
            var startButton = button.gameObject.AddComponent<TitleStartButton>();
            button.gameObject.AddComponent<HandprintStart>().Configure(buttonLook.gameObject, handLook.gameObject, startButton, HandprintHalf);
            handLook.gameObject.SetActive(false); // controllers until hands are seen

            // Quit, bottom left (George, 2026-11-03: "a main menu with the app's name and a Quit button"): smaller and
            // quieter than Start, mirrored across PICO's window handle. It asks "Sure?" before it closes the app.
            var quit = Holder("TitleQuitButton", hanging, On(new Vector3(-0.3f, -0.19f, -0.02f)));
            var quitLeaf = Primitive(PrimitiveType.Cube, "TitleQuitLeaf", quit, EnsureMaterial("QuitLeaf", "Ball", quitGrey), keepCollider: false);
            quitLeaf.localScale = new Vector3(0.19f, 0.08f, 0.035f);
            quitLeaf.gameObject.AddComponent<BoxCollider>();
            Hoverable(quitLeaf.gameObject);
            Localize(Flat(Label("TitleQuitLabel", UiText.Get(UiTerm.Quit, UiLanguage.English), quit, new Vector3(0f, 0f, OnButton), 0.034f)), UiTerm.Quit);
            quit.gameObject.AddComponent<QuitButton>();

            MeadowArt.TitleSign(card, TitleCardHeight); // a wooden sign with a little vine around the card (V3 review: richer menu)
            // Flutter, Bop and Boom hop along the sign's top rail in time with the title theme (V3 review: "your bouncy
            // happy characters"); children of the card, so they fly into the cloud with it on Start
            var friends = Holder("TitleFriends", card, new Vector3(0f, 0.375f, 0.03f));
            var types = new[] { OrbType.Flutter, OrbType.Bop, OrbType.Boom };
            for (int i = 0; i < types.Length; i++)
            {
                var friend = new GameObject($"TitleFriend_{types[i]}");
                friend.transform.SetParent(friends, false);
                friend.transform.localPosition = new Vector3(-0.32f + 0.32f * i, 0f, 0f);
                friend.transform.localRotation = Quaternion.identity; // faces toward -Z, like the card: toward the player
                friend.transform.localScale = Vector3.one * 0.16f;
                var body = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/Orb_{types[i]}.mat");
                if (CharacterArt.Has(types[i])) CharacterArt.Dress(friend, types[i], body);
                // standing on the top rail, above its leafy vine (owner, headset 2026-11-12: they sank into the post)
                // the body itself (not its outline shell) sits flush on the rail (owner, headset 2026-11-12)
                var bodyBox = friend.transform.Find("Visual").GetComponent<Renderer>().bounds;
                float rail = TitleCardAt.y + TitleRailTop * TitleScale; // the rail's top, in the room
                friend.transform.position += Vector3.up * (rail - bodyBox.min.y);
                float aboveFeet = friend.transform.position.y - rail;
                friend.AddComponent<TitleFriend>().Configure(i * 0.33f, aboveFeet / Mathf.Max(1e-4f, friend.transform.lossyScale.y) * friend.transform.localScale.y);
            }
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

// A steel ring round the rail (the rail runs along X, so the ring lies across it, in the YZ plane).
        static Mesh SteelRingMesh()
        {
            const string path = "Assets/RoomOrchestra/Meshes/SteelRing.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool created = mesh == null;
            if (created) mesh = new Mesh { name = "SteelRing" };
            const int around = 28, tube = 8;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int i = 0; i <= around; i++)
            {
                float u = i * Mathf.PI * 2f / around;
                for (int j = 0; j <= tube; j++)
                {
                    float v = j * Mathf.PI * 2f / tube;
                    float r = TitleRingRadius + TitleRingThickness * Mathf.Cos(v);
                    vertices.Add(new Vector3(TitleRingThickness * Mathf.Sin(v), r * Mathf.Cos(u), r * Mathf.Sin(u)));
                }
            }
            for (int i = 0; i < around; i++)
                for (int j = 0; j < tube; j++)
                {
                    int a = i * (tube + 1) + j, b = a + tube + 1;
                    triangles.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
                }
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            // outward normals: flip the winding if the first one points into the tube
            var n = mesh.normals;
            if (Vector3.Dot(n[0], vertices[0] - new Vector3(0f, TitleRingRadius, 0f)) < 0f)
            {
                for (int k = 0; k < triangles.Count; k += 3) (triangles[k + 1], triangles[k + 2]) = (triangles[k + 2], triangles[k + 1]);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals();
            }
            mesh.RecalculateBounds();
            if (created) AssetDatabase.CreateAsset(mesh, path);
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        // A handprint's outline (palm, four fingers and a short thumb, as a right hand pressed flat leaves it), drawn
        // as a thick dashed line, nothing filled in (owner, Fable refinement 2026-11-11), flat and facing the player;
        // about 15 cm wide and 18 cm tall.
        static Mesh HandprintMesh() => SaveFlatMesh("HandprintDashes", HandprintDashes());

        // The hand as pieces: a palm (ellipse) and five capsules (a centre line and a half width), fingers spread so
        // the outline runs cleanly between them.
        struct HandPiece { public bool Ellipse; public Vector2 A, B; public float Rx, Ry, W; }

        static HandPiece[] HandPieces()
        {
            HandPiece Finger(Vector2 from, float leanDegrees, float length, float halfWidth)
            {
                float a = leanDegrees * Mathf.Deg2Rad;
                return new HandPiece { A = from, B = from + new Vector2(-Mathf.Sin(a), Mathf.Cos(a)) * length, W = halfWidth };
            }
            return new[]
            {
                new HandPiece { Ellipse = true, A = new Vector2(0f, -0.026f), Rx = 0.045f, Ry = 0.047f }, // palm, small
                Finger(new Vector2(-0.04f, 0.0f), 16f, 0.062f, 0.0095f),     // index
                Finger(new Vector2(-0.013f, 0.008f), 5f, 0.072f, 0.0095f),   // middle
                Finger(new Vector2(0.014f, 0.006f), -6f, 0.068f, 0.0095f),   // ring
                Finger(new Vector2(0.039f, -0.004f), -18f, 0.052f, 0.009f),  // little
                Finger(new Vector2(-0.038f, -0.036f), 58f, 0.042f, 0.011f),  // thumb
            };
        }

        // Signed distance to the hand (negative inside): the pieces blended with a small rounding where they meet.
        static float HandDistance(HandPiece[] pieces, Vector2 q)
        {
            const float k = 0.004f;
            float d = float.MaxValue;
            foreach (var p in pieces)
            {
                float di;
                if (p.Ellipse) di = (new Vector2((q.x - p.A.x) / p.Rx, (q.y - p.A.y) / p.Ry).magnitude - 1f) * Mathf.Min(p.Rx, p.Ry);
                else
                {
                    var ab = p.B - p.A;
                    float t = Mathf.Clamp01(Vector2.Dot(q - p.A, ab) / ab.sqrMagnitude);
                    di = Vector2.Distance(q, p.A + ab * t) - p.W;
                }
                if (d == float.MaxValue) { d = di; continue; }
                float h = Mathf.Clamp01(0.5f + 0.5f * (di - d) / k); // smooth minimum
                d = Mathf.Lerp(di, d, h) - k * h * (1f - h);
            }
            return d;
        }

        // The hand's silhouette (marching squares over its distance field), one closed line, cut into even dashes:
        // quads of the line's width.
        static FlatShape HandprintDashes()
        {
            const float size = 1.15f, width = 0.0075f, dashShare = 0.6f, period = 0.02f, cell = 0.0008f;
            var pieces = HandPieces();
            float x0 = -0.12f, y0 = -0.1f, x1 = 0.1f, y1 = 0.1f;
            int nx = Mathf.CeilToInt((x1 - x0) / cell), ny = Mathf.CeilToInt((y1 - y0) / cell);
            var field = new float[nx + 1, ny + 1];
            for (int i = 0; i <= nx; i++)
                for (int j = 0; j <= ny; j++) field[i, j] = HandDistance(pieces, new Vector2(x0 + i * cell, y0 + j * cell));
            Vector2 Cross(int ia, int ja, int ib, int jb)
            {
                float a = field[ia, ja], b = field[ib, jb], t = a / (a - b);
                return Vector2.Lerp(new Vector2(x0 + ia * cell, y0 + ja * cell), new Vector2(x0 + ib * cell, y0 + jb * cell), t);
            }
            // segments, keyed by the cell edge each end lies on, so they chain exactly
            var links = new Dictionary<long, List<long>>();
            var point = new Dictionary<long, Vector2>();
            long Key(int i, int j, int side) => ((long)i * 4096 + j) * 2 + side; // side 0: the edge along x from (i,j); 1: along y
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < ny; j++)
                {
                    var crossings = new List<(long key, Vector2 at)>();
                    if ((field[i, j] < 0f) != (field[i + 1, j] < 0f)) crossings.Add((Key(i, j, 0), Cross(i, j, i + 1, j)));
                    if ((field[i + 1, j] < 0f) != (field[i + 1, j + 1] < 0f)) crossings.Add((Key(i + 1, j, 1), Cross(i + 1, j, i + 1, j + 1)));
                    if ((field[i, j + 1] < 0f) != (field[i + 1, j + 1] < 0f)) crossings.Add((Key(i, j + 1, 0), Cross(i, j + 1, i + 1, j + 1)));
                    if ((field[i, j] < 0f) != (field[i, j + 1] < 0f)) crossings.Add((Key(i, j, 1), Cross(i, j, i, j + 1)));
                    for (int c = 0; c + 1 < crossings.Count; c += 2)
                    {
                        var (a, at) = crossings[c];
                        var (b, bt) = crossings[c + 1];
                        point[a] = at;
                        point[b] = bt;
                        if (!links.TryGetValue(a, out var la)) links[a] = la = new List<long>();
                        if (!links.TryGetValue(b, out var lb)) links[b] = lb = new List<long>();
                        la.Add(b);
                        lb.Add(a);
                    }
                }
            // the longest loop is the hand's outline
            var seen = new HashSet<long>();
            List<Vector2> outline = null;
            foreach (var start in links.Keys)
            {
                if (seen.Contains(start)) continue;
                var loop = new List<Vector2>();
                long k = start, previous = -1;
                while (seen.Add(k))
                {
                    loop.Add(point[k] * size);
                    long step = -1;
                    foreach (var n in links[k]) if (n != previous && !seen.Contains(n)) { step = n; break; }
                    if (step < 0) break;
                    previous = k;
                    k = step;
                }
                if (outline == null || loop.Count > outline.Count) outline = loop;
            }
            var vertices = new List<float>();
            var triangles = new List<int>();
            if (outline == null) return new FlatShape { vertices = vertices.ToArray(), triangles = triangles.ToArray() };
            outline.Add(outline[0]);
            float length = 0f;
            for (int i = 1; i < outline.Count; i++) length += Vector2.Distance(outline[i - 1], outline[i]);
            int count = Mathf.Max(8, Mathf.RoundToInt(length / period));
            float even = length / count; // a whole number of dashes round the hand: no short one where it closes
            var dashPoints = new List<Vector2>();
            void Flush()
            {
                if (dashPoints.Count >= 2)
                    for (int i = 0; i < dashPoints.Count; i++)
                    {
                        var tangent = (dashPoints[Mathf.Min(i + 1, dashPoints.Count - 1)] - dashPoints[Mathf.Max(i - 1, 0)]).normalized;
                        var normal = new Vector2(-tangent.y, tangent.x) * (width / 2f);
                        int v = vertices.Count / 2;
                        vertices.Add(dashPoints[i].x + normal.x); vertices.Add(dashPoints[i].y + normal.y);
                        vertices.Add(dashPoints[i].x - normal.x); vertices.Add(dashPoints[i].y - normal.y);
                        if (i > 0) triangles.AddRange(new[] { v - 2, v, v - 1, v - 1, v, v + 1 });
                    }
                dashPoints.Clear();
            }
            float along = 0f;
            for (int i = 0; i < outline.Count; i++)
            {
                if (i > 0) along += Vector2.Distance(outline[i - 1], outline[i]);
                if ((along % even) < even * dashShare) dashPoints.Add(outline[i]); else Flush();
            }
            Flush();
            return new FlatShape { vertices = vertices.ToArray(), triangles = triangles.ToArray() };
        }

// PICO's hand material (its fresnel shader: one colour facing the eye, another at the edge), copied into the project
        // and coloured as an outline: nearly clear inside, solid mint at the edge.
        static Material PlayerHandLook(Material source)
        {
            string path = $"{MaterialDir}/PlayerHand.mat";
            var look = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (look == null)
            {
                look = new Material(source);
                AssetDatabase.CreateAsset(look, path);
            }
            look.shader = source.shader;
            look.CopyPropertiesFromMaterial(source);
            if (look.HasProperty("_InnerColor")) look.SetColor("_InnerColor", new Color(0.804f, 0.922f, 0.827f, 0.3f));
            if (look.HasProperty("_OutColor")) look.SetColor("_OutColor", new Color(0.804f, 0.922f, 0.827f, 1f));
            if (look.HasProperty("_FresnelPower")) look.SetFloat("_FresnelPower", 1.2f);
            if (look.HasProperty("_MainAlpha")) look.SetFloat("_MainAlpha", 1f);
            if (look.HasProperty("_PressIntensity")) look.SetFloat("_PressIntensity", 0f); // no pinch glow
            EditorUtility.SetDirty(look);
            return look;
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
            UiIcon("Undo", go.transform, mark); // a curved arrow, no square corner (V4 review)
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
                UiIcon("Pause", pauseIcon, mark); // rounded bars (V4 review: bevel the pause button's edges)
                playIcon = Holder("PlayIcon", go.transform, MusicControlButton.DefaultIconParked);
                UiIcon("Play", playIcon, mark);
            }
            else
            {
                UiIcon("Clear", go.transform, mark);
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
            UiIcon("Recenter", go.transform, mark);
            Localize(Label("RecenterLabel", "Recenter", go.transform, SmallLabelOffset, SmallLabelHeight), UiTerm.Recenter);
            go.AddComponent<RecenterButton>();
        }

        // Height (Seated / Standing), at the end of the front row: an up-down arrow, and a label with the setting.
        static void CreateHeightButton(Vector3 position, Transform root, Material slab, Material mark)
        {
            var go = SmallButton("HeightButton", position, root, slab, "HeightSlab");
            UiIcon("Height", go.transform, mark);
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
            var slabPart = ToySlab(go.transform, "ViewSlab", new Vector3(0.1f, 0.03f, 0.08f));
            TapBox(slabPart, new Vector3(0f, 0.04f, 0f), new Vector3(0.1f, 0.12f, 0.09f));
            // An eye: a pale lens with a dark pupil.
            UiIcon("View", go.transform, mark, pale: EnsureMaterial("ViewEyeWhite", "Ball", new Color(0.95f, 0.95f, 0.92f)));
            var label = Label("ViewLabel", ViewButton.TextFor(ViewModes.Default, UiLanguage.English), go.transform, new Vector3(0f, 0.004f, -0.075f), 0.034f);
            go.AddComponent<ViewButton>().Configure(label.GetComponentInChildren<TMP_Text>());
        }

        // The back row, right of Loudness: Sounds, the next instrument pack (its label names the pack in use).
        static void CreateSoundsButton(Transform root, Material slab, Material mark)
        {
            var go = new GameObject("SoundsButton");
            go.transform.SetParent(root, false);
            go.transform.position = TrayCenter + new Vector3(0.515f, 0f, 0f);
            go.transform.rotation = FacePlayer(go.transform.position);
            var slabPart = ToySlab(go.transform, "SoundsSlab", new Vector3(0.1f, 0.03f, 0.08f));
            TapBox(slabPart, new Vector3(0f, 0.04f, 0f), new Vector3(0.1f, 0.12f, 0.09f));
            // A little speaker with two sound waves (unlike Loudness's bars beside it).
            UiIcon("Sounds", go.transform, mark);
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
                // the V4 library: the same sounds in every pack, each matched to the first three's loudness
                pack.ocarina = SoundSets.Clips(SoundSets.LibraryDir, "Ocarina");
                pack.handpan = SoundSets.Clips(SoundSets.LibraryDir, "Handpan");
                pack.guzheng = SoundSets.Clips(SoundSets.LibraryDir, "Guzheng");
                pack.balafon = SoundSets.Clips(SoundSets.LibraryDir, "Balafon");
                pack.slitDrum = SoundSets.Clips(SoundSets.LibraryDir, "SlitDrum");
                pack.volumes = ((InstrumentType[])System.Enum.GetValues(typeof(InstrumentType)))
                    .Select(t => System.Array.IndexOf(Instruments.Library, t) >= 0 ? SoundSets.LibraryVolume(t.ToString()) : SoundSets.VolumeFor(dir, t.ToString())).ToArray();
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
            UiIcon("Lesson", go.transform, mark);
            Localize(Label("LessonLabel", UiText.Get(UiTerm.Lesson, UiLanguage.English), go.transform, SmallLabelOffset, SmallLabelHeight), UiTerm.Lesson);
            go.AddComponent<TutorialReplayButton>();
        }

        // The extras wait under the leaf during the guided song, so it starts with one thing to try (George,
        // 2026-11-03); saved tucked, like the stations, so nothing moves at startup when the lesson runs.
        // Since the menu (2026-11-08) the extras wait folded on its tray; Clear still waits under the leaf.
        static readonly string[] LessonTucked = { "ClearButton" };

        static void TuckForLesson(Transform leafRoot)
        {
            // The hiding place under the leaf is measured with the leaf at home: by now the opening has posed it out
            // of sight, and a hiding place measured there left the tucked buttons on the floor in front of the player
            // (owner, headset 2026-11-08: "a bunch of keys from the leaf still shows up on the floor").
            var posed = (leafRoot.localPosition, leafRoot.localRotation);
            leafRoot.localPosition = Vector3.zero;
            leafRoot.localRotation = Quaternion.identity;
            TuckAll(leafRoot);
            leafRoot.localPosition = posed.localPosition;
            leafRoot.localRotation = posed.localRotation;
        }

        static void TuckAll(Transform leafRoot)
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
            var cream = EnsureMaterial("SpeechBubbleRim", "Ball", new Color(0.949f, 0.910f, 0.847f));
            var plum = EnsureMaterial("SpeechBubble", "Ball", new Color(0.165f, 0.122f, 0.239f));
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

        // PICO XR mode (owner, 2026-11-08): the headset rig at the scene's origin (tracking space = scene space, where
        // the room scan arrives). Its eye camera clears to transparent black so video seethrough shows the real room,
        // without HDR or post-processing (PICO: either stops seethrough on Vulkan), and is saved switched off: XrRig
        // turns it and its ears on only on the device. Each controller gets a pointer ray and a dot.
        static void CreateXrRig()
        {
            foreach (var old in Object.FindObjectsByType<XrRig>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(old.gameObject);
            var rig = new GameObject("XR Origin");
            rig.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var eyes = new GameObject("Main Camera");
            eyes.tag = "MainCamera";
            eyes.transform.SetParent(rig.transform, false);
            eyes.transform.localPosition = Viewer.SeatedEye;
            var cam = eyes.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 40f; // the leaf waiting 80 m up before Start is never drawn
            cam.allowHDR = false;
            cam.stereoTargetEye = StereoTargetEyeMask.Both;
            var urp = eyes.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            urp.renderPostProcessing = false;
            cam.enabled = false;
            var ears = eyes.AddComponent<AudioListener>();
            ears.enabled = false;
            var xr = rig.AddComponent<XrRig>();
            xr.Configure(cam);
            rig.AddComponent<EnvironmentOcclusion>().Configure(DepthOccluder()); // PICO's depth hides what is behind real things

            var idle = Glow(EnsureMaterial("PointerRay", "Ball", new Color(0.85f, 0.92f, 1f)), new Color(0.85f, 0.92f, 1f));
            var hot = Glow(EnsureMaterial("PointerRayHot", "Ball", new Color(1f, 0.8f, 0.25f)), new Color(1f, 0.8f, 0.25f) * 1.4f);
            var rays = new LineRenderer[2];
            var dots = new Transform[2];
            for (int hand = 0; hand < 2; hand++)
            {
                var pointer = new GameObject(hand == 0 ? "PointerLeft" : "PointerRight");
                pointer.transform.SetParent(rig.transform, false);
                var line = pointer.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.positionCount = 2;
                line.widthCurve = AnimationCurve.Linear(0f, 0.004f, 1f, 0.0015f);
                line.sharedMaterial = idle;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.enabled = false;
                rays[hand] = line;
                var dot = Primitive(PrimitiveType.Sphere, "PointerDot", pointer.transform, idle, keepCollider: false);
                dot.localScale = Vector3.one * 0.02f;
                dot.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                dot.gameObject.SetActive(false);
                dots[hand] = dot;
            }
            // The player's own hands while the meadow hides the real ones (owner, headset 2026-11-15): PICO's hand models
            // (the SDK's HandLeft/HandRight prefabs follow every joint), without their pinch ray (the game points with the
            // eyes), in an outline look: solid mint at the hand's edge, faint inside.
            var hands = new GameObject("PlayerHands");
            hands.transform.SetParent(rig.transform, false);
            var models = new GameObject[2];
            for (int hand = 0; hand < 2; hand++)
            {
                string side = hand == 0 ? "Left" : "Right";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Packages/com.bytedance.pico.xr/Resources/Prefabs/Hand{side}.prefab");
                if (prefab == null) { Debug.LogError($"[RoomOrchestra] PICO's Hand{side} prefab not found"); continue; }
                var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, hands.transform);
                PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                model.name = $"Hand{side}";
                var follow = model.GetComponent("PXR_Hand");
                var so = new SerializedObject(follow);
                so.FindProperty("rayPose").objectReferenceValue = null;    // no ray: PXR_Hand leaves it alone when unset
                so.FindProperty("defaultRay").objectReferenceValue = null;
                so.ApplyModifiedPropertiesWithoutUndo();
                foreach (var name in new[] { "RayPose", "DefaultRay" })
                {
                    var ray = model.transform.Find(name);
                    if (ray != null) Object.DestroyImmediate(ray.gameObject);
                }
                foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    skin.sharedMaterial = PlayerHandLook(skin.sharedMaterial);
                    skin.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    skin.receiveShadows = false;
                    skin.updateWhenOffscreen = true; // its bounds follow the tracked joints
                }
                model.SetActive(false); // shown when its hand is tracked (HandOutline)
                models[hand] = model;
            }
            hands.AddComponent<HandOutline>().Configure(models[0], models[1]);

            var router = Object.FindFirstObjectByType<InstrumentInputRouter>();
            if (router != null)
            {
                router.ConfigurePointers(rays, dots, idle, hot);
                EditorUtility.SetDirty(router);
            }
            EditorUtility.SetDirty(xr);
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
        // ---- The menu (owner, 2026-11-08: "I think there might be too many buttons on the leaf") ----
        // The leaf keeps the characters, Menu, Undo, Pause and Clear; the rest wait on a tray Menu opens just above the
        // leaf, tilted toward the player, far row then near row.
        static readonly string[][] MenuRows =
        {
            new[] { "TogetherButton", "SoundsButton", "ViewButton", "LanguageButton", "HeightButton", "RealHidesButton" },
            new[] { "MenuCloseButton", "RecenterButton", "LessonButton", "StartOverButton", "QuitButton" },
        };
        // Flat over the leaf, like the leaf's own buttons (owner, headset 2026-11-08: the tilted tray was hard to read and
        // covered the Menu button): its own Close sits where Menu is.
        public const float MenuStep = 0.125f, MenuRowGap = 0.2f, MenuLift = 0.13f, MenuTilt = 0f, MenuFarStep = 0.08f;
        static readonly Vector3 MenuPlate = new(0.92f, 0.02f, 0.44f);

        static MenuTray CreateMenuTray(Transform leafRoot, Material mark)
        {
            var tray = Holder("MenuTray", leafRoot, Vector3.zero);
            tray.position = new Vector3(LeafCenter.x, ButtonY + MenuLift, LeafCenter.z + 0.04f);
            tray.localRotation = Quaternion.Euler(-MenuTilt, 0f, 0f);
            CreateMenuButton(LeafRow(1), leafRoot, AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/Pedestal.mat"), mark, "MenuCloseButton", UiTerm.MenuClose);
            var plum = EnsureMaterial("MenuPlate", "Ball", // navy #1E2A47 (owner, Fable refinement 2026-11-11)
                new Color(0.118f, 0.165f, 0.278f));
            // two level steps, the far row higher (like stadium seats) so a pointer from the lap clears the near row
            Box("MenuPlate", tray, new Vector3(0f, -0.016f, -MenuRowGap / 2f + 0.01f), new Vector3(MenuPlate.x, MenuPlate.y, MenuPlate.z / 2f + 0.02f), plum, keepCollider: false);
            Box("MenuPlateBack", tray, new Vector3(0f, MenuFarStep - 0.016f, MenuRowGap / 2f + 0.01f), new Vector3(MenuPlate.x, MenuPlate.y, MenuPlate.z / 2f), plum, keepCollider: false);
            for (int row = 0; row < MenuRows.Length; row++)
            {
                var names = MenuRows[row];
                for (int i = 0; i < names.Length; i++)
                {
                    var button = leafRoot.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == names[i] && t.parent != tray);
                    if (button == null)
                    {
                        Debug.LogWarning($"[RoomOrchestra] Menu: no {names[i]} to put on the tray");
                        continue;
                    }
                    button.SetParent(tray, false);
                    button.localPosition = new Vector3(MenuStep * (i - (names.Length - 1) / 2f), row == 0 ? MenuFarStep : 0f, row == 0 ? MenuRowGap / 2f : -MenuRowGap / 2f);
                    button.localRotation = Quaternion.identity;
                    if (row == 0) // the far step: two-line labels rise clear of its edge
                        foreach (Transform part in button)
                            if (part.name.EndsWith("Label") && part.name != "LanguageLabel") part.localPosition += Vector3.up * 0.03f; // 中文 already sits on top
                    if (row == 1) // the near row's two-line labels: their second line clear of the plate's front edge
                        foreach (Transform part in button)
                            if (part.name == "PlacesLabel" || part.name == "StartOverLabel") part.localPosition += Vector3.up * 0.022f;
                }
            }
            return tray.gameObject.AddComponent<MenuTray>();
        }

        static void CreateMenuButton(Vector3 position, Transform root, Material slab, Material mark, string name = "MenuButton", UiTerm word = UiTerm.Menu)
        {
            var go = SmallButton(name, position, root, slab, "MenuSlab");
            UiIcon(word == UiTerm.MenuClose ? "Close" : "Menu", go.transform, mark); // three bars; Close: a chevron down
            Localize(Label("MenuLabel", UiText.Get(word, UiLanguage.English), go.transform, SmallLabelOffset, SmallLabelHeight * 1.25f), word); // "Menu" has no tall letters: a little bigger to read
            go.AddComponent<MenuButton>();
        }

        static void CreatePlacesButton(Vector3 position, Transform root, Material slab, Material mark)
        {
            var go = SmallButton("PlacesButton", position, root, slab, "PlacesSlab");
            UiIcon("Places", go.transform, mark);
            Localize(Label("PlacesLabel", UiText.Get(UiTerm.Places, UiLanguage.English), go.transform, SmallLabelOffset, SmallLabelHeight * 2f), UiTerm.Places);
            go.AddComponent<PlacesButton>();
        }

        static void CreateRealHidesButton(Vector3 position, Transform root, Material slab, Material mark)
        {
            var go = SmallButton("RealHidesButton", position, root, slab, "RealHidesSlab");
            UiIcon("RealHides", go.transform, mark, slab: slab); // a ball half behind a wall
            var label = Label("RealHidesLabel", UiText.Get(UiTerm.RealHidesOn, UiLanguage.English), go.transform, SmallLabelOffset, SmallLabelHeight * 2f);
            Localize(label, UiTerm.RealHidesOn);
            go.AddComponent<RealHidesButton>();
        }

        // Menu: start completely over, the room scan included (owner, headset 2026-11-08, for a demo). A curved arrow back.
        static void CreateStartOverButton(Vector3 position, Transform root, Material slab, Material mark)
        {
            var go = SmallButton("StartOverButton", position, root, slab, "StartOverSlab");
            UiIcon("StartOver", go.transform, mark);
            Localize(Label("StartOverLabel", UiText.Get(UiTerm.StartOver, UiLanguage.English), go.transform, SmallLabelOffset, SmallLabelHeight * 2f), UiTerm.StartOver);
            go.AddComponent<StartOverButton>();
        }

        // Menu: Play together (owner, 2026-11-22: "an easier way to access than the easter egg"). Two people, side by side.
        static void CreateTogetherButton(Vector3 position, Transform root, Material slab, Material mark)
        {
            var go = SmallButton("TogetherButton", position, root, slab, "TogetherSlab");
            UiIcon("Together", go.transform, mark);
            Localize(Label("TogetherLabel", UiText.Get(UiTerm.Together, UiLanguage.English), go.transform, SmallLabelOffset, SmallLabelHeight * 2f), UiTerm.Together);
            go.AddComponent<TogetherButton>();
        }

        static void CreateQuitButton(Vector3 position, Transform root, Material slab, Material mark)
        {
            var go = SmallButton("QuitButton", position, root, slab, "QuitSlab");
            UiIcon("Quit", go.transform, mark, slab: slab); // a door ajar, toward the player
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
            // the bright leaf green under navy menu plates (owner, Fable refinement 2026-11-11: option 1), kept for the
            // fallback; the modelled leaf (owner, 2026-11-19) carries its greens and veins in its vertex colours
            var leafMaterial = EnsureMaterial("LeafPad", "Ball", new Color(0.42f, 0.74f, 0.4f));
            var leafMesh = LeafPadMesh("Leaf");
            var stalkMesh = LeafPadMesh("Stalk");
            var baked = MeadowArt.EnsureMaterial("LeafPadBaked", "RoomOrchestra/LitBakedAO", Color.white);
            if (baked.HasProperty("_Smoothness")) baked.SetFloat("_Smoothness", 0.3f);
            Transform leaf;
            if (leafMesh != null)
            {
                // a real leaf: a pointed tip, a rim that curls up, raised veins, flat where the buttons stand
                // (ArtSource/Leaf/build_leaf.py; its mesh is in metres round the leaf's centre)
                leaf = new GameObject("Leaf").transform;
                leaf.SetParent(leafHolder, false);
                leaf.gameObject.AddComponent<MeshFilter>().sharedMesh = leafMesh;
                leaf.gameObject.AddComponent<MeshRenderer>().sharedMaterial = baked;
                leaf.position = LeafCenter;
            }
            else
            {
                leaf = Primitive(PrimitiveType.Sphere, "Leaf", leafHolder, leafMaterial, keepCollider: false);
                leaf.position = LeafCenter;
                leaf.localScale = LeafSize;
                // Over the thick middle only: the leaf thins to nothing at its tips.
                var rib = Box("LeafRib", leafHolder, Vector3.zero, new Vector3(LeafSize.x * 0.75f, 0.004f, 0.008f), vein, keepCollider: false);
                rib.position = new Vector3(LeafCenter.x, LeafCenter.y + LeafSize.y / 2f - 0.001f, (TrayCenter.z + ButtonRowZ) / 2f);
            }
            // The stalk stands on the floor in a little hole (owner, 2026-10-29); in the opening the hole opens
            // and the stalk grows up to meet the swooping leaf. Saved closed: the opening's first frame.
            var stem = Holder("LeafStem", pad, Vector3.zero);
            var stalk = Box("LeafStalk", stem, Vector3.zero, Vector3.one, leafMaterial, keepCollider: false);
            if (stalkMesh != null)
            {
                // a tapered stalk that leans and bends a little, with a flared foot (in the cube's unit box)
                stalk.GetComponent<MeshFilter>().sharedMesh = stalkMesh;
                stalk.GetComponent<MeshRenderer>().sharedMaterial = baked;
            }
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
            leafStalk.Configure(stalk, hole, LeafCenter - new Vector3(0f, LeafSize.y / 2f, 0f), stalkMesh != null ? LeafStalkWidth : 0.03f, startGrown: 0f, startOpen: 0f, cloudCore);
            EditorUtility.SetDirty(leafStalk);
            return leafStalk;
        }

        public const string LeafPadFbx = "Assets/RoomOrchestra/Art/Leaf/LeafPad.fbx";
        // The modelled stalk's box: its tube is about 40% of it (2.4 cm at the foot of a grown stalk), its bend inside it.
        public const float LeafStalkWidth = 0.06f;

        static Mesh LeafPadMesh(string name)
        {
            if (AssetImporter.GetAtPath(LeafPadFbx) is ModelImporter importer && (importer.materialImportMode != ModelImporterMaterialImportMode.None || importer.importAnimation))
            {
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.importAnimation = false;
                importer.importCameras = false;
                importer.importLights = false;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAllAssetsAtPath(LeafPadFbx).OfType<Mesh>().FirstOrDefault(m => m.name == name);
        }

        // One small button on the leaf's front row: a slab facing the player and a tap box a little larger.
        static GameObject SmallButton(string name, Vector3 position, Transform root, Material slab, string slabName)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.position = position;
            go.transform.rotation = FacePlayer(position);
            var slabPart = ToySlab(go.transform, slabName, SmallSlab);
            TapBox(slabPart, new Vector3(0f, 0.03f, 0f), SmallCollider);
            return go;
        }

        // A toy button (owner, 2026-11-19: the Astro Bot direction, option A): a soft plum cap standing in a cream ring
        // that it sinks into when pressed (ToyPress). The cap is the old slab's height and place, a little narrower, so
        // its icon, label and tap box stay where they were; the ring is the old slab's width, round the cap's foot.
        static Material ToyCap() => EnsureMaterial("ToyCap", "Ball", new Color(0.227f, 0.176f, 0.333f));   // #3A2D55
        static Material ToyRing() => EnsureMaterial("ToyRing", "Ball", new Color(0.949f, 0.910f, 0.847f)); // #F2E8D8, the title card's cream

        static Transform ToySlab(Transform button, string slabName, Vector3 size)
        {
            var cap = Box(slabName, button, Vector3.zero, new Vector3(size.x - 0.014f, size.y, size.z - 0.014f), ToyCap(), keepCollider: false);
            Box(slabName.Replace("Slab", "Ring"), button, new Vector3(0f, -size.y / 2f + 0.008f, 0f), new Vector3(size.x, 0.016f, size.z), ToyRing(), keepCollider: false);
            button.gameObject.AddComponent<ToyPress>().Configure(cap);
            return cap;
        }

        // PICO's hover highlight (Version 1 feedback: nothing showed what you were pointing at) appears on an
        // object that carries both the tap collider and a visible mesh, so each tap volume sits on a visible
        // part. Taps still reach the control: input looks the owner up from the hit collider's parents.
        // PICO XR mode: what can be tapped needs only its collider; the controller's ray turns gold over it
        // (InstrumentInputRouter). PICO Spatial's per-object hover highlight went with that mode.
        static void Hoverable(GameObject visible) { }

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
            UiIcon("Call", go.transform, EnsureMaterial("CallLeaf", "Ball", new Color(0.36f, 0.72f, 0.38f)), vein: vein);
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
            var bodies = new List<Renderer>();
            var faces = new List<Renderer>();
            for (int i = 0; i < tiles.Length; i++)
            {
                var tile = Primitive(PrimitiveType.Cube, $"PitchTile{i}", go.transform, TileBody(target.Instrument), keepCollider: true);
                Hoverable(tile.gameObject);
                tile.position = new Vector3(go.transform.position.x, ColumnBottom + ColumnStep * i, go.transform.position.z);
                tile.rotation = FacePlayer(tile.position);
                tile.localScale = focused && i == StartPetal ? TileScale * 1.2f : TileScale;
                faces.Add(TileFace(tile, TileScale));
                bodies.Add(tile.GetComponent<Renderer>());
                var note = new MusicalNote(target.Instrument, i);
                string text = UiText.Tile(note, UiLanguage.English);
                // Label on the tile's player-facing side, sized in world units (not stretched by the tile).
                // Sized for a note name; any word (a drum sound, Chinese) shrinks to fit the tile, since the furniture
                // can play any instrument.
                var label = Label($"TileLabel{i}", "D", go.transform, Vector3.zero, 0.045f);
                label.position = tile.position - tile.forward * 0.026f; // in front of the face, even on the bigger glowing tile
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
            var mixTile = Primitive(PrimitiveType.Cube, "MixTile", go.transform, TileBody(target.Instrument), keepCollider: true);
            Hoverable(mixTile.gameObject);
            mixTile.position = new Vector3(go.transform.position.x, ColumnBottom - 0.14f, go.transform.position.z);
            mixTile.rotation = FacePlayer(mixTile.position);
            mixTile.localScale = new Vector3(0.18f, 0.11f, 0.03f); // room for what the instrument is, under its name
            faces.Add(TileFace(mixTile, mixTile.localScale));
            bodies.Add(mixTile.GetComponent<Renderer>());
            var caption = Label("InstrumentLabel", MixButton.TextFor(target.Instrument, 0, UiLanguage.English), go.transform, Vector3.zero, 0.09f); // name, what it is (small), volume
            caption.position = mixTile.position - mixTile.forward * 0.02f;
            caption.rotation = mixTile.rotation;
            var mix = mixTile.gameObject.AddComponent<MixButton>();
            mix.Configure(target, caption.GetComponentInChildren<TMP_Text>());
            // a small picture of the instrument beside its name (owner, 2026-11-11: names people may never have heard)
            var icon = InstrumentIcon.Create(go.transform, "InstrumentIcon", 0.9f);
            icon.transform.position = mixTile.position - mixTile.right * 0.15f;
            icon.transform.rotation = mixTile.rotation;
            icon.GetComponent<MeshFilter>().sharedMesh = InstrumentIconMesh(target.Instrument);
            icon.GetComponent<MeshRenderer>().sharedMaterial = MeadowArt.Lit();
            icon.Configure(target);
            EditorUtility.SetDirty(icon);
            EditorUtility.SetDirty(mix);

            // Under the name: Change gives this furniture the next instrument (InstrumentPacks).
            var changeTile = Primitive(PrimitiveType.Cube, "ChangeTile", go.transform, ToyCap(), keepCollider: true); // plum, like the leaf's buttons
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
            var tint = go.AddComponent<InstrumentTint>();
            var types = System.Enum.GetValues(typeof(InstrumentType)).Cast<InstrumentType>().ToArray();
            tint.Configure(target, bodies.ToArray(), faces.ToArray(), types.Select(TileBody).ToArray(), types.Select(TileFaceMaterial).ToArray());
            EditorUtility.SetDirty(tint);
            return column;
        }

        // The note tiles are rounded keycaps tinted by their instrument (owner, 2026-11-19): a darker body and a lighter,
        // glossy face; the tint follows the instrument when Change swaps it (InstrumentTint). White letters keep at least
        // 5:1 contrast on every face (TileTintTests), so the words, not the colour, say what a tile plays.
        public static readonly Dictionary<InstrumentType, Color32> TileTints = new()
        {
            { InstrumentType.Udu, new Color32(0xA0, 0x48, 0x2E, 255) },      // fired clay
            { InstrumentType.Mbira, new Color32(0x8C, 0x5A, 0x14, 255) },    // amber wood
            { InstrumentType.Erhu, new Color32(0x8E, 0x2A, 0x3F, 255) },     // red lacquer
            { InstrumentType.Ocarina, new Color32(0x2F, 0x64, 0x96, 255) },  // glazed blue
            { InstrumentType.Handpan, new Color32(0x1F, 0x6E, 0x6A, 255) },  // steel teal
            { InstrumentType.Guzheng, new Color32(0x6A, 0x3A, 0x7A, 255) },  // plum silk
            { InstrumentType.Balafon, new Color32(0x5A, 0x6A, 0x1E, 255) },  // olive gourd
            { InstrumentType.SlitDrum, new Color32(0x5E, 0x3B, 0x22, 255) }, // dark log
        };
        public const float TileFaceLight = 1.1f, TileBodyDark = 0.62f;

        static Color Shade(InstrumentType type, float k)
        {
            Color c = TileTints[type];
            return new Color(Mathf.Min(1f, c.r * k), Mathf.Min(1f, c.g * k), Mathf.Min(1f, c.b * k));
        }

        static Material TileBody(InstrumentType type) => EnsureMaterial($"TileBody_{type}", "Ball", Shade(type, TileBodyDark));

        static Material TileFaceMaterial(InstrumentType type)
        {
            var m = EnsureMaterial($"TileFace_{type}", "Ball", Shade(type, TileFaceLight));
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.6f); // glossy, like a sweet
            return m;
        }

        // The face: a pillowy panel on the tile's player side, slightly smaller than the tile (rounded with the controls).
        static Renderer TileFace(Transform tile, Vector3 size)
        {
            var face = Primitive(PrimitiveType.Cube, "TileFace", tile, null, keepCollider: false);
            face.localPosition = new Vector3(0f, 0f, -0.3f);
            face.localRotation = Quaternion.identity;
            face.localScale = new Vector3(1f - 0.012f / size.x, 1f - 0.012f / size.y, 0.55f);
            var type = tile.parent.name.StartsWith("PitchColumn_") && System.Enum.TryParse<InstrumentType>(tile.parent.name.Substring("PitchColumn_".Length), out var t) ? t : InstrumentType.Udu;
            var r = face.GetComponent<Renderer>();
            r.sharedMaterial = TileFaceMaterial(type);
            return r;
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

        // The room-scan progress bar (owner, headset 2026-11-12): a dark track, a green fill, the percentage above and
        // a hint below. It places itself in front of the player while the room is scanned.
        static void CreateScanProgressBar(Transform root)
        {
            var bar = Holder("ScanProgress", root, new Vector3(0f, 0.15f, -1.3f)); // saved low behind the seat, hidden; it places itself
            bar.rotation = FacePlayer(bar.position);
            var plate = EnsureMaterial("ScanBarPlate", "Ball", new Color(0.16f, 0.09f, 0.22f));
            var track = EnsureMaterial("ScanBarTrack", "Ball", new Color(0.3f, 0.24f, 0.36f));
            var fillLook = Glow(EnsureMaterial("ScanBarFill", "Ball", new Color(0.45f, 0.86f, 0.5f)), new Color(0.2f, 0.45f, 0.22f));
            Box("ScanPlate", bar, new Vector3(0f, 0f, 0.012f), new Vector3(0.5f, 0.2f, 0.015f), plate, keepCollider: false);
            var meter = Holder("ScanMeter", bar, new Vector3(0f, -0.005f, 0f));
            meter.localRotation = Quaternion.identity;
            meter.localScale = new Vector3(ScanProgressBar.BarWidth, 0.035f, 0.012f);
            Box("ScanTrack", meter, Vector3.zero, Vector3.one, track, keepCollider: false);
            var fill = Box("ScanFill", meter, new Vector3(0f, 0f, -0.25f), new Vector3(1f, 1.05f, 1f), fillLook, keepCollider: false); // saved full; it shows the progress
            var percent = Label("ScanLabel", "Scanning your room: 0%", bar, new Vector3(0f, 0.055f, -0.006f), 0.032f);
            percent.localRotation = Quaternion.identity;
            var help = Label("ScanHint", "Look all around: walls, floor, furniture", bar, new Vector3(0f, -0.06f, -0.006f), 0.022f);
            help.localRotation = Quaternion.identity;
            foreach (var r in bar.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var view = bar.gameObject.AddComponent<ScanProgressBar>();
            view.Configure(fill, percent.GetComponentInChildren<TMP_Text>(), help.GetComponentInChildren<TMP_Text>());
            EditorUtility.SetDirty(view);
        }

        const string InstrumentIconsFbx = "Assets/RoomOrchestra/Art/Icons/InstrumentIcons.fbx";
        const string UiIconsFbx = "Assets/RoomOrchestra/Art/Icons/UiIcons.fbx";

        /// <summary>A button's icon, modelled in Blender (ArtSource/Icons/build_ui_icons.py; owner's choice "A", soft
        /// bevel, each part one continuous solid): the mesh UiIcon_<key> in the mark's colour, and its other parts
        /// (_Wall, _Door: the slab's; _White: the pale; _Vein: the vein's). Built in the button's own space, so it sits
        /// at the holder's origin.</summary>
        static Material IconGold() => EnsureMaterial("IconGold", "Ball", new Color(0.949f, 0.784f, 0.227f)); // #F2C83A

        static void UiIcon(string key, Transform parent, Material mark, Material slab = null, Material pale = null, Material vein = null)
        {
            if (AssetImporter.GetAtPath(UiIconsFbx) is ModelImporter importer && (importer.materialImportMode != ModelImporterMaterialImportMode.None || importer.importNormals != ModelImporterNormals.Import))
            {
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.importNormals = ModelImporterNormals.Import;
                importer.importAnimation = false;
                importer.SaveAndReimport();
            }
            if (mark != null && mark.name == "OrbMotif") mark = IconGold(); // gold on the dark slab, as approved (owner, 2026-11-17)
            var meshes = AssetDatabase.LoadAllAssetsAtPath(UiIconsFbx).OfType<Mesh>().Where(m => m.name == $"UiIcon_{key}" || m.name.StartsWith($"UiIcon_{key}_")).ToList();
            if (meshes.Count == 0) Debug.LogWarning($"[RoomOrchestra] no icon UiIcon_{key} in {UiIconsFbx}");
            foreach (var mesh in meshes)
            {
                string part = mesh.name.Length > $"UiIcon_{key}".Length ? mesh.name.Substring($"UiIcon_{key}_".Length) : "";
                var go = new GameObject($"{key}Icon{(part == "" ? "" : "_" + part)}");
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = part switch { "Wall" or "Door" => slab ?? mark, "White" => pale ?? mark, "Vein" => vein ?? mark, _ => mark };
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        static Mesh InstrumentIconMesh(InstrumentType type)
        {
            if (AssetImporter.GetAtPath(InstrumentIconsFbx) is ModelImporter importer && importer.materialImportMode != ModelImporterMaterialImportMode.None)
            {
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.importAnimation = false;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAllAssetsAtPath(InstrumentIconsFbx).OfType<Mesh>().FirstOrDefault(m => m.name == $"Icon_{type}");
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
            // what waits inside the cloud is not drawn (PICO XR mode: bits poked out of it, owner 2026-11-08)
            var hider = dispenser.GetComponent<CloudHider>() ?? dispenser.gameObject.AddComponent<CloudHider>();
            hider.Configure(park, new[] { core, puffs });
            EditorUtility.SetDirty(hider);
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
