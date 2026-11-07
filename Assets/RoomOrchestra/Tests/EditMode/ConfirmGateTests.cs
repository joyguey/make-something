using NUnit.Framework;

namespace RoomOrchestra.Tests
{
    /// <summary>Two plain taps for anything that ends or wipes; any other press is a "no" (George, 2026-11-03).</summary>
    public class ConfirmGateTests
    {
        [Test]
        public void FirstTapAsksSecondTapGoesAnotherPressCancels()
        {
            var gate = new ConfirmGate();
            Assert.IsFalse(gate.Request(7), "first tap only asks");
            Assert.AreEqual(7, gate.Asking);
            Assert.IsTrue(gate.Request(7), "the same button again goes ahead");
            Assert.AreEqual(0, gate.Asking);
            Assert.IsFalse(gate.Request(7), "and the next time it asks again");

            gate.Cancel();
            Assert.IsFalse(gate.Request(7), "after a cancel it asks again");
            Assert.IsFalse(gate.Request(9), "a different dangerous button asks for itself");
            Assert.IsFalse(gate.Request(7), "not confirmed by the other button's ask");
        }

        [Test]
        public void ConfirmWordingExistsInBothLanguages()
        {
            Assert.AreEqual("Sure?", UiText.Get(UiTerm.ConfirmAgain, UiLanguage.English));
            Assert.AreEqual("确定？", UiText.Get(UiTerm.ConfirmAgain, UiLanguage.Chinese));
            Assert.AreEqual("退出", UiText.Get(UiTerm.Quit, UiLanguage.Chinese));
        }
    }
}
