using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// Quit (George, 2026-11-03: "a main menu with the app's name and a Quit button"). Asks "Sure?" first
    /// (<see cref="ConfirmGate"/>, via the input router), then closes the app. Its tap collider is on its visible slab.
    /// </summary>
    public class QuitButton : MonoBehaviour
    {
        /// <summary>Set by tests: counts quits instead of closing the app.</summary>
        public static int QuitRequests { get; set; }

        public void Press()
        {
            QuitRequests++;
            Debug.Log("[RoomOrchestra] Quit");
#if !UNITY_EDITOR
            Application.Quit();
#endif
        }
    }
}
