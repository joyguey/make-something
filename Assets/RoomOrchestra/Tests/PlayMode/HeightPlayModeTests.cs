using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace RoomOrchestra.Tests
{
    /// <summary>Height toggle with real transforms and physics (no headset).</summary>
    public class HeightPlayModeTests
    {
        GameObject m_Stage;

        [TearDown]
        public void TearDown()
        {
            ComfortSettings.SetPosture(PlayPosture.Seated);
            if (m_Stage != null) Object.Destroy(m_Stage);
        }

        [UnityTest]
        public IEnumerator HeightButtonLiftsTheControlsAndRaysFollowThem()
        {
            m_Stage = new GameObject("Stage");
            var controls = new GameObject("Controls");
            controls.transform.SetParent(m_Stage.transform, false);
            controls.AddComponent<ControlsRig>();
            var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tile.transform.SetParent(controls.transform, false);
            tile.transform.localPosition = new Vector3(0f, 0.55f, 1.5f);
            tile.transform.localScale = new Vector3(0.14f, 0.11f, 0.03f);
            var buttonGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            buttonGo.transform.SetParent(controls.transform, false);
            buttonGo.transform.localPosition = new Vector3(0.68f, 0.5f, 1f);
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(buttonGo.transform, false);
            var label = labelGo.AddComponent<TextMeshPro>();
            var button = buttonGo.AddComponent<HeightButton>();
            button.Configure(label);
            yield return null;

            button.Press();
            Assert.AreEqual(PlayPosture.Standing, ComfortSettings.Posture);
            Assert.AreEqual("Height\nStanding", label.text);
            Assert.AreEqual(0.55f + PostureLayout.StandingLift, tile.transform.position.y, 1e-4f, "tile lifted with the controls");
            var hand = new Vector3(0f, 1.15f, 0.3f);
            var aim = tile.transform.position - hand;
            Assert.IsTrue(Physics.Raycast(hand, aim.normalized, out var hit, aim.magnitude + 0.1f));
            Assert.AreEqual(tile, hit.collider.gameObject, "a pointer ray hits the tile where it now is, same frame");
            Assert.AreEqual(0.5f + PostureLayout.StandingLift, buttonGo.transform.position.y, 1e-4f, "the Height button moves too, so it can be pressed again");

            button.Press();
            Assert.AreEqual("Height\nSeated", label.text);
            Assert.AreEqual(0.55f, tile.transform.position.y, 1e-4f, "back at the seated height");
        }
    }
}
