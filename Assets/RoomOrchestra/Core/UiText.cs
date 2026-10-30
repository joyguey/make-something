using System;
using System.Collections.Generic;
using System.Linq;

namespace RoomOrchestra
{
    /// <summary>The languages the controls can show.</summary>
    public enum UiLanguage
    {
        English,
        Chinese // Simplified
    }

    /// <summary>Every word a control shows.</summary>
    public enum UiTerm
    {
        Flutter,
        Bop,
        Boom,
        Loudness,
        Undo,
        Pause,
        Play,
        Clear,
        Recenter,
        Height,
        Seated,
        Standing,
        Udu,
        Mbira,
        Erhu,
        Muted,
        Open,
        MutedHit,
        Slap,
        Bend,
        Tap,
        /// <summary>The language button's label: names the language a tap switches to, in that language.</summary>
        SwitchLanguage,
        /// <summary>The key above a jianpu note column ("1=D"); Chinese only, English tiles name the notes.</summary>
        KeyOfD,
        /// <summary>The leaf that calls the ball tray and buttons to the player.</summary>
        CallTray,
        /// <summary>The title card's line under the name.</summary>
        TitleSubtitle,
        /// <summary>The title card's button that begins the game.</summary>
        Start,
        // The guided first song: what Flutter says while each note is wanted (Core/TutorialPlan).
        TutorialHello,
        TutorialMa,
        TutorialMary,
        TutorialMaryHad,
        TutorialLamp,
        TutorialLit,
        TutorialDrum,
        TutorialBeat2,
        TutorialBeat3,
        TutorialBeat4,
        TutorialDone,
        TutorialTryAgain,
        TutorialOnGo,
        TutorialEarly,
        TutorialLate,
        /// <summary>The count's last word, shown on beat 4.</summary>
        Go,
        /// <summary>The leaf that skips the guided song.</summary>
        Skip,
        /// <summary>The leaf button that plays the guided song again.</summary>
        Lesson,
        /// <summary>Under an instrument's name: give this furniture the next instrument.</summary>
        ChangeInstrument,
        /// <summary>The leaf's instrument pack button, and the packs.</summary>
        Sounds,
        PackClassic,
        PackEarthy
    }

    /// <summary>
    /// All control text in English and Simplified Chinese. The only text in the game is these few
    /// control labels, so a table is enough. Chinese wording awaits a native-speaker review
    /// (docs/DECISION_LOG.md). Every character used here must be in the label font's atlas
    /// (Fonts/LittleEchoesLabels; an EditMode test checks).
    /// </summary>
    public static class UiText
    {
        static readonly Dictionary<UiTerm, (string en, string zh)> s_Terms = new()
        {
            { UiTerm.Flutter, ("Flutter", "飘飘") },
            { UiTerm.Bop, ("Bop", "波波") },
            { UiTerm.Boom, ("Boom", "嘭嘭") },
            { UiTerm.Loudness, ("Loudness", "力度") },
            { UiTerm.Undo, ("Undo", "撤销") },
            { UiTerm.Pause, ("Pause", "暂停") },
            { UiTerm.Play, ("Play", "播放") },
            { UiTerm.Clear, ("Clear", "清除") },
            { UiTerm.Recenter, ("Recenter", "复位") },
            { UiTerm.Height, ("Height", "高度") },
            { UiTerm.Seated, ("Seated", "坐姿") },
            { UiTerm.Standing, ("Standing", "站姿") },
            { UiTerm.Udu, ("udu", "乌杜鼓") },
            { UiTerm.Mbira, ("mbira", "拇指琴") },
            { UiTerm.Erhu, ("erhu", "二胡") },
            { UiTerm.Muted, ("Muted", "静音") },
            { UiTerm.Open, ("Open", "开音") },
            { UiTerm.MutedHit, ("Muted", "闷音") },
            { UiTerm.Slap, ("Slap", "拍击") },
            { UiTerm.Bend, ("Bend", "滑音") },
            { UiTerm.Tap, ("Tap", "轻敲") },
            { UiTerm.SwitchLanguage, ("中文", "English") },
            { UiTerm.KeyOfD, ("", "1=D") },
            { UiTerm.CallTray, ("Come here", "过来") },
            { UiTerm.TitleSubtitle, ("A mixed-reality musical playground", "混合现实音乐游乐场") },
            { UiTerm.Start, ("Start", "开始") },
            { UiTerm.TutorialHello, ("Hi, I'm Flutter! Tap the glowing F# by the chair.", "嗨，我是飘飘！点椅子旁发光的 3。") },
            { UiTerm.TutorialMa, ("\"Ma-\" Now the E, just below.", "\"玛——\" 再点下面的 2。") },
            { UiTerm.TutorialMary, ("\"Ma-ry\" Now D, the lowest.", "\"玛丽——\" 再点最低的 1。") },
            { UiTerm.TutorialMaryHad, ("\"Ma-ry had\" Back up to E.", "\"玛丽有——\" 再回到 2。") },
            { UiTerm.TutorialLamp, ("\"Mary had a\u2026\" Meet the lamp! Tap its F#.", "\"玛丽有只……\" 来认识台灯！点它的 3。") },
            { UiTerm.TutorialLit, ("\"Lit-\" F# again.", "\"小——\" 再点 3。") },
            { UiTerm.TutorialDrum, ("Here comes the table! Tap a beat on its drum.", "桌子来啦！在它的鼓上打拍子。") },
            { UiTerm.TutorialBeat2, ("Keep the beat\u2026 2", "继续打拍子……2") },
            { UiTerm.TutorialBeat3, ("Keep going\u2026 3", "继续……3") },
            { UiTerm.TutorialBeat4, ("\u2026and 4!", "……还有 4！") },
            { UiTerm.TutorialDone, ("You did it! That's \"Mary Had a Little Lamb\". The lesson's over: now play anything you like!", "你做到了！这就是《玛丽有只小羊羔》。教学结束：现在随便玩吧！") },
            { UiTerm.TutorialTryAgain, ("Almost! Try the glowing one.", "差一点！试试发光的那个。") },
            { UiTerm.TutorialOnGo, ("Finish the tune! Throw a ball at the lamp, or tap its F#, right on Go!", "唱完这首歌！在\"开始\"时把球扔向台灯，或点它的 3！") },
            { UiTerm.TutorialEarly, ("A little early! Wait for Go.", "早了一点！等到\"开始\"。") },
            { UiTerm.TutorialLate, ("A little late! Right on Go.", "晚了一点！正好在\"开始\"时。") },
            { UiTerm.Go, ("Go!", "开始！") },
            { UiTerm.Skip, ("Skip", "跳过") },
            { UiTerm.Lesson, ("Lesson", "教学") },
            { UiTerm.ChangeInstrument, ("Change", "换乐器") },
            { UiTerm.Sounds, ("Sounds", "音色") },
            { UiTerm.PackClassic, ("Classic", "经典") },
            { UiTerm.PackEarthy, ("Earthy", "质朴") },
        };

