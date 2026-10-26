using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// Brings the rainforest to life as music is composed (rules in Core/JungleGrowth): each note feeds
    /// its instrument's vine (table/udu, chair/mbira, lamp/erhu; the ball-stand vine feeds on every note),
    /// toadstools sprout from buds one by one, butterflies flutter out of the lamp while the song is
    /// layered and melodic, and with every vine grown and all butterflies out the toadstools hop in a
    /// chorus. Each new stage is logged so it can be checked on a device.
    /// </summary>
    public class JungleDirector : MonoBehaviour
    {
        [SerializeField] VineGrowth udu, mbira, erhu, stand;
        [SerializeField] Toadstool[] toadstools;
        [SerializeField] Butterfly[] butterflies;

        readonly JungleGrowth m_Growth = new();
        bool m_Chorus;

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
            MusicalHitPipeline.Committed += OnHit;
            OrbitView.NotePassed += OnEcho;
        }

        void OnDisable()
        {
            MusicalHitPipeline.Committed -= OnHit;
            OrbitView.NotePassed -= OnEcho;
        }

        void OnHit(MusicalHitEvent hit) => Heard(hit.Target);

        void OnEcho(InstrumentTarget target)
        {
            if (target != null) Heard(target.Instrument);
        }

        /// <summary>One note was heard on <paramref name="instrument"/>: the jungle grows a little.</summary>
        public void Heard(InstrumentType instrument)
        {
            m_Growth.NotePlayed(instrument);
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
