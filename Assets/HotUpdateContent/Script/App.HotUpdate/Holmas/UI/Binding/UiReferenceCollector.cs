using System.Collections.Generic;
using UnityEngine;

namespace App.HotUpdate.Holmas.UI.Binding
{
    public sealed class UiReferenceCollector : MonoBehaviour
    {
        [SerializeField] private List<UiBindingEntry> _entries = new List<UiBindingEntry>();

        public IReadOnlyList<UiBindingEntry> Entries => _entries;

        public int EntryCount => _entries != null ? _entries.Count : 0;

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
                    return;
                }
            }

            _entries.Add(UiBindingEntry.Create(bindingKey, target, eventName, nodePath));
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
    }
}
