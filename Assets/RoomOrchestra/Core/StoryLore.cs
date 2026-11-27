using System.Collections.Generic;

namespace RoomOrchestra
{
    /// <summary>
    /// The real instrument behind each story level (owner, 2026-11-25: "some of the story's lore should involve the history
    /// of each traditional instrument ... each level is trying to fix one aspect of the instrument"; owner's choices: real
    /// facts, gently told; finishing a level is a bonus, never a lock). The Grumble cracking an instrument is the game's
    /// fantasy; every fact here is real and kept to what sources agree on. Udu facts checked 2026-11-25 against
    /// Wikipedia "Udu", the Spencer Museum of Art (University of Kansas) record of an Igbo udu, and okwuid.com "Igbo
    /// Pottery: The Udu Drum"; disputed points (how it began, how old it is) are left out. Erhu facts checked 2026-11-26
    /// against Wikipedia "Erhu", Britannica "huqin", the Metropolitan Museum of Art's erhu record and China Daily
    /// "Five things to know about the erhu"; unconfirmed claims left out. Handpan facts checked 2026-11-26 against
    /// Wikipedia "Hang (instrument)" and "Handpan", PANArt's own history page and swissinfo.ch; its makers' wish that it
    /// be called the Hang (not a "hang drum") is told as they ask; the game's instrument is a handpan.
    /// </summary>
    /// <summary>What the level's instrument carries in the music, so its mending is heard there.</summary>
    public enum MusicRole { Beat, Chords, Tune }

    public sealed class InstrumentLore
    {
        public InstrumentLore(InstrumentType instrument, string name, string kind, string people, string broken, string restored, MusicRole role, params string[] facts)
        {
            Instrument = instrument; Name = name; Kind = kind; People = people; Broken = broken; Restored = restored; Role = role; Facts = facts;
        }

        /// <summary>What it is, for the card: "A clay pot drum", "A two-stringed fiddle".</summary>
        public string Kind { get; }
        /// <summary>What it carries in the music: the beat (the udu), the chords (the handpan), the tune (the erhu).</summary>
        public MusicRole Role { get; }
        public bool Melody => Role == MusicRole.Tune;

        public InstrumentType Instrument { get; }
        public string Name { get; }
        /// <summary>Who it comes from.</summary>
        public string People { get; }
        /// <summary>What the Grumble did to it (the game's fantasy).</summary>
        public string Broken { get; }
        /// <summary>Said when it is whole again.</summary>
        public string Restored { get; }
        /// <summary>Real facts, one told between each wave.</summary>
        public IReadOnlyList<string> Facts { get; }
    }

    public static class StoryLore
    {
        public static readonly InstrumentLore Udu = new(InstrumentType.Udu, "Udu", "A clay pot drum", "the Igbo people of southeastern Nigeria",
            "The Grumble has cracked the Udu, the clay pot drum, and stolen its deep voice. Without it, the meadow has lost its beat.",
            "The Udu's deep voice is back, and the meadow has its beat again!", MusicRole.Beat,
            "Udu means \"vessel\", a pot, in the Igbo language. It is one of the most important instruments in Igbo music.",
            "Igbo women potters make the udu by hand from clay. It is a prized skill, often learned from a mother, an aunt or another close relative.",
            "The udu's secret is its second hole. Strike it and the air inside answers with that deep, watery bass note. Move your hand over the hole and the sound changes.",
            "From the clay pot to stages all over the world, drummers everywhere love the udu's voice.");

        public static readonly InstrumentLore Erhu = new(InstrumentType.Erhu, "Erhu", "A two-stringed fiddle", "China",
            "The Grumble has fled toward the mountains with the Erhu, and snapped its two strings. Without them, the meadow has lost its song.",
            "The Erhu sings again, and the meadow has its melody back!", MusicRole.Tune,
            "Er means \"two\", for its two strings. Hu points to the Hu peoples of China's north and west: the erhu belongs to the huqin family of fiddles.",
            "Its bow is part of the instrument: the horsehair runs between the two strings, so the bow can't come off unless a string is taken off or breaks. Both sides of the hair are played.",
            "Its little sound box is traditionally covered with python skin, which gives the erhu its warm, singing voice.",
            "Chinese writing describes a huqin played with a horsehair bow more than 900 years ago, in the 11th century. In the 20th century, Liu Tianhua's work had a big influence on music for the erhu.");

        public static readonly InstrumentLore Handpan = new(InstrumentType.Handpan, "Handpan", "A tuned steel instrument, played with the hands,", "Switzerland",
            "At the foot of the World Tree, the Grumble has dented the Handpan, and its shining notes have gone dull and flat. Without its chords, the World Tree cannot sing.",
            "The Handpan shines again! With the Udu's beat, the Erhu's song and the Handpan's chords, the World Tree sings with every voice.", MusicRole.Chords,
            "The story of the handpan begins in 2000 in Bern, Switzerland, where steelpan makers Felix Rohner and Sabina Schärer created the first one. They named it the Hang.",
            "Hang means \"hand\" in Bernese German, because it is played with the hands. Its makers ask that it be called the Hang, not a \"hang drum\".",
            "It grew from the steelpan of Trinidad and Tobago: Felix Rohner had played the steel drum, and their workshop made steelpans before the Hang.",
            "It began as two half-shells of steel joined into a globe. Today many makers around the world build instruments inspired by it, called handpans.");

        /// <summary>The instrument each level restores, in order (owner, 2026-11-26: three levels, the udu, the erhu, the
        /// handpan).</summary>
        public static readonly InstrumentLore[] Levels = { Udu, Erhu, Handpan };

        public static string RestoredKey(InstrumentType instrument) => $"LittleEchoes.Story.Restored.{instrument}";
    }
}
