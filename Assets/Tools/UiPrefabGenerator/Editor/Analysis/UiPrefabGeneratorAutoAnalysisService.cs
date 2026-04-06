using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
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
        private const string InterpreterTypeName = "UiPrefabGenerator.Core.Intake.DefaultDesignPacketToUiPrefabSpecInterpreter, UiPrefabGenerator.Core.Intake";
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
            result.DesignPacket = BuildDesignPacket(request, result.Template, result);
            result.UiPrefabSpec = InterpretSpec(result.DesignPacket, result.Template, request, result);
            result.ResourceMatchReport = BuildResourceMatchReport(taskDirectory, request, result.Template, result.UiPrefabSpec, result);
            result.AnalysisResult = BuildAnalysisResult(expectedTaskId, request, result.Template, result.DesignPacket, result.UiPrefabSpec, result.ResourceMatchReport, result);
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

            for (int i = 0; i < templatePaths.Length; i++)
            {
                string templatePath = templatePaths[i];
                if (string.Equals(templatePath, defaultTemplatePath, StringComparison.Ordinal))
                {
                    return templatePath;
                }
            }

            return templatePaths[0];
        }

        private static DesignPacket BuildDesignPacket(
            UiGenerationTaskRequest request,
            UiGenerationTemplate template,
            UiPrefabGeneratorAutoAnalysisResult result)
        {
            var designPacket = new DesignPacket
            {
                PageId = request.PageId ?? string.Empty,
                PageTitle = request.PageTitle ?? string.Empty,
                PrefabName = request.PrefabName ?? string.Empty,
                Notes = BuildPacketNotes(request, template),
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

            foreach (DesignRuleDefinition rule in BuildRules(request, result))
            {
                designPacket.Rules.Add(rule);
            }

            if (!string.IsNullOrWhiteSpace(sourceImagePath))
            {
                designPacket.AssetSlotHints.Add(new DesignAssetSlotHint
                {
                    SlotId = "panel_bg",
                    Usage = "background image from source design",
                });
            }

            return designPacket;
        }

        private static string BuildPacketNotes(UiGenerationTaskRequest request, UiGenerationTemplate template)
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

            return string.Join("\n", notes);
        }

        private static string ResolveSourceImagePath(UiGenerationTaskRequest request)
        {
            if (request == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(request.SourceImageTaskAssetPath))
            {
                return request.SourceImageTaskAssetPath;
            }

            return request.SourceImageAssetPath ?? string.Empty;
        }

        private static List<DesignRuleDefinition> BuildRules(
            UiGenerationTaskRequest request,
            UiPrefabGeneratorAutoAnalysisResult result)
        {
            var rules = new List<DesignRuleDefinition>();

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
            if (rules == null || result == null || sourceValues == null)
            {
                return;
            }

            foreach (string value in sourceValues)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                if (MatchesAnyToken(value, "task_list", "task_list_scrollable"))
                {
                    AddRuleIfMissing(rules, "task_list_scrollable");
                    continue;
                }

                if (MatchesAnyToken(value, "claim_button", "claim", "button"))
                {
                    AddRuleIfMissing(rules, "claim_button_visualized");
                    AddRuleIfMissing(rules, "claim_button_clickable");
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
            if (rules == null || result == null || sourceValues == null)
            {
                return;
            }

            foreach (string value in sourceValues)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                if (MatchesAnyToken(value, "claim_button", "claim_task", "click"))
                {
                    AddRuleIfMissing(rules, "claim_button_visualized");
                    AddRuleIfMissing(rules, "claim_button_clickable");
                    continue;
                }

                result.UnresolvedItems.Add("unmapped_must_have_interaction:" + value);
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

        private static bool MatchesAnyToken(string value, params string[] tokens)
        {
            if (string.IsNullOrWhiteSpace(value) || tokens == null || tokens.Length == 0)
            {
                return false;
            }

            string normalizedValue = value.Trim().ToLowerInvariant();
            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                if (!string.IsNullOrWhiteSpace(token) && normalizedValue.Contains(token.Trim().ToLowerInvariant()))
                {
                    return true;
                }
            }

            return false;
        }

        private static UiPrefabSpec InterpretSpec(
            DesignPacket designPacket,
            UiGenerationTemplate template,
            UiGenerationTaskRequest request,
            UiPrefabGeneratorAutoAnalysisResult result)
        {
            try
            {
                IUiSpecInterpreter interpreter = CreateInterpreter(request, template);
                UiPrefabSpec spec = interpreter.Interpret(designPacket);
                if (spec == null)
                {
                    result.Errors.Add("UiPrefabSpec 解释结果为空。");
                    return new UiPrefabSpec();
                }

                return spec;
            }
            catch (TargetInvocationException exception)
            {
                Exception root = exception.InnerException ?? exception;
                result.Errors.Add(root.Message);
                return new UiPrefabSpec();
            }
            catch (Exception exception)
            {
                result.Errors.Add(exception.Message);
                return new UiPrefabSpec();
            }
        }

        private static IUiSpecInterpreter CreateInterpreter(UiGenerationTaskRequest request, UiGenerationTemplate template)
        {
            Type interpreterType = Type.GetType(InterpreterTypeName, false);
            if (interpreterType == null)
            {
                interpreterType = AppDomain.CurrentDomain
                    .GetAssemblies()
                    .Select(assembly => assembly.GetType("UiPrefabGenerator.Core.Intake.DefaultDesignPacketToUiPrefabSpecInterpreter", false))
                    .FirstOrDefault(type => type != null);
            }

            if (interpreterType == null)
            {
                throw new InvalidOperationException("无法加载 DefaultDesignPacketToUiPrefabSpecInterpreter。");
            }

            string generationProfileId = request != null && !string.IsNullOrWhiteSpace(request.ProfileId)
                ? request.ProfileId
                : template != null ? template.ProfileId ?? string.Empty : string.Empty;
            object instance = string.IsNullOrWhiteSpace(generationProfileId)
                ? Activator.CreateInstance(interpreterType)
                : Activator.CreateInstance(interpreterType, new object[] { null, generationProfileId });
            IUiSpecInterpreter interpreter = instance as IUiSpecInterpreter;
            if (interpreter == null)
            {
                throw new InvalidOperationException("解释器实例无法转换为 IUiSpecInterpreter。");
            }

            return interpreter;
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
            if (slotContexts.Count == 0)
            {
                return report;
            }

            foreach (KeyValuePair<string, PrefabBindingSlotContext> kvp in slotContexts)
            {
                UiAssetSlotMatch match = BuildSlotMatch(kvp.Key, kvp.Value, candidateAssets, request, template, result);
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
            if (request != null && !string.IsNullOrWhiteSpace(request.AssetRoot))
            {
                return request.AssetRoot;
            }

            if (template != null && !string.IsNullOrWhiteSpace(template.AssetRoot))
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

            var candidateAssets = new List<string>();
            foreach (string absolutePath in Directory.GetFiles(absoluteAssetRoot, "*", SearchOption.AllDirectories))
            {
                if (string.Equals(Path.GetExtension(absolutePath), ".meta", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (allowedExtensions.Count > 0 && !allowedExtensions.Contains(Path.GetExtension(absolutePath)))
                {
                    continue;
                }

                string assetPath = UiGenerationDataPaths.ToAssetPath(absolutePath);
                if (!string.IsNullOrWhiteSpace(assetPath))
                {
                    candidateAssets.Add(assetPath);
                }
            }

            candidateAssets.Sort(StringComparer.Ordinal);
            return candidateAssets;
        }

        private static Dictionary<string, PrefabBindingSlotContext> CollectSlotContexts(UiPrefabSpec spec)
        {
            var slotContexts = new Dictionary<string, PrefabBindingSlotContext>(StringComparer.Ordinal);
            foreach (UiNodeSpec node in spec.Nodes)
            {
                if (node == null || node.Components == null)
                {
                    continue;
                }

                for (int i = 0; i < node.Components.Count; i++)
                {
                    UiComponentSpec component = node.Components[i];
                    if (component == null || string.IsNullOrWhiteSpace(component.AssetSlot))
                    {
                        continue;
                    }

                    PrefabBindingSlotContext context;
                    if (!slotContexts.TryGetValue(component.AssetSlot, out context))
                    {
                        context = new PrefabBindingSlotContext();
                        slotContexts[component.AssetSlot] = context;
                    }

                    context.ComponentType = string.IsNullOrWhiteSpace(context.ComponentType) ? component.ComponentType ?? string.Empty : context.ComponentType;
                    if (string.IsNullOrWhiteSpace(context.Usage))
                    {
                        context.Usage = node.Layout != null ? node.Layout.LayoutSlot ?? string.Empty : string.Empty;
                    }
                    context.NodeIds.Add(node.NodeId ?? string.Empty);
                }
            }

            return slotContexts;
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

            List<UiAssetCandidate> candidates = new List<UiAssetCandidate>();
            foreach (string assetPath in candidateAssets)
            {
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
                    Recommended = false,
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
                match.Notes = match.SelectedAssetPath.Length > 0
                    ? "auto-selected"
                    : "top candidate below selection threshold";
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

            var slotTokens = Tokenize(normalizedSlot)
                .Concat(Tokenize(normalizedUsage))
                .Concat(Tokenize(normalizedComponent))
                .Concat(Tokenize(normalizedPage))
                .Concat(Tokenize(normalizedTitle))
                .Concat(Tokenize(normalizedPrefab))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            var assetTokens = Tokenize(normalizedAsset)
                .Concat(Tokenize(normalizedAssetPath))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (slotTokens.Count == 0 || assetTokens.Count == 0)
            {
                return 0f;
            }

            int overlap = 0;
            for (int i = 0; i < slotTokens.Count; i++)
            {
                string token = slotTokens[i];
                if (assetTokens.Contains(token))
                {
                    overlap++;
                }
            }

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

            return Math.Max(0f, Math.Min(1f, score));
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

            string normalized = NormalizeText(value);
            string[] segments = normalized.Split(new[] { ' ', '/', '\\', '_', '-', '.', ':' }, StringSplitOptions.RemoveEmptyEntries);
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

        private static UiGenerationAnalysisResult BuildAnalysisResult(
            string expectedTaskId,
            UiGenerationTaskRequest request,
            UiGenerationTemplate template,
            DesignPacket designPacket,
            UiPrefabSpec spec,
            UiResourceMatchReport report,
            UiPrefabGeneratorAutoAnalysisResult result)
        {
            var analysisResult = new UiGenerationAnalysisResult
            {
                TaskId = expectedTaskId ?? string.Empty,
                TemplateName = template != null ? template.TemplateName ?? string.Empty : string.Empty,
                ProfileId = request != null && !string.IsNullOrWhiteSpace(request.ProfileId)
                    ? request.ProfileId ?? string.Empty
                    : template != null ? template.ProfileId ?? string.Empty : string.Empty,
                DesignPacket = designPacket ?? new DesignPacket(),
                UiPrefabSpec = spec ?? new UiPrefabSpec(),
                ResourceMatchReport = report ?? new UiResourceMatchReport(),
            };

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
            builder.AppendLine("## DesignPacket");
            builder.AppendLine("- States: " + CountOf(result.DesignPacket != null ? result.DesignPacket.States : null));
            builder.AppendLine("- Rules: " + CountOf(result.DesignPacket != null ? result.DesignPacket.Rules : null));
            builder.AppendLine("- AssetSlotHints: " + CountOf(result.DesignPacket != null ? result.DesignPacket.AssetSlotHints : null));
            builder.AppendLine();
            builder.AppendLine("## UiPrefabSpec");
            builder.AppendLine("- Nodes: " + CountOf(result.UiPrefabSpec != null ? result.UiPrefabSpec.Nodes : null));
            builder.AppendLine("- Bindings: " + CountOf(result.UiPrefabSpec != null ? result.UiPrefabSpec.Bindings : null));
            builder.AppendLine("- Interactions: " + CountOf(result.UiPrefabSpec != null ? result.UiPrefabSpec.Interactions : null));
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
