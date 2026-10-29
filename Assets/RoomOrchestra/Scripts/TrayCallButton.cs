using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// The leaf that calls the pad (option C): one tap from anywhere in the room brings the ball tray,
    /// the buttons and the ball stand to the player, or sends them home if the player is at the seat.
    /// </summary>
    // Its tap collider sits on its visible slab, where PICO's hover highlight can show.
    public class TrayCallButton : MonoBehaviour
    {
        public void Press()
        {
            if (RoamingTray.Instance != null) RoamingTray.Instance.Call();
        }
    }
}
