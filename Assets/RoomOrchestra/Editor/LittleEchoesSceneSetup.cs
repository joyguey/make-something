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

        // Low enough that seated and standing sight lines to the chair seat pass above the tray.
        static readonly Vector3 TrayCenter = new Vector3(0f, 0.55f, 1.1f);

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

            // Targets left to right, each with a focus marker floating above its aim point
            var targets = Object.FindObjectsByType<InstrumentTarget>(FindObjectsSortMode.None)
                .OrderBy(t => t.AimPosition.x).ToArray();
            var markers = new Transform[targets.Length];
            var markerScale = new Vector3(0.14f, 0.006f, 0.14f);
            for (int i = 0; i < targets.Length; i++)
            {
                var marker = Primitive(PrimitiveType.Cylinder, $"FocusMarker_{targets[i].Instrument}", root.transform, focusMaterial, keepCollider: false);
                marker.position = targets[i].AimPosition + Vector3.up * 0.3f;
                marker.localScale = i == StartTarget ? markerScale : markerScale * 0.001f;
                markers[i] = marker;
            }

            var launcher = Object.FindFirstObjectByType<BallLauncher>();
            PreSkinBalls(orbMaterials[(int)OrbType.Flutter]);

            var selector = root.AddComponent<OrbSelector>();
            var so = new SerializedObject(selector);
            SetArray(so.FindProperty("targets"), targets);
            SetArray(so.FindProperty("focusMarkers"), markers);
            SetArray(so.FindProperty("tokens"), tokens);
            so.FindProperty("focusMarkerScale").vector3Value = markerScale;
            so.FindProperty("tierButton").objectReferenceValue = tierButton;
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
