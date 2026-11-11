using System;
using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// The meadow's time of day, from the headset's clock (owner, Fable refinement 2026-11-11: "A for morning and C for
    /// night time depending on the headset's time"): golden hour from 06:00 until 18:00, dusk the rest of the night.
    /// Both palettes are the same shapes (ArtSource/Meadow/build_meadow.py), so this only swaps each piece's mesh and
    /// the clouds' look, once, as the scene starts. Nothing else (the real room, the UI, the characters) changes.
    /// </summary>
    public class MeadowTimeOfDay : MonoBehaviour
    {
        public const int DayStartsAt = 6, DuskStartsAt = 18;

        [SerializeField] MeshFilter[] pieces = Array.Empty<MeshFilter>();
        [SerializeField] Mesh[] dayMeshes = Array.Empty<Mesh>();
        [SerializeField] Mesh[] duskMeshes = Array.Empty<Mesh>();
        [SerializeField] Renderer[] clouds = Array.Empty<Renderer>();
        [SerializeField] Material dayCloud, duskCloud;

        /// <summary>Set to force a time of day (tests, snapshots, demo recordings); null follows the clock.</summary>
        public static bool? ForceDusk;

        public bool IsDusk { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => ForceDusk = null;

        /// <summary>True from <see cref="DuskStartsAt"/> o'clock until <see cref="DayStartsAt"/> the next morning.</summary>
        public static bool IsDuskAt(int hour) => hour < DayStartsAt || hour >= DuskStartsAt;

        public void Configure(MeshFilter[] filters, Mesh[] day, Mesh[] dusk, Renderer[] cloudRenderers, Material cloudDay, Material cloudDusk)
        {
            pieces = filters;
            dayMeshes = day;
            duskMeshes = dusk;
            clouds = cloudRenderers;
            dayCloud = cloudDay;
            duskCloud = cloudDusk;
        }

        void Awake() => Apply(ForceDusk ?? IsDuskAt(DateTime.Now.Hour));

        /// <summary>Shows the meadow at dusk (<paramref name="dusk"/>) or in golden hour.</summary>
        public void Apply(bool dusk)
        {
            IsDusk = dusk;
            var meshes = dusk ? duskMeshes : dayMeshes;
            for (int i = 0; i < pieces.Length && i < meshes.Length; i++)
                if (pieces[i] != null && meshes[i] != null) pieces[i].sharedMesh = meshes[i];
            var look = dusk ? duskCloud : dayCloud;
            if (look != null)
                foreach (var c in clouds)
                    if (c != null) c.sharedMaterial = look;
        }
    }
}
