using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RoomOrchestra.Tests
{
    /// <summary>
    /// The game runs in PICO XR mode (owner, 2026-11-08): Unity draws it, the real room shows through (video
    /// seethrough), and the mixed-reality features it needs are on. Checked on the project and the saved scene.
    /// </summary>
    public class XrModeTests
    {
        const string ScenePath = "Assets/RoomOrchestra/Scenes/RoomOrchestra.unity";

        [Test]
        public void TheProjectIsInPicoXrModeWithTheRoomFeaturesOn()
        {
            var defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android);
            StringAssert.Contains("ENABLE_PICO_XR_SDK", defines);
            StringAssert.DoesNotContain("PICO_MS_SDK", defines);
            var settings = ByteDance.PICO.XR.PXR_Settings.GetSettings();
            Assert.IsNotNull(settings);
            Assert.AreEqual(ByteDance.PICO.XR.PXR_Settings.AppMode.XR, settings.appMode);
            var config = ByteDance.PICO.XR.PXR_ProjectSetting.GetProjectConfig();
            Assert.IsFalse(config.isSpatialAdapter);
            Assert.IsTrue(config.videoSeeThrough, "the real room behind the game");
            Assert.IsTrue(config.spatialMesh, "real walls and the scan grid");
            Assert.IsTrue(config.sceneCapture, "PICO's Room Capture");
            Assert.IsTrue(config.spatialAnchor, "places the player picks");
            Assert.IsTrue(config.environmentDepth, "PICO's depth sensing hides what is behind real things");
            Assert.IsFalse(System.IO.File.Exists("Assets/Plugins/Android/AndroidManifest.xml"), "PICO Spatial's launcher manifest is gone");
            Assert.IsFalse(System.IO.File.ReadAllText("Packages/manifest.json").Contains("com.bytedance.pico.spatialadapter"), "the PICO Spatial package is removed");
        }

        [Test]
        public void TheRigSeesTheRealRoomAndPointsWithBothControllers()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var rig = Object.FindFirstObjectByType<XrRig>();
            Assert.IsNotNull(rig, "an XR rig");
            Assert.IsNull(rig.transform.parent, "at the scene's top level");
            Assert.AreEqual(Vector3.zero, rig.transform.position, "tracking space is scene space");
            var cam = rig.Head;
            Assert.IsNotNull(cam);
            Assert.IsTrue(cam.CompareTag("MainCamera"));
            Assert.IsFalse(cam.enabled, "saved off; switched on only on the device");
            Assert.AreEqual(CameraClearFlags.SolidColor, cam.clearFlags);
            Assert.AreEqual(new Color(0f, 0f, 0f, 0f), cam.backgroundColor, "transparent: the seethrough shows");
            Assert.IsFalse(cam.allowHDR, "HDR stops seethrough on Vulkan");
            Assert.IsNotNull(cam.GetComponent<AudioListener>());
            var router = Object.FindFirstObjectByType<InstrumentInputRouter>();
            var so = new SerializedObject(router);
            Assert.AreEqual(2, so.FindProperty("rays").arraySize, "a ray per controller");
            Assert.AreEqual(2, so.FindProperty("reticles").arraySize);
            Assert.IsNotNull(so.FindProperty("rayHot").objectReferenceValue);
            var spatial = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(t => t.name.Contains("Spatial Camera")).Select(t => t.name).ToArray();
            Assert.IsEmpty(spatial, "no PICO Spatial camera left");
        }
    }
}
