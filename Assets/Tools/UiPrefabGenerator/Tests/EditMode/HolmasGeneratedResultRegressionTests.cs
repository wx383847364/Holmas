using NUnit.Framework;
using UiPrefabGenerator.HolmasAdapter;

namespace UiPrefabGenerator.Tests.EditMode
{
    public sealed class HolmasGeneratedResultRegressionTests
    {
        [Test]
        public void Consume_SampleManifestFixture_MatchesGoldenGeneratedResultPlanFixture()
        {
            var manifest = SampleFixtureLoader.LoadPrefabBindingManifest();
            var expected = SampleFixtureLoader.LoadHolmasGeneratedResultPlan();

            HolmasGeneratedResultConsumptionResult result = new HolmasGeneratedResultConsumer().Consume(manifest);

            Assert.That(result.Success, Is.True);
            SampleFixtureAssert.AreEqual(result.Plan, expected);
        }
    }
}
