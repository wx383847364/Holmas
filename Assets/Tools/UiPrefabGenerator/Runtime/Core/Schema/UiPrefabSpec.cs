using System;
using System.Collections.Generic;

namespace UiPrefabGenerator.Core.Schema
{
    [Serializable]
    public sealed class UiPrefabSpec
    {
        public string PageId = string.Empty;
        public string PrefabName = string.Empty;
        public string RootNodeId = string.Empty;
        public string GenerationProfileId = string.Empty;
        public List<UiNodeSpec> Nodes = new List<UiNodeSpec>();
        public List<UiBindingSpec> Bindings = new List<UiBindingSpec>();
        public List<UiInteractionSpec> Interactions = new List<UiInteractionSpec>();
    }

    [Serializable]
    public sealed class UiNodeSpec
    {
        public string NodeId = string.Empty;
        public string NodeName = string.Empty;
        public string ParentNodeId = string.Empty;
        public List<UiComponentSpec> Components = new List<UiComponentSpec>();
        public UiLayoutSpec Layout = new UiLayoutSpec();
    }

    [Serializable]
    public sealed class UiComponentSpec
    {
        public string ComponentType = string.Empty;
        public string BindingKey = string.Empty;
        public string AssetSlot = string.Empty;
    }

    [Serializable]
    public sealed class UiLayoutSpec
    {
        public string LayoutType = string.Empty;
        public string LayoutSlot = string.Empty;
    }

    [Serializable]
    public sealed class UiBindingSpec
    {
        public string NodeId = string.Empty;
        public string BindingKey = string.Empty;
    }

    [Serializable]
    public sealed class UiInteractionSpec
    {
        public string NodeId = string.Empty;
        public string EventName = string.Empty;
        public string HandlerKey = string.Empty;
    }
}
