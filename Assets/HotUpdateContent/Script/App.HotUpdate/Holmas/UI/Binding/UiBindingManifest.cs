using System;
using System.Collections.Generic;

namespace App.HotUpdate.Holmas.UI.Binding
{
    public sealed class UiBindingManifest
    {
        private readonly List<UiBindingManifestEntry> _entries = new List<UiBindingManifestEntry>();
        private readonly List<string> _manualWiringNodePaths = new List<string>();

        public UiBindingManifest(string screenId, string prefabName, string prefabAssetPath)
        {
            ScreenId = screenId ?? string.Empty;
            PrefabName = prefabName ?? string.Empty;
            PrefabAssetPath = prefabAssetPath ?? string.Empty;
        }

        public string ScreenId { get; }

        public string PrefabName { get; }

        public string PrefabAssetPath { get; }

        public IReadOnlyList<UiBindingManifestEntry> Entries => _entries;

        public IReadOnlyList<string> ManualWiringNodePaths => _manualWiringNodePaths;

        public void AddEntry(
            string bindingKey,
            string componentType,
            string nodePath,
            string eventName = null,
            bool requiresManualWiring = false,
            string notes = null)
        {
            _entries.Add(new UiBindingManifestEntry(
                bindingKey,
                componentType,
                nodePath,
                eventName,
                requiresManualWiring,
                notes));

            if (requiresManualWiring &&
                !string.IsNullOrWhiteSpace(nodePath) &&
                !_manualWiringNodePaths.Contains(nodePath))
            {
                _manualWiringNodePaths.Add(nodePath);
            }
        }

        public bool TryFindEntry(
            string bindingKey,
            string eventName,
            string nodePath,
            string componentType,
            out UiBindingManifestEntry entry)
        {
            UiBindingManifestEntry bestEntry = null;
            int bestScore = -1;

            for (int i = 0; i < _entries.Count; i++)
            {
                UiBindingManifestEntry current = _entries[i];
                if (current == null || !current.Matches(bindingKey, eventName, nodePath, componentType))
                {
                    continue;
                }

                int score = current.ComputeSpecificityScore(bindingKey, eventName, nodePath, componentType);
                if (score > bestScore)
                {
                    bestEntry = current;
                    bestScore = score;
                }
            }

            entry = bestEntry;
            return entry != null;
        }
    }

    public sealed class UiBindingManifestEntry
    {
        public UiBindingManifestEntry(
            string bindingKey,
            string componentType,
            string nodePath,
            string eventName,
            bool requiresManualWiring,
            string notes)
        {
            BindingKey = bindingKey ?? string.Empty;
            ComponentType = componentType ?? string.Empty;
            NodePath = nodePath ?? string.Empty;
            EventName = eventName ?? string.Empty;
            RequiresManualWiring = requiresManualWiring;
            Notes = notes ?? string.Empty;
        }

        public string BindingKey { get; }

        public string ComponentType { get; }

        public string NodePath { get; }

        public string EventName { get; }

        public bool RequiresManualWiring { get; }

        public string Notes { get; }

        public bool Matches(string bindingKey, string eventName, string nodePath, string componentType)
        {
            if (!MatchesField(BindingKey, bindingKey))
            {
                return false;
            }

            if (!MatchesField(EventName, eventName))
            {
                return false;
            }

            if (!MatchesField(NodePath, nodePath))
            {
                return false;
            }

            if (!MatchesField(ComponentType, componentType))
            {
                return false;
            }

            return true;
        }

        public int ComputeSpecificityScore(string bindingKey, string eventName, string nodePath, string componentType)
        {
            int score = 0;

            if (!string.IsNullOrWhiteSpace(bindingKey) && string.Equals(BindingKey, bindingKey, StringComparison.Ordinal))
            {
                score++;
            }

            if (!string.IsNullOrWhiteSpace(eventName) && string.Equals(EventName, eventName, StringComparison.Ordinal))
            {
                score++;
            }

            if (!string.IsNullOrWhiteSpace(nodePath) && string.Equals(NodePath, nodePath, StringComparison.Ordinal))
            {
                score++;
            }

            if (!string.IsNullOrWhiteSpace(componentType) && string.Equals(ComponentType, componentType, StringComparison.Ordinal))
            {
                score++;
            }

            return score;
        }

        private static bool MatchesField(string actual, string expected)
        {
            return string.IsNullOrWhiteSpace(expected) || string.Equals(actual, expected, StringComparison.Ordinal);
        }
    }
}
