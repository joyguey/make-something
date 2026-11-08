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
        TutorialWarmup,
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
        ScanLook,
        ScanFound,
        ScanNone,
        ConfirmQuit,
        ConfirmClear,
        ConfirmLesson,
        ConfirmLeave,
        ConfirmRestart,
        ConfirmCancel,
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
        PackEarthy,
        /// <summary>The leaf's View button (A11Y-08) and its settings.</summary>
        View,
        ViewStandard,
        ViewLarge,
        ViewContrast,
        ViewLargeContrast,
        // The Echo Battle (easter egg): the card's words. {0}/{1} are filled in (round, scores, points).
        BattleTitle,
        BattleFound,
        BattleHost,
        BattleJoin,
        BattleLeave,
        BattleWaiting,
        BattleLooking,
        BattleReady,
        BattleYourCall,
        BattleTheyEcho,
        BattleTheyCall,
        BattleListen,
        BattleEcho,
        BattleGotPoints,
        BattleRound,
        BattleScore,
        BattleWin,
        BattleLose,
        BattleDraw,
        BattleGone,
        BattleFinishLesson,
        ConfirmAgain,
        Quit
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
            { UiTerm.TutorialWarmup, ("Hi, I'm Flutter! Pick me up from the cloud (follow the arrow) and toss me at the chair. (Or tap a glowing tile.)", "嗨，我是飘飘！从云上拿起我（看箭头），扔向椅子。（也可以点发光的方块。）") },
            { UiTerm.TutorialHello, ("Yes! Every hit plays a note. Now a song: tap the glowing F#.", "对啦！每次击中都会奏出一个音。现在来唱首歌：点发光的 3。") },
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
            { UiTerm.ScanLook, ("Now look slowly around your room. Let's find your real table, chair and lamp!", "现在慢慢看看你的房间。我们来找找你真的桌子、椅子和台灯！") },
            { UiTerm.ScanFound, ("Found them! The instruments moved onto your furniture. Throw us at it!", "找到啦！乐器搬到你的家具上了。把我们扔过去吧！") },
            { UiTerm.ScanNone, ("I couldn't find your furniture, so the instruments stay right here.", "没找到你的家具，乐器就留在这里。") },
            { UiTerm.ConfirmQuit, ("Quit Little Echoes?", "退出游戏吗？") },
            { UiTerm.ConfirmClear, ("Clear your whole song?", "清除整首歌吗？") },
            { UiTerm.ConfirmLesson, ("Start the lesson again? Your song will be cleared.", "重新开始教学吗？你的歌会被清除。") },
            { UiTerm.ConfirmLeave, ("Leave the battle?", "离开对决吗？") },
            { UiTerm.ConfirmRestart, ("Restart", "重新开始") },
            { UiTerm.ConfirmCancel, ("Cancel", "取消") },
            { UiTerm.TutorialTryAgain, ("Almost! Try the glowing one.", "差一点！试试发光的那个。") },
            { UiTerm.TutorialOnGo, ("Last note! Pick me up and throw me at the lamp right on GO! (Or tap its F# on GO.)", "最后一个音！在\"开始\"时拿起我，扔向台灯！（也可以在\"开始\"时点它的 3。）") },
            { UiTerm.TutorialEarly, ("A little early! Wait for Go.", "早了一点！等到\"开始\"。") },
            { UiTerm.TutorialLate, ("A little late! Right on Go.", "晚了一点！正好在\"开始\"时。") },
            { UiTerm.Go, ("Go!", "开始！") },
            { UiTerm.Skip, ("Skip", "跳过") },
            { UiTerm.Lesson, ("Lesson", "教学") },
            { UiTerm.ChangeInstrument, ("Change", "换乐器") },
            { UiTerm.Sounds, ("Sounds", "音色") },
            { UiTerm.PackClassic, ("Classic", "经典") },
            { UiTerm.PackEarthy, ("Earthy", "质朴") },
            { UiTerm.View, ("View", "显示") },
            { UiTerm.ViewStandard, ("Standard", "标准") },
            { UiTerm.ViewLarge, ("Large text", "大字") },
            { UiTerm.ViewContrast, ("High contrast", "高对比") },
            { UiTerm.ViewLargeContrast, ("Large + contrast", "大字+高对比") },
            { UiTerm.BattleTitle, ("Echo Battle", "回声对决") },
            { UiTerm.BattleFound, ("Unlocked. Two headsets, same Wi-Fi: one hits HOST, the other JOIN.", "解锁了。两台头显连同一个Wi-Fi：一台点\"主持\"，另一台点\"加入\"。") },
            { UiTerm.BattleHost, ("HOST", "主持") },
            { UiTerm.BattleJoin, ("JOIN", "加入") },
            { UiTerm.BattleLeave, ("LEAVE", "离开") },
            { UiTerm.BattleWaiting, ("Waiting for a challenger...", "等待挑战者……") },
            { UiTerm.BattleLooking, ("Looking for a host on this Wi-Fi...", "正在这个Wi-Fi上寻找主持……") },
            { UiTerm.BattleReady, ("Get ready...", "准备……") },
            { UiTerm.BattleYourCall, ("GO! Drop your beat: up to 4 notes", "开始！出你的招：最多4个音") },
            { UiTerm.BattleTheyEcho, ("They're trying to echo you...", "对方正在模仿你……") },
            { UiTerm.BattleTheyCall, ("Their call. Listen up...", "轮到对方出招。仔细听……") },
            { UiTerm.BattleListen, ("Listen...", "听好……") },
            { UiTerm.BattleEcho, ("GO! Echo it back", "开始！照着来一遍") },
            { UiTerm.BattleGotPoints, ("+{0} points", "+{0} 分") },
            { UiTerm.BattleRound, ("ROUND {0}/3", "第 {0}/3 轮") },
            { UiTerm.BattleScore, ("YOU {0}  vs  {1} THEM", "你 {0}  比  {1} 对方") },
            { UiTerm.BattleWin, ("YOU WIN. Run it back? Hit HOST or JOIN.", "你赢了。再来一局？点\"主持\"或\"加入\"。") },
            { UiTerm.BattleLose, ("THEY WIN. Rematch? Hit HOST or JOIN.", "对方赢了。再战？点\"主持\"或\"加入\"。") },
            { UiTerm.BattleDraw, ("DRAW. Settle it: HOST or JOIN.", "平局。再决胜负：点\"主持\"或\"加入\"。") },
            { UiTerm.BattleGone, ("Lost the other headset.", "和另一台头显断开了。") },
            { UiTerm.BattleFinishLesson, ("Finish or skip the lesson first, then try again.", "先完成或跳过教学，再试一次。") },
            // A button that ends or wipes something asks first (George, 2026-11-03): its label reads this until tapped again.
            { UiTerm.ConfirmAgain, ("Sure?", "确定？") },
            { UiTerm.Quit, ("Quit", "退出") },
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
