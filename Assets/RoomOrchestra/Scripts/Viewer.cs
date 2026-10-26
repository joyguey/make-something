using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// Where the player's head is, once per frame: the headset pose on device (StageAnchor), the main
    /// camera in the Editor, or else the seated start position on the stage.
    /// </summary>
    public static class Viewer
    {
        static int s_Frame = -1;
        static Vector3 s_Position;

        public static readonly Vector3 SeatedEye = new Vector3(0f, 1.15f, 0f);

        public static bool TryGetPosition(out Vector3 position)
        {
            if (s_Frame != Time.frameCount)
            {
                s_Frame = Time.frameCount;
                var anchor = StageAnchor.Instance;
                if (anchor != null && anchor.TryGetHeadPose(out var pose)) s_Position = pose.position;
                else if (anchor != null) s_Position = anchor.transform.TransformPoint(SeatedEye);
                else s_Position = SeatedEye;
            }
            position = s_Position;
            return true;
        }
    }
}
