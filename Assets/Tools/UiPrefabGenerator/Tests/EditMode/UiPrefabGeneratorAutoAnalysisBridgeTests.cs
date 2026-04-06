using System;
using System.IO;
using NUnit.Framework;
using UiPrefabGenerator.Core.Request;
using UiPrefabGenerator.Core.Result;
using UiPrefabGenerator.Editor.Bridge;

namespace UiPrefabGenerator.Tests.EditMode
{
    public sealed class UiPrefabGeneratorAutoAnalysisBridgeTests
    {
        private const string TaskDirectory = "Assets/UiPrefabGeneratorData/Tasks/test_auto_analysis_bridge";
        private const string ScriptOverrideEnvironmentVariable = "UI_PREFAB_GENERATOR_AUTO_ANALYSIS_SCRIPT";
        private string _scriptPath;

        [TearDown]
        public void TearDown()
        {
            Environment.SetEnvironmentVariable(ScriptOverrideEnvironmentVariable, null);
            if (!string.IsNullOrWhiteSpace(_scriptPath) && File.Exists(_scriptPath))
            {
                File.Delete(_scriptPath);
            }

            UiPrefabGeneratorTestSupport.DeleteAssetIfExistsForTests(TaskDirectory);
        }

        [Test]
        public void RunTaskAutoAnalysis_SucceedsWhenArtifactsAreValid()
        {
            PrepareTaskRequest();
            WriteSuccessScript("exit 0\n");
            SaveValidAnalysisArtifacts("test_auto_analysis_bridge");

            UiPrefabGeneratorAutoAnalysisResult result = UiPrefabGeneratorAutoAnalysisBridge.RunTaskAutoAnalysis(TaskDirectory);

            Assert.That(result.Success, Is.True);
            Assert.That(result.ExitCode, Is.EqualTo(0));
            Assert.That(result.ErrorSummary, Is.Empty);
        }

        [Test]
        public void RunTaskAutoAnalysis_FailsWhenScriptReturnsNonZero()
        {
            PrepareTaskRequest();
            WriteSuccessScript("echo 'simulated failure' 1>&2\nexit 7\n");

            UiPrefabGeneratorAutoAnalysisResult result = UiPrefabGeneratorAutoAnalysisBridge.RunTaskAutoAnalysis(TaskDirectory);

            Assert.That(result.Success, Is.False);
            Assert.That(result.ExitCode, Is.EqualTo(7));
            Assert.That(result.GetFailureMessage(), Does.Contain("simulated failure"));
        }

        [Test]
        public void RunTaskAutoAnalysis_FailsWhenArtifactsAreMissing()
        {
            PrepareTaskRequest();
            WriteSuccessScript("exit 0\n");

            UiPrefabGeneratorAutoAnalysisResult result = UiPrefabGeneratorAutoAnalysisBridge.RunTaskAutoAnalysis(TaskDirectory);

            Assert.That(result.Success, Is.False);
            Assert.That(result.GetFailureMessage(), Does.Contain("缺少产物"));
        }

        [Test]
        public void RunTaskAutoAnalysis_FailsWhenTaskIdDoesNotMatchDirectory()
        {
            PrepareTaskRequest();
            WriteSuccessScript("exit 0\n");
            SaveValidAnalysisArtifacts("another_task");

            UiPrefabGeneratorAutoAnalysisResult result = UiPrefabGeneratorAutoAnalysisBridge.RunTaskAutoAnalysis(TaskDirectory);

            Assert.That(result.Success, Is.False);
            Assert.That(result.GetFailureMessage(), Does.Contain("task_id"));
        }

        private static void PrepareTaskRequest()
        {
            UiGenerationDataPaths.EnsureDataFolders();
            UiGenerationDataPaths.EnsureFolderExists(TaskDirectory);
            UiGenerationJsonFileUtility.SaveJson(
                TaskDirectory + "/" + UiGenerationDataPaths.RequestFileName,
                new UiGenerationTaskRequest
                {
                    TaskId = "test_auto_analysis_bridge",
                    TemplateName = "holmas_portrait_wechat_default",
                    ProfileId = "holmas_ugui_portrait",
                    PageId = "agency_main",
                    PageTitle = "Agency Main",
                    PrefabName = "AgencyMainPanel",
                    AssetRoot = "Assets/Res",
                });
        }

        private void WriteSuccessScript(string body)
        {
            _scriptPath = Path.Combine(Path.GetTempPath(), "ui_prefab_generator_auto_analysis_bridge_test.sh");
            File.WriteAllText(_scriptPath, "#!/usr/bin/env bash\nset -euo pipefail\n" + body);
            Environment.SetEnvironmentVariable(ScriptOverrideEnvironmentVariable, _scriptPath);
        }

        private static void SaveValidAnalysisArtifacts(string taskId)
        {
            UiGenerationTaskStorage.SaveAnalysisArtifacts(
                TaskDirectory,
                new UiGenerationAnalysisResult
                {
                    TaskId = taskId,
                    Success = true,
                    TemplateName = "holmas_portrait_wechat_default",
                    ProfileId = "holmas_ugui_portrait",
                    DesignPacket = SampleFixtureLoader.LoadDesignPacket(),
                    UiPrefabSpec = SampleFixtureLoader.LoadUiPrefabSpec(),
                    ResourceMatchReport = new UiPrefabGenerator.Core.ResourceMatch.UiResourceMatchReport
                    {
                        TaskId = taskId,
                        AssetRoot = "Assets/Res",
                    },
                });
            UiGenerationJsonFileUtility.SaveText(
                TaskDirectory + "/" + UiGenerationDataPaths.AnalysisSummaryFileName,
                "# ok\n");
        }
    }
}
