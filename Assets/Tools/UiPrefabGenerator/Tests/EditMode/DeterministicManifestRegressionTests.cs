using NUnit.Framework;
using UiPrefabGenerator.Editor.Generation;
using UiPrefabGenerator.HolmasAdapter;

namespace UiPrefabGenerator.Tests.EditMode
{
    public sealed class DeterministicManifestRegressionTests
    {
        [Test]
        public void GenerateDraft_FromSampleSpecFixture_MatchesGoldenManifestFixture()
        {
            var spec = SampleFixtureLoader.LoadUiPrefabSpec();
            var expected = SampleFixtureLoader.LoadPrefabBindingManifest();

            UiPrefabGenerationResult generation = new PreviewUnityPrefabGenerator().GenerateDraft(new UiPrefabGenerationRequest
            {
                Spec = spec,
                Profile = new HolmasUiProjectProfile(),
            });

            Assert.That(generation.Success, Is.True);
            SampleFixtureAssert.AreEqual(generation.Manifest, expected);
        }
    }
}
