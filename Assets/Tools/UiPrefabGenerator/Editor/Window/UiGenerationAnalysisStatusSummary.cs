using System;
using System.Collections.Generic;
using UiPrefabGenerator.Core.ResourceMatch;
using UiPrefabGenerator.Core.Result;
using UnityEditor;

namespace UiPrefabGenerator.Editor.Window
{
    public enum UiGenerationAnalysisStateKind
    {
        Success,
        SuccessWithWarnings,
        SuccessWithUnresolvedItems,
        Failed
    }

    public sealed class UiGenerationAnalysisStatusSummary
    {
        public UiGenerationAnalysisStateKind StateKind;
        public string StatusLabel = string.Empty;
        public string StatusSummary = string.Empty;
        public MessageType StatusMessageType = MessageType.None;
        public int NodeCount;
        public int BindingCount;
        public int InteractionCount;
        public int MatchCount;
        public int SelectedMatchCount;
        public int UnresolvedItemCount;
        public int UnresolvedSlotCount;
        public int WarningCount;
        public int ErrorCount;
        public bool IsWeakResult;
        public string WeakResultSummary = string.Empty;
        public List<string> UnresolvedSummaries = new List<string>();
        public List<string> WarningSummaries = new List<string>();
        public List<string> ErrorSummaries = new List<string>();
    }

    public static class UiGenerationAnalysisStatusSummarizer
    {
        public static UiGenerationAnalysisStatusSummary Build(UiGenerationAnalysisResult result)
        {
            var summary = new UiGenerationAnalysisStatusSummary();
            if (result == null)
            {
                summary.StateKind = UiGenerationAnalysisStateKind.Failed;
                summary.StatusLabel = "失败";
                summary.StatusSummary = "分析结果为空。";
                summary.StatusMessageType = MessageType.Error;
                summary.ErrorCount = 1;
                summary.ErrorSummaries.Add("分析结果为空。");
                return summary;
            }

            UiResourceMatchReport matchReport = result.ResourceMatchReport ?? new UiResourceMatchReport();
            summary.NodeCount = result.UiPrefabSpec != null && result.UiPrefabSpec.Nodes != null
                ? result.UiPrefabSpec.Nodes.Count
                : 0;
            summary.BindingCount = result.UiPrefabSpec != null && result.UiPrefabSpec.Bindings != null
                ? result.UiPrefabSpec.Bindings.Count
                : 0;
            summary.InteractionCount = result.UiPrefabSpec != null && result.UiPrefabSpec.Interactions != null
                ? result.UiPrefabSpec.Interactions.Count
                : 0;
            summary.MatchCount = matchReport.Matches != null ? matchReport.Matches.Count : 0;
            summary.SelectedMatchCount = CountSelectedMatches(matchReport.Matches);
            summary.UnresolvedItemCount = result.UnresolvedItems != null ? result.UnresolvedItems.Count : 0;
            summary.UnresolvedSlotCount = matchReport.UnresolvedSlots != null ? matchReport.UnresolvedSlots.Count : 0;
            summary.WarningCount = SafeCount(result.Warnings) + SafeCount(matchReport.Warnings);
            summary.ErrorCount = SafeCount(result.Errors);

            AddUnique(summary.UnresolvedSummaries, result.UnresolvedItems, null);
            AddUnique(summary.UnresolvedSummaries, matchReport.UnresolvedSlots, "未匹配资源槽：");
            AddUnique(summary.WarningSummaries, result.Warnings, null);
            AddUnique(summary.WarningSummaries, matchReport.Warnings, null);
            AddUnique(summary.ErrorSummaries, result.Errors, null);

            bool hasErrors = !result.Success || summary.ErrorCount > 0;
            bool hasUnresolved = summary.UnresolvedItemCount > 0 || summary.UnresolvedSlotCount > 0;
            bool hasWarnings = summary.WarningCount > 0;

            if (hasErrors)
            {
                summary.StateKind = UiGenerationAnalysisStateKind.Failed;
                summary.StatusLabel = "失败";
                summary.StatusMessageType = MessageType.Error;
            }
            else if (hasUnresolved)
            {
                summary.StateKind = UiGenerationAnalysisStateKind.SuccessWithUnresolvedItems;
                summary.StatusLabel = "成功但有未解决项";
                summary.StatusMessageType = MessageType.Warning;
            }
            else if (hasWarnings)
            {
                summary.StateKind = UiGenerationAnalysisStateKind.SuccessWithWarnings;
                summary.StatusLabel = "成功（有警告）";
                summary.StatusMessageType = MessageType.Warning;
            }
            else
            {
                summary.StateKind = UiGenerationAnalysisStateKind.Success;
                summary.StatusLabel = "成功";
                summary.StatusMessageType = MessageType.Info;
            }

            List<string> weakReasons = BuildWeakReasons(summary, matchReport.Matches);
            summary.IsWeakResult = result.Success && summary.ErrorCount == 0 && weakReasons.Count > 0;
            summary.WeakResultSummary = weakReasons.Count > 0 ? string.Join("；", weakReasons.ToArray()) : "结果强度正常。";
            summary.StatusSummary = string.Format(
                "分析状态：{0}。未解决项 {1}，未匹配资源槽 {2}，警告 {3}，错误 {4}。",
                summary.StatusLabel,
                summary.UnresolvedItemCount,
                summary.UnresolvedSlotCount,
                summary.WarningCount,
                summary.ErrorCount);
            return summary;
        }

