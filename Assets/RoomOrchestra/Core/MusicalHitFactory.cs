namespace RoomOrchestra
{
    /// <summary>
    /// The single gate every input path goes through: deduplicates, then stamps a stable id.
    /// Returns null when the contact is a duplicate of an action that already produced an event.
    /// </summary>
    public sealed class MusicalHitFactory
    {
        long m_NextEventId = 1;
        long m_NextActionId = 1;

        public CollisionDeduplicator Deduplicator { get; } = new();
        public long EventCount => m_NextEventId - 1;

        /// <summary>Allocates an id for a new intended action (one throw or one place press).</summary>
        public long BeginAction() => m_NextActionId++;

        public MusicalHitEvent TryCreate(long actionId, InstrumentType target, OrbType orb,
            IntensityTier tier, PitchInput pitch, InputPath path, double dspTime)
        {
            if (!Deduplicator.TryAcceptAction(actionId)) return null;
            return new MusicalHitEvent(m_NextEventId++, actionId, target, orb, tier, pitch, path, dspTime);
        }

        public void Reset()
        {
            Deduplicator.Reset();
            m_NextEventId = 1;
            m_NextActionId = 1;
        }
    }
}
