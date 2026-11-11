using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>The instrument icons' meshes (in <see cref="InstrumentType"/> order) and their look, for every <see cref="InstrumentIcon"/>.</summary>
    public class InstrumentIconLibrary : MonoBehaviour
    {
        [SerializeField] Mesh[] icons;
        [SerializeField] Material look;

        public static InstrumentIconLibrary Instance { get; private set; }
        public Material Look => look;

        public void Configure(Mesh[] meshes, Material material)
        {
            icons = meshes;
            look = material;
        }

        public Mesh MeshFor(InstrumentType type) => icons != null && (int)type < icons.Length ? icons[(int)type] : null;

        void Awake() => Instance = this;
        void OnEnable() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
