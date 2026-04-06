using NUnit.Framework;
using UiPrefabGenerator.Core.Manifest;
using UiPrefabGenerator.Core.Schema;

namespace UiPrefabGenerator.Tests.EditMode
{
    public sealed class PrefabBindingManifestBuilderTests
    {
        [Test]
        public void Build_Fails_WhenParentGraphContainsCycle()
        {
            var spec = new UiPrefabSpec
            {
                PageId = "agency_main",
                PrefabName = "AgencyMainPanel",
                RootNodeId = "root",
            };
            spec.Nodes.Add(new UiNodeSpec
            {
                NodeId = "root",
                NodeName = "AgencyMainPanel",
                ParentNodeId = "content",
                Components =
                {
                    new UiComponentSpec { ComponentType = "RectTransform" },
                },
            });
            spec.Nodes.Add(new UiNodeSpec
            {
                NodeId = "content",
                NodeName = "Content",
                ParentNodeId = "root",
                Components =
                {
                    new UiComponentSpec { ComponentType = "Image", AssetSlot = "panel_bg" },
                },
            });

            PrefabBindingManifestBuildResult result = new DefaultPrefabBindingManifestBuilder().Build(
                new PrefabBindingManifestBuildRequest
                {
                    Spec = spec,
                    PrefabDraftPath = "Assets/Res/Perfabs/Generated/Holmas/AgencyMainPanel.prefab",
                });

            Assert.That(result.Success, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("父子环"));
        }
    }
}
