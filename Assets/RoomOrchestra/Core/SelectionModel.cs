using System;

namespace RoomOrchestra
{
    /// <summary>
    /// State for the select-and-place path: which orb is chosen, which target has focus, and the
    /// dynamics preset. Every change is one discrete step, so no fine motor control is needed.
    /// </summary>
    public sealed class SelectionModel
    {
        readonly int m_TargetCount;

        public OrbType SelectedOrb { get; private set; }
        public int FocusedTarget { get; private set; }
        public IntensityTier Tier { get; private set; }

        public event Action Changed;

        public SelectionModel(int targetCount, OrbType orb = OrbType.Flutter, int focusedTarget = 0,
            IntensityTier tier = IntensityTier.Medium)
        {
            m_TargetCount = Math.Max(1, targetCount);
            SelectedOrb = orb;
            FocusedTarget = Wrap(focusedTarget, m_TargetCount);
            Tier = tier;
        }

        public int TargetCount => m_TargetCount;

        public void SelectOrb(OrbType orb)
        {
            if (SelectedOrb == orb) return;
            SelectedOrb = orb;
            Changed?.Invoke();
        }

        public void CycleOrb(int direction) =>
            SelectOrb((OrbType)Wrap((int)SelectedOrb + Math.Sign(direction), OrbCatalog.Count));

        public void FocusTarget(int index)
        {
            index = Wrap(index, m_TargetCount);
            if (FocusedTarget == index) return;
            FocusedTarget = index;
            Changed?.Invoke();
        }

        public void CycleTarget(int direction) => FocusTarget(FocusedTarget + Math.Sign(direction));

        public void SetTier(IntensityTier tier)
        {
            if (Tier == tier) return;
            Tier = tier;
            Changed?.Invoke();
        }

        /// <summary>Quiet -> Medium -> Strong -> Quiet.</summary>
        public void CycleTier() => SetTier((IntensityTier)Wrap((int)Tier + 1, 3));

        static int Wrap(int value, int count) => ((value % count) + count) % count;
    }
}
