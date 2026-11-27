using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>One of the title card's choices (Story, Sandbox, Play together): a tap, or a high five on its print,
    /// begins the game that way (its tap collider is on its visible leaf).</summary>
    public class TitleStartButton : MonoBehaviour
    {
        [SerializeField] TitleChoice choice = TitleChoice.Sandbox;

        public TitleChoice Choice => choice;

        public void Configure(TitleChoice which) => choice = which;

        public void Press() => TitleScreen.Instance?.Begin(choice);
    }
}
