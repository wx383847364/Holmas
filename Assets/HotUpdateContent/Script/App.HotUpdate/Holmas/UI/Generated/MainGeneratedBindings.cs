using App.HotUpdate.Holmas.UI.Binding;
using App.HotUpdate.Holmas.UI.Screens.Main;

namespace App.HotUpdate.Holmas.UI.Generated
{
    public static class MainGeneratedBindings
    {
        public const string PrefabName = "MainPanel";
        public const string PrefabAssetPath = "Assets/Res/Perfabs/UI/MainPanel.prefab";

        private static readonly UiBindingManifest ManifestInstance = BuildManifest();
        private static readonly UiRuntimeScreenDescriptor DescriptorInstance =
            new UiRuntimeScreenDescriptor(PrefabName, PrefabAssetPath, ManifestInstance);

        public static UiBindingManifest Manifest => ManifestInstance;

        public static UiRuntimeScreenDescriptor Descriptor => DescriptorInstance;

        private static UiBindingManifest BuildManifest()
        {
            var manifest = new UiBindingManifest(MainScreenRegistration.ScreenId, PrefabName, PrefabAssetPath);
            manifest.AddEntry(MainBindings.RootPanelKey, "RectTransform", MainBindings.RootNodePath, notes: "main_root");
            manifest.AddEntry(MainBindings.LevelTextKey, "TextMeshProUGUI", MainBindings.LevelTextNodePath, notes: "main_level");
            manifest.AddEntry(MainBindings.GoldTextKey, "TextMeshProUGUI", MainBindings.GoldTextNodePath, notes: "main_gold");
            manifest.AddEntry(MainBindings.SummaryTextKey, "TextMeshProUGUI", MainBindings.SummaryTextNodePath, notes: "main_summary");
            manifest.AddEntry(MainBindings.StatusTextKey, "TextMeshProUGUI", MainBindings.StatusTextNodePath, notes: "main_status");
            manifest.AddEntry(MainBindings.StartButtonKey, "Button", MainBindings.StartButtonNodePath, MainBindings.ButtonClickEvent, requiresManualWiring: true, notes: "controller_wires_start");
            manifest.AddEntry(MainBindings.PromotionButtonKey, "Button", MainBindings.PromotionButtonNodePath, MainBindings.ButtonClickEvent, requiresManualWiring: true, notes: "controller_wires_promotion");
            return manifest;
        }
    }
}