        /// <summary>Terms shown only in Chinese (blank in English).</summary>
        public static bool ChineseOnly(UiTerm term) => term == UiTerm.KeyOfD;

        // Chinese shows the notes in numbered notation (jianpu) with 1 = D, as erhu music is usually written.
        static readonly string[] s_JianpuDegrees = { "1", "2", "3", "5", "6" };

        public static string Get(UiTerm term, UiLanguage language) =>
            language == UiLanguage.Chinese ? s_Terms[term].zh : s_Terms[term].en;

        public static UiLanguage Other(UiLanguage language) =>
            language == UiLanguage.Chinese ? UiLanguage.English : UiLanguage.Chinese;

        public static string Name(OrbType orb, UiLanguage language) => Get(orb switch
        {
            OrbType.Flutter => UiTerm.Flutter,
            OrbType.Bop => UiTerm.Bop,
            _ => UiTerm.Boom,
        }, language);

        public static string Name(InstrumentType instrument, UiLanguage language) => Get(instrument switch
        {
            InstrumentType.Udu => UiTerm.Udu,
            InstrumentType.Mbira => UiTerm.Mbira,
            _ => UiTerm.Erhu,
        }, language);

        public static string Posture(PlayPosture posture, UiLanguage language) =>
            Get(posture == PlayPosture.Standing ? UiTerm.Standing : UiTerm.Seated, language);

        public static string Volume(int level, UiLanguage language)
        {
            if (MixLevels.IsMuted(level)) return Get(UiTerm.Muted, language);
            int percent = (int)(MixLevels.Gain(level) * 100f + 0.5f);
            return language == UiLanguage.Chinese ? $"音量 {percent}%" : $"Vol {percent}%";
        }

        /// <summary>A pitch tile's label: the note (D E F# A B, or 1 2 3 5 6) or the udu sound.</summary>
        public static string Tile(MusicalNote note, UiLanguage language)
        {
            if (note.IsPercussion)
                return Get(note.Articulation switch
                {
                    UduArticulation.Open => UiTerm.Open,
                    UduArticulation.Muted => UiTerm.MutedHit,
                    UduArticulation.Slap => UiTerm.Slap,
                    UduArticulation.Bend => UiTerm.Bend,
                    _ => UiTerm.Tap,
                }, language);
            return language == UiLanguage.Chinese ? s_JianpuDegrees[note.Index] : NoteMapping.NoteName(note.Index);
        }

        /// <summary>Every character any label can show, for building the font atlas.</summary>
        public static string AllCharacters()
        {
            var text = new System.Text.StringBuilder();
            foreach (var (en, zh) in s_Terms.Values) text.Append(en).Append(zh);
            foreach (UiLanguage language in Enum.GetValues(typeof(UiLanguage)))
            {
                for (int i = 0; i < MixLevels.Count; i++) text.Append(Volume(i, language));
                for (int i = 0; i < NoteMapping.Count; i++) text.Append(Tile(new MusicalNote(InstrumentType.Mbira, i), language));
            }
            for (char c = ' '; c <= '~'; c++) text.Append(c); // all of printable ASCII
            return new string(text.ToString().Distinct().OrderBy(c => c).ToArray());
        }
    }
}
