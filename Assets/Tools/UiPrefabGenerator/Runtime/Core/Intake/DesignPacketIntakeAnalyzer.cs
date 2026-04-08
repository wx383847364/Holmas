using System;
using System.Collections.Generic;
using UiPrefabGenerator.Core.Schema;

namespace UiPrefabGenerator.Core.Intake
{
    public sealed class DefaultDesignPacketIntakeAnalyzer : IDesignPacketIntakeAnalyzer
    {
        public DesignPacketIntakeAssessment Analyze(DesignPacket designPacket)
        {
            var assessment = new DesignPacketIntakeAssessment
            {
                SourcePacket = designPacket,
                IntakeProfileId = "design_packet_review_v1",
            };

            if (designPacket == null)
            {
                AddIssue(
                    assessment,
                    "packet_null",
                    DesignPacketIntakeIssueSeverity.Blocking,
                    DesignPacketIntakeIssueKind.MissingRequiredField,
                    "packet",
                    "DesignPacket 不能为空。",
                    "无法开始 intake 审阅。",
                    "先补齐 DesignPacket。",
                    true);
                return assessment;
            }

            ValidateRequiredFields(designPacket, assessment);
            ValidateStates(designPacket, assessment);
            ValidateImages(designPacket, assessment);
            ValidateAssetSlotHints(designPacket, assessment);
            ValidateElementHints(designPacket, assessment);
            ValidateRules(designPacket, assessment);
            ValidateReviewHints(designPacket, assessment);
            AppendSummaryNotes(designPacket, assessment);
            return assessment;
        }

        private static void ValidateRequiredFields(DesignPacket designPacket, DesignPacketIntakeAssessment assessment)
        {
            if (string.IsNullOrWhiteSpace(designPacket.PageId))
            {
                AddIssue(
                    assessment,
                    "page_id_missing",
                    DesignPacketIntakeIssueSeverity.Blocking,
                    DesignPacketIntakeIssueKind.MissingRequiredField,
                    "page_id",
                    "缺少页面标识。",
                    "DesignPacket.PageId 不能为空。",
                    "补齐稳定 page_id。",
                    true);
            }

            if (string.IsNullOrWhiteSpace(designPacket.PageTitle))
            {
                AddIssue(
                    assessment,
                    "page_title_missing",
                    DesignPacketIntakeIssueSeverity.Warning,
                    DesignPacketIntakeIssueKind.MissingRequiredField,
                    "page_title",
                    "缺少页面标题。",
                    "PageTitle 建议补齐，便于审阅稿和日志描述。",
                    "补一个可读标题。",
                    false);
            }

            if (string.IsNullOrWhiteSpace(designPacket.PrefabName))
            {
                AddIssue(
                    assessment,
                    "prefab_name_missing",
                    DesignPacketIntakeIssueSeverity.Blocking,
                    DesignPacketIntakeIssueKind.MissingRequiredField,
                    "prefab_name",
                    "缺少 prefab 名称。",
                    "PrefabName 不能为空。",
                    "补齐稳定 prefab 名称。",
                    true);
            }
        }

        private static void ValidateStates(DesignPacket designPacket, DesignPacketIntakeAssessment assessment)
        {
            if (designPacket.States == null || designPacket.States.Count == 0)
            {
                AddIssue(
                    assessment,
                    "states_missing",
                    DesignPacketIntakeIssueSeverity.Blocking,
                    DesignPacketIntakeIssueKind.MissingStateDefinition,
                    "states",
                    "缺少状态定义。",
                    "至少需要 1 个状态定义。",
                    "补齐 states。",
                    true);
                return;
            }

            var seenStates = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < designPacket.States.Count; i++)
            {
                DesignStateDefinition state = designPacket.States[i];
                if (state == null || string.IsNullOrWhiteSpace(state.StateId))
                {
                    AddIssue(
                        assessment,
                        "state_id_missing_" + i,
                        DesignPacketIntakeIssueSeverity.Blocking,
                        DesignPacketIntakeIssueKind.MissingStateDefinition,
                        "states[" + i + "].state_id",
                        "状态 ID 不能为空。",
                        "存在空状态定义。",
                        "为该状态补齐 state_id。",
                        true);
                    continue;
                }

                if (!seenStates.Add(state.StateId))
                {
                    AddIssue(
                        assessment,
                        "state_id_duplicate_" + state.StateId,
                        DesignPacketIntakeIssueSeverity.Blocking,
                        DesignPacketIntakeIssueKind.NamingConflict,
                        "states[" + i + "].state_id",
                        "状态 ID 重复。",
                        "重复的 state_id: " + state.StateId,
                        "保持状态 ID 唯一。",
                        true);
                }
            }
        }

