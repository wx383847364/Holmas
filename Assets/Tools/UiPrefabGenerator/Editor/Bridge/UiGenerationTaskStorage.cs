using System;
using System.Collections.Generic;
using System.IO;
using UiPrefabGenerator.Core.Profile;
using UiPrefabGenerator.Core.Request;
using UiPrefabGenerator.Core.ResourceMatch;
using UiPrefabGenerator.Core.Result;
using UiPrefabGenerator.Core.Schema;
using UiPrefabGenerator.Editor.Template;
using UiPrefabGenerator.Editor.Validation;
using UnityEditor;
using UnityEngine;

namespace UiPrefabGenerator.Editor.Bridge
{
    public static class UiGenerationTaskStorage
    {
        public const string RequestFileName = UiGenerationDataPaths.RequestFileName;
        public const string AnalysisResultFileName = UiGenerationDataPaths.AnalysisResultFileName;
        public const string DesignPacketFileName = UiGenerationDataPaths.DesignPacketFileName;
        public const string UiPrefabSpecFileName = UiGenerationDataPaths.UiPrefabSpecFileName;
        public const string ResourceMatchReportFileName = UiGenerationDataPaths.ResourceMatchReportFileName;
        public const string AnalysisSummaryFileName = UiGenerationDataPaths.AnalysisSummaryFileName;
        public const string ManifestFileName = UiGenerationDataPaths.ManifestFileName;
        public const string GenerationResultFileName = UiGenerationDataPaths.ExecutionResultFileName;

        public static string CreateTask(UiGenerationTaskRequest request, Texture2D sourceImage)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            UiGenerationDataPaths.EnsureDataFolders();
            string taskDirectory = UiGenerationDataPaths.GetTaskDirectory(request.TaskId);
            UiGenerationDataPaths.EnsureFolderExists(taskDirectory);

            if (sourceImage != null)
            {
                string sourceAssetPath = AssetDatabase.GetAssetPath(sourceImage);
                if (!string.IsNullOrWhiteSpace(sourceAssetPath))
                {
                    request.SourceImageAssetPath = sourceAssetPath;
                    request.SourceImageTaskAssetPath = CopySourceImage(taskDirectory, sourceAssetPath);
                }
            }

            UiGenerationJsonFileUtility.SaveJson(taskDirectory + "/" + UiGenerationDataPaths.RequestFileName, request);
            return taskDirectory;
        }

        public static string CreateRequestTask(UiGenerationTaskRequest request)
        {
            Texture2D sourceImage = null;
            if (request != null && !string.IsNullOrWhiteSpace(request.SourceImageAssetPath))
            {
                sourceImage = AssetDatabase.LoadAssetAtPath<Texture2D>(request.SourceImageAssetPath);
            }

            return CreateTask(request, sourceImage);
        }

        public static string CreateTaskDirectory(string taskId)
        {
            UiGenerationDataPaths.EnsureDataFolders();
            string taskDirectory = UiGenerationDataPaths.GetTaskDirectory(taskId);
            UiGenerationDataPaths.EnsureFolderExists(taskDirectory);
            return taskDirectory;
        }

        public static UiGenerationTaskRequest BuildRequest(
            UiGenerationTemplate template,
            string pageId,
            string pageTitle,
            string prefabName,
            string notes)
        {
            UiGenerationTemplate safeTemplate = template ?? UiGenerationTemplateStore.BuildPortraitWechatDefault();
            return new UiGenerationTaskRequest
            {
                TaskId = DateTime.UtcNow.ToString("yyyyMMdd_HHmmssfff"),
                CreatedAtUtc = DateTime.UtcNow.ToString("O"),
                TemplateName = safeTemplate.TemplateName ?? string.Empty,
                ProfileId = safeTemplate.ProfileId ?? string.Empty,
                TargetPlatform = safeTemplate.TargetPlatform ?? string.Empty,
                PageId = pageId ?? string.Empty,
                PageTitle = pageTitle ?? string.Empty,
                PrefabName = prefabName ?? string.Empty,
                PageType = safeTemplate.PageType ?? string.Empty,
                Orientation = safeTemplate.Orientation ?? string.Empty,
                ReferenceResolutionWidth = safeTemplate.ReferenceResolutionWidth,
                ReferenceResolutionHeight = safeTemplate.ReferenceResolutionHeight,
                AssetRoot = safeTemplate.AssetRoot ?? string.Empty,
                DraftPrefabRoot = safeTemplate.DraftPrefabRoot ?? string.Empty,
                Notes = notes ?? string.Empty,
                MustHaveNodes = new List<string>(safeTemplate.MustHaveNodes ?? new List<string>()),
                MustHaveInteractions = new List<string>(safeTemplate.MustHaveInteractions ?? new List<string>())
            };
        }

        public static bool TryLoadAnalysisResult(string taskDirectory, out UiGenerationAnalysisResult result, out string error)
        {
            result = null;
            error = string.Empty;
            string expectedTaskId = Path.GetFileName(taskDirectory) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(taskDirectory))
            {
                error = "任务目录为空。";
                return false;
            }

            string analysisResultPath = taskDirectory + "/" + UiGenerationDataPaths.AnalysisResultFileName;
            UiGenerationAnalysisResult loaded;
            if (UiGenerationJsonFileUtility.TryLoadJson(analysisResultPath, out loaded) && loaded != null)
            {
                if (!ValidateTaskId(expectedTaskId, loaded.TaskId, out error))
                {
                    return false;
                }

                if (string.IsNullOrWhiteSpace(loaded.TaskId))
                {
                    loaded.TaskId = expectedTaskId;
                }

                result = loaded;
                return true;
            }

