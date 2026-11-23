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

        /// <summary>A bare hand's fist must be held this long before it takes hold of furniture, and opened this long
        /// before it lets go (owner, headset 2026-11-22: "I couldn't tell when I was suddenly dragging the virtual
        /// furniture": a relaxed, curled hand looking at a piece took it, and the fist's flicker took and dropped it).
        /// The controller's grip button is deliberate and acts at once. The fist must stay on the same piece all the
        /// while (the gaze passing over another piece starts over), and the piece lifts when it is taken.</summary>
        public const float HandHoldSeconds = 0.7f, HandLetGoSeconds = 0.15f;

        readonly bool[] m_WasGrip = new bool[2];
        readonly float[] m_GripSince = { -1f, -1f }, m_OpenSince = { -1f, -1f };
        readonly bool[] m_GripUsed = new bool[2];
        readonly string[] m_Aimed = new string[2];
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
                bool bareHand = frame.Gaze || frame.BarePalm;
                float now = Time.unscaledTime;
                if (grip)
                {
                    if (closed) { m_GripSince[hand] = now; m_GripUsed[hand] = false; m_Aimed[hand] = null; }
                    m_OpenSince[hand] = -1f;
                }
                else if (m_OpenSince[hand] < 0f) m_OpenSince[hand] = now;
                // a bare hand's fist takes furniture once held a moment (once per fist); a controller's grip at once
                var furniture = RealFurniture.Instance;
                // a bare hand's fist must stay on one piece: looking off it, or onto another, starts the hold over
                if (bareHand && grip && !m_GripUsed[hand] && m_Hand < 0 && furniture != null)
                {
                    var aimed = furniture.WouldTake(at, frame.Tracked || frame.Gaze ? PointerRay(frame, rig) : (Ray?)null);
                    if (aimed != m_Aimed[hand]) { m_Aimed[hand] = aimed; m_GripSince[hand] = now; }
                }
                bool furnitureGrab = !m_GripUsed[hand] && grip && (bareHand ? m_Aimed[hand] != null && now - m_GripSince[hand] >= HandHoldSeconds : closed);
                if (m_Hand == hand)
                {
                    // a bare hand holding furniture lets go only once it has been open a moment (the fist flickers)
                    bool holding = grip || (m_Furniture && bareHand && now - m_OpenSince[hand] < HandLetGoSeconds);
                    if (holding)
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
                if (m_Hand < 0 && playing && closed && Near(at))
                {
                    m_Hand = hand;
                    m_Furniture = false;
                    m_GripUsed[hand] = true;
                    tray.BeginGrab(at);
                }
                else if (m_Hand < 0 && playing && furnitureGrab && furniture != null)
                {
                    bool took = furniture.TryBeginMove(at); // a kept virtual piece within reach (owner, 2026-11-12)
                    m_ByRay = false;
                    if (!took && (frame.Tracked || frame.Gaze)) // or pointed at, from afar
                    {
                        took = furniture.TryBeginMoveByRay(PointerRay(frame, rig));
                        m_ByRay = took;
                    }
                    m_GripUsed[hand] = true; // one try per fist: no grab later in the same fist as the head turns
                    if (took)
                    {
                        m_Hand = hand;
                        m_Furniture = true;
                        if (!frame.Gaze) Haptics.Pulse(hand, Haptics.GrabStrength, Haptics.GrabMs);
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
