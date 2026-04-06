using System;
using UiPrefabGenerator.Core.Profile;
using UiPrefabGenerator.Core.Result;
using UiPrefabGenerator.Core.Schema;
using UiPrefabGenerator.Editor.Bridge;
using UiPrefabGenerator.Editor.ResourceMatch;
using UiPrefabGenerator.Editor.Validation;

namespace UiPrefabGenerator.Editor.Generation
{
    public sealed class UiGenerationExecutionService
    {
        private const string HolmasPortraitProfileTypeName = "UiPrefabGenerator.HolmasAdapter.HolmasPortraitUiProjectProfile, UiPrefabGenerator.HolmasAdapter";
        private const string HolmasLandscapeProfileTypeName = "UiPrefabGenerator.HolmasAdapter.HolmasUiProjectProfile, UiPrefabGenerator.HolmasAdapter";

        private readonly IUnityPrefabGenerator _previewGenerator;
        private readonly IUnityPrefabDraftWriter _draftWriter;
        private readonly IPrefabBindingManifestValidator _manifestValidator;
        private readonly IPrefabDraftStructureValidator _draftStructureValidator;
        private readonly UiPrefabResourceBinder _resourceBinder;

        public UiGenerationExecutionService(
            IUnityPrefabGenerator previewGenerator = null,
            IUnityPrefabDraftWriter draftWriter = null,
            IPrefabBindingManifestValidator manifestValidator = null,
            IPrefabDraftStructureValidator draftStructureValidator = null,
            UiPrefabResourceBinder resourceBinder = null)
        {
            _previewGenerator = previewGenerator ?? new PreviewUnityPrefabGenerator();
            _draftWriter = draftWriter ?? new DefaultUnityPrefabDraftWriter();
            _manifestValidator = manifestValidator ?? new DefaultPrefabBindingManifestValidator();
            _draftStructureValidator = draftStructureValidator ?? new DefaultPrefabDraftStructureValidator();
            _resourceBinder = resourceBinder ?? new UiPrefabResourceBinder();
        }

