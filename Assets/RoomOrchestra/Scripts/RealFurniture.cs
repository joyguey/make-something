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
    /// Every real piece it found gets a glowing <see cref="RealSpot"/> (owner, headset 2026-11-08: there was no
    /// indication), spots appear while the scan looks, and tapping a spot moves the next instrument there (or sends the
    /// one there home), so the player decides; the automatic match is only the first suggestion.
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

        public enum Phase { Waiting, Scanning, Choosing, Placed, Restoring, LookingAround }

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
            public Vector3 FromScale;
            public float MoveStart = -1f;
            public bool GoingHome;
            /// <summary>The real object it lives on (or the box around its own furniture, kept), after choosing.</summary>
            public RealHome Home;
            /// <summary>Its virtual furniture is gone (it lives on a real object now).</summary>
            public bool Removed;
            public Renderer[] Looks;
            public Collider[] Solid;
        }

        readonly List<Station> m_Stations = new();
        readonly List<RealSpot> m_Spots = new();
        readonly List<FurniturePiece> m_Candidates = new();
        readonly List<int> m_Occupant = new(); // per candidate: the station living there, or -1
        Phase m_Phase = Phase.Waiting;
        float m_ScanStart, m_NextTry;

        /// <summary>
        /// On in PICO XR mode: after the lesson the player chooses where each instrument lives. (Off in v3.5, the last
        /// PICO Spatial build: its automatic placement put plates all over the owner's room, 2026-11-08.)
        /// </summary>
        public static bool Enabled { get; set; } = true;
        /// <summary>
        /// The old automatic placement from the scan's furniture labels (unreliable on the headset, 2026-11-08). Off:
        /// the player chooses instead (<see cref="Phase.Choosing"/>). Its tests switch it on.
        /// </summary>
        public static bool AutoPlace { get; set; }

        int m_Choosing;
        // The order Flutter asks in: the lesson's (owner, headset 2026-11-08: "double-check that it's the right one
        // according to the tutorial"): the chair's mbira, the lamp's erhu, then the table's udu.
        readonly List<int> m_Order = new();
        /// <summary>Choosing where the instruments live (the pointer places them).</summary>
        public bool Choosing => m_Phase == Phase.Choosing;
        Station ChoosingStation => Choosing && m_Choosing < m_Order.Count ? m_Stations[m_Order[m_Choosing]] : null;
        /// <summary>The instrument being placed now, or null.</summary>
        public InstrumentTarget ChoosingTarget => ChoosingStation?.Target;
        /// <summary>The order instruments are placed in: the lesson's.</summary>
        public static readonly InstrumentType[] LessonOrder = { InstrumentType.Mbira, InstrumentType.Erhu, InstrumentType.Udu };
        /// <summary>The real home of an instrument, or null.</summary>
        public RealHome HomeOf(InstrumentTarget target)
        {
            foreach (var s in m_Stations) if (s.Target == target) return s.Home;
            return null;
        }
        /// <summary>Is this instrument's virtual furniture gone (it lives on a real object)?</summary>
        public bool IsRemoved(InstrumentTarget target)
        {
            foreach (var s in m_Stations) if (s.Target == target) return s.Removed;
            return false;
        }
        /// <summary>The size of the spot that previews where the instrument will go (m).</summary>
        public const float CursorSide = 0.25f;
        const float SayPlaceSeconds = 30f;

        public static RealFurniture Instance { get; private set; }
        /// <summary>How long Flutter's "look around" shows before a full match is used (tests shorten it).</summary>
        public static float LookSeconds { get; set; } = MinLookSeconds;
        public Phase State => m_Phase;
        public int MatchedCount { get; private set; }

        void OnEnable() => Instance = this;
        void OnDisable() { if (Instance == this) Instance = null; }

        void Start()
        {
            FindStations();
            m_Spots.Clear();
            m_Spots.AddRange(FindObjectsByType<RealSpot>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            m_Spots.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            for (int i = 0; i < m_Spots.Count; i++) m_Spots[i].Index = i;
        }

        /// <summary>Real pieces with a spot, in spot order.</summary>
        public int SpotCount => m_Candidates.Count;
        public RealSpot SpotAt(int i) => i >= 0 && i < m_Spots.Count ? m_Spots[i] : null;
        /// <summary>The instrument living on spot <paramref name="i"/>, or null.</summary>
        public InstrumentTarget OccupantAt(int i) => i >= 0 && i < m_Occupant.Count && m_Occupant[i] >= 0 ? m_Stations[m_Occupant[i]].Target : null;

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
                s.Looks = root.GetComponentsInChildren<Renderer>(true);
                s.Solid = root.GetComponentsInChildren<Collider>(true);
                foreach (var o in orbits)
                    if (o.Target == target)
                    {
                        s.Orbit = o.transform;
                        s.OrbitOffset = o.transform.position - target.AimPosition;
                    }
                m_Stations.Add(s);
            }
        }

        // Where each piece lives in free play, at its own size (taken when the scan starts).
        void RememberHomes()
        {
            foreach (var s in m_Stations)
            {
                s.HomeLocalPosition = s.Root.localPosition;
                s.HomeLocalRotation = s.Root.localRotation;
                s.HomeLocalScale = s.Root.localScale;
                if (s.Aim != null) s.AimHome = s.Aim.localPosition;
                if (s.Orbit != null) s.OrbitOffset = s.Orbit.position - s.Target.AimPosition;
            }
        }

        void Update()
        {
            if (!Enabled && m_Phase == Phase.Waiting) return;
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
                    RememberHomes(); // now, not at startup: the opening still has the lamp and table tucked in the cloud
                    if (!AutoPlace)
                    {
                        // nothing to choose from (or to find the saved objects' shapes in) until the room scan is in
                        if (RealWalls.Instance == null || RealWalls.Instance.SurfaceCount == 0) return;
                        if (TryRestore()) break; // the places chosen last time, found again from their saved anchors
                        LookAround();
                        break;
                    }
                    m_Phase = Phase.Scanning;
                    m_ScanStart = Time.time;
                    Debug.Log("[RoomOrchestra] Real furniture: the lesson is over; looking for the real table, chair and lamp");
                    if (!Application.isEditor) tutorial?.Announce(UiTerm.ScanLook, SayLookSeconds);
                    break;
                case Phase.Restoring:
                    if (m_RestoreDone) FinishRestore();
                    break;
                case Phase.LookingAround:
                    LookingAround();
                    break;
                case Phase.Scanning:
                    if (Time.time < m_NextTry) return;
                    m_NextTry = Time.time + 1f;
                    ShowFound(); // what the scan has found so far, as it finds it
                    TryPlace(Time.time - m_ScanStart);
                    break;
                case Phase.Placed:
                    if (Time.time < m_NextTry) return;
                    m_NextTry = Time.time + 0.5f;
                    WriteSpots(); // language or instrument changes
                    RefineShapes();
                    break;
            }
        }

        bool m_RestoreDone;
        Dictionary<string, (Pose pose, Vector3 size)> m_Restored;

        // The places chosen last time: their anchors are looked up (a few seconds at most), then each instrument goes
        // back onto its object. False when nothing was saved.
        bool TryRestore()
        {
            if (!RealHomeStore.HasAnchors()) return false;
            m_Phase = Phase.Restoring;
            m_RestoreDone = false;
            Debug.Log("[RoomOrchestra] Real furniture: looking for the places chosen last time");
            _ = Restore();
            return true;
        }

        async System.Threading.Tasks.Task Restore()
        {
            var found = await RealHomeStore.FindAll();
            m_Restored = found;
            m_RestoreDone = true;
        }

        void FinishRestore()
        {
            var found = m_Restored ?? new Dictionary<string, (Pose pose, Vector3 size)>();
            m_Restored = null;
            var saved = RealHomeStore.Load();
            if (found.Count == 0 || found.Count < saved.Count)
            {
                Debug.Log($"[RoomOrchestra] Real furniture: {found.Count} of {saved.Count} saved places found; choosing again");
                RealHomeStore.Clear();
                StartChoosing();
                return;
            }
            InstrumentPacks.Instance?.ResetPlaces();
            ShowPitchColumns(false);
            MatchedCount = 0;
            foreach (var s in m_Stations)
            {
                if (found.TryGetValue(s.Name, out var place))
                {
                    LiveOn(s, place.pose, place.size, withShape: true);
                    MatchedCount++;
                }
                else KeepHome(s);
            }
            m_Phase = Phase.Placed;
            Physics.SyncTransforms();
            Debug.Log($"[RoomOrchestra] Real furniture: {MatchedCount} instruments back on the real objects chosen last time");
            if (!Application.isEditor) TutorialDirector.Instance?.Announce(UiTerm.PlaceWelcomeBack, SayResultSeconds);
        }

        // The scan keeps arriving: an object placed before its triangles were in (or found again from an anchor) takes
        // its scanned shape once there is one.
        void RefineShapes()
        {
            var walls = RealWalls.Instance;
            if (walls == null) return;
            foreach (var s in m_Stations)
            {
                if (s.Home == null || !s.Removed || s.Home.HasShape) continue;
                var tris = RoomObjects.ShapeIn(s.Home.WorldBounds, walls.PieceTriangles, SeatFloor().y);
                if (tris.Count >= 9) s.Home.SetShape(tris);
            }
        }

        // Owner, headset 2026-11-08: "I still think the room needs to be entirely scanned". Before choosing (first time,
        // or after Start over), Flutter asks the player to look slowly around the whole room while the scan glows and
        // fills in. The headset keeps scanning while they choose too. Not in the Editor (its tests have no room to scan).
        /// <summary>How long the player looks around the room before choosing (s).</summary>
        public static float LookAroundSeconds => Application.isEditor ? 0f : 6f;

        void LookAround()
        {
            m_Phase = Phase.LookingAround;
            m_ScanStart = Time.time;
            m_NextCoverage = 0f;
            RealWalls.ShowScan = true;
            Debug.Log("[RoomOrchestra] Real furniture: look around the whole room while the scan fills in (a bar shows how much)");
            if (!Application.isEditor) TutorialDirector.Instance?.Announce(UiTerm.ScanRoom, LookAroundMaxSeconds);
        }

        // ---- Moving a kept virtual piece by hand (owner, headset 2026-11-12: "I'd like to be able to grab and drag the
        // virtual furniture if I end up using the chair ... it was being occluded by the wall") ----
        // After the lesson, a grip or a fist on a piece of virtual furniture the player kept picks it up; it slides over
        // the floor with the hand, its echo ring and vines with it.
        Station m_Moving;
        Vector3 m_MoveOffset, m_MoveFrom;
        /// <summary>How close a hand must be to a kept piece to take hold of it (m, from its outline).</summary>
        public const float MoveReach = 0.2f;

        /// <summary>A hand at <paramref name="hand"/> takes hold of a kept virtual piece there; false if there is none.</summary>
        public bool TryBeginMove(Vector3 hand)
        {
            if (m_Phase != Phase.Placed && m_Phase != Phase.Choosing) return false;
            foreach (var s in m_Stations)
            {
                if (s.Removed || s.Root == null || s.MoveStart >= 0f) continue;
                var box = VisibleBox(s.Root);
                box.Expand(MoveReach * 2f);
                if (!box.Contains(hand)) continue;
                m_Moving = s;
                m_MoveFrom = VisibleBox(s.Root).center;
                m_MoveOffset = s.Root.position - hand;
                m_MoveOffset.y = 0f;
                Debug.Log($"[RoomOrchestra] Real furniture: the virtual {s.Name} taken by hand");
                return true;
            }
            return false;
        }

        public bool Moving => m_Moving != null;

        // From across the room (owner, headset 2026-11-12: "I couldn't move the chair, do I need to walk up to it?"):
        // a grip or a fist while pointing (or looking) at a kept piece takes hold of it; it then slides to where the
        // pointer (or the gaze) meets the floor.
        float m_MoveFloor;
        Vector3 m_RayOffset;
        public const float MoveRange = 6f;

        /// <summary>A grip along <paramref name="ray"/> takes hold of the kept virtual piece it points at; false if none.</summary>
        public bool TryBeginMoveByRay(Ray ray)
        {
            if (m_Phase != Phase.Placed && m_Phase != Phase.Choosing) return false;
            Station best = null;
            float bestDistance = MoveRange;
            foreach (var s in m_Stations)
            {
                if (s.Removed || s.Root == null || s.MoveStart >= 0f) continue;
                var box = VisibleBox(s.Root);
                box.Expand(0.1f);
                if (box.IntersectRay(ray, out float d) && d < bestDistance) { best = s; bestDistance = d; }
            }
            if (best == null) return false;
            var hold = VisibleBox(best.Root);
            m_Moving = best;
            m_MoveFrom = hold.center;
            m_MoveFloor = hold.min.y;
            m_RayOffset = best.Root.position - FloorPoint(ray, best.Root.position);
            m_RayOffset.y = 0f;
            Debug.Log($"[RoomOrchestra] Real furniture: the virtual {best.Name} taken by hand from {bestDistance:F1} m");
            return true;
        }

        Vector3 FloorPoint(Ray ray, Vector3 fallback)
        {
            if (Mathf.Abs(ray.direction.y) < 1e-3f) return fallback;
            float t = (m_MoveFloor - ray.origin.y) / ray.direction.y;
            if (t <= 0f || t > MoveRange) return fallback; // pointing up or too far: it stays where it is
            return ray.origin + ray.direction * t;
        }

        /// <summary>The held piece slides to where <paramref name="ray"/> meets the floor.</summary>
        public void MoveAlong(Ray ray)
        {
            var s = m_Moving;
            if (s == null) return;
            SetRoot(s, FloorPoint(ray, s.Root.position - m_RayOffset) + m_RayOffset);
        }

        static void SetRoot(Station s, Vector3 p)
        {
            p.y = s.Root.position.y; // it stays on the floor
            s.Root.position = p;
            if (s.Orbit != null) s.Orbit.position = s.Target.AimPosition + s.OrbitOffset;
            Physics.SyncTransforms();
        }

        public void MoveTo(Vector3 hand)
        {
            var s = m_Moving;
            if (s == null) return;
            SetRoot(s, hand + m_MoveOffset);
        }

        public void EndMove()
        {
            var s = m_Moving;
            if (s == null) return;
            m_Moving = null;
            var box = VisibleBox(s.Root);
            var jungle = JungleDirector.Instance;
            if (jungle != null)
                jungle.GrowAround(s.Name, new Vector3(m_MoveFrom.x, box.min.y, m_MoveFrom.z), box.size, new Vector3(box.center.x, box.min.y, box.center.z), box.size);
            UiSounds.Play(UiSound.Place, box.center);
            Debug.Log($"[RoomOrchestra] Real furniture: the virtual {s.Name} put down at {box.center:F2}");
        }

        /// <summary>Choosing starts once this share of the room around the player is scanned, after at least
        /// <see cref="LookAroundSeconds"/>, or after <see cref="LookAroundMaxSeconds"/> whatever the scan shows.</summary>
        public const float CoverageToChoose = 0.75f, LookAroundMaxSeconds = 45f;
        float m_NextCoverage, m_Coverage;
        /// <summary>The scan's coverage of the room around the player now (0..1).</summary>
        public float ScanCoverage => m_Coverage;

        void LookingAround()
        {
            float t = Time.time - m_ScanStart;
            if (Time.time >= m_NextCoverage)
            {
                m_NextCoverage = Time.time + 0.5f;
                var walls = RealWalls.Instance;
                var centre = StageAnchor.Instance != null && StageAnchor.Instance.TryGetHeadPose(out var head) ? head.position : SeatFloor();
                m_Coverage = walls != null ? walls.Coverage(centre) : 0f;
            }
            ScanProgressBar.Instance?.Show(m_Coverage);
            bool enough = m_Coverage >= CoverageToChoose && t >= LookAroundSeconds;
            if (!enough && t < (Application.isEditor ? LookAroundSeconds : LookAroundMaxSeconds)) return;
            Debug.Log($"[RoomOrchestra] Real furniture: the room is {m_Coverage * 100f:F0}% scanned after {t:F0} s; choosing begins");
            ScanProgressBar.Instance?.Done();
            StartChoosing();
        }

        /// <summary>Choose places again (Menu): the saved places are forgotten and Flutter asks again.</summary>
        public void ChooseAgain()
        {
            var tutorial = TutorialDirector.Instance;
            if (tutorial != null && tutorial.Active) return; // after the lesson
            RealHomeStore.Clear();
            PutBack();
            RememberHomes();
            StartChoosing();
        }

        /// <summary>
        /// Flutter asks where each instrument should live, in the lesson's order (owner, headset 2026-11-08: show me the
        /// mapped room and let me choose, instead of guessing). The instruments go back to the furniture the lesson
        /// gave them first (a swap from an earlier session had the table holding the erhu).
        /// </summary>
        public void StartChoosing()
        {
            InstrumentPacks.Instance?.ResetPlaces();
            m_Order.Clear();
            foreach (var type in LessonOrder)
                for (int i = 0; i < m_Stations.Count; i++)
                    if (m_Stations[i].Target.Instrument == type && !m_Order.Contains(i)) m_Order.Add(i);
            for (int i = 0; i < m_Stations.Count; i++) if (!m_Order.Contains(i)) m_Order.Add(i);
            m_Phase = Phase.Choosing;
            m_Choosing = 0;
            MatchedCount = 0;
            RealWalls.ShowScan = true;
            ShowPitchColumns(false);
            Debug.Log("[RoomOrchestra] Real furniture: the lesson is over; the player chooses where each instrument lives");
            AskWhere();
        }

        // The lesson's five-note columns stand beside the virtual furniture; once instruments live on real objects the
        // notes run along those objects instead (owner, 2026-11-08: the notes were out of reach).
        static void ShowPitchColumns(bool on)
        {
            foreach (var column in FindObjectsByType<PitchColumn>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (column.gameObject.activeSelf != on) column.gameObject.SetActive(on);
        }

        static UiTerm PlaceLine(InstrumentType instrument) => instrument switch
        {
            InstrumentType.Udu => UiTerm.PlaceUdu,
            InstrumentType.Mbira => UiTerm.PlaceMbira,
            _ => UiTerm.PlaceErhu
        };

        void AskWhere()
        {
            var target = ChoosingTarget;
            if (target == null) return;
            Debug.Log($"[RoomOrchestra] Real furniture: where should the {target.Instrument} live?");
            if (!Application.isEditor) TutorialDirector.Instance?.Announce(PlaceLine(target.Instrument), SayPlaceSeconds);
        }

        // The object under the pointer while choosing: found once per spot (the scan can be large).
        Bounds m_PreviewBox;
        Vector3 m_PreviewPoint = Vector3.positiveInfinity;
        bool m_HasPreview;
        GameObject m_PreviewShape;
        Material m_PreviewLook;

        /// <summary>Where the pointer touches the room now: the real object there glows, with the instrument's name.</summary>
        public void Preview(Vector3 point)
        {
            var target = ChoosingTarget;
            var cursor = SpotAt(0);
            if (target == null) return;
            if (!m_HasPreview || (point - m_PreviewPoint).sqrMagnitude > 0.04f * 0.04f && !m_PreviewBox.Contains(point))
            {
                m_PreviewPoint = point;
                m_PreviewBox = ObjectAt(point);
                m_HasPreview = true;
                ShowPreviewShape(m_PreviewBox, target.Instrument);
            }
            var top = new Vector3(m_PreviewBox.center.x, m_PreviewBox.max.y, m_PreviewBox.center.z);
            if (cursor != null)
            {
                cursor.Show(top, new Vector2(Mathf.Min(m_PreviewBox.size.x, CursorSide * 2f), Mathf.Min(m_PreviewBox.size.z, CursorSide * 2f)),
                    UiText.NameAndKind(target.Instrument, Localization.Current, 45), tappable: false);
                cursor.ShowIcon(target.Instrument); // its picture above its name (owner, 2026-11-11)
            }
        }

        /// <summary>The real object at a point on the room's surface, as a box in scene space.</summary>
        public static Bounds ObjectAt(Vector3 point)
        {
            var walls = RealWalls.Instance;
            var scan = RoomScan.Instance;
            var box = RoomObjects.Find(point, walls != null ? walls.PieceTriangles : null, scan != null ? scan.Furniture : null, SeatFloor().y);
            if (StageAnchor.Instance != null && StageAnchor.Instance.TryGetHeadPose(out var head))
            {
                box = RoomObjects.ClearOfPlayer(box, point, head.position); // every frame while choosing: no log
            }
            return box;
        }

        void ShowPreviewShape(Bounds box, InstrumentType instrument)
        {
            var walls = RealWalls.Instance;
            var tris = RoomObjects.ShapeIn(box, walls != null ? walls.PieceTriangles : null, SeatFloor().y);
            if (m_PreviewShape == null)
            {
                m_PreviewShape = new GameObject("ChoosingPreview");
                m_PreviewShape.AddComponent<MeshFilter>();
                var r = m_PreviewShape.AddComponent<MeshRenderer>();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                var look = walls != null ? walls.ScanLook : null;
                if (look != null) r.sharedMaterial = m_PreviewLook = new Material(look) { name = "ChoosingPreview" };
            }
            if (m_PreviewLook != null)
            {
                var tint = RealHome.TintFor(instrument);
                m_PreviewLook.SetColor("_BaseColor", new Color(tint.r, tint.g, tint.b, 0.55f));
            }
            var filter = m_PreviewShape.GetComponent<MeshFilter>();
            if (filter.sharedMesh == null) filter.sharedMesh = new Mesh { name = "ChoosingPreview", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            var mesh = filter.sharedMesh;
            mesh.Clear();
            var idx = new int[tris.Count];
            for (int i = 0; i < idx.Length; i++) idx[i] = i;
            mesh.SetVertices(tris);
            mesh.SetTriangles(idx, 0);
            mesh.RecalculateBounds();
            m_PreviewShape.SetActive(tris.Count > 0);
        }

        /// <summary>The pointer left the room's surface.</summary>
        public void HidePreview()
        {
            SpotAt(0)?.ShowIcon(null);
            SpotAt(0)?.Hide();
            m_HasPreview = false;
            if (m_PreviewShape != null) m_PreviewShape.SetActive(false);
        }

        /// <summary>The player pressed on the room's surface: the instrument being placed lives on that real object now.</summary>
        public void PlaceAt(Vector3 point)
        {
            var s = ChoosingStation;
            if (s == null) return;
            var box = m_HasPreview && (m_PreviewBox.Contains(point) || (point - m_PreviewPoint).sqrMagnitude < 0.04f * 0.04f) ? m_PreviewBox : ObjectAt(point);
            LiveOn(s, new Pose(box.center, Quaternion.identity), box.size, withShape: true);
            UiSounds.Play(UiSound.Place, box.center);
            MatchedCount++;
            RealHomeStore.Remember(s.Name, s.Home);
            Physics.SyncTransforms();
            NextChoice();
        }

        /// <summary>The player pressed the instrument itself: it stays on its virtual furniture (its notes along it too).</summary>
        public void KeepVirtual()
        {
            var s = ChoosingStation;
            if (s == null) return;
            Debug.Log($"[RoomOrchestra] Real furniture: the {s.Target.Instrument} stays on its virtual furniture");
            KeepHome(s);
            RealHomeStore.Forget(s.Name);
            NextChoice();
        }

        // A box around its own furniture, so its notes run along it like a real object's.
        void KeepHome(Station s)
        {
            DropHome(s);
            var box = VisibleBox(s.Root);
            s.Home = RealHome.Create(s.Name, s.Target, new Pose(box.center, Quaternion.identity), box.size, null, LabelStyle());
            s.Home.transform.SetParent(s.Root, true); // it moves with its furniture (recentring)
            s.Target.Home = s.Home;
        }

        /// <summary>
        /// The instrument lives on a real object: the virtual furniture goes in a poof, the object's own shape takes the
        /// ball's hits, the ball aims at its top, its echo orbit floats above it and its vines grow around it.
        /// </summary>
        void LiveOn(Station s, Pose pose, Vector3 size, bool withShape)
        {
            DropHome(s);
            var virtualBox = VisibleBox(s.Root);
            var walls = RealWalls.Instance;
            s.Home = RealHome.Create(s.Name, s.Target, pose, size, walls != null ? walls.ScanLook : null, LabelStyle());
            if (withShape) s.Home.SetShape(RoomObjects.ShapeIn(s.Home.WorldBounds, walls != null ? walls.PieceTriangles : null, SeatFloor().y));
            s.Target.Home = s.Home;
            if (!s.Removed) PoofCloud.PoofAt(virtualBox.center, 0.6f);
            PoofCloud.PoofAt(s.Home.Top, 0.5f);
            SetVirtualShown(s, false);
            s.Matched = true;
            s.Piece = new FurniturePiece(FurnitureKind.Other, s.Home.WorldBounds, s.Home.Top.y, 1);
            HoldOnHome(s);
            var jungle = JungleDirector.Instance;
            if (jungle != null)
                jungle.GrowAround(s.Name, new Vector3(virtualBox.center.x, virtualBox.min.y, virtualBox.center.z), virtualBox.size, s.Home.Floor, s.Home.WorldBounds.size);
            Debug.Log($"[RoomOrchestra] Real furniture: the {s.Target.Instrument} lives on the real object at {pose.position:F2} size {size:F2} ({(s.Home.HasShape ? "its scanned shape" : "a box")}); the virtual {s.Name} is gone");
        }

        // The station's own furniture (hidden and still once it lives on a real object), its aim and its orbit, held on
        // the real object every frame: recentring moves the stage, not the room.
        void HoldOnHome(Station s)
        {
            if (s.Home == null || !s.Removed) return;
            var floor = s.Home.Floor;
            if ((s.Root.position - floor).sqrMagnitude > 1e-8f) s.Root.position = floor; // its sound comes from there
            if (s.Aim != null)
            {
                var a = s.Home.Top + Vector3.up * 0.06f;
                if ((s.Aim.position - a).sqrMagnitude > 1e-8f) s.Aim.position = a;
            }
            if (s.Orbit != null)
            {
                var o = s.Target.AimPosition + s.OrbitOffset;
                if ((s.Orbit.position - o).sqrMagnitude > 1e-8f) s.Orbit.position = o;
            }
        }

        static void SetVirtualShown(Station s, bool on)
        {
            s.Removed = !on;
            if (s.Looks != null) foreach (var r in s.Looks) if (r != null) r.enabled = on;
            if (s.Solid != null) foreach (var c in s.Solid) if (c != null) c.enabled = on;
        }

        void DropHome(Station s)
        {
            if (s.Home != null) Destroy(s.Home.gameObject);
            s.Home = null;
            if (s.Target != null) s.Target.Home = null;
        }

        static Bounds VisibleBox(Transform root)
        {
            Bounds box = new Bounds(root.position, Vector3.one * 0.3f);
            bool any = false;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                if (!any) { box = r.bounds; any = true; }
                else box.Encapsulate(r.bounds);
            }
            return box;
        }

        static TMPro.TMP_Text s_LabelStyle;
        /// <summary>The text style the note names on real objects are made from (testing).</summary>
        public static TMPro.TMP_Text NoteLabelStyle() => LabelStyle();
        static TMPro.TMP_Text LabelStyle()
        {
            if (s_LabelStyle != null) return s_LabelStyle;
            var spot = FindFirstObjectByType<RealSpot>(FindObjectsInactive.Include);
            s_LabelStyle = spot != null ? spot.GetComponentInChildren<TMPro.TMP_Text>(true) : null;
            return s_LabelStyle;
        }

        void NextChoice()
        {
            HidePreview();
            m_Choosing++;
            if (m_Choosing < m_Order.Count)
            {
                AskWhere();
                return;
            }
            FinishChoosing();
        }

        /// <summary>
        /// Undo while choosing (owner, headset 2026-11-08: "while selecting furniture, if the player hits the undo
        /// button, it should undo the last furniture that was assigned"): the last instrument placed goes back onto its
        /// virtual furniture and Flutter asks for it again. Also right after the last choice, until a note is played.
        /// False when there is no choice to take back (Undo then works on the song as usual).
        /// </summary>
        public bool UndoChoice()
        {
            bool choosing = m_Phase == Phase.Choosing;
            bool justChosen = m_Phase == Phase.Placed && m_Order.Count > 0 && m_Choosing >= m_Order.Count
                              && (EchoConductor.Instance == null || EchoConductor.Instance.Echoes.Count == 0);
            if ((!choosing && !justChosen) || m_Choosing <= 0) return false;
            m_Choosing = Mathf.Min(m_Choosing, m_Order.Count) - 1;
            var s = m_Stations[m_Order[m_Choosing]];
            var where = s.Home != null ? s.Home.Top : s.Root.position;
            bool wasMatched = s.Matched;
            DropHome(s);
            if (s.Removed)
            {
                SetVirtualShown(s, true);
                JungleDirector.Instance?.GrowAtHome(s.Name);
            }
            s.Matched = false;
            s.GoingHome = false;
            s.MoveStart = -1f;
            s.Root.localPosition = s.HomeLocalPosition;
            s.Root.localRotation = s.HomeLocalRotation;
            s.Root.localScale = s.HomeLocalScale;
            if (s.HitBox != null) s.HitBox.enabled = false;
            if (s.Aim != null) s.Aim.localPosition = s.AimHome;
            if (s.Orbit != null) s.Orbit.position = s.Target.AimPosition + s.OrbitOffset;
            if (wasMatched) MatchedCount = Mathf.Max(0, MatchedCount - 1);
            RealHomeStore.Forget(s.Name);
            PoofCloud.PoofAt(where, 0.5f);
            UiSounds.Play(UiSound.Cancel, where);
            HidePreview();
            m_Phase = Phase.Choosing;
            RealWalls.ShowScan = true;
            Physics.SyncTransforms();
            Debug.Log($"[RoomOrchestra] Real furniture: undo; the {s.Target.Instrument} is back on its virtual {s.Name}, choose again");
            AskWhere();
            return true;
        }

        void FinishChoosing()
        {
            m_Phase = Phase.Placed;
            RealWalls.ShowScan = false;
            Debug.Log($"[RoomOrchestra] Real furniture: {MatchedCount} of {m_Stations.Count} instruments on real places the player chose");
            // a virtual piece kept: say how to move it (owner, headset 2026-11-12: "include instructions ... if the
            // player chooses to use a virtual version of the instrument"), and give it longer to read
            bool kept = m_Stations.Exists(st => !st.Removed && st.Home != null);
            if (!Application.isEditor) TutorialDirector.Instance?.Announce(kept ? UiTerm.PlaceDoneMovable : UiTerm.PlaceDone, kept ? SayResultSeconds * 2f : SayResultSeconds);
        }

        /// <summary>Every real piece an instrument could live on, biggest first (the scan's order).</summary>
        static List<FurniturePiece> Eligible(IReadOnlyList<FurniturePiece> pieces, Vector3 seatFloor)
        {
            var list = new List<FurniturePiece>();
            if (pieces == null) return list;
            foreach (var piece in pieces)
                if (piece.Kind != FurnitureKind.None && Fits(piece, seatFloor)) list.Add(piece);
            return list;
        }

        static bool Fits(FurniturePiece piece, Vector3 seatFloor)
        {
            var flat = piece.Top - seatFloor;
            float height = flat.y;
            flat.y = 0f;
            float reach = flat.magnitude;
            if (reach < MinReach || reach > MaxReach || height < MinTop || height > MaxTop) return false;
            return piece.Kind == FurnitureKind.Lamp || piece.Kind == FurnitureKind.Other
                   || (piece.Box.size.x >= MinTopSide && piece.Box.size.z >= MinTopSide); // a speck is not a table
        }

        void ShowFound()
        {
            var scan = RoomScan.Instance;
            m_Candidates.Clear();
            m_Occupant.Clear();
            foreach (var piece in Eligible(scan != null ? scan.Furniture : null, SeatFloor()))
            {
                if (m_Candidates.Count >= m_Spots.Count) break;
                m_Candidates.Add(piece);
                m_Occupant.Add(-1);
            }
            WriteSpots(reposition: true);
        }

        void WriteSpots(bool reposition = false)
        {
            for (int i = 0; i < m_Spots.Count; i++)
            {
                var spot = m_Spots[i];
                if (i >= m_Candidates.Count)
                {
                    spot.Hide();
                    continue;
                }
                var text = m_Occupant[i] >= 0 ? UiText.NameAndKind(m_Stations[m_Occupant[i]].Target.Instrument, Localization.Current, 45) : "+";
                if (reposition || !spot.Shown)
                {
                    var box = m_Candidates[i].Box;
                    spot.Show(m_Candidates[i].Top, new Vector2(box.size.x, box.size.z), text);
                }
                else spot.SetText(text);
            }
        }

        /// <summary>
        /// A tap on a spot: the next instrument lives there (udu, mbira, erhu, then none). If it lived on another spot,
        /// the one that was here swaps over to that spot; otherwise the one that was here goes home to its virtual piece.
        /// </summary>
        public void Tap(RealSpot spot)
        {
            if (m_Phase != Phase.Placed || spot == null) return;
            int i = spot.Index;
            if (i < 0 || i >= m_Candidates.Count) return;
            int current = m_Occupant[i];
            int next = current + 1 >= m_Stations.Count ? -1 : current + 1;
            int was = next >= 0 ? m_Occupant.IndexOf(next) : -1;
            if (current >= 0)
            {
                // swap: the one here goes where the newcomer came from (or home), so other spots keep an instrument
                if (was >= 0)
                {
                    m_Occupant[was] = current;
                    MoveOnto(m_Stations[current], m_Candidates[was]);
                }
                else SendHome(current);
            }
            else if (was >= 0) m_Occupant[was] = -1;
            if (next >= 0)
            {
                m_Occupant[i] = next;
                MoveOnto(m_Stations[next], m_Candidates[i]);
            }
            else m_Occupant[i] = -1;
            MatchedCount = 0;
            foreach (var s in m_Stations) if (s.Matched) MatchedCount++;
            WriteSpots();
            Physics.SyncTransforms();
            var name = next >= 0 ? m_Stations[next].Target.Instrument.ToString() : "nothing";
            Debug.Log($"[RoomOrchestra] Real furniture: spot {i} ({m_Candidates[i].Kind}) now has {name}");
        }

        void SendHome(int station)
        {
            var s = m_Stations[station];
            if (!s.Matched) return;
            s.Matched = false;
            s.GoingHome = true;
            if (s.HitBox != null) s.HitBox.enabled = false;
            if (s.Aim != null) s.Aim.localPosition = s.AimHome;
            s.From = s.Root.position;
            s.FromScale = s.Root.localScale;
            s.To = s.Root.parent != null ? s.Root.parent.TransformPoint(s.HomeLocalPosition) : s.HomeLocalPosition;
            s.ToScale = s.HomeLocalScale;
            s.MoveStart = Time.time;
            PoofCloud.PoofAt(s.From, 0.4f);
        }

        void LateUpdate()
        {
            if (m_Phase != Phase.Placed && m_Phase != Phase.Choosing && m_Phase != Phase.Restoring) return;
            foreach (var s in m_Stations)
            {
                if (s.Removed)
                {
                    HoldOnHome(s);
                    continue;
                }
                if (!s.Matched && !s.GoingHome) continue;
                float u = s.MoveStart < 0f ? 1f : Mathf.Clamp01((Time.time - s.MoveStart) / MoveSeconds);
                float e = u * u * (3f - 2f * u);
                var p = Vector3.Lerp(s.From, s.To, e) + Vector3.up * (HopHeight * 4f * u * (1f - u));
                var scale = Vector3.Lerp(s.FromScale, s.ToScale, e);
                if (u >= 1f) s.MoveStart = -1f;
                if (s.GoingHome && u >= 1f)
                {
                    s.GoingHome = false; // home: back in the stage's own space (it follows recentring again)
                    s.Root.localPosition = s.HomeLocalPosition;
                    s.Root.localScale = s.HomeLocalScale;
                    if (s.Orbit != null) s.Orbit.position = s.Target.AimPosition + s.OrbitOffset;
                    continue;
                }
                if ((s.Root.position - p).sqrMagnitude > 1e-8f) s.Root.position = p; // also holds it there if the stage is recentred
                if (s.Root.localScale != scale) s.Root.localScale = scale;
                if (s.Matched && s.HitBox != null) FitHitBox(s);
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
                    if (!Fits(piece, seatFloor)) continue;
                    var flat = piece.Top - seatFloor;
                    flat.y = 0f;
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
            // the spots: every suggested piece, then the other pieces found, biggest first, as many as there are spots
            var scan = RoomScan.Instance;
            m_Candidates.Clear();
            m_Occupant.Clear();
            for (int i = 0; i < picks.Length; i++)
                if (picks[i].HasValue && m_Candidates.Count < Mathf.Max(m_Spots.Count, picks.Length))
                {
                    m_Candidates.Add(picks[i].Value);
                    m_Occupant.Add(i);
                }
            foreach (var piece in Eligible(scan != null ? scan.Furniture : null, SeatFloor()))
            {
                if (m_Candidates.Count >= m_Spots.Count) break;
                if (!m_Candidates.Exists(c => c.Box.center == piece.Box.center)) { m_Candidates.Add(piece); m_Occupant.Add(-1); }
            }
            MatchedCount = 0;
            for (int i = 0; i < m_Stations.Count; i++)
            {
                if (!picks[i].HasValue) continue;
                MoveOnto(m_Stations[i], picks[i].Value);
                MatchedCount++;
            }
            m_Phase = Phase.Placed;
            WriteSpots(reposition: true);
            Physics.SyncTransforms();
        }

        void MoveOnto(Station s, FurniturePiece piece)
        {
            bool fromHome = !s.Matched && !s.GoingHome;
            s.Matched = true;
            s.GoingHome = false;
            s.Piece = piece;
            s.From = s.Root.position;
            s.FromScale = s.Root.localScale;
            s.ToScale = s.HomeLocalScale * MarkerScale;
            // its feet on the real top: how far its pivot sits above its lowest point, at marker size
            if (fromHome) s.BaseOffset = (s.Root.position.y - Lowest(s.Root)) * MarkerScale; // measured at its own size
            s.To = piece.Top + Vector3.up * s.BaseOffset;
            s.MoveStart = Time.time;
            if (s.HitBox == null)
            {
                var go = new GameObject($"RealHitBox_{s.Name}");
                go.transform.SetParent(s.Root, false);
                s.HitBox = go.AddComponent<BoxCollider>();
            }
            s.HitBox.enabled = true;
            PoofCloud.PoofAt(s.From, 0.4f);
            PoofCloud.PoofAt(piece.Top, 0.5f);
            Debug.Log($"[RoomOrchestra] Real furniture: {s.Name} ({s.Target.Instrument}) moves onto the real {piece}");
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
            HidePreview();
            foreach (var s in m_Stations)
            {
                DropHome(s);
                if (s.Removed)
                {
                    SetVirtualShown(s, true);
                    s.Root.localPosition = s.HomeLocalPosition;
                    JungleDirector.Instance?.GrowAtHome(s.Name);
                }
                if (!s.Matched && !s.GoingHome) continue;
                s.Matched = false;
                s.GoingHome = false;
                s.MoveStart = -1f;
                s.Root.localPosition = s.HomeLocalPosition;
                s.Root.localRotation = s.HomeLocalRotation;
                s.Root.localScale = s.HomeLocalScale;
                if (s.HitBox != null) s.HitBox.enabled = false;
                if (s.Aim != null) s.Aim.localPosition = s.AimHome;
                if (s.Orbit != null) s.Orbit.position = s.Target.AimPosition + s.OrbitOffset;
            }
            MatchedCount = 0;
            m_Candidates.Clear();
            m_Occupant.Clear();
            foreach (var spot in m_Spots) spot.Hide();
            RealWalls.ShowScan = false;
            ShowPitchColumns(true);
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
