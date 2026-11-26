using System.Collections.Generic;

namespace RoomOrchestra
{
    /// <summary>
    /// The real instrument behind each story level (owner, 2026-11-25: "some of the story's lore should involve the history
    /// of each traditional instrument ... each level is trying to fix one aspect of the instrument"; owner's choices: real
    /// facts, gently told; finishing a level is a bonus, never a lock). The Grumble cracking an instrument is the game's
    /// fantasy; every fact here is real and kept to what sources agree on. Udu facts checked 2026-11-25 against
    /// Wikipedia "Udu", the Spencer Museum of Art (University of Kansas) record of an Igbo udu, and okwuid.com "Igbo
    /// Pottery: The Udu Drum"; disputed points (how it began, how old it is) are left out.
    /// </summary>
    public sealed class InstrumentLore
    {
        public InstrumentLore(InstrumentType instrument, string name, string people, string broken, string restored, params string[] facts)
        {
            Instrument = instrument; Name = name; People = people; Broken = broken; Restored = restored; Facts = facts;
        }

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
        public static readonly InstrumentLore Udu = new(InstrumentType.Udu, "Udu", "the Igbo people of southeastern Nigeria",
            "The Grumble has cracked the Udu, the clay pot drum, and stolen its deep voice. Without it, the meadow has lost its beat.",
            "The Udu's deep voice is back, and the meadow has its beat again!",
            "Udu means \"vessel\", a pot, in the Igbo language. It is one of the most important instruments in Igbo music.",
            "Igbo women potters make the udu by hand from clay. It is a prized skill, often learned from a mother, an aunt or another close relative.",
            "The udu's secret is its second hole. Strike it and the air inside answers with that deep, watery bass note. Move your hand over the hole and the sound changes.",
            "From the clay pot to stages all over the world, drummers everywhere love the udu's voice.");

        /// <summary>The instrument each level restores (Level 1 is index 0).</summary>
        public static readonly InstrumentLore[] Levels = { Udu };

        public static string RestoredKey(InstrumentType instrument) => $"LittleEchoes.Story.Restored.{instrument}";
    }
}
