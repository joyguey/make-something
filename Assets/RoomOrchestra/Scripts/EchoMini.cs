using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>Tappable mini echo on an orbit ring (its tap collider is on its body mesh, for the hover highlight).</summary>
    public class EchoMini : MonoBehaviour
    {
        [SerializeField] OrbitView orbit;

        public OrbitView Orbit => orbit;

        public void Configure(OrbitView owner) => orbit = owner;
    }
}
