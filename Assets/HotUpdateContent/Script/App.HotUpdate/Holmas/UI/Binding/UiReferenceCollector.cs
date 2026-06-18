using System.Collections.Generic;
using UnityEngine;
using FoundationCollector = WX.Foundation.UI.Binding.UiReferenceCollector;

namespace App.HotUpdate.Holmas.UI.Binding
{
    /// <summary>
    /// Legacy serialized collector retained so existing Holmas prefabs keep their script GUID and field data.
    /// Runtime binding consumption is mirrored into WX.Foundation.UI.Binding.UiReferenceCollector.
    /// </summary>
    public sealed class UiReferenceCollector : MonoBehaviour
    {
        [SerializeField] private List<UiBindingEntry> _entries = new List<UiBindingEntry>();
        private FoundationCollector _foundationCollector;

        public IReadOnlyList<UiBindingEntry> Entries => _entries;

        public int EntryCount => _entries != null ? _entries.Count : 0;

        public void Clear()
        {
            _entries.Clear();
            SyncToFoundationCollector();
        }

        public void RegisterOrReplace(string bindingKey, Component target, string eventName = null, string nodePath = null)
        {
            if (string.IsNullOrWhiteSpace(bindingKey) || target == null)
            {
                return;
            }

            for (int i = 0; i < _entries.Count; i++)
            {
                UiBindingEntry entry = _entries[i];
                if (entry != null && entry.BindingKey == bindingKey && entry.EventName == (eventName ?? string.Empty))
                {
                    _entries[i] = UiBindingEntry.Create(bindingKey, target, eventName, nodePath);
                    SyncToFoundationCollector();
                    return;
                }
            }

            _entries.Add(UiBindingEntry.Create(bindingKey, target, eventName, nodePath));
            SyncToFoundationCollector();
        }

        public bool TryGetEntry(string bindingKey, string eventName, string nodePath, string componentType, out UiBindingEntry entry)
        {
            UiBindingEntry bestEntry = null;
            int bestScore = -1;

            for (int i = 0; i < _entries.Count; i++)
            {
                UiBindingEntry current = _entries[i];
                if (current == null)
                {
                    continue;
                }

                if (!current.Matches(bindingKey, eventName, nodePath, componentType))
                {
                    continue;
                }

                int score = 0;
                if (!string.IsNullOrWhiteSpace(bindingKey))
                {
                    score++;
                }

                if (!string.IsNullOrWhiteSpace(eventName))
                {
                    score++;
                }

                if (!string.IsNullOrWhiteSpace(nodePath))
                {
                    score++;
                }

                if (!string.IsNullOrWhiteSpace(componentType))
                {
                    score++;
                }

                if (score > bestScore)
                {
                    bestEntry = current;
                    bestScore = score;
                }
            }

            entry = bestEntry;
            return entry != null;
        }

        public bool TryGetEntry(string bindingKey, string eventName, string nodePath, out UiBindingEntry entry)
        {
            return TryGetEntry(bindingKey, eventName, nodePath, null, out entry);
        }

        public FoundationCollector GetOrCreateFoundationCollector()
        {
            if (_foundationCollector == null)
            {
                _foundationCollector = GetComponent<FoundationCollector>();
            }

            if (_foundationCollector == null)
            {
                _foundationCollector = gameObject.AddComponent<FoundationCollector>();
                _foundationCollector.hideFlags = HideFlags.DontSave;
            }

            SyncToFoundationCollector();
            return _foundationCollector;
        }

        private void SyncToFoundationCollector()
        {
            if (_foundationCollector == null)
            {
                _foundationCollector = GetComponent<FoundationCollector>();
            }

            if (_foundationCollector == null)
            {
                return;
            }

            _foundationCollector.Clear();
            for (int i = 0; i < _entries.Count; i++)
            {
                UiBindingEntry entry = _entries[i];
                if (entry == null || entry.Target == null)
                {
                    continue;
                }

                _foundationCollector.RegisterOrReplace(
                    entry.BindingKey,
                    entry.Target,
                    entry.EventName,
                    entry.NodePath);
            }
        }
    }
}
