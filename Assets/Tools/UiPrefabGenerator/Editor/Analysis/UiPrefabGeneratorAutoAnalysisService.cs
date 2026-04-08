using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UiPrefabGenerator.Core.Intake;
using UiPrefabGenerator.Core.Interpretation;
using UiPrefabGenerator.Core.Profile;
using UiPrefabGenerator.Core.Request;
using UiPrefabGenerator.Core.ResourceMatch;
using UiPrefabGenerator.Core.Result;
using UiPrefabGenerator.Core.Schema;
using UiPrefabGenerator.Editor.Bridge;
using UiPrefabGenerator.Editor.Template;
using UnityEditor;
using UnityEngine;

namespace UiPrefabGenerator.Editor.Analysis
{
    public sealed class UiPrefabGeneratorAutoAnalysisService
    {
        private const string MockEvidenceProviderId = "local_mock_review_only_v1";
        private const float LowConfidenceThreshold = 0.7f;
        private const float SelectedCandidateThreshold = 0.6f;
        private const int MaxCandidateCountPerSlot = 5;
        private static readonly string[] DefaultSearchExtensions = { ".png", ".jpg", ".jpeg", ".prefab", ".asset" };

        public UiPrefabGeneratorAutoAnalysisResult AnalyzeTask(string taskDirectory)
        {
            var result = new UiPrefabGeneratorAutoAnalysisResult();
            if (string.IsNullOrWhiteSpace(taskDirectory))
            {
                result.Errors.Add("任务目录为空。");
                return result;
            }

            if (!UiGenerationTaskStorage.TryLoadTaskRequest(taskDirectory, out UiGenerationTaskRequest request) || request == null)
            {
                result.Errors.Add("未找到可读取的 request.json。");
                return result;
            }

            string expectedTaskId = Path.GetFileName(taskDirectory) ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(request.TaskId) &&
                !string.Equals(request.TaskId, expectedTaskId, StringComparison.Ordinal))
            {
                result.Errors.Add("request.json 中的 task_id 与任务目录不一致。");
                return result;
            }

            result.Request = request;
            result.Template = ResolveTemplate(request, result);
            result.VisualUnderstanding = BuildVisualUnderstanding(expectedTaskId, request, result.Template, result);
            List<string> expectedSemanticRoles = BuildExpectedSemanticRoles(request, result.VisualUnderstanding);
            result.DesignPacket = BuildDesignPacket(request, result.Template, result.VisualUnderstanding, expectedSemanticRoles, result);
            result.IntakeAssessment = AnalyzeIntake(result.DesignPacket, result);
            result.GatingReport = BuildGatingReport(expectedTaskId, result.IntakeAssessment);
            result.VisualReviewReport = BuildVisualReviewReport(expectedTaskId, result.VisualUnderstanding, result.IntakeAssessment, result);
            result.UiPrefabSpec = InterpretSpec(result.DesignPacket, request, result.Template, result);
            result.ResourceMatchReport = BuildResourceMatchReport(taskDirectory, request, result.Template, result.UiPrefabSpec, result);
            result.PreviewRenderPlan = BuildPreviewRenderPlan(expectedTaskId, request, result.VisualUnderstanding, result.UiPrefabSpec);
            result.PreviewDiffReport = BuildPreviewDiffReport(expectedTaskId, result.VisualReviewReport, result.PreviewRenderPlan);
            result.AnalysisResult = BuildAnalysisResult(
                expectedTaskId,
                request,
                result.Template,
                result.VisualUnderstanding,
                result.VisualReviewReport,
                result.GatingReport,
                result.PreviewRenderPlan,
                result.PreviewDiffReport,
                result.DesignPacket,
                result.UiPrefabSpec,
                result.ResourceMatchReport,
                result);
            result.AnalysisSummaryMarkdown = BuildSummaryMarkdown(taskDirectory, result);
            result.AnalysisResult.Success = result.Errors.Count == 0;
            result.Success = result.Errors.Count == 0;
            return result;
        }

        private static UiGenerationTemplate ResolveTemplate(UiGenerationTaskRequest request, UiPrefabGeneratorAutoAnalysisResult result)
        {
            string templateAssetPath = SelectTemplateAssetPath(request);
            if (string.IsNullOrWhiteSpace(templateAssetPath))
            {
                result.Warnings.Add("未找到明确的模板文件，已回退到竖屏默认模板。");
                return UiGenerationTemplateStore.BuildPortraitWechatDefault();
            }

            UiGenerationTemplate template = UiGenerationTemplateStore.LoadTemplate(templateAssetPath);
            if (template == null)
            {
                result.Errors.Add("无法加载模板: " + templateAssetPath);
                return UiGenerationTemplateStore.BuildPortraitWechatDefault();
            }

            if (!string.IsNullOrWhiteSpace(request.ProfileId) &&
                !string.IsNullOrWhiteSpace(template.ProfileId) &&
                !string.Equals(request.ProfileId, template.ProfileId, StringComparison.Ordinal))
            {
                result.Warnings.Add("request.ProfileId 与模板 ProfileId 不一致，当前以模板为准。");
            }

            return template;
        }

        private static string SelectTemplateAssetPath(UiGenerationTaskRequest request)
        {
            string requestedTemplateName = request != null ? request.TemplateName ?? string.Empty : string.Empty;
            string defaultTemplatePath = UiGenerationDataPaths.DefaultPortraitTemplatePath;
            string[] templatePaths = UiGenerationTemplateStore.GetTemplateAssetPaths();
            if (templatePaths == null || templatePaths.Length == 0)
            {
                return defaultTemplatePath;
            }

            if (!string.IsNullOrWhiteSpace(requestedTemplateName))
            {
                for (int i = 0; i < templatePaths.Length; i++)
                {
                    string templatePath = templatePaths[i];
                    string fileName = Path.GetFileNameWithoutExtension(templatePath) ?? string.Empty;
                    if (string.Equals(fileName, requestedTemplateName, StringComparison.Ordinal))
                    {
                        return templatePath;
                    }

                    UiGenerationTemplate template = UiGenerationTemplateStore.LoadTemplate(templatePath);
                    if (template != null &&
                        string.Equals(template.TemplateName ?? string.Empty, requestedTemplateName, StringComparison.Ordinal))
                    {
                        return templatePath;
                    }
                }
            }

            return templatePaths.Contains(defaultTemplatePath) ? defaultTemplatePath : templatePaths[0];
        }

