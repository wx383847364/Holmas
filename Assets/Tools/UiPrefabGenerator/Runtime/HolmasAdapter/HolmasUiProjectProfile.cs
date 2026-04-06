using System.Collections.Generic;
using UiPrefabGenerator.Core.Profile;

namespace UiPrefabGenerator.HolmasAdapter
{
    public sealed class HolmasUiProjectProfile : IProjectUiProfile
    {
        private static readonly string[] AllowedTypes =
        {
            "RectTransform",
            "CanvasGroup",
            "Image",
            "RawImage",
            "Text",
            "Button",
            "Toggle",
            "Slider",
            "Scrollbar",
            "ScrollRect",
            "InputField",
            "Mask",
            "RectMask2D",
            "HorizontalLayoutGroup",
            "VerticalLayoutGroup",
            "GridLayoutGroup",
            "LayoutElement",
            "ContentSizeFitter"
        };

        public string ProfileId
        {
            get { return "holmas_ugui"; }
        }

        public string DraftPrefabRoot
        {
            get { return "Assets/Res/Perfabs/Generated/Holmas"; }
        }

        public string RuntimeBindingNamespace
        {
            get { return "App.HotUpdate.Holmas.UI.Generated"; }
        }

        public IReadOnlyCollection<string> AllowedComponentTypes
        {
            get { return AllowedTypes; }
        }

        public string BuildDraftPrefabPath(string prefabName)
        {
            return NormalizePath(DraftPrefabRoot) + "/" + (prefabName ?? string.Empty) + ".prefab";
        }

        public bool IsDraftPrefabPathWithinAllowedRoot(string prefabDraftPath)
        {
            string normalizedRoot = NormalizePath(DraftPrefabRoot);
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

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            return path.Replace('\\', '/').TrimEnd('/');
        }
    }
}
