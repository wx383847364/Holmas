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
            manifest.AddEntry(
                MainBindings.LeaderboardButtonKey,
                "Button",
                MainBindings.LeaderboardButtonNodePath,
                MainBindings.ButtonClickEvent,
                requiresManualWiring: true,
                notes: "controller_wires_leaderboard");
            manifest.AddEntry(MainBindings.MinesBgImageKey, "Image", MainBindings.MinesBgNodePath, notes: "board_background_image");
            manifest.AddEntry(MainBindings.MinesBgMaskKey, "RectMask2D", MainBindings.MinesBgNodePath, notes: "board_background_mask");
            manifest.AddEntry(MainBindings.MinesBgFrameOverlayImageKey, "Image", MainBindings.MinesBgFrameOverlayNodePath, notes: "board_frame_overlay_image");
            manifest.AddEntry(MainBindings.BoardContentRectKey, "RectTransform", MainBindings.BoardContentRectNodePath, notes: "board_content_rect");
            manifest.AddEntry(MainBindings.MinesGroupKey, "RectTransform", MainBindings.MinesGroupNodePath, notes: "embedded_board_parent");
            manifest.AddEntry(MainBindings.BoardContainerKey, "RectTransform", MainBindings.BoardContainerNodePath, notes: "embedded_runtime_board");
            manifest.AddEntry(MainBindings.TutorialBoardContainerKey, "RectTransform", MainBindings.TutorialBoardContainerNodePath, notes: "tutorial_board_overlay");
            for (int i = 0; i < MainBindings.TaskSlotCount; i++)
            {
                manifest.AddEntry(
                    MainBindings.TaskSlotRootKeys[i],
                    "RectTransform",
                    MainBindings.TaskSlotRootNodePaths[i],
                    notes: "main_task_slot_root");
                manifest.AddEntry(
                    MainBindings.TaskSlotButtonKeys[i],
                    "Button",
                    MainBindings.TaskSlotRootNodePaths[i],
                    MainBindings.ButtonClickEvent,
                    requiresManualWiring: true,
                    notes: "controller_wires_task_slot");
                manifest.AddEntry(
                    MainBindings.TaskSlotBackgroundImageKeys[i],
                    "Image",
                    MainBindings.TaskSlotRootNodePaths[i],
                    notes: "main_task_slot_background");
                manifest.AddEntry(
                    MainBindings.TaskProgressTextKeys[i],
                    "Text",
                    MainBindings.TaskProgressTextNodePaths[i],
                    notes: "main_task_progress_static_text");
                manifest.AddEntry(
                    MainBindings.TaskProgressSliderKeys[i],
                    "Slider",
                    MainBindings.TaskProgressSliderNodePaths[i],
                    notes: "main_task_progress_slider");
                manifest.AddEntry(
                    MainBindings.TaskRewardIconKeys[i],
                    "Image",
                    MainBindings.TaskRewardIconNodePaths[i],
                    notes: "main_task_reward_icon");
                manifest.AddEntry(
                    MainBindings.TaskCatIconKeys[i],
                    "Image",
                    MainBindings.TaskCatIconNodePaths[i],
                    notes: "main_task_cat_icon");
                manifest.AddEntry(
                    MainBindings.TaskLockKeys[i],
                    "RectTransform",
                    MainBindings.TaskLockNodePaths[i],
                    notes: "main_task_lock");
                manifest.AddEntry(
                    MainBindings.TaskTitleTextKeys[i],
                    "TextMeshProUGUI",
                    MainBindings.TaskTitleTextNodePaths[i],
                    notes: "main_task_title_static_text");
                manifest.AddEntry(
                    MainBindings.TaskRewardTextKeys[i],
                    "TextMeshProUGUI",
                    MainBindings.TaskRewardTextNodePaths[i],
                    notes: "main_task_reward_static_text");
            }

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
