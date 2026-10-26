using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// Brings the rainforest to life as music is composed (rules in Core/JungleGrowth): each note ADDED to
    /// the song feeds its instrument's vine (table/udu, chair/mbira, lamp/erhu; the ball-stand vine feeds
    /// on every note); the loop repeating does not grow anything,
    /// toadstools sprout from buds one by one, butterflies flutter out of the lamp while the song is
    /// layered and melodic, and with every vine grown and all butterflies out the toadstools hop in a
    /// chorus. Clear sweeps it all away in big smoke clouds and the next song grows it again. Each new
    /// stage is logged so it can be checked on a device.
    /// </summary>
    public class JungleDirector : MonoBehaviour
    {
        [SerializeField] VineGrowth udu, mbira, erhu, stand;
        [SerializeField] Toadstool[] toadstools;
        [SerializeField] Butterfly[] butterflies;

        JungleGrowth m_Growth = new();
        bool m_Chorus;
        float m_VanishAt = -1f;

        /// <summary>How long after Clear the jungle vanishes: when the smoke is at its thickest.</summary>
        public const float VanishDelay = 0.2f;

        public JungleGrowth Growth => m_Growth;
        public bool InChorus => m_Chorus;

        public void Configure(VineGrowth uduVine, VineGrowth mbiraVine, VineGrowth erhuVine, VineGrowth standVine, Toadstool[] floor, Butterfly[] flock)
        {
            udu = uduVine;
            mbira = mbiraVine;
            erhu = erhuVine;
            stand = standVine;
            toadstools = floor;
            butterflies = flock;
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
            foreach (var vine in new[] { udu, mbira, erhu, stand })
                if (vine != null && vine.Shown > 0 && PoofCloud.PoofAt(vine.GrownCentre(), ClearSmokeSize)) clouds++;
            if (toadstools != null)
                foreach (var t in toadstools)
                    if (t != null && t.Sprouted && PoofCloud.PoofAt(t.transform.position, ClearSmokeSize * 0.6f)) clouds++;
            if (butterflies != null)
                foreach (var b in butterflies)
                    if (b != null && !b.AtHome && PoofCloud.PoofAt(b.transform.position, ClearSmokeSize * 0.5f)) clouds++;
            m_VanishAt = Time.time + VanishDelay;
            Debug.Log($"[RoomOrchestra] Jungle: cleared in a cloud of smoke ({clouds} clouds)");
        }

        /// <summary>Clear's smoke clouds, as a multiple of a ball's landing poof.</summary>
        public const float ClearSmokeSize = 4f;

        void Vanish()
        {
            m_VanishAt = -1f;
            m_Growth = new JungleGrowth();
            foreach (var vine in new[] { udu, mbira, erhu, stand })
                if (vine != null) vine.ResetGrowth();
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
            var vine = VineFor(instrument);
            if (vine != null && vine.SetProgress(m_Growth.VineProgress(instrument)))
                Debug.Log($"[RoomOrchestra] Jungle: {instrument} vine {vine.Shown}/{vine.SegmentCount}{(vine.Bloomed ? ", in bloom" : "")}");
            if (stand != null) stand.SetProgress(m_Growth.StandVineProgress);
            if (toadstools != null)
                for (int i = 0; i < m_Growth.MushroomsSprouted && i < toadstools.Length; i++)
                    if (toadstools[i] != null) toadstools[i].Sprout();
        }

        VineGrowth VineFor(InstrumentType instrument) => instrument switch
        {
            InstrumentType.Udu => udu,
            InstrumentType.Mbira => mbira,
            _ => erhu,
        };

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
            Debug.Log($"[RoomOrchestra] Jungle: chorus {(chorus ? "begins!" : "rests")}");
        }
    }
}
