using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// Brings the rainforest to life as music is composed (rules in Core/JungleGrowth): once the song has
    /// a few notes, each note ADDED feeds its instrument's vine (table/udu, chair/mbira, lamp/erhu) and
    /// pops up another flower around the room; the loop repeating does not grow anything,
    /// toadstools sprout from buds one by one, butterflies flutter out of the lamp while the song is
    /// layered and melodic, and with every vine grown and all butterflies out the toadstools hop in a
    /// chorus. Clear sweeps it all away in big smoke clouds and the next song grows it again. Each new
    /// stage is logged so it can be checked on a device.
    /// </summary>
    public class JungleDirector : MonoBehaviour
    {
        [SerializeField] VineGrowth udu, mbira, erhu;
        [SerializeField] Toadstool[] toadstools;
        [SerializeField] FloorFlower[] flowers;
        [SerializeField] Butterfly[] butterflies;

        JungleGrowth m_Growth = new();
        bool m_Chorus;
        float m_VanishAt = -1f;

        /// <summary>How long after Clear the jungle vanishes: when the smoke is at its thickest.</summary>
        public const float VanishDelay = 0.2f;

        public JungleGrowth Growth => m_Growth;
        public bool InChorus => m_Chorus;

        public void Configure(VineGrowth uduVine, VineGrowth mbiraVine, VineGrowth erhuVine, Toadstool[] floor, Butterfly[] flock, FloorFlower[] bloom = null)
        {
            udu = uduVine;
            mbira = mbiraVine;
            erhu = erhuVine;
            toadstools = floor;
            butterflies = flock;
            flowers = bloom;
        }

        void OnEnable()
        {
            EchoConductor.NoteAdded += OnNoteAdded;
            EchoConductor.Cleared += OnCleared;
        }

        void OnDisable()
        {
            EchoConductor.NoteAdded -= OnNoteAdded;
            EchoConductor.Cleared -= OnCleared;
        }

        /// <summary>
        /// The song was cleared: big smoke clouds burst over everything that has grown, and under them the
        /// jungle goes back to bare buds (vines folded, toadstools buds, butterflies home). The next song
        /// grows it again from the start.
        /// </summary>
        void OnCleared()
        {
            int clouds = 0;
            foreach (var vine in new[] { udu, mbira, erhu })
                if (vine != null && vine.Shown > 0 && PoofCloud.PoofAt(vine.GrownCentre(), ClearSmokeSize)) clouds++;
            if (toadstools != null)
                foreach (var t in toadstools)
                    if (t != null && t.Sprouted && PoofCloud.PoofAt(t.transform.position, ClearSmokeSize * 0.6f)) clouds++;
            if (butterflies != null)
                foreach (var b in butterflies)
                    if (b != null && !b.AtHome && PoofCloud.PoofAt(b.transform.position, ClearSmokeSize * 0.5f)) clouds++;
            // Last, so the bigger growth gets the clouds first; when every cloud is busy a flower just goes.
            if (flowers != null)
                foreach (var f in flowers)
                    if (f != null && f.Sprouted && PoofCloud.PoofAt(f.transform.position, ClearSmokeSize * 0.5f)) clouds++;
            m_VanishAt = Time.time + VanishDelay;
            Debug.Log($"[RoomOrchestra] Jungle: cleared in a cloud of smoke ({clouds} clouds)");
        }

        /// <summary>
        /// After the lesson (owner, headset 2026-11-08: "the flowers and mushrooms are still there. they should also
        /// disappear and a clean slate should be available"): what the lesson's song grew goes in the same smoke as Clear.
        /// </summary>
        public void ClearForFreshStart() => OnCleared();

        /// <summary>Clear's smoke clouds, as a multiple of a ball's landing poof.</summary>
        public const float ClearSmokeSize = 4f;

        void Vanish()
        {
            m_VanishAt = -1f;
            m_Growth = new JungleGrowth();
            foreach (var vine in new[] { udu, mbira, erhu })
                if (vine != null) vine.ResetGrowth();
            if (flowers != null)
                foreach (var f in flowers)
                    if (f != null) f.Unsprout();
            RealWalls.Instance?.ReleaseFlowerSpots();
            if (toadstools != null)
                foreach (var t in toadstools)
                    if (t != null) t.Unsprout();
            if (butterflies != null)
                foreach (var b in butterflies)
                    if (b != null) b.SendHomeNow();
            m_Chorus = false;
        }

        void OnNoteAdded(EchoEvent echo) => NoteAdded(echo.Target);

        /// <summary>A note was added to the song on <paramref name="instrument"/>: the jungle grows a little.</summary>
        public void NoteAdded(InstrumentType instrument)
        {
            m_Growth.NoteAdded(instrument);
            // Every vine, not only this note's: when growth wakes up, vines fed earlier catch up together.
            foreach (var type in new[] { InstrumentType.Udu, InstrumentType.Mbira, InstrumentType.Erhu })
            {
                var vine = VineFor(type);
                if (vine != null && vine.SetProgress(m_Growth.VineProgress(type)))
                    Debug.Log($"[RoomOrchestra] Jungle: {type} vine {vine.Shown}/{vine.SegmentCount}{(vine.Bloomed ? ", in bloom" : "")}");
            }
            // Owner, headset 2026-11-08: "I haven't seen any mushrooms in my room anymore, just the flowers". In the
            // real room the toadstools stayed on the lesson's floor plan, often inside real furniture or under the
            // scanned floor, which now hides what is behind it. Like the flowers, they grow on a flat, clear real
            // surface the player can see (the lesson's room keeps them on its own floor).
            var room = VirtualRoom.Instance;
            bool realRoom = room == null || room.Gone;
            if (toadstools != null)
                for (int i = 0; i < m_Growth.MushroomsSprouted && i < toadstools.Length; i++)
                    if (toadstools[i] != null && !toadstools[i].Sprouted)
                    {
                        if (realRoom) PlaceOnRealSurface(toadstools[i]);
                        toadstools[i].Sprout();
                    }
            if (flowers != null)
                for (int i = 0; i < m_Growth.FlowersSprouted(flowers.Length); i++)
                    if (flowers[i] != null && !flowers[i].Sprouted)
                    {
                        PlaceOnRealSurface(flowers[i]);
                        flowers[i].Sprout();
                    }
        }

        // With a room scan, a flower grows on a flat, clear real surface near its own place (George, 2026-11-03),
        // moved while it is still an unseen speck; without one it keeps the place it was given.
        static void PlaceOnRealSurface(FloorSprout flower)
        {
            var walls = RealWalls.Instance;
            if (walls == null || !walls.TryTakeFlowerSpot(flower.transform.position, out var spot)) return;
            flower.transform.position = spot;
            Debug.Log($"[RoomOrchestra] Jungle: {flower.name} grows on a real surface at {spot:F2}");
        }

        VineGrowth VineFor(InstrumentType instrument) => instrument switch
        {
            InstrumentType.Udu => udu,
            InstrumentType.Mbira => mbira,
            _ => erhu,
        };

        // ---- What is drawn (PICO XR mode draws exactly what is enabled) ----
        // Owner, headset 2026-11-08: "when I first started the game, there were flower and vine artifacts already
        // appearing" and "in the distance, I can still see the vine and sparkle artifacts and they are not mapped to
        // anything". Things that have not grown waited as small specks (PICO Spatial drew switched-off objects, so
        // nothing could be switched off); now anything not grown is not drawn at all, and a station's sparkles show
        // only while its instrument is out.
        Renderer[][] m_SproutLooks, m_BloomLooks, m_FlyLooks;
        Renderer[][][] m_SegmentLooks; // [vine][segment]
        FloorSprout[] m_Sprouts;
        VineGrowth[] m_Vines;
        SparkleTwinkle[] m_Sparkles;
        Renderer[][] m_SparkleLooks;

        void CacheLooks()
        {
            if (m_Sprouts != null) return;
            var sprouts = new System.Collections.Generic.List<FloorSprout>();
            if (toadstools != null) foreach (var t in toadstools) if (t != null) sprouts.Add(t);
            if (flowers != null) foreach (var f in flowers) if (f != null) sprouts.Add(f);
            m_Sprouts = sprouts.ToArray();
            m_SproutLooks = System.Array.ConvertAll(m_Sprouts, s => s.GetComponentsInChildren<Renderer>(true));
            m_Vines = new[] { udu, mbira, erhu };
            m_SegmentLooks = new Renderer[m_Vines.Length][][];
            m_BloomLooks = new Renderer[m_Vines.Length][];
            for (int v = 0; v < m_Vines.Length; v++)
            {
                m_SegmentLooks[v] = new Renderer[0][];
                m_BloomLooks[v] = new Renderer[0];
                if (m_Vines[v] == null) continue;
                var segments = m_Vines[v].Segments;
                if (segments != null)
                {
                    m_SegmentLooks[v] = new Renderer[segments.Count][];
                    for (int i = 0; i < segments.Count; i++)
                        m_SegmentLooks[v][i] = segments[i] != null ? segments[i].GetComponentsInChildren<Renderer>(true) : new Renderer[0];
                }
                m_BloomLooks[v] = LooksOf(m_Vines[v].Flowers);
            }
            m_FlyLooks = new Renderer[butterflies != null ? butterflies.Length : 0][];
            for (int b = 0; b < m_FlyLooks.Length; b++)
            {
                var looks = new System.Collections.Generic.List<Renderer>();
                if (butterflies[b] != null)
                {
                    looks.AddRange(butterflies[b].GetComponentsInChildren<Renderer>(true));
                    if (butterflies[b].Trail != null)
                        foreach (var dot in butterflies[b].Trail) if (dot != null) looks.AddRange(dot.GetComponentsInChildren<Renderer>(true));
                }
                m_FlyLooks[b] = looks.ToArray();
            }
            m_Sparkles = GetComponentsInChildren<SparkleTwinkle>(true);
            m_SparkleLooks = System.Array.ConvertAll(m_Sparkles, s => s.GetComponentsInChildren<Renderer>(true));
        }

        // One renderer list per piece, in the piece's order.
        static Renderer[] LooksOf(System.Collections.Generic.IReadOnlyList<Transform> pieces)
        {
            var looks = new System.Collections.Generic.List<Renderer>();
            if (pieces != null) foreach (var p in pieces) if (p != null) looks.AddRange(p.GetComponentsInChildren<Renderer>(true));
            return looks.ToArray();
        }

        static void Show(Renderer[] looks, bool on)
        {
            if (looks == null) return;
            foreach (var r in looks) if (r != null && r.enabled != on) r.enabled = on;
        }

        void UpdateLooks()
        {
            CacheLooks();
            for (int i = 0; i < m_Sprouts.Length; i++) Show(m_SproutLooks[i], m_Sprouts[i] != null && m_Sprouts[i].Sprouted);
            for (int v = 0; v < m_Vines.Length; v++)
            {
                var vine = m_Vines[v];
                if (vine == null) continue;
                for (int i = 0; i < m_SegmentLooks[v].Length; i++) Show(m_SegmentLooks[v][i], i < vine.Shown); // grown pieces only
                Show(m_BloomLooks[v], vine.Bloomed);
            }
            for (int b = 0; b < m_FlyLooks.Length; b++) Show(m_FlyLooks[b], butterflies[b] != null && !butterflies[b].AtHome);
            for (int s = 0; s < m_Sparkles.Length; s++)
            {
                var target = m_Sparkles[s].Instrument;
                Show(m_SparkleLooks[s], target != null && !StationReveal.IsHidden(target.Instrument));
            }
            FollowRealHomes();
        }

        // ---- Growing around the real furniture ----
        // Owner, headset 2026-11-08: "Is it not possible to have the vines grow around the actual furniture object?"
        // When an instrument moves onto a real object, its vines, sparkles (and the lamp's butterflies) move with it:
        // each keeps its place relative to the virtual furniture's floor middle, stretched to the real object's size
        // (horizontally to its footprint, up to its height), so the table's two leg vines climb the real table's ends.
        sealed class Follow
        {
            public Transform Part;
            public Vector3 HomeLocal, HomeScale;
            public Quaternion HomeRotation;
            public bool Following;
            public Vector3 At; // world position while following
            public float Scale;
        }

        readonly System.Collections.Generic.Dictionary<string, Follow[]> m_Follows = new();

        static string[] PartsFor(string furniture) => furniture switch
        {
            "Table" => new[] { "Vine_TableL", "Vine_TableR", "Sparkles_Udu" },
            "Chair" => new[] { "Vine_Chair", "Sparkles_Mbira" },
            "Lamp" => new[] { "Vine_Lamp", "Sparkles_Erhu", "Flock" },
            _ => new string[0]
        };

        Follow[] FollowsFor(string furniture)
        {
            if (m_Follows.TryGetValue(furniture, out var list)) return list;
            var parts = new System.Collections.Generic.List<Follow>();
            foreach (var name in PartsFor(furniture))
            {
                var part = transform.Find(name);
                if (part == null) continue;
                parts.Add(new Follow { Part = part, HomeLocal = part.localPosition, HomeRotation = part.localRotation, HomeScale = part.localScale });
            }
            return m_Follows[furniture] = parts.ToArray();
        }

        /// <summary>
        /// <paramref name="furniture"/>'s jungle (vines, sparkles, butterflies) moves from the virtual piece (its floor
        /// middle <paramref name="virtualFloor"/> and size) onto a real object (its floor middle and size), in scene space.
        /// </summary>
        public void GrowAround(string furniture, Vector3 virtualFloor, Vector3 virtualSize, Vector3 realFloor, Vector3 realSize)
        {
            var stage = transform.parent != null ? transform.parent : transform;
            float up = Mathf.Clamp(realSize.y / Mathf.Max(0.05f, virtualSize.y), 0.35f, 2.5f);
            foreach (var f in FollowsFor(furniture))
            {
                // home pose first, then the offset from the virtual floor middle, in the stage's own axes
                var home = transform.TransformPoint(f.HomeLocal);
                var offset = Quaternion.Inverse(stage.rotation) * (home - virtualFloor);
                var across = Quaternion.Inverse(stage.rotation) * realSize;
                var virtualAcross = virtualSize;
                offset.x *= Mathf.Clamp(Mathf.Abs(across.x) / Mathf.Max(0.05f, virtualAcross.x), 0.3f, 3f);
                offset.z *= Mathf.Clamp(Mathf.Abs(across.z) / Mathf.Max(0.05f, virtualAcross.z), 0.3f, 3f);
                offset.y *= up;
                f.At = realFloor + stage.rotation * offset;
                f.Scale = up;
                if (f.Part.name == "Flock")
                {
                    // Owner, headset 2026-11-08: the butterflies weren't seen. Around a real lamp across the room (or on
                    // their lesson loops 2.4-2.9 m out) they flew behind real walls, which hide them. In the real room
                    // they loop in front of the player instead, at an easy height.
                    f.At = home + stage.rotation * FlockInRealRoom;
                    f.Scale = 1f;
                }
                f.Following = true;
            }
            FollowRealHomes();
            Debug.Log($"[RoomOrchestra] Jungle: the {furniture}'s vines and sparkles grow around the real object at {realFloor:F2} (x{up:F2} tall)");
        }

        /// <summary>How far the butterflies' loops move from the lesson's (stage axes, m): nearer and a little lower.</summary>
        public static readonly Vector3 FlockInRealRoom = new(0f, -0.25f, -1.3f);

        /// <summary>Back on the virtual furniture (the lesson again).</summary>
        public void GrowAtHome(string furniture)
        {
            foreach (var f in FollowsFor(furniture))
            {
                f.Following = false;
                f.Part.localPosition = f.HomeLocal;
                f.Part.localRotation = f.HomeRotation;
                f.Part.localScale = f.HomeScale;
            }
        }

        // Held in place in the room every frame (recentring moves the stage, not the real furniture).
        void FollowRealHomes()
        {
            foreach (var list in m_Follows.Values)
                foreach (var f in list)
                {
                    if (!f.Following || f.Part == null) continue;
                    if ((f.Part.position - f.At).sqrMagnitude > 1e-8f) f.Part.position = f.At;
                    var scale = f.HomeScale * f.Scale;
                    if (f.Part.localScale != scale) f.Part.localScale = scale;
                }
        }

        public static JungleDirector Instance { get; private set; }

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void LateUpdate() => UpdateLooks();

        void Update()
        {
            if (m_VanishAt > 0f && Time.time >= m_VanishAt) Vanish();
            var conductor = EchoConductor.Instance;
            if (conductor == null || butterflies == null) return;
            int wanted = JungleGrowth.ButterfliesFor(conductor.Sequencer.Echoes);
            int outNow = 0;
            for (int i = 0; i < butterflies.Length; i++)
            {
                if (butterflies[i] == null) continue;
                butterflies[i].SetOut(i < wanted);
                if (butterflies[i].IsOut) outNow++;
            }
            bool chorus = m_Growth.Chorus(outNow);
            if (chorus == m_Chorus) return;
            m_Chorus = chorus;
            if (toadstools != null)
                foreach (var t in toadstools)
                    if (t != null) t.Chorus = chorus;
            if (flowers != null)
                foreach (var f in flowers)
                    if (f != null) f.Chorus = chorus;
            Debug.Log($"[RoomOrchestra] Jungle: chorus {(chorus ? "begins!" : "rests")}");
        }
    }
}
