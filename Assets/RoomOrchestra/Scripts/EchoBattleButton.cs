using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>A button on the Echo Battle card: Host, Join, Solo (against the game, on this headset) or Leave (its tap
    /// collider is on its visible slab).</summary>
    public class EchoBattleButton : MonoBehaviour
    {
        public enum Kind { Host, Join, Leave, Solo } // appended: saved by number

        [SerializeField] Kind kind;

        public Kind ButtonKind => kind;

        public void Configure(Kind buttonKind) => kind = buttonKind;

        public void Press() => EchoBattleDirector.Instance?.Press(kind);
    }
}
