using NUnit.Framework;
using UiPrefabGenerator.Editor.Generation;
using UiPrefabGenerator.Editor.Validation;
using UiPrefabGenerator.HolmasAdapter;
using UnityEditor;
using UnityEngine;

namespace UiPrefabGenerator.Tests.EditMode
{
    public sealed class PrefabDraftStructureValidatorTests
    {
        [Test]
        public void Validate_PassesForSampleDraftAndFailsWhenClaimButtonImageIsRemoved()
        {
            try
            {
                var spec = SampleFixtureLoader.LoadUiPrefabSpec();
                var profile = new HolmasUiProjectProfile();
                string prefabPath = profile.BuildDraftPrefabPath(spec.PrefabName);

                UiPrefabDraftWriteResult writeResult = new DefaultUnityPrefabDraftWriter().WriteDraft(new UiPrefabDraftWriteRequest
                {
                    Spec = spec,
                    Profile = profile,
                    PrefabDraftPath = prefabPath,
                });

                Assert.That(writeResult.Success, Is.True);

                var validator = new DefaultPrefabDraftStructureValidator();
                UiPrefabValidationResult validResult = validator.Validate(writeResult.PrefabDraftPath, spec);
                Assert.That(validResult.IsValid, Is.True);

                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(writeResult.PrefabDraftPath);
                try
                {
                    GameObject claimButton = prefabRoot.transform.Find("ClaimButton").gameObject;
                    Object.DestroyImmediate(claimButton.GetComponent<UnityEngine.UI.Image>());
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, writeResult.PrefabDraftPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }

                UiPrefabValidationResult invalidResult = validator.Validate(writeResult.PrefabDraftPath, spec);
                Assert.That(invalidResult.IsValid, Is.False);
                Assert.That(invalidResult.Issues, Has.Some.Matches<UiPrefabValidationIssue>(issue => issue.Message.Contains("缺少组件") && issue.FieldPath.Contains("ClaimButton")));
            }
            finally
            {
                UiPrefabGeneratorTestSupport.CleanupGeneratedDraftRoot();
            }
        }
    }
}
