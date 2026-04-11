using NUnit.Framework;

namespace UiPrefabGenerator.Tests.EditMode
{
    [SetUpFixture]
    public sealed class UiPrefabGeneratorGeneratedRootFixture
    {
        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            UiPrefabGeneratorTestSupport.PreserveTrackedGeneratedAssets();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            UiPrefabGeneratorTestSupport.CleanupGeneratedDraftRoot();
        }
    }
}
