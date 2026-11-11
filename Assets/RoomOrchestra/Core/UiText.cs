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
        PlaceUdu,
        PlaceMbira,
        PlaceErhu,
        PlaceDone,
        PlaceWelcomeBack,
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
        /// <summary>The leaf's Menu button (owner, 2026-11-08: fewer buttons on the leaf), and the tray it opens.</summary>
        Menu,
        MenuClose,
        /// <summary>Menu: choose again where the instruments live.</summary>
        Places,
        /// <summary>Menu: wipe everything saved (song, furniture, settings, lesson) and begin again, room scan included.</summary>
        StartOver,
        ConfirmStartOver,
        /// <summary>After the lesson: look around the whole room so the scan covers it before choosing furniture.</summary>
        ScanRoom,
        /// <summary>Menu: real things hide the game's things (PICO's depth), on or off.</summary>
        RealHidesOn,
        RealHidesOff,
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
        Quit,
        /// <summary>The V4 instrument library's names (appended: terms are stored by number).</summary>
        Ocarina,
        Handpan,
        Guzheng,
        Balafon,
        SlitDrum,
        /// <summary>The room-scan progress bar (owner, headset 2026-11-12).</summary>
        ScanProgress,
        ScanProgressHint,
        /// <summary>Furniture chosen with a virtual piece kept: how to change and move it (owner, headset 2026-11-12).</summary>
        PlaceDoneMovable
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
            { UiTerm.Ocarina, ("ocarina", "陶笛") },
            { UiTerm.Handpan, ("handpan", "手碟") },
            { UiTerm.Guzheng, ("guzheng", "古筝") },
            { UiTerm.Balafon, ("balafon", "巴拉风木琴") },
            { UiTerm.SlitDrum, ("slit drum", "木鼓") },
            { UiTerm.ScanProgress, ("Scanning your room: {0}%", "正在扫描房间：{0}%") },
            { UiTerm.PlaceDoneMovable, ("All set! Tap the picture above any furniture to change its instrument. To move a virtual one, point at it and hold the grip button (or look at it and make a fist), then drag it.", "好啦！点家具上面的小图可以换乐器。要移动虚拟家具，指着它按住握把键（或看着它握拳），然后拖动。") },
            { UiTerm.ScanProgressHint, ("Look all around: walls, floor, furniture", "看一圈：墙、地板、家具") },
            { UiTerm.Muted, ("Muted", "静音") },
            { UiTerm.Open, ("Deep", "开音") },
            { UiTerm.MutedHit, ("Soft", "闷音") },
            { UiTerm.Slap, ("Slap", "拍击") },
            { UiTerm.Bend, ("Wobble", "滑音") },
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
            { UiTerm.ScanLook, ("Look slowly around your room. Furniture I find gets a glowing spot!", "慢慢看看你的房间。我找到的家具上会出现发光的圆点！") },
            { UiTerm.ScanFound, ("Tap a glowing spot to choose which instrument lives there. Then throw us at it!", "点一下发光的圆点，选一个乐器放在那里。然后把我们扔过去！") },
            { UiTerm.ScanNone, ("I couldn't find your furniture, so the instruments stay right here.", "没找到你的家具，乐器就留在这里。") },
            { UiTerm.PlaceUdu, ("Last, the udu drum: point at a real table and press. Or press the table to keep it here.", "最后是乌杜鼓：指向一张真的桌子，按一下。或者按桌子，让它留在这里。") },
            { UiTerm.PlaceMbira, ("The glow is your room. Where should the mbira live? Point at a real chair or sofa and press. Or press the chair to keep it here.", "发光的就是你的房间。拇指琴要住在哪里？指向一把真的椅子或沙发，按一下。或者按椅子，让它留在这里。") },
            { UiTerm.PlaceErhu, ("Now the erhu: point at a real lamp or light and press. Or press the lamp to keep it here.", "现在是二胡：指向一盏真的灯，按一下。或者按台灯，让它留在这里。") },
            { UiTerm.PlaceDone, ("All set! Point at your furniture to see its five notes, then press to play, or throw us at it! Tap the picture above it to change the instrument.", "好啦！指向你的家具，看看它的五个音，然后按一下来弹，或者把我们扔过去！点它上面的小图可以换乐器。") },
            { UiTerm.PlaceWelcomeBack, ("Welcome back! Your instruments are where you left them.", "欢迎回来！乐器都还在你上次放的地方。") },
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
            { UiTerm.Menu, ("Menu", "菜单") },
            { UiTerm.MenuClose, ("Close", "关闭") },
            { UiTerm.Places, ("Choose\nfurniture", "选\n家具") },
            { UiTerm.StartOver, ("Start\nover", "从头\n开始") },
            { UiTerm.ConfirmStartOver, ("Start completely over? Your song, chosen furniture and settings are wiped, and you scan your room again.", "完全从头开始吗？你的歌、选好的家具和设置都会清除，然后重新扫描房间。") },
            { UiTerm.ScanRoom, ("Let's scan your room! Look slowly all around: the walls, the floor and every piece of furniture. Watch the glow fill in.", "我们来扫描房间吧！慢慢看一圈：墙、地板和每件家具。看着光慢慢铺满。") },
            { UiTerm.RealHidesOn, ("Hiding\nOn", "遮挡\n开") },
            { UiTerm.RealHidesOff, ("Hiding\nOff", "遮挡\n关") },
            { UiTerm.ChangeInstrument, ("Change", "换乐器") },
            { UiTerm.Sounds, ("Sounds", "音色") },
            { UiTerm.PackClassic, ("Classic", "经典") },
            { UiTerm.PackEarthy, ("Earthy", "质朴") },
            { UiTerm.View, ("Text size", "文字大小") },
            { UiTerm.ViewStandard, ("Standard", "标准") },
            { UiTerm.ViewLarge, ("Large", "大") },
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
            InstrumentType.Ocarina => UiTerm.Ocarina,
            InstrumentType.Handpan => UiTerm.Handpan,
            InstrumentType.Guzheng => UiTerm.Guzheng,
            InstrumentType.Balafon => UiTerm.Balafon,
            InstrumentType.SlitDrum => UiTerm.SlitDrum,
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
