using System;
using System.Collections.Generic;

namespace UiPrefabGenerator.Core.Request
{
    [Serializable]
    public sealed class UiGenerationTaskRequest
    {
        public string TaskId = string.Empty;
        public string CreatedAtUtc = string.Empty;
        public string TemplateName = string.Empty;
        public string ProfileId = string.Empty;
        public string TargetPlatform = string.Empty;
        public string SourceImageAssetPath = string.Empty;
        public string SourceImageTaskAssetPath = string.Empty;
        public string PageId = string.Empty;
        public string PageTitle = string.Empty;
        public string PrefabName = string.Empty;
        public string PageType = string.Empty;
        public string Orientation = string.Empty;
        public int ReferenceResolutionWidth;
        public int ReferenceResolutionHeight;
        public string AssetRoot = string.Empty;
        public string DraftPrefabRoot = string.Empty;
        public string Notes = string.Empty;
        public List<string> MustHaveNodes = new List<string>();
        public List<string> MustHaveInteractions = new List<string>();
    }
}
