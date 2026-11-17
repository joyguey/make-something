using System;
using System.Collections.Generic;
using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// Instrument packs and which instrument each piece of furniture plays (V3 list: "instrument packs and
    /// assigning any instrument to any furniture"). A pack is one sound set for the three instruments (all our own
    /// synthesis): "Classic" (the approved set, ArtSource/Audio/gen_world_instruments.py) and "Earthy" (the
    /// alternative set, ArtSource/Audio/synthesize_instruments.py), each trimmed to keep the approved mix balance.
    /// A piece of furniture's Change button gives it the next instrument, swapping with whichever furniture had it;
    /// the song's echoes follow their instrument. Both choices are remembered on the device (local only).
    /// While the guided song runs, the furniture keeps its first instruments (the lesson is about them).
    /// </summary>
    public class InstrumentPacks : MonoBehaviour
    {
        public const string PackPrefKey = "RoomOrchestra.SoundPack", PlacesPrefKey = "RoomOrchestra.InstrumentPlaces";

        [Serializable]
        public class Pack
        {
            public UiTerm name;
            public AudioClip[] udu, mbira, erhu;
            [Tooltip("The V4 library (the same sounds in every pack): ocarina, handpan, guzheng, balafon, slit drum.")]
            public AudioClip[] ocarina, handpan, guzheng, balafon, slitDrum;
            [Tooltip("Each instrument's volume in this pack (Udu, Mbira, Erhu), trimmed to the approved balance.")]
            public float[] volumes = { 0.7f, 0.7f, 0.7f };

            public AudioClip[] For(InstrumentType type) => type switch
            {
                InstrumentType.Udu => udu,
                InstrumentType.Mbira => mbira,
                InstrumentType.Ocarina => ocarina,
                InstrumentType.Handpan => handpan,
                InstrumentType.Guzheng => guzheng,
                InstrumentType.Balafon => balafon,
                InstrumentType.SlitDrum => slitDrum,
                _ => erhu,
            };

            public float VolumeFor(InstrumentType type) => volumes != null && (int)type < volumes.Length ? volumes[(int)type] : 0.7f;
        }

        [SerializeField] Pack[] packs;
        [Tooltip("The furniture, left to right, and the instrument each plays at first.")]
        [SerializeField] InstrumentTarget[] furniture;
        [SerializeField] InstrumentType[] firstPlaces;

        int m_Pack;

        public static InstrumentPacks Instance { get; private set; }
        /// <summary>A pack or an instrument's place changed (labels follow).</summary>
        public static event Action Changed;
        public IReadOnlyList<Pack> Packs => packs;
        public int PackIndex => m_Pack;
        public Pack Current => packs != null && packs.Length > 0 ? packs[m_Pack] : null;
        public IReadOnlyList<InstrumentTarget> Furniture => furniture;

        /// <summary>An Echo Battle holds the instruments where they are (the host's, once hosting; both players' once
        /// the rooms match): Change does nothing until the battle ends.</summary>
        public static bool Locked;
        /// <summary>The host's instruments while the joining player matches their room to them: Change gives a piece
        /// the next one still missing. Null outside that.</summary>
        public static IReadOnlyList<InstrumentType> Wanted;
        /// <summary>Change was pressed while <see cref="Locked"/> (the battle card says why).</summary>
        public static event Action RefusedWhileLocked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Locked = false;
            Wanted = null;
        }

        /// <summary>The instrument on each piece of furniture, left to right.</summary>
        public InstrumentType[] Places()
        {
            var places = new InstrumentType[furniture.Length];
            for (int i = 0; i < furniture.Length; i++) places[i] = furniture[i].Instrument;
            return places;
        }

        /// <summary>Puts a whole arrangement back (the player's own, after a battle).</summary>
        public void SetPlaces(IReadOnlyList<InstrumentType> places)
        {
            if (places == null || furniture == null || places.Count != furniture.Length) return;
            bool changed = false;
            for (int i = 0; i < furniture.Length; i++)
                if (furniture[i] != null && furniture[i].Instrument != places[i])
                {
                    SetPlace(furniture[i], places[i]);
                    changed = true;
                }
            if (!changed) return;
            ApplySounds();
            Remember();
            Changed?.Invoke();
        }

        public void Configure(Pack[] soundPacks, InstrumentTarget[] leftToRight)
        {
            packs = soundPacks;
            furniture = leftToRight;
            firstPlaces = new InstrumentType[leftToRight.Length];
            for (int i = 0; i < leftToRight.Length; i++) firstPlaces[i] = leftToRight[i].Instrument;
        }

        void OnEnable() => Instance = this;

        void OnDisable()
        {
            if (Instance == this) Instance = null;
        }

        // The device's remembered choices, applied before anything plays (audio only: nothing visible moves).
        void Start()
        {
            int pack = PlayerPrefs.GetInt(PackPrefKey, 0);
            var places = ParsePlaces(PlayerPrefs.GetString(PlacesPrefKey, ""), furniture != null ? furniture.Length : 0);
            if (places != null && !TutorialDirector.RememberedDone) places = null; // a first lesson starts as authored
            if (places != null) for (int i = 0; i < places.Length; i++) SetPlace(furniture[i], places[i]);
            if (pack > 0 && packs != null && pack < packs.Length) m_Pack = pack;
            if (places != null || m_Pack != 0) ApplySounds();
        }

        /// <summary>"Udu,Mbira,Erhu" (left to right) back to places; null unless it is a full, valid arrangement.</summary>
        public static InstrumentType[] ParsePlaces(string text, int count)
        {
            if (string.IsNullOrEmpty(text)) return null;
            var parts = text.Split(',');
            if (parts.Length != count) return null;
            var result = new InstrumentType[count];
            var seen = new HashSet<InstrumentType>();
            for (int i = 0; i < count; i++)
                if (!Enum.TryParse(parts[i], out result[i]) || !seen.Add(result[i])) return null;
            return result;
        }

        public static string FormatPlaces(IReadOnlyList<InstrumentType> places) => string.Join(",", places);

        /// <summary>
        /// The furniture's own instrument (the first arrangement's) for whichever furniture plays <paramref name="type"/>
        /// now: the rainforest grows by the furniture, whatever instrument it plays (V4 library).
        /// </summary>
        public InstrumentType HomeOf(InstrumentType type)
        {
            if (furniture == null || firstPlaces == null) return type;
            for (int i = 0; i < furniture.Length && i < firstPlaces.Length; i++)
                if (furniture[i] != null && furniture[i].Instrument == type) return firstPlaces[i];
            return type;
        }

        /// <summary>The next instrument after <paramref name="type"/> (udu, mbira, erhu, then the V4 library, and round).</summary>
        public static InstrumentType Next(InstrumentType type) =>
            (InstrumentType)(((int)type + 1) % Enum.GetValues(typeof(InstrumentType)).Length);

        /// <summary>The leaf's Sounds button: the next pack.</summary>
        public void NextPack()
        {
            if (packs == null || packs.Length == 0) return;
            UsePack((m_Pack + 1) % packs.Length);
        }

        public void UsePack(int index)
        {
            if (packs == null || index < 0 || index >= packs.Length) return;
            m_Pack = index;
            PlayerPrefs.SetInt(PackPrefKey, index);
            PlayerPrefs.Save();
            ApplySounds();
            Debug.Log($"[RoomOrchestra] Sounds: {UiText.Get(packs[index].name, UiLanguage.English)} pack");
            Changed?.Invoke();
        }

        /// <summary>A furniture's Change button: it plays the next instrument, swapping with whichever had it.</summary>
        public bool ChangeInstrument(InstrumentTarget piece)
        {
            if (piece == null) return false;
            if (TutorialDirector.Instance != null && TutorialDirector.Instance.Active)
            {
                Debug.Log("[RoomOrchestra] Instruments stay where they are during the lesson");
                return false;
            }
            if (Locked)
            {
                Debug.Log("[RoomOrchestra] Instruments are locked in for the Echo Battle");
                RefusedWhileLocked?.Invoke();
                return false;
            }
            if (Wanted != null)
            {
                var here = new HashSet<InstrumentType>();
                foreach (var f in furniture) if (f != null) here.Add(f.Instrument);
                return Place(piece, EchoBattleRules.NextWanted(Wanted, here, piece.Instrument));
            }
            return Place(piece, Next(piece.Instrument));
        }

        /// <summary>Puts <paramref name="type"/> on <paramref name="piece"/>; the furniture that had it takes the old one.</summary>
        public bool Place(InstrumentTarget piece, InstrumentType type)
        {
            if (piece == null || piece.Instrument == type) return false;
            InstrumentTarget other = null;
            foreach (var f in furniture) if (f != null && f.Instrument == type) other = f;
            var old = piece.Instrument;
            if (other != null) SetPlace(other, old);
            SetPlace(piece, type);
            ApplySounds();
            Remember();
            Debug.Log($"[RoomOrchestra] {piece.name} now plays the {type}" + (other != null ? $"; {other.name} plays the {old}" : ""));
            Changed?.Invoke();
            return true;
        }

        /// <summary>Back to the first arrangement (the guided song is about it).</summary>
        public void ResetPlaces()
        {
            if (Locked || Wanted != null) return; // a battle holds them
            bool changed = false;
            for (int i = 0; i < furniture.Length; i++)
                if (furniture[i] != null && furniture[i].Instrument != firstPlaces[i])
                {
                    SetPlace(furniture[i], firstPlaces[i]);
                    changed = true;
                }
            if (!changed) return;
            ApplySounds();
            Debug.Log("[RoomOrchestra] Instruments back in their first places");
            Changed?.Invoke();
        }

        void Remember()
        {
            if (Wanted != null) return; // matching the host's set is for the battle only: the player's own arrangement stays remembered
            PlayerPrefs.SetString(PlacesPrefKey, FormatPlaces(Places()));
            PlayerPrefs.Save();
        }

        static void SetPlace(InstrumentTarget piece, InstrumentType type) => piece.SetInstrument(type);

        // Every piece takes its instrument's sound in the current pack; anything already queued is re-queued on the
        // furniture that now plays it (the song follows its instruments).
        void ApplySounds()
        {
            var pack = Current;
            if (pack == null) return;
            foreach (var piece in furniture)
                if (piece != null) piece.SetSound(pack.For(piece.Instrument), pack.VolumeFor(piece.Instrument));
            EchoConductor.Instance?.Requeue();
        }
    }
}
