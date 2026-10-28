using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// Switches the controls to the player's language once the start delay has passed (changing text in
    /// the first frames risks PICO Spatial keeping the exported English).
    /// </summary>
    public class LanguageDirector : MonoBehaviour
    {
        void Update()
        {
            if (Time.timeSinceLevelLoad < JellyBody.StartDelaySeconds) return;
            Localization.Set(Localization.Preferred(), remember: false);
            enabled = false;
        }
    }
}
