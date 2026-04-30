using NUnit.Framework;
using UiPrefabGenerator.Core.Schema;
using UiPrefabGenerator.HolmasAdapter;

namespace UiPrefabGenerator.Tests.EditMode
{
    public sealed class HolmasUiProjectProfileTests
    {
        [Test]
        public void HolmasProfile_UsesIsolatedDraftPath()
        {
            var profile = new HolmasUiProjectProfile();
            var portraitProfile = new HolmasPortraitUiProjectProfile();

            Assert.That(profile.ProfileId, Is.EqualTo("holmas_ugui"));
            Assert.That(profile.DraftPrefabRoot, Is.EqualTo("Assets/HotUpdateContent/Res/Perfabs/Generated/Holmas"));
            Assert.That(profile.AllowedComponentTypes, Has.Member("Button"));
            Assert.That(profile.AllowedComponentTypes, Has.Member("ScrollRect"));
            Assert.That(profile.AllowedComponentTypes, Has.Member("TextMeshProUGUI"));
            Assert.That(profile.AllowedComponentTypes, Has.Member("TMP_InputField"));
            Assert.That(portraitProfile.ProfileId, Is.EqualTo("holmas_ugui_portrait"));
            Assert.That(portraitProfile.DraftPrefabRoot, Is.EqualTo("Assets/HotUpdateContent/Res/Perfabs/Generated/Holmas/Portrait"));
        }

        [Test]
        public void GeneratedResultConsumer_ValidatesAndNormalizesManifest()
        {
            var consumer = new HolmasGeneratedResultConsumer();
            var manifest = new PrefabBindingManifest
            {
                PrefabName = "AgencyMainPanel",
                PrefabDraftPath = "Assets/HotUpdateContent/Res/Perfabs/Generated/Holmas/Portrait/AgencyMainPanel.prefab",
            };
            manifest.Entries.Add(new PrefabBindingEntry
            {
                NodePath = "AgencyMainPanel/ClaimButton",
                ComponentType = "Button",
                BindingKey = "claim_button",
                EventName = "on_click",
                RequiresManualWiring = true,
            });

            var result = consumer.Consume(manifest);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Plan.ProfileId, Is.EqualTo("holmas_ugui_portrait"));
            Assert.That(result.Plan.ManualWiringNodePaths, Is.EquivalentTo(new[] { "AgencyMainPanel/ClaimButton" }));
        }
    }
}
