using System.Collections.Generic;
using System.Linq;

namespace RoomOrchestra
{
    /// <summary>
    /// Story mode's words in Chinese (owner, 2026-11-26: Chinese text for story mode before the Dec 3 freeze). Keyed by
    /// the English as written in the story code and lore; a line not listed here shows in English. The friends,
    /// instruments and other names follow the game's own Chinese (UiText: 飘飘 波波 嘭嘭, 乌杜鼓 二胡 手碟). The Discords
    /// are 吱吱怪 (Squeak), 晃晃怪 (Wobbler), 哐哐怪 (Clang), the Grumble 嘟囔怪. Written by Claude; a native speaker
    /// should review it before release. Its characters are in the label font (ArtSource/Fonts/subset_label_font.py
    /// reads this file; UiText.AllCharacters includes <see cref="AllChinese"/>).
    /// </summary>
    public static class StoryText
    {
        static readonly Dictionary<string, string> s_Chinese = new()
        {
            // the sign, titles, the HUD
            { "Level {0}: {1}", "第{0}关：{1}" },
            { "The Seed", "种子" },
            { "The Two Strings", "两根弦" },
            { "The World Tree", "世界树" },
            { "Wave {0}", "第{0}波" },
            { "Get ready", "准备" },
            { "The Grumble", "嘟囔怪" },
            { "Complete!", "完成！" },
            { "Wave {0} of {1}", "第{0}波，共{1}波" },
            { "Wave {0} of {1}, then the Grumble", "第{0}波，共{1}波，然后是嘟囔怪" },
            { "Harmony", "和声" },
            { "Discord", "噪音" },
            // buttons
            { "Pause", "暂停" },
            { "Resume", "继续" },
            { "Next level", "下一关" },
            { "Play again", "再玩一次" },
            { "Try again", "再试一次" },
            { "Easy aim: on", "轻松瞄准：开" },
            { "Easy aim: off", "轻松瞄准：关" },
            { "Leave", "离开" },
            { "Sure? Leave", "确定？离开" },
            { "Begin", "开始" },
            { "Collect", "收下" },
            { "Close", "关闭" },
            { "< Level {0}", "< 第{0}关" },
            { "Level {0} >", "第{0}关 >" },
            // banners and captions
            { "Easy aim on: slower Discords, stronger aim help", "轻松瞄准已开：噪音怪更慢，瞄准帮助更强" },
            { "Easy aim off", "轻松瞄准已关" },
            { "Your room is nearly full of Discord! Beat the ones in your room!", "你的房间快被噪音填满了！快打败房间里的噪音怪！" },
            { "Discord is filling your room! Beat the ones that got in.", "噪音正在填满你的房间！打败溜进来的噪音怪。" },
            { "Too many Discords got into your room, and the meadow went grey. Try again?", "太多噪音怪溜进了你的房间，草地也变灰了。再试一次？" },
            { "Too many Discords got into your room, and the meadow went grey. Try again? Easy aim slows the Discords down and helps your aim.", "太多噪音怪溜进了你的房间，草地也变灰了。再试一次？“轻松瞄准”能让噪音怪慢下来，还会帮你瞄准。" },
            { "Press Leave again to go back to the sandbox", "再按一次“离开”，回到自由玩" },
            { "Paused. Nothing moves until you press Resume.", "已暂停。按“继续”之前，一切都不会动。" },
            { "Flick first, then Hold again", "先弹一次，才能再换" },
            { "Wave cleared!", "这一波清除了！" },
            { "Harmony full! Your next flick is a CHORUS!", "和声满了！下一次弹就是大合唱！" },
            { "Harmony full! Your next shot is a CHORUS!", "和声满了！下一次发射就是大合唱！" },
            { "CHORUS!", "大合唱！" },
            { "x{0} streak", "{0}连击" },
            { "streak lost", "连击中断" },
            { "{0} in a row!", "连续{0}次！" },
            { "On the beat!", "踩在节拍上！" },
            { "A Discord got into your room!", "一个噪音怪跑进你的房间了！" },
            { "Crack!", "裂开了！" },
            { "Shell {0} of {1}: needs {2}  {3}", "第{0}层壳，共{1}层：需要{2}  {3}" },
            { "Shell broken! Now it needs {0}.", "壳碎了！现在需要{0}。" },
            { "{0} bounces off! It needs {1}.", "{0}被弹开了！它需要{1}。" },
            { "Sour note! Boom's shockwave only cracks Clangs.", "走音了！嘭嘭的冲击波只能打碎哐哐怪。" },
            { "Sour note! {0} beats that one.", "走音了！那个要用{0}。" },
            { "Level {0} complete!", "第{0}关完成！" },
            { "Oh no!", "哎呀！" },
            { "{0} The Grumble and {1} Discords turned back into music.", "{0}嘟囔怪和{1}个噪音怪都变回了音乐。" },
            { "Level {0} is ready: press Next level when you've collected the card.", "第{0}关已经开启：收下卡片后，按“下一关”。" },
            { "You restored every instrument: the journey is complete! Thank you for playing.", "你修复了所有乐器：旅程完成了！谢谢你的游玩。" },
            { "The friends walk on toward the World Tree... ", "朋友们继续走向世界树……" },
            { "Restored! Collect its card to read its story.", "修复好了！收下它的卡片，读读它的故事。" },
            { "Card collected! Find it on Story's first card.", "卡片已收下！在故事模式的第一张卡上可以找到它。" },
            { "{0} card", "{0}卡片" },
            // the cards
            { "{0} from {1}.", "来自{1}的{0}。" },
            { "{0} Help Flutter, Bop and Boom mend it!", "{0}帮飘飘、波波和嘭嘭修好它吧！" },
            { "HOW TO PLAY\nHold your left hand open: a friend sits on your palm. Curl your right index finger against your thumb beside it, then flick it at a Discord. Each Discord shows the friend that beats it. Flip your left hand over and back to swap friends.",
              "怎么玩\n张开左手：一个朋友会坐在你的手心上。把右手食指抵在大拇指上，放在朋友旁边，然后弹向噪音怪。每个噪音怪都会显示能打败它的朋友。把左手翻过去再翻回来，就能换朋友。" },
            { "HOW TO PLAY\nA friend sits by your left controller. Point your right controller at a Discord and pull the trigger. Each Discord shows the friend that beats it. Pull the left trigger to swap friends.",
              "怎么玩\n一个朋友坐在你的左手柄旁边。用右手柄对准噪音怪，扣下扳机。每个噪音怪都会显示能打败它的朋友。扣一下左手扳机就能换朋友。" },
            // Level 1's cues
            { "Squeaks! Tiny, spiky and fast. Flutter (the tall one) flies straight through a whole line of them. Every Discord you beat becomes a note that brings colour back to the meadow.",
              "吱吱怪！又小又尖又快。飘飘（高高的那个）能一口气穿过一整排。你打败的每个噪音怪都会变成一个音符，把颜色带回草地。" },
            { "Wobblers! They sway about. Bop (the round one) bounces off your walls to reach them. Not the friend you need? Flip your left hand over and back to swap it with your held friend.",
              "晃晃怪！它们晃来晃去。波波（圆圆的那个）会从你的墙上弹过去找到它们。手上不是你要的朋友？把左手翻过去再翻回来，就能和留着的朋友交换。" },
            { "Wobblers! They sway about. Bop (the round one) bounces off your walls to reach them. Not the friend you need? Pull the left trigger to swap it with your held friend.",
              "晃晃怪！它们晃来晃去。波波（圆圆的那个）会从你的墙上弹过去找到它们。手上不是你要的朋友？扣一下左手扳机，就能和留着的朋友交换。" },
            { "Clangs! They wear armour. Only Boom (the wide one) can crack it with a big shockwave.", "哐哐怪！它们穿着盔甲。只有嘭嘭（宽宽的那个）的大冲击波能把盔甲打碎。" },
            { "Here they all come! Each Discord carries a little picture of the friend that beats it.", "它们全都来了！每个噪音怪身上都带着一个小图案，告诉你哪个朋友能打败它。" },
            { "The World Tree is close... but the Grumble blocks the way! Its shell shows which friend can crack it: spikes for Flutter, jelly for Bop, armour for Boom. Crack all three shells!",
              "世界树就在眼前……可是嘟囔怪挡住了路！它的壳告诉你哪个朋友能打碎它：尖刺用飘飘，果冻用波波，盔甲用嘭嘭。把三层壳都打碎！" },
            { "The Grumble's mountain hideout! It has grown a fourth shell, and each takes five hits. Watch which friend each shell needs!",
              "嘟囔怪的山中老巢！它长出了第四层壳，每层都要打五下。看清每层壳需要哪个朋友！" },
            { "The Grumble's last stand at the World Tree: five shells, five hits each. Everything you've learned, all at once!",
              "嘟囔怪在世界树下做最后的抵抗：五层壳，每层五下。把你学到的全部用上！" },
            // Level 2's waves
            { "Squeaks and Wobblers together! Watch the little friend each one carries.", "吱吱怪和晃晃怪一起来了！看看每个身上带着的小朋友。" },
            { "Wobblers and Clangs! Bop bounces, Boom cracks. Missed flicks cost Harmony now, so aim true.", "晃晃怪和哐哐怪！波波会反弹，嘭嘭能打碎。现在弹空会扣和声，瞄准一点。" },
            { "All three at once, and quicker! Use Hold to keep the friend you need ready.", "三种一起来，而且更快！翻一翻左手，把需要的朋友准备好。" },
            { "The last stretch before the mountains: a whole crowd of Discords!", "进山前的最后一段路：一大群噪音怪！" },
            // Level 3's waves
            { "Night falls at the foot of the World Tree. All three kinds at once, from the start!", "夜幕降临在世界树下。三种噪音怪一开始就一起来！" },
            { "More of them, and quicker. Keep your streak for a Chorus!", "更多，更快。保持连击，攒出大合唱！" },
            { "They are coming faster now. Hold the friend you need and flick on the beat!", "它们来得更快了。留好需要的朋友，踩着节拍弹！" },
            { "The last climb to the World Tree: everything the Grumble has left!", "通往世界树的最后一段：嘟囔怪的全部手下！" },
            // the Udu
            { "A clay pot drum", "陶罐鼓" },
            { "the Igbo people of southeastern Nigeria", "尼日利亚东南部的伊博人" },
            { "The Grumble has cracked the Udu, the clay pot drum, and stolen its deep voice. Without it, the meadow has lost its beat.",
              "嘟囔怪把乌杜鼓这只陶罐鼓弄裂了，还偷走了它低沉的声音。没有它，草地失去了节拍。" },
            { "The Udu's deep voice is back, and the meadow has its beat again!", "乌杜鼓低沉的声音回来了，草地又有了节拍！" },
            { "Udu means \"vessel\", a pot, in the Igbo language. It is one of the most important instruments in Igbo music.",
              "在伊博语里，“乌杜”的意思是“容器”，也就是罐子。它是伊博音乐中最重要的乐器之一。" },
            { "Igbo women potters make the udu by hand from clay. It is a prized skill, often learned from a mother, an aunt or another close relative.",
              "乌杜鼓由伊博族的女陶艺师用黏土手工制作。这是一门珍贵的手艺，常常是跟妈妈、阿姨或其他亲人学来的。" },
            { "The udu's secret is its second hole. Strike it and the air inside answers with that deep, watery bass note. Move your hand over the hole and the sound changes.",
              "乌杜鼓的秘密在于它的第二个孔。拍一下，里面的空气就会回应出低沉、像水一样的低音。用手在孔上移动，声音就会改变。" },
            { "From the clay pot to stages all over the world, drummers everywhere love the udu's voice.", "从陶罐到世界各地的舞台，各地的鼓手都喜爱乌杜鼓的声音。" },
            // the Erhu
            { "A two-stringed fiddle", "两根弦的拉弦乐器" },
            { "China", "中国" },
            { "The Grumble has fled toward the mountains with the Erhu, and snapped its two strings. Without them, the meadow has lost its song.",
              "嘟囔怪带着二胡逃向了山里，还弄断了它的两根弦。没有它们，草地失去了歌声。" },
            { "The Erhu sings again, and the meadow has its melody back!", "二胡又唱起来了，草地找回了旋律！" },
            { "Er means \"two\", for its two strings. Hu points to the Hu peoples of China's north and west: the erhu belongs to the huqin family of fiddles.",
              "“二”指它的两根弦。“胡”指中国北方和西方的胡人：二胡属于胡琴家族。" },
            { "Its bow is part of the instrument: the horsehair runs between the two strings, so the bow can't come off unless a string is taken off or breaks. Both sides of the hair are played.",
              "二胡的弓是乐器的一部分：马尾穿在两根弦之间，除非取下或断掉一根弦，弓是拿不下来的。马尾的两面都能用来拉。" },
            { "Its little sound box is traditionally covered with python skin, which gives the erhu its warm, singing voice.",
              "它小小的琴筒传统上蒙着蟒皮，让二胡有温暖、像歌唱一样的声音。" },
            { "Chinese writing describes a huqin played with a horsehair bow more than 900 years ago, in the 11th century. In the 20th century, Liu Tianhua's work had a big influence on music for the erhu.",
              "早在九百多年前的11世纪，中国的文字就记载了用马尾弓拉的胡琴。到了20世纪，刘天华的创作对二胡音乐影响深远。" },
            // the Handpan
            { "A tuned steel instrument, played with the hands,", "用手演奏的调音钢制乐器" },
            { "Switzerland", "瑞士" },
            { "At the foot of the World Tree, the Grumble has dented the Handpan, and its shining notes have gone dull and flat. Without its chords, the World Tree cannot sing.",
              "在世界树下，嘟囔怪把手碟砸出了凹痕，它闪亮的音变得又闷又走调。没有它的和弦，世界树就唱不出歌来。" },
            { "The Handpan shines again! With the Udu's beat, the Erhu's song and the Handpan's chords, the World Tree sings with every voice.",
              "手碟又闪亮了！有了乌杜鼓的节拍、二胡的歌声和手碟的和弦，世界树用所有的声音歌唱。" },
            { "The story of the handpan begins in 2000 in Bern, Switzerland, where steelpan makers Felix Rohner and Sabina Schärer created the first one. They named it the Hang.",
              "手碟的故事始于2000年的瑞士伯尔尼：钢鼓制作者Felix Rohner和Sabina Schärer做出了第一个。他们给它取名叫Hang。" },
            { "Hang means \"hand\" in Bernese German, because it is played with the hands. Its makers ask that it be called the Hang, not a \"hang drum\".",
              "在伯尔尼德语里，Hang的意思是“手”，因为它是用手演奏的。它的制作者希望大家叫它Hang，而不是“Hang鼓”。" },
            { "It grew from the steelpan of Trinidad and Tobago: Felix Rohner had played the steel drum, and their workshop made steelpans before the Hang.",
              "它源自特立尼达和多巴哥的钢鼓：Felix Rohner以前演奏过钢鼓，他们的工作室在做Hang之前也制作钢鼓。" },
            { "It began as two half-shells of steel joined into a globe. Today many makers around the world build instruments inspired by it, called handpans.",
              "它最初是两个钢制半球合成的一个球体。如今，世界各地有很多制作者受它启发做出类似的乐器，称为手碟。" },
        };

        /// <summary><paramref name="english"/> in <paramref name="language"/> (English when no translation is listed).</summary>
        public static string In(string english, UiLanguage language) =>
            language == UiLanguage.Chinese && english != null && s_Chinese.TryGetValue(english, out var zh) && !string.IsNullOrEmpty(zh) ? zh : english;

        /// <summary>Every listed English line (tests check story code and lore against it).</summary>
        public static IEnumerable<string> English => s_Chinese.Keys;

        /// <summary>Every character of the Chinese (for the label font).</summary>
        public static string AllChinese => string.Concat(s_Chinese.Values);
    }
}
