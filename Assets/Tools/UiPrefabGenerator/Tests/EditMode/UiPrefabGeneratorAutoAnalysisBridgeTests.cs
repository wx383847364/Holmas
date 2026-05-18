using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using UiPrefabGenerator.Core.Request;
using UiPrefabGenerator.Core.Result;
using UiPrefabGenerator.Editor.Bridge;
using UnityEditor;
using UnityEngine;

namespace UiPrefabGenerator.Tests.EditMode
{
    public sealed class UiPrefabGeneratorAutoAnalysisBridgeTests
    {
        private const string TaskDirectory = "Assets/UiPrefabGeneratorData/Tasks/test_auto_analysis_bridge";
        private const string ScriptSourceTaskDirectory = "Assets/UiPrefabGeneratorData/Tasks/test_auto_analysis_bridge_script_source";
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
            UiPrefabGeneratorTestSupport.DeleteAssetIfExistsForTests(ScriptSourceTaskDirectory);
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
        public void RunTaskAutoAnalysis_SucceedsWhenOnlyCoreArtifactsAreSyncedBack()
        {
            PrepareTaskRequest();
            WriteSuccessScript("exit 0\n");
            SaveCoreOnlyAnalysisArtifacts("test_auto_analysis_bridge");

            UiPrefabGeneratorAutoAnalysisResult result = UiPrefabGeneratorAutoAnalysisBridge.RunTaskAutoAnalysis(TaskDirectory);

            Assert.That(result.Success, Is.True);
            Assert.That(result.ExitCode, Is.EqualTo(0));
            Assert.That(result.ErrorSummary, Is.Empty);
        }

        [Test]
        public void RunTaskAutoAnalysis_SucceedsWhenScriptSyncsFullReviewArtifactsBack()
        {
            PrepareTaskRequest();
            SaveValidAnalysisArtifacts("test_auto_analysis_bridge", ScriptSourceTaskDirectory);
            WriteSuccessSyncScript(ScriptSourceTaskDirectory);

            UiPrefabGeneratorAutoAnalysisResult result = UiPrefabGeneratorAutoAnalysisBridge.RunTaskAutoAnalysis(TaskDirectory);

            Assert.That(result.Success, Is.True);
            Assert.That(File.Exists(UiPrefabGeneratorTestSupport.ToAbsolutePath(TaskDirectory + "/" + UiGenerationDataPaths.VisualUnderstandingFileName)), Is.True);
            Assert.That(File.Exists(UiPrefabGeneratorTestSupport.ToAbsolutePath(TaskDirectory + "/" + UiGenerationDataPaths.VisualReviewReportFileName)), Is.True);
            Assert.That(File.Exists(UiPrefabGeneratorTestSupport.ToAbsolutePath(TaskDirectory + "/" + UiGenerationDataPaths.IntakeAssessmentFileName)), Is.True);
            Assert.That(File.Exists(UiPrefabGeneratorTestSupport.ToAbsolutePath(TaskDirectory + "/" + UiGenerationDataPaths.GatingReportFileName)), Is.True);
            Assert.That(File.Exists(UiPrefabGeneratorTestSupport.ToAbsolutePath(TaskDirectory + "/" + UiGenerationDataPaths.PreviewRenderPlanFileName)), Is.True);
            Assert.That(File.Exists(UiPrefabGeneratorTestSupport.ToAbsolutePath(TaskDirectory + "/" + UiGenerationDataPaths.PreviewRenderImageFileName)), Is.True);
            Assert.That(File.Exists(UiPrefabGeneratorTestSupport.ToAbsolutePath(TaskDirectory + "/" + UiGenerationDataPaths.PreviewDiffReportFileName)), Is.True);

            Texture2D previewTexture = UiGenerationTaskStorage.LoadPreviewRenderTexture(TaskDirectory);
            Assert.That(previewTexture, Is.Not.Null);
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
            File.WriteAllText(_scriptPath, "#!/usr/bin/env bash\nset -euo pipefail\n" + body, new UTF8Encoding(false));
            Environment.SetEnvironmentVariable(ScriptOverrideEnvironmentVariable, _scriptPath);
        }

        private void WriteSuccessSyncScript(string sourceTaskDirectory)
        {
            string absoluteSourceDirectory = UiPrefabGeneratorTestSupport.ToAbsolutePath(sourceTaskDirectory);
            WriteSuccessScript(
                "target_dir=\"\"\n" +
                "while [[ $# -gt 0 ]]; do\n" +
                "  case \"$1\" in\n" +
                "    --task-dir)\n" +
                "      target_dir=\"$2\"\n" +
                "      shift 2\n" +
                "      ;;\n" +
                "    *)\n" +
                "      shift\n" +
                "      ;;\n" +
                "  esac\n" +
                "done\n" +
                "if [[ -z \"$target_dir\" ]]; then\n" +
                "  echo 'missing task dir' 1>&2\n" +
                "  exit 9\n" +
                "fi\n" +
                "mkdir -p \"$target_dir\"\n" +
                "for file in \\\n" +
                "  visual_understanding.json \\\n" +
                "  visual_review_report.json \\\n" +
                "  design_packet.json \\\n" +
                "  design_packet_intake_assessment.json \\\n" +
                "  gating_report.json \\\n" +
                "  ui_prefab_spec.json \\\n" +
                "  resource_match_report.json \\\n" +
                "  preview_render_plan.json \\\n" +
                "  preview_render.png \\\n" +
                "  preview_diff_report.json \\\n" +
                "  analysis_result.json \\\n" +
                "  analysis_summary.md; do\n" +
                "  cp \"" + absoluteSourceDirectory + "/$file\" \"$target_dir/$file\"\n" +
                "done\n" +
                "exit 0\n");
        }

        private static void SaveValidAnalysisArtifacts(string taskId)
        {
            SaveValidAnalysisArtifacts(taskId, TaskDirectory);
        }

        private static void SaveValidAnalysisArtifacts(string taskId, string taskDirectory)
        {
            UiGenerationDataPaths.EnsureDataFolders();
            UiGenerationDataPaths.EnsureFolderExists(taskDirectory);
            UiGenerationTaskStorage.SaveAnalysisArtifacts(
                taskDirectory,
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
                taskDirectory + "/" + UiGenerationDataPaths.AnalysisSummaryFileName,
                "# ok\n");
        }

        private static void SaveCoreOnlyAnalysisArtifacts(string taskId)
        {
            UiGenerationAnalysisResult analysis = new UiGenerationAnalysisResult
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
            };

            UiGenerationJsonFileUtility.SaveJson(TaskDirectory + "/" + UiGenerationDataPaths.DesignPacketFileName, analysis.DesignPacket);
            UiGenerationJsonFileUtility.SaveJson(TaskDirectory + "/" + UiGenerationDataPaths.UiPrefabSpecFileName, analysis.UiPrefabSpec);
            UiGenerationJsonFileUtility.SaveJson(TaskDirectory + "/" + UiGenerationDataPaths.ResourceMatchReportFileName, analysis.ResourceMatchReport);
            UiGenerationJsonFileUtility.SaveJson(TaskDirectory + "/" + UiGenerationDataPaths.AnalysisResultFileName, analysis);
            UiGenerationJsonFileUtility.SaveText(TaskDirectory + "/" + UiGenerationDataPaths.AnalysisSummaryFileName, "# ok\n");
        }
    }
}