            string designPacketPath = taskDirectory + "/" + UiGenerationDataPaths.DesignPacketFileName;
            string specPath = taskDirectory + "/" + UiGenerationDataPaths.UiPrefabSpecFileName;
            string matchPath = taskDirectory + "/" + UiGenerationDataPaths.ResourceMatchReportFileName;

            DesignPacket designPacket;
            UiPrefabSpec spec;
            if (!UiGenerationJsonFileUtility.TryLoadJson(designPacketPath, out designPacket) || designPacket == null)
            {
                error = "未找到 analysis_result.json，也未找到可读取的 design_packet.json。";
                return false;
            }

            if (!UiGenerationJsonFileUtility.TryLoadJson(specPath, out spec) || spec == null)
            {
                error = "未找到可读取的 ui_prefab_spec.json。";
                return false;
            }

            UiResourceMatchReport matchReport;
            if (!UiGenerationJsonFileUtility.TryLoadJson(matchPath, out matchReport) || matchReport == null)
            {
                matchReport = new UiResourceMatchReport();
            }
            else if (!ValidateTaskId(expectedTaskId, matchReport.TaskId, out error))
            {
                return false;
            }

            result = new UiGenerationAnalysisResult
            {
                TaskId = expectedTaskId,
                Success = true,
                DesignPacket = designPacket,
                UiPrefabSpec = spec,
                ResourceMatchReport = matchReport,
            };
            result.ResourceMatchReport.TaskId = expectedTaskId;
            return true;
        }

        public static bool TryLoadTaskRequest(string taskDirectory, out UiGenerationTaskRequest request)
        {
            request = null;
            if (string.IsNullOrWhiteSpace(taskDirectory))
            {
                return false;
            }

            return UiGenerationJsonFileUtility.TryLoadJson(
                taskDirectory + "/" + UiGenerationDataPaths.RequestFileName,
                out request) && request != null;
        }

        public static void SaveAnalysisArtifacts(string taskDirectory, UiGenerationAnalysisResult result)
        {
            if (string.IsNullOrWhiteSpace(taskDirectory) || result == null)
            {
                return;
            }

            UiGenerationJsonFileUtility.SaveJson(
                taskDirectory + "/" + UiGenerationDataPaths.AnalysisResultFileName,
                result);

            if (result.DesignPacket != null)
            {
                UiGenerationJsonFileUtility.SaveJson(
                    taskDirectory + "/" + UiGenerationDataPaths.DesignPacketFileName,
                    result.DesignPacket);
            }

            if (result.UiPrefabSpec != null)
            {
                UiGenerationJsonFileUtility.SaveJson(
                    taskDirectory + "/" + UiGenerationDataPaths.UiPrefabSpecFileName,
                    result.UiPrefabSpec);
            }

            if (result.ResourceMatchReport != null)
            {
                UiGenerationJsonFileUtility.SaveJson(
                    taskDirectory + "/" + UiGenerationDataPaths.ResourceMatchReportFileName,
                    result.ResourceMatchReport);
            }
        }

        public static void SaveExecutionArtifacts(
            string taskDirectory,
            PrefabBindingManifest manifest,
            UiGenerationExecutionResult result)
        {
            if (manifest != null)
            {
                UiGenerationJsonFileUtility.SaveText(
                    taskDirectory + "/" + UiGenerationDataPaths.ManifestFileName,
                    PrefabBindingManifestFixtureSerializer.Serialize(manifest));
            }

            if (result != null)
            {
                UiGenerationJsonFileUtility.SaveJson(
                    taskDirectory + "/" + UiGenerationDataPaths.ExecutionResultFileName,
                    result);
            }
        }

        public static string LoadAnalysisSummary(string taskDirectory)
        {
            return UiGenerationJsonFileUtility.LoadText(taskDirectory + "/" + UiGenerationDataPaths.AnalysisSummaryFileName);
        }

        public static string GetRequestPath(string taskDirectory)
        {
            return taskDirectory + "/" + UiGenerationDataPaths.RequestFileName;
        }

        private static string CopySourceImage(string taskDirectory, string sourceAssetPath)
        {
            string extension = Path.GetExtension(sourceAssetPath);
            string destinationAssetPath = taskDirectory + "/source_image" + extension;
            string sourceAbsolutePath = UiGenerationDataPaths.ToAbsolutePath(sourceAssetPath);
            string destinationAbsolutePath = UiGenerationDataPaths.ToAbsolutePath(destinationAssetPath);
            string destinationDirectory = Path.GetDirectoryName(destinationAbsolutePath);
            if (!string.IsNullOrWhiteSpace(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            File.Copy(sourceAbsolutePath, destinationAbsolutePath, true);
            AssetDatabase.ImportAsset(destinationAssetPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();
            return destinationAssetPath;
        }

        private static bool ValidateTaskId(string expectedTaskId, string actualTaskId, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(actualTaskId))
            {
                return true;
            }

            if (string.Equals(expectedTaskId, actualTaskId, StringComparison.Ordinal))
            {
                return true;
            }

            error = "分析结果 task_id 与任务目录不一致。"
                + " 目录=" + expectedTaskId
                + " 文件=" + actualTaskId;
            return false;
        }
    }
}
