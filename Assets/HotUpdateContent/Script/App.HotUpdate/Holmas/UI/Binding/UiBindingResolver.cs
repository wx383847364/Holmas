using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace App.HotUpdate.Holmas.UI.Binding
{
    public sealed class UiBindingResolver
    {
        private readonly UiReferenceCollector _collector;

        public UiBindingResolver(UiReferenceCollector collector)
        {
            _collector = collector;
        }

        public bool HasCollector => _collector != null;

        public bool TryResolve<T>(string bindingKey, out T component, string eventName = null, string nodePath = null)
            where T : Component
        {
            component = null;
            if (_collector == null)
            {
                return false;
            }

            if (!_collector.TryGetEntry(bindingKey, eventName, nodePath, out UiBindingEntry entry))
            {
                return false;
            }

            component = entry.Target as T;
            return component != null;
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
