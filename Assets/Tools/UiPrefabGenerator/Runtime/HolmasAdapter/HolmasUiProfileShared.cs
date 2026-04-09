using System.Collections.Generic;

namespace UiPrefabGenerator.HolmasAdapter
{
    internal static class HolmasUiProfileShared
    {
        public const string TmpLegacyDirectory = "Assets/HotUpdateContent/Script/App.HotUpdate/Holmas/UI/Tool/Tmp";

        private static readonly string[] AllowedTypes =
        {
            "RectTransform",
            "CanvasGroup",
            "Image",
            "RawImage",
            "Text",
            "TextMeshProUGUI",
            "TMP_Text",
            "Button",
            "Toggle",
            "Slider",
            "Scrollbar",
            "ScrollRect",
            "InputField",
            "TMP_InputField",
            "TMP_Dropdown",
            "Mask",
            "RectMask2D",
            "HorizontalLayoutGroup",
            "VerticalLayoutGroup",
            "GridLayoutGroup",
            "LayoutElement",
            "ContentSizeFitter"
        };

        public static IReadOnlyCollection<string> AllowedComponentTypes
        {
            get { return AllowedTypes; }
        }

        public static string BuildDraftPrefabPath(string draftPrefabRoot, string prefabName)
        {
            return NormalizePath(draftPrefabRoot) + "/" + (prefabName ?? string.Empty) + ".prefab";
        }

        public static bool IsDraftPrefabPathWithinAllowedRoot(string prefabDraftPath, string draftPrefabRoot)
        {
            string normalizedRoot = NormalizePath(draftPrefabRoot);
            string normalizedDraftPath = NormalizePath(prefabDraftPath);
            if (string.IsNullOrWhiteSpace(normalizedRoot) || string.IsNullOrWhiteSpace(normalizedDraftPath))
            {
                return false;
            }

            if (!normalizedDraftPath.StartsWith(normalizedRoot, System.StringComparison.Ordinal))
            {
                return false;
            }

            if (normalizedDraftPath.Length == normalizedRoot.Length)
            {
                return true;
            }

            return normalizedDraftPath[normalizedRoot.Length] == '/';
        }

        public static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            return path.Replace('\\', '/').TrimEnd('/');
        }
    }
}