        public UiGenerationExecutionResult Execute(
            string taskDirectory,
            UiGenerationTemplate template,
            UiGenerationAnalysisResult analysisResult)
        {
            var result = new UiGenerationExecutionResult
            {
                TaskId = analysisResult != null ? analysisResult.TaskId ?? string.Empty : string.Empty,
                TemplateName = template != null ? template.TemplateName ?? string.Empty : string.Empty,
                ProfileId = template != null ? template.ProfileId ?? string.Empty : string.Empty,
                UsedSpecPath = taskDirectory + "/" + UiGenerationDataPaths.UiPrefabSpecFileName,
            };

            if (template == null)
            {
                result.Errors.Add("生成模板不能为空。");
                return Finish(taskDirectory, null, result);
            }

            if (analysisResult == null || analysisResult.UiPrefabSpec == null)
            {
                result.Errors.Add("分析结果或 UiPrefabSpec 不能为空。");
                return Finish(taskDirectory, null, result);
            }

            IProjectUiProfile profile = CreateProfile(template.ProfileId);
            if (profile == null)
            {
                result.Errors.Add("无法创建 Profile: " + (template.ProfileId ?? string.Empty));
                return Finish(taskDirectory, null, result);
            }

            if (!string.IsNullOrWhiteSpace(template.DraftPrefabRoot) &&
                !string.Equals(template.DraftPrefabRoot, profile.DraftPrefabRoot, StringComparison.Ordinal))
            {
                result.Warnings.Add("模板 DraftPrefabRoot 与 Profile 默认输出目录不一致，当前以 Profile 为准。");
            }

            UiPrefabGenerationResult previewResult = _previewGenerator.GenerateDraft(new UiPrefabGenerationRequest
            {
                Spec = analysisResult.UiPrefabSpec,
                Profile = profile,
            });
            result.Errors.AddRange(previewResult.Errors);
            result.Warnings.AddRange(previewResult.Warnings);
            if (!previewResult.Success)
            {
                return Finish(taskDirectory, null, result);
            }

            UiPrefabDraftWriteResult draftWrite = _draftWriter.WriteDraft(new UiPrefabDraftWriteRequest
            {
                Spec = analysisResult.UiPrefabSpec,
                Profile = profile,
                PrefabDraftPath = previewResult.PrefabDraftPath,
            });
            result.Errors.AddRange(draftWrite.Errors);
            result.Warnings.AddRange(draftWrite.Warnings);
            if (!draftWrite.Success)
            {
                return Finish(taskDirectory, previewResult.Manifest, result);
            }

            result.PrefabPath = draftWrite.PrefabDraftPath ?? previewResult.PrefabDraftPath ?? string.Empty;
            PrefabBindingManifest manifest = previewResult.Manifest ?? new PrefabBindingManifest();
            manifest.PrefabDraftPath = result.PrefabPath;

            _resourceBinder.Apply(result.PrefabPath, manifest, analysisResult.ResourceMatchReport, result);

            UiPrefabValidationResult manifestValidation = _manifestValidator.Validate(manifest, profile);
            result.ManifestValidationPassed = manifestValidation != null && manifestValidation.IsValid;
            AppendValidationIssues(result, manifestValidation);

            UiPrefabValidationResult structureValidation = _draftStructureValidator.Validate(result.PrefabPath, analysisResult.UiPrefabSpec);
            result.StructureValidationPassed = structureValidation != null && structureValidation.IsValid;
            AppendValidationIssues(result, structureValidation);

            for (int i = 0; i < manifest.Entries.Count; i++)
            {
                PrefabBindingEntry entry = manifest.Entries[i];
                if (entry != null && entry.RequiresManualWiring && !result.ManualWiringNodes.Contains(entry.NodePath))
                {
                    result.ManualWiringNodes.Add(entry.NodePath);
                }
            }

            result.ManifestPath = taskDirectory + "/" + UiGenerationDataPaths.ManifestFileName;
            result.Success = result.Errors.Count == 0 && result.ManifestValidationPassed && result.StructureValidationPassed;
            return Finish(taskDirectory, manifest, result);
        }

        private static IProjectUiProfile CreateProfile(string profileId)
        {
            string typeName;
            if (string.Equals(profileId, "holmas_ugui_portrait", StringComparison.Ordinal))
            {
                typeName = HolmasPortraitProfileTypeName;
            }
            else if (string.Equals(profileId, "holmas_ugui", StringComparison.Ordinal))
            {
                typeName = HolmasLandscapeProfileTypeName;
            }
            else
            {
                return null;
            }

            Type type = Type.GetType(typeName, false);
            return type == null ? null : Activator.CreateInstance(type) as IProjectUiProfile;
        }

        private static void AppendValidationIssues(UiGenerationExecutionResult result, UiPrefabValidationResult validation)
        {
            if (result == null || validation == null || validation.Issues == null)
            {
                return;
            }

            for (int i = 0; i < validation.Issues.Count; i++)
            {
                UiPrefabValidationIssue issue = validation.Issues[i];
                if (issue == null || string.IsNullOrWhiteSpace(issue.Message))
                {
                    continue;
                }

                if (issue.Severity == UiPrefabValidationIssueSeverity.Error)
                {
                    result.Errors.Add(issue.Message);
                }
                else
                {
                    result.Warnings.Add(issue.Message);
                }
            }
        }

        private static UiGenerationExecutionResult Finish(string taskDirectory, PrefabBindingManifest manifest, UiGenerationExecutionResult result)
        {
            if (!string.IsNullOrWhiteSpace(taskDirectory))
            {
                UiGenerationTaskStorage.SaveExecutionArtifacts(taskDirectory, manifest, result);
            }

            return result;
        }
    }
}
