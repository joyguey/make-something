using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RoomOrchestra.Tests
{
    /// <summary>The restyle in motion (owner, 2026-11-19): a toy button's press, the hit bloom, and tiles that follow
    /// their instrument.</summary>
    public class GameplayLookPlayModeTests
    {
        GameObject m_Root;

        [SetUp] public void SetUp() => m_Root = new GameObject("GameplayLookTest");

        [TearDown]
        public void TearDown()
        {
            HitBloom.ReducedMotion = false;
            PressJiggle.ReducedMotion = false;
            Object.Destroy(m_Root);
        }

        (GameObject button, Transform cap, Transform icon) ToyButton()
        {
            var go = new GameObject("UndoButton");
            go.transform.SetParent(m_Root.transform, false);
            go.transform.localPosition = new Vector3(0f, 1f, 1f);
            var cap = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
            cap.name = "UndoSlab";
            cap.SetParent(go.transform, false);
            cap.localScale = new Vector3(0.076f, 0.03f, 0.058f);
            var icon = new GameObject("UndoIcon").transform;
            icon.SetParent(go.transform, false);
            icon.localPosition = new Vector3(0f, 0.02f, 0f);
            go.AddComponent<UndoButton>();
            go.AddComponent<ToyPress>().Configure(cap);
            return (go, cap, icon);
        }

        [UnityTest]
        public IEnumerator AToyButtonSinksWithItsIconAndComesBackExactly()
        {
            var (go, cap, icon) = ToyButton();
            var capHome = cap.localPosition;
            var iconHome = icon.localPosition;
            var rotation = go.transform.localRotation;
            yield return null;
            Assert.IsTrue(PressJiggle.Play(go.GetComponent<UndoButton>()));
            Assert.IsNull(go.GetComponent<PressJiggle>(), "a toy button presses down instead of wobbling");
            float deepest = 0f;
            float until = Time.unscaledTime + ToyPress.Seconds + 0.2f;
            while (Time.unscaledTime < until)
            {
                yield return null;
                deepest = Mathf.Max(deepest, capHome.y - cap.localPosition.y);
                Assert.AreEqual(capHome.y - cap.localPosition.y, iconHome.y - icon.localPosition.y, 1e-5f, "the icon rides on the cap");
            }
            Assert.Greater(deepest, ToyPress.Depth * 0.8f, "it sinks");
            Assert.LessOrEqual(deepest, ToyPress.Depth * 1.01f, "no deeper than its ring");
            Assert.IsFalse(go.GetComponent<ToyPress>().Pressing);
            Assert.Less(Vector3.Distance(capHome, cap.localPosition), 1e-6f, "the cap is home");
            Assert.Less(Vector3.Distance(iconHome, icon.localPosition), 1e-6f, "the icon is home");
            Assert.AreEqual(rotation, go.transform.localRotation, "the button itself never moved");
        }

        [UnityTest]
        public IEnumerator APressKeepsAPositionTheButtonSetsMeanwhile()
        {
            var (go, cap, icon) = ToyButton();
            yield return null;
            go.GetComponent<ToyPress>().Press();
            yield return null;
            yield return null;
            // Pause swaps its icons mid-press: that move must survive the press
            var swapped = new Vector3(0f, -0.05f, 0f);
            float pressedBy = icon.localPosition.y - 0.02f;
            icon.localPosition = swapped + new Vector3(0f, pressedBy, 0f);
            yield return new WaitForSecondsRealtime(ToyPress.Seconds + 0.1f);
            Assert.AreEqual(swapped.y, icon.localPosition.y, 1e-5f);
        }

        InstrumentTarget Target()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "TestChair";
            go.transform.SetParent(m_Root.transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, 2f);
            go.transform.localScale = new Vector3(2f, 3f, 2f);
            return go.AddComponent<InstrumentTarget>();
        }

        HitBloom Bloom()
        {
            var holder = new GameObject("HitBloom");
            holder.transform.SetParent(m_Root.transform, false);
            var park = new GameObject("Park").transform;
            park.SetParent(m_Root.transform, false);
            park.position = new Vector3(0f, -2f, 0f);
            var petals = new Transform[12];
            for (int i = 0; i < petals.Length; i++)
            {
                petals[i] = GameObject.CreatePrimitive(PrimitiveType.Quad).transform;
                petals[i].SetParent(holder.transform, false);
                petals[i].position = park.position;
            }
            var ripples = new Transform[2];
            for (int i = 0; i < ripples.Length; i++)
            {
                ripples[i] = new GameObject($"Ripple{i}").transform;
                ripples[i].SetParent(holder.transform, false);
                ripples[i].gameObject.AddComponent<MeshFilter>();
                ripples[i].gameObject.AddComponent<MeshRenderer>();
            }
            var bloom = holder.AddComponent<HitBloom>();
            bloom.Configure(petals, new Material[0], ripples, park);
            return bloom;
        }

        [UnityTest]
        public IEnumerator ALiveHitSquashesThenRestoresAndPetalsSettleBackInThePark()
        {
            var bloom = Bloom();
            var target = Target();
            var scale = target.transform.localScale;
            yield return null;
            Assert.IsTrue(bloom.Bloom(target));
            float lowest = scale.y;
            float until = Time.time + HitBloom.PetalSeconds + 0.2f;
            int most = 0;
            while (Time.time < until)
            {
                yield return null;
                lowest = Mathf.Min(lowest, target.transform.localScale.y);
                most = Mathf.Max(most, bloom.ActivePetals);
            }
            Assert.Less(lowest, scale.y * 0.97f, "it squashes");
            Assert.Greater(lowest, scale.y * (1f - HitBloom.Squash - 0.005f), "by about 6%, no more");
            Assert.AreEqual(scale, target.transform.localScale, "back to its exact size");
            Assert.GreaterOrEqual(most, 6, "a puff of petals");
            Assert.AreEqual(0, bloom.ActivePetals, "all settled");
            foreach (var p in bloom.Petals) Assert.Less(p.position.y, -1.9f, $"{p.name} parked again");
            Assert.IsFalse(System.Array.Exists(bloom.PetalLooks, r => r.enabled), "parked petals are not drawn");
        }

        [UnityTest]
        public IEnumerator ReducedMotionDoesntBloom()
        {
            var bloom = Bloom();
            var target = Target();
            var scale = target.transform.localScale;
            yield return null;
            HitBloom.ReducedMotion = true;
            Assert.IsFalse(bloom.Bloom(target), "reduced motion: nothing moves");
            yield return null;
            Assert.AreEqual(scale, target.transform.localScale);
            Assert.AreEqual(0, bloom.ActivePetals);
        }

        [UnityTest]
        public IEnumerator TheSquashLetsGoIfSomethingElseResizesTheFurniture()
        {
            var bloom = Bloom();
            var target = Target();
            yield return null;
            bloom.Bloom(target);
            yield return null;
            yield return null;
            var revealed = new Vector3(0.5f, 0.5f, 0.5f); // a station shrinking into the cloud, say
            target.transform.localScale = revealed;
            yield return new WaitForSeconds(HitBloom.SquashSeconds + 0.1f);
            Assert.AreEqual(revealed, target.transform.localScale, "its size was left as the other owner set it");
        }
    }
}
