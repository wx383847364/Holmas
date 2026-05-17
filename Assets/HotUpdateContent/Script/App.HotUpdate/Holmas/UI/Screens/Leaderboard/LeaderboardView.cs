using System;
using System.Collections.Generic;
using App.HotUpdate.Holmas.UI.Binding;
using App.HotUpdate.Holmas.UI.Core;
using App.Shared.Contracts;
using App.Shared.Holmas.Leaderboards;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace App.HotUpdate.Holmas.UI.Screens.Leaderboard
{
    public sealed class LeaderboardView : MonoBehaviour
    {
        private readonly List<HolmasLeaderboardEntry> _listEntries = new List<HolmasLeaderboardEntry>();
        private LeaderboardBindings _bindings;
        private LeaderboardLoopListView _loopListView;
        private LeaderboardItemView _top1View;
        private LeaderboardItemView _top2View;
        private LeaderboardItemView _top3View;
        private LeaderboardItemView _myInfoView;
        private HolmasCatSpriteLoader _catSpriteLoader;
        private LeaderboardAvatarSpriteLoader _avatarSpriteLoader;
        private UnityAction _currentBackAction;
        private UnityAction _currentRewardAction;
        private UnityAction<bool> _currentLevelAction;
        private UnityAction<bool> _currentWeeklyAction;
        private UnityAction<bool> _currentDailyAction;
        private bool _syncingToggles;

        #if UNITY_EDITOR
        public void EnsureBindingSurface()
        {
            gameObject.name = LeaderboardBindings.RootNodePath;
            UiReferenceCollector collector = gameObject.GetComponent<UiReferenceCollector>() ?? gameObject.AddComponent<UiReferenceCollector>();
            RectTransform root = gameObject.GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
            Stretch(root);

            Button backButton = FindFirstDescendantByName<Button>("Back_btn") ?? CreateButton(transform, "Back_btn", "<", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -40f), new Vector2(96f, 72f));
            Button rewardButton = FindFirstDescendantByName<Button>("RewardInfo_btn") ?? CreateButton(transform, "RewardInfo_btn", "奖励", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -40f), new Vector2(128f, 72f));
            Toggle levelToggle = FindFirstDescendantByName<Toggle>("LevelToggle") ?? CreateToggle(transform, "LevelToggle", "等级总榜", new Vector2(0.28f, 0f));
            Toggle weeklyToggle = FindFirstDescendantByName<Toggle>("WeekCatCountToggle") ?? CreateToggle(transform, "WeekCatCountToggle", "寻猫周榜", new Vector2(0.5f, 0f));
            Toggle dailyToggle = FindFirstDescendantByName<Toggle>("DaliyMoneyToggle") ?? CreateToggle(transform, "DaliyMoneyToggle", "财富日榜", new Vector2(0.72f, 0f));
            EnsureToggleGroup(levelToggle, weeklyToggle, dailyToggle);

            Text titleText = FindByPath<Text>(LeaderboardBindings.TitleTextNodePath) ?? CreateText(transform, "Title_txt", "等级总榜", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -82f), new Vector2(400f, 80f), 44, TextAnchor.MiddleCenter);
            Text statusText = FindByPath<Text>(LeaderboardBindings.StatusTextNodePath) ?? CreateText(transform, "LeaderboardStatusText", string.Empty, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -132f), new Vector2(500f, 36f), 22, TextAnchor.MiddleCenter);

            RectTransform leaderInfo = FindByPath<RectTransform>(LeaderboardBindings.LeaderInfoNodePath) ?? GetOrCreateRect(transform, "LeaderInfo");
            ScrollRect leaderList = FindByPath<ScrollRect>(LeaderboardBindings.LeaderListNodePath) ?? CreateScrollRect(leaderInfo, "LeaderList");
            RectTransform leaderListContent = FindByPath<RectTransform>(LeaderboardBindings.LeaderListContentNodePath) ?? GetOrCreateRect(leaderList.transform, "GameObject");
            RectTransform itemTemplate = FindByPath<RectTransform>(LeaderboardBindings.ItemTemplateNodePath) ?? CreateFallbackPlayerInfo(leaderListContent, "PlayerInfo");
            RectTransform myInfo = FindByPath<RectTransform>(LeaderboardBindings.MyInfoNodePath) ?? CreateFallbackPlayerInfo(leaderInfo, "MyInfo");
            RectTransform top1 = FindByPath<RectTransform>(LeaderboardBindings.Top1NodePath) ?? CreateFallbackTopInfo(leaderInfo, "No.1", new Vector2(0f, 561f));
            RectTransform top2 = FindByPath<RectTransform>(LeaderboardBindings.Top2NodePath) ?? CreateFallbackTopInfo(leaderInfo, "No.2", new Vector2(-282f, 529f));
            RectTransform top3 = FindByPath<RectTransform>(LeaderboardBindings.Top3NodePath) ?? CreateFallbackTopInfo(leaderInfo, "No.3", new Vector2(287f, 517f));
            EnsureItemBindingSurface(itemTemplate);
            EnsureItemBindingSurface(myInfo);
            EnsureItemBindingSurface(top1);
            EnsureItemBindingSurface(top2);
            EnsureItemBindingSurface(top3);

            leaderList.content = leaderListContent;
            if (leaderList.viewport == null || leaderList.viewport == leaderListContent)
            {
                leaderList.viewport = leaderList.GetComponent<RectTransform>();
            }
            _loopListView = leaderList.GetComponent<LeaderboardLoopListView>() ?? leaderList.gameObject.AddComponent<LeaderboardLoopListView>();
            _loopListView.Configure(leaderList, leaderListContent, itemTemplate);

            collector.RegisterOrReplace(LeaderboardBindings.RootPanelKey, root, nodePath: LeaderboardBindings.RootNodePath);
            collector.RegisterOrReplace(LeaderboardBindings.BackButtonKey, backButton, LeaderboardBindings.ButtonClickEvent, LeaderboardBindings.BackButtonNodePath);
            collector.RegisterOrReplace(LeaderboardBindings.RewardButtonKey, rewardButton, LeaderboardBindings.ButtonClickEvent, LeaderboardBindings.RewardButtonNodePath);
            collector.RegisterOrReplace(LeaderboardBindings.LevelToggleKey, levelToggle, LeaderboardBindings.ToggleChangedEvent, LeaderboardBindings.LevelToggleNodePath);
            collector.RegisterOrReplace(LeaderboardBindings.WeeklyCatsToggleKey, weeklyToggle, LeaderboardBindings.ToggleChangedEvent, LeaderboardBindings.WeeklyCatsToggleNodePath);
            collector.RegisterOrReplace(LeaderboardBindings.DailyMoneyToggleKey, dailyToggle, LeaderboardBindings.ToggleChangedEvent, LeaderboardBindings.DailyMoneyToggleNodePath);
            collector.RegisterOrReplace(LeaderboardBindings.TitleTextKey, titleText, nodePath: LeaderboardBindings.TitleTextNodePath);
            collector.RegisterOrReplace(LeaderboardBindings.StatusTextKey, statusText, nodePath: LeaderboardBindings.StatusTextNodePath);
            collector.RegisterOrReplace(LeaderboardBindings.LeaderInfoKey, leaderInfo, nodePath: LeaderboardBindings.LeaderInfoNodePath);
            collector.RegisterOrReplace(LeaderboardBindings.LeaderListKey, leaderList, nodePath: LeaderboardBindings.LeaderListNodePath);
            collector.RegisterOrReplace(LeaderboardBindings.LeaderListContentKey, leaderListContent, nodePath: LeaderboardBindings.LeaderListContentNodePath);
            collector.RegisterOrReplace(LeaderboardBindings.ItemTemplateKey, itemTemplate, nodePath: LeaderboardBindings.ItemTemplateNodePath);
            collector.RegisterOrReplace(LeaderboardBindings.MyInfoKey, myInfo, nodePath: LeaderboardBindings.MyInfoNodePath);
            collector.RegisterOrReplace(LeaderboardBindings.Top1Key, top1, nodePath: LeaderboardBindings.Top1NodePath);
            collector.RegisterOrReplace(LeaderboardBindings.Top2Key, top2, nodePath: LeaderboardBindings.Top2NodePath);
            collector.RegisterOrReplace(LeaderboardBindings.Top3Key, top3, nodePath: LeaderboardBindings.Top3NodePath);
        }

        private static void EnsureItemBindingSurface(RectTransform root)
        {
            if (root == null)
            {
                return;
            }

            LeaderboardItemBindingSurface surface =
                root.GetComponent<LeaderboardItemBindingSurface>() ??
                root.gameObject.AddComponent<LeaderboardItemBindingSurface>();
            surface.AssignForEditor();
        }
        #endif

        public void Bind(LeaderboardBindings bindings)
        {
            _bindings = bindings ?? new LeaderboardBindings();
            _top1View = new LeaderboardItemView(_bindings.Top1);
            _top2View = new LeaderboardItemView(_bindings.Top2);
            _top3View = new LeaderboardItemView(_bindings.Top3);
            _myInfoView = new LeaderboardItemView(_bindings.MyInfo);
            ApplyCatSpriteLoaderToItemViews();
            ApplyAvatarSpriteLoaderToItemViews();

            if (_bindings.LeaderList != null)
            {
                _loopListView = _bindings.LeaderList.GetComponent<LeaderboardLoopListView>() ?? _bindings.LeaderList.gameObject.AddComponent<LeaderboardLoopListView>();
                _loopListView.Configure(_bindings.LeaderList, _bindings.LeaderListContent, _bindings.ItemTemplate);
                _loopListView.SetCatSpriteLoader(_catSpriteLoader);
                _loopListView.SetAvatarSpriteLoader(_avatarSpriteLoader);
            }
        }

        public void SetAssetsRuntime(IAssetsRuntime assetsRuntime, INetClient netClient = null)
        {
            if (_catSpriteLoader == null)
            {
                _catSpriteLoader = new HolmasCatSpriteLoader(assetsRuntime);
            }
            else
            {
                _catSpriteLoader.SetAssetsRuntime(assetsRuntime);
            }

            _avatarSpriteLoader?.Dispose();
            _avatarSpriteLoader = new LeaderboardAvatarSpriteLoader(_catSpriteLoader, netClient);
            ApplyCatSpriteLoaderToItemViews();
            ApplyAvatarSpriteLoaderToItemViews();
            _loopListView?.SetCatSpriteLoader(_catSpriteLoader);
            _loopListView?.SetAvatarSpriteLoader(_avatarSpriteLoader);
        }

        public void ReleaseAssets()
        {
            ReleaseAvatarItemViews();
            _loopListView?.ReleaseAvatarItems();
            _loopListView?.SetAvatarSpriteLoader(null);
            _avatarSpriteLoader?.Dispose();
            _avatarSpriteLoader = null;
            _catSpriteLoader?.Dispose();
            _catSpriteLoader = null;
            ApplyCatSpriteLoaderToItemViews();
            ApplyAvatarSpriteLoaderToItemViews();
            _loopListView?.SetCatSpriteLoader(null);
        }

        public void SetBackAction(UnityAction action)
        {
            ReplaceButtonAction(_bindings?.BackButton, ref _currentBackAction, action);
        }

        public void SetRewardAction(UnityAction action)
        {
            ReplaceButtonAction(_bindings?.RewardButton, ref _currentRewardAction, action);
        }

        public void SetTabActions(UnityAction<bool> levelAction, UnityAction<bool> weeklyAction, UnityAction<bool> dailyAction)
        {
            ReplaceToggleAction(_bindings?.LevelToggle, ref _currentLevelAction, levelAction);
            ReplaceToggleAction(_bindings?.WeeklyCatsToggle, ref _currentWeeklyAction, weeklyAction);
            ReplaceToggleAction(_bindings?.DailyMoneyToggle, ref _currentDailyAction, dailyAction);
        }

        public void Render(LeaderboardVm vm)
        {
            if (vm == null)
            {
                return;
            }

            SetText(_bindings?.TitleText, vm.Title);
            SetText(_bindings?.StatusText, BuildStatusText(vm));
            SyncToggles(vm.SelectedType);
            RenderTopEntries(vm.Entries ?? Array.Empty<HolmasLeaderboardEntry>());
            RenderListEntries(vm.Entries ?? Array.Empty<HolmasLeaderboardEntry>());
            RenderSelfEntry(vm.SelfEntry);
        }

        public bool IsSyncingToggles => _syncingToggles;

        private void RenderTopEntries(HolmasLeaderboardEntry[] entries)
        {
            RenderTopEntry(_bindings?.Top1, _top1View, entries, 0);
            RenderTopEntry(_bindings?.Top2, _top2View, entries, 1);
            RenderTopEntry(_bindings?.Top3, _top3View, entries, 2);
        }

        private static void RenderTopEntry(RectTransform root, LeaderboardItemView itemView, HolmasLeaderboardEntry[] entries, int index)
        {
            if (root == null || itemView == null)
            {
                return;
            }

            HolmasLeaderboardEntry entry = entries != null && index >= 0 && index < entries.Length ? entries[index] : null;
            root.gameObject.SetActive(entry != null);
            if (entry != null)
            {
                itemView.Bind(entry, showRank: false);
            }
        }

        private void RenderListEntries(HolmasLeaderboardEntry[] entries)
        {
            _listEntries.Clear();
            if (entries != null)
            {
                for (int i = 3; i < entries.Length; i++)
                {
                    if (entries[i] != null)
                    {
                        _listEntries.Add(entries[i]);
                    }
                }
            }

            _loopListView?.SetItems(_listEntries, resetScroll: true);
        }

        private void RenderSelfEntry(HolmasLeaderboardEntry selfEntry)
        {
            if (_bindings?.MyInfo == null || _myInfoView == null)
            {
                return;
            }

            _bindings.MyInfo.gameObject.SetActive(true);
            _myInfoView.Bind(selfEntry, showRank: true);
        }

        private static string BuildStatusText(LeaderboardVm vm)
        {
            if (vm == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(vm.Status))
            {
                return string.IsNullOrWhiteSpace(vm.PeriodText) ? vm.Status : vm.Status + "  " + vm.PeriodText;
            }

            return vm.PeriodText ?? string.Empty;
        }

        private void SyncToggles(HolmasLeaderboardType type)
        {
            _syncingToggles = true;
            _bindings?.LevelToggle?.SetIsOnWithoutNotify(type == HolmasLeaderboardType.Level);
            _bindings?.WeeklyCatsToggle?.SetIsOnWithoutNotify(type == HolmasLeaderboardType.WeeklyCatsFound);
            _bindings?.DailyMoneyToggle?.SetIsOnWithoutNotify(type == HolmasLeaderboardType.DailyTaskIncome);
            _syncingToggles = false;
        }

        private void ApplyCatSpriteLoaderToItemViews()
        {
            _top1View?.SetCatSpriteLoader(_catSpriteLoader);
            _top2View?.SetCatSpriteLoader(_catSpriteLoader);
            _top3View?.SetCatSpriteLoader(_catSpriteLoader);
            _myInfoView?.SetCatSpriteLoader(_catSpriteLoader);
        }

        private void ApplyAvatarSpriteLoaderToItemViews()
        {
            _top1View?.SetAvatarSpriteLoader(_avatarSpriteLoader);
            _top2View?.SetAvatarSpriteLoader(_avatarSpriteLoader);
            _top3View?.SetAvatarSpriteLoader(_avatarSpriteLoader);
            _myInfoView?.SetAvatarSpriteLoader(_avatarSpriteLoader);
        }

        private void ReleaseAvatarItemViews()
        {
            _top1View?.ReleaseAvatar();
            _top2View?.ReleaseAvatar();
            _top3View?.ReleaseAvatar();
            _myInfoView?.ReleaseAvatar();
        }

        private static void ReplaceButtonAction(Button button, ref UnityAction currentAction, UnityAction nextAction)
        {
            if (button == null)
            {
                currentAction = nextAction;
                return;
            }

            if (currentAction != null)
            {
                button.onClick.RemoveListener(currentAction);
            }

            currentAction = nextAction;
            if (currentAction != null)
            {
                button.onClick.AddListener(currentAction);
            }
        }

        private static void ReplaceToggleAction(Toggle toggle, ref UnityAction<bool> currentAction, UnityAction<bool> nextAction)
        {
            if (toggle == null)
            {
                currentAction = nextAction;
                return;
            }

            if (currentAction != null)
            {
                toggle.onValueChanged.RemoveListener(currentAction);
            }

            currentAction = nextAction;
            if (currentAction != null)
            {
                toggle.onValueChanged.AddListener(currentAction);
            }
        }

        private static void EnsureToggleGroup(params Toggle[] toggles)
        {
            ToggleGroup group = null;
            for (int i = 0; i < toggles.Length; i++)
            {
                if (toggles[i] != null && toggles[i].group != null)
                {
                    group = toggles[i].group;
                    break;
                }
            }

            if (group == null)
            {
                for (int i = 0; i < toggles.Length; i++)
                {
                    if (toggles[i] == null)
                    {
                        continue;
                    }

                    GameObject groupObject = new GameObject("LeaderboardToggleGroup", typeof(RectTransform), typeof(ToggleGroup));
                    groupObject.transform.SetParent(toggles[i].transform.parent, false);
                    group = groupObject.GetComponent<ToggleGroup>();
                    break;
                }
            }

            if (group == null)
            {
                return;
            }

            group.allowSwitchOff = false;
            foreach (Toggle toggle in toggles)
            {
                if (toggle != null)
                {
                    toggle.group = group;
                }
            }
        }

        private T FindByPath<T>(string nodePath) where T : Component
        {
            if (string.IsNullOrWhiteSpace(nodePath))
            {
                return null;
            }

            if (nodePath == LeaderboardBindings.RootNodePath)
            {
                return gameObject.GetComponent<T>();
            }

            string localPath = nodePath.StartsWith(LeaderboardBindings.RootNodePath + "/", StringComparison.Ordinal)
                ? nodePath.Substring(LeaderboardBindings.RootNodePath.Length + 1)
                : nodePath;
            Transform found = transform.Find(localPath);
            return found != null ? found.GetComponent<T>() : null;
        }

        private T FindFirstDescendantByName<T>(string objectName) where T : Component
        {
            Transform[] all = transform.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == objectName)
                {
                    T component = all[i].GetComponent<T>();
                    if (component != null)
                    {
                        return component;
                    }
                }
            }

            return null;
        }

        private static ScrollRect CreateScrollRect(Transform parent, string name)
        {
            RectTransform rect = GetOrCreateRect(parent, name);
            ConfigureRect(rect, new Vector2(0.08f, 0.2f), new Vector2(0.92f, 0.72f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            ScrollRect scrollRect = rect.GetComponent<ScrollRect>() ?? rect.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            return scrollRect;
        }

        private static RectTransform CreateFallbackPlayerInfo(Transform parent, string name)
        {
            RectTransform row = GetOrCreateRect(parent, name);
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(0f, 1f);
            row.pivot = new Vector2(0.5f, 0.5f);
            row.anchoredPosition = new Vector2(522.75f, -89.1f);
            row.sizeDelta = new Vector2(100f, 100f);

            Image background = GetOrCreateRect(row, "Image").GetComponent<Image>() ?? GetOrCreateRect(row, "Image").gameObject.AddComponent<Image>();
            background.color = new Color(1f, 0.92f, 0.62f, 1f);
            ConfigureRect(background.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(980f, 150f));

            CreateText(row, "MyLeadInfo", "未上榜", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-409f, 0f), new Vector2(130f, 50f), 32, TextAnchor.MiddleCenter);
            CreateText(row, "Name", "我是第一", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -48f), new Vector2(240f, 50f), 32, TextAnchor.MiddleCenter);
            CreateText(row, "LeadCount", "100", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(368f, 0f), new Vector2(160f, 50f), 32, TextAnchor.MiddleCenter);
            return row;
        }

        private static RectTransform CreateFallbackTopInfo(Transform parent, string name, Vector2 position)
        {
            RectTransform row = CreateFallbackPlayerInfo(parent, name);
            row.anchoredPosition = position;
            return row;
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
        {
            RectTransform rect = GetOrCreateRect(parent, name);
            ConfigureRect(rect, anchorMin, anchorMax, pivot, position, size);
            Image image = rect.GetComponent<Image>() ?? rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.18f, 0.36f, 0.68f, 0.95f);
            Button button = rect.GetComponent<Button>() ?? rect.gameObject.AddComponent<Button>();
            CreateText(rect, name + "_Label", label, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, 24, TextAnchor.MiddleCenter);
            return button;
        }

        private static Toggle CreateToggle(Transform parent, string name, string label, Vector2 anchor)
        {
            RectTransform rect = GetOrCreateRect(parent, name);
            ConfigureRect(rect, anchor, anchor, new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(180f, 48f));
            Toggle toggle = rect.GetComponent<Toggle>() ?? rect.gameObject.AddComponent<Toggle>();
            Image image = rect.GetComponent<Image>() ?? rect.gameObject.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.18f);
            toggle.targetGraphic = image;
            CreateText(rect, "Label", label, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, 20, TextAnchor.MiddleCenter);
            return toggle;
        }

        private static Text CreateText(Transform parent, string name, string value, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
        {
            RectTransform rect = GetOrCreateRect(parent, name);
            ConfigureRect(rect, anchorMin, anchorMax, pivot, position, size);
            Text text = rect.GetComponent<Text>() ?? rect.gameObject.AddComponent<Text>();
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            SetText(text, value);
            return text;
        }

        private static RectTransform GetOrCreateRect(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                return existing.GetComponent<RectTransform>() ?? existing.gameObject.AddComponent<RectTransform>();
            }

            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            return obj.GetComponent<RectTransform>();
        }

        private static void SetText(Text text, string value)
        {
            if (text != null)
            {
                string safeValue = value ?? string.Empty;
                if (text.text != safeValue)
                {
                    text.text = safeValue;
                }
            }
        }

        private static void Stretch(RectTransform rect)
        {
            ConfigureRect(rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        }

        private static void ConfigureRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
