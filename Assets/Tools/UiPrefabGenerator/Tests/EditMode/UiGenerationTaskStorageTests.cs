using System.IO;
using NUnit.Framework;
using UiPrefabGenerator.Core.ResourceMatch;
using UiPrefabGenerator.Core.Request;
using UiPrefabGenerator.Core.Result;
using UiPrefabGenerator.Core.Schema;
using UiPrefabGenerator.Editor.Bridge;
using UiPrefabGenerator.Editor.Template;
using UnityEditor;
using UnityEngine;

namespace UiPrefabGenerator.Tests.EditMode
{
    public sealed class UiGenerationTaskStorageTests
    {
        private const string TempImageAssetPath = "Assets/UiPrefabGeneratorData/Cache/test_source_image.png";

        [Test]
        public void DefaultPortraitTemplate_CanBeLoaded()
        {
            UiGenerationTemplateStore.EnsureDefaultTemplateExists();

            var template = UiGenerationTemplateStore.LoadTemplate(UiGenerationDataPaths.DefaultPortraitTemplatePath);

            Assert.That(template, Is.Not.Null);
            Assert.That(template.ProfileId, Is.EqualTo("holmas_ugui_portrait"));
            Assert.That(template.Orientation, Is.EqualTo("portrait"));
            Assert.That(template.DraftPrefabRoot, Is.EqualTo("Assets/Res/Perfabs/Generated/Holmas/Portrait"));
        }

        [Test]
        public void CreateRequestTask_WritesRequestAndCopiesSourceImage()
        {
            try
            {
                CreateTempImageAsset();
                var request = new UiGenerationTaskRequest
                {
                    TaskId = "test_request_task",
                    TemplateName = "holmas_portrait_wechat_default",
                    ProfileId = "holmas_ugui_portrait",
                    SourceImageAssetPath = TempImageAssetPath,
                    PageId = "portrait_home",
                    PageTitle = "Portrait Home",
                    PrefabName = "PortraitHomePanel",
                    Orientation = "portrait",
                    ReferenceResolutionWidth = 1080,
                    ReferenceResolutionHeight = 1920,
                    AssetRoot = "Assets/Res",
                    DraftPrefabRoot = "Assets/Res/Perfabs/Generated/Holmas/Portrait",
                };
                Texture2D sourceImage = AssetDatabase.LoadAssetAtPath<Texture2D>(TempImageAssetPath);
                string taskDirectory = UiGenerationTaskStorage.CreateTask(request, sourceImage);
                string requestPath = taskDirectory + "/" + UiGenerationDataPaths.RequestFileName;

                Assert.That(File.Exists(UiPrefabGeneratorTestSupport.ToAbsolutePath(requestPath)), Is.True);

                UiGenerationTaskRequest loadedRequest;
                Assert.That(UiGenerationJsonFileUtility.TryLoadJson(requestPath, out loadedRequest), Is.True);
                Assert.That(loadedRequest, Is.Not.Null);
                Assert.That(loadedRequest.SourceImageTaskAssetPath, Does.Contain("source_image"));
                Assert.That(File.Exists(UiPrefabGeneratorTestSupport.ToAbsolutePath(loadedRequest.SourceImageTaskAssetPath)), Is.True);
            }
            finally
            {
                CleanupTaskArtifacts("Assets/UiPrefabGeneratorData/Tasks/test_request_task");
                UiPrefabGeneratorTestSupport.DeleteAssetIfExistsForTests(TempImageAssetPath);
                UiPrefabGeneratorTestSupport.DeleteAssetIfExistsForTests("Assets/UiPrefabGeneratorData/Cache");
                AssetDatabase.Refresh();
            }
        }

