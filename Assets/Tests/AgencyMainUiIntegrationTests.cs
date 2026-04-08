using App.HotUpdate.Holmas.UI.Binding;
using App.HotUpdate.Holmas.UI.Core;
using App.HotUpdate.Holmas.UI.Generated;
using App.HotUpdate.Holmas.UI.Screens.AgencyMain;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Holmas.Tests
{
    public sealed class AgencyMainUiIntegrationTests
    {
        [Test]
        public void AgencyMainGeneratedPrefab_ResolvesFormalBindings()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AgencyMainGeneratedBindings.PrefabAssetPath);
            Assert.That(prefab, Is.Not.Null, "AgencyMain 正式 prefab 资源缺失。");

            GameObject instance = Object.Instantiate(prefab);
            try
            {
                UiReferenceCollector collector = instance.GetComponent<UiReferenceCollector>();
                Assert.That(collector, Is.Not.Null, "AgencyMain prefab 缺少 UiReferenceCollector。");

                var resolver = new UiBindingResolver(collector, AgencyMainGeneratedBindings.Manifest);
                AgencyMainBindings bindings = AgencyMainBindings.Resolve(resolver);

                Assert.That(bindings.HasRequiredBindings, Is.True, "AgencyMainBindings 未能完整解析正式 binding。");
                Assert.That(
                    instance.transform.Find(AgencyMainBindings.ContentNodeName)?.GetComponent<UiSafeAreaFitter>(),
                    Is.Not.Null,
                    "AgencyMain prefab 缺少 UiSafeAreaFitter。");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
    }
}
