using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RoomOrchestra.EditorTools
{
    /// <summary>
    /// Diagnostic only: removes the LittleEchoes controls from the saved scene so a build can show whether
    /// they trigger the PICO Spatial rendering mismatch. Restore the scene from git afterwards.
    /// Batch: Unity -batchmode -quit -executeMethod RoomOrchestra.EditorTools.SpatialBisect.StripLittleEchoes
    /// </summary>
    public static class SpatialBisect
    {
        public static void StripLittleEchoes()
        {
            var scene = EditorSceneManager.OpenScene(LittleEchoesSceneSetup.ScenePath);
            var root = GameObject.Find("RoomOrchestra")?.transform.Find("LittleEchoes");
            if (root != null) Object.DestroyImmediate(root.gameObject);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[RoomOrchestra] Bisect: LittleEchoes {(root != null ? "removed" : "not found")}");
        }
    }
}
