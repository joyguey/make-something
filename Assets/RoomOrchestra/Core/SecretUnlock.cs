using System.Collections.Generic;

namespace RoomOrchestra
{
    /// <summary>
    /// The Echo Battle's easter egg: pick the three friends in order, Flutter, Bop, Boom, twice, within
    /// <see cref="WindowSeconds"/>. Fed with every change of the selected character, so a tap on a character and the
    /// controller's next-character button both count. Pure rules.
    /// </summary>
    public sealed class SecretUnlock
    {
        public const float WindowSeconds = 6f;
        static readonly OrbType[] Pattern = { OrbType.Flutter, OrbType.Bop, OrbType.Boom, OrbType.Flutter, OrbType.Bop, OrbType.Boom };

        readonly List<(OrbType orb, float time)> m_Recent = new();

        /// <summary>A new selection at <paramref name="time"/> (seconds); true when it completes the pattern.</summary>
        public bool Selected(OrbType orb, float time)
        {
            m_Recent.Add((orb, time));
            if (m_Recent.Count > Pattern.Length) m_Recent.RemoveAt(0);
            if (m_Recent.Count < Pattern.Length) return false;
            for (int i = 0; i < Pattern.Length; i++)
                if (m_Recent[i].orb != Pattern[i]) return false;
            if (time - m_Recent[0].time > WindowSeconds) return false;
            m_Recent.Clear();
            return true;
        }
    }
}
