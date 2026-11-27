using System.Linq;
using NUnit.Framework;

namespace RoomOrchestra.Tests
{
    /// <summary>Story mode in Chinese (owner, 2026-11-26): every line a player reads has a translation.</summary>
    public class StoryTextTests
    {
        static void Translated(string english, string what)
        {
            Assert.IsNotNull(english, what);
            Assert.AreNotEqual(english, StoryText.In(english, UiLanguage.Chinese), $"{what} has Chinese: \"{english}\"");
            Assert.AreEqual(english, StoryText.In(english, UiLanguage.English), "English stays English");
        }

        [Test]
        public void EveryLevelsWordsAndEveryInstrumentsStoryAreInChinese()
        {
            foreach (var level in StoryLevel.All)
            {
                Translated(level.Name, $"Level {level.Number}'s name");
                foreach (var wave in level.Waves) if (wave.Text != null) Translated(wave.Text, $"a Level {level.Number} wave");
            }
            foreach (var lore in StoryLore.Levels)
            {
                Translated(lore.Kind, $"{lore.Name}'s kind");
                Translated(lore.People, $"{lore.Name}'s people");
                Translated(lore.Broken, $"{lore.Name}'s broken line");
                Translated(lore.Restored, $"{lore.Name}'s restored line");
                foreach (var fact in lore.Facts) Translated(fact, $"a {lore.Name} fact");
            }
        }

        [Test]
        public void TheLabelFontCanDrawEveryStoryCharacter()
        {
            var all = UiText.AllCharacters();
            foreach (char c in StoryText.AllChinese) Assert.IsTrue(all.IndexOf(c) >= 0, $"'{c}' is a label character");
            Assert.IsTrue(StoryText.English.Any(e => e.Contains("Schärer")), "names keep their letters");
        }
    }
}
