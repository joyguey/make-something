using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>Tappable mini echo on an orbit ring.</summary>
    [RequireComponent(typeof(Collider))]
    public class EchoMini : MonoBehaviour
    {
        [SerializeField] OrbitView orbit;

        public OrbitView Orbit => orbit;

        public void Configure(OrbitView owner) => orbit = owner;
    }
}
