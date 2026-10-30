using System.Linq;
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

        /// <summary>
        /// The guided song from the seated eye (Unity's renderer, not PICO's; never saved): its first step, the chair
        /// alone with Flutter's bubble and the glowing F#, and its last, everything out with the 3D count.
        /// Batch: Unity -batchmode -quit -executeMethod RoomOrchestra.EditorTools.SceneSnapshot.CaptureTutorial -snapshotPath out
        /// (writes out-start.png, out-count3.png, out-countgo.png).
        /// </summary>
        public static void CaptureTutorial()
        {
            string path = "docs/evidence/tutorial";
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-snapshotPath") path = args[i + 1];
            EditorSceneManager.OpenScene(LittleEchoesSceneSetup.ScenePath);
            Object.FindFirstObjectByType<IntroSequence>().Finish(skipTutorial: false);
            var tutorial = Object.FindFirstObjectByType<TutorialDirector>();
            var so = new SerializedObject(tutorial);
            var bubble = tutorial.Bubble;
            bubble.localPosition = so.FindProperty("bubbleHome").vector3Value;
            bubble.localScale = so.FindProperty("bubbleScale").vector3Value;
            var text = bubble.GetComponentInChildren<TMPro.TMP_Text>();
            var eye = new Vector3(0f, 1.15f, 0f);

            // The first step: the chair's F# glows behind its tile.
            text.text = UiText.Get(UiTerm.TutorialHello, UiLanguage.English);
            text.ForceMeshUpdate();
            var tile = Object.FindObjectsByType<PitchTile>(FindObjectsSortMode.None).First(t => t.Target.Instrument == InstrumentType.Mbira && t.Petal == 2).transform;
            tutorial.Glow.SetPositionAndRotation(tile.position + tile.forward * (tile.lossyScale.z / 2f + 0.006f), tile.rotation);
            tutorial.Glow.localScale = new Vector3(tile.lossyScale.x + 0.035f, tile.lossyScale.y + 0.035f, 0.006f);
            CaptureCloseUp(path + "-start.png", eye, new Vector3(0f, 0.85f, 1.6f), 70f);

            // The last step: everything out, the count on show.
            foreach (var station in Object.FindObjectsByType<StationReveal>(FindObjectsSortMode.None)) station.ShowNow();
            tutorial.Glow.localScale = Vector3.one * 0.001f;
            text.text = UiText.Get(UiTerm.TutorialOnGo, UiLanguage.English);
            text.ForceMeshUpdate();
            var pieces = tutorial.CountPieces;
            void Show(int index)
            {
                foreach (var p in pieces) p.localScale = Vector3.one * 0.001f;
                pieces[index].localPosition = Vector3.zero;
                pieces[index].localScale = Vector3.one;
            }
            Show(0);
            CaptureCloseUp(path + "-count3.png", eye, new Vector3(0.1f, 1.0f, 1.8f), 80f);
            Show(3);
            CaptureCloseUp(path + "-countgo.png", eye, new Vector3(0.1f, 1.0f, 1.8f), 80f);
            Debug.Log($"[RoomOrchestra] Tutorial snapshots saved to {path}-*.png");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
    }
}