        [Test]
        public void TryLoadAnalysisResult_RebuildsFromFallbackArtifacts()
        {
            string taskDirectory = "Assets/UiPrefabGeneratorData/Tasks/test_execution_task";
            try
            {
                UiGenerationDataPaths.EnsureDataFolders();
                UiGenerationDataPaths.EnsureFolderExists(taskDirectory);

                var designPacket = SampleFixtureLoader.LoadDesignPacket();
                var spec = SampleFixtureLoader.LoadUiPrefabSpec();
                var report = new UiResourceMatchReport
                {
                    TaskId = "test_execution_task",
                    AssetRoot = "Assets/Res",
                };
                report.Matches.Add(new UiAssetSlotMatch
                {
                    AssetSlot = "claim_button_bg",
                    ComponentType = "Image",
                    SelectedAssetPath = "Assets/Res/Fake/claim_button.png",
                    SelectedAssetType = "Sprite",
                    Confidence = 0.8f,
                });

                UiGenerationJsonFileUtility.SaveJson(taskDirectory + "/" + UiGenerationDataPaths.DesignPacketFileName, designPacket);
                UiGenerationJsonFileUtility.SaveJson(taskDirectory + "/" + UiGenerationDataPaths.UiPrefabSpecFileName, spec);
                UiGenerationJsonFileUtility.SaveJson(taskDirectory + "/" + UiGenerationDataPaths.ResourceMatchReportFileName, report);

                UiGenerationAnalysisResult loadedResult;
                string error;
                Assert.That(UiGenerationTaskStorage.TryLoadAnalysisResult(taskDirectory, out loadedResult, out error), Is.True);
                Assert.That(error, Is.Empty);
                Assert.That(loadedResult, Is.Not.Null);
                Assert.That(loadedResult.TaskId, Is.EqualTo("test_execution_task"));
                Assert.That(loadedResult.Success, Is.True);
                Assert.That(loadedResult.DesignPacket, Is.Not.Null);
                Assert.That(loadedResult.UiPrefabSpec, Is.Not.Null);
                Assert.That(loadedResult.ResourceMatchReport, Is.Not.Null);
                Assert.That(loadedResult.ResourceMatchReport.Matches.Count, Is.EqualTo(1));
            }
            finally
            {
                CleanupTaskArtifacts(taskDirectory);
            }
        }

        [Test]
        public void TryLoadAnalysisResult_FallsBackWhenResourceMatchReportIsMissing()
        {
            string taskDirectory = "Assets/UiPrefabGeneratorData/Tasks/test_missing_match_report";
            try
            {
                UiGenerationDataPaths.EnsureDataFolders();
                UiGenerationDataPaths.EnsureFolderExists(taskDirectory);

                var designPacket = SampleFixtureLoader.LoadDesignPacket();
                var spec = SampleFixtureLoader.LoadUiPrefabSpec();

                UiGenerationJsonFileUtility.SaveJson(taskDirectory + "/" + UiGenerationDataPaths.DesignPacketFileName, designPacket);
                UiGenerationJsonFileUtility.SaveJson(taskDirectory + "/" + UiGenerationDataPaths.UiPrefabSpecFileName, spec);

                UiGenerationAnalysisResult loadedResult;
                string error;
                Assert.That(UiGenerationTaskStorage.TryLoadAnalysisResult(taskDirectory, out loadedResult, out error), Is.True);
                Assert.That(error, Is.Empty);
                Assert.That(loadedResult, Is.Not.Null);
                Assert.That(loadedResult.ResourceMatchReport, Is.Not.Null);
                Assert.That(loadedResult.ResourceMatchReport.Matches, Is.Empty);
                Assert.That(loadedResult.ResourceMatchReport.UnresolvedSlots, Is.Empty);
            }
            finally
            {
                CleanupTaskArtifacts(taskDirectory);
            }
        }

