using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// Take hold of the leaf by its edge (or, after the lesson, a piece of virtual furniture the player kept) and put it somewhere else (owner, headset 2026-11-08: "make the leaf grabbable
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
        bool m_Furniture; // holding a kept virtual piece of furniture, not the leaf
        bool m_ByRay;     // ... from afar, along the pointer (or the gaze)

        // The pointer's ray in scene space: the controller's, or the eyes' with the hands (look and pinch).
        static Ray PointerRay(PointerFrame frame, Transform rig)
        {
            var origin = rig != null ? rig.TransformPoint(frame.Position) : frame.Position;
            var dir = (rig != null ? rig.rotation * frame.Rotation : frame.Rotation) * Vector3.forward;
            return new Ray(origin, dir);
        }
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
                var furniture = RealFurniture.Instance;
                if (m_Hand == hand)
                {
                    if (grip)
                    {
                        if (m_Furniture && m_ByRay) furniture?.MoveAlong(PointerRay(frame, rig));
                        else if (m_Furniture) furniture?.MoveTo(at);
                        else tray.MoveGrab(at);
                    }
                    else
                    {
                        if (m_Furniture) furniture?.EndMove();
                        else tray.EndGrab();
                        m_Hand = -1;
                    }
                    continue;
                }
                if (m_Hand < 0 && closed && playing)
                {
                    if (Near(at))
                    {
                        m_Hand = hand;
                        m_Furniture = false;
                        tray.BeginGrab(at);
                    }
                    else if (furniture != null && furniture.TryBeginMove(at)) // a kept virtual piece within reach (owner, 2026-11-12)
                    {
                        m_Hand = hand;
                        m_Furniture = true;
                        m_ByRay = false;
                        UiSounds.Play(UiSound.Press, at);
                    }
                    else if (furniture != null && (frame.Tracked || frame.Gaze) && furniture.TryBeginMoveByRay(PointerRay(frame, rig))) // or pointed at, from afar
                    {
                        m_Hand = hand;
                        m_Furniture = true;
                        m_ByRay = true;
                        UiSounds.Play(UiSound.Press, at);
                    }
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
