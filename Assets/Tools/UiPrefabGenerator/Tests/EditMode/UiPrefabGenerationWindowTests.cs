using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UiPrefabGenerator.Core.Request;
using UiPrefabGenerator.Core.Result;
using UiPrefabGenerator.Editor.Bridge;
using UiPrefabGenerator.Editor.Window;
using UnityEngine;

namespace UiPrefabGenerator.Tests.EditMode
{
    public sealed class UiPrefabGenerationWindowTests
    {
        private const string TaskDirectory = "Assets/UiPrefabGeneratorData/Tasks/test_window_auto_analysis_refresh";
        private const string ScriptSourceTaskDirectory = "Assets/UiPrefabGeneratorData/Tasks/test_window_auto_analysis_refresh_source";
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
        public void TryRefreshAnalysisResult_LoadsPreviewTextureAfterBridgeSync()
        {
            PrepareTaskRequest();
            SaveValidAnalysisArtifacts("test_window_auto_analysis_refresh", ScriptSourceTaskDirectory);
            WriteSuccessSyncScript(ScriptSourceTaskDirectory);

            UiPrefabGeneratorAutoAnalysisResult bridgeResult = UiPrefabGeneratorAutoAnalysisBridge.RunTaskAutoAnalysis(TaskDirectory);
            Assert.That(bridgeResult.Success, Is.True);

            UiPrefabGenerationWindow window = ScriptableObject.CreateInstance<UiPrefabGenerationWindow>();
            try
            {
                SetPrivateField(window, "_currentTaskDirectory", TaskDirectory);

                object[] args = { null };
                bool refreshSuccess = (bool)typeof(UiPrefabGenerationWindow)
                    .GetMethod("TryRefreshAnalysisResult", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(window, args);

                Assert.That(refreshSuccess, Is.True, args[0] as string ?? string.Empty);

                object previewTexture = typeof(UiPrefabGenerationWindow)
                    .GetField("_previewRenderTexture", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(window);

                Assert.That(previewTexture, Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        private static void PrepareTaskRequest()
        {
            UiGenerationDataPaths.EnsureDataFolders();
            UiGenerationDataPaths.EnsureFolderExists(TaskDirectory);
            UiGenerationJsonFileUtility.SaveJson(
                TaskDirectory + "/" + UiGenerationDataPaths.RequestFileName,
                new UiGenerationTaskRequest
                {
                    TaskId = "test_window_auto_analysis_refresh",
                    TemplateName = "holmas_portrait_wechat_default",
                    ProfileId = "holmas_ugui_portrait",
                    PageId = "agency_main",
                    PageTitle = "Agency Main",
                    PrefabName = "AgencyMainPanel",
                    AssetRoot = "Assets/Res",
                });
        }

        private void WriteSuccessSyncScript(string sourceTaskDirectory)
        {
            string absoluteSourceDirectory = UiPrefabGeneratorTestSupport.ToAbsolutePath(sourceTaskDirectory);
            _scriptPath = Path.Combine(Path.GetTempPath(), "ui_prefab_generator_window_refresh_test.sh");
            File.WriteAllText(
                _scriptPath,
                "#!/usr/bin/env bash\nset -euo pipefail\n" +
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
            Environment.SetEnvironmentVariable(ScriptOverrideEnvironmentVariable, _scriptPath);
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

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            typeof(UiPrefabGenerationWindow)
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);
        }
    }
}
