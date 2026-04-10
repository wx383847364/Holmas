using NUnit.Framework;
using UiPrefabGenerator.Editor.Generation;
using UiPrefabGenerator.HolmasAdapter;

namespace UiPrefabGenerator.Tests.EditMode
{
    public sealed class SampleUiPipelineRunnerTests
    {
        [Test]
        public void Run_SamplePipelineProducesGoldenManifestAdapterPlanAndPrefabDraft()
        {
            try
            {
                var report = new DefaultSampleUiPipelineRunner().Run(new SampleUiPipelineRequest
                {
                    DesignPacket = SampleFixtureLoader.LoadDesignPacket(),
                    Profile = new HolmasPortraitUiProjectProfile(),
                });

                Assert.That(report.Success, Is.True);
                Assert.That(report.PrefabDraftPath, Is.EqualTo("Assets/Res/Perfabs/Generated/Holmas/Portrait/AgencyMainPanel.prefab"));
                SampleFixtureAssert.AreEqual(report.PreviewGeneration.Manifest, SampleFixtureLoader.LoadPrefabBindingManifest());
                SampleFixtureAssert.AreEqual(
                    ((UiPrefabGenerator.HolmasAdapter.HolmasGeneratedResultConsumptionResult)report.AdapterResult).Plan,
                    SampleFixtureLoader.LoadHolmasGeneratedResultPlan());
                Assert.That(report.ManifestValidation.IsValid, Is.True);
                Assert.That(report.DraftStructureValidation.IsValid, Is.True);
                Assert.That(report.DraftWrite.Success, Is.True);
                Assert.That(report.ManifestText, Is.Not.Empty);
                Assert.That(report.AdapterPlanText, Is.Not.Empty);
                Assert.That(report.Stages, Has.Some.Matches<SampleUiPipelineStageReport>(stage => stage.StageId == "manifest_validation" && stage.Success));
                Assert.That(report.Stages, Has.Some.Matches<SampleUiPipelineStageReport>(stage => stage.StageId == "prefab_structure_validation" && stage.Success));
            }
            finally
            {
                UiPrefabGeneratorTestSupport.CleanupGeneratedDraftRoot();
            }
        }

        [Test]
        public void Run_IsDeterministicAcrossRepeatedExecutions()
        {
            try
            {
                var runner = new DefaultSampleUiPipelineRunner();
                var request = new SampleUiPipelineRequest
                {
                    DesignPacket = SampleFixtureLoader.LoadDesignPacket(),
                    Profile = new HolmasPortraitUiProjectProfile(),
                };

                SampleUiPipelineReport first = runner.Run(request);
                SampleUiPipelineReport second = runner.Run(request);

                Assert.That(first.Success, Is.True);
                Assert.That(second.Success, Is.True);
                Assert.That(first.ManifestText, Is.EqualTo(second.ManifestText));
                Assert.That(first.AdapterPlanText, Is.EqualTo(second.AdapterPlanText));
                Assert.That(first.PrefabDraftPath, Is.EqualTo(second.PrefabDraftPath));
            }
            finally
            {
                UiPrefabGeneratorTestSupport.CleanupGeneratedDraftRoot();
            }
        }
    }
}
