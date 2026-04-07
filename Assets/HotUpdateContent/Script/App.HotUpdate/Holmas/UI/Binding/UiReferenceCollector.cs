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

        public bool TryGetEntry(string bindingKey, string eventName, string nodePath, out UiBindingEntry entry)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                UiBindingEntry current = _entries[i];
                if (current == null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(bindingKey) && current.BindingKey == bindingKey)
                {
                    entry = current;
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(eventName) && current.EventName == eventName)
                {
                    entry = current;
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(nodePath) && current.NodePath == nodePath)
                {
                    entry = current;
                    return true;
                }
            }

            entry = null;
            return false;
        }
    }
}
