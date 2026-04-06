using System;
using System.Collections.Generic;
using UiPrefabGenerator.Core.Schema;

namespace UiPrefabGenerator.HolmasAdapter
{
    [Serializable]
    public sealed class HolmasGeneratedBindingView
    {
        public string NodePath = string.Empty;
        public string ComponentType = string.Empty;
        public string BindingKey = string.Empty;
        public string AssetSlot = string.Empty;
        public string EventName = string.Empty;
        public bool RequiresManualWiring;
        public string Notes = string.Empty;
    }

    [Serializable]
    public sealed class HolmasGeneratedResultPlan
    {
        public string ProfileId = string.Empty;
        public string PrefabName = string.Empty;
        public string PrefabDraftPath = string.Empty;
        public string RuntimeBindingNamespace = string.Empty;
        public List<HolmasGeneratedBindingView> Entries = new List<HolmasGeneratedBindingView>();
        public List<string> ManualWiringNodePaths = new List<string>();
    }

    [Serializable]
    public sealed class HolmasGeneratedResultConsumptionResult
    {
        public bool Success;
        public HolmasGeneratedResultPlan Plan = new HolmasGeneratedResultPlan();
        public List<string> Errors = new List<string>();
        public List<string> Warnings = new List<string>();
    }

    public sealed class HolmasGeneratedResultConsumer
    {
        private readonly HolmasUiProjectProfile _profile;

        public HolmasGeneratedResultConsumer()
            : this(new HolmasUiProjectProfile())
        {
        }

        public HolmasGeneratedResultConsumer(HolmasUiProjectProfile profile)
        {
            _profile = profile ?? new HolmasUiProjectProfile();
        }

        public HolmasGeneratedResultConsumptionResult Consume(PrefabBindingManifest manifest)
        {
            var result = new HolmasGeneratedResultConsumptionResult();
            result.Plan.ProfileId = _profile.ProfileId;
            result.Plan.RuntimeBindingNamespace = _profile.RuntimeBindingNamespace;

            if (manifest == null)
            {
                result.Errors.Add("PrefabBindingManifest 不能为空。");
                return result;
            }

            if (string.IsNullOrWhiteSpace(manifest.PrefabName))
            {
                result.Errors.Add("PrefabBindingManifest.PrefabName 不能为空。");
            }

            if (string.IsNullOrWhiteSpace(manifest.PrefabDraftPath))
            {
                result.Errors.Add("PrefabBindingManifest.PrefabDraftPath 不能为空。");
            }

            if (manifest.Entries == null || manifest.Entries.Count == 0)
            {
                result.Errors.Add("PrefabBindingManifest.Entries 不能为空。");
                return result;
            }

            string expectedDraftPath = _profile.BuildDraftPrefabPath(manifest.PrefabName);
            result.Plan.PrefabName = manifest.PrefabName ?? string.Empty;
            result.Plan.PrefabDraftPath = manifest.PrefabDraftPath ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(manifest.PrefabDraftPath) &&
                !_profile.IsDraftPrefabPathWithinAllowedRoot(manifest.PrefabDraftPath))
            {
                result.Errors.Add(string.Format(
                    "PrefabDraftPath 不在 Holmas 允许目录内: {0}。",
                    manifest.PrefabDraftPath));
            }

            if (!string.IsNullOrWhiteSpace(expectedDraftPath) &&
                !string.Equals(expectedDraftPath, manifest.PrefabDraftPath, StringComparison.Ordinal))
            {
                result.Warnings.Add(string.Format(
                    "PrefabDraftPath 与 Holmas 约定路径不一致: expected {0}, actual {1}。",
                    expectedDraftPath,
                    manifest.PrefabDraftPath));
            }

            var manualWiringNodePaths = new List<string>();
            for (int i = 0; i < manifest.Entries.Count; i++)
            {
                PrefabBindingEntry entry = manifest.Entries[i];
                if (entry == null)
                {
                    result.Errors.Add(string.Format("PrefabBindingManifest.Entries[{0}] 不能为空。", i));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(entry.NodePath))
                {
                    result.Errors.Add(string.Format("PrefabBindingManifest.Entries[{0}].NodePath 不能为空。", i));
                }

                if (string.IsNullOrWhiteSpace(entry.ComponentType))
                {
                    result.Errors.Add(string.Format("PrefabBindingManifest.Entries[{0}].ComponentType 不能为空。", i));
                }

                result.Plan.Entries.Add(new HolmasGeneratedBindingView
                {
                    NodePath = entry.NodePath ?? string.Empty,
                    ComponentType = entry.ComponentType ?? string.Empty,
                    BindingKey = entry.BindingKey ?? string.Empty,
                    AssetSlot = entry.AssetSlot ?? string.Empty,
                    EventName = entry.EventName ?? string.Empty,
                    RequiresManualWiring = entry.RequiresManualWiring,
                    Notes = entry.Notes ?? string.Empty,
                });

                if (entry.RequiresManualWiring && !string.IsNullOrWhiteSpace(entry.NodePath))
                {
                    if (!manualWiringNodePaths.Contains(entry.NodePath))
                    {
                        manualWiringNodePaths.Add(entry.NodePath);
                    }
                }
            }

            result.Plan.ManualWiringNodePaths.AddRange(manualWiringNodePaths);
            result.Success = result.Errors.Count == 0;
            return result;
        }
    }
}
