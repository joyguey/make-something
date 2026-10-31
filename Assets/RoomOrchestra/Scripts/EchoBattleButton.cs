using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>A button on the Echo Battle card: Host, Join or Leave (its tap collider is on its visible slab).</summary>
    public class EchoBattleButton : MonoBehaviour
    {
        public enum Kind { Host, Join, Leave }

        [SerializeField] Kind kind;

        public Kind ButtonKind => kind;

        public void Configure(Kind buttonKind) => kind = buttonKind;

        public void Press() => EchoBattleDirector.Instance?.Press(kind);
    }
}
