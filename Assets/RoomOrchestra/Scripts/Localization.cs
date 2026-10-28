using System;
using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// The language the controls show. The scene is authored in English, so nothing changes at startup;
    /// <see cref="LanguageDirector"/> applies the player's language once the start delay has passed.
    /// The player's own choice (the language button) is remembered on this headset only (PlayerPrefs);
    /// until they make one, the headset's system language decides.
    /// </summary>
    public static class Localization
    {
        public const string PrefKey = "RoomOrchestra.UiLanguage";

        public static UiLanguage Current { get; private set; } = UiLanguage.English;

        public static event Action<UiLanguage> Changed;

        /// <summary>Shows <paramref name="language"/>; <paramref name="remember"/> stores it as the player's choice.</summary>
        public static void Set(UiLanguage language, bool remember)
        {
            if (remember)
            {
                PlayerPrefs.SetInt(PrefKey, (int)language);
                PlayerPrefs.Save();
            }
            if (Current == language) return;
            Current = language;
            Debug.Log($"[RoomOrchestra] Language: {language}{(remember ? " (player's choice)" : "")}");
            Changed?.Invoke(language);
        }

        /// <summary>The language button: switches and remembers.</summary>
        public static void Toggle() => Set(UiText.Other(Current), remember: true);

        /// <summary>The player's saved choice, else the headset's system language.</summary>
        public static UiLanguage Preferred()
        {
            int saved = PlayerPrefs.GetInt(PrefKey, -1);
            if (Enum.IsDefined(typeof(UiLanguage), saved)) return (UiLanguage)saved;
            return FromSystem(Application.systemLanguage);
        }

        public static UiLanguage FromSystem(SystemLanguage language) => language switch
        {
            SystemLanguage.Chinese or SystemLanguage.ChineseSimplified or SystemLanguage.ChineseTraditional => UiLanguage.Chinese,
            _ => UiLanguage.English,
        };

        // Fresh each play session, even with domain reload off (matches the authored scene).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetToDefaults() => Current = UiLanguage.English;
    }
}