        private static VisualUnderstandingBundle BuildVisualUnderstanding(
            string taskId,
            UiGenerationTaskRequest request,
            UiGenerationTemplate template,
            UiPrefabGeneratorAutoAnalysisResult result)
        {
            string sourceImagePath = ResolveSourceImagePath(request);
            ResolveCanvasSize(sourceImagePath, template, out int canvasWidth, out int canvasHeight);

            var bundle = new VisualUnderstandingBundle
            {
                TaskId = taskId ?? string.Empty,
                SourceImagePath = sourceImagePath,
                ProviderId = MockEvidenceProviderId,
                ProviderRunId = MockEvidenceProviderId + ":" + (taskId ?? string.Empty),
                CanvasWidth = canvasWidth,
                CanvasHeight = canvasHeight,
            };

            if (!string.IsNullOrWhiteSpace(sourceImagePath))
            {
                AddElement(bundle, CreateElement(
                    "panel_background",
                    "image",
                    "panel_background",
                    "PanelBackground",
                    0.98f,
                    CreateRect(0f, 0f, 1f, 1f),
                    string.Empty,
                    new VisualStyleEvidence
                    {
                        AssetSlotHint = "panel_bg",
                        LayoutHint = "full_screen",
                        ShapeHint = "panel",
                    }));
            }
            else
            {
                result.Warnings.Add("缺少 source image，视觉证据会退化为 request 线索。");
            }

            if (!string.IsNullOrWhiteSpace(request != null ? request.PageTitle : string.Empty))
            {
                AddElement(bundle, CreateElement(
                    "title_text",
                    "text",
                    "title_text",
                    "TitleText",
                    0.91f,
                    CreateRect(0.1f, 0.08f, 0.8f, 0.08f),
                    request.PageTitle,
                    new VisualStyleEvidence
                    {
                        LayoutHint = "top_header",
                    }));
            }

            if (ShouldAddPrimaryButton(request))
            {
                AddElement(bundle, CreateElement(
                    "primary_button",
                    "button",
                    "primary_button",
                    "PrimaryButton",
                    0.82f,
                    CreateRect(0.18f, 0.78f, 0.64f, 0.1f),
                    ResolvePrimaryButtonText(request),
                    new VisualStyleEvidence
                    {
                        PrimaryColor = "#4B8EF7",
                        ShapeHint = "rounded_rect",
                        AssetSlotHint = "primary_button_bg",
                        LayoutHint = "bottom_cta",
                    }));
            }

            if (ShouldAddNumericValue(request))
            {
                float confidence = ResolveNumericConfidence(request);
                AddElement(bundle, CreateElement(
                    "numeric_value_display",
                    "number",
                    "numeric_value_display",
                    "NumericValueDisplay",
                    confidence,
                    CreateRect(0.62f, 0.18f, 0.22f, 0.08f),
                    ResolveNumericText(request),
                    new VisualStyleEvidence
                    {
                        PrimaryColor = "#F7D95A",
                        LayoutHint = "top_right_metric",
                    },
                    true));
            }

            if (HasTaskListSignal(request))
            {
                AddElement(bundle, CreateElement(
                    "task_list",
                    "list",
                    "task_list",
                    "TaskList",
                    0.84f,
                    CreateRect(0.08f, 0.28f, 0.84f, 0.4f),
                    string.Empty,
                    new VisualStyleEvidence
                    {
                        LayoutHint = "scroll_list",
                    }));
            }

            PopulateConfidenceSummary(bundle, BuildExpectedSemanticRoles(request, bundle));
            return bundle;
        }

        private static DesignPacket BuildDesignPacket(
            UiGenerationTaskRequest request,
            UiGenerationTemplate template,
            VisualUnderstandingBundle visualUnderstanding,
            List<string> expectedSemanticRoles,
            UiPrefabGeneratorAutoAnalysisResult result)
        {
            var designPacket = new DesignPacket
            {
                PageId = request.PageId ?? string.Empty,
                PageTitle = request.PageTitle ?? string.Empty,
                PrefabName = request.PrefabName ?? string.Empty,
                Notes = BuildPacketNotes(request, template, visualUnderstanding),
            };

            string sourceImagePath = ResolveSourceImagePath(request);
            if (string.IsNullOrWhiteSpace(sourceImagePath))
            {
                result.Warnings.Add("request.json 中没有可用的 source image 路径。");
            }

            designPacket.DesignImages.Add(new DesignImageReference
            {
                ImageId = "source_image",
                ImagePath = sourceImagePath,
                StateId = "default",
            });
            designPacket.States.Add(new DesignStateDefinition
            {
                StateId = "default",
                Description = "default state",
            });

            foreach (string role in expectedSemanticRoles)
            {
                if (!designPacket.ExpectedSemanticRoles.Contains(role))
                {
                    designPacket.ExpectedSemanticRoles.Add(role);
                }
            }

            foreach (DesignRuleDefinition rule in BuildRules(request, visualUnderstanding, expectedSemanticRoles, result))
            {
                designPacket.Rules.Add(rule);
            }

            foreach (DesignElementHint hint in BuildElementHints(visualUnderstanding))
            {
                designPacket.ElementHints.Add(hint);
                if (!string.IsNullOrWhiteSpace(hint.AssetSlot) &&
                    !designPacket.AssetSlotHints.Exists(item => item != null && string.Equals(item.SlotId, hint.AssetSlot, StringComparison.Ordinal)))
                {
                    designPacket.AssetSlotHints.Add(new DesignAssetSlotHint
                    {
                        SlotId = hint.AssetSlot,
                        Usage = string.IsNullOrWhiteSpace(hint.LayoutSlot) ? hint.SemanticRole : hint.LayoutSlot,
                    });
                }
            }

            if (!string.IsNullOrWhiteSpace(sourceImagePath) &&
                !designPacket.AssetSlotHints.Exists(item => item != null && string.Equals(item.SlotId, "panel_bg", StringComparison.Ordinal)))
            {
                designPacket.AssetSlotHints.Add(new DesignAssetSlotHint
                {
                    SlotId = "panel_bg",
                    Usage = "background image from source design",
                });
            }

            return designPacket;
        }

        private static DesignPacketIntakeAssessment AnalyzeIntake(
            DesignPacket designPacket,
            UiPrefabGeneratorAutoAnalysisResult result)
        {
            try
            {
                return new DefaultDesignPacketIntakeAnalyzer().Analyze(designPacket);
            }
            catch (Exception exception)
            {
                result.Errors.Add("intake analyzer 执行失败: " + exception.Message);
                return new DesignPacketIntakeAssessment
                {
                    SourcePacket = designPacket,
                    IntakeProfileId = "design_packet_review_v1",
                };
            }
        }

        private static UiGenerationGatingReport BuildGatingReport(
            string taskId,
            DesignPacketIntakeAssessment intakeAssessment)
        {
            var report = new UiGenerationGatingReport
            {
                TaskId = taskId ?? string.Empty,
                IntakeProfileId = intakeAssessment != null ? intakeAssessment.IntakeProfileId ?? string.Empty : string.Empty,
                Status = "ready",
            };

            if (intakeAssessment == null)
            {
                return report;
            }

            if (intakeAssessment.Notes != null)
            {
                report.Notes.AddRange(intakeAssessment.Notes);
            }

            if (intakeAssessment.UnresolvedItems != null)
            {
                for (int i = 0; i < intakeAssessment.UnresolvedItems.Count; i++)
                {
                    DesignPacketIntakeIssue issue = intakeAssessment.UnresolvedItems[i];
                    if (issue == null)
                    {
                        continue;
                    }

                    report.Issues.Add(new UiGenerationGatingIssue
                    {
                        IssueId = string.IsNullOrWhiteSpace(issue.IssueId) ? "gating_issue_" + i : issue.IssueId,
                        Severity = issue.Severity.ToString(),
                        Kind = issue.Kind.ToString(),
                        FieldPath = issue.FieldPath ?? string.Empty,
                        Summary = issue.Summary ?? string.Empty,
                        Details = issue.Details ?? string.Empty,
                        SuggestedResolution = issue.SuggestedResolution ?? string.Empty,
                        RequiresHumanDecision = issue.RequiresHumanDecision,
                    });
                }
            }

            bool hasBlocking = report.Issues.Any(issue => issue != null && string.Equals(issue.Severity, "Blocking", StringComparison.Ordinal));
            report.Status = hasBlocking ? "blocking" : report.Issues.Count > 0 ? "needs_review" : "ready";
            return report;
        }