        private static void ValidateImages(DesignPacket designPacket, DesignPacketIntakeAssessment assessment)
        {
            if (designPacket.DesignImages == null || designPacket.DesignImages.Count == 0)
            {
                AddIssue(
                    assessment,
                    "design_images_missing",
                    DesignPacketIntakeIssueSeverity.Blocking,
                    DesignPacketIntakeIssueKind.MissingDesignImage,
                    "design_images",
                    "缺少设计图引用。",
                    "至少需要 1 个 design_images 条目。",
                    "补齐设计图引用。",
                    true);
                return;
            }

            var knownStates = new HashSet<string>(StringComparer.Ordinal);
            if (designPacket.States != null)
            {
                for (int i = 0; i < designPacket.States.Count; i++)
                {
                    DesignStateDefinition state = designPacket.States[i];
                    if (state != null && !string.IsNullOrWhiteSpace(state.StateId))
                    {
                        knownStates.Add(state.StateId);
                    }
                }
            }

            for (int i = 0; i < designPacket.DesignImages.Count; i++)
            {
                DesignImageReference image = designPacket.DesignImages[i];
                if (image == null)
                {
                    AddIssue(
                        assessment,
                        "design_image_null_" + i,
                        DesignPacketIntakeIssueSeverity.Blocking,
                        DesignPacketIntakeIssueKind.MissingDesignImage,
                        "design_images[" + i + "]",
                        "设计图引用不能为空。",
                        "存在空的设计图条目。",
                        "补齐或移除该条目。",
                        true);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(image.ImagePath))
                {
                    AddIssue(
                        assessment,
                        "image_path_missing_" + i,
                        DesignPacketIntakeIssueSeverity.Blocking,
                        DesignPacketIntakeIssueKind.MissingDesignImage,
                        "design_images[" + i + "].image_path",
                        "设计图路径不能为空。",
                        "image_path 缺失。",
                        "补齐 image_path。",
                        true);
                }

                if (string.IsNullOrWhiteSpace(image.StateId))
                {
                    AddIssue(
                        assessment,
                        "image_state_missing_" + i,
                        DesignPacketIntakeIssueSeverity.Blocking,
                        DesignPacketIntakeIssueKind.AmbiguousImageStateMapping,
                        "design_images[" + i + "].state_id",
                        "设计图缺少状态映射。",
                        "每张设计图都应明确映射到一个状态。",
                        "补齐 state_id。",
                        true);
                    continue;
                }

                if (knownStates.Count > 0 && !knownStates.Contains(image.StateId))
                {
                    AddIssue(
                        assessment,
                        "image_state_unknown_" + image.StateId,
                        DesignPacketIntakeIssueSeverity.Blocking,
                        DesignPacketIntakeIssueKind.AmbiguousImageStateMapping,
                        "design_images[" + i + "].state_id",
                        "设计图引用了未定义状态。",
                        "image state_id 不存在于 states: " + image.StateId,
                        "修正 state_id 或补状态定义。",
                        true);
                }
            }
        }

