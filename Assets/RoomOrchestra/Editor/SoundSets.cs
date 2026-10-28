using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RoomOrchestra.EditorTools
{
    /// <summary>
    /// Switches the instruments between the owner's chosen set (Audio/Udu, /Mbira, /Erhu; synthesized by
    /// ArtSource/Audio/gen_world_instruments.py) and the alternative voices synthesized by
    /// ArtSource/Audio/synthesize_instruments.py (Audio/Synth/...), for an owner listening comparison. The game renders every voice to the same
    /// peak, so in-game loudness follows a sound's average-to-peak ratio: each instrument's volume is
    /// trimmed by the measured ratio so the mix balance the owner approved is kept.
    /// </summary>
    public static class SoundSets
    {
        public const string OriginalDir = "Assets/RoomOrchestra/Audio";
        public const string SynthDir = "Assets/RoomOrchestra/Audio/Synth";
        /// <summary>Instrument volume with the approved prototype set.</summary>
        public const float BaseVolume = 0.7f;

        public static void UseOriginal() => Use(OriginalDir);
        public static void UseSynth() => Use(SynthDir);

        public static void Use(string dir)
        {
            var scene = EditorSceneManager.OpenScene(LittleEchoesSceneSetup.ScenePath);
            foreach (var target in Object.FindObjectsByType<InstrumentTarget>(FindObjectsSortMode.None))
            {
                string name = target.Instrument.ToString();
                var clips = Clips(dir, name);
                var original = Clips(OriginalDir, name);
                float trim = dir == OriginalDir ? 1f : Mathf.Clamp(Loudness(original) / Loudness(clips), 0.2f, 1.5f);
                var so = new SerializedObject(target);
                var notes = so.FindProperty("notes");
                notes.arraySize = clips.Length;
                for (int i = 0; i < clips.Length; i++) notes.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
                so.FindProperty("volume").floatValue = BaseVolume * trim;
                so.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log($"[RoomOrchestra] {name}: {clips.Length} notes from {dir}, volume {BaseVolume * trim:F2} (trim {20f * Mathf.Log10(trim):+0.0;-0.0} dB)");
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>The five note clips for <paramref name="instrument"/> in <paramref name="dir"/>, low to high.</summary>
        public static AudioClip[] Clips(string dir, string instrument)
        {
            var paths = Enumerable.Range(0, NoteMapping.Count)
                .Select(i => AssetDatabase.FindAssets($"{instrument}_{i}_ t:AudioClip", new[] { $"{dir}/{instrument}" })
                    .Select(AssetDatabase.GUIDToAssetPath).First(p => System.IO.Path.GetDirectoryName(p).Replace('\\', '/') == $"{dir}/{instrument}"))
                .ToArray();
            foreach (var path in paths) MatchImport(path);
            return paths.Select(AssetDatabase.LoadAssetAtPath<AudioClip>).ToArray();
        }

        // Same import as the approved set: decompressed on load (voices are rendered from the sample data).
        static void MatchImport(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is AudioImporter importer)) return;
            var settings = importer.defaultSampleSettings;
            if (settings.loadType == AudioClipLoadType.DecompressOnLoad && settings.compressionFormat == AudioCompressionFormat.Vorbis) return;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 1f;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }

        /// <summary>Average RMS-to-peak ratio over the clips: how loud they sound once rendered to one peak level.</summary>
        public static float Loudness(AudioClip[] clips)
        {
            float sum = 0f;
            foreach (var clip in clips)
            {
                clip.LoadAudioData();
                var data = new float[clip.samples * clip.channels];
                clip.GetData(data, 0);
                float peak = data.Max(v => Mathf.Abs(v));
                float rms = Mathf.Sqrt(data.Sum(v => v * v) / data.Length);
                sum += peak > 0f ? rms / peak : 0f;
            }
            return sum / clips.Length;
        }
    }
}
