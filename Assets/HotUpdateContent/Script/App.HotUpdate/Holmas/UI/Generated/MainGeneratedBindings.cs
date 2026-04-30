using App.HotUpdate.Holmas.UI.Binding;
using App.HotUpdate.Holmas.UI.Screens.Main;

namespace App.HotUpdate.Holmas.UI.Generated
{
    public static class MainGeneratedBindings
    {
        public const string PrefabName = "MainPanel";
        public const string PrefabAssetPath = "Assets/HotUpdateContent/Res/Perfabs/UI/MainPanel.prefab";

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
            manifest.AddEntry(MainBindings.EnergyTextKey, "TextMeshProUGUI", MainBindings.EnergyTextNodePath, notes: "main_energy");
            manifest.AddEntry(
                MainBindings.PromotionButtonKey,
                "Button",
                MainBindings.PromotionButtonNodePath,
                MainBindings.ButtonClickEvent,
                requiresManualWiring: true,
                notes: "controller_wires_promotion");
            manifest.AddEntry(
                MainBindings.HelpButtonKey,
                "Button",
                MainBindings.HelpButtonNodePath,
                MainBindings.ButtonClickEvent,
                requiresManualWiring: true,
                notes: "controller_wires_help");
            manifest.AddEntry(
                MainBindings.GmButtonKey,
                "Button",
                MainBindings.GmButtonNodePath,
                MainBindings.ButtonClickEvent,
                requiresManualWiring: true,
                notes: "controller_wires_gm");
            manifest.AddEntry(MainBindings.MinesGroupKey, "RectTransform", MainBindings.MinesGroupNodePath, notes: "embedded_board_parent");
            manifest.AddEntry(MainBindings.BoardContainerKey, "RectTransform", MainBindings.BoardContainerNodePath, notes: "embedded_runtime_board");
            manifest.AddEntry(MainBindings.TutorialBoardContainerKey, "RectTransform", MainBindings.TutorialBoardContainerNodePath, notes: "tutorial_board_overlay");
            manifest.AddEntry(
                MainBindings.WalkToggleKey,
                "Toggle",
                MainBindings.WalkToggleNodePath,
                MainBindings.ToggleChangedEvent,
                requiresManualWiring: true,
                notes: "controller_wires_walk_mode");
            manifest.AddEntry(
                MainBindings.FindToggleKey,
                "Toggle",
                MainBindings.FindToggleNodePath,
                MainBindings.ToggleChangedEvent,
                requiresManualWiring: true,
                notes: "controller_wires_find_mode");
            return manifest;
        }
    }
}
