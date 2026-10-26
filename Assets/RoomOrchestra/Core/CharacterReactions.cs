using System.Collections.Generic;

namespace RoomOrchestra
{
    public enum Expression
    {
        Idle,
        Curious,  // first of its kind on an object: wide eyes and an "oh"
        Sibling   // another of the same character already there: the two glance at each other
    }

    /// <summary>
    /// Which face a character pulls, from what the music is doing (docs/ART_AND_AUDIO_BIBLE.md:
    /// surprise on first hit, siblings glance and smile). Busy loops get livelier by themselves: every
    /// mini hops on its own note.
    /// </summary>
    public static class CharacterReactions
    {
        /// <summary>
        /// Reaction of the echo just planted. For a sibling, <paramref name="siblingId"/> is the most
        /// recent other echo of the same character on the same object.
        /// </summary>
        public static Expression OnPlanted(IReadOnlyList<EchoEvent> echoes, EchoEvent planted, out long siblingId)
        {
            siblingId = 0;
            for (int i = echoes.Count - 1; i >= 0; i--)
            {
                var e = echoes[i];
                if (e.StableId == planted.StableId || e.Target != planted.Target || e.Orb != planted.Orb) continue;
                siblingId = e.StableId;
                return Expression.Sibling;
            }
            return Expression.Curious;
        }
    }
}
