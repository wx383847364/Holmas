using System;
using System.Collections.Generic;

namespace UiPrefabGenerator.Core.Profile
{
    [Serializable]
    public sealed class UiGenerationTemplate
    {
        public string TemplateName = string.Empty;
        public string ProfileId = string.Empty;
        public string TargetPlatform = string.Empty;
        public string Orientation = string.Empty;
        public int ReferenceResolutionWidth;
        public int ReferenceResolutionHeight;
        public string CanvasScaleMode = string.Empty;
        public string MatchMode = string.Empty;
        public float MatchWidthOrHeight;
        public string SafeAreaMode = string.Empty;
        public UiSafeAreaInsets SafeAreaInsets = new UiSafeAreaInsets();
        public string RootLayoutMode = string.Empty;
        public string PageType = string.Empty;
        public string VisualDensity = string.Empty;
        public string AssetRoot = string.Empty;
        public string DraftPrefabRoot = string.Empty;
        public string RuntimeBindingNamespace = string.Empty;
        public string ResourceMatchStrictness = string.Empty;
        public string NodeNameStyle = string.Empty;
        public string BindingKeyStyle = string.Empty;
        public string TextStrategy = string.Empty;
        public bool ManualReviewRequired = true;
        public bool AutoPingPrefabAfterGeneration = true;
        public bool AutoOpenPreviewAfterGeneration;
        public bool IgnoreDecorativeElements = true;
        public List<string> ResourceSearchExtensions = new List<string>();
        public List<string> MustHaveNodes = new List<string>();
        public List<string> MustHaveInteractions = new List<string>();
        public string Notes = string.Empty;
    }

    [Serializable]
    public sealed class UiSafeAreaInsets
    {
        public float Top;
        public float Bottom;
        public float Left;
        public float Right;
    }
}
