using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// Take hold of the leaf by its edge and put it somewhere else (owner, headset 2026-11-08: "make the leaf grabbable
    /// from the side so if I try and grab it with my full hand or controller, I can adjust it to where the location makes
    /// sense and the cloud will keep its position relative to the leaf"). A controller's grip button, or a bare hand
    /// closed into a fist, within <see cref="Reach"/> of the leaf grabs it; the leaf and its cloud follow the hand
    /// (<see cref="RoamingTray.MoveGrab"/>) until the grip opens. The trigger and pinch still tap and throw as before.
    /// </summary>
    public class LeafGrab : MonoBehaviour
    {
        /// <summary>How close to the leaf (m, from its edge) a grab takes hold of it.</summary>
        public const float Reach = 0.12f;

        readonly bool[] m_WasGrip = new bool[2];
        int m_Hand = -1;
        Renderer m_Leaf;

        /// <summary>The hand holding the leaf (0 left, 1 right), or -1.</summary>
        public int Holding => m_Hand;

        void Update()
        {
            var tray = RoamingTray.Instance;
            var router = FindRouter();
            if (tray == null || router == null || router.Pointers == null) return;
            var intro = IntroSequence.Instance;
            var title = TitleScreen.Instance;
            bool playing = (intro == null || intro.Done) && (title == null || !title.Showing);
            var rig = XrRig.Instance != null ? XrRig.Instance.transform : null;
            for (int hand = 0; hand < 2; hand++)
            {
                var frame = router.Pointers.Read(hand);
                var at = rig != null ? rig.TransformPoint(frame.GripPosition) : frame.GripPosition;
                bool grip = frame.Grip;
                bool closed = grip && !m_WasGrip[hand];
                m_WasGrip[hand] = grip;
                if (m_Hand == hand)
                {
                    if (grip) tray.MoveGrab(at);
                    else
                    {
                        tray.EndGrab();
                        m_Hand = -1;
                    }
                    continue;
                }
                if (m_Hand < 0 && closed && playing && Near(at))
                {
                    m_Hand = hand;
                    tray.BeginGrab(at);
                }
            }
        }

        /// <summary>Whether a grab at <paramref name="world"/> reaches the leaf.</summary>
        public bool Near(Vector3 world)
        {
            if (m_Leaf == null)
            {
                var intro = IntroSequence.Instance;
                var leaf = intro != null && intro.Leaf != null ? intro.Leaf.Find("Leaf") : null;
                m_Leaf = leaf != null ? leaf.GetComponent<Renderer>() : null;
            }
            if (m_Leaf == null) return false;
            var box = m_Leaf.bounds;
            box.Expand(Reach * 2f);
            return box.Contains(world);
        }

        InstrumentInputRouter m_Router;
        InstrumentInputRouter FindRouter() => m_Router != null ? m_Router : (m_Router = FindFirstObjectByType<InstrumentInputRouter>());
    }
}
