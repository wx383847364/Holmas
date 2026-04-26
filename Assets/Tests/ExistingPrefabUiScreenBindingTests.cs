using App.HotUpdate.Holmas.UI.Binding;
using App.HotUpdate.Holmas.UI.Generated;
using App.HotUpdate.Holmas.UI.Screens.Battle;
using App.HotUpdate.Holmas.UI.Screens.Loading;
using App.HotUpdate.Holmas.UI.Screens.Main;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

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
                Assert.That(bindings.HelpButton.name, Is.EqualTo("HelpButton"), "MainPanel 应创建可重看教程的帮助按钮。");
                Assert.That(bindings.GmButton.name, Is.EqualTo("GmButton"), "MainPanel 应创建独立 GM 调试入口。");
                Assert.That(bindings.StartTutorialButton.name, Is.EqualTo("StartTutorialButton"), "MainPanel 应创建正式可用的新手引导入口。");
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
        public void BattlePrefab_CanBuildRuntimeBindings()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BattleGeneratedBindings.PrefabAssetPath);
            Assert.That(prefab, Is.Not.Null, "BattlePanel prefab 缺失。");

            GameObject instance = Object.Instantiate(prefab);
            try
            {
                BattleView view = instance.GetComponent<BattleView>() ?? instance.AddComponent<BattleView>();
                view.EnsureBindingSurface();

                UiReferenceCollector collector = instance.GetComponent<UiReferenceCollector>();
                Assert.That(collector, Is.Not.Null, "BattlePanel 运行时 collector 创建失败。");

                var resolver = new UiBindingResolver(collector, BattleGeneratedBindings.Manifest);
                BattleBindings bindings = BattleBindings.Resolve(resolver);
                Assert.That(bindings.HasRequiredBindings, Is.True, "BattleBindings 未能解析最小运行时 binding。");
                Assert.That(instance.transform.Find("RuntimeOverlay/AddEnergyButton"), Is.Null, "BattlePanel 不应再创建加体力按钮。");
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
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
    }
}
