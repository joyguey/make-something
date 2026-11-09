using UnityEngine;
using UnityEngine.SceneManagement;

namespace RoomOrchestra
{
    /// <summary>
    /// Menu: start completely over (owner, headset 2026-11-08: "start completely over including the scanning process",
    /// for a demo video). Asks first. Then everything this app saved on the headset is wiped (the song is not saved;
    /// the chosen furniture and its spatial anchors, settings, language, lesson done) and the game begins again at the
    /// title: lesson, then looking around the whole room while the scan fills in, then choosing furniture. The headset's
    /// own room data belongs to PICO's system and is not touched.
    /// </summary>
    public class StartOverButton : MonoBehaviour, IPressable
    {
        public void Press()
        {
            MenuTray.Instance?.SetOpen(false);
            Debug.Log("[RoomOrchestra] Start over: wiping saved furniture, settings and the lesson; beginning again");
            RealHomeStore.ForgetAnchorsOnDevice();
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            StartOver.Reset();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }

    /// <summary>What a fresh start resets in memory (statics that outlive a scene load).</summary>
    public static class StartOver
    {
        public static event System.Action Resetting;

        public static void Reset()
        {
            ComfortSettings.ResetToDefaults();
            EnvironmentOcclusion.ForgetSetting();
            Resetting?.Invoke();
        }
    }
}
