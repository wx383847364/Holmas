using System;
using System.Collections.Generic;

namespace UiPrefabGenerator.Core.Result
{
    [Serializable]
    public sealed class UiGenerationExecutionResult
    {
        public string TaskId = string.Empty;
        public string TemplateName = string.Empty;
        public string ProfileId = string.Empty;
        public bool Success;
        public string PrefabPath = string.Empty;
        public string ManifestPath = string.Empty;
        public string UsedSpecPath = string.Empty;
        public bool ManifestValidationPassed;
        public bool StructureValidationPassed;
        public List<string> AutoBoundAssets = new List<string>();
        public List<string> UnmatchedAssetSlots = new List<string>();
        public List<string> ManualWiringNodes = new List<string>();
        public List<string> Warnings = new List<string>();
        public List<string> Errors = new List<string>();
    }
}
