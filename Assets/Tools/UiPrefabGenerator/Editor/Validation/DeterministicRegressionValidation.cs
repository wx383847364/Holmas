using System;
using System.Text;
using UiPrefabGenerator.Core.Schema;

namespace UiPrefabGenerator.Editor.Validation
{
    public static class PrefabBindingManifestFixtureSerializer
    {
        public static string Serialize(PrefabBindingManifest manifest)
        {
            if (manifest == null)
            {
                throw new ArgumentNullException(nameof(manifest));
            }

            var builder = new StringBuilder();
            builder.AppendLine("{");
            AppendString(builder, 1, "prefab_name", manifest.PrefabName, true);
            AppendString(builder, 1, "prefab_draft_path", manifest.PrefabDraftPath, true);
            builder.AppendLine("  \"entries\": [");

            for (int i = 0; i < manifest.Entries.Count; i++)
            {
                PrefabBindingEntry entry = manifest.Entries[i] ?? new PrefabBindingEntry();
                builder.AppendLine("    {");
                AppendString(builder, 3, "node_path", entry.NodePath, true);
                AppendString(builder, 3, "component_type", entry.ComponentType, true);
                AppendString(builder, 3, "binding_key", entry.BindingKey, true);
                AppendString(builder, 3, "asset_slot", entry.AssetSlot, true);
                AppendString(builder, 3, "event_name", entry.EventName, true);
                AppendBoolean(builder, 3, "requires_manual_wiring", entry.RequiresManualWiring, true);
                AppendString(builder, 3, "notes", entry.Notes, false);
                builder.Append("    }");
                builder.AppendLine(i == manifest.Entries.Count - 1 ? string.Empty : ",");
            }

            builder.AppendLine("  ]");
            builder.Append('}');
            return builder.ToString();
        }

        private static void AppendString(StringBuilder builder, int indent, string name, string value, bool trailingComma)
        {
            builder.Append(new string(' ', indent * 2));
            builder.Append('"');
            builder.Append(name);
            builder.Append("\": ");
            builder.Append('"');
            builder.Append(Escape(value));
            builder.Append('"');
            if (trailingComma)
            {
                builder.Append(',');
            }

            builder.AppendLine();
        }

        private static void AppendBoolean(StringBuilder builder, int indent, string name, bool value, bool trailingComma)
        {
            builder.Append(new string(' ', indent * 2));
            builder.Append('"');
            builder.Append(name);
            builder.Append("\": ");
            builder.Append(value ? "true" : "false");
            if (trailingComma)
            {
                builder.Append(',');
            }

            builder.AppendLine();
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"");
        }
    }

    public static class DeterministicRegressionValidator
    {
        public static UiPrefabValidationResult CompareGoldenText(string actual, string expected)
        {
            var result = new UiPrefabValidationResult();
            string normalizedActual = Normalize(actual);
            string normalizedExpected = Normalize(expected);
            if (string.Equals(normalizedActual, normalizedExpected, StringComparison.Ordinal))
            {
                return result;
            }

            result.Issues.Add(new UiPrefabValidationIssue
            {
                Severity = UiPrefabValidationIssueSeverity.Error,
                Category = UiPrefabValidationIssueCategory.Fixture,
                FieldPath = "golden_fixture",
                Message = "生成结果与 golden fixture 不一致。",
            });
            return result;
        }

        private static string Normalize(string text)
        {
            return (text ?? string.Empty).Replace("\r\n", "\n").Trim();
        }
    }
}
