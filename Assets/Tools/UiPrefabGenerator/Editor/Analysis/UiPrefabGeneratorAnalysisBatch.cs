using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UiPrefabGenerator.Editor.Bridge;
using UnityEditor;
using UnityEngine;

namespace UiPrefabGenerator.Editor.Analysis
{
    public static class UiPrefabGeneratorAnalysisBatch
    {
        private const string TaskDirEnvName = "UI_PREFAB_TASK_AUTO_ANALYSIS_TASK_DIR";
        private const string ReportPathEnvName = "UI_PREFAB_TASK_AUTO_ANALYSIS_REPORT_PATH";
        private const string DefaultReportFileName = "ui_prefab_task_auto_analysis_report.json";
        private const string SuccessPattern = "UiPrefabGenerator task auto analysis finished.";

        public static void RunTaskAutoAnalysisBatch()
        {
            string taskDirectory = Environment.GetEnvironmentVariable(TaskDirEnvName);
            string reportPath = Environment.GetEnvironmentVariable(ReportPathEnvName);
            if (string.IsNullOrWhiteSpace(reportPath))
            {
                reportPath = Path.Combine(Path.GetTempPath(), DefaultReportFileName);
            }

            var report = new UiPrefabGeneratorAnalysisBatchReport
            {
                TaskDirectory = taskDirectory ?? string.Empty,
            };

            try
            {
                RunTaskAutoAnalysis(taskDirectory, report);
                report.Success = true;
                Debug.Log(string.Format(
                    "{0} task={1}, report={2}",
                    SuccessPattern,
                    report.TaskId ?? string.Empty,
                    Path.GetFullPath(reportPath)));
            }
            catch (Exception exception)
            {
                report.Success = false;
                report.ErrorMessage = exception.Message;
                report.Errors.Add(exception.Message);
                Debug.LogError(exception);
                throw;
            }
            finally
            {
                WriteReport(reportPath, report);
            }
        }

        private static void RunTaskAutoAnalysis(string taskDirectory, UiPrefabGeneratorAnalysisBatchReport report)
        {
            if (string.IsNullOrWhiteSpace(taskDirectory))
            {
                throw new InvalidOperationException("未指定任务目录。");
            }

            string normalizedTaskDirectory = taskDirectory.Replace('\\', '/');
            if (!UiGenerationTaskStorage.TryLoadTaskRequest(normalizedTaskDirectory, out var request) || request == null)
            {
                throw new InvalidOperationException("任务目录中缺少可读取的 request.json: " + normalizedTaskDirectory);
            }

            var service = new UiPrefabGeneratorAutoAnalysisService();
            UiPrefabGeneratorAutoAnalysisResult analysis = service.AnalyzeTask(normalizedTaskDirectory);

            report.TaskId = request.TaskId ?? string.Empty;
            report.TemplateName = analysis.AnalysisResult != null ? analysis.AnalysisResult.TemplateName ?? string.Empty : string.Empty;
            report.ProfileId = analysis.AnalysisResult != null ? analysis.AnalysisResult.ProfileId ?? string.Empty : string.Empty;
            report.AnalysisResultPath = normalizedTaskDirectory + "/" + UiGenerationDataPaths.AnalysisResultFileName;
            report.AnalysisSummaryPath = normalizedTaskDirectory + "/" + UiGenerationDataPaths.AnalysisSummaryFileName;
            report.Errors.AddRange(analysis.Errors);
            report.Warnings.AddRange(analysis.Warnings);

            if (!analysis.Success)
            {
                throw new InvalidOperationException(BuildFailureMessage(analysis));
            }

            SaveAnalysisArtifacts(normalizedTaskDirectory, analysis);
            report.AnalysisResultPath = normalizedTaskDirectory + "/" + UiGenerationDataPaths.AnalysisResultFileName;
            report.AnalysisSummaryPath = normalizedTaskDirectory + "/" + UiGenerationDataPaths.AnalysisSummaryFileName;
        }

        private static void SaveAnalysisArtifacts(string taskDirectory, UiPrefabGeneratorAutoAnalysisResult analysis)
        {
            if (analysis == null || analysis.AnalysisResult == null)
            {
                throw new InvalidOperationException("分析结果为空，无法回写。");
            }

            UiGenerationTaskStorage.SaveAnalysisArtifacts(taskDirectory, analysis.AnalysisResult);
            UiGenerationJsonFileUtility.SaveText(
                taskDirectory + "/" + UiGenerationDataPaths.AnalysisSummaryFileName,
                analysis.AnalysisSummaryMarkdown ?? string.Empty);
        }

        private static string BuildFailureMessage(UiPrefabGeneratorAutoAnalysisResult analysis)
        {
            var builder = new StringBuilder();
            builder.AppendLine("UiPrefabGenerator task auto analysis failed.");
            if (analysis != null)
            {
                if (analysis.Errors.Count > 0)
                {
                    builder.AppendLine("Errors:");
                    for (int i = 0; i < analysis.Errors.Count; i++)
                    {
                        builder.AppendLine("- " + analysis.Errors[i]);
                    }
                }

                if (analysis.Warnings.Count > 0)
                {
                    builder.AppendLine("Warnings:");
                    for (int i = 0; i < analysis.Warnings.Count; i++)
                    {
                        builder.AppendLine("- " + analysis.Warnings[i]);
                    }
                }
            }

            return builder.ToString().Trim();
        }

        private static void WriteReport(string reportPath, UiPrefabGeneratorAnalysisBatchReport report)
        {
            if (string.IsNullOrWhiteSpace(reportPath))
            {
                return;
            }

            string normalizedReportPath = Path.GetFullPath(reportPath);
            string directory = Path.GetDirectoryName(normalizedReportPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(normalizedReportPath, JsonUtility.ToJson(report, true), new UTF8Encoding(false));
        }
    }

    [Serializable]
    internal sealed class UiPrefabGeneratorAnalysisBatchReport
    {
        public string TaskDirectory = string.Empty;
        public string TaskId = string.Empty;
        public string TemplateName = string.Empty;
        public string ProfileId = string.Empty;
        public string AnalysisResultPath = string.Empty;
        public string AnalysisSummaryPath = string.Empty;
        public bool Success;
        public string ErrorMessage = string.Empty;
        public List<string> Warnings = new List<string>();
        public List<string> Errors = new List<string>();
    }
}