        [Test]
        public void TryLoadAnalysisResult_FailsWhenDesignPacketIsMissing()
        {
            string taskDirectory = "Assets/UiPrefabGeneratorData/Tasks/test_missing_design_packet";
            try
            {
                UiGenerationDataPaths.EnsureDataFolders();
                UiGenerationDataPaths.EnsureFolderExists(taskDirectory);

                UiGenerationJsonFileUtility.SaveJson(
                    taskDirectory + "/" + UiGenerationDataPaths.UiPrefabSpecFileName,
                    SampleFixtureLoader.LoadUiPrefabSpec());

                UiGenerationAnalysisResult loadedResult;
                string error;
                Assert.That(UiGenerationTaskStorage.TryLoadAnalysisResult(taskDirectory, out loadedResult, out error), Is.False);
                Assert.That(loadedResult, Is.Null);
                Assert.That(error, Does.Contain("design_packet"));
            }
            finally
            {
                CleanupTaskArtifacts(taskDirectory);
            }
        }

        [Test]
        public void TryLoadAnalysisResult_FailsWhenUiPrefabSpecIsMissing()
        {
            string taskDirectory = "Assets/UiPrefabGeneratorData/Tasks/test_missing_ui_prefab_spec";
            try
            {
                UiGenerationDataPaths.EnsureDataFolders();
                UiGenerationDataPaths.EnsureFolderExists(taskDirectory);

                UiGenerationJsonFileUtility.SaveJson(
                    taskDirectory + "/" + UiGenerationDataPaths.DesignPacketFileName,
                    SampleFixtureLoader.LoadDesignPacket());

                UiGenerationAnalysisResult loadedResult;
                string error;
                Assert.That(UiGenerationTaskStorage.TryLoadAnalysisResult(taskDirectory, out loadedResult, out error), Is.False);
                Assert.That(loadedResult, Is.Null);
                Assert.That(error, Does.Contain("ui_prefab_spec"));
            }
            finally
            {
                CleanupTaskArtifacts(taskDirectory);
            }
        }

        [Test]
        public void TryLoadAnalysisResult_FailsWhenTaskIdDoesNotMatchDirectory()
        {
            string taskDirectory = "Assets/UiPrefabGeneratorData/Tasks/test_task_id_guard";
            try
            {
                UiGenerationDataPaths.EnsureDataFolders();
                UiGenerationDataPaths.EnsureFolderExists(taskDirectory);

                UiGenerationJsonFileUtility.SaveJson(
                    taskDirectory + "/" + UiGenerationDataPaths.AnalysisResultFileName,
                    new UiGenerationAnalysisResult
                    {
                        TaskId = "another_task",
                        Success = true,
                        DesignPacket = SampleFixtureLoader.LoadDesignPacket(),
                        UiPrefabSpec = SampleFixtureLoader.LoadUiPrefabSpec(),
                    });

                UiGenerationAnalysisResult loadedResult;
                string error;
                Assert.That(UiGenerationTaskStorage.TryLoadAnalysisResult(taskDirectory, out loadedResult, out error), Is.False);
                Assert.That(loadedResult, Is.Null);
                Assert.That(error, Does.Contain("task_id"));
            }
            finally
            {
                CleanupTaskArtifacts(taskDirectory);
            }
        }

