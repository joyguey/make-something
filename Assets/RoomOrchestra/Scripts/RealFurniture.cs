using System.Collections.Generic;
using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// The original vision (owner, 2026-11-07): play with the real furniture. The lesson keeps its virtual table,
    /// chair and lamp in the virtual room; once the lesson is over and that room has come down, Flutter asks the
    /// player to look around, the headset's labelled scan (<see cref="RoomScan"/>) names the real furniture, and each
    /// instrument moves onto a matching real piece: the table's udu onto a real table, the chair's mbira onto a real
    /// chair or sofa, the lamp's erhu onto a real lamp. The virtual piece shrinks to a small marker sitting on its real
    /// one (so the player can see which instrument lives where), and an invisible box the size of the real piece takes
    /// the hits, so a ball that hits the real furniture plays it (higher up, higher note) and its echo orbit floats
    /// above it. A piece with no real match stays where it was. Lesson (replay) puts everything back.
    /// PICO Spatial: the hit box is a collider with no renderer; everything visible already exists and is only moved.
    /// Memory only: nothing about the room is saved or sent.
    /// </summary>
    public class RealFurniture : MonoBehaviour
    {
        /// <summary>Look for this long at least (seconds) before settling for a partial match.</summary>
        public const float MinScanSeconds = 8f, MaxScanSeconds = 25f;
        /// <summary>Flutter's "look around" line stays up at least this long, even when everything is found at once.</summary>
        public const float MinLookSeconds = 4f;
        /// <summary>A table-like top must be at least this wide each way (m) for a ball to land on; a lamp needn't.</summary>
        public const float MinTopSide = 0.3f;
        /// <summary>The virtual piece's size, as a share of its own, as a marker on the real one.</summary>
        public const float MarkerScale = 0.3f;
        public const float MoveSeconds = 0.9f, HopHeight = 0.5f;
        /// <summary>Real pieces must be within this horizontal reach of the seat (m), and their tops this high above the floor.</summary>
        public const float MinReach = 0.6f, MaxReach = 4.5f, MinTop = 0.2f, MaxTop = 2.3f;
        /// <summary>The hit box is this much bigger than the scanned piece on every side, so it is hit before the real wall behind.</summary>
        public const float HitMargin = 0.04f;
        const float SayLookSeconds = 7f, SayResultSeconds = 6f;

        public enum Phase { Waiting, Scanning, Placed }

        /// <summary>Which real furniture each virtual piece prefers, best first.</summary>
        public static FurnitureKind[] Preferences(string virtualPiece) => virtualPiece switch
        {
            "Table" => new[] { FurnitureKind.Table, FurnitureKind.Cabinet, FurnitureKind.Bed, FurnitureKind.Sofa },
            "Chair" => new[] { FurnitureKind.Chair, FurnitureKind.Sofa, FurnitureKind.Bed, FurnitureKind.Cabinet },
            "Lamp" => new[] { FurnitureKind.Lamp, FurnitureKind.Other, FurnitureKind.Cabinet, FurnitureKind.Table },
            _ => new FurnitureKind[0]
        };

        sealed class Station
        {
            public string Name;
            public Transform Root;
            public InstrumentTarget Target;
            public Transform Orbit;
            public Vector3 OrbitOffset;
            public Vector3 HomeLocalPosition, HomeLocalScale;
            public Quaternion HomeLocalRotation;
            public bool Matched;
            public FurniturePiece Piece;
            public BoxCollider HitBox;
            public Vector3 From, To;
            public Vector3 ToScale;
            public float BaseOffset; // from the root's pivot down to its feet, at marker size
            public Transform Aim;
            public Vector3 AimHome;
        }

        readonly List<Station> m_Stations = new();
        Phase m_Phase = Phase.Waiting;
        float m_ScanStart, m_MoveStart = -1f, m_NextTry;

        public static RealFurniture Instance { get; private set; }
        /// <summary>How long Flutter's "look around" shows before a full match is used (tests shorten it).</summary>
        public static float LookSeconds { get; set; } = MinLookSeconds;
        public Phase State => m_Phase;
        public int MatchedCount { get; private set; }

        void OnEnable() => Instance = this;
        void OnDisable() { if (Instance == this) Instance = null; }

        void Start() => FindStations();

        void FindStations()
        {
            m_Stations.Clear();
            var furniture = GameObject.Find("RoomOrchestra")?.transform.Find("Furniture");
            if (furniture == null) return;
            var orbits = FindObjectsByType<OrbitView>(FindObjectsSortMode.None);
            foreach (Transform root in furniture)
            {
                var target = root.GetComponentInChildren<InstrumentTarget>(true);
                if (target == null) continue;
                var s = new Station
                {
                    Name = root.name,
                    Root = root,
                    Target = target,
                    HomeLocalPosition = root.localPosition,
                    HomeLocalRotation = root.localRotation,
                    HomeLocalScale = root.localScale,
                    Aim = target.transform.Find("AimPoint") ?? root.Find("AimPoint"),
                };
                if (s.Aim != null) s.AimHome = s.Aim.localPosition;
                foreach (var o in orbits)
                    if (o.Target == target)
                    {
                        s.Orbit = o.transform;
                        s.OrbitOffset = o.transform.position - target.AimPosition;
                    }
                m_Stations.Add(s);
            }
        }

        void Update()
        {
            var tutorial = TutorialDirector.Instance;
            bool lesson = tutorial != null && tutorial.Active;
            if (lesson && m_Phase != Phase.Waiting)
            {
                PutBack(); // Lesson again (Replay normally does this first): back in the virtual room
                return;
            }
            switch (m_Phase)
            {
                case Phase.Waiting:
                    var room = VirtualRoom.Instance;
                    if (lesson || room == null || !room.Gone || m_Stations.Count == 0) return;
                    if (tutorial != null && !tutorial.Plan.Done) return; // a lesson about to start (Lesson) or not yet finished
                    if (tutorial != null && tutorial.BubbleOut) return; // let Flutter finish the lesson's last line first
                    m_Phase = Phase.Scanning;
                    m_ScanStart = Time.time;
                    Debug.Log("[RoomOrchestra] Real furniture: the lesson is over; looking for the real table, chair and lamp");
                    if (!Application.isEditor) tutorial?.Announce(UiTerm.ScanLook, SayLookSeconds);
                    break;
                case Phase.Scanning:
                    if (Time.time < m_NextTry) return;
                    m_NextTry = Time.time + 1f;
                    TryPlace(Time.time - m_ScanStart);
                    break;
            }
        }

        void LateUpdate()
        {
            if (m_Phase != Phase.Placed) return;
            float u = m_MoveStart < 0f ? 1f : Mathf.Clamp01((Time.time - m_MoveStart) / MoveSeconds);
            if (u >= 1f) m_MoveStart = -1f;
            float e = u * u * (3f - 2f * u);
            foreach (var s in m_Stations)
            {
                if (!s.Matched) continue;
                var p = Vector3.Lerp(s.From, s.To, e) + Vector3.up * (HopHeight * 4f * u * (1f - u));
                if ((s.Root.position - p).sqrMagnitude > 1e-8f) s.Root.position = p; // also holds it there if the stage is recentred
                var scale = Vector3.Lerp(s.HomeLocalScale, s.ToScale, e);
                if (s.Root.localScale != scale) s.Root.localScale = scale;
                if (s.HitBox != null) FitHitBox(s);
                if (s.Orbit != null)
                {
                    var o = s.Target.AimPosition + s.OrbitOffset;
                    if ((s.Orbit.position - o).sqrMagnitude > 1e-8f) s.Orbit.position = o;
                }
            }
        }

        void TryPlace(float scanned)
        {
            var scan = RoomScan.Instance;
            var pieces = scan != null ? scan.Furniture : null;
            var seat = SeatFloor();
            var forward = StageForward();
            var picks = Match(m_Stations.ConvertAll(s => (s.Name, s.Root.position)), pieces ?? new List<FurniturePiece>(), seat, forward);
            int found = 0;
            foreach (var p in picks) if (p.HasValue) found++;
            bool all = found == m_Stations.Count;
            // all found: go; some found: give the rest a little longer; none: keep looking until the time is up
            bool ready = (all && scanned >= LookSeconds) || scanned >= MaxScanSeconds || (found > 0 && scanned >= MinScanSeconds * 2f);
            if (!ready) return;
            Place(picks);
            var tutorial = TutorialDirector.Instance;
            if (!Application.isEditor) tutorial?.Announce(found > 0 ? UiTerm.ScanFound : UiTerm.ScanNone, SayResultSeconds);
            Debug.Log($"[RoomOrchestra] Real furniture: {found} of {m_Stations.Count} instruments on real furniture after {scanned:F0} s of looking ({scan?.PieceCount ?? 0} scanned pieces)");
        }

        /// <summary>
        /// Picks a real piece for each virtual piece (by name, at its current place): the preferred kinds first, within
        /// reach of the seat, in front of the player rather than behind, near where the virtual piece was; never one real
        /// piece for two instruments. Null where nothing fits (that instrument stays where it is).
        /// </summary>
        public static FurniturePiece?[] Match(IReadOnlyList<(string name, Vector3 at)> stations, IReadOnlyList<FurniturePiece> pieces,
            Vector3 seatFloor, Vector3 forward)
        {
            var result = new FurniturePiece?[stations.Count];
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-6f) forward = Vector3.forward;
            forward.Normalize();
            // every allowed (instrument, piece) pairing with its score; the best pairings are handed out first, so the
            // instrument a piece suits best gets it (a sofa goes to the chair's mbira before the table's udu)
            var pairs = new List<(float score, int station, int piece)>();
            for (int si = 0; si < stations.Count; si++)
            {
                var prefs = Preferences(stations[si].name);
                for (int pi = 0; pi < pieces.Count; pi++)
                {
                    var piece = pieces[pi];
                    int rank = System.Array.IndexOf(prefs, piece.Kind);
                    if (rank < 0) continue;
                    var flat = piece.Top - seatFloor;
                    float height = flat.y;
                    flat.y = 0f;
                    float reach = flat.magnitude;
                    if (reach < MinReach || reach > MaxReach || height < MinTop || height > MaxTop) continue;
                    if (piece.Kind != FurnitureKind.Lamp && piece.Kind != FurnitureKind.Other
                        && (piece.Box.size.x < MinTopSide || piece.Box.size.z < MinTopSide)) continue; // a speck, not a table
                    float behind = Vector3.Dot(flat.normalized, forward) < -0.2f ? 3f : 0f; // a throw behind you is hard
                    var fromHome = piece.Top - stations[si].at;
                    fromHome.y = 0f;
                    pairs.Add((rank * 4f + behind + fromHome.magnitude * 0.5f, si, pi));
                }
            }
            pairs.Sort((a, b) => a.score.CompareTo(b.score));
            var used = new HashSet<int>();
            foreach (var (_, si, pi) in pairs)
            {
                if (result[si].HasValue || used.Contains(pi)) continue;
                used.Add(pi);
                result[si] = pieces[pi];
            }
            return result;
        }

        void Place(FurniturePiece?[] picks)
        {
            MatchedCount = 0;
            for (int i = 0; i < m_Stations.Count; i++)
            {
                var s = m_Stations[i];
                if (!picks[i].HasValue) continue;
                s.Matched = true;
                s.Piece = picks[i].Value;
                MatchedCount++;
                s.From = s.Root.position;
                s.ToScale = s.HomeLocalScale * MarkerScale;
                // its feet on the real top: how far its pivot sits above its lowest point, at marker size
                s.BaseOffset = (s.Root.position.y - Lowest(s.Root)) * MarkerScale; // measured at its own size
                s.To = s.Piece.Top + Vector3.up * s.BaseOffset;
                if (s.HitBox == null)
                {
                    var go = new GameObject($"RealHitBox_{s.Name}");
                    go.transform.SetParent(s.Root, false);
                    s.HitBox = go.AddComponent<BoxCollider>();
                }
                s.HitBox.enabled = true;
                PoofCloud.PoofAt(s.From, 0.4f);
                PoofCloud.PoofAt(s.Piece.Top, 0.5f);
                Debug.Log($"[RoomOrchestra] Real furniture: {s.Name} ({s.Target.Instrument}) moves onto the real {s.Piece}");
            }
            m_Phase = Phase.Placed;
            m_MoveStart = Time.time;
            Physics.SyncTransforms();
        }

        // Sizes the invisible hit box to the real piece (the root is scaled uniformly, so divide it out).
        void FitHitBox(Station s)
        {
            var t = s.HitBox.transform;
            var size = s.Piece.Box.size + Vector3.one * HitMargin * 2f;
            float scale = Mathf.Max(1e-4f, s.Root.lossyScale.x);
            if ((t.position - s.Piece.Box.center).sqrMagnitude > 1e-8f) t.position = s.Piece.Box.center;
            if (t.rotation != Quaternion.identity) t.rotation = Quaternion.identity;
            var local = size / scale;
            if (t.localScale != local) t.localScale = local;
            if (s.Aim != null)
            {
                var a = s.Piece.Top + Vector3.up * 0.06f;
                if ((s.Aim.position - a).sqrMagnitude > 1e-8f) s.Aim.position = a;
            }
        }

        static float Lowest(Transform root)
        {
            float low = root.position.y;
            bool any = false;
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                low = any ? Mathf.Min(low, r.bounds.min.y) : r.bounds.min.y;
                any = true;
            }
            return low;
        }

        /// <summary>Back in the virtual room (Lesson): every piece home at its own size, the hit boxes off.</summary>
        public void PutBack()
        {
            foreach (var s in m_Stations)
            {
                if (!s.Matched) continue;
                s.Matched = false;
                s.Root.localPosition = s.HomeLocalPosition;
                s.Root.localRotation = s.HomeLocalRotation;
                s.Root.localScale = s.HomeLocalScale;
                if (s.HitBox != null) s.HitBox.enabled = false;
                if (s.Aim != null) s.Aim.localPosition = s.AimHome;
                if (s.Orbit != null) s.Orbit.position = s.Target.AimPosition + s.OrbitOffset;
            }
            MatchedCount = 0;
            m_Phase = Phase.Waiting;
            Debug.Log("[RoomOrchestra] Real furniture: back in the virtual room for the lesson");
        }

        static Vector3 SeatFloor()
        {
            var stage = GameObject.Find("RoomOrchestra");
            return stage != null ? stage.transform.position : Vector3.zero;
        }

        static Vector3 StageForward()
        {
            var stage = GameObject.Find("RoomOrchestra");
            return stage != null ? stage.transform.forward : Vector3.forward;
        }
    }
}
