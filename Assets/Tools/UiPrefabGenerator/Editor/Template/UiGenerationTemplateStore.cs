using System;
using System.IO;
using UiPrefabGenerator.Core.Profile;
using UiPrefabGenerator.Editor.Bridge;

namespace UiPrefabGenerator.Editor.Template
{
    public static class UiGenerationTemplateStore
    {
        public static string DefaultTemplateAssetPath
        {
            get { return UiGenerationDataPaths.DefaultPortraitTemplatePath; }
        }

        public static UiGenerationTemplate BuildPortraitWechatDefault()
        {
            var template = new UiGenerationTemplate
            {
                TemplateName = "holmas_portrait_wechat_default",
                ProfileId = "holmas_ugui_portrait",
                TargetPlatform = "wechat_minigame",
                Orientation = "portrait",
                ReferenceResolutionWidth = 1080,
                ReferenceResolutionHeight = 1920,
                CanvasScaleMode = "ScaleWithScreenSize",
                MatchMode = "match_height",
                MatchWidthOrHeight = 1f,
                SafeAreaMode = "simulate_mobile",
                RootLayoutMode = "fullscreen_mobile",
                PageType = "mobile_fullscreen",
                VisualDensity = "normal",
                AssetRoot = "Assets/Res",
                DraftPrefabRoot = "Assets/Res/Perfabs/Generated/Holmas/Portrait",
                RuntimeBindingNamespace = "App.HotUpdate.Holmas.UI.Generated",
                ResourceMatchStrictness = "balanced",
                NodeNameStyle = "PascalCase",
                BindingKeyStyle = "snake_case",
                TextStrategy = "placeholder_only",
                ManualReviewRequired = true,
                AutoPingPrefabAfterGeneration = true,
                AutoOpenPreviewAfterGeneration = false,
                IgnoreDecorativeElements = true,
                Notes = "Default portrait template for WeChat mini-game style UGUI generation."
            };
            template.ResourceSearchExtensions.Add(".png");
            template.ResourceSearchExtensions.Add(".jpg");
            template.ResourceSearchExtensions.Add(".prefab");
            template.ResourceSearchExtensions.Add(".asset");
            return template;
        }

        public static void EnsureDefaultTemplateExists()
        {
            UiGenerationDataPaths.EnsureDataFolders();
            if (File.Exists(UiGenerationDataPaths.ToAbsolutePath(UiGenerationDataPaths.DefaultPortraitTemplatePath)))
            {
                return;
            }

            SaveTemplate(UiGenerationDataPaths.DefaultPortraitTemplatePath, BuildPortraitWechatDefault());
        }

        public static string[] GetTemplateAssetPaths()
        {
            EnsureDefaultTemplateExists();
            string absoluteRoot = UiGenerationDataPaths.ToAbsolutePath(UiGenerationDataPaths.TemplatesRoot);
            if (!Directory.Exists(absoluteRoot))
            {
                return Array.Empty<string>();
            }

            string[] files = Directory.GetFiles(absoluteRoot, "*.json", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                files[i] = UiGenerationDataPaths.ToAssetPath(files[i]);
            }

            Array.Sort(files, StringComparer.Ordinal);
            return files;
        }

        public static UiGenerationTemplate LoadTemplate(string assetPath)
        {
            UiGenerationTemplate template;
            if (UiGenerationJsonFileUtility.TryLoadJson(assetPath, out template) && template != null)
            {
                return template;
            }

            return BuildPortraitWechatDefault();
        }

        public static void SaveTemplate(string assetPath, UiGenerationTemplate template)
        {
            if (template == null)
            {
                throw new ArgumentNullException(nameof(template));
            }

            UiGenerationJsonFileUtility.SaveJson(assetPath, template);
        }
    }
}
