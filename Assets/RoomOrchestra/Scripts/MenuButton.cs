using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>The leaf's Menu button: opens and closes the menu tray; its label says what a press will do.</summary>
    public class MenuButton : MonoBehaviour, IPressable
    {
        void OnEnable() => MenuTray.Toggled += Relabel;
        void OnDisable() => MenuTray.Toggled -= Relabel;

        public void Press() => MenuTray.Instance?.Toggle();

        void Relabel(bool open)
        {
            var label = GetComponentInChildren<LocalizedLabel>(true);
            if (label != null) label.SetTerm(open ? UiTerm.MenuClose : UiTerm.Menu);
        }
    }
}
