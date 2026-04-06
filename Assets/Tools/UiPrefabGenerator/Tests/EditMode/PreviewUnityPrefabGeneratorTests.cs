using NUnit.Framework;
using UiPrefabGenerator.Core.Schema;
using UiPrefabGenerator.Editor.Generation;
using UiPrefabGenerator.HolmasAdapter;

namespace UiPrefabGenerator.Tests.EditMode
{
    public sealed class PreviewUnityPrefabGeneratorTests
    {
        [Test]
        public void GenerateDraft_BuildsExpectedDraftPathAndManifest()
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
                    new UiComponentSpec { ComponentType = "Image", AssetSlot = "panel_bg" },
                },
            });
            spec.Nodes.Add(new UiNodeSpec
            {
                NodeId = "claim_button",
                NodeName = "ClaimButton",
                ParentNodeId = "root",
                Components =
                {
                    new UiComponentSpec { ComponentType = "RectTransform" },
                    new UiComponentSpec { ComponentType = "Button", BindingKey = "claim_button" },
                },
            });
            spec.Interactions.Add(new UiInteractionSpec
            {
                NodeId = "claim_button",
                EventName = "on_click",
                HandlerKey = "claim_task",
            });

            var result = new PreviewUnityPrefabGenerator().GenerateDraft(new UiPrefabGenerationRequest
            {
                Spec = spec,
                Profile = new HolmasUiProjectProfile(),
            });

            Assert.That(result.Success, Is.True);
            Assert.That(result.PrefabDraftPath, Is.EqualTo("Assets/Res/Perfabs/Generated/Holmas/AgencyMainPanel.prefab"));
            Assert.That(result.Manifest.Entries.Count, Is.EqualTo(4));
            Assert.That(result.Manifest.Entries[1].Notes, Does.Contain("asset_slot=panel_bg"));
            Assert.That(result.Manifest.Entries[2].EventName, Is.Empty);
            Assert.That(result.Manifest.Entries[2].RequiresManualWiring, Is.False);
            Assert.That(result.Manifest.Entries[3].EventName, Is.EqualTo("on_click"));
            Assert.That(result.Manifest.Entries[3].RequiresManualWiring, Is.True);
            Assert.That(result.Manifest.Entries[3].Notes, Does.Contain("handler_key=claim_task"));
        }
    }
}
