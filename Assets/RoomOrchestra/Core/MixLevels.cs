namespace RoomOrchestra
{
    /// <summary>
    /// Per-instrument volume, one tap per step: 100% -> 60% -> 30% -> Muted -> 100%. Discrete steps
    /// instead of a slider, so no drag or fine motor control is needed. Master volume is the headset's
    /// own volume keys.
    /// </summary>
    public static class MixLevels
    {
        static readonly float[] s_Gains = { 1f, 0.6f, 0.3f, 0f };

        public static int Count => s_Gains.Length;

        public static float Gain(int level) => s_Gains[Wrap(level)];

        public static int Next(int level) => Wrap(level + 1);

        public static bool IsMuted(int level) => Gain(level) <= 0f;

        public static string Label(int level) =>
            IsMuted(level) ? "Muted" : $"Vol {(int)(Gain(level) * 100f + 0.5f)}%";

        static int Wrap(int level) => ((level % s_Gains.Length) + s_Gains.Length) % s_Gains.Length;
    }
}
