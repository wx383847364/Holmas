using NUnit.Framework;
using UiPrefabGenerator.Core.Schema;
using UiPrefabGenerator.Editor.Validation;
using UiPrefabGenerator.HolmasAdapter;

namespace UiPrefabGenerator.Tests.EditMode
{
    public sealed class PrefabBindingManifestValidatorTests
    {
        [Test]
        public void Validate_Fails_WhenEventIsNotMarkedForManualWiring()
        {
            var manifest = new PrefabBindingManifest
            {
                PrefabName = "AgencyMainPanel",
                PrefabDraftPath = "Assets/Res/Perfabs/Generated/Holmas/AgencyMainPanel.prefab",
            };
            manifest.Entries.Add(new PrefabBindingEntry
            {
                NodePath = "AgencyMainPanel/ClaimButton",
                ComponentType = "Button",
                EventName = "on_click",
                RequiresManualWiring = false,
            });

            UiPrefabValidationResult result = new DefaultPrefabBindingManifestValidator().Validate(
                manifest,
                new HolmasUiProjectProfile());

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Issues, Has.Some.Matches<UiPrefabValidationIssue>(issue => issue.FieldPath.Contains("requires_manual_wiring")));
        }

        [Test]
        public void Validate_Fails_WhenDraftPathEscapesProfileRoot()
        {
            var manifest = new PrefabBindingManifest
            {
                PrefabName = "AgencyMainPanel",
                PrefabDraftPath = "Assets/Res/Perfabs/Generated/Other/AgencyMainPanel.prefab",
            };
            manifest.Entries.Add(new PrefabBindingEntry
            {
                NodePath = "AgencyMainPanel",
                ComponentType = "RectTransform",
            });

            UiPrefabValidationResult result = new DefaultPrefabBindingManifestValidator().Validate(
                manifest,
                new HolmasUiProjectProfile());

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Issues, Has.Some.Matches<UiPrefabValidationIssue>(issue => issue.Category == UiPrefabValidationIssueCategory.Adapter));
        }

        [Test]
        public void Validate_Fails_WhenComponentIsOutsideProfileWhitelist()
        {
            var manifest = new PrefabBindingManifest
            {
                PrefabName = "AgencyMainPanel",
                PrefabDraftPath = "Assets/Res/Perfabs/Generated/Holmas/AgencyMainPanel.prefab",
            };
            manifest.Entries.Add(new PrefabBindingEntry
            {
                NodePath = "AgencyMainPanel/Content",
                ComponentType = "MeshRenderer",
            });

            UiPrefabValidationResult result = new DefaultPrefabBindingManifestValidator().Validate(
                manifest,
                new HolmasUiProjectProfile());

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Issues, Has.Some.Matches<UiPrefabValidationIssue>(issue => issue.FieldPath.Contains("component_type")));
        }

        [Test]
        public void Validate_Fails_WhenImageIsMissingAssetSlot()
        {
            var manifest = new PrefabBindingManifest
            {
                PrefabName = "AgencyMainPanel",
                PrefabDraftPath = "Assets/Res/Perfabs/Generated/Holmas/AgencyMainPanel.prefab",
            };
            manifest.Entries.Add(new PrefabBindingEntry
            {
                NodePath = "AgencyMainPanel",
                ComponentType = "Image",
                AssetSlot = string.Empty,
            });

            UiPrefabValidationResult result = new DefaultPrefabBindingManifestValidator().Validate(
                manifest,
                new HolmasUiProjectProfile());

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Issues, Has.Some.Matches<UiPrefabValidationIssue>(issue => issue.FieldPath.Contains("asset_slot")));
        }

        [Test]
        public void Validate_Fails_WhenEntriesProduceNamingConflict()
        {
            var manifest = new PrefabBindingManifest
            {
                PrefabName = "AgencyMainPanel",
                PrefabDraftPath = "Assets/Res/Perfabs/Generated/Holmas/AgencyMainPanel.prefab",
            };
            manifest.Entries.Add(new PrefabBindingEntry
            {
                NodePath = "AgencyMainPanel/ClaimButton",
                ComponentType = "Button",
                BindingKey = "claim_button",
            });
            manifest.Entries.Add(new PrefabBindingEntry
            {
                NodePath = "AgencyMainPanel/ClaimButton",
                ComponentType = "Button",
                BindingKey = "claim_button_secondary",
            });

            UiPrefabValidationResult result = new DefaultPrefabBindingManifestValidator().Validate(
                manifest,
                new HolmasUiProjectProfile());

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Issues, Has.Some.Matches<UiPrefabValidationIssue>(issue => issue.Message.Contains("命名冲突")));
        }

        [Test]
        public void Validate_Fails_WhenEntriesAreDuplicated()
        {
            var manifest = new PrefabBindingManifest
            {
                PrefabName = "AgencyMainPanel",
                PrefabDraftPath = "Assets/Res/Perfabs/Generated/Holmas/AgencyMainPanel.prefab",
            };
            manifest.Entries.Add(new PrefabBindingEntry
            {
                NodePath = "AgencyMainPanel",
                ComponentType = "Image",
                AssetSlot = "panel_bg",
                Notes = "sample",
            });
            manifest.Entries.Add(new PrefabBindingEntry
            {
                NodePath = "AgencyMainPanel",
                ComponentType = "Image",
                AssetSlot = "panel_bg",
                Notes = "sample",
            });

            UiPrefabValidationResult result = new DefaultPrefabBindingManifestValidator().Validate(
                manifest,
                new HolmasUiProjectProfile());

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Issues, Has.Some.Matches<UiPrefabValidationIssue>(issue => issue.Message.Contains("重复 manifest entry")));
        }
    }
}
