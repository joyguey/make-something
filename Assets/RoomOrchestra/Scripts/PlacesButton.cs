using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>Menu: choose again where each instrument lives (the saved places are forgotten).</summary>
    public class PlacesButton : MonoBehaviour, IPressable
    {
        public void Press()
        {
            MenuTray.Instance?.SetOpen(false);
            var placer = RealFurniture.Instance;
            if (placer == null) return;
            Debug.Log("[RoomOrchestra] Places: choosing again where the instruments live");
            placer.ChooseAgain();
        }
    }
}
