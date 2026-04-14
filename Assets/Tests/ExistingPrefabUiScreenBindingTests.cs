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
