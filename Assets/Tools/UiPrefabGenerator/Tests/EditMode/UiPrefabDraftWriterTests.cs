using NUnit.Framework;
using UiPrefabGenerator.Editor.Generation;
using UiPrefabGenerator.HolmasAdapter;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace UiPrefabGenerator.Tests.EditMode
{
    public sealed class UiPrefabDraftWriterTests
    {
        [Test]
        public void WriteDraft_CreatesOpenablePrefabDraftWithExpectedHierarchy()
        {
            try
            {
                var spec = SampleFixtureLoader.LoadUiPrefabSpec();
                var profile = new HolmasUiProjectProfile();

                UiPrefabDraftWriteResult result = new DefaultUnityPrefabDraftWriter().WriteDraft(new UiPrefabDraftWriteRequest
                {
                    Spec = spec,
                    Profile = profile,
                    PrefabDraftPath = profile.BuildDraftPrefabPath(spec.PrefabName),
                });

                Assert.That(result.Success, Is.True);
                Assert.That(result.PrefabDraftPath, Is.EqualTo("Assets/Res/Perfabs/Generated/Holmas/AgencyMainPanel.prefab"));

                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(result.PrefabDraftPath);
                try
                {
                    Assert.That(prefabRoot.name, Is.EqualTo("AgencyMainPanel"));
                    Assert.That(prefabRoot.transform.Find("TaskList"), Is.Not.Null);
                    Assert.That(prefabRoot.transform.Find("ClaimButton"), Is.Not.Null);

                    GameObject claimButton = prefabRoot.transform.Find("ClaimButton").gameObject;
                    Assert.That(claimButton.GetComponent<RectTransform>(), Is.Not.Null);
                    Assert.That(claimButton.GetComponent<Image>(), Is.Not.Null);
                    Assert.That(claimButton.GetComponent<Button>(), Is.Not.Null);
                    Assert.That(claimButton.GetComponent<Button>().targetGraphic, Is.EqualTo(claimButton.GetComponent<Image>()));
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }
            finally
            {
                UiPrefabGeneratorTestSupport.CleanupGeneratedDraftRoot();
            }
        }
    }
}
