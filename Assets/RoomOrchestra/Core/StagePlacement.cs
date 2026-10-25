using System;

namespace RoomOrchestra
{
    /// <summary>Where the stage's start point goes on the floor plan, and which way it faces.</summary>
    public readonly struct StagePose
    {
        public float X { get; }
        public float Z { get; }
        public float YawDegrees { get; }

        public StagePose(float x, float z, float yawDegrees)
        {
            X = x;
            Z = z;
            YawDegrees = yawDegrees;
        }
    }

    /// <summary>
    /// Recenter for seated play: the stage was laid out for a player at its origin facing +Z, so
    /// recentering puts that origin under the player's head and turns the stage to the way they face.
    /// Height is left alone so furniture stays on the floor. Looking straight down or up still gives
    /// a heading (from the top of the head), so the player never has to sit up straight to recenter.
    /// </summary>
    public static class StagePlacement
    {
        /// <summary>Below this horizontal share of the gaze, the head's up vector gives the heading.</summary>
        public const float MinFlatForward = 0.3f;

        public static bool TryRecenter(float headX, float headZ,
            float forwardX, float forwardY, float forwardZ,
            float upX, float upY, float upZ, out StagePose pose)
        {
            float x = forwardX, z = forwardZ;
            if (Math.Sqrt(x * x + z * z) < MinFlatForward)
            {
                // Looking down tips the top of the head forward; looking up tips it back.
                float sign = forwardY < 0f ? 1f : -1f;
                x = upX * sign;
                z = upZ * sign;
            }
            if (Math.Sqrt(x * x + z * z) < 1e-3)
            {
                pose = default;
                return false;
            }
            pose = new StagePose(headX, headZ, (float)(Math.Atan2(x, z) * 180.0 / Math.PI));
            return true;
        }
    }
}
