using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// The title card hangs from the sign's top rail on two steel rings and rocks like a hung sign (owner, Fable
    /// refinement 2026-11-11: "if there was a sudden gust of wind, the post would rock forward and back a little. Make
    /// it so if you pushed it with your hand, it would also rock back and forth"). A damped pendulum about the rail:
    /// now and then a soft gust nudges it a few degrees; a hand (or a controller) moving into the card carries it with
    /// the hand, and it swings on and settles. Gentle on purpose (no fast or large motion), and only on the title.
    /// The rotation is about the pivot's local X axis (along the rail); the card hangs below the pivot.
    /// </summary>
    public class TitleSwing : MonoBehaviour
    {
        /// <summary>Pivot to the card's middle (m): sets how fast it swings (about 0.8 swings a second).</summary>
        public const float Length = 0.45f;
        /// <summary>How quickly a swing dies away (1/s): a push settles in about four seconds.</summary>
        public const float Damping = 0.9f;
        /// <summary>The furthest it ever swings (degrees), however hard it is pushed.</summary>
        public const float MaxAngle = 22f;
        /// <summary>A gust's nudge (degrees of swing it starts) and the quiet between gusts (s).</summary>
        public const float GustDegrees = 3f, GustGapMin = 7f, GustGapMax = 15f;
        /// <summary>How close to the card's face a hand counts as touching it (m), and the card's half size (m).</summary>
        public const float TouchDepth = 0.06f;

        [SerializeField] Vector2 halfSize = new Vector2(0.475f, 0.295f);
        [SerializeField] float cardCentreBelow = 0.3595f;
        [SerializeField] float cardFaceZ = -0.03f; // the card's face, in front of the rail (pivot space)

        float m_Angle, m_Speed;  // radians, radians per second (positive tips the bottom toward the player)
        float m_NextGust = -1f;
        readonly Vector3[] m_LastPalm = new Vector3[2];
        readonly bool[] m_HadPalm = new bool[2];
        System.Random m_Random = new System.Random(2026_1111);

        /// <summary>No gusts (tests, snapshots); pushes still work.</summary>
        public static bool Still;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Still = false;

        public float AngleDegrees => m_Angle * Mathf.Rad2Deg;

        public void Configure(Vector2 cardHalfSize, float pivotAboveCardCentre, float faceZ)
        {
            halfSize = cardHalfSize;
            cardCentreBelow = pivotAboveCardCentre;
            cardFaceZ = faceZ;
        }

        void Update() => Step(Mathf.Min(Time.deltaTime, 1f / 30f));

        /// <summary>Advances the swing by <paramref name="dt"/> seconds (tests call it directly).</summary>
        public void Step(float dt)
        {
            if (dt <= 0f) return;
            bool showing = TitleScreen.Instance == null || TitleScreen.Instance.Showing;
            if (showing && !Still) Gusts();
            if (showing) Pushes(dt);
            // a hung sign: gravity pulls it back to hanging straight; the air slows it
            float pull = -(Physics.gravity.magnitude / Length) * Mathf.Sin(m_Angle);
            m_Speed += (pull - Damping * m_Speed) * dt;
            m_Angle += m_Speed * dt;
            float max = MaxAngle * Mathf.Deg2Rad;
            if (Mathf.Abs(m_Angle) > max)
            {
                m_Angle = Mathf.Sign(m_Angle) * max;
                m_Speed = 0f;
            }
            transform.localRotation = Quaternion.Euler(m_Angle * Mathf.Rad2Deg, 0f, 0f); // + about X: the bottom toward -Z, the player
        }

        /// <summary>Starts a swing of about <paramref name="degrees"/> (a gust; positive tips the bottom toward the player).</summary>
        public void Nudge(float degrees) => m_Speed += degrees * Mathf.Deg2Rad * Mathf.Sqrt(Physics.gravity.magnitude / Length);

        void Gusts()
        {
            float now = Time.time;
            if (m_NextGust < 0f) m_NextGust = now + 3f + (float)m_Random.NextDouble() * 3f;
            if (now < m_NextGust) return;
            m_NextGust = now + GustGapMin + (float)m_Random.NextDouble() * (GustGapMax - GustGapMin);
            float strength = GustDegrees * (0.6f + 0.4f * (float)m_Random.NextDouble());
            Nudge(m_Random.NextDouble() < 0.5 ? strength : -strength);
        }

        // A hand moving into the card's face carries it: the card's bottom moves with the hand, then swings on.
        void Pushes(float dt)
        {
            for (int hand = 0; hand < 2; hand++)
            {
                if (!InstrumentInputRouter.TryGetPalm(hand, out var palm))
                {
                    m_HadPalm[hand] = false;
                    continue;
                }
                var before = m_LastPalm[hand];
                bool had = m_HadPalm[hand];
                m_LastPalm[hand] = palm;
                m_HadPalm[hand] = true;
                if (had) Push(palm, (palm - before) / dt);
            }
        }

        /// <summary>A hand at <paramref name="palm"/> moving at <paramref name="velocity"/> (scene space) against the card.</summary>
        public bool Push(Vector3 palm, Vector3 velocity)
        {
            // the card's face, in the hanging card's own space (its middle at the origin)
            var local = transform.InverseTransformPoint(palm) + new Vector3(0f, cardCentreBelow, -cardFaceZ);
            float scale = Mathf.Max(1e-4f, transform.lossyScale.y);
            if (Mathf.Abs(local.x) > halfSize.x || Mathf.Abs(local.y) > halfSize.y || Mathf.Abs(local.z) * scale > TouchDepth) return false;
            float below = Mathf.Max(0.05f, cardCentreBelow - local.y) * scale; // the touch's distance under the rail (m)
            float into = Vector3.Dot(velocity, transform.forward);             // toward +Z: away from the player
            float carried = -into / below;                                     // the swing speed that keeps up with the hand
            if (into > 0f && m_Speed > carried) m_Speed = carried;
            else if (into < 0f && m_Speed < carried) m_Speed = carried;
            else return false;
            return true;
        }
    }
}
