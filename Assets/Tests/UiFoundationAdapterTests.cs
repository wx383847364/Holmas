using App.HotUpdate.Holmas.UI.Binding;
using NUnit.Framework;
using UnityEngine;
using FoundationCollector = WX.Foundation.UI.Binding.UiReferenceCollector;

namespace Holmas.Tests
{
    public sealed class UiFoundationAdapterTests
    {
        [Test]
        public void LegacyCollectorCreatesTemporaryFoundationCollector()
        {
            GameObject root = new GameObject("UiFoundationAdapterTests");
            try
            {
                UiReferenceCollector legacyCollector = root.AddComponent<UiReferenceCollector>();

                FoundationCollector foundationCollector = legacyCollector.GetOrCreateFoundationCollector();

                Assert.That(foundationCollector, Is.Not.Null);
                Assert.That(foundationCollector.hideFlags, Is.EqualTo(HideFlags.DontSave));
                Assert.That(legacyCollector.GetOrCreateFoundationCollector(), Is.SameAs(foundationCollector));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
