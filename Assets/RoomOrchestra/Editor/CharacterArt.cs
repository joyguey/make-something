using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RoomOrchestra.EditorTools
{
    /// <summary>
    /// Dresses scene objects as Flutter / Bop / Boom from the meshes built by
    /// ArtSource/Characters/build_characters.py (Blender, from the owner-approved reference).
    /// Each part is its own child with a project material, so colours stay editable and face parts
    /// can move for expressions. Built at edit time only: nothing is created at runtime.
    /// </summary>
    public static class CharacterArt
    {
        public const string Dir = "Assets/RoomOrchestra/Art/Characters";
        const string MaterialDir = "Assets/RoomOrchestra/Materials";

        static Color ToColor(Rgb c) => new Color(c.R, c.G, c.B);

        /// <summary>Rim and face come from the orb catalog (runtime recolours balls with them); shine and motif are art-only.</summary>
        static (Color rim, Color face, Color shine, Color motif) Palette(OrbType type)
        {
            var profile = OrbCatalog.Get(type);
            var (shine, motif) = type switch
            {
                OrbType.Flutter => (new Color(0.86f, 0.96f, 0.97f), new Color(0.93f, 0.80f, 0.40f)), // reference sparkle
                OrbType.Bop => (new Color(0.90f, 0.84f, 0.98f), ToColor(profile.Rim)),
                _ => (new Color(0.96f, 0.86f, 0.66f), ToColor(profile.Rim)),
            };
            return (ToColor(profile.Rim), ToColor(profile.Face), shine, motif);
        }

        static string FbxPath(OrbType type) => $"{Dir}/{type}.fbx";

        public static bool Has(OrbType type) => Part(type, "Body") != null;

        public static Mesh Part(OrbType type, string part)
        {
            string path = FbxPath(type);
            EnsureImportSettings(path);
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().FirstOrDefault(m => m.name == $"{type}_{part}");
        }

        // Meshes only: project materials colour them, and no animation, cameras or lights come along.
        static void EnsureImportSettings(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer)) return;
            if (importer.materialImportMode == ModelImporterMaterialImportMode.None && !importer.importAnimation) return;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.SaveAndReimport();
        }

        static Material EnsureMaterial(string name, Color color)
        {
            string path = $"{MaterialDir}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/Ball.mat"));
                AssetDatabase.CreateAsset(material, path);
            }
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// Tray character on <paramref name="root"/>: a "Visual" child carries the body, rim, face, shine and
        /// motif so JellyBody can breathe and wobble it while the token keeps its selection size. The
        /// surprised "oh" mouth is parked inside the body until an expression brings it forward (PICO
        /// Spatial draws inactive objects, so parking hides it).
        /// </summary>
        public static void Dress(GameObject root, OrbType type, Material body)
        {
            var (rimColor, faceColor, shineColor, motifColor) = Palette(type);
            var rim = EnsureMaterial($"Rim_{type}", rimColor);
            var face = EnsureMaterial($"Face_{type}", faceColor);
            var shine = EnsureMaterial($"Shine_{type}", shineColor);
            var motif = EnsureMaterial($"Motif_{type}", motifColor);

            var visual = Visual(root.transform, type, "Body", body);
            Child(visual, type, "Outline", rim, Vector3.zero);
            var eyeL = Child(visual, type, "EyeL", face, Vector3.zero);
            var eyeR = Child(visual, type, "EyeR", face, Vector3.zero);
            var smile = Child(visual, type, "Smile", face, Vector3.zero);
            var oh = Child(visual, type, "Mouth_O", face, MouthParked);
            Child(visual, type, "Highlight", shine, Vector3.zero);
            Child(visual, type, "Motif", motif, Vector3.zero);
            Alive(root, visual, type, awake: true, eyeL, eyeR, smile, oh);
        }

        static Transform Visual(Transform root, OrbType type, string bodyPart, Material body)
        {
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root, false);
            visual.gameObject.AddComponent<MeshFilter>().sharedMesh = Part(type, bodyPart);
            visual.gameObject.AddComponent<MeshRenderer>().sharedMaterial = body;
            return visual;
        }

        // Face and jelly body on the root, both driving parts under the visual.
        static void Alive(GameObject root, Transform visual, OrbType type, bool awake, Transform eyeL, Transform eyeR, Transform smile, Transform oh = null)
        {
            var face = root.AddComponent<CharacterFace>();
            face.Configure(eyeL, eyeR, smile, oh);
            var jelly = root.AddComponent<JellyBody>();
            jelly.Configure(visual, type, awake);
            EditorUtility.SetDirty(face);
            EditorUtility.SetDirty(jelly);
        }

        /// <summary>
        /// Lighter echo-mini version (body, rim, eyes, smile) on <paramref name="root"/>: 54 of these orbit
        /// the furniture, so they skip the shine, motif and spare mouth.
        /// </summary>
        public static void DressMini(GameObject root, OrbType type, Material body)
        {
            var (rimColor, faceColor, _, _) = Palette(type);
            var rim = EnsureMaterial($"Rim_{type}", rimColor);
            var face = EnsureMaterial($"Face_{type}", faceColor);
            // The look sits on a child so the jelly can squash it while the orbit owns the root's scale.
            var visual = Visual(root.transform, type, "MiniBody", body);
            Child(visual, type, "MiniOutline", rim, Vector3.zero);
            var eyeL = Child(visual, type, "MiniEyeL", face, Vector3.zero);
            var eyeR = Child(visual, type, "MiniEyeR", face, Vector3.zero);
            var smile = Child(visual, type, "MiniSmile", face, Vector3.zero);
            Alive(root, visual, type, awake: false, eyeL, eyeR, smile); // asleep in the hub until it joins the ring
        }

        /// <summary>
        /// Throwable ball: Bop's round body (the ball's scale gives each orb its silhouette) with a rim,
        /// eyes and smile that the ball recolours for whichever character it currently is. Re-running
        /// replaces the parts from a previous run (balls live outside the rebuilt Little Echoes root).
        /// </summary>
        public static (Renderer body, Renderer rim, Renderer[] face) DressBall(GameObject ball, OrbType current)
        {
            // Earlier runs: the look lived on the root, or on a Visual child.
            var oldVisual = ball.transform.Find("Visual");
            if (oldVisual != null) Object.DestroyImmediate(oldVisual.gameObject);
            foreach (var part in new[] { "Outline", "EyeL", "EyeR", "Smile", "Mouth_O" })
            {
                var old = ball.transform.Find(part);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }
            foreach (var c in ball.GetComponents<CharacterFace>()) Object.DestroyImmediate(c);
            foreach (var c in ball.GetComponents<JellyBody>()) Object.DestroyImmediate(c);
            if (ball.TryGetComponent<MeshRenderer>(out var rootRenderer)) Object.DestroyImmediate(rootRenderer);
            if (ball.TryGetComponent<MeshFilter>(out var rootFilter)) Object.DestroyImmediate(rootFilter);

            var (rimColor, faceColor, _, _) = Palette(current);
            var rim = EnsureMaterial($"Rim_{current}", rimColor);
            var face = EnsureMaterial($"Face_{current}", faceColor);
            var bodyMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/Orb_{current}.mat");
            var visual = Visual(ball.transform, OrbType.Bop, "Body", bodyMaterial);
            var outline = Child(visual, OrbType.Bop, "Outline", rim, Vector3.zero);
            var eyeL = Child(visual, OrbType.Bop, "EyeL", face, Vector3.zero);
            var eyeR = Child(visual, OrbType.Bop, "EyeR", face, Vector3.zero);
            var smile = Child(visual, OrbType.Bop, "Smile", face, Vector3.zero);
            var oh = Child(visual, OrbType.Bop, "Mouth_O", face, MouthParked); // for the scared fall to the floor
            Alive(ball, visual, current, awake: false, eyeL, eyeR, smile, oh); // the launcher wakes the ball it hands out
            var parts = new[] { eyeL.GetComponent<Renderer>(), eyeR.GetComponent<Renderer>(), smile.GetComponent<Renderer>(), oh.GetComponent<Renderer>() };
            return (visual.GetComponent<Renderer>(), outline.GetComponent<Renderer>(), parts);
        }

        /// <summary>Offset that sinks the "oh" mouth (built on the face at z ~ -0.41) wholly inside the body.</summary>
        public static readonly Vector3 MouthParked = new Vector3(0f, 0f, 0.25f);

        static Transform Child(Transform parent, OrbType type, string part, Material material, Vector3 localPosition)
        {
            var go = new GameObject(part);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.AddComponent<MeshFilter>().sharedMesh = Part(type, part);
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go.transform;
        }
    }
}
