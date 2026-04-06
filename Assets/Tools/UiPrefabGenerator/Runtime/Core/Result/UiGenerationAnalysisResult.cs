using System;
using System.Collections.Generic;
using UiPrefabGenerator.Core.ResourceMatch;
using UiPrefabGenerator.Core.Schema;

namespace UiPrefabGenerator.Core.Result
{
    [Serializable]
    public sealed class UiGenerationAnalysisResult
    {
        public string TaskId = string.Empty;
        public bool Success;
        public string TemplateName = string.Empty;
        public string ProfileId = string.Empty;
        public DesignPacket DesignPacket = new DesignPacket();
        public UiPrefabSpec UiPrefabSpec = new UiPrefabSpec();
        public UiResourceMatchReport ResourceMatchReport = new UiResourceMatchReport();
        public List<string> UnresolvedItems = new List<string>();
        public List<string> Warnings = new List<string>();
        public List<string> Errors = new List<string>();
    }
}
