using System.Linq;
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
        static readonly Vector3 TrayCenter = new Vector3(-0.55f, 0.8f, 1.1f);

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
            }

            var tierButton = CreateTierButton(root.transform, slabMaterial, motifMaterial);
            var petalButton = CreatePetalButton(root.transform, slabMaterial, motifMaterial);

            // Targets left to right; one focus marker floats above the focused one
            var targets = Object.FindObjectsByType<InstrumentTarget>(FindObjectsSortMode.None)
                .OrderBy(t => t.AimPosition.x).ToArray();
            const float markerLift = 0.3f;
            var marker = Primitive(PrimitiveType.Cylinder, "FocusMarker", root.transform, focusMaterial, keepCollider: false);
            marker.position = targets[StartTarget].AimPosition + Vector3.up * markerLift;
            marker.localScale = new Vector3(0.14f, 0.006f, 0.14f);

            var launcher = Object.FindFirstObjectByType<BallLauncher>();
            PreSkinBalls(orbMaterials[(int)OrbType.Flutter]);

            // Echo orbits: one clock-face ring per target, all driven by one conductor.
            var tickMaterial = EnsureMaterial("OrbitTick", "Ball", new Color(0.85f, 0.87f, 0.9f));
            var orbits = targets.Select(t => CreateOrbit(t, root.transform, orbMaterials, tickMaterial, focusMaterial, motifMaterial)).ToArray();
            var conductor = root.AddComponent<EchoConductor>();
            conductor.Configure(orbits);
            EditorUtility.SetDirty(conductor);
            CreateUndoButton(root.transform, slabMaterial, motifMaterial);

            var selector = root.AddComponent<OrbSelector>();
            var so = new SerializedObject(selector);
            SetArray(so.FindProperty("targets"), targets);
            so.FindProperty("focusMarker").objectReferenceValue = marker;
            so.FindProperty("focusMarkerLift").floatValue = markerLift;
            SetArray(so.FindProperty("tokens"), tokens);
            so.FindProperty("tierButton").objectReferenceValue = tierButton;
            so.FindProperty("petalButton").objectReferenceValue = petalButton;
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
            view.Configure(target, park, pools[0], pools[1], pools[2], OrbitRadius);
            EditorUtility.SetDirty(view);
            return view;
        }

        // Undo, beside the intensity button: a slab with a hooked arrow.
        static void CreateUndoButton(Transform root, Material slab, Material mark)
        {
            var go = new GameObject("UndoButton");
            go.transform.SetParent(root, false);
            go.transform.position = TrayCenter + new Vector3(0.36f, 0f, -0.1f);
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.03f, 0f);
            box.size = new Vector3(0.12f, 0.08f, 0.08f);
            Box("UndoSlab", go.transform, Vector3.zero, new Vector3(0.12f, 0.03f, 0.07f), slab, keepCollider: false);
            Box("UndoArrowStem", go.transform, new Vector3(0.01f, 0.025f, 0f), new Vector3(0.06f, 0.012f, 0.012f), mark, keepCollider: false);
            Box("UndoArrowHook", go.transform, new Vector3(0.04f, 0.04f, 0f), new Vector3(0.012f, 0.03f, 0.012f), mark, keepCollider: false);
            var head = Box("UndoArrowHead", go.transform, new Vector3(-0.025f, 0.025f, 0f), new Vector3(0.022f, 0.022f, 0.012f), mark, keepCollider: false);
            head.localRotation = Quaternion.Euler(0f, 0f, 45f);
            go.AddComponent<UndoButton>();
        }

        // Five rising steps in front of the tray: low to high note (or the five udu sounds).
        static PetalButton CreatePetalButton(Transform root, Material slab, Material steps)
        {
            var go = new GameObject("PetalButton");
            go.transform.SetParent(root, false);
            go.transform.position = TrayCenter + new Vector3(0f, 0f, -0.14f);
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.04f, 0f);
            box.size = new Vector3(0.24f, 0.12f, 0.09f);
            Box("PetalSlab", go.transform, Vector3.zero, new Vector3(0.24f, 0.03f, 0.08f), slab, keepCollider: false);

            var petal = go.AddComponent<PetalButton>();
            var stepTransforms = new Transform[NoteMapping.Count];
            for (int i = 0; i < NoteMapping.Count; i++)
            {
                var step = Primitive(PrimitiveType.Cube, $"PetalStep{i}", go.transform, steps, keepCollider: false);
                step.localPosition = new Vector3(-0.088f + 0.044f * i, 0.015f, 0f);
                stepTransforms[i] = step;
            }
            var so = new SerializedObject(petal);
            SetArray(so.FindProperty("steps"), stepTransforms);
            so.ApplyModifiedPropertiesWithoutUndo();
            petal.Show(StartPetal); // authored in its start state, so nothing changes at runtime startup
            return petal;
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
