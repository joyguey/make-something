using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// Select-and-place: choose an orb, choose a target, place. Produces exactly the same
    /// <see cref="MusicalHitEvent"/> a throw would, through <see cref="MusicalHitPipeline"/>.
    /// Works from taps (tray tokens, furniture), controller buttons, and editor keys, one-handed.
    /// </summary>
    public class OrbSelector : MonoBehaviour
    {
        [Tooltip("Targets in left-to-right order; controller up/down steps through them.")]
        [SerializeField] InstrumentTarget[] targets;
        [Tooltip("One marker per target (same order), shown above the focused target.")]
        [SerializeField] Transform[] focusMarkers;
        [SerializeField] Vector3 focusMarkerScale = new Vector3(0.14f, 0.006f, 0.14f);
        [SerializeField] OrbToken[] tokens;
        [SerializeField] TierButton tierButton;
        [SerializeField] BallLauncher launcher;

        [Header("Initial state (authored in the scene to match, so nothing changes at startup)")]
        [SerializeField] OrbType startOrb = OrbType.Flutter;
        [SerializeField] int startTarget = 1;
        [SerializeField] IntensityTier startTier = IntensityTier.Medium;
        [Tooltip("Pitch petal used when placing (0 = lowest, 4 = highest) until petals get their own UI.")]
        [SerializeField, Range(0, PitchInput.PetalCount - 1)] int placePetal = 2;

        SelectionModel m_Model;

        public static OrbSelector Instance { get; private set; }

        public SelectionModel Model => m_Model ??= CreateModel();
        public OrbType SelectedOrb => Model.SelectedOrb;
        public IntensityTier Tier => Model.Tier;
        public InstrumentTarget FocusedTarget =>
            targets != null && targets.Length > 0 ? targets[Model.FocusedTarget] : null;

        SelectionModel CreateModel()
        {
            var model = new SelectionModel(targets != null ? targets.Length : 1, startOrb, startTarget, startTier);
            model.Changed += Refresh;
            return model;
        }

        void Awake()
        {
            Instance = this;
            _ = Model;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Wires references when built from code (tests); scenes set them in the inspector.</summary>
        public void Configure(InstrumentTarget[] orderedTargets, BallLauncher ballLauncher)
        {
            targets = orderedTargets;
            launcher = ballLauncher;
            m_Model = null;
            _ = Model;
        }

        public void Select(OrbType orb) => Model.SelectOrb(orb);
        public void CycleOrb(int direction) => Model.CycleOrb(direction);
        public void CycleTarget(int direction) => Model.CycleTarget(direction);
        public void CycleTier() => Model.CycleTier();
        public void SetTier(IntensityTier tier) => Model.SetTier(tier);

        public void Focus(InstrumentTarget target)
        {
            int index = System.Array.IndexOf(targets, target);
            if (index >= 0) Model.FocusTarget(index);
        }

        /// <summary>Places the selected orb on the focused target.</summary>
        public MusicalHitEvent PlaceOnFocused() => PlaceOn(FocusedTarget);

        /// <summary>Places the selected orb on <paramref name="target"/> at the default petal.</summary>
        public MusicalHitEvent PlaceOn(InstrumentTarget target) => PlaceOn(target, PitchInput.FromPetal(placePetal));

        public MusicalHitEvent PlaceOn(InstrumentTarget target, PitchInput pitch)
        {
            if (target == null) return null;
            Focus(target);
            long action = MusicalHitPipeline.BeginAction();
            return MusicalHitPipeline.Submit(action, target, Model.SelectedOrb, Model.Tier, pitch, InputPath.Place);
        }

        public void Execute(ControllerCommand command)
        {
            switch (command)
            {
                case ControllerCommand.NextOrb: CycleOrb(1); break;
                case ControllerCommand.PreviousOrb: CycleOrb(-1); break;
                case ControllerCommand.NextTarget: CycleTarget(1); break;
                case ControllerCommand.PreviousTarget: CycleTarget(-1); break;
                case ControllerCommand.Place: PlaceOnFocused(); break;
                case ControllerCommand.CycleIntensity: CycleTier(); break;
            }
        }

        // Visuals only change after a real selection change, never at startup.
        void Refresh()
        {
            if (tokens != null)
                foreach (var token in tokens)
                    if (token != null) token.SetSelected(token.Orb == Model.SelectedOrb);

            if (focusMarkers != null)
                for (int i = 0; i < focusMarkers.Length; i++)
                {
                    if (focusMarkers[i] == null) continue;
                    Vector3 scale = i == Model.FocusedTarget ? focusMarkerScale : focusMarkerScale * 0.001f;
                    if (focusMarkers[i].localScale != scale) focusMarkers[i].localScale = scale;
                }

            if (tierButton != null) tierButton.Show(Model.Tier);
            if (launcher != null) launcher.SetOrb(Model.SelectedOrb);
        }
    }
}
