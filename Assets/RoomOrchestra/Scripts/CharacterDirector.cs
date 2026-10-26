using System.Collections.Generic;
using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// Turns musical events into faces: the tray character and the new echo look surprised the first
    /// time a character lands on an object; a second one of the same kind glances at its sibling (and
    /// the sibling glances back); a busy loop (CharacterReactions.ExcitedAt echoes or more) bounces on
    /// each new bar. Minis are assigned by the orbits a frame after planting, so reactions wait for them.
    /// </summary>
    public class CharacterDirector : MonoBehaviour
    {
        [SerializeField] EchoConductor conductor;
        [SerializeField] OrbToken[] tokens;

        readonly List<(long id, Expression expression, long siblingId)> m_Pending = new();
        long m_LastBar = long.MinValue;

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
                if (token != null && token.Orb == echo.Orb && token.TryGetComponent<CharacterFace>(out var face))
                    face.Show(Expression.Curious);
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
                    mini.Show(Expression.Sibling, Mathf.Sign(side));
                    sibling.Show(Expression.Sibling, -Mathf.Sign(side));
                }
                else
                {
                    mini.Show(Expression.Curious);
                }
            }

            long bar = conductor.Clock.BarIndex(AudioSettings.dspTime);
            if (bar == m_LastBar) return;
            m_LastBar = bar;
            if (conductor.IsPaused) return;
            foreach (var orbit in conductor.Orbits)
            {
                if (orbit == null || orbit.Target == null) continue;
                if (!CharacterReactions.IsExcited(conductor.Sequencer.CountOn(orbit.Target.Instrument))) continue;
                foreach (var echo in conductor.Sequencer.Echoes)
                {
                    var mini = echo.Target == orbit.Target.Instrument ? orbit.MiniFor(echo.StableId) : null;
                    if (mini != null && mini.TryGetComponent<CharacterFace>(out var face)) face.Bounce();
                }
            }
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