        private static void ValidateAssetSlotHints(DesignPacket designPacket, DesignPacketIntakeAssessment assessment)
        {
            if (designPacket.AssetSlotHints == null || designPacket.AssetSlotHints.Count == 0)
            {
                AddIssue(
                    assessment,
                    "asset_hints_missing",
                    DesignPacketIntakeIssueSeverity.Warning,
                    DesignPacketIntakeIssueKind.MissingAssetSlotHint,
                    "asset_slot_hints",
                    "缺少资源位提示。",
                    "当前可继续产出最小 spec，但后续资源位绑定可能需要人工补位。",
                    "补至少 1 个 asset_slot_hint。",
                    true);
                return;
            }

            var seenSlots = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < designPacket.AssetSlotHints.Count; i++)
            {
                DesignAssetSlotHint hint = designPacket.AssetSlotHints[i];
                if (hint == null || string.IsNullOrWhiteSpace(hint.SlotId))
                {
                    AddIssue(
                        assessment,
                        "asset_hint_missing_" + i,
                        DesignPacketIntakeIssueSeverity.Warning,
                        DesignPacketIntakeIssueKind.MissingAssetSlotHint,
                        "asset_slot_hints[" + i + "].slot_id",
                        "资源位提示缺少 slot_id。",
                        "该资源位提示无法参与自动推断。",
                        "补齐 slot_id。",
                        true);
                    continue;
                }

                if (!seenSlots.Add(hint.SlotId))
                {
                    AddIssue(
                        assessment,
                        "asset_hint_duplicate_" + hint.SlotId,
                        DesignPacketIntakeIssueSeverity.Warning,
                        DesignPacketIntakeIssueKind.AmbiguousAssetSlotMapping,
                        "asset_slot_hints[" + i + "].slot_id",
                        "资源位提示重复。",
                        "重复 slot_id: " + hint.SlotId,
                        "统一到单一 slot_id。",
                        true);
                }
            }
        }

        private static void ValidateRules(DesignPacket designPacket, DesignPacketIntakeAssessment assessment)
        {
            if (designPacket.Rules == null)
            {
                return;
            }

            for (int i = 0; i < designPacket.Rules.Count; i++)
            {
                DesignRuleDefinition rule = designPacket.Rules[i];
                if (rule == null || string.IsNullOrWhiteSpace(rule.RuleId))
                {
                    AddIssue(
                        assessment,
                        "rule_missing_" + i,
                        DesignPacketIntakeIssueSeverity.Warning,
                        DesignPacketIntakeIssueKind.UnsupportedRule,
                        "rules[" + i + "]",
                        "存在无法识别的规则项。",
                        "rule_id 缺失，当前无法自动解释。",
                        "补齐 rule_id 或删除该规则。",
                        true);
                    continue;
                }

                if (!IsRuleSupported(rule.RuleId))
                {
                    AddIssue(
                        assessment,
                        "rule_unsupported_" + rule.RuleId,
                        DesignPacketIntakeIssueSeverity.Warning,
                        DesignPacketIntakeIssueKind.UnsupportedRule,
                        "rules[" + i + "].rule_id",
                        "当前规则需要人工确认。",
                        "rule_id 尚未接入自动解释: " + rule.RuleId,
                        "先由人工审阅，再决定是否扩展解释器。",
                        true);
                }
            }
        }

        private static void ValidateElementHints(DesignPacket designPacket, DesignPacketIntakeAssessment assessment)
        {
            List<DesignElementHint> elementHints = designPacket.ElementHints;
            List<string> expectedRoles = designPacket.ExpectedSemanticRoles;
            if ((elementHints == null || elementHints.Count == 0) &&
                (expectedRoles == null || expectedRoles.Count == 0))
            {
                return;
            }

            var seenHintIds = new HashSet<string>(StringComparer.Ordinal);
            var roleCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            var assetSlotToHintId = new Dictionary<string, string>(StringComparer.Ordinal);

            if (elementHints != null)
            {
                for (int i = 0; i < elementHints.Count; i++)
                {
                    DesignElementHint hint = elementHints[i];
                    if (hint == null)
                    {
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(hint.HintId) && !seenHintIds.Add(hint.HintId))
                    {
                        AddIssue(
                            assessment,
                            "element_hint_duplicate_" + hint.HintId,
                            DesignPacketIntakeIssueSeverity.Warning,
                            DesignPacketIntakeIssueKind.NamingConflict,
                            "element_hints[" + i + "].hint_id",
                            "存在重复的 element hint 标识。",
                            "重复 hint_id: " + hint.HintId,
                            "保持 hint_id 唯一。",
                            true);
                    }

                    if (string.IsNullOrWhiteSpace(hint.SemanticRole))
                    {
                        AddIssue(
                            assessment,
                            "element_hint_role_missing_" + i,
                            DesignPacketIntakeIssueSeverity.Warning,
                            DesignPacketIntakeIssueKind.HumanDecisionRequired,
                            "element_hints[" + i + "].semantic_role",
                            "存在缺少语义角色的 element hint。",
                            "该 hint 无法稳定参与 intake / spec 归一化。",
                            "补齐 semantic_role，或在人工审阅中忽略该条目。",
                            true);
                        continue;
                    }

                    int roleCount;
                    roleCounts.TryGetValue(hint.SemanticRole, out roleCount);
                    roleCounts[hint.SemanticRole] = roleCount + 1;

                    if (hint.Confidence > 0f && hint.Confidence < 0.65f)
                    {
                        AddIssue(
                            assessment,
                            "element_hint_low_confidence_" + (hint.HintId ?? i.ToString()),
                            DesignPacketIntakeIssueSeverity.Warning,
                            DesignPacketIntakeIssueKind.LowConfidenceEvidence,
                            "element_hints[" + i + "].confidence",
                            "存在低置信度 element hint。",
                            "semantic_role=" + hint.SemanticRole + ", confidence=" + hint.Confidence.ToString("0.00"),
                            "优先人工复核该元素，再决定是否进入后续 spec 审阅。",
                            true);
                    }

                    if (!string.IsNullOrWhiteSpace(hint.DisplayText) &&
                        hint.Confidence > 0f &&
                        hint.Confidence < 0.7f)
                    {
                        AddIssue(
                            assessment,
                            "element_hint_low_confidence_text_" + (hint.HintId ?? i.ToString()),
                            DesignPacketIntakeIssueSeverity.Warning,
                            DesignPacketIntakeIssueKind.LowConfidenceEvidence,
                            "element_hints[" + i + "].display_text",
                            "存在低置信文本识别。",
                            "display_text=" + hint.DisplayText,
                            "人工确认文本后再决定是否作为 binding 或文案依据。",
                            true);
                    }

                    if (!string.IsNullOrWhiteSpace(hint.AssetSlot))
                    {
                        string existingHintId;
                        if (assetSlotToHintId.TryGetValue(hint.AssetSlot, out existingHintId))
                        {
                            AddIssue(
                                assessment,
                                "element_hint_slot_ambiguous_" + hint.AssetSlot,
                                DesignPacketIntakeIssueSeverity.Warning,
                                DesignPacketIntakeIssueKind.AmbiguousAssetSlotMapping,
                                "element_hints[" + i + "].asset_slot",
                                "存在资源位歧义。",
                                "多个 hint 竞争同一 asset_slot: " + hint.AssetSlot,
                                "人工确认最终应绑定的资源位归属。",
                                true);
                        }
                        else
                        {
                            assetSlotToHintId[hint.AssetSlot] = hint.HintId ?? string.Empty;
                        }
                    }

                    if (HasLayoutConflict(hint.Bounds))
                    {
                        AddIssue(
                            assessment,
                            "element_hint_layout_conflict_" + (hint.HintId ?? i.ToString()),
                            DesignPacketIntakeIssueSeverity.Warning,
                            DesignPacketIntakeIssueKind.LayoutConflict,
                            "element_hints[" + i + "].bounds",
                            "存在可疑布局冲突。",
                            "bounds 的 width/height 非法，或超出有效归一化范围。",
                            "人工检查该元素边界，再继续 spec 审阅。",
                            true);
                    }
                }
            }

            if (expectedRoles != null)
            {
                for (int i = 0; i < expectedRoles.Count; i++)
                {
                    string expectedRole = expectedRoles[i];
                    if (string.IsNullOrWhiteSpace(expectedRole))
                    {
                        continue;
                    }

                    int count;
                    roleCounts.TryGetValue(expectedRole, out count);
                    if (count == 0)
                    {
                        AddIssue(
                            assessment,
                            "expected_role_missing_" + expectedRole,
                            DesignPacketIntakeIssueSeverity.Warning,
                            DesignPacketIntakeIssueKind.MissingCriticalControl,
                            "expected_semantic_roles[" + i + "]",
                            "缺少关键控件或语义元素。",
                            "未在 element hints 中找到 expected semantic role: " + expectedRole,
                            "人工确认该角色是否确实缺失，或补充证据后重跑分析。",
                            true);
                    }
                    else if (count > 1 && IsSingleOwnerRole(expectedRole))
                    {
                        AddIssue(
                            assessment,
                            "expected_role_conflict_" + expectedRole,
                            DesignPacketIntakeIssueSeverity.Warning,
                            DesignPacketIntakeIssueKind.SemanticConflict,
                            "expected_semantic_roles[" + i + "]",
                            "存在语义冲突。",
                            "单实例角色出现多个候选: " + expectedRole,
                            "人工确认哪一个候选才是正式语义角色。",
                            true);
                    }
                }
            }
        }

        private static void AppendSummaryNotes(DesignPacket designPacket, DesignPacketIntakeAssessment assessment)
        {
            assessment.Notes.Add("design_images=" + SafeCount(designPacket.DesignImages));
            assessment.Notes.Add("states=" + SafeCount(designPacket.States));
            assessment.Notes.Add("rules=" + SafeCount(designPacket.Rules));
            assessment.Notes.Add("asset_slot_hints=" + SafeCount(designPacket.AssetSlotHints));
            assessment.Notes.Add("expected_semantic_roles=" + SafeCount(designPacket.ExpectedSemanticRoles));
            assessment.Notes.Add("element_hints=" + SafeCount(designPacket.ElementHints));
            assessment.Notes.Add("review_hints=" + SafeCount(designPacket.ReviewHints));
        }

        private static void ValidateReviewHints(DesignPacket designPacket, DesignPacketIntakeAssessment assessment)
        {
            if (designPacket == null || designPacket.ReviewHints == null)
            {
                return;
            }

            for (int i = 0; i < designPacket.ReviewHints.Count; i++)
            {
                DesignReviewHint hint = designPacket.ReviewHints[i];
                if (hint == null || string.IsNullOrWhiteSpace(hint.IssueKind))
                {
                    continue;
                }

                AddIssue(
                    assessment,
                    string.IsNullOrWhiteSpace(hint.HintId) ? "review_hint_" + i : hint.HintId,
                    ParseReviewHintSeverity(hint),
                    MapReviewHintKind(hint.IssueKind),
                    "review_hints[" + i + "]",
                    string.IsNullOrWhiteSpace(hint.Summary) ? "存在待人工确认的 review hint。" : hint.Summary,
                    hint.Details ?? string.Empty,
                    hint.SuggestedResolution ?? string.Empty,
                    hint.RequiresHumanDecision);
            }
        }

        private static bool IsRuleSupported(string ruleId)
        {
            return string.Equals(ruleId, "fullscreen_root", StringComparison.Ordinal) ||
                   string.Equals(ruleId, "panel_background", StringComparison.Ordinal) ||
                   string.Equals(ruleId, "title_text", StringComparison.Ordinal) ||
                   string.Equals(ruleId, "primary_button", StringComparison.Ordinal) ||
                   string.Equals(ruleId, "numeric_value_display", StringComparison.Ordinal) ||
                   string.Equals(ruleId, "task_list_scrollable", StringComparison.Ordinal) ||
                   string.Equals(ruleId, "claim_button_clickable", StringComparison.Ordinal) ||
                   string.Equals(ruleId, "claim_button_visualized", StringComparison.Ordinal);
        }

        private static bool HasLayoutConflict(UiRect rect)
        {
            if (rect == null)
            {
                return false;
            }

            if (rect.Width <= 0f || rect.Height <= 0f)
            {
                return true;
            }

            if (rect.X < 0f || rect.Y < 0f)
            {
                return true;
            }

            if (rect.X + rect.Width > 1.001f || rect.Y + rect.Height > 1.001f)
            {
                return true;
            }

            return false;
        }

        private static bool IsSingleOwnerRole(string semanticRole)
        {
            return string.Equals(semanticRole, "panel_background", StringComparison.Ordinal) ||
                   string.Equals(semanticRole, "title_text", StringComparison.Ordinal) ||
                   string.Equals(semanticRole, "primary_button", StringComparison.Ordinal) ||
                   string.Equals(semanticRole, "numeric_value_display", StringComparison.Ordinal);
        }

        private static int SafeCount<T>(List<T> items)
        {
            return items == null ? 0 : items.Count;
        }

        private static DesignPacketIntakeIssueSeverity ParseReviewHintSeverity(DesignReviewHint hint)
        {
            if (hint == null)
            {
                return DesignPacketIntakeIssueSeverity.Warning;
            }

            if (hint.BlocksSpec || string.Equals(hint.Severity, "blocking", StringComparison.OrdinalIgnoreCase))
            {
                return DesignPacketIntakeIssueSeverity.Blocking;
            }

            if (string.Equals(hint.Severity, "info", StringComparison.OrdinalIgnoreCase))
            {
                return DesignPacketIntakeIssueSeverity.Info;
            }

            return DesignPacketIntakeIssueSeverity.Warning;
        }

        private static DesignPacketIntakeIssueKind MapReviewHintKind(string issueKind)
        {
            if (string.Equals(issueKind, "low_confidence_evidence", StringComparison.OrdinalIgnoreCase))
            {
                return DesignPacketIntakeIssueKind.LowConfidenceEvidence;
            }

            if (string.Equals(issueKind, "missing_critical_control", StringComparison.OrdinalIgnoreCase))
            {
                return DesignPacketIntakeIssueKind.MissingCriticalControl;
            }

            if (string.Equals(issueKind, "layout_conflict", StringComparison.OrdinalIgnoreCase))
            {
                return DesignPacketIntakeIssueKind.LayoutConflict;
            }

            if (string.Equals(issueKind, "semantic_conflict", StringComparison.OrdinalIgnoreCase))
            {
                return DesignPacketIntakeIssueKind.SemanticConflict;
            }

            return DesignPacketIntakeIssueKind.HumanDecisionRequired;
        }

        private static void AddIssue(
            DesignPacketIntakeAssessment assessment,
            string issueId,
            DesignPacketIntakeIssueSeverity severity,
            DesignPacketIntakeIssueKind kind,
            string fieldPath,
            string summary,
            string details,
            string suggestedResolution,
            bool requiresHumanDecision)
        {
            assessment.UnresolvedItems.Add(new DesignPacketIntakeIssue
            {
                IssueId = issueId ?? string.Empty,
                Severity = severity,
                Kind = kind,
                FieldPath = fieldPath ?? string.Empty,
                Summary = summary ?? string.Empty,
                Details = details ?? string.Empty,
                SuggestedResolution = suggestedResolution ?? string.Empty,
                RequiresHumanDecision = requiresHumanDecision,
            });
        }
    }
}
