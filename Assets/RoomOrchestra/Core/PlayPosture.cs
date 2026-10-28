namespace RoomOrchestra
{
    public enum PlayPosture
    {
        Seated,
        Standing
    }

    /// <summary>
    /// Height setup. The stage is laid out for a seated player (eyes ~1.15 m); standing lifts every
    /// control (tray, buttons, pitch columns) together so the player does not stoop to reach them.
    /// Furniture stays on the floor. The lift is a preset, not read from the headset: whether the
    /// Spatial scene origin is the floor on a real headset is not verified yet.
    /// </summary>
    public static class PostureLayout
    {
        /// <summary>
        /// Standing hands sit ~0.3 m higher than seated ones, so lifting by the same amount keeps every
        /// hand-to-control pointer line as clear as seated (0.4 m let the tray shadow the udu volume tile).
        /// </summary>
        public const float StandingLift = 0.3f;

        public static float ControlsLift(PlayPosture posture) => posture == PlayPosture.Standing ? StandingLift : 0f;

        public static float EyeHeight(PlayPosture posture) => posture == PlayPosture.Standing ? 1.6f : 1.15f;

        public static ReachEnvelope Reach(PlayPosture posture) =>
            posture == PlayPosture.Standing ? ReachEnvelope.Standing : ReachEnvelope.Seated;

        public static PlayPosture Next(PlayPosture posture) =>
            posture == PlayPosture.Seated ? PlayPosture.Standing : PlayPosture.Seated;
    }
}
