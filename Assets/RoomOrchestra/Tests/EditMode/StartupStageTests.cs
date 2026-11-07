using NUnit.Framework;
using UnityEditor;

namespace RoomOrchestra.Tests
{
    /// <summary>
    /// The app opens straight into its full-room Stage, not a Shared Space window first (George, 2026-11-03). PICO's
    /// SpatialAdapter declares the startup container from the default spatial camera configuration.
    /// </summary>
    public class StartupStageTests
    {
        [Test]
        public void TheDefaultSpatialCameraConfigurationIsTheFullSpaceOne()
        {
            var settings = AssetDatabase.LoadMainAssetAtPath("Assets/Settings/SpatialAdapterRuntimeSettings.asset");
            Assert.IsNotNull(settings);
            var config = new SerializedObject(settings).FindProperty("m_defaultSpatialCameraConfiguration").objectReferenceValue;
            Assert.IsNotNull(config, "a default configuration is set");
            Assert.AreEqual("Assets/RoomOrchestra/Settings/RoomOrchestraFullSpace.asset", AssetDatabase.GetAssetPath(config),
                "the Unconstrained (Full Space) one, so the manifest declares a Stage rather than a window");
            Assert.AreEqual(1, new SerializedObject(config).FindProperty("mode").enumValueIndex, "Unconstrained");
        }
        // The Stage's text name (Editor/StageManifestName) is checked in the built APK's manifest: aapt2 dump xmltree.
    }
}
