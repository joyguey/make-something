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
        [Tooltip("One marker that moves above the focused target. It is never hidden by scaling: PICO Spatial " +
                 "exports scene poses, and near-zero scales there drew objects in the wrong place.")]
        [SerializeField] Transform focusMarker;
        [SerializeField] float focusMarkerLift = 0.3f;
        [SerializeField] OrbToken[] tokens;
        [SerializeField] TierButton tierButton;
        [Tooltip("Pitch columns (one per target); the focused target's column shows the selected pitch.")]
        [SerializeField] PitchColumn[] pitchColumns;
        [SerializeField] BallLauncher launcher;

        [Header("Initial state (authored in the scene to match, so nothing changes at startup)")]
        [SerializeField] OrbType startOrb = OrbType.Flutter;
        [SerializeField] int startTarget = 1;
        [SerializeField] IntensityTier startTier = IntensityTier.Medium;
        [Tooltip("Pitch petal selected at start (0 = lowest note / first udu sound, 4 = highest).")]
        [SerializeField, Range(0, PitchInput.PetalCount - 1)] int startPetal = 2;

        SelectionModel m_Model;

        public static OrbSelector Instance { get; private set; }

        public SelectionModel Model => m_Model ??= CreateModel();
        public OrbType SelectedOrb => Model.SelectedOrb;
        public IntensityTier Tier => Model.Tier;
        public int Petal => Model.Petal;
        public InstrumentTarget FocusedTarget =>
            targets != null && targets.Length > 0 ? targets[Model.FocusedTarget] : null;

        SelectionModel CreateModel()
        {
            var model = new SelectionModel(targets != null ? targets.Length : 1, startOrb, startTarget, startTier, startPetal);
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
        /// <summary>Moves the focus to the next target, passing over any the guided song still keeps hidden.</summary>
        public void CycleTarget(int direction)
        {
            int count = targets != null ? targets.Length : 1;
            for (int i = 0; i < count; i++)
            {
                Model.CycleTarget(direction);
                if (FocusedTarget == null || !StationReveal.IsHidden(FocusedTarget.Instrument)) return;
            }
        }
        public void CycleTier() => Model.CycleTier();
        public void SetTier(IntensityTier tier) => Model.SetTier(tier);
        public void CyclePetal() => Model.CyclePetal();
        public void SetPetal(int petal) => Model.SetPetal(petal);

        public void Focus(InstrumentTarget target)
        {
            int index = System.Array.IndexOf(targets, target);
            if (index >= 0) Model.FocusTarget(index);
        }

        /// <summary>Plays the tile's note on its target with the selected orb (and remembers that pitch).</summary>
        public MusicalHitEvent PlaceOnTile(PitchTile tile)
        {
            if (tile == null || tile.Target == null) return null;
            Model.SetPetal(tile.Petal);
            return PlaceOn(tile.Target, PitchInput.FromPetal(tile.Petal));
        }

        /// <summary>Places the selected orb on the focused target.</summary>
        public MusicalHitEvent PlaceOnFocused() => PlaceOn(FocusedTarget);

        /// <summary>Places the selected orb on <paramref name="target"/> at the selected petal.</summary>
        public MusicalHitEvent PlaceOn(InstrumentTarget target) => PlaceOn(target, PitchInput.FromPetal(Model.Petal));

        static bool? s_ThrowOnTap;
        /// <summary>
        /// A tapped note flies there as a ball (owner, 2026-11-07: "tapping the note seems to defeat the purpose of
        /// throwing the ball"); still one tap, no aim or strength needed. On by default in the app; the Editor's
        /// older tests set it off to check the direct path.
        /// </summary>
        public static bool ThrowOnTap
        {
            get => s_ThrowOnTap ?? !Application.isEditor;
            set => s_ThrowOnTap = value;
        }

        public MusicalHitEvent PlaceOn(InstrumentTarget target, PitchInput pitch)
        {
            if (target == null) return null;
            Focus(target);
            if (ThrowOnTap && launcher != null && launcher.TryThrowAt(target, Model.SelectedOrb, pitch, Model.Tier))
                return null; // the note plays when the ball lands
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
                case ControllerCommand.CyclePetal: CyclePetal(); break;
            }
        }

        // Visuals only change after a real selection change, never at startup.
        void Refresh()
        {
            if (tokens != null)
                foreach (var token in tokens)
                    if (token != null) token.SetSelected(token.Orb == Model.SelectedOrb);

            if (focusMarker != null && FocusedTarget != null)
            {
                Vector3 position = FocusedTarget.AimPosition + Vector3.up * focusMarkerLift;
                if (focusMarker.position != position) focusMarker.position = position;
            }

            if (tierButton != null) tierButton.Show(Model.Tier);
            if (pitchColumns != null)
                foreach (var column in pitchColumns)
                    if (column != null) column.Show(column.Target == FocusedTarget ? Model.Petal : -1);
            if (launcher != null) launcher.SetOrb(Model.SelectedOrb);
        }
    }
}
