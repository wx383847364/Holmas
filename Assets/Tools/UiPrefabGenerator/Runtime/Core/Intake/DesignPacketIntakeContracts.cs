using System;
using System.Collections.Generic;
using UiPrefabGenerator.Core.Schema;

namespace UiPrefabGenerator.Core.Intake
{
    public interface IDesignPacketIntakeAnalyzer
    {
        DesignPacketIntakeAssessment Analyze(DesignPacket designPacket);
    }

    public enum DesignPacketIntakeIssueSeverity
    {
        Info = 0,
        Warning = 1,
        Blocking = 2,
    }

    public enum DesignPacketIntakeIssueKind
    {
        MissingRequiredField = 0,
        MissingDesignImage = 1,
        MissingStateDefinition = 2,
        MissingAssetSlotHint = 3,
        AmbiguousImageStateMapping = 4,
        AmbiguousAssetSlotMapping = 5,
        UnsupportedRule = 6,
        NamingConflict = 7,
        InvalidFieldValue = 8,
        HumanDecisionRequired = 9,
        LowConfidenceEvidence = 10,
        MissingCriticalControl = 11,
        LayoutConflict = 12,
        SemanticConflict = 13,
    }

    [Serializable]
    public sealed class DesignPacketIntakeIssue
    {
        public string IssueId = string.Empty;
        public DesignPacketIntakeIssueSeverity Severity;
        public DesignPacketIntakeIssueKind Kind;
        public string FieldPath = string.Empty;
        public string Summary = string.Empty;
        public string Details = string.Empty;
        public string SuggestedResolution = string.Empty;
        public string RelatedImageId = string.Empty;
        public string RelatedStateId = string.Empty;
        public string RelatedSlotId = string.Empty;
        public bool RequiresHumanDecision;
    }

    [Serializable]
    public sealed class DesignPacketIntakeAssessment
    {
        public DesignPacket SourcePacket;
        public string IntakeProfileId = string.Empty;
        public List<DesignPacketIntakeIssue> UnresolvedItems = new List<DesignPacketIntakeIssue>();
        public List<string> Notes = new List<string>();

        public bool HasBlockingIssues
        {
            get
            {
                if (UnresolvedItems == null)
                {
                    return false;
                }

                for (int i = 0; i < UnresolvedItems.Count; i++)
                {
                    DesignPacketIntakeIssue item = UnresolvedItems[i];
                    if (item != null && item.Severity == DesignPacketIntakeIssueSeverity.Blocking)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public bool IsReadyForSpec
        {
            get { return SourcePacket != null && !HasBlockingIssues; }
        }
    }
}
