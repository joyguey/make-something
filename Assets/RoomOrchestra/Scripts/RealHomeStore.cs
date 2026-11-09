using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// Remembers where each instrument lives between sessions (owner, 2026-11-08: "If it's possible to remember the
    /// furnitures based on the last session, build that into the app as well"). Each chosen real object gets a PICO
    /// spatial anchor, persisted on the headset's own storage; the app keeps only the anchor's id and the object's
    /// size in its local preferences. Next time the anchors are looked up again, and each instrument goes back onto its
    /// object wherever the headset now puts it. Nothing leaves the device. Without anchors (the Editor, an emulator
    /// without them, or an anchor not found) the player simply chooses again.
    /// </summary>
    public static class RealHomeStore
    {
        public const string PrefKey = "LittleEchoes.RealHomes.v1";
        /// <summary>How long looking up or saving an anchor may take before giving up (s).</summary>
        public const int TimeoutSeconds = 8;

        [Serializable]
        public class Entry
        {
            public string furniture;
            public string anchor; // the spatial anchor's id, or empty while it is being made
            public float sx, sy, sz;
        }

        [Serializable]
        class Saved
        {
            public List<Entry> homes = new();
        }

        /// <summary>The saved places, by furniture name.</summary>
        public static List<Entry> Load()
        {
            try
            {
                var json = PlayerPrefs.GetString(PrefKey, "");
                if (string.IsNullOrEmpty(json)) return new List<Entry>();
                return JsonUtility.FromJson<Saved>(json)?.homes ?? new List<Entry>();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RoomOrchestra] Saved places unreadable, choosing again: {e.Message}");
                return new List<Entry>();
            }
        }

        static void Save(List<Entry> homes)
        {
            PlayerPrefs.SetString(PrefKey, JsonUtility.ToJson(new Saved { homes = homes }));
            PlayerPrefs.Save();
        }

        public static bool HasAnchors()
        {
            foreach (var e in Load()) if (!string.IsNullOrEmpty(e.anchor)) return true;
            return false;
        }

        /// <summary>Forgets every saved place (the player chooses again).</summary>
        public static void Clear()
        {
            PlayerPrefs.DeleteKey(PrefKey);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Start over: the saved places' anchors are removed from the headset's storage too (in the background, best
        /// effort; read before the preferences are wiped). Then nothing is found again next time.
        /// </summary>
        public static void ForgetAnchorsOnDevice()
        {
            var ids = new List<Guid>();
            foreach (var e in Load()) if (Guid.TryParse(e.anchor, out var g)) ids.Add(g);
            Clear();
            if (ids.Count > 0) _ = UnpersistAsync(ids);
        }

        static async Task UnpersistAsync(List<Guid> ids)
        {
#if UNITY_ANDROID && !UNITY_EDITOR && ENABLE_PICO_XR_SDK
            try
            {
                if (!await StartAnchors()) return;
                using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds));
                var (_, handles) = await ByteDance.PICO.XR.PXR_MixedReality.QuerySpatialAnchorAsync(ids.ToArray(), cancel.Token);
                int removed = 0;
                if (handles != null)
                    foreach (var handle in handles)
                        if (await ByteDance.PICO.XR.PXR_MixedReality.UnPersistSpatialAnchorAsync(handle, cancel.Token) == ByteDance.PICO.XR.PxrResult.SUCCESS) removed++;
                Debug.Log($"[RoomOrchestra] Start over: {removed} of {ids.Count} saved anchors removed from the headset");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RoomOrchestra] Start over: anchors not removed ({e.Message}); they are forgotten anyway");
            }
#else
            await Task.Yield();
