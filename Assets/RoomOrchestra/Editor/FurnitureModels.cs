using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RoomOrchestra.EditorTools
{
    /// <summary>
    /// More detailed furniture (Version 1 feedback: "more detailed 3D models"), modelled in Blender by
    /// ArtSource/Furniture/build_furniture.py at each part's real size: a planked table top with an apron, tapered legs,
    /// a cushioned seat, a slatted chair back, a stepped lamp base and a collared pole. Each Blender mesh is stored in its
    /// part's own local space (world size divided by the part's scale), so it replaces the primitive exactly: sizes,
    /// colliders, taps, vines and layout are unchanged. <see cref="DumpPartSizes"/> writes the sizes Blender builds to.
    /// </summary>
    public static class FurnitureModels
    {
        public const string FbxPath = "Assets/RoomOrchestra/Art/Furniture/Furniture.fbx";
        const string SizesPath = "ArtSource/Furniture/part_sizes.json";

        // (furniture, part) -> the Blender mesh that replaces it.
        static readonly (string furniture, string part, string mesh)[] Parts =
        {
            ("Table", "Top", "Furniture_TableTop"), ("Table", "Leg", "Furniture_TableLeg"),
            ("Chair", "Seat", "Furniture_ChairSeat"), ("Chair", "Back", "Furniture_ChairBack"), ("Chair", "Leg", "Furniture_ChairLeg"),
            ("Lamp", "Base", "Furniture_LampBase"), ("Lamp", "Pole", "Furniture_LampPole"),
        };

        [System.Serializable] class PartSize { public string mesh; public float[] scale; public float[] size; }
        [System.Serializable] class PartSizes { public PartSize[] parts; }

        /// <summary>Batch: -executeMethod RoomOrchestra.EditorTools.FurnitureModels.DumpPartSizes (after the scene setup).</summary>
        public static void DumpPartSizes()
        {
            EditorSceneManager.OpenScene(LittleEchoesSceneSetup.ScenePath);
            Object.FindFirstObjectByType<IntroSequence>().Finish(); // the stations home, at full size
            var furniture = GameObject.Find("RoomOrchestra").transform.Find("Furniture");
            var list = new List<PartSize>();
            foreach (var (f, p, mesh) in Parts)
            {
                var part = furniture.Find(f).Cast<Transform>().First(t => t.name == p);
                var s = part.lossyScale;
                // From the scale, not the mesh (a built model may hang outside its box): a cylinder is 2 units tall.
                var b = p == "Base" || p == "Pole" ? new Vector3(1f, 2f, 1f) : Vector3.one;
                list.Add(new PartSize { mesh = mesh, scale = new[] { s.x, s.y, s.z }, size = new[] { b.x * s.x, b.y * s.y, b.z * s.z } });
            }
            File.WriteAllText(SizesPath, JsonUtility.ToJson(new PartSizes { parts = list.ToArray() }, true));
            Debug.Log($"[RoomOrchestra] Furniture part sizes written to {SizesPath}");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>Puts the Blender meshes on the furniture's parts (scene setup); false if the models aren't built yet.</summary>
        public static bool Apply(Transform furniture)
        {
            if (furniture == null || !File.Exists(FbxPath)) return false;
            if (AssetImporter.GetAtPath(FbxPath) is ModelImporter importer && (importer.materialImportMode != ModelImporterMaterialImportMode.None || importer.importAnimation))
            {
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.importAnimation = false;
                importer.importCameras = false;
                importer.importLights = false;
                importer.SaveAndReimport();
            }
            var meshes = AssetDatabase.LoadAllAssetsAtPath(FbxPath).OfType<Mesh>().ToDictionary(m => m.name);
            int placed = 0;
            foreach (var (f, p, name) in Parts)
            {
                var piece = furniture.Find(f);
                if (piece == null || !meshes.TryGetValue(name, out var mesh)) continue;
                foreach (Transform part in piece)
                    if (part.name == p && part.TryGetComponent<MeshFilter>(out var filter))
                    {
                        filter.sharedMesh = mesh;
                        // its ambient occlusion, baked in Blender (V3 review), darkens it through its own material's copy
                        if (part.TryGetComponent<MeshRenderer>(out var look)) look.sharedMaterial = BakedAO.For(mesh, look.sharedMaterial);
                        placed++;
                    }
            }
            Debug.Log($"[RoomOrchestra] Furniture models: {placed} parts");
            return placed > 0;
        }
    }
}
