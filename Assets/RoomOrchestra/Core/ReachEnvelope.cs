using System;

namespace RoomOrchestra
{
    public enum ReachResult
    {
        Ok,
        TooLow,
        TooHigh,
        TooClose,
        TooFar,
        OutsideView
    }

    /// <summary>
    /// Comfortable interaction zone relative to the player's start position (origin, facing +Z).
    /// Objects are selected with a pointer ray, so "reach" means: visible without turning, never
    /// overhead or at the floor, and within ray distance. Distances are horizontal (meters).
    /// MinDistance stays at 1 m: PICO Spatial drew props closer than that incorrectly in this project.
    /// </summary>
    public readonly struct ReachEnvelope
    {
        public float MinHeight { get; }
        public float MaxHeight { get; }
        public float MinDistance { get; }
        public float MaxDistance { get; }
        public float MaxYawDegrees { get; }

        public ReachEnvelope(float minHeight, float maxHeight, float minDistance, float maxDistance, float maxYawDegrees)
        {
            MinHeight = minHeight;
            MaxHeight = maxHeight;
            MinDistance = minDistance;
            MaxDistance = maxDistance;
            MaxYawDegrees = maxYawDegrees;
        }

        /// <summary>Seated: eye height ~1.15 m; nothing above eye-level reach or below shin height.</summary>
        public static ReachEnvelope Seated => new ReachEnvelope(0.3f, 1.4f, 1.0f, 3.0f, 35f);

        public static ReachEnvelope Standing => new ReachEnvelope(0.3f, 1.8f, 1.0f, 3.5f, 45f);

        public ReachResult Check(float x, float y, float z)
        {
            if (y < MinHeight) return ReachResult.TooLow;
            if (y > MaxHeight) return ReachResult.TooHigh;
            double distance = Math.Sqrt(x * x + z * z);
            if (distance < MinDistance) return ReachResult.TooClose;
            if (distance > MaxDistance) return ReachResult.TooFar;
            double yaw = Math.Abs(Math.Atan2(x, z) * 180.0 / Math.PI);
            return yaw > MaxYawDegrees ? ReachResult.OutsideView : ReachResult.Ok;
        }
    }
}
