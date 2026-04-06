using NUnit.Framework;
using UiPrefabGenerator.Core.ResourceMatch;
using UiPrefabGenerator.Editor.Bridge;

namespace UiPrefabGenerator.Tests.EditMode
{
    public sealed class UiResourceMatchReportTests
    {
        [Test]
        public void SaveAndLoad_RoundTripsCandidatesAndUnresolvedSlots()
        {
            const string assetPath = "Assets/UiPrefabGeneratorData/Cache/ui_resource_match_report_roundtrip.json";

            try
            {
                var report = new UiResourceMatchReport
                {
                    TaskId = "roundtrip_task",
                    AssetRoot = "Assets/Res",
                };
                report.Matches.Add(new UiAssetSlotMatch
                {
                    AssetSlot = "panel_bg",
                    ComponentType = "Image",
                    SelectedAssetPath = string.Empty,
                    SelectedAssetType = string.Empty,
                    Confidence = 0.15f,
                    Notes = "keep unresolved when confidence is low",
                    Candidates =
                    {
                        new UiAssetCandidate
                        {
                            AssetPath = "Assets/Res/UI/panel_bg_low.png",
                            AssetType = "Sprite",
                            Score = 0.15f,
                            Reason = "weak visual similarity",
                            Recommended = false,
                        }
                    },
                });
                report.UnresolvedSlots.Add("panel_bg");
                report.UnresolvedSlots.Add("claim_button_bg");

                UiGenerationJsonFileUtility.SaveJson(assetPath, report);

                UiResourceMatchReport loaded;
                Assert.That(UiGenerationJsonFileUtility.TryLoadJson(assetPath, out loaded), Is.True);
                Assert.That(loaded, Is.Not.Null);
                Assert.That(loaded.TaskId, Is.EqualTo("roundtrip_task"));
                Assert.That(loaded.AssetRoot, Is.EqualTo("Assets/Res"));
                Assert.That(loaded.Matches.Count, Is.EqualTo(1));
                Assert.That(loaded.Matches[0].Candidates.Count, Is.EqualTo(1));
                Assert.That(loaded.Matches[0].Candidates[0].Score, Is.EqualTo(0.15f).Within(0.0001f));
                Assert.That(loaded.UnresolvedSlots, Has.Member("panel_bg"));
                Assert.That(loaded.UnresolvedSlots, Has.Member("claim_button_bg"));
            }
            finally
            {
                UiPrefabGeneratorTestSupport.DeleteAssetIfExistsForTests(assetPath);
            }
        }
    }
}
