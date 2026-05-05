using App.HotUpdate.Holmas.UI.Binding;
using App.HotUpdate.Holmas.UI.Screens.Leaderboard;

namespace App.HotUpdate.Holmas.UI.Generated
{
    public static class LeaderboardGeneratedBindings
    {
        public const string PrefabName = "LeadbroadPanel";
        public const string PrefabAssetPath = "Assets/HotUpdateContent/Res/Perfabs/UI/LeadbroadPanel.prefab";

        private static readonly UiBindingManifest ManifestInstance = BuildManifest();
        private static readonly UiRuntimeScreenDescriptor DescriptorInstance =
            new UiRuntimeScreenDescriptor(PrefabName, PrefabAssetPath, ManifestInstance);

        public static UiBindingManifest Manifest => ManifestInstance;

        public static UiRuntimeScreenDescriptor Descriptor => DescriptorInstance;

        private static UiBindingManifest BuildManifest()
        {
            var manifest = new UiBindingManifest(LeaderboardScreenRegistration.ScreenId, PrefabName, PrefabAssetPath);
            manifest.AddEntry(LeaderboardBindings.RootPanelKey, "RectTransform", LeaderboardBindings.RootNodePath, notes: "leaderboard_root");
            manifest.AddEntry(LeaderboardBindings.BackButtonKey, "Button", LeaderboardBindings.BackButtonNodePath, LeaderboardBindings.ButtonClickEvent, requiresManualWiring: true, notes: "controller_wires_back");
            manifest.AddEntry(LeaderboardBindings.RewardButtonKey, "Button", LeaderboardBindings.RewardButtonNodePath, LeaderboardBindings.ButtonClickEvent, requiresManualWiring: true, notes: "controller_wires_reward");
            manifest.AddEntry(LeaderboardBindings.LevelToggleKey, "Toggle", LeaderboardBindings.LevelToggleNodePath, LeaderboardBindings.ToggleChangedEvent, requiresManualWiring: true, notes: "controller_wires_level_tab");
            manifest.AddEntry(LeaderboardBindings.WeeklyCatsToggleKey, "Toggle", LeaderboardBindings.WeeklyCatsToggleNodePath, LeaderboardBindings.ToggleChangedEvent, requiresManualWiring: true, notes: "controller_wires_week_tab");
            manifest.AddEntry(LeaderboardBindings.DailyMoneyToggleKey, "Toggle", LeaderboardBindings.DailyMoneyToggleNodePath, LeaderboardBindings.ToggleChangedEvent, requiresManualWiring: true, notes: "controller_wires_day_tab");
            manifest.AddEntry(LeaderboardBindings.TitleTextKey, "Text", LeaderboardBindings.TitleTextNodePath, notes: "prefab_title");
            manifest.AddEntry(LeaderboardBindings.LeaderInfoKey, "RectTransform", LeaderboardBindings.LeaderInfoNodePath, notes: "leaderboard_content_root");
            manifest.AddEntry(LeaderboardBindings.LeaderListKey, "ScrollRect", LeaderboardBindings.LeaderListNodePath, notes: "virtualized_scroll_rect");
            manifest.AddEntry(LeaderboardBindings.LeaderListContentKey, "RectTransform", LeaderboardBindings.LeaderListContentNodePath, notes: "virtualized_scroll_content");
            manifest.AddEntry(LeaderboardBindings.ItemTemplateKey, "RectTransform", LeaderboardBindings.ItemTemplateNodePath, notes: "player_info_item_template");
            manifest.AddEntry(LeaderboardBindings.MyInfoKey, "RectTransform", LeaderboardBindings.MyInfoNodePath, notes: "self_entry");
            manifest.AddEntry(LeaderboardBindings.Top1Key, "RectTransform", LeaderboardBindings.Top1NodePath, notes: "top_1_entry");
            manifest.AddEntry(LeaderboardBindings.Top2Key, "RectTransform", LeaderboardBindings.Top2NodePath, notes: "top_2_entry");
            manifest.AddEntry(LeaderboardBindings.Top3Key, "RectTransform", LeaderboardBindings.Top3NodePath, notes: "top_3_entry");
            return manifest;
        }
    }
}
