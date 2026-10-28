using System.Collections.Generic;
using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// Turns musical events into reactions: the tray character hops and says "oh"; the new echo hops
    /// and looks surprised the first time a character lands on an object; a second one of the same
    /// kind hops and glances at its sibling (which glances back). Minis are assigned by the orbits a
    /// frame after planting, so reactions wait for them. (Each mini also hops when its note plays: OrbitView.)
    /// </summary>
    public class CharacterDirector : MonoBehaviour
    {
        [SerializeField] EchoConductor conductor;
        [SerializeField] OrbToken[] tokens;

        readonly List<(long id, Expression expression, long siblingId)> m_Pending = new();

        public void Configure(EchoConductor echoConductor, OrbToken[] trayTokens)
        {
            conductor = echoConductor;
            tokens = trayTokens;
        }

        void OnEnable()
        {
            if (conductor != null) conductor.Planted += OnPlanted;
        }

        void OnDisable()
        {
            if (conductor != null) conductor.Planted -= OnPlanted;
        }

        void OnPlanted(EchoEvent echo)
        {
            var expression = CharacterReactions.OnPlanted(conductor.Sequencer.Echoes, echo, out long sibling);
            m_Pending.Add((echo.StableId, expression, sibling));
            if (tokens == null) return;
            foreach (var token in tokens)
                if (token != null && token.Orb == echo.Orb) React(token.transform, Expression.Curious);
        }

        void LateUpdate()
        {
            if (conductor == null) return;
            for (int i = m_Pending.Count - 1; i >= 0; i--)
            {
                var (id, expression, siblingId) = m_Pending[i];
                var mini = FaceFor(id, out var orbit);
                if (mini == null)
                {
                    if (conductor.Sequencer.Find(id) == null) m_Pending.RemoveAt(i); // removed before it showed
                    continue;
                }
                m_Pending.RemoveAt(i);
                var sibling = siblingId != 0 ? FaceFor(siblingId, out _) : null;
                if (expression == Expression.Sibling && sibling != null)
                {
                    float side = Side(orbit, sibling.transform) - Side(orbit, mini.transform);
                    React(mini.transform, Expression.Sibling, Mathf.Sign(side));
                    React(sibling.transform, Expression.Sibling, -Mathf.Sign(side));
                }
                else
                {
                    React(mini.transform, Expression.Curious);
                }
            }
        }

        static void React(Transform character, Expression expression, float glance = 0f)
        {
            if (character.TryGetComponent<CharacterFace>(out var face)) face.Show(expression, glance);
            if (character.TryGetComponent<JellyBody>(out var body)) body.Hop();

        }

        // Left/right as the player sees the ring.
        static float Side(OrbitView orbit, Transform t) => orbit.transform.InverseTransformPoint(t.position).x;

        CharacterFace FaceFor(long echoId, out OrbitView orbit)
        {
            orbit = null;
            foreach (var o in conductor.Orbits)
            {
                var mini = o != null ? o.MiniFor(echoId) : null;
                if (mini == null) continue;
                orbit = o;
                return mini.TryGetComponent<CharacterFace>(out var face) ? face : null;
            }
            return null;
        }
    }
}
