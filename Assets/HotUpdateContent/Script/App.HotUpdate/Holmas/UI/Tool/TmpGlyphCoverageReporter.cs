using System.Collections.Generic;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEngine;

namespace App.HotUpdate.Holmas.UI.Tool
{
    /// <summary>
    /// 在 TMP 赋值前主动检查缺字，额外输出更直观的字符提示。
    /// </summary>
    public static class TmpGlyphCoverageReporter
    {
        private static readonly FieldInfo WarningsDisabledField =
            typeof(TMP_Settings).GetField("m_warningsDisabled", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly HashSet<string> ReportedMissingGlyphs = new HashSet<string>();
        private static bool warningsDisabledApplied;

        public static void SetText(TMP_Text textComponent, string value)
        {
            if (textComponent == null)
            {
                return;
            }

            DisableTmpDefaultWarnings();

            string safeValue = value ?? string.Empty;
            if (textComponent.text == safeValue)
            {
                return;
            }

            ReportMissingGlyphs(textComponent, safeValue);
            textComponent.text = safeValue;
        }

        private static void DisableTmpDefaultWarnings()
        {
            if (warningsDisabledApplied)
            {
                return;
            }

            TMP_Settings settings = TMP_Settings.instance;
            if (settings == null || WarningsDisabledField == null)
            {
                return;
            }

            WarningsDisabledField.SetValue(settings, true);
            warningsDisabledApplied = true;
        }

        private static void ReportMissingGlyphs(TMP_Text textComponent, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            TMP_FontAsset sourceFontAsset = textComponent.font ?? TMP_Settings.defaultFontAsset;
            if (sourceFontAsset == null)
            {
                return;
            }

            var seenUnicode = new HashSet<uint>();
            var missingChars = new StringBuilder();
            var unicodeList = new StringBuilder();

            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                if (char.IsControl(character) || char.IsWhiteSpace(character))
                {
                    continue;
                }

                uint unicode = character;
                if (!seenUnicode.Add(unicode))
                {
                    continue;
                }

                if (HasGlyph(textComponent, sourceFontAsset, unicode))
                {
                    continue;
                }

                if (missingChars.Length > 0)
                {
                    missingChars.Append(' ');
                    unicodeList.Append(", ");
                }

                missingChars.Append('\'').Append(character).Append('\'');
                unicodeList.Append("U+").Append(unicode.ToString("X4"));
            }

            if (missingChars.Length == 0)
            {
                return;
            }

            string reportKey = textComponent.GetInstanceID() + "|" + sourceFontAsset.GetInstanceID() + "|" + unicodeList;
            if (!ReportedMissingGlyphs.Add(reportKey))
            {
                return;
            }

            Debug.LogWarningFormat(
                textComponent,
                "TmpGlyphCoverageReporter: text object [{0}] using font [{1}] is missing glyphs {2}. Unicode: {3}. Text=\"{4}\"",
                GetTransformPath(textComponent.transform),
                sourceFontAsset.name,
                missingChars,
                unicodeList,
                value);
        }

        private static bool HasGlyph(TMP_Text textComponent, TMP_FontAsset sourceFontAsset, uint unicode)
        {
            bool isAlternativeTypeface;
            if (TMP_FontAssetUtilities.GetCharacterFromFontAsset(
                    unicode,
                    sourceFontAsset,
                    true,
                    textComponent.fontStyle,
                    textComponent.fontWeight,
                    out isAlternativeTypeface) != null)
            {
                return true;
            }

            if (TMP_Settings.fallbackFontAssets != null &&
                TMP_Settings.fallbackFontAssets.Count > 0 &&
                TMP_FontAssetUtilities.GetCharacterFromFontAssets(
                    unicode,
                    sourceFontAsset,
                    TMP_Settings.fallbackFontAssets,
                    true,
                    textComponent.fontStyle,
                    textComponent.fontWeight,
                    out isAlternativeTypeface) != null)
            {
                return true;
            }

            TMP_FontAsset defaultFontAsset = TMP_Settings.defaultFontAsset;
            if (defaultFontAsset != null &&
                defaultFontAsset != sourceFontAsset &&
                TMP_FontAssetUtilities.GetCharacterFromFontAsset(
                    unicode,
                    defaultFontAsset,
                    true,
                    textComponent.fontStyle,
                    textComponent.fontWeight,
                    out isAlternativeTypeface) != null)
            {
                return true;
            }

            return false;
        }

        private static string GetTransformPath(Transform target)
        {
            if (target == null)
            {
                return "<null>";
            }

            var pathBuilder = new StringBuilder(target.name);
            Transform current = target.parent;
            while (current != null)
            {
                pathBuilder.Insert(0, '/');
                pathBuilder.Insert(0, current.name);
                current = current.parent;
            }

            return pathBuilder.ToString();
        }
    }
}
