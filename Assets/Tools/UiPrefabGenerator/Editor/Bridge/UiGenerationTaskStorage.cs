using System;
using System.Collections.Generic;
using System.IO;
using UiPrefabGenerator.Core.Intake;
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
        public const string VisualUnderstandingFileName = UiGenerationDataPaths.VisualUnderstandingFileName;
        public const string VisualReviewReportFileName = UiGenerationDataPaths.VisualReviewReportFileName;
        public const string DesignPacketFileName = UiGenerationDataPaths.DesignPacketFileName;
        public const string IntakeAssessmentFileName = UiGenerationDataPaths.IntakeAssessmentFileName;
        public const string GatingReportFileName = UiGenerationDataPaths.GatingReportFileName;
        public const string UiPrefabSpecFileName = UiGenerationDataPaths.UiPrefabSpecFileName;
        public const string ResourceMatchReportFileName = UiGenerationDataPaths.ResourceMatchReportFileName;
        public const string PreviewRenderPlanFileName = UiGenerationDataPaths.PreviewRenderPlanFileName;
        public const string PreviewRenderImageFileName = UiGenerationDataPaths.PreviewRenderImageFileName;
        public const string PreviewDiffReportFileName = UiGenerationDataPaths.PreviewDiffReportFileName;
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

                LoadOptionalReviewArtifacts(taskDirectory, loaded);
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
            LoadOptionalReviewArtifacts(taskDirectory, result);
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

        public static bool TryLoadExecutionResult(string taskDirectory, out UiGenerationExecutionResult result, out string error)
        {
            result = null;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(taskDirectory))
            {
                error = "任务目录为空。";
                return false;
            }

            string expectedTaskId = Path.GetFileName(taskDirectory) ?? string.Empty;
            if (!UiGenerationJsonFileUtility.TryLoadJson(
                    taskDirectory + "/" + UiGenerationDataPaths.ExecutionResultFileName,
                    out result) || result == null)
            {
                error = "未找到可读取的 generation_result.json。";
                return false;
            }

            if (!ValidateTaskId(expectedTaskId, result.TaskId, out error))
            {
                result = null;
                return false;
            }

            if (string.IsNullOrWhiteSpace(result.TaskId))
            {
                result.TaskId = expectedTaskId;
            }

            return true;
        }

        public static void SaveAnalysisArtifacts(
            string taskDirectory,
            UiGenerationAnalysisResult result,
            DesignPacketIntakeAssessment intakeAssessment = null)
        {
            if (string.IsNullOrWhiteSpace(taskDirectory) || result == null)
            {
                return;
            }

            UiGenerationJsonFileUtility.SaveJson(
                taskDirectory + "/" + UiGenerationDataPaths.AnalysisResultFileName,
                result);

            if (result.VisualUnderstanding != null)
            {
                UiGenerationJsonFileUtility.SaveJson(
                    taskDirectory + "/" + UiGenerationDataPaths.VisualUnderstandingFileName,
                    result.VisualUnderstanding);
            }

            if (result.VisualReviewReport != null)
            {
                UiGenerationJsonFileUtility.SaveJson(
                    taskDirectory + "/" + UiGenerationDataPaths.VisualReviewReportFileName,
                    result.VisualReviewReport);
            }

            if (result.DesignPacket != null)
            {
                UiGenerationJsonFileUtility.SaveJson(
                    taskDirectory + "/" + UiGenerationDataPaths.DesignPacketFileName,
                    result.DesignPacket);
            }

            if (intakeAssessment == null)
            {
                intakeAssessment = new DesignPacketIntakeAssessment
                {
                    SourcePacket = result.DesignPacket,
                    IntakeProfileId = result.GatingReport != null && !string.IsNullOrWhiteSpace(result.GatingReport.IntakeProfileId)
                        ? result.GatingReport.IntakeProfileId
                        : "design_packet_review_v1",
                };
            }

            UiGenerationJsonFileUtility.SaveJson(
                taskDirectory + "/" + UiGenerationDataPaths.IntakeAssessmentFileName,
                intakeAssessment);

            if (result.UiPrefabSpec != null)
            {
                UiGenerationJsonFileUtility.SaveJson(
                    taskDirectory + "/" + UiGenerationDataPaths.UiPrefabSpecFileName,
                    result.UiPrefabSpec);
            }

            UiGenerationGatingReport gatingReport = result.GatingReport ?? new UiGenerationGatingReport
            {
                TaskId = result.TaskId,
                IntakeProfileId = "design_packet_review_v1",
                Status = "ready",
            };
            UiGenerationJsonFileUtility.SaveJson(
                taskDirectory + "/" + UiGenerationDataPaths.GatingReportFileName,
                gatingReport);

            if (result.ResourceMatchReport != null)
            {
                UiGenerationJsonFileUtility.SaveJson(
                    taskDirectory + "/" + UiGenerationDataPaths.ResourceMatchReportFileName,
                    result.ResourceMatchReport);
            }

            if (result.PreviewRenderPlan != null)
            {
                UiGenerationJsonFileUtility.SaveJson(
                    taskDirectory + "/" + UiGenerationDataPaths.PreviewRenderPlanFileName,
                    result.PreviewRenderPlan);
            }

            if (result.PreviewDiffReport != null)
            {
                UiGenerationJsonFileUtility.SaveJson(
                    taskDirectory + "/" + UiGenerationDataPaths.PreviewDiffReportFileName,
                    result.PreviewDiffReport);
            }

            SavePreviewRenderImage(taskDirectory, result.PreviewRenderPlan);
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

        public static Texture2D LoadPreviewRenderTexture(string taskDirectory)
        {
            if (string.IsNullOrWhiteSpace(taskDirectory))
            {
                return null;
            }

            string assetPath = taskDirectory + "/" + UiGenerationDataPaths.PreviewRenderImageFileName;
            string absolutePath = UiGenerationDataPaths.ToAbsolutePath(assetPath);
            if (File.Exists(absolutePath))
            {
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                AssetDatabase.Refresh();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
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

        private static void LoadOptionalReviewArtifacts(string taskDirectory, UiGenerationAnalysisResult result)
        {
            if (string.IsNullOrWhiteSpace(taskDirectory) || result == null)
            {
                return;
            }

            if (ShouldLoadVisualUnderstanding(result.VisualUnderstanding))
            {
                UiGenerationJsonFileUtility.TryLoadJson(
                    taskDirectory + "/" + UiGenerationDataPaths.VisualUnderstandingFileName,
                    out result.VisualUnderstanding);
            }

            if (ShouldLoadVisualReviewReport(result.VisualReviewReport))
            {
                UiGenerationJsonFileUtility.TryLoadJson(
                    taskDirectory + "/" + UiGenerationDataPaths.VisualReviewReportFileName,
                    out result.VisualReviewReport);
            }

            if (ShouldLoadPreviewRenderPlan(result.PreviewRenderPlan))
            {
                UiGenerationJsonFileUtility.TryLoadJson(
                    taskDirectory + "/" + UiGenerationDataPaths.PreviewRenderPlanFileName,
                    out result.PreviewRenderPlan);
            }

            if (ShouldLoadPreviewDiffReport(result.PreviewDiffReport))
            {
                UiGenerationJsonFileUtility.TryLoadJson(
                    taskDirectory + "/" + UiGenerationDataPaths.PreviewDiffReportFileName,
                    out result.PreviewDiffReport);
            }

            if (ShouldLoadGatingReport(result.GatingReport))
            {
                UiGenerationJsonFileUtility.TryLoadJson(
                    taskDirectory + "/" + UiGenerationDataPaths.GatingReportFileName,
                    out result.GatingReport);
            }

            LoadOptionalReviewIssues(taskDirectory, result);
        }

        private static bool ShouldLoadVisualUnderstanding(VisualUnderstandingBundle bundle)
        {
            return bundle == null ||
                   (string.IsNullOrWhiteSpace(bundle.TaskId) &&
                    string.IsNullOrWhiteSpace(bundle.ProviderId) &&
                    (bundle.Elements == null || bundle.Elements.Count == 0));
        }

        private static bool ShouldLoadVisualReviewReport(VisualReviewReport report)
        {
            return report == null ||
                   (string.IsNullOrWhiteSpace(report.TaskId) &&
                    string.IsNullOrWhiteSpace(report.ReviewStatus) &&
                    (report.Issues == null || report.Issues.Count == 0));
        }

        private static bool ShouldLoadPreviewRenderPlan(PreviewRenderPlan plan)
        {
            return plan == null ||
                   (string.IsNullOrWhiteSpace(plan.TaskId) &&
                    string.IsNullOrWhiteSpace(plan.PlanVersion) &&
                    (plan.Nodes == null || plan.Nodes.Count == 0));
        }

        private static bool ShouldLoadPreviewDiffReport(PreviewDiffReport report)
        {
            return report == null ||
                   (string.IsNullOrWhiteSpace(report.TaskId) &&
                    (report.Regions == null || report.Regions.Count == 0));
        }

        private static bool ShouldLoadGatingReport(UiGenerationGatingReport report)
        {
            return report == null ||
                   (string.IsNullOrWhiteSpace(report.TaskId) &&
                    string.IsNullOrWhiteSpace(report.Status) &&
                    (report.Issues == null || report.Issues.Count == 0));
        }

        private static void LoadOptionalReviewIssues(string taskDirectory, UiGenerationAnalysisResult result)
        {
            if (result == null || string.IsNullOrWhiteSpace(taskDirectory))
            {
                return;
            }

            if (result.ReviewIssues == null)
            {
                result.ReviewIssues = new List<UiGenerationReviewIssue>();
            }

            if (result.ReviewIssues.Count == 0)
            {
                AppendReviewIssuesFromVisualReview(result.ReviewIssues, result.VisualReviewReport);

                DesignPacketIntakeAssessment intakeAssessment;
                if (UiGenerationJsonFileUtility.TryLoadJson(
                        taskDirectory + "/" + UiGenerationDataPaths.IntakeAssessmentFileName,
                        out intakeAssessment) &&
                    intakeAssessment != null)
                {
                    AppendReviewIssuesFromIntakeAssessment(result.ReviewIssues, intakeAssessment);
                }
            }
        }

        private static void AppendReviewIssuesFromVisualReview(
            List<UiGenerationReviewIssue> reviewIssues,
            VisualReviewReport report)
        {
            if (reviewIssues == null || report == null || report.Issues == null)
            {
                return;
            }

            for (int i = 0; i < report.Issues.Count; i++)
            {
                VisualReviewIssue issue = report.Issues[i];
                if (issue == null)
                {
                    continue;
                }

                reviewIssues.Add(new UiGenerationReviewIssue
                {
                    IssueId = issue.IssueId ?? string.Empty,
                    Source = "evidence",
                    IssueKind = issue.Kind ?? string.Empty,
                    Severity = ParseReviewSeverity(issue.Severity),
                    Summary = issue.Summary ?? string.Empty,
                    RelatedId = issue.RelatedElementId ?? string.Empty,
                    SuggestedResolution = issue.SuggestedAction ?? string.Empty,
                    RequiresHumanDecision = issue.RequiresHumanDecision,
                });
            }
        }

        private static void AppendReviewIssuesFromIntakeAssessment(
            List<UiGenerationReviewIssue> reviewIssues,
            DesignPacketIntakeAssessment assessment)
        {
            if (reviewIssues == null || assessment == null || assessment.UnresolvedItems == null)
            {
                return;
            }

            for (int i = 0; i < assessment.UnresolvedItems.Count; i++)
            {
                DesignPacketIntakeIssue issue = assessment.UnresolvedItems[i];
                if (issue == null)
                {
                    continue;
                }

                reviewIssues.Add(new UiGenerationReviewIssue
                {
                    IssueId = issue.IssueId ?? string.Empty,
                    Source = "intake",
                    IssueKind = issue.Kind.ToString(),
                    Severity = issue.Severity == DesignPacketIntakeIssueSeverity.Blocking
                        ? UiGenerationReviewSeverity.Blocking
                        : issue.Severity == DesignPacketIntakeIssueSeverity.Warning
                            ? UiGenerationReviewSeverity.Warning
                            : UiGenerationReviewSeverity.Info,
                    Summary = issue.Summary ?? string.Empty,
                    Details = issue.Details ?? string.Empty,
                    RelatedId = issue.FieldPath ?? string.Empty,
                    SuggestedResolution = issue.SuggestedResolution ?? string.Empty,
                    RequiresHumanDecision = issue.RequiresHumanDecision,
                });
            }
        }

        private static UiGenerationReviewSeverity ParseReviewSeverity(string severity)
        {
            if (string.Equals(severity, "blocking", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(severity, "error", StringComparison.OrdinalIgnoreCase))
            {
                return UiGenerationReviewSeverity.Blocking;
            }

            if (string.Equals(severity, "warning", StringComparison.OrdinalIgnoreCase))
            {
                return UiGenerationReviewSeverity.Warning;
            }

            return UiGenerationReviewSeverity.Info;
        }

        private static void SavePreviewRenderImage(string taskDirectory, PreviewRenderPlan plan)
        {
            if (string.IsNullOrWhiteSpace(taskDirectory) || plan == null)
            {
                return;
            }

            ResolvePreviewCanvasSize(plan, out int width, out int height);
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                FillTexture(texture, Color.white);

                if (plan.Nodes != null)
                {
                    for (int i = 0; i < plan.Nodes.Count; i++)
                    {
                        DrawPreviewNode(texture, plan.Nodes[i]);
                    }
                }

                texture.Apply();
                string previewAssetPath = taskDirectory + "/" + UiGenerationDataPaths.PreviewRenderImageFileName;
                string absolutePath = UiGenerationDataPaths.ToAbsolutePath(previewAssetPath);
                string directory = Path.GetDirectoryName(absolutePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
                AssetDatabase.ImportAsset(previewAssetPath, ImportAssetOptions.ForceUpdate);
                AssetDatabase.Refresh();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static void ResolvePreviewCanvasSize(PreviewRenderPlan plan, out int width, out int height)
        {
            width = Mathf.Max(240, plan != null ? plan.CanvasWidth : 0);
            height = Mathf.Max(320, plan != null ? plan.CanvasHeight : 0);

            int maxDimension = Mathf.Max(width, height);
            if (maxDimension <= 720)
            {
                return;
            }

            float scale = 720f / maxDimension;
            width = Mathf.Max(240, Mathf.RoundToInt(width * scale));
            height = Mathf.Max(320, Mathf.RoundToInt(height * scale));
        }

        private static void FillTexture(Texture2D texture, Color color)
        {
            if (texture == null)
            {
                return;
            }

            Color[] pixels = new Color[texture.width * texture.height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }

            texture.SetPixels(pixels);
        }

        private static void DrawPreviewNode(Texture2D texture, PreviewRenderNode node)
        {
            if (texture == null || node == null || node.Bounds == null)
            {
                return;
            }

            int xMin = Mathf.Clamp(Mathf.RoundToInt(node.Bounds.X * texture.width), 0, texture.width - 1);
            int yMin = Mathf.Clamp(Mathf.RoundToInt((1f - node.Bounds.Y - node.Bounds.Height) * texture.height), 0, texture.height - 1);
            int width = Mathf.Clamp(Mathf.RoundToInt(node.Bounds.Width * texture.width), 1, texture.width - xMin);
            int height = Mathf.Clamp(Mathf.RoundToInt(node.Bounds.Height * texture.height), 1, texture.height - yMin);
            Color fillColor = ParseColor(node.FillColor, ResolveFallbackFillColor(node.NodeType));
            Color strokeColor = ParseColor(node.StrokeColor, Color.black);

            for (int y = yMin; y < yMin + height; y++)
            {
                for (int x = xMin; x < xMin + width; x++)
                {
                    bool isBorder = x == xMin || x == xMin + width - 1 || y == yMin || y == yMin + height - 1;
                    texture.SetPixel(x, y, isBorder ? strokeColor : fillColor);
                }
            }
        }

        private static Color ResolveFallbackFillColor(string nodeType)
        {
            if (string.Equals(nodeType, "button", StringComparison.OrdinalIgnoreCase))
            {
                return new Color(0.27f, 0.56f, 0.88f, 1f);
            }

            if (string.Equals(nodeType, "text", StringComparison.OrdinalIgnoreCase))
            {
                return new Color(0.9f, 0.9f, 0.9f, 1f);
            }

            if (string.Equals(nodeType, "number", StringComparison.OrdinalIgnoreCase))
            {
                return new Color(0.98f, 0.86f, 0.38f, 1f);
            }

            return new Color(0.82f, 0.86f, 0.9f, 1f);
        }

        private static Color ParseColor(string colorHex, Color fallback)
        {
            if (!string.IsNullOrWhiteSpace(colorHex) && ColorUtility.TryParseHtmlString(colorHex, out Color parsed))
            {
                return parsed;
            }

            return fallback;
        }
    }
}