        private static List<string> BuildWeakReasons(
            UiGenerationAnalysisStatusSummary summary,
            List<UiAssetSlotMatch> matches)
        {
            var reasons = new List<string>();
            if (summary.NodeCount <= 1)
            {
                reasons.Add(string.Format("仅识别到 {0} 个节点", summary.NodeCount));
            }

            if (summary.MatchCount > 0)
            {
                if (summary.SelectedMatchCount == 0)
                {
                    reasons.Add(string.Format("0/{0} 个资源槽完成自动选中", summary.MatchCount));
                }
                else if (summary.SelectedMatchCount * 2 < summary.MatchCount)
                {
                    reasons.Add(string.Format("仅 {0}/{1} 个资源槽完成自动选中", summary.SelectedMatchCount, summary.MatchCount));
                }
            }

            if (summary.UnresolvedSlotCount > 0)
            {
                reasons.Add(string.Format("存在 {0} 个未匹配资源槽", summary.UnresolvedSlotCount));
            }

            float averageSelectedConfidence = GetAverageSelectedConfidence(matches);
            if (averageSelectedConfidence > 0f && averageSelectedConfidence < 0.35f)
            {
                reasons.Add(string.Format("已选中资源平均置信度仅 {0:0.00}", averageSelectedConfidence));
            }

            return reasons;
        }

        private static float GetAverageSelectedConfidence(List<UiAssetSlotMatch> matches)
        {
            if (matches == null || matches.Count == 0)
            {
                return 0f;
            }

            float totalConfidence = 0f;
            int selectedCount = 0;
            for (int i = 0; i < matches.Count; i++)
            {
                UiAssetSlotMatch match = matches[i];
                if (match == null || string.IsNullOrWhiteSpace(match.SelectedAssetPath))
                {
                    continue;
                }

                totalConfidence += match.Confidence;
                selectedCount++;
            }

            return selectedCount > 0 ? totalConfidence / selectedCount : 0f;
        }

        private static int CountSelectedMatches(List<UiAssetSlotMatch> matches)
        {
            if (matches == null)
            {
                return 0;
            }

            int selectedCount = 0;
            for (int i = 0; i < matches.Count; i++)
            {
                UiAssetSlotMatch match = matches[i];
                if (match != null && !string.IsNullOrWhiteSpace(match.SelectedAssetPath))
                {
                    selectedCount++;
                }
            }

            return selectedCount;
        }

        private static int SafeCount<T>(List<T> items)
        {
            return items != null ? items.Count : 0;
        }

        private static void AddUnique(List<string> target, List<string> source, string prefix)
        {
            if (target == null || source == null)
            {
                return;
            }

            for (int i = 0; i < source.Count; i++)
            {
                string item = source[i];
                if (string.IsNullOrWhiteSpace(item))
                {
                    continue;
                }

                string value = string.IsNullOrWhiteSpace(prefix) ? item : prefix + item;
                if (!target.Contains(value))
                {
                    target.Add(value);
                }
            }
        }
    }
}
