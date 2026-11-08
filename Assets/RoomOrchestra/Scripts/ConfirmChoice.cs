using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>One of the confirm card's two buttons.</summary>
    public class ConfirmChoice : MonoBehaviour
    {
        [SerializeField] bool yes;
        public bool Yes => yes;
        public void Configure(bool doIt) => yes = doIt;
    }
}