        private static VisualReviewReport BuildVisualReviewReport(
            string taskId,
            VisualUnderstandingBundle bundle,
            DesignPacketIntakeAssessment intakeAssessment,
            UiPrefabGeneratorAutoAnalysisResult result)
        {
            var report = new VisualReviewReport
            {
                TaskId = taskId ?? string.Empty,
            };

            if (bundle != null && bundle.Elements != null)
            {
                for (int i = 0; i < bundle.Elements.Count; i++)
                {
                    VisualElementEvidence element = bundle.Elements[i];
                    if (element == null || element.Confidence >= LowConfidenceThreshold)
                    {
                        continue;
                    }

                    report.LowConfidenceElementIds.Add(element.ElementId ?? string.Empty);
                    report.Issues.Add(new VisualReviewIssue
                    {
                        IssueId = "visual_low_confidence_" + (element.ElementId ?? i.ToString()),
                        Severity = "warning",
                        Kind = "low_confidence_evidence",
                        RelatedElementId = element.ElementId ?? string.Empty,
                        Summary = "元素识别置信度偏低: " + (element.DisplayName ?? element.SemanticRole ?? string.Empty),
                        SuggestedAction = "人工复核该元素语义和文本。",
                        RequiresHumanDecision = true,
                    });
                }

                if (bundle.ConfidenceSummary != null && bundle.ConfidenceSummary.BlockingReasons != null)
                {
                    for (int i = 0; i < bundle.ConfidenceSummary.BlockingReasons.Count; i++)
                    {
                        string blockingReason = bundle.ConfidenceSummary.BlockingReasons[i];
                        if (string.IsNullOrWhiteSpace(blockingReason))
                        {
                            continue;
                        }

                        string semanticRole = blockingReason.StartsWith("missing:", StringComparison.Ordinal)
                            ? blockingReason.Substring("missing:".Length)
                            : blockingReason;
                        report.MissingSemanticRoles.Add(semanticRole);
                        report.Issues.Add(new VisualReviewIssue
                        {
                            IssueId = "missing_semantic_role_" + semanticRole,
                            Severity = "warning",
                            Kind = "missing_critical_control",
                            Summary = "缺少关键语义角色: " + semanticRole,
                            SuggestedAction = "补充证据或人工确认该语义角色是否确实不存在。",
                            RequiresHumanDecision = true,
                        });
                    }
                }
            }

            if (intakeAssessment != null && intakeAssessment.UnresolvedItems != null)
            {
                for (int i = 0; i < intakeAssessment.UnresolvedItems.Count; i++)
                {
                    DesignPacketIntakeIssue issue = intakeAssessment.UnresolvedItems[i];
                    if (issue == null)
                    {
                        continue;
                    }

                    string severity = issue.Severity == DesignPacketIntakeIssueSeverity.Blocking ? "blocking"
                        : issue.Severity == DesignPacketIntakeIssueSeverity.Warning ? "warning"
                        : "info";
                    report.Issues.Add(new VisualReviewIssue
                    {
                        IssueId = string.IsNullOrWhiteSpace(issue.IssueId) ? "intake_issue_" + i : issue.IssueId,
                        Severity = severity,
                        Kind = issue.Kind.ToString(),
                        RelatedElementId = issue.RelatedSlotId ?? string.Empty,
                        Summary = issue.Summary ?? string.Empty,
                        SuggestedAction = issue.SuggestedResolution ?? string.Empty,
                        RequiresHumanDecision = issue.RequiresHumanDecision,
                    });

                    if (issue.Severity == DesignPacketIntakeIssueSeverity.Blocking)
                    {
                        result.Errors.Add("blocking_intake_issue:" + (issue.IssueId ?? i.ToString()));
                    }
                    else
                    {
                        result.UnresolvedItems.Add("intake_issue:" + (issue.IssueId ?? i.ToString()));
                    }
                }
            }

            report.BlockingIssueCount = report.Issues.Count(issue => issue != null && string.Equals(issue.Severity, "blocking", StringComparison.OrdinalIgnoreCase));
            report.WarningIssueCount = report.Issues.Count(issue => issue != null && string.Equals(issue.Severity, "warning", StringComparison.OrdinalIgnoreCase));
            report.ReviewStatus = report.BlockingIssueCount > 0 ? "blocking" : report.WarningIssueCount > 0 ? "needs_review" : "clean";
            report.Notes.Add("review_only=true");
            return report;
        }

        private static UiPrefabSpec InterpretSpec(
            DesignPacket designPacket,
            UiGenerationTaskRequest request,
            UiGenerationTemplate template,
            UiPrefabGeneratorAutoAnalysisResult result)
        {
            try
            {
                string generationProfileId = !string.IsNullOrWhiteSpace(request != null ? request.ProfileId : string.Empty)
                    ? request.ProfileId
                    : template != null ? template.ProfileId ?? string.Empty : string.Empty;
                var interpreter = new DefaultDesignPacketToUiPrefabSpecInterpreter(null, generationProfileId);
                UiPrefabSpec spec = interpreter.Interpret(designPacket);
                if (spec == null)
                {
                    result.Errors.Add("UiPrefabSpec 解释结果为空。");
                    return new UiPrefabSpec();
                }

                return spec;
            }
            catch (Exception exception)
            {
                result.Errors.Add(exception.Message);
                return new UiPrefabSpec();
            }
        }

        private static UiResourceMatchReport BuildResourceMatchReport(
            string taskDirectory,
            UiGenerationTaskRequest request,
            UiGenerationTemplate template,
            UiPrefabSpec spec,
            UiPrefabGeneratorAutoAnalysisResult result)
        {
            var report = new UiResourceMatchReport
            {
                TaskId = Path.GetFileName(taskDirectory) ?? string.Empty,
                AssetRoot = ResolveAssetRoot(request, template),
            };

            if (spec == null || spec.Nodes == null)
            {
                result.Warnings.Add("UiPrefabSpec 为空，资源匹配将跳过。");
                return report;
            }

            string absoluteAssetRoot = UiGenerationDataPaths.ToAbsolutePath(report.AssetRoot);
            List<string> candidateAssets = new List<string>();
            if (!Directory.Exists(absoluteAssetRoot))
            {
                result.Warnings.Add("资源根目录不存在: " + report.AssetRoot);
            }
            else
            {
                candidateAssets = CollectCandidateAssets(absoluteAssetRoot, template);
                if (candidateAssets.Count == 0)
                {
                    result.Warnings.Add("资源根目录下没有可用于匹配的资源文件。");
                }
            }

            Dictionary<string, PrefabBindingSlotContext> slotContexts = CollectSlotContexts(spec);
            foreach (KeyValuePair<string, PrefabBindingSlotContext> pair in slotContexts)
            {
                UiAssetSlotMatch match = BuildSlotMatch(pair.Key, pair.Value, candidateAssets, request, template, result);
                report.Matches.Add(match);
                if (string.IsNullOrWhiteSpace(match.SelectedAssetPath))
                {
                    report.UnresolvedSlots.Add(match.AssetSlot);
                }
            }

            return report;
        }

        private static string ResolveAssetRoot(UiGenerationTaskRequest request, UiGenerationTemplate template)
        {
            if (!string.IsNullOrWhiteSpace(request != null ? request.AssetRoot : string.Empty))
            {
                return request.AssetRoot;
            }

            if (!string.IsNullOrWhiteSpace(template != null ? template.AssetRoot : string.Empty))
            {
                return template.AssetRoot;
            }

            return "Assets";
        }

