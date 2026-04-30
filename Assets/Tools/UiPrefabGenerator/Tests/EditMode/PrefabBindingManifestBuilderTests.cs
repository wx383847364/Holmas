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
                    PrefabDraftPath = "Assets/HotUpdateContent/Res/Perfabs/Generated/Holmas/AgencyMainPanel.prefab",
                });

            Assert.That(result.Success, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("父子环"));
        }

        [Test]
        public void Build_Warns_WhenInteractionHasNoCarrierComponent()
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
                Components =
                {
                    new UiComponentSpec { ComponentType = "RectTransform" },
                },
            });
            spec.Nodes.Add(new UiNodeSpec
            {
                NodeId = "header",
                NodeName = "Header",
                ParentNodeId = "root",
                Components =
                {
                    new UiComponentSpec { ComponentType = "RectTransform" },
                },
            });
            spec.Interactions.Add(new UiInteractionSpec
            {
                NodeId = "header",
                EventName = "on_click",
                HandlerKey = "open_header",
            });

            PrefabBindingManifestBuildResult result = new DefaultPrefabBindingManifestBuilder().Build(
                new PrefabBindingManifestBuildRequest
                {
                    Spec = spec,
                    PrefabDraftPath = "Assets/HotUpdateContent/Res/Perfabs/Generated/Holmas/AgencyMainPanel.prefab",
                });

            Assert.That(result.Success, Is.True);
            Assert.That(result.Errors, Is.Empty);
            Assert.That(result.Warnings, Has.Some.Contains("未找到可承载交互的组件"));
            Assert.That(result.Manifest.Entries.Count, Is.EqualTo(2));
            Assert.That(result.Manifest.Entries[1].NodePath, Is.EqualTo("AgencyMainPanel/Header"));
            Assert.That(result.Manifest.Entries[1].ComponentType, Is.EqualTo("RectTransform"));
            Assert.That(result.Manifest.Entries[1].EventName, Is.Empty);
            Assert.That(result.Manifest.Entries[1].RequiresManualWiring, Is.False);
            Assert.That(result.Manifest.Entries[1].Notes, Does.Not.Contain("handler_key=open_header"));
        }

        [Test]
        public void Build_MultiComponentClaimButton_OnlyMarksButtonForManualWiring()
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
                Components =
                {
                    new UiComponentSpec { ComponentType = "RectTransform" },
                },
            });
            spec.Nodes.Add(new UiNodeSpec
            {
                NodeId = "claim_button",
                NodeName = "ClaimButton",
                ParentNodeId = "root",
                Layout = new UiLayoutSpec
                {
                    LayoutType = "Anchored",
                    LayoutSlot = "claim_button",
                },
                Components =
                {
                    new UiComponentSpec { ComponentType = "RectTransform" },
                    new UiComponentSpec { ComponentType = "Image", AssetSlot = "claim_button_bg" },
                    new UiComponentSpec { ComponentType = "Button", BindingKey = "claim_button" },
                },
            });
            spec.Interactions.Add(new UiInteractionSpec
            {
                NodeId = "claim_button",
                EventName = "on_click",
                HandlerKey = "claim_task",
            });

            PrefabBindingManifestBuildResult result = new DefaultPrefabBindingManifestBuilder().Build(
                new PrefabBindingManifestBuildRequest
                {
                    Spec = spec,
                    PrefabDraftPath = "Assets/HotUpdateContent/Res/Perfabs/Generated/Holmas/AgencyMainPanel.prefab",
                });

            Assert.That(result.Success, Is.True);
            Assert.That(result.Errors, Is.Empty);
            Assert.That(result.Warnings, Is.Empty);
            Assert.That(result.Manifest.Entries.Count, Is.EqualTo(4));
            Assert.That(result.Manifest.Entries[1].ComponentType, Is.EqualTo("RectTransform"));
            Assert.That(result.Manifest.Entries[1].RequiresManualWiring, Is.False);
            Assert.That(result.Manifest.Entries[2].ComponentType, Is.EqualTo("Image"));
            Assert.That(result.Manifest.Entries[2].AssetSlot, Is.EqualTo("claim_button_bg"));
            Assert.That(result.Manifest.Entries[2].EventName, Is.Empty);
            Assert.That(result.Manifest.Entries[2].RequiresManualWiring, Is.False);
            Assert.That(result.Manifest.Entries[3].ComponentType, Is.EqualTo("Button"));
            Assert.That(result.Manifest.Entries[3].EventName, Is.EqualTo("on_click"));
            Assert.That(result.Manifest.Entries[3].RequiresManualWiring, Is.True);
            Assert.That(result.Manifest.Entries[3].Notes, Does.Contain("handler_key=claim_task"));
        }
    }
}
