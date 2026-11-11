using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// How smoothly the game runs on the headset, written to the session log every 10 s: the average frame rate, the
    /// slowest frame and how many frames missed the display's rate. V4 turned on 4x anti-aliasing at full resolution
    /// and added the meadow; this is the evidence that it still holds the headset's frame rate. Device only.
    /// </summary>
    public class FrameRateLog : MonoBehaviour
    {
        public const float WindowSeconds = 10f;

        float m_Start, m_Worst;
        int m_Frames, m_Slow;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Begin()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            var go = new GameObject("FrameRateLog");
            DontDestroyOnLoad(go);
            go.AddComponent<FrameRateLog>();
#endif
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (m_Start <= 0f) m_Start = Time.unscaledTime;
            m_Frames++;
            m_Worst = Mathf.Max(m_Worst, dt);
            float target = 1f / Mathf.Max(60f, Screen.currentResolution.refreshRateRatio.value > 1 ? (float)Screen.currentResolution.refreshRateRatio.value : 72f);
            if (dt > target * 1.5f) m_Slow++;
            float span = Time.unscaledTime - m_Start;
            if (span < WindowSeconds) return;
            Debug.Log($"[RoomOrchestra] Frame rate: {m_Frames / span:F0} fps average, slowest {m_Worst * 1000f:F0} ms, {m_Slow} of {m_Frames} frames late");
            m_Start = Time.unscaledTime;
            m_Frames = 0;
            m_Slow = 0;
            m_Worst = 0f;
        }
    }
}
