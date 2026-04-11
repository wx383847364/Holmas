using App.HotUpdate.Holmas.UI.Binding;
using App.HotUpdate.Holmas.UI.Screens.Battle;

namespace App.HotUpdate.Holmas.UI.Generated
{
    public static class BattleGeneratedBindings
    {
        public const string PrefabName = "BattlePanel";
        public const string PrefabAssetPath = "Assets/Res/Perfabs/UI/BattlePanel.prefab";

        private static readonly UiBindingManifest ManifestInstance = BuildManifest();
        private static readonly UiRuntimeScreenDescriptor DescriptorInstance =
            new UiRuntimeScreenDescriptor(PrefabName, PrefabAssetPath, ManifestInstance);

        public static UiBindingManifest Manifest => ManifestInstance;

        public static UiRuntimeScreenDescriptor Descriptor => DescriptorInstance;

        private static UiBindingManifest BuildManifest()
        {
            var manifest = new UiBindingManifest(BattleScreenRegistration.ScreenId, PrefabName, PrefabAssetPath);
            manifest.AddEntry(BattleBindings.RootPanelKey, "RectTransform", BattleBindings.RootNodePath, notes: "battle_root");
            manifest.AddEntry(BattleBindings.BackButtonKey, "Button", BattleBindings.BackButtonNodePath, BattleBindings.ButtonClickEvent, requiresManualWiring: true, notes: "controller_wires_back");
            manifest.AddEntry(BattleBindings.LevelTextKey, "TextMeshProUGUI", BattleBindings.LevelTextNodePath, notes: "battle_level");
            manifest.AddEntry(BattleBindings.GoldTextKey, "TextMeshProUGUI", BattleBindings.GoldTextNodePath, notes: "battle_gold");
            manifest.AddEntry(BattleBindings.SummaryTextKey, "TextMeshProUGUI", BattleBindings.SummaryTextNodePath, notes: "battle_summary");
            manifest.AddEntry(BattleBindings.StatusTextKey, "TextMeshProUGUI", BattleBindings.StatusTextNodePath, notes: "battle_status");
            manifest.AddEntry(BattleBindings.BoardContainerKey, "RectTransform", BattleBindings.BoardContainerNodePath, notes: "runtime_board");
            return manifest;
        }
    }
}
