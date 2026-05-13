using App.HotUpdate.Holmas.UI.Binding;
using App.HotUpdate.Holmas.UI.Generated;
using App.HotUpdate.Holmas.UI.Screens.Leaderboard;
using App.HotUpdate.Holmas.UI.Screens.Loading;
using App.HotUpdate.Holmas.UI.Screens.Main;
using UnityEditor;
using UnityEngine;

namespace Holmas.Editor
{
    public static class HolmasStaticBindingAuthoring
    {
        [MenuItem("Holmas/UI/Refresh All Static Bindings")]
        public static void RefreshAll()
        {
            RefreshMain();
            RefreshLoading();
            RefreshLeaderboard();
            BattlePanelStaticBindingAuthoring.Refresh();
            AgencyMainFormalPrefabAuthoring.GenerateAndValidateForBatchMode();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Holmas static UI bindings refreshed.");
        }

        public static void RefreshAllForBatchMode()
        {
            RefreshAll();
        }

        [MenuItem("Holmas/UI/Refresh MainPanel Static Bindings")]
        public static void RefreshMain()
        {
            RefreshPrefab(
                MainGeneratedBindings.PrefabAssetPath,
                root =>
                {
                    MainView view = root.GetComponent<MainView>() ?? root.AddComponent<MainView>();
                    view.EnsureBindingSurface();
                    ValidateMain(root);
                });
        }

        [MenuItem("Holmas/UI/Refresh LoadingPanel Static Bindings")]
        public static void RefreshLoading()
        {
            RefreshPrefab(
                LoadingGeneratedBindings.PrefabAssetPath,
                root =>
                {
                    LoadingView view = root.GetComponent<LoadingView>() ?? root.AddComponent<LoadingView>();
                    view.EnsureBindingSurface();
                    ValidateLoading(root);
                });
        }

        [MenuItem("Holmas/UI/Refresh Leaderboard Static Bindings")]
        public static void RefreshLeaderboard()
        {
            RefreshPrefab(
                LeaderboardGeneratedBindings.PrefabAssetPath,
                root =>
                {
                    LeaderboardView view = root.GetComponent<LeaderboardView>() ?? root.AddComponent<LeaderboardView>();
                    view.EnsureBindingSurface();
                    ValidateLeaderboard(root);
                });
        }

        private static void RefreshPrefab(string prefabPath, System.Action<GameObject> refresh)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null)
            {
                throw new System.InvalidOperationException("UI prefab 缺失：" + prefabPath);
            }

