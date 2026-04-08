using System;
using System.Collections.Generic;
using UiPrefabGenerator.Core.Manifest;
using UiPrefabGenerator.Core.Profile;
using UiPrefabGenerator.Core.Schema;
using UiPrefabGenerator.Core.Validation;

namespace UiPrefabGenerator.Editor.Generation
{
    public interface IUnityPrefabGenerator
    {
        UiPrefabGenerationResult GenerateDraft(UiPrefabGenerationRequest request);
    }

    [Serializable]
    public sealed class UiPrefabGenerationRequest
    {
        public UiPrefabSpec Spec;
        public IProjectUiProfile Profile;
    }

    [Serializable]
    public sealed class UiPrefabGenerationResult
    {
        public bool Success;
        public string PrefabDraftPath = string.Empty;
        public PrefabBindingManifest Manifest = new PrefabBindingManifest();
        public List<string> Errors = new List<string>();
        public List<string> Warnings = new List<string>();
    }

    public sealed class PreviewUnityPrefabGenerator : IUnityPrefabGenerator
    {
        private readonly IUiSpecValidator _validator;
        private readonly IPrefabBindingManifestBuilder _manifestBuilder;

        public PreviewUnityPrefabGenerator(
            IUiSpecValidator validator = null,
            IPrefabBindingManifestBuilder manifestBuilder = null)
        {
            _validator = validator ?? new DefaultUiSpecValidator();
            _manifestBuilder = manifestBuilder ?? new DefaultPrefabBindingManifestBuilder();
        }

        public UiPrefabGenerationResult GenerateDraft(UiPrefabGenerationRequest request)
        {
            var result = new UiPrefabGenerationResult();
            if (request == null)
            {
                result.Errors.Add("UiPrefabGenerationRequest 不能为空。");
                return result;
            }

            if (request.Spec == null)
            {
                result.Errors.Add("UiPrefabGenerationRequest.Spec 不能为空。");
                return result;
            }

            if (request.Profile == null)
            {
                result.Errors.Add("UiPrefabGenerationRequest.Profile 不能为空。");
                return result;
            }

            UiPrefabSpecValidationResult validation = _validator.Validate(request.Spec);
            result.Errors.AddRange(validation.Errors);
            result.Warnings.AddRange(validation.Warnings);
            if (!validation.IsValid)
            {
                return result;
            }

            var allowedComponentTypes = request.Profile.AllowedComponentTypes ?? new string[0];
            var allowedTypes = new HashSet<string>(allowedComponentTypes, StringComparer.Ordinal);
            for (int i = 0; i < request.Spec.Nodes.Count; i++)
            {
                UiNodeSpec node = request.Spec.Nodes[i];
                if (node == null || node.Components == null)
                {
                    continue;
                }

                for (int componentIndex = 0; componentIndex < node.Components.Count; componentIndex++)
                {
                    UiComponentSpec component = node.Components[componentIndex];
                    if (component == null || string.IsNullOrWhiteSpace(component.ComponentType))
                    {
                        continue;
                    }

                    if (!allowedTypes.Contains(component.ComponentType))
                    {
                        result.Errors.Add(string.Format("组件类型不在当前 Profile 白名单内: {0}。", component.ComponentType));
                    }
                }
            }

            if (result.Errors.Count > 0)
            {
                return result;
            }

            result.PrefabDraftPath = request.Profile.DraftPrefabRoot.TrimEnd('/') + "/" + request.Spec.PrefabName + ".prefab";
            PrefabBindingManifestBuildResult manifestResult = _manifestBuilder.Build(new PrefabBindingManifestBuildRequest
            {
                Spec = request.Spec,
                PrefabDraftPath = result.PrefabDraftPath,
            });

            result.Errors.AddRange(manifestResult.Errors);
            result.Warnings.AddRange(manifestResult.Warnings);
            if (!manifestResult.Success)
            {
                return result;
            }

            result.Manifest = manifestResult.Manifest ?? new PrefabBindingManifest();
            result.Success = true;
            return result;
        }
    }
}
