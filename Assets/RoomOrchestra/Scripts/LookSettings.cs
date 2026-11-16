using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// The game's painted look (owner, Fable refinement 2026-11-11: option B, "keep the rim on characters and furniture
    /// only and leave it off the ground"): RoomOrchestra/LitBakedAO lights every baked shape as painted light (a lit side
    /// and a shade side with a soft edge), a warm rim of light round the characters and furniture, and the scene's haze
    /// fades the far meadow into the sky. All per pixel and cheap; no post-processing. Set as the scene loads, and in
    /// the editor, so snapshots show the same look.
    /// </summary>
    [ExecuteAlways]
    public class LookSettings : MonoBehaviour
    {
        public const float Painted = 1f, Rim = 0.5f;
        public static readonly Color RimColour = new Color(1f, 0.93f, 0.8f);
        public const float HazeStart = 10f, HazeEnd = 35f;

        void OnEnable() => Apply();

        /// <summary>Sets the look for every material (globals).</summary>
        public static void Apply()
        {
            Shader.SetGlobalFloat("_RO_Toon", Painted);
            Shader.SetGlobalFloat("_RO_Rim", Rim);
            Shader.SetGlobalColor("_RO_RimColor", RimColour);
        }
    }
}
