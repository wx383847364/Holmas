using System.IO;
using NUnit.Framework;
using UiPrefabGenerator.Core.Result;
using UiPrefabGenerator.Editor.Generation;
using UiPrefabGenerator.Editor.Template;
using UnityEditor;
using UnityEngine;

namespace UiPrefabGenerator.Tests.EditMode
{
    public sealed class UiGenerationExecutionServiceTests
    {
        private const string TaskDirectory = "Assets/UiPrefabGeneratorData/Cache/UiGenerationExecutionServiceTests";

        [Test]
        public void Execute_GeneratesPortraitPrefabAndExecutionArtifacts()
        {
            try
            {
                var analysis = new UiGenerationAnalysisResult
                {
                    TaskId = "portrait_test",
                    Success = true,
                    DesignPacket = SampleFixtureLoader.LoadDesignPacket(),
                    UiPrefabSpec = SampleFixtureLoader.LoadUiPrefabSpec(),
                };
                analysis.UiPrefabSpec.GenerationProfileId = "holmas_ugui_portrait";

                var service = new UiGenerationExecutionService();
                var template = UiGenerationTemplateStore.BuildPortraitWechatDefault();

                UiGenerationExecutionResult result = service.Execute(TaskDirectory, template, analysis);

                Assert.That(result.Success, Is.True);
                Assert.That(result.PrefabPath, Is.EqualTo("Assets/HotUpdateContent/Res/Perfabs/Generated/Holmas/Portrait/AgencyMainPanel.prefab"));
                Assert.That(result.ManifestPath, Is.EqualTo(TaskDirectory + "/prefab_binding_manifest.json"));
                Assert.That(result.ManifestValidationPassed, Is.True);
                Assert.That(result.StructureValidationPassed, Is.True);
                Assert.That(result.ManualWiringNodes, Has.Member("AgencyMainPanel/ClaimButton"));
                Assert.That(AssetDatabase.LoadAssetAtPath<Object>(result.PrefabPath), Is.Not.Null);
                Assert.That(File.Exists(UiPrefabGeneratorTestSupport.ToAbsolutePath(TaskDirectory + "/generation_result.json")), Is.True);
                Assert.That(File.Exists(UiPrefabGeneratorTestSupport.ToAbsolutePath(TaskDirectory + "/prefab_binding_manifest.json")), Is.True);
            }
            finally
            {
                UiPrefabGeneratorTestSupport.CleanupGeneratedDraftRoot();
                UiPrefabGeneratorTestSupport.DeleteAssetIfExistsForTests(TaskDirectory);
            }
        }
    }
}
