using NUnit.Framework;
using UiPrefabGenerator.Core.Intake;
using UiPrefabGenerator.Core.Schema;

namespace UiPrefabGenerator.Tests.EditMode
{
    public sealed class DesignPacketInterpreterTests
    {
        [Test]
        public void Interpret_SampleFixtureMatchesGoldenSpec()
        {
            DesignPacket packet = SampleFixtureLoader.LoadDesignPacket();
            UiPrefabSpec expected = SampleFixtureLoader.LoadUiPrefabSpec();

            UiPrefabSpec spec = new DefaultDesignPacketToUiPrefabSpecInterpreter().Interpret(packet);

            SampleFixtureAssert.AreEqual(spec, expected);
        }

        [Test]
        public void Interpret_ThrowsWhenBlockingIssuesRemain()
        {
            var packet = new DesignPacket
            {
                PrefabName = "AgencyMainPanel",
            };

            Assert.That(
                () => new DefaultDesignPacketToUiPrefabSpecInterpreter().Interpret(packet),
                Throws.InvalidOperationException);
        }
    }
}
