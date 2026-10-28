using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RoomOrchestra.EditorTools
{
    /// <summary>
    /// Renders the RoomOrchestra scene from the editor preview camera (the seated/standing start view)
    /// to a PNG, as layout evidence. This is Unity's renderer, not the PICO Spatial renderer.
    /// Batch (needs graphics, so no -nographics):
    /// Unity -batchmode -quit -executeMethod RoomOrchestra.EditorTools.SceneSnapshot.Capture -snapshotPath out.png
    /// </summary>
    public static class SceneSnapshot
    {
        public static void Capture()
        {
            string path = "docs/evidence/scene-start-view.png";
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-snapshotPath") path = args[i + 1];

            EditorSceneManager.OpenScene(LittleEchoesSceneSetup.ScenePath);
            var camera = GameObject.Find("EditorPreviewCamera")?.GetComponent<Camera>();
            if (camera == null)
            {
                Debug.LogError("[RoomOrchestra] EditorPreviewCamera not found");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            const int width = 1600, height = 900;
            var rt = new RenderTexture(width, height, 24);
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            camera.targetTexture = null;
            RenderTexture.active = null;

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, image.EncodeToPNG());
            Debug.Log($"[RoomOrchestra] Snapshot saved to {path}");
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(rt);
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>
        /// Close-up of part of the open scene (e.g. the blob tray) from <paramref name="eye"/>, with a
        /// temporary camera that is destroyed afterwards (never saved). Unity's renderer, not PICO's.
        /// </summary>
        public static void CaptureCloseUp(string path, Vector3 eye, Vector3 lookAt, float fieldOfView = 30f)
        {
            var go = new GameObject("SnapshotCloseUpCamera") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var camera = go.AddComponent<Camera>();
                camera.fieldOfView = fieldOfView;
                camera.nearClipPlane = 0.02f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.12f, 0.12f, 0.12f);
                go.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(lookAt - eye));
                const int width = 1200, height = 800;
                var rt = new RenderTexture(width, height, 24);
                camera.targetTexture = rt;
                camera.Render();
                RenderTexture.active = rt;
                var image = new Texture2D(width, height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                camera.targetTexture = null;
                RenderTexture.active = null;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, image.EncodeToPNG());
                Object.DestroyImmediate(image);
                Object.DestroyImmediate(rt);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