        private static List<string> CollectCandidateAssets(string absoluteAssetRoot, UiGenerationTemplate template)
        {
            var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            IEnumerable<string> searchExtensions = template != null && template.ResourceSearchExtensions != null && template.ResourceSearchExtensions.Count > 0
                ? template.ResourceSearchExtensions
                : DefaultSearchExtensions;

            foreach (string extension in searchExtensions)
            {
                if (!string.IsNullOrWhiteSpace(extension))
                {
                    allowedExtensions.Add(extension.StartsWith(".", StringComparison.Ordinal) ? extension : "." + extension);
                }
            }

            var assets = new List<string>();
            foreach (string absolutePath in Directory.GetFiles(absoluteAssetRoot, "*", SearchOption.AllDirectories))
            {
                string extension = Path.GetExtension(absolutePath);
                if (string.Equals(extension, ".meta", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (allowedExtensions.Count > 0 && !allowedExtensions.Contains(extension))
                {
                    continue;
                }

                string assetPath = UiGenerationDataPaths.ToAssetPath(absolutePath);
                if (!string.IsNullOrWhiteSpace(assetPath))
                {
                    assets.Add(assetPath);
                }
            }

            assets.Sort(StringComparer.Ordinal);
            return assets;
        }

        private static Dictionary<string, PrefabBindingSlotContext> CollectSlotContexts(UiPrefabSpec spec)
        {
            var contexts = new Dictionary<string, PrefabBindingSlotContext>(StringComparer.Ordinal);
            if (spec == null || spec.Nodes == null)
            {
                return contexts;
            }

            for (int i = 0; i < spec.Nodes.Count; i++)
            {
                UiNodeSpec node = spec.Nodes[i];
                if (node == null || node.Components == null)
                {
                    continue;
                }

                for (int j = 0; j < node.Components.Count; j++)
                {
                    UiComponentSpec component = node.Components[j];
                    if (component == null || string.IsNullOrWhiteSpace(component.AssetSlot))
                    {
                        continue;
                    }

                    if (!contexts.TryGetValue(component.AssetSlot, out PrefabBindingSlotContext context))
                    {
                        context = new PrefabBindingSlotContext();
                        contexts[component.AssetSlot] = context;
                    }

                    context.ComponentType = string.IsNullOrWhiteSpace(context.ComponentType) ? component.ComponentType ?? string.Empty : context.ComponentType;
                    context.Usage = string.IsNullOrWhiteSpace(context.Usage) && node.Layout != null ? node.Layout.LayoutSlot ?? string.Empty : context.Usage;
                    context.NodeIds.Add(node.NodeId ?? string.Empty);
                }
            }

            return contexts;
        }

        private static UiAssetSlotMatch BuildSlotMatch(
            string slotId,
            PrefabBindingSlotContext context,
            List<string> candidateAssets,
            UiGenerationTaskRequest request,
            UiGenerationTemplate template,
            UiPrefabGeneratorAutoAnalysisResult result)
        {
            var match = new UiAssetSlotMatch
            {
                AssetSlot = slotId ?? string.Empty,
                ComponentType = context != null ? context.ComponentType ?? string.Empty : string.Empty,
            };

            var candidates = new List<UiAssetCandidate>();
            for (int i = 0; i < candidateAssets.Count; i++)
            {
                string assetPath = candidateAssets[i];
                float score = ScoreCandidate(slotId, context, assetPath, request, template);
                if (score <= 0f)
                {
                    continue;
                }

                candidates.Add(new UiAssetCandidate
                {
                    AssetPath = assetPath,
                    AssetType = ResolveAssetType(assetPath, match.ComponentType),
                    Score = score,
                    Reason = BuildCandidateReason(slotId, context, assetPath, score),
                });
            }

            candidates = candidates
                .OrderByDescending(candidate => candidate.Score)
                .ThenBy(candidate => candidate.AssetPath, StringComparer.Ordinal)
                .Take(MaxCandidateCountPerSlot)
                .ToList();

            if (candidates.Count > 0)
            {
                candidates[0].Recommended = candidates[0].Score >= SelectedCandidateThreshold;
                match.Confidence = candidates[0].Score;
                match.SelectedAssetPath = candidates[0].Score >= SelectedCandidateThreshold ? candidates[0].AssetPath : string.Empty;
                match.SelectedAssetType = candidates[0].AssetType;
                match.Notes = string.IsNullOrWhiteSpace(match.SelectedAssetPath) ? "top candidate below selection threshold" : "auto-selected";
            }

            match.Candidates = candidates;
            if (string.IsNullOrWhiteSpace(match.SelectedAssetPath))
            {
                result.UnresolvedItems.Add("resource_slot:" + slotId);
            }

            return match;
        }

        private static float ScoreCandidate(
            string slotId,
            PrefabBindingSlotContext context,
            string assetPath,
            UiGenerationTaskRequest request,
            UiGenerationTemplate template)
        {
            string normalizedSlot = NormalizeText(slotId);
            string normalizedUsage = NormalizeText(context != null ? context.Usage : string.Empty);
            string normalizedComponent = NormalizeText(context != null ? context.ComponentType : string.Empty);
            string normalizedPage = NormalizeText(request != null ? request.PageId : string.Empty);
            string normalizedTitle = NormalizeText(request != null ? request.PageTitle : string.Empty);
            string normalizedPrefab = NormalizeText(request != null ? request.PrefabName : string.Empty);
            string normalizedAsset = NormalizeText(Path.GetFileNameWithoutExtension(assetPath) ?? string.Empty);
            string normalizedAssetPath = NormalizeText(assetPath);

            List<string> slotTokens = Tokenize(normalizedSlot)
                .Concat(Tokenize(normalizedUsage))
                .Concat(Tokenize(normalizedComponent))
                .Concat(Tokenize(normalizedPage))
                .Concat(Tokenize(normalizedTitle))
                .Concat(Tokenize(normalizedPrefab))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            List<string> assetTokens = Tokenize(normalizedAsset)
                .Concat(Tokenize(normalizedAssetPath))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (slotTokens.Count == 0 || assetTokens.Count == 0)
            {
                return 0f;
            }

            int overlap = slotTokens.Count(token => assetTokens.Contains(token));
            float score = (float)overlap / slotTokens.Count;
            if (!string.IsNullOrWhiteSpace(normalizedAsset) && !string.IsNullOrWhiteSpace(normalizedSlot) && normalizedAsset.Contains(normalizedSlot))
            {
                score += 0.3f;
            }

            if (!string.IsNullOrWhiteSpace(normalizedAsset) && !string.IsNullOrWhiteSpace(normalizedUsage) && normalizedAsset.Contains(normalizedUsage))
            {
                score += 0.1f;
            }

            if (IsImageComponent(context != null ? context.ComponentType : string.Empty) && IsImageAsset(assetPath))
            {
                score += 0.1f;
            }

            if (template != null && !string.IsNullOrWhiteSpace(template.PageType) && normalizedAssetPath.Contains(NormalizeText(template.PageType)))
            {
                score += 0.05f;
            }

            return Mathf.Clamp01(score);
        }

        private static PreviewRenderPlan BuildPreviewRenderPlan(
            string taskId,
            UiGenerationTaskRequest request,
            VisualUnderstandingBundle bundle,
            UiPrefabSpec spec)
        {
            var plan = new PreviewRenderPlan
            {
                TaskId = taskId ?? string.Empty,
                PlanVersion = "structured_preview_v1",
                SourceImagePath = ResolveSourceImagePath(request),
                CanvasWidth = bundle != null && bundle.CanvasWidth > 0 ? bundle.CanvasWidth : 1080,
                CanvasHeight = bundle != null && bundle.CanvasHeight > 0 ? bundle.CanvasHeight : 1920,
            };

            if (bundle != null && bundle.Elements != null)
            {
                for (int i = 0; i < bundle.Elements.Count; i++)
                {
                    VisualElementEvidence element = bundle.Elements[i];
                    if (element == null || string.Equals(element.SemanticRole, "root_canvas", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    plan.Nodes.Add(new PreviewRenderNode
                    {
                        NodeId = element.ElementId ?? string.Empty,
                        NodeType = string.IsNullOrWhiteSpace(element.SemanticRole) ? element.ElementType ?? string.Empty : element.SemanticRole,
                        Bounds = element.EvidenceBounds ?? CreateRect(0.1f, 0.1f, 0.8f, 0.08f),
                        Text = element.Text != null ? element.Text.NormalizedText ?? string.Empty : string.Empty,
                        FillColor = ResolvePreviewFillColor(element.SemanticRole),
                        StrokeColor = element.Confidence < LowConfidenceThreshold ? "#D9485F" : "#1F6FEB",
                        AssetSlot = element.Style != null ? element.Style.AssetSlotHint ?? string.Empty : string.Empty,
                        ZOrder = element.ZOrder,
                    });
                }
            }

            if (spec != null && spec.Nodes != null)
            {
                for (int i = 0; i < spec.Nodes.Count; i++)
                {
                    UiNodeSpec node = spec.Nodes[i];
                    if (node == null || string.IsNullOrWhiteSpace(node.NodeId))
                    {
                        continue;
                    }

                    bool exists = plan.Nodes.Exists(item => item != null && string.Equals(item.NodeId, node.NodeId, StringComparison.Ordinal));
                    if (!exists)
                    {
                        plan.Nodes.Add(new PreviewRenderNode
                        {
                            NodeId = node.NodeId,
                            NodeType = "spec_only",
                            Bounds = CreateRect(0.1f, 0.1f + (i * 0.08f), 0.8f, 0.06f),
                            Text = node.NodeName ?? string.Empty,
                            FillColor = "#C2E7FF",
                            StrokeColor = "#1F6FEB",
                            AssetSlot = ResolveFirstAssetSlot(node),
                            ZOrder = i,
                        });
                    }
                }
            }

            plan.Notes.Add("review_only=true");
            return plan;
        }

        private static PreviewDiffReport BuildPreviewDiffReport(
            string taskId,
            VisualReviewReport reviewReport,
            PreviewRenderPlan plan)
        {
            var report = new PreviewDiffReport
            {
                TaskId = taskId ?? string.Empty,
                CoverageScore = CalculateCoverageScore(plan),
                LayoutSimilarityScore = CalculateLayoutSimilarityScore(plan),
            };

            if (reviewReport != null && reviewReport.Issues != null)
            {
                for (int i = 0; i < reviewReport.Issues.Count; i++)
                {
                    VisualReviewIssue issue = reviewReport.Issues[i];
                    if (issue == null || string.IsNullOrWhiteSpace(issue.Summary))
                    {
                        continue;
                    }

                    report.Regions.Add(new PreviewDiffRegion
                    {
                        RegionId = string.IsNullOrWhiteSpace(issue.IssueId) ? "preview_diff_" + i : issue.IssueId,
                        Bounds = ResolveDiffBounds(plan, issue.RelatedElementId),
                        DiffKind = issue.Kind ?? string.Empty,
                        Summary = issue.Summary ?? string.Empty,
                        SeverityScore = string.Equals(issue.Severity, "blocking", StringComparison.OrdinalIgnoreCase) ? 1f : 0.55f,
                    });
                }
            }

            if (report.Regions.Count == 0)
            {
                report.Regions.Add(new PreviewDiffRegion
                {
                    RegionId = "preview_baseline",
                    Bounds = CreateRect(0.05f, 0.05f, 0.25f, 0.06f),
                    DiffKind = "review_baseline",
                    Summary = "当前 structured preview 可用于 review-only 对照。",
                    SeverityScore = 0.1f,
                });
            }

            report.MissingRegionCount = report.Regions.Count;
            report.Notes.Add("review_only=true");
            return report;
        }

        private static UiGenerationAnalysisResult BuildAnalysisResult(
            string expectedTaskId,
            UiGenerationTaskRequest request,
            UiGenerationTemplate template,
            VisualUnderstandingBundle visualUnderstanding,
            VisualReviewReport visualReviewReport,
            UiGenerationGatingReport gatingReport,
            PreviewRenderPlan previewRenderPlan,
            PreviewDiffReport previewDiffReport,
            DesignPacket designPacket,
            UiPrefabSpec spec,
            UiResourceMatchReport report,
            UiPrefabGeneratorAutoAnalysisResult result)
        {
            var analysisResult = new UiGenerationAnalysisResult
            {
                TaskId = expectedTaskId ?? string.Empty,
                TemplateName = template != null ? template.TemplateName ?? string.Empty : string.Empty,
                ProfileId = !string.IsNullOrWhiteSpace(request != null ? request.ProfileId : string.Empty)
                    ? request.ProfileId
                    : template != null ? template.ProfileId ?? string.Empty : string.Empty,
                VisualUnderstanding = visualUnderstanding ?? new VisualUnderstandingBundle(),
                VisualReviewReport = visualReviewReport ?? new VisualReviewReport(),
                GatingReport = gatingReport ?? new UiGenerationGatingReport(),
                PreviewRenderPlan = previewRenderPlan ?? new PreviewRenderPlan(),
                PreviewDiffReport = previewDiffReport ?? new PreviewDiffReport(),
                DesignPacket = designPacket ?? new DesignPacket(),
                UiPrefabSpec = spec ?? new UiPrefabSpec(),
                ResourceMatchReport = report ?? new UiResourceMatchReport(),
            };

            analysisResult.ReviewIssues.AddRange(BuildReviewIssues(visualReviewReport, gatingReport));
            analysisResult.UnresolvedItems.AddRange(result.UnresolvedItems);
            analysisResult.Warnings.AddRange(result.Warnings);
            analysisResult.Errors.AddRange(result.Errors);
            return analysisResult;
        }

        private static string BuildSummaryMarkdown(string taskDirectory, UiPrefabGeneratorAutoAnalysisResult result)
        {
            var builder = new StringBuilder();
            builder.AppendLine("# UiPrefabGenerator Auto Analysis");
            builder.AppendLine();
            builder.AppendLine("- TaskDirectory: " + taskDirectory);
            builder.AppendLine("- TaskId: " + SafeField(result.AnalysisResult != null ? result.AnalysisResult.TaskId : string.Empty));
            builder.AppendLine("- TemplateName: " + SafeField(result.Template != null ? result.Template.TemplateName : string.Empty));
            builder.AppendLine("- ProfileId: " + SafeField(result.AnalysisResult != null ? result.AnalysisResult.ProfileId : string.Empty));
            builder.AppendLine("- SourceImage: " + SafeField(ResolveSourceImagePath(result.Request)));
            builder.AppendLine();
            builder.AppendLine("## VisualEvidence");
            builder.AppendLine("- Provider: " + SafeField(result.VisualUnderstanding != null ? result.VisualUnderstanding.ProviderId : string.Empty));
            builder.AppendLine("- Elements: " + CountOf(result.VisualUnderstanding != null ? result.VisualUnderstanding.Elements : null));
            builder.AppendLine("- LowConfidenceElements: " + (result.VisualUnderstanding != null && result.VisualUnderstanding.ConfidenceSummary != null
                ? result.VisualUnderstanding.ConfidenceSummary.LowConfidenceElementCount
                : 0));
            builder.AppendLine("- ReviewIssues: " + CountOf(result.VisualReviewReport != null ? result.VisualReviewReport.Issues : null));
            builder.AppendLine();
            builder.AppendLine("## IntakeGating");
            builder.AppendLine("- Status: " + SafeField(result.GatingReport != null ? result.GatingReport.Status : string.Empty));
            builder.AppendLine("- Issues: " + CountOf(result.GatingReport != null ? result.GatingReport.Issues : null));
            builder.AppendLine();
            builder.AppendLine("## DesignPacket");
            builder.AppendLine("- States: " + CountOf(result.DesignPacket != null ? result.DesignPacket.States : null));
            builder.AppendLine("- Rules: " + CountOf(result.DesignPacket != null ? result.DesignPacket.Rules : null));
            builder.AppendLine("- AssetSlotHints: " + CountOf(result.DesignPacket != null ? result.DesignPacket.AssetSlotHints : null));
            builder.AppendLine("- ExpectedSemanticRoles: " + CountOf(result.DesignPacket != null ? result.DesignPacket.ExpectedSemanticRoles : null));
            builder.AppendLine("- ElementHints: " + CountOf(result.DesignPacket != null ? result.DesignPacket.ElementHints : null));
            builder.AppendLine();
            builder.AppendLine("## UiPrefabSpec");
            builder.AppendLine("- Nodes: " + CountOf(result.UiPrefabSpec != null ? result.UiPrefabSpec.Nodes : null));
            builder.AppendLine("- Bindings: " + CountOf(result.UiPrefabSpec != null ? result.UiPrefabSpec.Bindings : null));
            builder.AppendLine("- Interactions: " + CountOf(result.UiPrefabSpec != null ? result.UiPrefabSpec.Interactions : null));
            builder.AppendLine();
            builder.AppendLine("## StructuredPreview");
            builder.AppendLine("- Nodes: " + CountOf(result.PreviewRenderPlan != null ? result.PreviewRenderPlan.Nodes : null));
            builder.AppendLine("- DiffRegions: " + CountOf(result.PreviewDiffReport != null ? result.PreviewDiffReport.Regions : null));
            builder.AppendLine("- CoverageScore: " + (result.PreviewDiffReport != null ? result.PreviewDiffReport.CoverageScore.ToString("0.00") : "0.00"));
            builder.AppendLine();
            builder.AppendLine("## ResourceMatch");
            builder.AppendLine("- Slots: " + CountOf(result.ResourceMatchReport != null ? result.ResourceMatchReport.Matches : null));
            builder.AppendLine("- UnresolvedSlots: " + CountOf(result.ResourceMatchReport != null ? result.ResourceMatchReport.UnresolvedSlots : null));

            if (result.UnresolvedItems.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("## UnresolvedItems");
                for (int i = 0; i < result.UnresolvedItems.Count; i++)
                {
                    builder.AppendLine("- " + result.UnresolvedItems[i]);
                }
            }

            if (result.Warnings.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("## Warnings");
                for (int i = 0; i < result.Warnings.Count; i++)
                {
                    builder.AppendLine("- " + result.Warnings[i]);
                }
            }

            if (result.Errors.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("## Errors");
                for (int i = 0; i < result.Errors.Count; i++)
                {
                    builder.AppendLine("- " + result.Errors[i]);
                }
            }

            return builder.ToString();
        }

        private static string BuildPacketNotes(
            UiGenerationTaskRequest request,
            UiGenerationTemplate template,
            VisualUnderstandingBundle visualUnderstanding)
        {
            var notes = new List<string>();
            if (!string.IsNullOrWhiteSpace(request.Notes))
            {
                notes.Add(request.Notes.Trim());
            }

            if (template != null && !string.IsNullOrWhiteSpace(template.Notes))
            {
                notes.Add(template.Notes.Trim());
            }

            if (visualUnderstanding != null && visualUnderstanding.ConfidenceSummary != null)
            {
                notes.Add("low_confidence_elements=" + visualUnderstanding.ConfidenceSummary.LowConfidenceElementCount);
            }

            return string.Join("\n", notes.Where(note => !string.IsNullOrWhiteSpace(note)).ToArray());
        }

        private static string ResolveSourceImagePath(UiGenerationTaskRequest request)
        {
            if (!string.IsNullOrWhiteSpace(request != null ? request.SourceImageTaskAssetPath : string.Empty))
            {
                return request.SourceImageTaskAssetPath;
            }

            return request != null ? request.SourceImageAssetPath ?? string.Empty : string.Empty;
        }

        private static List<string> BuildExpectedSemanticRoles(
            UiGenerationTaskRequest request,
            VisualUnderstandingBundle visualUnderstanding)
        {
            var roles = new List<string>();
            if (visualUnderstanding != null && !string.IsNullOrWhiteSpace(visualUnderstanding.SourceImagePath))
            {
                AddRoleIfMissing(roles, "panel_background");
            }

            if (!string.IsNullOrWhiteSpace(request != null ? request.PageTitle : string.Empty))
            {
                AddRoleIfMissing(roles, "title_text");
            }

            if (ShouldAddPrimaryButton(request))
            {
                AddRoleIfMissing(roles, "primary_button");
            }

            if (ShouldAddNumericValue(request))
            {
                AddRoleIfMissing(roles, "numeric_value_display");
            }

            if (HasTaskListSignal(request))
            {
                AddRoleIfMissing(roles, "task_list");
            }

            return roles;
        }

        private static List<DesignElementHint> BuildElementHints(VisualUnderstandingBundle visualUnderstanding)
        {
            var hints = new List<DesignElementHint>();
            if (visualUnderstanding == null || visualUnderstanding.Elements == null)
            {
                return hints;
            }

            for (int i = 0; i < visualUnderstanding.Elements.Count; i++)
            {
                VisualElementEvidence element = visualUnderstanding.Elements[i];
                if (element == null || string.IsNullOrWhiteSpace(element.SemanticRole))
                {
                    continue;
                }

                hints.Add(new DesignElementHint
                {
                    HintId = element.ElementId ?? string.Empty,
                    SourceElementId = element.ElementId ?? string.Empty,
                    ElementType = element.ElementType ?? string.Empty,
                    SemanticRole = element.SemanticRole ?? string.Empty,
                    SuggestedNodeId = BuildSuggestedNodeId(element.SemanticRole),
                    DisplayText = element.Text != null ? element.Text.NormalizedText ?? string.Empty : string.Empty,
                    AssetSlot = element.Style != null ? element.Style.AssetSlotHint ?? string.Empty : string.Empty,
                    LayoutSlot = element.Style != null ? element.Style.LayoutHint ?? string.Empty : string.Empty,
                    BindingKey = ResolveBindingKey(element.SemanticRole),
                    HandlerKey = ResolveHandlerKey(element.SemanticRole),
                    Confidence = element.Confidence,
                    Required = IsRequiredSemanticRole(element.SemanticRole),
                    RequiresHumanDecision = element.Confidence < LowConfidenceThreshold,
                    Bounds = element.EvidenceBounds ?? CreateRect(0.1f, 0.1f, 0.8f, 0.08f),
                });
            }

            return hints;
        }

        private static List<DesignRuleDefinition> BuildRules(
            UiGenerationTaskRequest request,
            VisualUnderstandingBundle visualUnderstanding,
            List<string> expectedSemanticRoles,
            UiPrefabGeneratorAutoAnalysisResult result)
        {
            var rules = new List<DesignRuleDefinition>();

            if (visualUnderstanding != null && visualUnderstanding.Elements != null)
            {
                for (int i = 0; i < visualUnderstanding.Elements.Count; i++)
                {
                    VisualElementEvidence element = visualUnderstanding.Elements[i];
                    if (element != null)
                    {
                        AppendRulesFromSemanticRole(rules, element.SemanticRole);
                    }
                }
            }

            if (expectedSemanticRoles != null)
            {
                for (int i = 0; i < expectedSemanticRoles.Count; i++)
                {
                    AppendRulesFromSemanticRole(rules, expectedSemanticRoles[i]);
                }
            }

            if (request != null)
            {
                AppendRulesFromMustHaveNodes(rules, request.MustHaveNodes, result);
                AppendRulesFromMustHaveInteractions(rules, request.MustHaveInteractions, result);
            }

            if (rules.Count == 0)
            {
                result.UnresolvedItems.Add("no_supported_rules");
            }

            return rules;
        }

        private static void AppendRulesFromMustHaveNodes(
            ICollection<DesignRuleDefinition> rules,
            IEnumerable<string> sourceValues,
            UiPrefabGeneratorAutoAnalysisResult result)
        {
            if (rules == null || sourceValues == null)
            {
                return;
            }

            foreach (string value in sourceValues)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                if (MatchesAnyToken(value, "task_list", "scroll"))
                {
                    AddRuleIfMissing(rules, "task_list_scrollable");
                    continue;
                }

                if (MatchesAnyToken(value, "claim_button", "claim", "button", "cta"))
                {
                    AddRuleIfMissing(rules, "primary_button");
                    continue;
                }

                if (MatchesAnyToken(value, "score", "point", "coin", "gold", "number", "numeric", "value"))
                {
                    AddRuleIfMissing(rules, "numeric_value_display");
                    continue;
                }

                result.UnresolvedItems.Add("unmapped_must_have_node:" + value);
            }
        }

        private static void AppendRulesFromMustHaveInteractions(
            ICollection<DesignRuleDefinition> rules,
            IEnumerable<string> sourceValues,
            UiPrefabGeneratorAutoAnalysisResult result)
        {
            if (rules == null || sourceValues == null)
            {
                return;
            }

            foreach (string value in sourceValues)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                if (MatchesAnyToken(value, "claim_button", "claim_task", "click", "submit"))
                {
                    AddRuleIfMissing(rules, "primary_button");
                    continue;
                }

                result.UnresolvedItems.Add("unmapped_must_have_interaction:" + value);
            }
        }

        private static void AppendRulesFromSemanticRole(ICollection<DesignRuleDefinition> rules, string semanticRole)
        {
            if (string.IsNullOrWhiteSpace(semanticRole))
            {
                return;
            }

            if (string.Equals(semanticRole, "panel_background", StringComparison.Ordinal))
            {
                AddRuleIfMissing(rules, "panel_background");
                return;
            }

            if (string.Equals(semanticRole, "title_text", StringComparison.Ordinal))
            {
                AddRuleIfMissing(rules, "title_text");
                return;
            }

            if (string.Equals(semanticRole, "primary_button", StringComparison.Ordinal))
            {
                AddRuleIfMissing(rules, "primary_button");
                return;
            }

            if (string.Equals(semanticRole, "numeric_value_display", StringComparison.Ordinal))
            {
                AddRuleIfMissing(rules, "numeric_value_display");
                return;
            }

            if (string.Equals(semanticRole, "task_list", StringComparison.Ordinal))
            {
                AddRuleIfMissing(rules, "task_list_scrollable");
            }
        }

        private static void AddRuleIfMissing(ICollection<DesignRuleDefinition> rules, string ruleId)
        {
            if (rules == null || string.IsNullOrWhiteSpace(ruleId))
            {
                return;
            }

            if (rules.Any(rule => rule != null && string.Equals(rule.RuleId, ruleId, StringComparison.Ordinal)))
            {
                return;
            }

            rules.Add(new DesignRuleDefinition
            {
                RuleId = ruleId,
                Description = ruleId,
            });
        }

        private static void AddRoleIfMissing(List<string> roles, string role)
        {
            if (roles == null || string.IsNullOrWhiteSpace(role) || roles.Contains(role))
            {
                return;
            }

            roles.Add(role);
        }

        private static bool ShouldAddPrimaryButton(UiGenerationTaskRequest request)
        {
            return HasAnySignal(request, "claim_button", "claim", "button", "cta", "submit");
        }

        private static bool ShouldAddNumericValue(UiGenerationTaskRequest request)
        {
            return HasAnySignal(request, "score", "point", "coin", "gold", "number", "numeric", "value");
        }

        private static bool HasTaskListSignal(UiGenerationTaskRequest request)
        {
            return HasAnySignal(request, "task_list", "scroll", "list");
        }

        private static bool HasAnySignal(UiGenerationTaskRequest request, params string[] tokens)
        {
            if (request == null || tokens == null || tokens.Length == 0)
            {
                return false;
            }

            if (ContainsAnyToken(request.PageId, tokens) ||
                ContainsAnyToken(request.PageTitle, tokens) ||
                ContainsAnyToken(request.Notes, tokens))
            {
                return true;
            }

            if (request.MustHaveNodes != null && request.MustHaveNodes.Any(value => ContainsAnyToken(value, tokens)))
            {
                return true;
            }

            return request.MustHaveInteractions != null && request.MustHaveInteractions.Any(value => ContainsAnyToken(value, tokens));
        }

        private static bool ContainsAnyToken(string value, params string[] tokens)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string normalized = value.Trim().ToLowerInvariant();
            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                if (!string.IsNullOrWhiteSpace(token) && normalized.Contains(token.Trim().ToLowerInvariant()))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool MatchesAnyToken(string value, params string[] tokens)
        {
            return ContainsAnyToken(value, tokens);
        }

        private static string ResolvePrimaryButtonText(UiGenerationTaskRequest request)
        {
            if (request != null && request.MustHaveInteractions != null)
            {
                for (int i = 0; i < request.MustHaveInteractions.Count; i++)
                {
                    string interaction = request.MustHaveInteractions[i];
                    if (ContainsAnyToken(interaction, "claim"))
                    {
                        return "Claim";
                    }
                }
            }

            return "Continue";
        }

        private static string ResolveNumericText(UiGenerationTaskRequest request)
        {
            if (!string.IsNullOrWhiteSpace(request != null ? request.PageTitle : string.Empty) &&
                ContainsAnyToken(request.PageTitle, "coin", "gold"))
            {
                return "888";
            }

            return "99";
        }

        private static float ResolveNumericConfidence(UiGenerationTaskRequest request)
        {
            return !string.IsNullOrWhiteSpace(request != null ? request.Notes : string.Empty) &&
                   ContainsAnyToken(request.Notes, "approx", "estimate")
                ? 0.66f
                : 0.78f;
        }

        private static void ResolveCanvasSize(string sourceImagePath, UiGenerationTemplate template, out int width, out int height)
        {
            width = template != null && template.ReferenceResolutionWidth > 0 ? template.ReferenceResolutionWidth : 1080;
            height = template != null && template.ReferenceResolutionHeight > 0 ? template.ReferenceResolutionHeight : 1920;

            if (string.IsNullOrWhiteSpace(sourceImagePath))
            {
                return;
            }

            string absolutePath = UiGenerationDataPaths.ToAbsolutePath(sourceImagePath);
            if (!File.Exists(absolutePath))
            {
                return;
            }

            byte[] bytes = File.ReadAllBytes(absolutePath);
            if (bytes == null || bytes.Length == 0)
            {
                return;
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(bytes, false))
                {
                    return;
                }

                width = Mathf.Max(1, texture.width);
                height = Mathf.Max(1, texture.height);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static void AddElement(VisualUnderstandingBundle bundle, VisualElementEvidence element)
        {
            if (bundle == null || element == null)
            {
                return;
            }

            bundle.Elements.Add(element);
            if (!string.IsNullOrWhiteSpace(element.ParentElementId))
            {
                bundle.Hierarchy.Add(new VisualHierarchyEdge
                {
                    ParentElementId = element.ParentElementId,
                    ChildElementId = element.ElementId ?? string.Empty,
                    RelationType = "contains",
                    Confidence = element.Confidence,
                });
            }
        }

        private static VisualElementEvidence CreateElement(
            string elementId,
            string elementType,
            string semanticRole,
            string displayName,
            float confidence,
            UiRect bounds,
            string text,
            VisualStyleEvidence style,
            bool isNumeric = false)
        {
            return new VisualElementEvidence
            {
                ElementId = elementId ?? string.Empty,
                ElementType = elementType ?? string.Empty,
                SemanticRole = semanticRole ?? string.Empty,
                DisplayName = displayName ?? string.Empty,
                Confidence = confidence,
                ParentElementId = "root_canvas",
                EvidenceBounds = bounds ?? CreateRect(0.1f, 0.1f, 0.8f, 0.08f),
                Text = new VisualTextEvidence
                {
                    RawText = text ?? string.Empty,
                    NormalizedText = text ?? string.Empty,
                    Confidence = confidence,
                    IsNumeric = isNumeric,
                    TextRole = isNumeric ? "numeric_value" : string.IsNullOrWhiteSpace(text) ? string.Empty : "label",
                },
                Style = style ?? new VisualStyleEvidence(),
            };
        }

        private static void PopulateConfidenceSummary(VisualUnderstandingBundle bundle, List<string> expectedSemanticRoles)
        {
            if (bundle == null)
            {
                return;
            }

            int lowConfidenceCount = 0;
            float totalConfidence = 0f;
            int counted = 0;

            if (bundle.Elements != null)
            {
                for (int i = 0; i < bundle.Elements.Count; i++)
                {
                    VisualElementEvidence element = bundle.Elements[i];
                    if (element == null)
                    {
                        continue;
                    }

                    totalConfidence += element.Confidence;
                    counted++;
                    if (element.Confidence < LowConfidenceThreshold)
                    {
                        lowConfidenceCount++;
                    }
                }
            }

            if (expectedSemanticRoles != null)
            {
                for (int i = 0; i < expectedSemanticRoles.Count; i++)
                {
                    string role = expectedSemanticRoles[i];
                    bool found = bundle.Elements != null && bundle.Elements.Exists(element => element != null && string.Equals(element.SemanticRole, role, StringComparison.Ordinal));
                    if (!found)
                    {
                        bundle.ConfidenceSummary.MissingCriticalElementCount++;
                        bundle.ConfidenceSummary.BlockingReasons.Add("missing:" + role);
                    }
                }
            }

            bundle.ConfidenceSummary.LowConfidenceElementCount = lowConfidenceCount;
            bundle.ConfidenceSummary.OverallConfidence = counted > 0 ? totalConfidence / counted : 0f;
            bundle.Notes.Add("provider=mock/local review-only evidence");
        }

        private static string BuildSuggestedNodeId(string semanticRole)
        {
            if (string.IsNullOrWhiteSpace(semanticRole))
            {
                return "node";
            }

            return semanticRole;
        }

        private static string ResolveBindingKey(string semanticRole)
        {
            if (string.Equals(semanticRole, "title_text", StringComparison.Ordinal))
            {
                return "title_text";
            }

            if (string.Equals(semanticRole, "primary_button", StringComparison.Ordinal))
            {
                return "primary_button";
            }

            if (string.Equals(semanticRole, "numeric_value_display", StringComparison.Ordinal))
            {
                return "numeric_value";
            }

            if (string.Equals(semanticRole, "task_list", StringComparison.Ordinal))
            {
                return "task_list";
            }

            return string.Empty;
        }

        private static string ResolveHandlerKey(string semanticRole)
        {
            return string.Equals(semanticRole, "primary_button", StringComparison.Ordinal)
                ? "primary_action"
                : string.Empty;
        }

        private static bool IsRequiredSemanticRole(string semanticRole)
        {
            return string.Equals(semanticRole, "panel_background", StringComparison.Ordinal) ||
                   string.Equals(semanticRole, "title_text", StringComparison.Ordinal) ||
                   string.Equals(semanticRole, "primary_button", StringComparison.Ordinal) ||
                   string.Equals(semanticRole, "numeric_value_display", StringComparison.Ordinal);
        }

        private static UiRect CreateRect(float x, float y, float width, float height)
        {
            return new UiRect
            {
                X = x,
                Y = y,
                Width = width,
                Height = height,
            };
        }

        private static string ResolvePreviewFillColor(string semanticRole)
        {
            if (string.Equals(semanticRole, "panel_background", StringComparison.Ordinal))
            {
                return "#E8EEF7";
            }

            if (string.Equals(semanticRole, "title_text", StringComparison.Ordinal))
            {
                return "#F7F3D1";
            }

            if (string.Equals(semanticRole, "primary_button", StringComparison.Ordinal))
            {
                return "#A8D1FF";
            }

            if (string.Equals(semanticRole, "numeric_value_display", StringComparison.Ordinal))
            {
                return "#FBE39C";
            }

            if (string.Equals(semanticRole, "task_list", StringComparison.Ordinal))
            {
                return "#DDE8CF";
            }

            return "#E5E5E5";
        }

        private static string ResolveFirstAssetSlot(UiNodeSpec node)
        {
            if (node == null || node.Components == null)
            {
                return string.Empty;
            }

            for (int i = 0; i < node.Components.Count; i++)
            {
                UiComponentSpec component = node.Components[i];
                if (component != null && !string.IsNullOrWhiteSpace(component.AssetSlot))
                {
                    return component.AssetSlot;
                }
            }

            return string.Empty;
        }

        private static float CalculateCoverageScore(PreviewRenderPlan plan)
        {
            if (plan == null || plan.Nodes == null || plan.Nodes.Count == 0)
            {
                return 0f;
            }

            int matchedNodes = plan.Nodes.Count(node => node != null && !string.Equals(node.NodeType, "spec_only", StringComparison.Ordinal));
            return Mathf.Clamp01(matchedNodes / (float)Math.Max(1, plan.Nodes.Count));
        }

        private static float CalculateLayoutSimilarityScore(PreviewRenderPlan plan)
        {
            if (plan == null || plan.Nodes == null || plan.Nodes.Count == 0)
            {
                return 0f;
            }

            int confidentNodes = plan.Nodes.Count(node => node != null && !string.Equals(node.StrokeColor, "#D9485F", StringComparison.Ordinal));
            return Mathf.Clamp01(confidentNodes / (float)Math.Max(1, plan.Nodes.Count));
        }

        private static UiRect ResolveDiffBounds(PreviewRenderPlan plan, string relatedElementId)
        {
            if (plan != null && plan.Nodes != null)
            {
                for (int i = 0; i < plan.Nodes.Count; i++)
                {
                    PreviewRenderNode node = plan.Nodes[i];
                    if (node != null && string.Equals(node.NodeId, relatedElementId, StringComparison.Ordinal))
                    {
                        return node.Bounds ?? CreateRect(0.1f, 0.1f, 0.2f, 0.06f);
                    }
                }
            }

            return CreateRect(0.08f, 0.08f, 0.2f, 0.06f);
        }

        private static string ResolveAssetType(string assetPath, string componentType)
        {
            string extension = Path.GetExtension(assetPath) ?? string.Empty;
            if (string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase))
            {
                return IsImageComponent(componentType) ? "Sprite" : "Texture2D";
            }

            if (string.Equals(extension, ".prefab", StringComparison.OrdinalIgnoreCase))
            {
                return "Prefab";
            }

            if (string.Equals(extension, ".asset", StringComparison.OrdinalIgnoreCase))
            {
                return "Asset";
            }

            return "Unknown";
        }

        private static bool IsImageComponent(string componentType)
        {
            return string.Equals(componentType, "Image", StringComparison.Ordinal) ||
                   string.Equals(componentType, "RawImage", StringComparison.Ordinal);
        }

        private static bool IsImageAsset(string assetPath)
        {
            string extension = Path.GetExtension(assetPath) ?? string.Empty;
            return string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildCandidateReason(string slotId, PrefabBindingSlotContext context, string assetPath, float score)
        {
            return string.Format(
                "slot={0}; component={1}; usage={2}; asset={3}; score={4:0.00}",
                slotId ?? string.Empty,
                context != null ? context.ComponentType ?? string.Empty : string.Empty,
                context != null ? context.Usage ?? string.Empty : string.Empty,
                assetPath ?? string.Empty,
                score);
        }

        private static IEnumerable<string> Tokenize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                yield break;
            }

            string[] segments = NormalizeText(value).Split(new[] { ' ', '/', '\\', '_', '-', '.', ':' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < segments.Length; i++)
            {
                string segment = segments[i].Trim();
                if (!string.IsNullOrWhiteSpace(segment))
                {
                    yield return segment;
                }
            }
        }

        private static string NormalizeText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = char.ToLowerInvariant(value[i]);
                builder.Append(char.IsLetterOrDigit(c) ? c : ' ');
            }

            return builder.ToString();
        }

