using App.HotUpdate.Holmas.UI.Binding;
using UnityEngine;
using UnityEngine.UI;

namespace App.HotUpdate.Holmas.UI.Screens.Leaderboard
{
    public sealed class LeaderboardBindings
    {
        public const string RootPanelKey = "leaderboard/root_panel";
        public const string BackButtonKey = "leaderboard/back_button";
        public const string RewardButtonKey = "leaderboard/reward_button";
        public const string LevelToggleKey = "leaderboard/level_toggle";
        public const string WeeklyCatsToggleKey = "leaderboard/weekly_cats_toggle";
        public const string DailyMoneyToggleKey = "leaderboard/daily_money_toggle";
        public const string TitleTextKey = "leaderboard/title_text";
        public const string LeaderInfoKey = "leaderboard/leader_info";
        public const string LeaderListKey = "leaderboard/leader_list";
        public const string LeaderListContentKey = "leaderboard/leader_list_content";
        public const string ItemTemplateKey = "leaderboard/item_template";
        public const string MyInfoKey = "leaderboard/my_info";
        public const string Top1Key = "leaderboard/top_1";
        public const string Top2Key = "leaderboard/top_2";
        public const string Top3Key = "leaderboard/top_3";
        public const string ButtonClickEvent = "on_click";
        public const string ToggleChangedEvent = "on_value_changed";

        public const string RootNodePath = "LeadbroadPanel";
        public const string BackButtonNodePath = RootNodePath + "/Back_btn";
        public const string RewardButtonNodePath = RootNodePath + "/RewardInfo_btn";
        public const string LevelToggleNodePath = RootNodePath + "/Toggles/LevelToggle";
        public const string WeeklyCatsToggleNodePath = RootNodePath + "/Toggles/WeekCatCountToggle";
        public const string DailyMoneyToggleNodePath = RootNodePath + "/Toggles/DaliyMoneyToggle";
        public const string TitleTextNodePath = RootNodePath + "/Title_txt";
        public const string LeaderInfoNodePath = RootNodePath + "/LeaderInfo";
        public const string LeaderListNodePath = LeaderInfoNodePath + "/LeaderList";
        public const string LeaderListContentNodePath = LeaderListNodePath + "/GameObject";
        public const string ItemTemplateNodePath = LeaderListContentNodePath + "/PlayerInfo";
        public const string MyInfoNodePath = LeaderInfoNodePath + "/MyInfo";
        public const string Top1NodePath = LeaderInfoNodePath + "/No.1";
        public const string Top2NodePath = LeaderInfoNodePath + "/No.2";
        public const string Top3NodePath = LeaderInfoNodePath + "/No.3";

        public RectTransform RootPanel;
        public Button BackButton;
        public Button RewardButton;
        public Toggle LevelToggle;
        public Toggle WeeklyCatsToggle;
        public Toggle DailyMoneyToggle;
        public Text TitleText;
        public RectTransform LeaderInfo;
        public ScrollRect LeaderList;
        public RectTransform LeaderListContent;
        public RectTransform ItemTemplate;
        public RectTransform MyInfo;
        public RectTransform Top1;
        public RectTransform Top2;
        public RectTransform Top3;

        public bool HasRequiredBindings =>
            RootPanel != null &&
            BackButton != null &&
            RewardButton != null &&
            LevelToggle != null &&
            WeeklyCatsToggle != null &&
            DailyMoneyToggle != null &&
            TitleText != null &&
            LeaderInfo != null &&
            LeaderList != null &&
            LeaderListContent != null &&
            ItemTemplate != null &&
            MyInfo != null &&
            Top1 != null &&
            Top2 != null &&
            Top3 != null;

        public static LeaderboardBindings Resolve(UiBindingResolver resolver)
        {
            var bindings = new LeaderboardBindings();
            if (resolver == null || !resolver.HasCollector)
            {
                return bindings;
            }

            resolver.TryResolve(RootPanelKey, out bindings.RootPanel, nodePath: RootNodePath);
            resolver.TryResolve(BackButtonKey, out bindings.BackButton, ButtonClickEvent, BackButtonNodePath);
            resolver.TryResolve(RewardButtonKey, out bindings.RewardButton, ButtonClickEvent, RewardButtonNodePath);
            resolver.TryResolve(LevelToggleKey, out bindings.LevelToggle, ToggleChangedEvent, LevelToggleNodePath);
            resolver.TryResolve(WeeklyCatsToggleKey, out bindings.WeeklyCatsToggle, ToggleChangedEvent, WeeklyCatsToggleNodePath);
            resolver.TryResolve(DailyMoneyToggleKey, out bindings.DailyMoneyToggle, ToggleChangedEvent, DailyMoneyToggleNodePath);
            resolver.TryResolve(TitleTextKey, out bindings.TitleText, nodePath: TitleTextNodePath);
            resolver.TryResolve(LeaderInfoKey, out bindings.LeaderInfo, nodePath: LeaderInfoNodePath);
            resolver.TryResolve(LeaderListKey, out bindings.LeaderList, nodePath: LeaderListNodePath);
            resolver.TryResolve(LeaderListContentKey, out bindings.LeaderListContent, nodePath: LeaderListContentNodePath);
            resolver.TryResolve(ItemTemplateKey, out bindings.ItemTemplate, nodePath: ItemTemplateNodePath);
            resolver.TryResolve(MyInfoKey, out bindings.MyInfo, nodePath: MyInfoNodePath);
            resolver.TryResolve(Top1Key, out bindings.Top1, nodePath: Top1NodePath);
            resolver.TryResolve(Top2Key, out bindings.Top2, nodePath: Top2NodePath);
            resolver.TryResolve(Top3Key, out bindings.Top3, nodePath: Top3NodePath);
            return bindings;
        }
    }
}
