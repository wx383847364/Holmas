using System.Collections;
using System.Threading.Tasks;
using App.HotUpdate.Holmas.UI.Binding;
using App.HotUpdate.Holmas.UI.Generated;
using App.HotUpdate.Holmas.UI.Screens.Battle;
using App.HotUpdate.Holmas.UI.Screens.Leaderboard;
using App.HotUpdate.Holmas.UI.Screens.Loading;
using App.HotUpdate.Holmas.UI.Screens.Main;
using App.Shared.Contracts;
using App.Shared.Holmas.Leaderboards;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Holmas.Tests
{
    public sealed class ExistingPrefabUiScreenBindingTests
    {
        [Test]
        public void MainPrefab_CanBuildRuntimeBindings()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MainGeneratedBindings.PrefabAssetPath);
            Assert.That(prefab, Is.Not.Null, "MainPanel prefab 缺失。");

            GameObject instance = Object.Instantiate(prefab);
            try
            {
                MainView view = instance.GetComponent<MainView>() ?? instance.AddComponent<MainView>();
                view.EnsureBindingSurface();

                UiReferenceCollector collector = instance.GetComponent<UiReferenceCollector>();
                Assert.That(collector, Is.Not.Null, "MainPanel 运行时 collector 创建失败。");

                var resolver = new UiBindingResolver(collector, MainGeneratedBindings.Manifest);
                MainBindings bindings = MainBindings.Resolve(resolver);
                Assert.That(bindings.HasRequiredBindings, Is.True, "MainBindings 未能解析最小运行时 binding。");
                Assert.That(bindings.MinesGroup.name, Is.EqualTo("MinesGroup"));
                Assert.That(bindings.BoardContainer.name, Is.EqualTo("BoardContainer"));
                Assert.That(bindings.BoardContainer.IsChildOf(bindings.MinesGroup), Is.True, "Main 内嵌棋盘必须挂在 MinesGroup 下。");
                Assert.That(bindings.TutorialBoardContainer.name, Is.EqualTo("TutorialBoardContainer"));
                Assert.That(bindings.TutorialBoardContainer.IsChildOf(bindings.MinesGroup), Is.True, "教程棋盘容器必须挂在 MinesGroup 下。");
                Assert.That(instance.transform.Find("RuntimeOverlay/StartButton"), Is.Null, "MainPanel 不应再创建开始/继续找猫按钮。");
                Assert.That(instance.transform.Find("RuntimeOverlay/StatusText"), Is.Null, "主界面状态文本应移入 GM 工具 Runtime 信息区。");
                Assert.That(bindings.StatusText, Is.Null, "MainPanel 不应再绑定常驻 StatusText。");
                Assert.That(bindings.HelpButton.name, Is.EqualTo("HelpButton"), "MainPanel 应创建可重看教程的帮助按钮。");
                Assert.That(bindings.GmButton.name, Is.EqualTo("GmButton"), "MainPanel 应创建独立 GM 调试入口。");
                Assert.That(bindings.LeaderboardButton.name, Is.EqualTo("Leaderboard_btn"), "MainPanel 应绑定现有排行榜按钮。");
                Assert.That(instance.transform.Find("BackgroundImage/Leaderboard_btn"), Is.Not.Null, "排行榜按钮必须保留在 prefab 原始路径。");
                Assert.That(instance.transform.Find("RuntimeOverlay/TopTools/HelpButton"), Is.Not.Null, "帮助按钮应位于右上角工具区。");
                Assert.That(instance.transform.Find("RuntimeOverlay/TopTools/GmButton"), Is.Not.Null, "GM 按钮应位于右上角工具区。");
                Assert.That(instance.transform.Find("RuntimeOverlay/BottomTools/HelpButton"), Is.Null, "帮助按钮不应残留在底部工具区。");
                Assert.That(instance.transform.Find("RuntimeOverlay/BottomTools/GmButton"), Is.Null, "GM 按钮不应残留在底部工具区。");
                Assert.That(instance.transform.Find("RuntimeOverlay/BottomTools/StartTutorialButton"), Is.Null, "MainPanel 不应再创建开始引导按钮。");
                RectTransform topTools = instance.transform.Find("RuntimeOverlay/TopTools") as RectTransform;
                Assert.That(topTools, Is.Not.Null, "MainPanel 应创建右上角工具区。");
                Assert.That(topTools.anchorMin, Is.EqualTo(Vector2.one), "右上角工具区应锚定到屏幕右上角。");
                Assert.That(topTools.anchorMax, Is.EqualTo(Vector2.one), "右上角工具区应锚定到屏幕右上角。");
                Assert.That(topTools.pivot, Is.EqualTo(Vector2.one), "右上角工具区 pivot 应位于右上角。");
                Assert.That(bindings.AddEnergyButton, Is.Null, "MainPanel 不应再常驻创建加体力按钮。");
                Assert.That(bindings.TutorialStepInput, Is.Null, "MainPanel 不应再常驻创建开发模式步骤输入框。");
                Assert.That(bindings.SummaryText, Is.Null, "MainPanel 不应再常驻展示 RuntimeOverlay 调试信息。");
                Assert.That(bindings.WalkToggle.isOn, Is.True, "WalkToggle 应作为默认行走模式。");
                Assert.That(bindings.FindToggle, Is.Not.Null, "FindToggle 缺失。");
                Assert.That(bindings.FindToggle.isOn, Is.False, "FindToggle 默认不应选中。");
                Assert.That(bindings.WalkToggle.group, Is.SameAs(bindings.FindToggle.group), "Walk/Find Toggle 必须属于同一个互斥 ToggleGroup。");
                Assert.That(bindings.WalkToggle.group.allowSwitchOff, Is.False, "Walk/Find Toggle 不允许同时关闭。");

                bindings.FindToggle.isOn = true;
                Assert.That(bindings.WalkToggle.isOn, Is.False, "FindToggle 打开后 WalkToggle 必须关闭。");

                bindings.WalkToggle.isOn = true;
                Assert.That(bindings.FindToggle.isOn, Is.False, "WalkToggle 打开后 FindToggle 必须关闭。");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void LeaderboardPrefab_CanBuildRuntimeBindings()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LeaderboardGeneratedBindings.PrefabAssetPath);
            Assert.That(prefab, Is.Not.Null, "LeadbroadPanel prefab 缺失。");

            GameObject instance = Object.Instantiate(prefab);
            try
            {
                LeaderboardView view = instance.GetComponent<LeaderboardView>() ?? instance.AddComponent<LeaderboardView>();
                view.EnsureBindingSurface();

                UiReferenceCollector collector = instance.GetComponent<UiReferenceCollector>();
                Assert.That(collector, Is.Not.Null, "LeadbroadPanel 运行时 collector 创建失败。");

                var resolver = new UiBindingResolver(collector, LeaderboardGeneratedBindings.Manifest);
                LeaderboardBindings bindings = LeaderboardBindings.Resolve(resolver);
                Assert.That(bindings.HasRequiredBindings, Is.True, "LeaderboardBindings 未能解析最小运行时 binding。");
                Assert.That(bindings.BackButton.name, Is.EqualTo("Back_btn"));
                Assert.That(bindings.RewardButton.name, Is.EqualTo("RewardInfo_btn"));
                Assert.That(bindings.DailyMoneyToggle.name, Is.EqualTo("DaliyMoneyToggle"), "资源拼写保持原样，代码 binding 负责封装。");
                Assert.That(bindings.LeaderInfo.name, Is.EqualTo("LeaderInfo"));
                Assert.That(bindings.LeaderList.name, Is.EqualTo("LeaderList"));
                Assert.That(bindings.LeaderListContent.name, Is.EqualTo("GameObject"));
                Assert.That(bindings.LeaderListContent.IsChildOf(bindings.LeaderList.transform), Is.True, "排行榜列表 content 必须挂在 LeaderList 下。");
                Assert.That(bindings.ItemTemplate.name, Is.EqualTo("PlayerInfo"), "虚拟列表模板固定使用 LeaderList/GameObject/PlayerInfo。");
                Assert.That(bindings.ItemTemplate.IsChildOf(bindings.LeaderListContent), Is.True, "PlayerInfo 模板必须位于 LeaderList/GameObject 下。");
                Assert.That(bindings.MyInfo.name, Is.EqualTo("MyInfo"));
                Assert.That(bindings.Top1.name, Is.EqualTo("No.1"));
                Assert.That(bindings.Top2.name, Is.EqualTo("No.2"));
                Assert.That(bindings.Top3.name, Is.EqualTo("No.3"));
                Assert.That(HasChildImage(bindings.ItemTemplate, "HeadIcon"), Is.True, "PlayerInfo 模板应包含头像 HeadIcon。");
                Assert.That(HasChildImage(bindings.ItemTemplate, "FrameIcon"), Is.True, "PlayerInfo 模板应包含头像框 FrameIcon。");
                Assert.That(HasChildImage(bindings.ItemTemplate, "LeadIcon"), Is.True, "PlayerInfo 模板应包含分数图标 LeadIcon。");
                Assert.That(instance.transform.Find("LeaderboardRuntimeOverlay"), Is.Null, "排行榜不应再创建 RuntimeOverlay 文本列表。");

                view.Bind(bindings);
                view.Render(new LeaderboardVm
                {
                    SelectedType = HolmasLeaderboardType.Level,
                    Title = "等级总榜",
                    PeriodText = "长期榜",
                    Entries = BuildLeaderboardEntries(100),
                    SelfEntry = new HolmasLeaderboardEntry { Rank = 88, DisplayName = "local-player", Score = 12, IsSelf = true },
                });

                LeaderboardLoopListView loopList = bindings.LeaderList.GetComponent<LeaderboardLoopListView>();
                Assert.That(loopList, Is.Not.Null, "LeaderList 应挂载排行榜专用虚拟列表组件。");
                Assert.That(bindings.LeaderList.viewport, Is.EqualTo(bindings.LeaderList.GetComponent<RectTransform>()), "LeaderList 自身应作为固定 viewport，GameObject 只作为滚动 content。");
                Assert.That(bindings.LeaderList.GetComponent<RectMask2D>(), Is.Not.Null, "固定 viewport 必须带 RectMask2D 裁剪运行时 item。");
                Assert.That(bindings.LeaderList.GetComponent<RectMask2D>().enabled, Is.True, "固定 viewport 裁剪必须启用。");
                Mask contentMask = bindings.LeaderListContent.GetComponent<Mask>();
                Assert.That(contentMask == null || !contentMask.enabled, Is.True, "GameObject content 不应继续作为裁剪 viewport。");
                RectMask2D contentRectMask = bindings.LeaderListContent.GetComponent<RectMask2D>();
                Assert.That(contentRectMask == null || !contentRectMask.enabled, Is.True, "GameObject content 不应继续作为裁剪 viewport。");
                Assert.That(bindings.LeaderListContent.anchorMin, Is.EqualTo(new Vector2(0f, 1f)), "可变高 content 应锚定到 viewport 顶部。");
                Assert.That(bindings.LeaderListContent.anchorMax, Is.EqualTo(new Vector2(1f, 1f)), "可变高 content 应横向拉伸并锚定到 viewport 顶部。");
                Assert.That(bindings.LeaderListContent.pivot, Is.EqualTo(new Vector2(0.5f, 1f)), "可变高 content 应使用顶部 pivot，避免高度变化时整体漂移。");
                Assert.That(loopList.ItemCount, Is.EqualTo(97), "第 4 名之后的数据才进入虚拟列表。");
                Assert.That(loopList.PoolCapacity, Is.LessThan(20), "100 条排行榜数据不应创建 100 个 item。");
                Assert.That(loopList.ActivePoolItemCount, Is.EqualTo(loopList.PoolCapacity));
                RectTransform firstRuntimeItem = bindings.LeaderListContent.Find("PlayerInfo_Runtime_00") as RectTransform;
                Assert.That(firstRuntimeItem, Is.Not.Null, "虚拟列表应创建可复用运行时 item。");
                AssertRuntimeItemUsesTopAnchor(firstRuntimeItem);
                Assert.That(firstRuntimeItem.anchoredPosition.x, Is.EqualTo(0f).Within(0.001f), "运行时 item 应以 viewport 中心作为水平基准。");
                Assert.That(firstRuntimeItem.anchoredPosition.y, Is.EqualTo(ExpectedFirstRuntimeItemY(bindings.ItemTemplate)).Within(0.001f), "首个运行时 item 应按视觉顶部和顶部留白定位。");
                Assert.That(RuntimeItemVisualTopInsideViewport(firstRuntimeItem, bindings.LeaderList.viewport), Is.True, "首个运行时 item 的可见顶部不应被 viewport 裁切。");
                Assert.That(RuntimeItemKeyChildrenInsideViewport(firstRuntimeItem, bindings.LeaderList.viewport), Is.True, "首个运行时 item 的关键文本应位于 viewport 内。");
                Assert.That(ChildCenterInsideViewport(firstRuntimeItem, bindings.LeaderList.viewport, "HeadIcon"), Is.True, "首个运行时 item 的头像应位于 viewport 内。");
                Assert.That(AnyActiveRuntimeItemCenterInsideViewport(bindings.LeaderListContent, bindings.LeaderList.viewport), Is.True, "至少一个 active item 应位于固定 viewport 裁剪范围内。");

                loopList.SetScrollOffsetForTests(160f * 12f);
                Assert.That(loopList.FirstVisibleIndex, Is.GreaterThanOrEqualTo(10), "滚动到中段后应复用 item 展示中段数据。");
                Assert.That(ActiveRuntimeItemContainsText(bindings.LeaderListContent, "No.14"), Is.True, "滚动到中段后，复用 item 应重绑正确名次。");
                Assert.That(ActiveRuntimeItemContainsText(bindings.LeaderListContent, "玩家14"), Is.True, "滚动到中段后，复用 item 应重绑正确昵称。");
                RectTransform rank14Item = FindActiveRuntimeItemContainingText(bindings.LeaderListContent, "No.14");
                Assert.That(rank14Item, Is.Not.Null, "滚动到中段后应能定位到 No.14 对应的复用 item。");
                AssertRuntimeItemUsesTopAnchor(rank14Item);
                Assert.That(rank14Item.anchoredPosition.x, Is.EqualTo(0f).Within(0.001f), "滚动后运行时 item 仍应以 viewport 中心作为水平基准。");
                Assert.That(rank14Item.anchoredPosition.y, Is.EqualTo(ExpectedFirstRuntimeItemY(bindings.ItemTemplate) - 10f * 160f).Within(0.001f), "No.14 对应 dataIndex=10，应按顶部坐标系定位。");
                Assert.That(AnyActiveRuntimeItemCenterInsideViewport(bindings.LeaderListContent, bindings.LeaderList.viewport), Is.True, "滚动后 active item 仍应落在固定 viewport 裁剪范围内。");

                view.Render(new LeaderboardVm
                {
                    SelectedType = HolmasLeaderboardType.DailyTaskIncome,
                    Title = "财富日榜",
                    PeriodText = "周期：20260505",
                    Entries = BuildLeaderboardEntries(4),
                    SelfEntry = new HolmasLeaderboardEntry { Rank = 0, DisplayName = "local-player", Score = 7, IsSelf = true },
                });

                Assert.That(loopList.ItemCount, Is.EqualTo(1), "切榜后旧榜列表数据不应残留。");
                Assert.That(loopList.ActivePoolItemCount, Is.EqualTo(1), "数据少于可见数量时，多余复用 item 应隐藏。");
                Assert.That(bindings.LeaderListContent.anchoredPosition.y, Is.EqualTo(0f).Within(0.001f), "切榜后列表应回到顶部。");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void LeaderboardAvatarSpriteLoader_UsesWechatAvatarBeforeDefaultIcon()
        {
            var imageObject = new GameObject("HeadIcon", typeof(RectTransform), typeof(Image));
            try
            {
                Image image = imageObject.GetComponent<Image>();
                var netClient = new FakeNetClient(CreateSinglePixelPng());
                var loader = new LeaderboardAvatarSpriteLoader(null, netClient);

                try
                {
                    loader.Bind(image, new HolmasLeaderboardEntry
                    {
                        PlayerId = "self",
                        DisplayName = "本地玩家",
                        WechatAvatarUrl = "https://avatar.example/self.png",
                        AvatarIconPath = HolmasLeaderboardAvatarDefaults.DefaultAvatarIconPath,
                    });

                    Assert.That(netClient.LastUrl, Is.EqualTo("https://avatar.example/self.png"));
                    Assert.That(image.sprite, Is.Not.Null, "存在微信头像 URL 时应优先使用远程头像结果。");
                    Assert.That(image.preserveAspect, Is.True);
                }
                finally
                {
                    loader.Dispose();
                }
            }
            finally
            {
                Object.DestroyImmediate(imageObject);
            }
        }

        [UnityTest]
        public IEnumerator LeaderboardAvatarSpriteLoader_IgnoresStaleRemoteAvatarAfterFallbackOnlyRebind()
        {
            var imageObject = new GameObject("HeadIcon", typeof(RectTransform), typeof(Image));
            try
            {
                Image image = imageObject.GetComponent<Image>();
                var netClient = new DeferredNetClient();
                var loader = new LeaderboardAvatarSpriteLoader(null, netClient);

                try
                {
                    loader.Bind(image, new HolmasLeaderboardEntry
                    {
                        PlayerId = "old-player",
                        DisplayName = "旧玩家",
                        WechatAvatarUrl = "https://avatar.example/old.png",
                        AvatarIconPath = HolmasLeaderboardAvatarDefaults.DefaultAvatarIconPath,
                    });

                    Assert.That(netClient.LastUrl, Is.EqualTo("https://avatar.example/old.png"));

                    loader.Bind(image, new HolmasLeaderboardEntry
                    {
                        PlayerId = "new-player",
                        DisplayName = "新玩家",
                        AvatarIconPath = HolmasLeaderboardAvatarDefaults.DefaultAvatarIconPath,
                    });

                    netClient.Complete(CreateSinglePixelPng());
                    yield return null;
                    yield return null;

                    Assert.That(image.sprite, Is.Null, "复用 item 重新绑定到无微信头像玩家后，旧远程请求不应覆盖当前头像。");
                }
                finally
                {
                    loader.Dispose();
                }
            }
            finally
            {
                Object.DestroyImmediate(imageObject);
            }
        }

        [Test]
        public void BattlePrefab_CanBuildRuntimeBindings()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BattleGeneratedBindings.PrefabAssetPath);
            Assert.That(prefab, Is.Not.Null, "BattlePanel prefab 缺失。");

            GameObject instance = Object.Instantiate(prefab);
            try
            {
                BattleView view = instance.GetComponent<BattleView>();
                Assert.That(view, Is.Not.Null, "BattlePanel prefab 必须静态挂载 BattleView，运行时不再 AddComponent 补挂。");

                UiReferenceCollector collector = instance.GetComponent<UiReferenceCollector>();
                Assert.That(collector, Is.Not.Null, "BattlePanel prefab 必须静态挂载 UiReferenceCollector，运行时不再查找/创建 binding surface。");

                var resolver = new UiBindingResolver(collector, BattleGeneratedBindings.Manifest);
                BattleBindings bindings = BattleBindings.Resolve(resolver);
                Assert.That(bindings.HasRequiredBindings, Is.True, "BattleBindings 未能解析最小运行时 binding。");
                Assert.That(instance.transform.Find("RuntimeOverlay/AddEnergyButton"), Is.Null, "BattlePanel 不应再创建加体力按钮。");
                Assert.That(instance.transform.Find("RuntimeOverlay/BoardContainer"), Is.Null, "BattlePanel 不应再创建全屏找猫棋盘容器。");
                Assert.That(bindings.BackButton.name, Is.EqualTo("Back_btn"));
                Assert.That(bindings.BuildButton.name, Is.EqualTo("Build_btn"));
                for (int i = 0; i < BattlePresenter.VisibleStageCount; i++)
                {
                    Assert.That(bindings.StageButtons[i], Is.Not.Null, $"Stage{i + 1} 按钮缺失。");
                    Assert.That(bindings.StageImages[i], Is.Not.Null, $"Stage{i + 1} 主视觉缺失。");
                    Assert.That(bindings.StageNameTexts[i], Is.Not.Null, $"Stage{i + 1} 名称文本缺失。");
                    Assert.That(bindings.StageLocks[i], Is.Not.Null, $"Stage{i + 1} 锁定态缺失。");
                    Assert.That(bindings.StageImages[i].transform, Is.EqualTo(bindings.StageButtons[i].transform), $"Stage{i + 1} 建筑主图应绑定按钮本体，不能绑定到 Image 标签底板。");
                    Assert.That(bindings.StageLocks[i].name, Is.EqualTo("lock"), $"Stage{i + 1} 锁定态必须静态绑定 prefab 内的 lock 节点。");
                    Assert.That(bindings.StageLocks[i].transform.parent.name, Is.EqualTo("Image"), $"Stage{i + 1} 锁定态必须来自 Stage/Image/lock，运行时不做递归查找。");
                    Assert.That(bindings.BuildStageButtons[i], Is.Not.Null, $"BuildStage{i + 1} 按钮缺失。");
                    Assert.That(bindings.BuildStageImages[i], Is.Not.Null, $"BuildStage{i + 1} 主视觉缺失。");
                    Assert.That(bindings.BuildStageNameTexts[i], Is.Not.Null, $"BuildStage{i + 1} 名称文本缺失。");
                    Assert.That(bindings.BuildStageLocks[i], Is.Not.Null, $"BuildStage{i + 1} 锁定态缺失。");
                    Assert.That(bindings.BuildStageBaseStarGroups[i], Is.Not.Null, $"BuildStage{i + 1} 底星容器缺失。");
                    Assert.That(bindings.BuildStageActiveStarGroups[i], Is.Not.Null, $"BuildStage{i + 1} 点亮星容器缺失。");
                    Assert.That(bindings.BuildStageButtons[i].name, Is.EqualTo($"BuildStage{i + 1}"), $"BuildStage{i + 1} 必须静态绑定到底部 promotion slot。");
                    Assert.That(bindings.BuildStageImages[i].transform.parent, Is.EqualTo(bindings.BuildStageButtons[i].transform), $"BuildStage{i + 1}/Image 必须是底部 promotion slot 的直接子节点。");
                }

                view.Bind(bindings);
                RectTransform stageBar1Rect = bindings.StageBars[0].GetComponent<RectTransform>();
                Vector2 stageBar1AnchorMin = stageBar1Rect.anchorMin;
                Vector2 stageBar1AnchorMax = stageBar1Rect.anchorMax;
                Vector2 stageBar1AnchoredPosition = stageBar1Rect.anchoredPosition;
                Vector2 stageBar1SizeDelta = stageBar1Rect.sizeDelta;
                Quaternion stageBar1Rotation = stageBar1Rect.localRotation;
                var stages = new BattleStageVm[BattlePresenter.VisibleStageCount];
                for (int i = 0; i < stages.Length; i++)
                {
                    stages[i] = new BattleStageVm
                    {
                        SlotIndex = i,
                        AgencyStageId = i + 1,
                        StageName = $"Stage {i + 1}",
                        ProgressLabel = "1/3",
                        StarCount = i == 0 ? 2 : 0,
                        Visible = true,
                        Unlocked = true,
                        Selected = i == 0,
                    };
                }

                view.Render(new BattleVm
                {
                    BuildButtonLabel = "Stage 1\n选择宣传项升级",
                    BuildButtonEnabled = true,
                    PromotionSlots = new[]
                    {
                        new BattlePromotionSlotVm
                        {
                            SlotIndex = 0,
                            AgencyStageId = 1,
                            PromotionId = "leaflet",
                            StageName = "Stage 1",
                            StageImage = "Assets/HotUpdateContent/Res/Textures/buildings/building01.png",
                            ProgressLabel = "2/3",
                            StarCap = 3,
                            StarCount = 2,
                            Visible = true,
                            Unlocked = true,
                            Current = true,
                            CanBuild = true,
                        },
                        new BattlePromotionSlotVm
                        {
                            SlotIndex = 1,
                            AgencyStageId = 1,
                            PromotionId = "radio",
                            StageName = "Stage 1",
                            StageImage = "Assets/HotUpdateContent/Res/Textures/buildings/building01.png",
                            ProgressLabel = "1/3",
                            StarCap = 3,
                            StarCount = 1,
                            Visible = true,
                            Unlocked = true,
                            Current = true,
                            CanBuild = true,
                        },
                        new BattlePromotionSlotVm
                        {
                            SlotIndex = 2,
                            AgencyStageId = 1,
                            PromotionId = "tv",
                            StageName = "Stage 1",
                            StageImage = "Assets/HotUpdateContent/Res/Textures/buildings/building01.png",
                            ProgressLabel = "0/3",
                            StarCap = 3,
                            StarCount = 0,
                            Visible = true,
                            Unlocked = true,
                            Current = true,
                            CanBuild = true,
                        },
                    },
                    Stages = stages,
                    StageBars = new[]
                    {
                        new BattleStageBarVm { SlotIndex = 0, Visible = true, Progress = 0.4f },
                        new BattleStageBarVm { SlotIndex = 1, Visible = true, Progress = 0f },
                        new BattleStageBarVm { SlotIndex = 2, Visible = true, Progress = 0f },
                        new BattleStageBarVm { SlotIndex = 3, Visible = true, Progress = 0f },
                    },
                });

                Assert.That(bindings.StageNameTexts[0].color, Is.Not.EqualTo(Color.white), "Stage 标签文字不能再是白色，否则会淹没在白色标签底板里。");
                Assert.That(
                    bindings.StageNameTexts[0].transform.GetSiblingIndex(),
                    Is.EqualTo(bindings.StageNameTexts[0].transform.parent.childCount - 1),
                    "Stage 标签文字必须绘制在标签底板之上。");
                Assert.That(bindings.StageLocks[0].gameObject.activeSelf, Is.False, "已解锁 Stage 必须关闭静态绑定的 Image/lock 节点。");
                Transform buildStarGroup = bindings.BuildStageButtons[0].transform.Find("stargroup");
                Assert.That(buildStarGroup, Is.Not.Null, "BuildStage1 应保留 stargroup 星级容器。");
                Assert.That(buildStarGroup.gameObject.activeSelf, Is.True, "stargroup 是常亮底星层，不应被运行时关闭。");
                Transform buildActiveStarGroup = bindings.BuildStageButtons[0].transform.Find("stargroup_1");
                Assert.That(buildActiveStarGroup, Is.Not.Null, "BuildStage1 应保留 stargroup_1 点亮星级容器。");
                Assert.That(buildActiveStarGroup.gameObject.activeSelf, Is.True, "stargroup_1 是实际星级显示层，不应被运行时关闭。");
                RectTransform buildSlotRect = bindings.BuildStageButtons[0].GetComponent<RectTransform>();
                RectTransform buildImageRect = bindings.BuildStageImages[0].rectTransform;
                RectTransform buildStarRect = buildStarGroup as RectTransform;
                Assert.That(buildSlotRect.sizeDelta.y, Is.EqualTo(190f).Within(0.01f), "BuildStage slot 需要足够高度容纳星星、114x114 图标和文字。");
                Assert.That(buildImageRect.sizeDelta, Is.EqualTo(new Vector2(114f, 114f)), "BuildStage 图标默认尺寸必须是 114x114。");
                Assert.That(buildStarRect.anchorMin, Is.EqualTo(new Vector2(0.5f, 1f)), "星星应锚定在 slot 顶部。");
                Assert.That(buildStarRect.anchoredPosition.y, Is.EqualTo(-20f).Within(0.01f), "星星应在图标上方，而不是压到图标中间。");
                int activeStarCount = 0;
                int activeSlotCount = 0;
                for (int i = 0; i < buildActiveStarGroup.childCount; i++)
                {
                    Transform child = buildActiveStarGroup.GetChild(i);
                    if (child == null || !child.name.StartsWith("star", System.StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (!child.gameObject.activeSelf)
                    {
                        continue;
                    }

                    activeSlotCount++;
                    Image starImage = child.GetComponent<Image>();
                    if (starImage != null && starImage.color.a > 0.5f)
                    {
                        activeStarCount++;
                    }
                }

                Assert.That(activeSlotCount, Is.EqualTo(3), "promotion 星级槽位数量必须跟随 promotionLevelCaps，而不是固定 5 颗。");
                Assert.That(activeStarCount, Is.EqualTo(2), "promotion 星级显示必须跟随当前 promotion 等级。");
                Assert.That(bindings.BuildStageButtons[3].gameObject.activeSelf, Is.False, "promotionLevelCaps 只有 3 项时，第 4 个底部 slot 必须隐藏。");
                Assert.That(bindings.BuildStageButtons[4].gameObject.activeSelf, Is.False, "promotionLevelCaps 只有 3 项时，第 5 个底部 slot 必须隐藏。");
                Assert.That(bindings.BuildButton.transform.Find("Image").gameObject.activeSelf, Is.False, "Build_btn 旧单图标必须隐藏，底部应显示 promotion slots。");

                for (int i = 0; i < BattlePresenter.VisibleStageBarCount; i++)
                {
                    Assert.That(bindings.StageBars[i], Is.Not.Null, $"StageBar{i + 1} 缺失。");
                }

                Assert.That(bindings.StageBars, Has.Length.EqualTo(4), "5 个城市之间只需要 4 条 StageBar 连接线。");
                Assert.That(bindings.StageBars[0].value, Is.EqualTo(0.4f).Within(0.0001f), "StageBar1 必须通过 Slider.value 表示连接进度。");
                Assert.That(stageBar1Rect.anchorMin, Is.EqualTo(stageBar1AnchorMin), "运行时不能重写 prefab 内 StageBar1 的 anchorMin。");
                Assert.That(stageBar1Rect.anchorMax, Is.EqualTo(stageBar1AnchorMax), "运行时不能重写 prefab 内 StageBar1 的 anchorMax。");
                Assert.That(stageBar1Rect.anchoredPosition, Is.EqualTo(stageBar1AnchoredPosition), "运行时不能重写 prefab 内 StageBar1 的位置。");
                Assert.That(stageBar1Rect.sizeDelta, Is.EqualTo(stageBar1SizeDelta), "运行时不能重写 prefab 内 StageBar1 的尺寸。");
                Assert.That(stageBar1Rect.localRotation, Is.EqualTo(stageBar1Rotation), "运行时不能重写 prefab 内 StageBar1 的旋转。");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void LoadingPrefab_CanBuildRuntimeBindings()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LoadingGeneratedBindings.PrefabAssetPath);
            Assert.That(prefab, Is.Not.Null, "LoadingPanel prefab 缺失。");

            GameObject instance = Object.Instantiate(prefab);
            try
            {
                LoadingView view = instance.GetComponent<LoadingView>() ?? instance.AddComponent<LoadingView>();
                view.EnsureBindingSurface();

                UiReferenceCollector collector = instance.GetComponent<UiReferenceCollector>();
                Assert.That(collector, Is.Not.Null, "LoadingPanel 运行时 collector 创建失败。");

                var resolver = new UiBindingResolver(collector, LoadingGeneratedBindings.Manifest);
                LoadingBindings bindings = LoadingBindings.Resolve(resolver);
                Assert.That(bindings.HasRequiredBindings, Is.True, "LoadingBindings 未能解析最小运行时 binding。");

                view.Bind(bindings);
                bindings.LoadingBar.value = 1f;
                view.Render(new LoadingVm
                {
                    Status = "Loading...",
                    Progress = 0f,
                    TargetProgress = 0.5f,
                    AnimationDurationSeconds = 2f,
                    Animate = true,
                });
                Assert.That(bindings.LoadingBar.value, Is.EqualTo(0f), "LoadingPanel 每次打开时必须从传入进度开始，不能沿用 prefab 默认满格。");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static HolmasLeaderboardEntry[] BuildLeaderboardEntries(int count)
        {
            var entries = new HolmasLeaderboardEntry[count];
            for (int i = 0; i < count; i++)
            {
                entries[i] = new HolmasLeaderboardEntry
                {
                    PlayerId = "player-" + (i + 1).ToString("00"),
                    DisplayName = "玩家" + (i + 1).ToString("00"),
                    AvatarIconPath = HolmasLeaderboardAvatarDefaults.DefaultAvatarIconPath,
                    Rank = i + 1,
                    Score = 1000 - i,
                    IsSelf = i == 9,
                };
            }

            return entries;
        }

        private static bool ActiveRuntimeItemContainsText(RectTransform content, string expectedText)
        {
            return FindActiveRuntimeItemContainingText(content, expectedText) != null;
        }

        private static RectTransform FindActiveRuntimeItemContainingText(RectTransform content, string expectedText)
        {
            if (content == null)
            {
                return null;
            }

            for (int i = 0; i < content.childCount; i++)
            {
                Transform child = content.GetChild(i);
                if (child == null || !child.gameObject.activeSelf || !child.name.StartsWith("PlayerInfo_Runtime", System.StringComparison.Ordinal))
                {
                    continue;
                }

                Text[] texts = child.GetComponentsInChildren<Text>(true);
                for (int textIndex = 0; textIndex < texts.Length; textIndex++)
                {
                    if (texts[textIndex] != null && texts[textIndex].text == expectedText)
                    {
                        return child as RectTransform;
                    }
                }
            }

            return null;
        }

        private static bool AnyActiveRuntimeItemCenterInsideViewport(RectTransform content, RectTransform viewport)
        {
            if (content == null || viewport == null)
            {
                return false;
            }

            Vector3[] viewportCorners = new Vector3[4];
            viewport.GetWorldCorners(viewportCorners);
            Rect viewportRect = Rect.MinMaxRect(
                viewportCorners[0].x,
                viewportCorners[0].y,
                viewportCorners[2].x,
                viewportCorners[2].y);

            for (int i = 0; i < content.childCount; i++)
            {
                RectTransform child = content.GetChild(i) as RectTransform;
                if (child == null || !child.gameObject.activeSelf || !child.name.StartsWith("PlayerInfo_Runtime", System.StringComparison.Ordinal))
                {
                    continue;
                }

                Vector3 worldCenter = child.TransformPoint(child.rect.center);
                if (viewportRect.Contains(new Vector2(worldCenter.x, worldCenter.y)))
                {
                    return true;
                }
            }

            return false;
        }

        private static void AssertRuntimeItemUsesTopAnchor(RectTransform item)
        {
            Assert.That(item, Is.Not.Null);
            Assert.That(item.anchorMin, Is.EqualTo(new Vector2(0.5f, 1f)), "运行时 item 必须使用顶部居中锚点。");
            Assert.That(item.anchorMax, Is.EqualTo(new Vector2(0.5f, 1f)), "运行时 item 必须使用顶部居中锚点。");
        }

        private static bool RuntimeItemKeyChildrenInsideViewport(RectTransform item, RectTransform viewport)
        {
            return ChildCenterInsideViewport(item, viewport, "MyLeadInfo") &&
                   ChildCenterInsideViewport(item, viewport, "Name") &&
                   ChildCenterInsideViewport(item, viewport, "LeadCount");
        }

        private static bool HasChildImage(RectTransform root, string childName)
        {
            Transform child = root != null ? root.Find(childName) : null;
            return child != null && child.GetComponent<Image>() != null;
        }

        private static float ExpectedFirstRuntimeItemY(RectTransform template)
        {
            return -12f - CalculateVisualTop(template);
        }

        private static bool RuntimeItemVisualTopInsideViewport(RectTransform item, RectTransform viewport)
        {
            if (item == null || viewport == null)
            {
                return false;
            }

            Vector3[] viewportCorners = new Vector3[4];
            viewport.GetWorldCorners(viewportCorners);
            return CalculateWorldVisualTop(item) <= viewportCorners[1].y + 0.001f;
        }

        private static bool ChildCenterInsideViewport(RectTransform item, RectTransform viewport, string childName)
        {
            Transform child = item != null ? item.Find(childName) : null;
            RectTransform childRect = child as RectTransform;
            if (childRect == null || viewport == null)
            {
                return false;
            }

            Vector3[] viewportCorners = new Vector3[4];
            viewport.GetWorldCorners(viewportCorners);
            Rect viewportRect = Rect.MinMaxRect(
                viewportCorners[0].x,
                viewportCorners[0].y,
                viewportCorners[2].x,
                viewportCorners[2].y);
            Vector3 worldCenter = childRect.TransformPoint(childRect.rect.center);
            return viewportRect.Contains(new Vector2(worldCenter.x, worldCenter.y));
        }

        private static float CalculateVisualTop(RectTransform root)
        {
            if (root == null)
            {
                return 50f;
            }

            RectTransform[] children = root.GetComponentsInChildren<RectTransform>(true);
            float visualTop = float.NegativeInfinity;
            var corners = new Vector3[4];
            for (int i = 0; i < children.Length; i++)
            {
                RectTransform child = children[i];
                if (child == null || child == root)
                {
                    continue;
                }

                child.GetLocalCorners(corners);
                for (int cornerIndex = 0; cornerIndex < corners.Length; cornerIndex++)
                {
                    Vector3 world = child.TransformPoint(corners[cornerIndex]);
                    Vector3 local = root.InverseTransformPoint(world);
                    if (local.y > visualTop)
                    {
                        visualTop = local.y;
                    }
                }
            }

            if (float.IsNegativeInfinity(visualTop))
            {
                return Mathf.Abs(root.rect.height) * (1f - root.pivot.y);
            }

            return Mathf.Max(0f, visualTop);
        }

        private static float CalculateWorldVisualTop(RectTransform root)
        {
            RectTransform[] children = root.GetComponentsInChildren<RectTransform>(true);
            float visualTop = float.NegativeInfinity;
            var corners = new Vector3[4];
            for (int i = 0; i < children.Length; i++)
            {
                RectTransform child = children[i];
                if (child == null || child == root)
                {
                    continue;
                }

                child.GetWorldCorners(corners);
                for (int cornerIndex = 0; cornerIndex < corners.Length; cornerIndex++)
                {
                    if (corners[cornerIndex].y > visualTop)
                    {
                        visualTop = corners[cornerIndex].y;
                    }
                }
            }

            if (float.IsNegativeInfinity(visualTop))
            {
                Vector3[] rootCorners = new Vector3[4];
                root.GetWorldCorners(rootCorners);
                return rootCorners[1].y;
            }

            return visualTop;
        }

        private static byte[] CreateSinglePixelPng()
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.magenta);
            texture.Apply();
            byte[] bytes = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);
            return bytes;
        }

        private sealed class FakeNetClient : INetClient
        {
            private readonly byte[] _responseData;

            public FakeNetClient(byte[] responseData)
            {
                _responseData = responseData;
            }

            public bool IsConnected => true;

            public string LastUrl { get; private set; }

            public void Initialize()
            {
            }

            public void Update(float deltaTime)
            {
            }

            public void Shutdown()
            {
            }

            public Task<TransportResponse> SendRequestAsync(
                string url,
                string method = "GET",
                byte[] body = null,
                System.Collections.Generic.Dictionary<string, string> headers = null)
            {
                LastUrl = url;
                return Task.FromResult(new TransportResponse
                {
                    StatusCode = 200,
                    Data = _responseData,
                });
            }
        }

        private sealed class DeferredNetClient : INetClient
        {
            private readonly TaskCompletionSource<TransportResponse> _completion = new TaskCompletionSource<TransportResponse>();

            public bool IsConnected => true;

            public string LastUrl { get; private set; }

            public void Initialize()
            {
            }

            public void Update(float deltaTime)
            {
            }

            public void Shutdown()
            {
            }

            public Task<TransportResponse> SendRequestAsync(
                string url,
                string method = "GET",
                byte[] body = null,
                System.Collections.Generic.Dictionary<string, string> headers = null)
            {
                LastUrl = url;
                return _completion.Task;
            }

            public void Complete(byte[] responseData)
            {
                _completion.TrySetResult(new TransportResponse
                {
                    StatusCode = 200,
                    Data = responseData,
                });
            }
        }
    }
}
