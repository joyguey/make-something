using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>Menu: real things hide the game's things (PICO's depth sensing, or the scanned room), on or off.</summary>
    public class RealHidesButton : MonoBehaviour, IPressable
    {
        void Start() => Relabel();

        public void Press()
        {
            EnvironmentOcclusion.Wanted = !EnvironmentOcclusion.Wanted;
            Relabel();
        }

        void Relabel()
        {
            var label = GetComponentInChildren<LocalizedLabel>(true);
            if (label != null) label.SetTerm(EnvironmentOcclusion.Wanted ? UiTerm.RealHidesOn : UiTerm.RealHidesOff);
        }
    }
}
