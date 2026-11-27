using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// Tappable mini echo on an orbit ring (its tap collider is on its body mesh, for the hover highlight). Its collider
    /// is a trigger: the pointer's ray still finds it (InstrumentInputRouter casts with QueryTriggerInteraction.Collide),
    /// but a thrown friend passes through (PICO review of v5.0.1: "the rotating slime circle has collision with the
    /// tossed slime" and blocked later throws; owner 2026-11-26: fix it).
    /// </summary>
    public class EchoMini : MonoBehaviour
    {
        [SerializeField] OrbitView orbit;

        public OrbitView Orbit => orbit;

        public void Configure(OrbitView owner) => orbit = owner;

        void Awake()
        {
            foreach (var c in GetComponentsInChildren<Collider>(true)) c.isTrigger = true;
        }
    }
}
