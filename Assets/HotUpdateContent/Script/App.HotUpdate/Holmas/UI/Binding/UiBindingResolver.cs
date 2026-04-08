using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace App.HotUpdate.Holmas.UI.Binding
{
    public sealed class UiBindingResolver
    {
        private readonly UiReferenceCollector _collector;
        private readonly UiBindingManifest _manifest;

        public UiBindingResolver(UiReferenceCollector collector, UiBindingManifest manifest = null)
        {
            _collector = collector;
            _manifest = manifest;
        }

        public bool HasCollector => _collector != null;

        public bool HasManifest => _manifest != null;

        public bool TryResolve<T>(string bindingKey, out T component, string eventName = null, string nodePath = null)
            where T : Component
        {
            component = null;
            if (_collector == null)
            {
                return false;
            }

            string componentType = typeof(T).Name;
            UiBindingManifestEntry manifestEntry = null;
            if (_manifest != null)
            {
                _manifest.TryFindEntry(bindingKey, eventName, nodePath, componentType, out manifestEntry);
            }

            string resolvedBindingKey = manifestEntry != null && !string.IsNullOrWhiteSpace(manifestEntry.BindingKey)
                ? manifestEntry.BindingKey
                : bindingKey;
            string resolvedEventName = manifestEntry != null && !string.IsNullOrWhiteSpace(manifestEntry.EventName)
                ? manifestEntry.EventName
                : eventName;
            string resolvedNodePath = manifestEntry != null && !string.IsNullOrWhiteSpace(manifestEntry.NodePath)
                ? manifestEntry.NodePath
                : nodePath;
            string resolvedComponentType = manifestEntry != null && !string.IsNullOrWhiteSpace(manifestEntry.ComponentType)
                ? manifestEntry.ComponentType
                : componentType;

            if (!_collector.TryGetEntry(resolvedBindingKey, resolvedEventName, resolvedNodePath, resolvedComponentType, out UiBindingEntry entry))
            {
                if (manifestEntry != null)
                {
                    return false;
                }

                if (!_collector.TryGetEntry(bindingKey, eventName, nodePath, componentType, out entry))
                {
                    return false;
                }
            }

            component = entry.Target as T;
            return component != null;
        }

        public bool HasExplicitBinding<T>(string bindingKey, string eventName = null, string nodePath = null)
            where T : Component
        {
            return TryResolve(bindingKey, out T _, eventName, nodePath);
        }

        public T ResolveRequired<T>(string bindingKey, string eventName = null, string nodePath = null)
            where T : Component
        {
            if (TryResolve(bindingKey, out T component, eventName, nodePath))
            {
                return component;
            }

            throw new System.InvalidOperationException(
                $"UiBindingResolver: 找不到 binding_key={bindingKey}, event_name={eventName}, node_path={nodePath} 对应的 {typeof(T).Name}。");
        }

        public bool BindButtonClick(string bindingKey, string eventName, UnityAction handler, string nodePath = null)
        {
            if (handler == null)
            {
                return false;
            }

            if (!TryResolve(bindingKey, out Button button, eventName, nodePath))
            {
                return false;
            }

            button.onClick.AddListener(handler);
            return true;
        }
    }
}