#endif
        }

        /// <summary>The instrument on <paramref name="furniture"/> stays on its own furniture: nothing to remember.</summary>
        public static void Forget(string furniture)
        {
            var homes = Load();
            if (homes.RemoveAll(e => e.furniture == furniture) > 0) Save(homes);
        }

        /// <summary>Remembers <paramref name="home"/>: an anchor where it stands, saved on the headset.</summary>
        public static void Remember(string furniture, RealHome home)
        {
            if (home == null) return;
            var size = home.Size;
            _ = RememberAsync(furniture, new Pose(home.transform.position, home.transform.rotation), size, home);
        }

        static async Task RememberAsync(string furniture, Pose pose, Vector3 size, RealHome home)
        {
            string id = await MakeAnchor(pose);
            if (home == null) return; // taken back (Undo, Choose furniture) while the anchor was being made
            var homes = Load();
            homes.RemoveAll(e => e.furniture == furniture);
            if (string.IsNullOrEmpty(id))
            {
                Save(homes);
                return; // no anchor: it is not remembered (it would come back in the wrong place)
            }
            homes.Add(new Entry { furniture = furniture, anchor = id, sx = size.x, sy = size.y, sz = size.z });
            Save(homes);
            Debug.Log($"[RoomOrchestra] Saved places: the {furniture}'s real object is remembered on this headset");
        }

        /// <summary>Each saved place found again: its pose now (scene space) and its size. Empty when none is found.</summary>
        public static async Task<Dictionary<string, (Pose pose, Vector3 size)>> FindAll()
        {
            var found = new Dictionary<string, (Pose, Vector3)>();
            var homes = Load();
            if (homes.Count == 0) return found;
#if UNITY_ANDROID && !UNITY_EDITOR && ENABLE_PICO_XR_SDK
            try
            {
                if (!await StartAnchors()) return found;
                var ids = new List<Guid>();
                foreach (var e in homes) if (Guid.TryParse(e.anchor, out var g)) ids.Add(g);
                if (ids.Count == 0) return found;
                using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds));
                var (result, handles) = await ByteDance.PICO.XR.PXR_MixedReality.QuerySpatialAnchorAsync(ids.ToArray(), cancel.Token);
                Debug.Log($"[RoomOrchestra] Saved places: looked up {ids.Count} anchors: {result}, {handles?.Count ?? 0} found");
                if (handles == null) return found;
                foreach (var handle in handles)
                {
                    if (ByteDance.PICO.XR.PXR_MixedReality.GetAnchorUuid(handle, out var uuid) != ByteDance.PICO.XR.PxrResult.SUCCESS) continue;
                    var entry = homes.Find(e => Guid.TryParse(e.anchor, out var g) && g == uuid);
                    if (entry == null) continue;
                    if (ByteDance.PICO.XR.PXR_MixedReality.LocateAnchor(handle, out var position, out var rotation) != ByteDance.PICO.XR.PxrResult.SUCCESS) continue;
                    // the anchor is in tracking space; the rig sits at the scene's origin, so that is scene space
                    var upright = Quaternion.Euler(0f, rotation.eulerAngles.y, 0f); // furniture stands upright
                    found[entry.furniture] = (new Pose(position, upright), new Vector3(entry.sx, entry.sy, entry.sz));
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RoomOrchestra] Saved places: lookup failed ({e.Message}); choosing again");
            }
#else
            await Task.Yield();
#endif
            return found;
        }

        static async Task<string> MakeAnchor(Pose pose)
        {
#if UNITY_ANDROID && !UNITY_EDITOR && ENABLE_PICO_XR_SDK
            try
            {
                if (!await StartAnchors()) return null;
                using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds));
                var (result, handle, uuid) = await ByteDance.PICO.XR.PXR_MixedReality.CreateSpatialAnchorAsync(pose.position, pose.rotation, cancel.Token);
                if (result != ByteDance.PICO.XR.PxrResult.SUCCESS)
                {
                    Debug.LogWarning($"[RoomOrchestra] Saved places: no anchor ({result})");
                    return null;
                }
                var persisted = await ByteDance.PICO.XR.PXR_MixedReality.PersistSpatialAnchorAsync(handle, cancel.Token);
                if (persisted != ByteDance.PICO.XR.PxrResult.SUCCESS)
                {
                    Debug.LogWarning($"[RoomOrchestra] Saved places: anchor not saved ({persisted})");
                    return null;
                }
                return uuid.ToString();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RoomOrchestra] Saved places: anchor failed ({e.Message})");
                return null;
            }
#else
            await Task.Yield();
            return null;
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR && ENABLE_PICO_XR_SDK
        static bool s_Started;

        static async Task<bool> StartAnchors()
        {
            if (s_Started) return true;
            using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds));
            var result = await ByteDance.PICO.XR.PXR_MixedReality.StartSenseDataProvider(ByteDance.PICO.XR.PxrSenseDataProviderType.SpatialAnchor, cancel.Token);
            s_Started = result == ByteDance.PICO.XR.PxrResult.SUCCESS;
            Debug.Log($"[RoomOrchestra] Saved places: spatial anchors {(s_Started ? "ready" : $"unavailable ({result})")}");
            return s_Started;
        }
#endif
    }
}
