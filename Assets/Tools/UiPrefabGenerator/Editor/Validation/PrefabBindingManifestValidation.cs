using UiPrefabGenerator.Core.Profile;
using UiPrefabGenerator.Core.Schema;
using System;
using System.Collections.Generic;

namespace UiPrefabGenerator.Editor.Validation
{
    public interface IPrefabBindingManifestValidator
    {
        UiPrefabValidationResult Validate(PrefabBindingManifest manifest, IProjectUiProfile profile);
    }

    public sealed class DefaultPrefabBindingManifestValidator : IPrefabBindingManifestValidator
    {
        public UiPrefabValidationResult Validate(PrefabBindingManifest manifest, IProjectUiProfile profile)
        {
            var result = new UiPrefabValidationResult();
            if (manifest == null)
            {
                AddIssue(result, UiPrefabValidationIssueSeverity.Error, UiPrefabValidationIssueCategory.Generator, "manifest", "PrefabBindingManifest 不能为空。");
                return result;
            }

            if (string.IsNullOrWhiteSpace(manifest.PrefabName))
            {
                AddIssue(result, UiPrefabValidationIssueSeverity.Error, UiPrefabValidationIssueCategory.Generator, "prefab_name", "PrefabName 不能为空。");
            }

            if (string.IsNullOrWhiteSpace(manifest.PrefabDraftPath))
            {
                AddIssue(result, UiPrefabValidationIssueSeverity.Error, UiPrefabValidationIssueCategory.Generator, "prefab_draft_path", "PrefabDraftPath 不能为空。");
            }

            if (manifest.Entries == null || manifest.Entries.Count == 0)
            {
                AddIssue(result, UiPrefabValidationIssueSeverity.Error, UiPrefabValidationIssueCategory.Generator, "entries", "Entries 不能为空。");
                return result;
            }

            if (profile != null && !string.IsNullOrWhiteSpace(manifest.PrefabDraftPath))
            {
                string root = (profile.DraftPrefabRoot ?? string.Empty).TrimEnd('/');
                if (!string.IsNullOrWhiteSpace(root) && !manifest.PrefabDraftPath.StartsWith(root + "/", System.StringComparison.Ordinal))
                {
                    AddIssue(result, UiPrefabValidationIssueSeverity.Error, UiPrefabValidationIssueCategory.Adapter, "prefab_draft_path", "生成路径不在 profile 允许目录内。");
                }
            }

            var allowedComponentTypes = new HashSet<string>(StringComparer.Ordinal);
            if (profile != null && profile.AllowedComponentTypes != null)
            {
                foreach (string componentType in profile.AllowedComponentTypes)
                {
                    if (!string.IsNullOrWhiteSpace(componentType))
                    {
                        allowedComponentTypes.Add(componentType);
                    }
                }
            }

            var entrySignatures = new HashSet<string>(StringComparer.Ordinal);
            var nodeComponentKeys = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < manifest.Entries.Count; i++)
            {
                PrefabBindingEntry entry = manifest.Entries[i];
                if (entry == null)
                {
                    AddIssue(result, UiPrefabValidationIssueSeverity.Error, UiPrefabValidationIssueCategory.Generator, "entries[" + i + "]", "Entry 不能为空。");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(entry.NodePath))
                {
                    AddIssue(result, UiPrefabValidationIssueSeverity.Error, UiPrefabValidationIssueCategory.Generator, "entries[" + i + "].node_path", "NodePath 不能为空。");
                }

                if (string.IsNullOrWhiteSpace(entry.ComponentType))
                {
                    AddIssue(result, UiPrefabValidationIssueSeverity.Error, UiPrefabValidationIssueCategory.Generator, "entries[" + i + "].component_type", "ComponentType 不能为空。");
                }
                else
                {
                    if (allowedComponentTypes.Count > 0 && !allowedComponentTypes.Contains(entry.ComponentType))
                    {
                        AddIssue(result, UiPrefabValidationIssueSeverity.Error, UiPrefabValidationIssueCategory.Adapter, "entries[" + i + "].component_type", "ComponentType 不在当前 profile 白名单内。");
                    }

                    if (RequiresAssetSlot(entry.ComponentType) && string.IsNullOrWhiteSpace(entry.AssetSlot))
                    {
                        AddIssue(result, UiPrefabValidationIssueSeverity.Error, UiPrefabValidationIssueCategory.Generator, "entries[" + i + "].asset_slot", "当前组件缺少 asset_slot。");
                    }
                }

                if (!string.IsNullOrWhiteSpace(entry.EventName) && !entry.RequiresManualWiring)
                {
                    AddIssue(result, UiPrefabValidationIssueSeverity.Error, UiPrefabValidationIssueCategory.Generator, "entries[" + i + "].requires_manual_wiring", "存在 event_name 时必须标记 requires_manual_wiring。");
                }

                if (entry.RequiresManualWiring && string.IsNullOrWhiteSpace(entry.EventName))
                {
                    AddIssue(result, UiPrefabValidationIssueSeverity.Warning, UiPrefabValidationIssueCategory.Generator, "entries[" + i + "].event_name", "标记了 requires_manual_wiring 但未声明 event_name。");
                }

                string exactEntrySignature = BuildExactEntrySignature(entry);
                if (!entrySignatures.Add(exactEntrySignature))
                {
                    AddIssue(result, UiPrefabValidationIssueSeverity.Error, UiPrefabValidationIssueCategory.Generator, "entries[" + i + "]", "检测到重复 manifest entry。");
                }

                if (!string.IsNullOrWhiteSpace(entry.NodePath) && !string.IsNullOrWhiteSpace(entry.ComponentType))
                {
                    string nodeComponentKey = entry.NodePath + "|" + entry.ComponentType;
                    if (!nodeComponentKeys.Add(nodeComponentKey))
                    {
                        AddIssue(result, UiPrefabValidationIssueSeverity.Error, UiPrefabValidationIssueCategory.Generator, "entries[" + i + "]", "检测到相同 node_path 与 component_type 的命名冲突。");
                    }
                }
            }

            return result;
        }

        private static bool RequiresAssetSlot(string componentType)
        {
            return string.Equals(componentType, "Image", StringComparison.Ordinal) ||
                   string.Equals(componentType, "RawImage", StringComparison.Ordinal);
        }

        private static string BuildExactEntrySignature(PrefabBindingEntry entry)
        {
            if (entry == null)
            {
                return string.Empty;
            }

            return string.Join(
                "|",
                new[]
                {
                    entry.NodePath ?? string.Empty,
                    entry.ComponentType ?? string.Empty,
                    entry.BindingKey ?? string.Empty,
                    entry.AssetSlot ?? string.Empty,
                    entry.EventName ?? string.Empty,
                    entry.RequiresManualWiring ? "1" : "0",
                    entry.Notes ?? string.Empty
                });
        }

        private static void AddIssue(
            UiPrefabValidationResult result,
            UiPrefabValidationIssueSeverity severity,
            UiPrefabValidationIssueCategory category,
            string fieldPath,
            string message)
        {
            result.Issues.Add(new UiPrefabValidationIssue
            {
                Severity = severity,
                Category = category,
                FieldPath = fieldPath ?? string.Empty,
                Message = message ?? string.Empty,
            });
        }
    }
}
