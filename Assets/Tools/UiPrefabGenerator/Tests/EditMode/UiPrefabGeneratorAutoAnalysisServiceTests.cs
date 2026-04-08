using System.IO;
using NUnit.Framework;
using UiPrefabGenerator.Editor.Bridge;
using UnityEngine;

namespace UiPrefabGenerator.Tests.EditMode
{
    public sealed class UiPrefabGeneratorAutoAnalysisServiceTests
    {
        private const string TaskDirectory = "Assets/UiPrefabGeneratorData/Tasks/test_auto_analysis_service";
        private const string AssetRoot = "Assets/UiPrefabGeneratorData/Cache/AutoAnalysisServiceAssets";
        private const string SourceImagePath = "Assets/UiPrefabGeneratorData/Cache/AutoAnalysisServiceAssets/source_image.png";

        [TearDown]
        public void TearDown()
        {
            UiPrefabGeneratorTestSupport.DeleteAssetIfExistsForTests(TaskDirectory);
            UiPrefabGeneratorTestSupport.DeleteAssetIfExistsForTests(AssetRoot);
        }

        [Test]
        public void AnalyzeTask_MapsSupportedMustHaveValuesAndPreservesUnknownItems()
        {
            PrepareAssetRoot(
                ("panel_bg_portrait.png", "panel"),
                ("primary_button_bg_portrait.png", "claim"));
            PrepareTaskRequest(
                mustHaveNodes: new[] { "task_list", "claim_button", "unknown_badge" },
                mustHaveInteractions: new[] { "claim_task" });

            var service = new UiPrefabGenerator.Editor.Analysis.UiPrefabGeneratorAutoAnalysisService();

            UiPrefabGenerator.Editor.Analysis.UiPrefabGeneratorAutoAnalysisResult result = service.AnalyzeTask(TaskDirectory);

            Assert.That(result.Success, Is.True);
            Assert.That(result.UiPrefabSpec.GenerationProfileId, Is.EqualTo("holmas_ugui_portrait"));
            Assert.That(result.UiPrefabSpec.Nodes, Has.Some.Matches<UiPrefabGenerator.Core.Schema.UiNodeSpec>(node => node.NodeId == "task_list"));
            Assert.That(result.UiPrefabSpec.Nodes, Has.Some.Matches<UiPrefabGenerator.Core.Schema.UiNodeSpec>(node => node.NodeId == "primary_button"));
            Assert.That(result.UiPrefabSpec.Nodes, Has.Some.Matches<UiPrefabGenerator.Core.Schema.UiNodeSpec>(node => node.NodeId == "title_text"));
            Assert.That(result.UnresolvedItems, Has.Some.EqualTo("unmapped_must_have_node:unknown_badge"));
            Assert.That(result.ResourceMatchReport.Matches, Has.Some.Matches<UiPrefabGenerator.Core.ResourceMatch.UiAssetSlotMatch>(match => match.AssetSlot == "panel_bg" && !string.IsNullOrWhiteSpace(match.SelectedAssetPath)));
            Assert.That(result.ResourceMatchReport.Matches, Has.Some.Matches<UiPrefabGenerator.Core.ResourceMatch.UiAssetSlotMatch>(match => match.AssetSlot == "primary_button_bg" && !string.IsNullOrWhiteSpace(match.SelectedAssetPath)));
            Assert.That(result.VisualUnderstanding, Is.Not.Null);
            Assert.That(result.VisualUnderstanding.Elements, Has.Some.Matches<UiPrefabGenerator.Core.Schema.VisualElementEvidence>(element => element.SemanticRole == "primary_button"));
            Assert.That(result.VisualReviewReport, Is.Not.Null);
            Assert.That(result.PreviewRenderPlan, Is.Not.Null);
            Assert.That(result.PreviewDiffReport, Is.Not.Null);
        }

        [Test]
        public void AnalyzeTask_LeavesLowScoreCandidateUnresolved()
        {
            PrepareAssetRoot(("panel_variant.png", "panel"));
            PrepareTaskRequest(mustHaveNodes: null, mustHaveInteractions: null);

            var service = new UiPrefabGenerator.Editor.Analysis.UiPrefabGeneratorAutoAnalysisService();

            UiPrefabGenerator.Editor.Analysis.UiPrefabGeneratorAutoAnalysisResult result = service.AnalyzeTask(TaskDirectory);

            Assert.That(result.Success, Is.True);
            Assert.That(result.ResourceMatchReport.Matches, Has.Count.EqualTo(1));
            Assert.That(result.ResourceMatchReport.Matches[0].AssetSlot, Is.EqualTo("panel_bg"));
            Assert.That(result.ResourceMatchReport.Matches[0].Candidates.Count, Is.GreaterThanOrEqualTo(1));
            Assert.That(result.ResourceMatchReport.Matches[0].SelectedAssetPath, Is.Empty);
            Assert.That(result.ResourceMatchReport.UnresolvedSlots, Has.Member("panel_bg"));
            Assert.That(result.PreviewRenderPlan.Nodes.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(result.PreviewDiffReport.Regions.Count, Is.GreaterThanOrEqualTo(1));
        }

        private static void PrepareTaskRequest(string[] mustHaveNodes, string[] mustHaveInteractions)
        {
            UiGenerationDataPaths.EnsureDataFolders();
            UiGenerationDataPaths.EnsureFolderExists(TaskDirectory);
            UiGenerationJsonFileUtility.SaveJson(
                TaskDirectory + "/" + UiGenerationDataPaths.RequestFileName,
                new UiPrefabGenerator.Core.Request.UiGenerationTaskRequest
                {
                    TaskId = "test_auto_analysis_service",
                    TemplateName = "holmas_portrait_wechat_default",
                    ProfileId = "holmas_ugui_portrait",
                    PageId = "agency_main",
                    PageTitle = "Agency Main",
                    PrefabName = "AgencyMainPanel",
                    AssetRoot = AssetRoot,
                    SourceImageTaskAssetPath = SourceImagePath,
                    MustHaveNodes = mustHaveNodes != null ? new System.Collections.Generic.List<string>(mustHaveNodes) : new System.Collections.Generic.List<string>(),
                    MustHaveInteractions = mustHaveInteractions != null ? new System.Collections.Generic.List<string>(mustHaveInteractions) : new System.Collections.Generic.List<string>(),
                });
        }

        private static void PrepareAssetRoot(params (string fileName, string content)[] files)
        {
            string absoluteRoot = UiPrefabGeneratorTestSupport.ToAbsolutePath(AssetRoot);
            Directory.CreateDirectory(absoluteRoot);
            WritePng(UiPrefabGeneratorTestSupport.ToAbsolutePath(SourceImagePath));
            for (int i = 0; i < files.Length; i++)
            {
                WritePng(Path.Combine(absoluteRoot, files[i].fileName));
            }
        }

        private static void WritePng(string absolutePath)
        {
            string directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }
    }
}
