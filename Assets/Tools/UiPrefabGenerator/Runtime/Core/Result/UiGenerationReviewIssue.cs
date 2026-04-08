using System;

namespace UiPrefabGenerator.Core.Result
{
    public enum UiGenerationReviewSeverity
    {
        Info = 0,
        Warning = 1,
        Blocking = 2,
    }

    [Serializable]
    public sealed class UiGenerationReviewIssue
    {
        public string IssueId = string.Empty;
        public string Source = string.Empty;
        public string IssueKind = string.Empty;
        public UiGenerationReviewSeverity Severity;
        public string Summary = string.Empty;
        public string Details = string.Empty;
        public string RelatedId = string.Empty;
        public string SuggestedResolution = string.Empty;
        public float Confidence = 1f;
        public bool RequiresHumanDecision;
    }
}
