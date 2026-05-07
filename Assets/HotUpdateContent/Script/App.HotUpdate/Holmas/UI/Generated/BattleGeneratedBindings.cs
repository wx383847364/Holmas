using App.HotUpdate.Holmas.UI.Binding;
using App.HotUpdate.Holmas.UI.Screens.Battle;

namespace App.HotUpdate.Holmas.UI.Generated
{
    public static class BattleGeneratedBindings
    {
        public const string PrefabName = "BattlePanel";
        public const string PrefabAssetPath = "Assets/HotUpdateContent/Res/Perfabs/UI/BattlePanel.prefab";

        private static readonly UiBindingManifest ManifestInstance = BuildManifest();
        private static readonly UiRuntimeScreenDescriptor DescriptorInstance =
            new UiRuntimeScreenDescriptor(PrefabName, PrefabAssetPath, ManifestInstance);

        public static UiBindingManifest Manifest => ManifestInstance;

        public static UiRuntimeScreenDescriptor Descriptor => DescriptorInstance;

        private static UiBindingManifest BuildManifest()
        {
            var manifest = new UiBindingManifest(BattleScreenRegistration.ScreenId, PrefabName, PrefabAssetPath);
            manifest.AddEntry(BattleBindings.RootPanelKey, "RectTransform", BattleBindings.RootNodePath, notes: "battle_root");
            manifest.AddEntry(
                BattleBindings.BackButtonKey,
                "Button",
                BattleBindings.BackButtonNodePath,
                BattleBindings.ButtonClickEvent,
                requiresManualWiring: true,
                notes: "controller_wires_back");
            manifest.AddEntry(
                BattleBindings.BuildButtonKey,
                "Button",
                BattleBindings.BuildButtonNodePath,
                BattleBindings.ButtonClickEvent,
                requiresManualWiring: true,
                notes: "controller_wires_publicity_build");
            manifest.AddEntry(BattleBindings.BuildButtonTextKey, "TextMeshProUGUI", BattleBindings.BuildButtonTextNodePath, notes: "build_button_label");
            manifest.AddEntry(BattleBindings.LevelTextKey, "TextMeshProUGUI", BattleBindings.LevelTextNodePath, notes: "battle_level");
            manifest.AddEntry(BattleBindings.GoldTextKey, "TextMeshProUGUI", BattleBindings.GoldTextNodePath, notes: "battle_gold");
            manifest.AddEntry(BattleBindings.EnergyTextKey, "TextMeshProUGUI", BattleBindings.EnergyTextNodePath, notes: "battle_energy");
            manifest.AddEntry(BattleBindings.SummaryTextKey, "TextMeshProUGUI", BattleBindings.SummaryTextNodePath, notes: "battle_summary");
            manifest.AddEntry(BattleBindings.StatusTextKey, "TextMeshProUGUI", BattleBindings.StatusTextNodePath, notes: "battle_status");
            for (int i = 0; i < BattlePresenter.VisibleStageCount; i++)
            {
                manifest.AddEntry(BattleBindings.StageButtonKeys[i], "Button", BattleBindings.StageButtonNodePaths[i], BattleBindings.ButtonClickEvent, requiresManualWiring: true, notes: "publicity_stage_button");
                manifest.AddEntry(BattleBindings.StageImageKeys[i], "Image", BattleBindings.StageImageNodePaths[i], notes: "publicity_stage_image");
                manifest.AddEntry(BattleBindings.StageNameTextKeys[i], "TextMeshProUGUI", BattleBindings.StageNameTextNodePaths[i], notes: "publicity_stage_label");
                manifest.AddEntry(BattleBindings.StageLockKeys[i], "RectTransform", BattleBindings.StageLockNodePaths[i], notes: "publicity_stage_lock");
                manifest.AddEntry(BattleBindings.BuildStageButtonKeys[i], "Button", BattleBindings.BuildStageButtonNodePaths[i], BattleBindings.ButtonClickEvent, requiresManualWiring: true, notes: "publicity_build_stage_button");
                manifest.AddEntry(BattleBindings.BuildStageImageKeys[i], "Image", BattleBindings.BuildStageImageNodePaths[i], notes: "publicity_build_stage_image");
                manifest.AddEntry(BattleBindings.BuildStageNameTextKeys[i], "TextMeshProUGUI", BattleBindings.BuildStageNameTextNodePaths[i], notes: "publicity_build_stage_label");
                manifest.AddEntry(BattleBindings.BuildStageLockKeys[i], "RectTransform", BattleBindings.BuildStageLockNodePaths[i], notes: "publicity_build_stage_lock");
                manifest.AddEntry(BattleBindings.BuildStageBaseStarGroupKeys[i], "RectTransform", BattleBindings.BuildStageBaseStarGroupNodePaths[i], notes: "publicity_build_stage_base_stars");
                manifest.AddEntry(BattleBindings.BuildStageActiveStarGroupKeys[i], "RectTransform", BattleBindings.BuildStageActiveStarGroupNodePaths[i], notes: "publicity_build_stage_active_stars");
            }

            for (int i = 0; i < BattlePresenter.VisibleStageBarCount; i++)
            {
                manifest.AddEntry(BattleBindings.StageBarKeys[i], "Image", BattleBindings.StageBarNodePaths[i], notes: "publicity_stage_progress_bar");
            }

            return manifest;
        }
    }
}