        [Test]
        public void TryLoadAnalysisResult_PrefersAnalysisResultJsonWhenPresent()
        {
            string taskDirectory = "Assets/UiPrefabGeneratorData/Tasks/test_analysis_result_precedence";
            try
            {
                UiGenerationDataPaths.EnsureDataFolders();
                UiGenerationDataPaths.EnsureFolderExists(taskDirectory);

                UiGenerationJsonFileUtility.SaveJson(
                    taskDirectory + "/" + UiGenerationDataPaths.DesignPacketFileName,
                    CreateDesignPacket("fallback_page", "Fallback Page", "FallbackPrefab"));
                UiGenerationJsonFileUtility.SaveJson(
                    taskDirectory + "/" + UiGenerationDataPaths.UiPrefabSpecFileName,
                    CreateUiPrefabSpec("fallback_page", "FallbackPrefab"));
                UiGenerationJsonFileUtility.SaveJson(
                    taskDirectory + "/" + UiGenerationDataPaths.ResourceMatchReportFileName,
                    CreateResourceMatchReport("test_analysis_result_precedence", null, false));

                UiGenerationAnalysisResult expected = CreateAnalysisResult(
                    "test_analysis_result_precedence",
                    "direct_page",
                    "Direct Page",
                    "DirectPrefab",
                    "DirectTemplate",
                    "direct_profile");
                UiGenerationJsonFileUtility.SaveJson(
                    taskDirectory + "/" + UiGenerationDataPaths.AnalysisResultFileName,
                    expected);

                UiGenerationAnalysisResult loadedResult;
                string error;
                Assert.That(UiGenerationTaskStorage.TryLoadAnalysisResult(taskDirectory, out loadedResult, out error), Is.True);
                Assert.That(error, Is.Empty);
                Assert.That(loadedResult.TaskId, Is.EqualTo("test_analysis_result_precedence"));
                Assert.That(loadedResult.TemplateName, Is.EqualTo("DirectTemplate"));
                Assert.That(loadedResult.ProfileId, Is.EqualTo("direct_profile"));
                Assert.That(loadedResult.DesignPacket.PageId, Is.EqualTo("direct_page"));
                Assert.That(loadedResult.DesignPacket.PrefabName, Is.EqualTo("DirectPrefab"));
            }
            finally
            {
                CleanupTaskArtifacts(taskDirectory);
            }
        }

        [Test]
        public void SaveAnalysisArtifacts_WritesAllAnalysisArtifacts()
        {
            string taskDirectory = "Assets/UiPrefabGeneratorData/Tasks/test_save_analysis_artifacts";
            try
            {
                UiGenerationDataPaths.EnsureDataFolders();
                UiGenerationDataPaths.EnsureFolderExists(taskDirectory);

                UiGenerationAnalysisResult analysis = CreateAnalysisResult(
                    "test_save_analysis_artifacts",
                    "analysis_page",
                    "Analysis Page",
                    "AnalysisPrefab",
                    "AnalysisTemplate",
                    "analysis_profile");

                UiGenerationTaskStorage.SaveAnalysisArtifacts(taskDirectory, analysis);

                Assert.That(File.Exists(UiPrefabGeneratorTestSupport.ToAbsolutePath(taskDirectory + "/" + UiGenerationDataPaths.AnalysisResultFileName)), Is.True);
                Assert.That(File.Exists(UiPrefabGeneratorTestSupport.ToAbsolutePath(taskDirectory + "/" + UiGenerationDataPaths.DesignPacketFileName)), Is.True);
                Assert.That(File.Exists(UiPrefabGeneratorTestSupport.ToAbsolutePath(taskDirectory + "/" + UiGenerationDataPaths.UiPrefabSpecFileName)), Is.True);
                Assert.That(File.Exists(UiPrefabGeneratorTestSupport.ToAbsolutePath(taskDirectory + "/" + UiGenerationDataPaths.ResourceMatchReportFileName)), Is.True);

                UiGenerationAnalysisResult loadedResult;
                string error;
                Assert.That(UiGenerationTaskStorage.TryLoadAnalysisResult(taskDirectory, out loadedResult, out error), Is.True);
                Assert.That(error, Is.Empty);
                Assert.That(loadedResult.TaskId, Is.EqualTo("test_save_analysis_artifacts"));
                Assert.That(loadedResult.TemplateName, Is.EqualTo("AnalysisTemplate"));
                Assert.That(loadedResult.ProfileId, Is.EqualTo("analysis_profile"));
                Assert.That(loadedResult.ResourceMatchReport.Matches.Count, Is.EqualTo(1));
                Assert.That(loadedResult.ResourceMatchReport.UnresolvedSlots, Has.Member("claim_button_bg"));
            }
            finally
            {
                CleanupTaskArtifacts(taskDirectory);
            }
        }

