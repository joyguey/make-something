using System.IO;
using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// Keeps the game's own log lines ([RoomOrchestra] and any error) in a file on the headset, so a whole session can be
    /// read afterwards (owner, 2026-11-08: "please look at the log for the mistakes", and the headset's shared log had
    /// already dropped the start of that session). The last three sessions are kept in the app's own files folder
    /// (adb pull /sdcard/Android/data/com.littleechoes.app/files/logs); nothing is sent anywhere.
    /// </summary>
    public static class SessionLog
    {
        public const int Keep = 3;
        static StreamWriter s_Writer;

        public static string Folder => Path.Combine(Application.persistentDataPath, "logs");
        public static string Current { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Begin()
        {
            if (Application.isEditor || s_Writer != null) return;
            try
            {
                Directory.CreateDirectory(Folder);
                for (int i = Keep - 1; i >= 1; i--)
                {
                    var older = Path.Combine(Folder, $"session-{i}.log");
                    var newer = i == 1 ? Path.Combine(Folder, "session.log") : Path.Combine(Folder, $"session-{i - 1}.log");
                    if (File.Exists(older)) File.Delete(older);
                    if (File.Exists(newer)) File.Move(newer, older);
                }
                Current = Path.Combine(Folder, "session.log");
                s_Writer = new StreamWriter(Current, false) { AutoFlush = true };
                s_Writer.WriteLine($"Little Echoes {Application.version} session {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                Application.logMessageReceivedThreaded += Write;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[RoomOrchestra] Session log unavailable: {e.Message}");
            }
        }

        static readonly object s_Lock = new();

        static void Write(string message, string stack, LogType type)
        {
            bool ours = message != null && message.StartsWith("[RoomOrchestra]");
            bool bad = type == LogType.Error || type == LogType.Exception || type == LogType.Assert;
            if (!ours && !bad) return;
            lock (s_Lock)
            {
                try
                {
                    s_Writer?.WriteLine($"{Time.realtimeSinceStartup,8:F2} {(bad ? type.ToString().ToUpperInvariant() + " " : "")}{message}");
                    if (bad && !string.IsNullOrEmpty(stack)) s_Writer?.WriteLine(stack);
                }
                catch { /* a full disk never stops the game */ }
            }
        }
    }
}