            try
            {
                refresh(root);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log("Static bindings refreshed: " + prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ValidateMain(GameObject root)
        {
            UiReferenceCollector collector = RequireCollector(root, MainGeneratedBindings.PrefabAssetPath);
            var resolver = new UiBindingResolver(collector, MainGeneratedBindings.Manifest);
            MainBindings bindings = MainBindings.Resolve(resolver);
            if (bindings == null || !bindings.HasRequiredBindings)
            {
                throw new System.InvalidOperationException("MainPanel 静态绑定不完整。" + BuildMainMissingReport(bindings));
            }
        }

        private static string BuildMainMissingReport(MainBindings bindings)
        {
            if (bindings == null)
            {
                return " bindings=null";
            }

            var missing = new System.Collections.Generic.List<string>();
            if (bindings.RootPanel == null) missing.Add(MainBindings.RootPanelKey);
            if (bindings.LevelText == null) missing.Add(MainBindings.LevelTextKey);
            if (bindings.GoldText == null) missing.Add(MainBindings.GoldTextKey);
            if (bindings.EnergyText == null) missing.Add(MainBindings.EnergyTextKey);
            if (bindings.PromotionButton == null) missing.Add(MainBindings.PromotionButtonKey);
            if (bindings.HelpButton == null) missing.Add(MainBindings.HelpButtonKey);
            if (bindings.GmButton == null) missing.Add(MainBindings.GmButtonKey);
            if (bindings.LeaderboardButton == null) missing.Add(MainBindings.LeaderboardButtonKey);
            if (bindings.MinesBgImage == null) missing.Add(MainBindings.MinesBgImageKey);
            if (bindings.MinesBgMask == null) missing.Add(MainBindings.MinesBgMaskKey);
            if (bindings.MinesBgFrameOverlayImage == null) missing.Add(MainBindings.MinesBgFrameOverlayImageKey);
            if (bindings.BoardContentRect == null) missing.Add(MainBindings.BoardContentRectKey);
            if (bindings.MinesGroup == null) missing.Add(MainBindings.MinesGroupKey);
            if (bindings.BoardContainer == null) missing.Add(MainBindings.BoardContainerKey);
            if (bindings.TutorialBoardContainer == null) missing.Add(MainBindings.TutorialBoardContainerKey);
            if (bindings.WalkToggle == null) missing.Add(MainBindings.WalkToggleKey);
            if (bindings.FindToggle == null) missing.Add(MainBindings.FindToggleKey);
            for (int i = 0; i < MainBindings.TaskSlotCount; i++)
            {
                if (bindings.TaskSlotRoots[i] == null) missing.Add(MainBindings.TaskSlotRootKeys[i]);
                if (bindings.TaskSlotButtons[i] == null) missing.Add(MainBindings.TaskSlotButtonKeys[i]);
                if (bindings.TaskSlotBackgroundImages[i] == null) missing.Add(MainBindings.TaskSlotBackgroundImageKeys[i]);
                if (bindings.TaskProgressTexts[i] == null) missing.Add(MainBindings.TaskProgressTextKeys[i]);
                if (bindings.TaskProgressSliders[i] == null) missing.Add(MainBindings.TaskProgressSliderKeys[i]);
                if (bindings.TaskRewardIcons[i] == null) missing.Add(MainBindings.TaskRewardIconKeys[i]);
                if (bindings.TaskCatIcons[i] == null) missing.Add(MainBindings.TaskCatIconKeys[i]);
                if (bindings.TaskLocks[i] == null) missing.Add(MainBindings.TaskLockKeys[i]);
                if (bindings.TaskTitleTexts[i] == null) missing.Add(MainBindings.TaskTitleTextKeys[i]);
                if (bindings.TaskRewardTexts[i] == null) missing.Add(MainBindings.TaskRewardTextKeys[i]);
            }

            return missing.Count > 0 ? " missing=" + string.Join(", ", missing) : string.Empty;
        }

        private static void ValidateLoading(GameObject root)
        {
            UiReferenceCollector collector = RequireCollector(root, LoadingGeneratedBindings.PrefabAssetPath);
            var resolver = new UiBindingResolver(collector, LoadingGeneratedBindings.Manifest);
            LoadingBindings bindings = LoadingBindings.Resolve(resolver);
            if (bindings == null || !bindings.HasRequiredBindings)
            {
                throw new System.InvalidOperationException("LoadingPanel 静态绑定不完整。");
            }
        }

        private static void ValidateLeaderboard(GameObject root)
        {
            UiReferenceCollector collector = RequireCollector(root, LeaderboardGeneratedBindings.PrefabAssetPath);
            var resolver = new UiBindingResolver(collector, LeaderboardGeneratedBindings.Manifest);
            LeaderboardBindings bindings = LeaderboardBindings.Resolve(resolver);
            if (bindings == null || !bindings.HasRequiredBindings)
            {
                throw new System.InvalidOperationException("LeadbroadPanel 静态绑定不完整。");
            }
        }

        private static UiReferenceCollector RequireCollector(GameObject root, string prefabPath)
        {
            UiReferenceCollector collector = root.GetComponent<UiReferenceCollector>();
            if (collector == null)
            {
                throw new System.InvalidOperationException(prefabPath + " 缺少 UiReferenceCollector。");
            }

            return collector;
        }
    }
}
