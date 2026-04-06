using System;
using System.Collections.Generic;

namespace UiPrefabGenerator.Core.Schema
{
    [Serializable]
    public sealed class PrefabBindingManifest
    {
        public string PrefabName = string.Empty;
        public string PrefabDraftPath = string.Empty;
        public List<PrefabBindingEntry> Entries = new List<PrefabBindingEntry>();
    }

    [Serializable]
    public sealed class PrefabBindingEntry
    {
        public string NodePath = string.Empty;
        public string ComponentType = string.Empty;
        public string BindingKey = string.Empty;
        public string AssetSlot = string.Empty;
        public string EventName = string.Empty;
        public bool RequiresManualWiring;
        public string Notes = string.Empty;
    }
}
