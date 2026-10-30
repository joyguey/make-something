using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>The title card's Start button: a tap begins the game (its tap collider is on its visible leaf).</summary>
    public class TitleStartButton : MonoBehaviour
    {
        public void Press() => TitleScreen.Instance?.Begin();
    }
}