        private static List<UiGenerationReviewIssue> BuildReviewIssues(
            VisualReviewReport visualReviewReport,
            UiGenerationGatingReport gatingReport)
        {
            var issues = new List<UiGenerationReviewIssue>();

            if (visualReviewReport != null && visualReviewReport.Issues != null)
            {
                for (int i = 0; i < visualReviewReport.Issues.Count; i++)
                {
                    VisualReviewIssue issue = visualReviewReport.Issues[i];
                    if (issue == null)
                    {
                        continue;
                    }

                    issues.Add(new UiGenerationReviewIssue
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

            if (gatingReport != null && gatingReport.Issues != null)
            {
                for (int i = 0; i < gatingReport.Issues.Count; i++)
                {
                    UiGenerationGatingIssue issue = gatingReport.Issues[i];
                    if (issue == null)
                    {
                        continue;
                    }

                    issues.Add(new UiGenerationReviewIssue
                    {
                        IssueId = issue.IssueId ?? string.Empty,
                        Source = "gating",
                        IssueKind = issue.Kind ?? string.Empty,
                        Severity = ParseReviewSeverity(issue.Severity),
                        Summary = issue.Summary ?? string.Empty,
                        Details = issue.Details ?? string.Empty,
                        RelatedId = issue.FieldPath ?? string.Empty,
                        SuggestedResolution = issue.SuggestedResolution ?? string.Empty,
                        RequiresHumanDecision = issue.RequiresHumanDecision,
                    });
                }
            }

            return issues;
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

        private static string SafeField(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "(empty)" : value;
        }

        private static int CountOf<T>(ICollection<T> values)
        {
            return values == null ? 0 : values.Count;
        }

        private sealed class PrefabBindingSlotContext
        {
            public string ComponentType = string.Empty;
            public string Usage = string.Empty;
            public readonly List<string> NodeIds = new List<string>();
        }
    }

    public sealed class UiPrefabGeneratorAutoAnalysisResult
    {
        public UiGenerationTaskRequest Request;
        public UiGenerationTemplate Template;
        public VisualUnderstandingBundle VisualUnderstanding;
        public VisualReviewReport VisualReviewReport;
        public UiGenerationGatingReport GatingReport;
        public PreviewRenderPlan PreviewRenderPlan;
        public PreviewDiffReport PreviewDiffReport;
        public DesignPacketIntakeAssessment IntakeAssessment;
        public DesignPacket DesignPacket;
        public UiPrefabSpec UiPrefabSpec;
        public UiResourceMatchReport ResourceMatchReport;
        public UiGenerationAnalysisResult AnalysisResult;
        public string AnalysisSummaryMarkdown = string.Empty;
        public bool Success;
        public readonly List<string> Warnings = new List<string>();
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> UnresolvedItems = new List<string>();
    }
}
