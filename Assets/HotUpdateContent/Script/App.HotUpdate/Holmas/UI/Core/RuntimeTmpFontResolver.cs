using System;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace App.HotUpdate.Holmas.UI.Core
{
    /// <summary>
    /// 运行时中文字体兜底。
    /// 优先使用项目内受控字体资源；只有在开发机缺资源时，才退回系统字体做本地临时兜底。
    /// </summary>
    public static class RuntimeTmpFontResolver
    {
        public const string ProjectChineseFontAssetPath = "Assets/HotUpdateContent/Res/Fonts/NotoSansSC.ttf";

        // 只有项目内字体资源缺失时，才尝试这些系统字体作为开发机临时兜底。
        private static readonly string[] PreferredChineseFontNames =
        {
            "Noto Sans CJK SC",
            "Noto Sans SC",
            "Source Han Sans SC",
            "PingFang SC",
            "Hiragino Sans GB",
            "Songti SC",
            "STHeiti",
            "Microsoft YaHei UI",
            "Microsoft YaHei",
            "SimHei",
            "Arial Unicode MS",
        };

        private const string DefaultChineseSample = "主界面已就绪开始找猫宣传升级返回当前没有活跃任务未进入棋盘";

        private static Font _projectChineseFont;
        private static Font _systemFallbackFont;
        private static bool _attemptedSystemFontLoad;
        private static bool _loggedSystemFallbackWarning;
        private static TMP_FontAsset _projectChineseTmpFontAsset;
        private static TMP_FontAsset _systemFallbackTmpFontAsset;

        public static void SetProjectChineseFont(Font font)
        {
            _projectChineseFont = font;
            _projectChineseTmpFontAsset = null;
        }

        public static void EnsureFontSupportsText(TMP_Text text, string contentSample = null)
        {
            if (text == null)
            {
                return;
            }

            string sample = string.IsNullOrWhiteSpace(contentSample) ? DefaultChineseSample : contentSample;
            TMP_FontAsset resolved = ResolveFontAsset(text.font, sample);
            if (resolved != null && !ReferenceEquals(text.font, resolved))
            {
                // 只在当前字体不支持目标文本时才替换，尽量不影响已有 prefab 上显式配置的字体。
                text.font = resolved;
            }
        }

        public static void EnsureFontSupportsText(Text text, string contentSample = null)
        {
            if (text == null)
            {
                return;
            }

            Font resolved = GetPreferredUnityFont();
            if (resolved != null && !ReferenceEquals(text.font, resolved))
            {
                text.font = resolved;
            }
        }

        private static TMP_FontAsset ResolveFontAsset(TMP_FontAsset current, string sample)
        {
            // 顺序是：
            // 1. 当前字体自己能否覆盖；
            // 2. TMP 默认字体是否能覆盖；
            // 3. 项目内受控中文字体；
            // 4. 最后才是开发机系统字体兜底。
            if (SupportsText(current, sample))
            {
                return current;
            }

            TMP_FontAsset defaultAsset = TMP_Settings.defaultFontAsset;
            if (SupportsText(defaultAsset, sample))
            {
                return defaultAsset;
            }

            TMP_FontAsset projectAsset = GetOrCreateProjectChineseFontAsset();
            if (SupportsText(projectAsset, sample))
            {
                return projectAsset;
            }

            TMP_FontAsset systemFallbackAsset = GetOrCreateSystemFallbackTmpFontAsset();
            if (SupportsText(systemFallbackAsset, sample))
            {
                return systemFallbackAsset;
            }

            return current ?? defaultAsset ?? projectAsset ?? systemFallbackAsset;
        }

        private static Font GetPreferredUnityFont()
        {
            Font projectFont = GetProjectChineseFont();
            if (projectFont != null)
            {
                return projectFont;
            }

            return GetSystemFallbackFont();
        }

        private static Font GetProjectChineseFont()
        {
            if (_projectChineseFont != null)
            {
                return _projectChineseFont;
            }

            return _projectChineseFont;
        }

        private static Font GetSystemFallbackFont()
        {
#if !UNITY_EDITOR
            // 发布路径只认项目内受控字体资源，避免目标端再次退回不可控的系统字体。
            return null;
#else
            if (_systemFallbackFont != null)
            {
                return _systemFallbackFont;
            }

            if (_attemptedSystemFontLoad)
            {
                return null;
            }

            _attemptedSystemFontLoad = true;
            try
            {
                _systemFallbackFont = Font.CreateDynamicFontFromOSFont(PreferredChineseFontNames, 36);
                if (_systemFallbackFont != null && !_loggedSystemFallbackWarning)
                {
                    _loggedSystemFallbackWarning = true;
                    Debug.LogWarning(
                        "RuntimeTmpFontResolver: project font asset " + ProjectChineseFontAssetPath + " is unavailable. " +
                        "Falling back to an OS font for local/dev use only.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"RuntimeTmpFontResolver: failed to create system font fallback. {ex.Message}");
            }

            return _systemFallbackFont;
#endif
        }

        private static TMP_FontAsset GetOrCreateProjectChineseFontAsset()
        {
            if (_projectChineseTmpFontAsset != null)
            {
                return _projectChineseTmpFontAsset;
            }

            Font projectFont = GetProjectChineseFont();
            if (projectFont == null)
            {
                return null;
            }

            _projectChineseTmpFontAsset = CreateDynamicTmpFontAsset(projectFont, "Holmas Project Chinese Fallback");
            return _projectChineseTmpFontAsset;
        }

        private static TMP_FontAsset GetOrCreateSystemFallbackTmpFontAsset()
        {
            if (_systemFallbackTmpFontAsset != null)
            {
                return _systemFallbackTmpFontAsset;
            }

            Font fallbackFont = GetSystemFallbackFont();
            if (fallbackFont == null)
            {
                return null;
            }

            _systemFallbackTmpFontAsset = CreateDynamicTmpFontAsset(fallbackFont, "Holmas System Chinese Fallback");
            return _systemFallbackTmpFontAsset;
        }

        private static TMP_FontAsset CreateDynamicTmpFontAsset(Font sourceFont, string assetName)
        {
            if (sourceFont == null)
            {
                return null;
            }

            try
            {
                TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
                    sourceFont,
                    90,
                    9,
                    GlyphRenderMode.SDFAA,
                    1024,
                    1024,
                    AtlasPopulationMode.Dynamic,
                    true);

                if (fontAsset != null)
                {
                    fontAsset.name = assetName;
                    fontAsset.hideFlags = HideFlags.DontUnloadUnusedAsset;
                    fontAsset.TryAddCharacters(DefaultChineseSample, true);
                }

                return fontAsset;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"RuntimeTmpFontResolver: failed to create TMP font asset {assetName}. {ex.Message}");
                return null;
            }
        }

        private static bool SupportsText(TMP_FontAsset fontAsset, string sample)
        {
            if (fontAsset == null || string.IsNullOrEmpty(sample))
            {
                return false;
            }

            // 用 HasCharacter 做逐字校验，确保不是“字体存在但中文仍缺字形”的假可用状态。
            for (int i = 0; i < sample.Length; i++)
            {
                char ch = sample[i];
                if (char.IsWhiteSpace(ch) || char.IsControl(ch))
                {
                    continue;
                }

                if (!fontAsset.HasCharacter(ch, true, true))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
