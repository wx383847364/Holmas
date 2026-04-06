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
}
