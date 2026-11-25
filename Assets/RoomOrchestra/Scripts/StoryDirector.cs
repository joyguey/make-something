using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// Story mode, feel prototype for the owner's headset checkpoint (2026-11-25; docs/STORY_MODE.md). Grey-box on
    /// purpose: it tests how flicking a friend feels, not the look. A "Story test" sticker beside the leaf starts it:
    /// a portal ring opens ahead; Discords (Squeak = small ball, Wobbler = tall capsule, Clang = cube) drift out toward
    /// the player; the current friend (the real model, small) floats above the left hand, the next two at the wrist;
    /// a quick right-hand move from beside it flicks it (a pinch or the trigger aims and launches instead: the
    /// accessible way); the left hand's pinch (or its trigger) is Hold. The right friend beats a Discord; the wrong one
    /// makes it grow. Discords that reach the player drift in the room and fill the Discord meter (a grey mist thickens
    /// round the player); full = lost. Beat them all = won. Sandbox input pauses while it runs and comes back after.
    /// Everything here is built at runtime, so the Sandbox scene is untouched.
    /// </summary>
    public class StoryDirector : MonoBehaviour
    {
        public const int Waves = 12;
        public const float SpawnEvery = 3.2f, DriftSpeed = 0.22f, EscapeDistance = 0.9f, HitRadius = 0.16f;

        public static StoryDirector Instance { get; private set; }
        public bool Running { get; private set; }
        public FriendQueue Queue { get; private set; }
        public DiscordMeter Meter { get; private set; }

        sealed class Discord { public DiscordKind Kind; public Transform Body; public bool Loose; public float Size = 1f; public float Phase; }
        sealed class Shot { public OrbType Friend; public Transform Body; public Vector3 Velocity; public float Until; public HashSet<Discord> Hit = new(); }

        readonly List<Discord> m_Discords = new();
        readonly List<Shot> m_Shots = new();
        readonly FlickDetector m_Flick = new();
        Transform m_Root, m_Portal, m_PalmFriend, m_Mist;
        readonly Transform[] m_Wrist = new Transform[FriendQueue.Preview];
        TMP_Text m_Status;
        int m_Spawned, m_Beaten;
        float m_NextSpawn, m_EndAt = -1f;
        bool m_WasRightSelect, m_WasLeftSelect;
        Material m_MistMat;
        InstrumentInputRouter m_Router;
        LeafGrab m_LeafGrab;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (FindFirstObjectByType<StoryDirector>() != null || FindFirstObjectByType<InstrumentInputRouter>() == null) return;
            new GameObject("StoryDirector").AddComponent<StoryDirector>();
        }

        void OnEnable() => Instance = this;
        void OnDisable() { if (Instance == this) Instance = null; }

        void Start() => MakeStartSticker();

        // ---------- entry: a temporary "Story test" sticker beside the leaf (prototype only) ----------
        void MakeStartSticker()
        {
            var intro = IntroSequence.Instance;
            var leaf = intro != null ? intro.Leaf : null;
            if (leaf == null) return;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "StoryTestSticker";
            go.transform.SetParent(leaf, false);
            go.transform.localPosition = new Vector3(-0.62f, 0.12f, 0.05f);
            go.transform.localScale = new Vector3(0.14f, 0.05f, 0.08f);
            Paint(go, new Color(1f, 0.82f, 0.25f), 0.4f);
            go.AddComponent<StoryStartSticker>();
            Words(go.transform, "Story\ntest", new Vector3(0f, 0.9f, 0f), 0.35f);
        }

        public void Begin()
        {
            if (Running) return;
            Running = true;
            m_Router = FindFirstObjectByType<InstrumentInputRouter>();
            m_LeafGrab = FindFirstObjectByType<LeafGrab>();
            if (m_LeafGrab != null) m_LeafGrab.enabled = false;
            if (m_Router != null) m_Router.enabled = false; // Sandbox taps and throws pause; its pointer source is still read here
            EchoConductor.Instance?.SetSongMuted(true);
            Queue = new FriendQueue(System.Environment.TickCount);
            Meter = new DiscordMeter(1f);
            m_Spawned = m_Beaten = 0;
            m_NextSpawn = Time.time + 2f;
            m_EndAt = -1f;
            m_Root = new GameObject("StoryPrototype").transform;
            var head = Head();
            var fwd = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
            // the portal: a glowing ring 1.7 m ahead and a little right, at eye height
            m_Portal = new GameObject("Portal").transform;
            m_Portal.SetParent(m_Root);
            m_Portal.position = head.position + fwd * 1.7f + Vector3.Cross(Vector3.up, fwd) * 0.35f;
            m_Portal.rotation = Quaternion.LookRotation(-fwd);
            var ring = m_Portal.gameObject.AddComponent<LineRenderer>();
            ring.loop = true; ring.positionCount = 64; ring.useWorldSpace = false; ring.widthMultiplier = 0.025f;
            for (int i = 0; i < 64; i++) { float a = i / 64f * Mathf.PI * 2f; ring.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 0.5f); }
            ring.material = Glow(new Color(1f, 0.85f, 0.5f));
            m_PalmFriend = FriendModel(Queue.Current, 0.07f);
            for (int i = 0; i < m_Wrist.Length; i++) m_Wrist[i] = FriendModel(Queue.Next[i], 0.035f);
            // the Discord meter: a grey mist round the player, thicker as the room fills
            var mist = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(mist.GetComponent<Collider>());
            mist.name = "DiscordMist"; m_Mist = mist.transform; m_Mist.SetParent(m_Root); m_Mist.localScale = Vector3.one * 3.2f;
            m_MistMat = Paint(mist, new Color(0.45f, 0.44f, 0.47f, 0f), 0f, transparent: true, cullFront: true);
            var status = new GameObject("StoryStatus").transform; status.SetParent(m_Root);
            status.position = m_Portal.position + Vector3.up * 0.65f; status.rotation = m_Portal.rotation * Quaternion.Euler(0, 180, 0);
            m_Status = Words(status, "", Vector3.zero, 0.07f);
            Say("Flick the friend on your left palm at the Discords!\nFlutter beats Squeaks (balls), Bop beats Wobblers (tall), Boom beats Clangs (cubes).");
            Debug.Log("[RoomOrchestra] Story prototype: started");
        }

        void End(string why)
        {
            Debug.Log($"[RoomOrchestra] Story prototype: {why}");
            Running = false;
            if (m_Root != null) Destroy(m_Root.gameObject);
            m_Discords.Clear(); m_Shots.Clear();
            if (m_PalmFriend != null) Destroy(m_PalmFriend.gameObject);
            for (int i = 0; i < m_Wrist.Length; i++) if (m_Wrist[i] != null) Destroy(m_Wrist[i].gameObject);
            if (m_LeafGrab != null) m_LeafGrab.enabled = true;
            if (m_Router != null) m_Router.enabled = true;
            EchoConductor.Instance?.SetSongMuted(false);
        }

        void Update()
        {
            if (!Running) return;
            float dt = Time.deltaTime, now = Time.time;
            if (m_EndAt > 0f) { if (now >= m_EndAt) End(m_Status != null ? m_Status.text : "over"); return; }
            var pointers = m_Router != null ? m_Router.Pointers : null;
            var rig = XrRig.Instance != null ? XrRig.Instance.transform : null;
            PointerFrame left = pointers != null ? pointers.Read(0) : default, right = pointers != null ? pointers.Read(1) : default;
            Vector3 W(Vector3 p) => rig != null ? rig.TransformPoint(p) : p;
            var head = Head();
            if (left.Grip && right.Grip) { End("left by the player (both fists)"); return; }  // a way out at any time
            // the friend floats above the left palm (or in front, low, if the left hand isn't seen)
            var palm = (left.Tracked || left.BarePalm) ? W(left.GripPosition) : head.position + head.forward * 0.35f - head.up * 0.25f - head.right * 0.12f;
            var friendAt = palm + Vector3.up * 0.09f;
            Place(m_PalmFriend, friendAt, head.position, now);
            for (int i = 0; i < m_Wrist.Length; i++) Place(m_Wrist[i], palm - head.right * 0.02f + Vector3.down * (0.05f + 0.045f * i) + head.forward * -0.02f, head.position, now);
            // Hold: the left hand's pinch or trigger (the palm flip comes after the checkpoint)
            if (left.Select && !m_WasLeftSelect && Queue.Hold()) { UiSounds.Play(UiSound.Open, friendAt); RefreshHand(); }
            m_WasLeftSelect = left.Select;
            // flick: a quick right-hand move from beside the friend; or pinch / trigger to aim and launch
            var rightHand = right.Tracked || right.BarePalm ? W(right.Gaze ? right.HandPosition : right.GripPosition) : Vector3.positiveInfinity;
            if (!float.IsInfinity(rightHand.x) && m_Flick.Step(rightHand, friendAt, now, dt, out var dir)) Launch(friendAt, dir, "flick");
            else if (right.Select && !m_WasRightSelect)
            {
                var aimFrom = W(right.Gaze ? right.HandPosition : right.Position);
                var aimDir = (rig != null ? rig.rotation : Quaternion.identity) * (right.Gaze ? right.HandRotation : right.Rotation) * Vector3.forward;
                Launch(friendAt, (aimFrom + aimDir * 3f - friendAt).normalized, "aim");
            }
            m_WasRightSelect = right.Select;
            Spawn(now);
            MoveDiscords(head.position, dt, now);
            MoveShots(dt, now);
            int loose = 0; foreach (var d in m_Discords) if (d.Loose) loose++;
            Meter.Tick(dt, loose);
            m_MistMat.color = new Color(0.45f, 0.44f, 0.47f, Meter.Level * 0.7f);
            m_Mist.position = head.position;
            if (Meter.Lost) { Say("The room filled with Discord. Level lost."); UiSounds.Play(UiSound.Cancel, head.position); m_EndAt = now + 3f; }
            else if (m_Spawned >= Waves && m_Discords.Count == 0) { Say($"You did it! {m_Beaten} Discords turned back into music."); UiSounds.Play(UiSound.Place, head.position); m_EndAt = now + 3f; }
        }

        void Launch(Vector3 from, Vector3 direction, string how)
        {
            var targets = new Vector3[m_Discords.Count];
            for (int i = 0; i < targets.Length; i++) targets[i] = m_Discords[i].Body.position;
            direction = FlickDetector.Assist(from, direction, targets, 22f);
            var friend = Queue.Flick();
            float speed = friend == OrbType.Flutter ? 6f : friend == OrbType.Bop ? 4.5f : 3.4f;
            var body = FriendModel(friend, 0.07f);
            body.SetParent(m_Root); body.position = from;
            m_Shots.Add(new Shot { Friend = friend, Body = body, Velocity = direction * speed, Until = Time.time + 2.5f });
            UiSounds.Play(UiSound.Press, from);
            Debug.Log($"[RoomOrchestra] Story prototype: {friend} launched ({how}, hand speed {m_Flick.Speed:F1} m/s)");
            RefreshHand();
        }

        void RefreshHand()
        {
            ReplaceModel(ref m_PalmFriend, Queue.Current, 0.07f);
            for (int i = 0; i < m_Wrist.Length; i++) ReplaceModel(ref m_Wrist[i], Queue.Next[i], 0.035f);
        }

        void ReplaceModel(ref Transform slot, OrbType friend, float size)
        {
            if (slot != null) Destroy(slot.gameObject);
            slot = FriendModel(friend, size);
        }

        void Spawn(float now)
        {
            if (m_Spawned >= Waves || now < m_NextSpawn) return;
            m_NextSpawn = now + SpawnEvery * (m_Spawned < 4 ? 1.3f : 1f);
            var kind = (DiscordKind)Random.Range(0, 3);
            var shape = kind == DiscordKind.Squeak ? PrimitiveType.Sphere : kind == DiscordKind.Wobbler ? PrimitiveType.Capsule : PrimitiveType.Cube;
            var go = GameObject.CreatePrimitive(shape);
            Destroy(go.GetComponent<Collider>());
            go.name = $"Discord_{kind}";
            go.transform.SetParent(m_Root);
            var r = Random.insideUnitCircle * 0.3f;
            go.transform.position = m_Portal.TransformPoint(new Vector3(r.x, r.y, -0.05f));
            go.transform.localScale = kind == DiscordKind.Wobbler ? new Vector3(0.11f, 0.12f, 0.11f) : Vector3.one * (kind == DiscordKind.Squeak ? 0.1f : 0.14f);
            Paint(go, kind == DiscordKind.Squeak ? new Color(0.95f, 0.4f, 0.6f) : kind == DiscordKind.Wobbler ? new Color(0.6f, 0.4f, 0.95f) : new Color(0.55f, 0.55f, 0.6f), 0.5f);
            m_Discords.Add(new Discord { Kind = kind, Body = go.transform, Phase = Random.value * 10f });
            m_Spawned++;
        }

        void MoveDiscords(Vector3 head, float dt, float now)
        {
            foreach (var d in m_Discords)
            {
                var p = d.Body.position;
                if (!d.Loose)
                {
                    var to = head - p;
                    p += to.normalized * DriftSpeed * dt + new Vector3(Mathf.Sin(now * 2f + d.Phase), Mathf.Cos(now * 1.7f + d.Phase), 0f) * 0.05f * dt;
                    if (to.magnitude < EscapeDistance)
                    {
                        d.Loose = true;
                        UiSounds.Play(UiSound.Cancel, p);
                        Debug.Log($"[RoomOrchestra] Story prototype: a {d.Kind} got into the room");
                    }
                }
                else  // loose in the room: circling the player
                {
                    float a = now * 0.5f + d.Phase;
                    var target = head + new Vector3(Mathf.Cos(a), Mathf.Sin(a * 0.7f) * 0.25f, Mathf.Sin(a)) * 1.1f;
                    p = Vector3.MoveTowards(p, target, 0.4f * dt);
                }
                d.Body.position = p;
                d.Body.Rotate(0f, 90f * dt, 0f);
            }
        }

        void MoveShots(float dt, float now)
        {
            for (int i = m_Shots.Count - 1; i >= 0; i--)
            {
                var s = m_Shots[i];
                if (s.Friend == OrbType.Boom) s.Velocity += Vector3.down * 1.2f * dt; // a heavy lob
                s.Body.position += s.Velocity * dt;
                s.Body.Rotate(360f * dt, 0f, 0f);
                bool spent = now > s.Until;
                for (int k = m_Discords.Count - 1; k >= 0 && !spent; k--)
                {
                    var d = m_Discords[k];
                    if (s.Hit.Contains(d)) continue;
                    float reach = HitRadius * d.Size + (s.Friend == OrbType.Boom ? 0.12f : 0f);
                    if (Vector3.Distance(s.Body.position, d.Body.position) > reach) continue;
                    s.Hit.Add(d);
                    if (StoryRules.Matches(s.Friend, d.Kind))
                    {
                        UiSounds.Play(UiSound.Confirm, d.Body.position);
                        PoofCloud.PoofAt(d.Body.position, 0.4f);
                        if (d.Loose) Meter.Beaten();
                        Destroy(d.Body.gameObject); m_Discords.RemoveAt(k); m_Beaten++;
                        Debug.Log($"[RoomOrchestra] Story prototype: {s.Friend} beat a {d.Kind}");
                        if (s.Friend != OrbType.Flutter) spent = true; // Flutter pierces on
                    }
                    else
                    {
                        UiSounds.Play(UiSound.Cancel, d.Body.position);
                        d.Size *= 1.15f; d.Body.localScale *= 1.15f; // the wrong friend: it grows
                        spent = true;
                        Debug.Log($"[RoomOrchestra] Story prototype: {s.Friend} can't beat a {d.Kind}: it grows");
                    }
                }
                if (spent) { Destroy(s.Body.gameObject); m_Shots.RemoveAt(i); }
            }
        }

        // ---------- helpers ----------
        static Transform Head() => Camera.main != null ? Camera.main.transform : new GameObject("NoHead").transform;

        static void Place(Transform t, Vector3 at, Vector3 look, float now)
        {
            if (t == null) return;
            t.position = at + Vector3.up * Mathf.Sin(now * 3f) * 0.006f;
            var d = look - at; d.y = 0f;
            if (d.sqrMagnitude > 1e-4f) t.rotation = Quaternion.LookRotation(d);
        }

        /// <summary>A small copy of a friend's real model (its meshes only, from the leaf's token), `size` m tall.</summary>
        Transform FriendModel(OrbType friend, float size)
        {
            var holder = new GameObject($"Story_{friend}").transform;
            OrbToken token = null;
            foreach (var t in FindObjectsByType<OrbToken>(FindObjectsSortMode.None)) if (t.Orb == friend) { token = t; break; }
            if (token == null)
            {
                var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(ball.GetComponent<Collider>());
                ball.transform.SetParent(holder, false); ball.transform.localScale = Vector3.one * size;
                return holder;
            }
            var bounds = new Bounds(token.transform.position, Vector3.zero);
            foreach (var r in token.GetComponentsInChildren<MeshRenderer>()) bounds.Encapsulate(r.bounds);
            float k = size / Mathf.Max(0.01f, bounds.size.y);
            foreach (var r in token.GetComponentsInChildren<MeshRenderer>())
            {
                var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
                var part = new GameObject(r.name).transform;
                part.SetParent(holder, false);
                part.localPosition = (r.transform.position - bounds.center) * k;
                part.localRotation = Quaternion.Inverse(token.transform.rotation) * r.transform.rotation;
                part.localScale = r.transform.lossyScale * k;
                part.gameObject.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                var mr = part.gameObject.AddComponent<MeshRenderer>(); mr.sharedMaterials = r.sharedMaterials;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            return holder;
        }

        static Material Paint(GameObject go, Color c, float glow, bool transparent = false, bool cullFront = false)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            var m = new Material(shader != null ? shader : Shader.Find("Sprites/Default")) { color = c };
            if (transparent)
            {
                m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha); m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0); m.renderQueue = 3000; m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            if (cullFront) m.SetFloat("_Cull", 1f);
            go.GetComponent<Renderer>().sharedMaterial = m;
            return m;
        }

        static Material Glow(Color c)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            return new Material(shader != null ? shader : Shader.Find("Sprites/Default")) { color = c };
        }

        static TMP_Text Words(Transform parent, string text, Vector3 at, float height)
        {
            var go = new GameObject("Words"); go.transform.SetParent(parent, false); go.transform.localPosition = at;
            var t = go.AddComponent<TextMeshPro>();
            var style = RealFurniture.NoteLabelStyle();
            if (style != null) { t.font = style.font; t.fontSharedMaterial = style.fontSharedMaterial; }
            t.fontSize = height * 10f; t.alignment = TextAlignmentOptions.Center; t.color = Color.white;
            t.rectTransform.sizeDelta = new Vector2(1.6f, 0.4f); t.text = text;
            return t;
        }

        void Say(string text) { if (m_Status != null) m_Status.text = text; }
    }

    /// <summary>The temporary "Story test" sticker beside the leaf (prototype checkpoint only).</summary>
    public class StoryStartSticker : MonoBehaviour, IPressable
    {
        public void Press() => StoryDirector.Instance?.Begin();
    }
}
