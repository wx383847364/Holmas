using System;
using System.Collections.Generic;

namespace UiPrefabGenerator.Core.Schema
{
    [Serializable]
    public sealed class UiRect
    {
        public float X;
        public float Y;
        public float Width;
        public float Height;
    }

    [Serializable]
    public sealed class VisualUnderstandingBundle
    {
        public string TaskId = string.Empty;
        public string SourceImagePath = string.Empty;
        public string ProviderId = string.Empty;
        public string ProviderRunId = string.Empty;
        public int CanvasWidth;
        public int CanvasHeight;
        public List<VisualElementEvidence> Elements = new List<VisualElementEvidence>();
        public List<VisualHierarchyEdge> Hierarchy = new List<VisualHierarchyEdge>();
        public VisualConfidenceSummary ConfidenceSummary = new VisualConfidenceSummary();
        public List<string> Notes = new List<string>();
    }

    [Serializable]
    public sealed class VisualElementEvidence
    {
        public string ElementId = string.Empty;
        public string ElementType = string.Empty;
        public string SemanticRole = string.Empty;
        public string DisplayName = string.Empty;
        public UiRect EvidenceBounds = new UiRect();
        public float Confidence;
        public int ZOrder;
        public string ParentElementId = string.Empty;
        public VisualTextEvidence Text = new VisualTextEvidence();
        public VisualStyleEvidence Style = new VisualStyleEvidence();
        public List<string> Tags = new List<string>();
    }

    [Serializable]
    public sealed class VisualTextEvidence
    {
        public string RawText = string.Empty;
        public string NormalizedText = string.Empty;
        public float Confidence;
        public bool IsNumeric;
        public string TextRole = string.Empty;
    }

    [Serializable]
    public sealed class VisualStyleEvidence
    {
        public string PrimaryColor = string.Empty;
        public string ShapeHint = string.Empty;
        public string AssetSlotHint = string.Empty;
        public string LayoutHint = string.Empty;
    }

    [Serializable]
    public sealed class VisualHierarchyEdge
    {
        public string ParentElementId = string.Empty;
        public string ChildElementId = string.Empty;
        public string RelationType = string.Empty;
        public float Confidence;
    }

    [Serializable]
    public sealed class VisualConfidenceSummary
    {
        public float OverallConfidence;
        public int LowConfidenceElementCount;
        public int MissingCriticalElementCount;
        public List<string> BlockingReasons = new List<string>();
    }

    [Serializable]
    public sealed class VisualReviewReport
    {
        public string TaskId = string.Empty;
        public string ReviewStatus = string.Empty;
        public int BlockingIssueCount;
        public int WarningIssueCount;
        public List<VisualReviewIssue> Issues = new List<VisualReviewIssue>();
        public List<string> LowConfidenceElementIds = new List<string>();
        public List<string> MissingSemanticRoles = new List<string>();
        public List<string> Notes = new List<string>();
    }

    [Serializable]
    public sealed class VisualReviewIssue
    {
        public string IssueId = string.Empty;
        public string Severity = string.Empty;
        public string Kind = string.Empty;
        public string RelatedElementId = string.Empty;
        public string Summary = string.Empty;
        public string SuggestedAction = string.Empty;
        public bool RequiresHumanDecision;
    }

    [Serializable]
    public sealed class UiGenerationGatingReport
    {
        public string TaskId = string.Empty;
        public string IntakeProfileId = string.Empty;
        public string Status = string.Empty;
        public List<UiGenerationGatingIssue> Issues = new List<UiGenerationGatingIssue>();
        public List<string> Notes = new List<string>();
    }

    [Serializable]
    public sealed class UiGenerationGatingIssue
    {
        public string IssueId = string.Empty;
        public string Severity = string.Empty;
        public string Kind = string.Empty;
        public string FieldPath = string.Empty;
        public string Summary = string.Empty;
        public string Details = string.Empty;
        public string SuggestedResolution = string.Empty;
        public bool RequiresHumanDecision;
    }

    [Serializable]
    public sealed class PreviewRenderPlan
    {
        public string TaskId = string.Empty;
        public string PlanVersion = string.Empty;
        public string SourceImagePath = string.Empty;
        public int CanvasWidth;
        public int CanvasHeight;
        public List<PreviewRenderNode> Nodes = new List<PreviewRenderNode>();
        public List<string> Notes = new List<string>();
    }

    [Serializable]
    public sealed class PreviewRenderNode
    {
        public string NodeId = string.Empty;
        public string NodeType = string.Empty;
        public UiRect Bounds = new UiRect();
        public string Text = string.Empty;
        public string FillColor = string.Empty;
        public string StrokeColor = string.Empty;
        public string AssetSlot = string.Empty;
        public int ZOrder;
    }

    [Serializable]
    public sealed class PreviewDiffReport
    {
        public string TaskId = string.Empty;
        public float CoverageScore;
        public float LayoutSimilarityScore;
        public int MissingRegionCount;
        public List<PreviewDiffRegion> Regions = new List<PreviewDiffRegion>();
        public List<string> Notes = new List<string>();
    }

    [Serializable]
    public sealed class PreviewDiffRegion
    {
        public string RegionId = string.Empty;
        public UiRect Bounds = new UiRect();
        public string DiffKind = string.Empty;
        public string Summary = string.Empty;
        public float SeverityScore;
    }
}
