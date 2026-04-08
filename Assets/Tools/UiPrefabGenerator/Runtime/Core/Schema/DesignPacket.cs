using System;
using System.Collections.Generic;

namespace UiPrefabGenerator.Core.Schema
{
    [Serializable]
    public sealed class DesignPacket
    {
        public string PageId = string.Empty;
        public string PageTitle = string.Empty;
        public string PrefabName = string.Empty;
        public List<DesignImageReference> DesignImages = new List<DesignImageReference>();
        public List<DesignStateDefinition> States = new List<DesignStateDefinition>();
        public List<DesignRuleDefinition> Rules = new List<DesignRuleDefinition>();
        public List<DesignAssetSlotHint> AssetSlotHints = new List<DesignAssetSlotHint>();
        public List<string> ExpectedSemanticRoles = new List<string>();
        public List<DesignElementHint> ElementHints = new List<DesignElementHint>();
        public List<DesignReviewHint> ReviewHints = new List<DesignReviewHint>();
        public string Notes = string.Empty;
    }

    [Serializable]
    public sealed class DesignImageReference
    {
        public string ImageId = string.Empty;
        public string ImagePath = string.Empty;
        public string StateId = string.Empty;
    }

    [Serializable]
    public sealed class DesignStateDefinition
    {
        public string StateId = string.Empty;
        public string Description = string.Empty;
    }

    [Serializable]
    public sealed class DesignRuleDefinition
    {
        public string RuleId = string.Empty;
        public string Description = string.Empty;
    }

    [Serializable]
    public sealed class DesignAssetSlotHint
    {
        public string SlotId = string.Empty;
        public string Usage = string.Empty;
    }

    [Serializable]
    public sealed class DesignElementHint
    {
        public string HintId = string.Empty;
        public string SourceElementId = string.Empty;
        public string ElementType = string.Empty;
        public string SemanticRole = string.Empty;
        public string SuggestedNodeId = string.Empty;
        public string DisplayText = string.Empty;
        public string AssetSlot = string.Empty;
        public string LayoutSlot = string.Empty;
        public string BindingKey = string.Empty;
        public string HandlerKey = string.Empty;
        public float Confidence;
        public bool Required;
        public bool RequiresHumanDecision;
        public UiRect Bounds = new UiRect();
    }

    [Serializable]
    public sealed class DesignReviewHint
    {
        public string HintId = string.Empty;
        public string IssueKind = string.Empty;
        public string Severity = string.Empty;
        public string Summary = string.Empty;
        public string Details = string.Empty;
        public string RelatedHintId = string.Empty;
        public string SuggestedResolution = string.Empty;
        public float Confidence = 1f;
        public bool BlocksSpec;
        public bool RequiresHumanDecision;
    }
}
