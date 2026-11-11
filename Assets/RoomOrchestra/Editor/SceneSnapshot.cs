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
        // The scene, with the meadow at dusk when the command line says -dusk (golden hour otherwise; the scene's own).
        static void OpenScene()
        {
            EditorSceneManager.OpenScene(LittleEchoesSceneSetup.ScenePath);
            if (System.Environment.GetCommandLineArgs().Contains("-dusk"))
                Object.FindFirstObjectByType<MeadowTimeOfDay>()?.Apply(true);
        }

        public static void Capture()
        {
            string path = "docs/evidence/scene-start-view.png";
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-snapshotPath") path = args[i + 1];

            OpenScene();
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

        /// <summary>The leaf from the seat with its menu closed and open (-snapshotPath: folder). Not saved.</summary>
        public static void CaptureMenu()
        {
            string dir = "docs/evidence";
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-snapshotPath") dir = args[i + 1];
            OpenScene();
            Object.FindFirstObjectByType<IntroSequence>()?.Finish();
            var tray = Object.FindFirstObjectByType<MenuTray>();
            var eye = new Vector3(0f, 1.15f, 0.05f);
            var leaf = new Vector3(-0.2f, 0.55f, 1.15f);
            tray.transform.localScale = Vector3.one * MenuTray.ClosedScale;
            CaptureCloseUp(Path.Combine(dir, "leaf-menu-closed.png"), eye, leaf, 45f);
            tray.transform.localScale = Vector3.one;
            CaptureCloseUp(Path.Combine(dir, "leaf-menu-open.png"), eye, leaf, 45f);
            Debug.Log($"[RoomOrchestra] Menu snapshots saved to {dir}");
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
            OpenScene();
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

        /// <summary>
        /// The virtual starting room coming apart, from the seated eye (Unity's renderer, not PICO's; never saved):
        /// frames at 0, 1, 2 s and after. Batch: -executeMethod RoomOrchestra.EditorTools.SceneSnapshot.CaptureRoom
        /// -snapshotPath out (writes out-0.png ... out-3.png).
        /// </summary>
        public static void CaptureRoom()
        {
            string path = "docs/evidence/virtual-room";
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-snapshotPath") path = args[i + 1];
            OpenScene();
            Object.FindFirstObjectByType<IntroSequence>().Finish(skipTutorial: false);
            foreach (var station in Object.FindObjectsByType<StationReveal>(FindObjectsSortMode.None)) station.ShowNow();
            var room = Object.FindFirstObjectByType<VirtualRoom>();
            var so = new SerializedObject(room);
            var from = room.transform.TransformPoint(so.FindProperty("rippleFrom").vector3Value);
            var delays = room.Panels.Select(p => VirtualRoom.DelayFor(Vector3.Distance(p.position, from))).ToArray();
            var eye = new Vector3(0f, 1.15f, 0f);
            float[] times = { 0f, 1f, 2f, 9f };
            for (int f = 0; f < times.Length; f++)
            {
                for (int i = 0; i < room.Panels.Count; i++)
                    room.PosePanel(i, (times[f] - delays[i]) / VirtualRoom.PanelSeconds);
                CaptureCloseUp($"{path}-{f}.png", eye, new Vector3(0f, 1.1f, 2f), 95f);
            }
            Debug.Log($"[RoomOrchestra] Room snapshots saved to {path}-*.png");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>Close-up of the leaf's controls from the seated eye (Unity's renderer). Batch: -executeMethod
        /// RoomOrchestra.EditorTools.SceneSnapshot.CaptureLeaf -snapshotPath out.png</summary>
        public static void CaptureLeaf()
        {
            string path = "docs/evidence/leaf.png";
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-snapshotPath") path = args[i + 1];
            OpenScene();
            // -lesson: as the guided song starts (the leaf's extras tucked away); otherwise free play.
            Object.FindFirstObjectByType<IntroSequence>().Finish(skipTutorial: !args.Contains("-lesson"));
            if (!args.Contains("-lesson")) TuckAway.ShowAllNow();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-viewMode") Object.FindFirstObjectByType<ViewSettings>().Preview((ViewMode)int.Parse(args[i + 1]));
            CaptureCloseUp(path, new Vector3(0f, 1.15f, 0f), new Vector3(-0.1f, 0.5f, 1.15f), 28f);
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>The Echo Battle card (easter egg) out beside the leaf, from the seated eye (Unity's renderer; never
        /// saved). Batch: -executeMethod RoomOrchestra.EditorTools.SceneSnapshot.CaptureBattle -snapshotPath out.png</summary>
        public static void CaptureBattle()
        {
            string path = "docs/evidence/echo-battle.png";
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-snapshotPath") path = args[i + 1];
            OpenScene();
            Object.FindFirstObjectByType<IntroSequence>().Finish();
            var battle = Object.FindFirstObjectByType<EchoBattleDirector>();
            var so = new SerializedObject(battle);
            battle.Card.localPosition = so.FindProperty("cardHome").vector3Value;
            battle.Card.localScale = so.FindProperty("cardScale").vector3Value;
            CaptureCloseUp(path, new Vector3(0f, 1.15f, 0f), new Vector3(-0.3f, 1.0f, 1.6f), 70f);
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>The furniture up close (Unity's renderer; never saved). Batch: -executeMethod
        /// RoomOrchestra.EditorTools.SceneSnapshot.CaptureFurniture -snapshotPath out.png</summary>
        public static void CaptureFurniture()
        {
            string path = "docs/evidence/furniture.png";
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-snapshotPath") path = args[i + 1];
            OpenScene();
            Object.FindFirstObjectByType<IntroSequence>().Finish();
            Object.FindFirstObjectByType<TutorialDirector>().SkipNow();
            var room = Object.FindFirstObjectByType<VirtualRoom>();
            foreach (var panel in room.Panels) panel.localScale = Vector3.one * 0.0001f; // out of the shot
            CaptureCloseUp(path, new Vector3(0.3f, 1.35f, 0.9f), new Vector3(0f, 0.55f, 2.4f), 62f);
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
    }
}
