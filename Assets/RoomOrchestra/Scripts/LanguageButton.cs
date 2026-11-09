using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// Tappable language switch, English / 中文. Its label (a <see cref="LocalizedLabel"/>) names the
    /// language a tap switches to, written in that language, so a player who reads only one of them can
    /// still find it. The choice is remembered on this headset.
    /// </summary>
    // Its tap collider sits on its visible slab, where PICO's hover highlight can show.
    public class LanguageButton : MonoBehaviour
    {
        public void Press() => Localization.Toggle();
    }
}