        [Test]
        public void LoadAnalysisSummary_ReturnsSavedMarkdown()
        {
            string taskDirectory = "Assets/UiPrefabGeneratorData/Tasks/test_analysis_summary";
            try
            {
                UiGenerationDataPaths.EnsureDataFolders();
                UiGenerationDataPaths.EnsureFolderExists(taskDirectory);

                const string expectedSummary = "## analysis\n- ok\n";
                UiGenerationJsonFileUtility.SaveText(taskDirectory + "/" + UiGenerationDataPaths.AnalysisSummaryFileName, expectedSummary);

                Assert.That(UiGenerationTaskStorage.LoadAnalysisSummary(taskDirectory), Is.EqualTo(expectedSummary));
            }
            finally
            {
                CleanupTaskArtifacts(taskDirectory);
            }
        }

        private static void CreateTempImageAsset()
        {
            Texture2D texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[16];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color(1f, 1f, 1f, 1f);
            }

            texture.SetPixels(pixels);
            texture.Apply();

            string absolutePath = UiPrefabGeneratorTestSupport.ToAbsolutePath(TempImageAssetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
            File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(TempImageAssetPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.Refresh();
        }

        private static UiGenerationAnalysisResult CreateAnalysisResult(
            string taskId,
            string pageId,
            string pageTitle,
            string prefabName,
            string templateName,
            string profileId)
        {
            return new UiGenerationAnalysisResult
            {
                TaskId = taskId,
                Success = true,
                TemplateName = templateName,
                ProfileId = profileId,
                DesignPacket = CreateDesignPacket(pageId, pageTitle, prefabName),
                UiPrefabSpec = CreateUiPrefabSpec(pageId, prefabName),
                ResourceMatchReport = CreateResourceMatchReport(taskId, "Assets/Res", true),
            };
        }

        private static DesignPacket CreateDesignPacket(string pageId, string pageTitle, string prefabName)
        {
            return new DesignPacket
            {
                PageId = pageId,
                PageTitle = pageTitle,
                PrefabName = prefabName,
                Notes = "test",
            };
        }

        private static UiPrefabSpec CreateUiPrefabSpec(string pageId, string prefabName)
        {
            return new UiPrefabSpec
            {
                PageId = pageId,
                PrefabName = prefabName,
                RootNodeId = "root",
                GenerationProfileId = "test_profile",
            };
        }

        private static UiResourceMatchReport CreateResourceMatchReport(string taskId, string assetRoot, bool includeSelectedAsset)
        {
            var report = new UiResourceMatchReport
            {
                TaskId = taskId,
                AssetRoot = assetRoot ?? string.Empty,
            };
            report.Matches.Add(new UiAssetSlotMatch
            {
                AssetSlot = "panel_bg",
                ComponentType = "Image",
                SelectedAssetPath = includeSelectedAsset ? "Assets/Res/UI/panel_bg.png" : string.Empty,
                SelectedAssetType = includeSelectedAsset ? "Sprite" : string.Empty,
                Confidence = includeSelectedAsset ? 0.9f : 0.2f,
                Notes = includeSelectedAsset ? "selected" : "low confidence",
            });
            report.UnresolvedSlots.Add("claim_button_bg");
            if (!includeSelectedAsset)
            {
                report.Matches[0].Candidates.Add(new UiAssetCandidate
                {
                    AssetPath = "Assets/Res/UI/panel_bg_low.png",
                    AssetType = "Sprite",
                    Score = 0.2f,
                    Reason = "low score",
                    Recommended = false,
                });
            }

            return report;
        }

        private static void CleanupTaskArtifacts(string taskDirectory)
        {
            UiPrefabGeneratorTestSupport.DeleteAssetIfExistsForTests(taskDirectory);
            UiPrefabGeneratorTestSupport.DeleteAssetIfExistsForTests("Assets/UiPrefabGeneratorData/Tasks");
            AssetDatabase.Refresh();
        }
    }
}
