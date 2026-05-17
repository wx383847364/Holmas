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
                notes: "publicity_build_container");
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
            }

            int templateIndex = BattleBindings.BuildStageTemplateIndex;
            manifest.AddEntry(BattleBindings.BuildStageButtonKeys[templateIndex], "Button", BattleBindings.BuildStageButtonNodePaths[templateIndex], BattleBindings.ButtonClickEvent, requiresManualWiring: true, notes: "publicity_build_stage_template_button");
            manifest.AddEntry(BattleBindings.BuildStageImageKeys[templateIndex], "Image", BattleBindings.BuildStageImageNodePaths[templateIndex], notes: "publicity_build_stage_template_image");
            manifest.AddEntry(BattleBindings.BuildStageNameTextKeys[templateIndex], "TextMeshProUGUI", BattleBindings.BuildStageNameTextNodePaths[templateIndex], notes: "publicity_build_stage_template_label");
            manifest.AddEntry(BattleBindings.BuildStageLockKeys[templateIndex], "RectTransform", BattleBindings.BuildStageLockNodePaths[templateIndex], notes: "publicity_build_stage_template_lock");
            manifest.AddEntry(BattleBindings.BuildStageBaseStarGroupKeys[templateIndex], "RectTransform", BattleBindings.BuildStageBaseStarGroupNodePaths[templateIndex], notes: "publicity_build_stage_template_base_stars");
            manifest.AddEntry(BattleBindings.BuildStageActiveStarGroupKeys[templateIndex], "RectTransform", BattleBindings.BuildStageActiveStarGroupNodePaths[templateIndex], notes: "publicity_build_stage_template_active_stars");

            for (int i = 0; i < BattlePresenter.VisibleStageBarCount; i++)
            {
                manifest.AddEntry(BattleBindings.StageBarKeys[i], "Slider", BattleBindings.StageBarNodePaths[i], notes: "publicity_stage_progress_bar");
            }

            return manifest;
        }
    }
}
