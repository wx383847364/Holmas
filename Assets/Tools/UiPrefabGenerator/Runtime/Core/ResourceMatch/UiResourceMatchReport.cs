using System;
using System.Collections.Generic;

namespace UiPrefabGenerator.Core.ResourceMatch
{
    [Serializable]
    public sealed class UiResourceMatchReport
    {
        public string TaskId = string.Empty;
        public string AssetRoot = string.Empty;
        public List<UiAssetSlotMatch> Matches = new List<UiAssetSlotMatch>();
        public List<string> UnresolvedSlots = new List<string>();
        public List<string> Warnings = new List<string>();
    }

    [Serializable]
    public sealed class UiAssetSlotMatch
    {
        public string AssetSlot = string.Empty;
        public string ComponentType = string.Empty;
        public string SelectedAssetPath = string.Empty;
        public string SelectedAssetType = string.Empty;
        public float Confidence;
        public string Notes = string.Empty;
        public List<UiAssetCandidate> Candidates = new List<UiAssetCandidate>();
    }

    [Serializable]
    public sealed class UiAssetCandidate
    {
        public string AssetPath = string.Empty;
        public string AssetType = string.Empty;
        public float Score;
        public string Reason = string.Empty;
        public bool Recommended;
    }
}
