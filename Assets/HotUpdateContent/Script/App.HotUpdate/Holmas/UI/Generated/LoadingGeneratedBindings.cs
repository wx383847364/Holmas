using App.HotUpdate.Holmas.UI.Binding;
using App.HotUpdate.Holmas.UI.Screens.Loading;

namespace App.HotUpdate.Holmas.UI.Generated
{
    public static class LoadingGeneratedBindings
    {
        public const string PrefabName = "LoadingPanel";
        public const string PrefabAssetPath = "Assets/Res/Perfabs/UI/LoadingPanel.prefab";

        private static readonly UiBindingManifest ManifestInstance = BuildManifest();
        private static readonly UiRuntimeScreenDescriptor DescriptorInstance =
            new UiRuntimeScreenDescriptor(PrefabName, PrefabAssetPath, ManifestInstance);

        public static UiBindingManifest Manifest => ManifestInstance;

        public static UiRuntimeScreenDescriptor Descriptor => DescriptorInstance;

        private static UiBindingManifest BuildManifest()
        {
            var manifest = new UiBindingManifest(LoadingScreenRegistration.ScreenId, PrefabName, PrefabAssetPath);
            manifest.AddEntry(LoadingBindings.RootPanelKey, "RectTransform", LoadingBindings.RootNodePath, notes: "loading_root");
            manifest.AddEntry(LoadingBindings.LoadingBarKey, "Slider", LoadingBindings.LoadingBarNodePath, notes: "loading_bar");
            manifest.AddEntry(LoadingBindings.StatusTextKey, "TextMeshProUGUI", LoadingBindings.StatusTextNodePath, notes: "loading_status");
            return manifest;
        }
    }
}
