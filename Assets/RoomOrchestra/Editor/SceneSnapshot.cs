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
        // The scene, with the meadow at dusk when the command line says -dusk (golden hour otherwise; the scene's own),
        // and the title's handprint Start with -hands.
        static void OpenScene()
        {
            EditorSceneManager.OpenScene(LittleEchoesSceneSetup.ScenePath);
            if (System.Environment.GetCommandLineArgs().Contains("-dusk"))
                Object.FindFirstObjectByType<MeadowTimeOfDay>()?.Apply(true);
            // look trials: -toon 0..1, -rim 0..1 (warm), -haze start end (m) in the sky's horizon colour
            var look = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < look.Length - 1; i++)
            {
                var inv = System.Globalization.CultureInfo.InvariantCulture;
                if (look[i] == "-toon") Shader.SetGlobalFloat("_RO_Toon", float.Parse(look[i + 1], inv));
                if (look[i] == "-rim") { Shader.SetGlobalFloat("_RO_Rim", float.Parse(look[i + 1], inv)); Shader.SetGlobalColor("_RO_RimColor", new Color(1f, 0.93f, 0.8f)); }
                if (look[i] == "-haze" && i + 2 < look.Length)
                {
                    RenderSettings.fog = true;
                    RenderSettings.fogMode = FogMode.Linear;
                    RenderSettings.fogStartDistance = float.Parse(look[i + 1], inv);
                    RenderSettings.fogEndDistance = float.Parse(look[i + 2], inv);
                    bool dusk = look.Contains("-dusk");
                    RenderSettings.fogColor = dusk ? new Color(0.878f, 0.604f, 0.478f) : new Color(0.949f, 0.851f, 0.722f);
                }
            }
            if (look.Contains("-ghosthand")) // PICO's hand model in the outline look, in its rest pose, by the Start print
            {
                var model = Object.FindFirstObjectByType<HandOutline>()?.Model(1);
                if (model != null)
                {
                    model.SetActive(true);
                    model.transform.position = new Vector3(0.22f, 0.98f, 0.5f);
                    model.transform.rotation = Quaternion.Euler(-80f, 0f, 0f);
                }
            }
            if (look.Contains("-askquit")) // the title's Quit asks "Quit Little Echoes?"
            {
                var asker = Object.FindFirstObjectByType<TitleScreen>().Card.GetComponentInChildren<QuitButton>(true);
                Object.FindFirstObjectByType<ConfirmCard>().Show(asker);
            }
            if (System.Environment.GetCommandLineArgs().Contains("-hands")) // as a bare-handed player sees it
                Object.FindFirstObjectByType<HandprintStart>(FindObjectsInactive.Include)?.Apply(true);
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
            camera.Render(); // a first frame sets up the lighting; the second is the picture
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

        /// <summary>The sky from the seat: ahead and up (-snapshotPath folder/name: writes -ahead.png), and back toward
        /// the sun or moon (-back.png). -dusk for the night sky. Unity's renderer; never saved.</summary>
        public static void CaptureSky()
        {
            string path = "docs/evidence/sky";
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-snapshotPath") path = args[i + 1];
            OpenScene();
            var eye = new Vector3(0f, 1.15f, 0f);
            CaptureCloseUp($"{path}-ahead.png", eye, eye + new Vector3(0f, 0.45f, 1f), 80f);
            var light = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).First(l => l.type == LightType.Directional);
            CaptureCloseUp($"{path}-back.png", eye, eye - light.transform.forward + Vector3.down * 0.25f, 70f);
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>The three friends' faces, close up (-snapshotPath out.png). Unity's renderer; never saved.</summary>
        public static void CaptureFriends()
        {
            string path = "docs/evidence/friends.png";
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-snapshotPath") path = args[i + 1];
            OpenScene();
            // the same three characters as on the leaf (CharacterArt.Dress), where nothing stands in front of them:
            // on the title sign's rail
            var friends = GameObject.Find("TitleFriends").transform;
            var middle = friends.GetChild(1).position;
            CaptureCloseUp(path, middle + new Vector3(0f, 0.03f, -0.42f), middle + Vector3.up * 0.03f, 50f);
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>The title card on its sign with the friends on the rail, from the seated eye (-snapshotPath out.png;
        /// -dusk for the night meadow, -hands for the handprint Start, -tilt degrees to show it mid-swing, -side to look
        /// from the side). Unity's renderer; never saved.</summary>
        public static void CaptureTitle()
        {
            string path = "docs/evidence/title.png";
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-snapshotPath") path = args[i + 1];
            OpenScene();
            var card = GameObject.Find("TitleCard").transform;
            // the sign is at arm's reach: seen from the seat (or standing with -standing) with the headset's wide view
            var eye = new Vector3(0f, args.Contains("-standing") ? 1.65f : 1.15f, 0f);
            float fov = 85f;
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-tilt") card.Find("Hanging").localRotation = Quaternion.Euler(float.Parse(args[i + 1], System.Globalization.CultureInfo.InvariantCulture), 0f, 0f);
                if (args[i] == "-side") { eye = card.position + new Vector3(1.3f, 0.25f, -0.55f); fov = 38f; }
                if (args[i] == "-eye") { eye = new Vector3(0f, 1.15f, 0f) + Vector3.right * float.Parse(args[i + 1], System.Globalization.CultureInfo.InvariantCulture) + Vector3.forward * 0.9f; }
            }
            CaptureCloseUp(path, eye, args.Contains("-side") ? card.position + Vector3.up * 0.05f : new Vector3(0.12f, card.position.y - 0.1f, card.position.z), fov);
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>
        /// Close-up of part of the open scene (e.g. the blob tray) from <paramref name="eye"/>, with a
        /// temporary camera that is destroyed afterwards (never saved). Unity's renderer, not PICO's.
        /// </summary>
        public static void CaptureCloseUp(string path, Vector3 eye, Vector3 lookAt, float fieldOfView = 30f)
        {
            ShaderUtil.allowAsyncCompilation = false; // a single frame: compile every shader now, never a placeholder
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
                camera.Render(); // a first frame sets up the lighting; the second is the picture
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
