using System;
using UnityEngine;

namespace App.HotUpdate.Holmas.UI.Binding
{
    [Serializable]
    public sealed class UiBindingEntry
    {
        [SerializeField] private string _bindingKey = string.Empty;
        [SerializeField] private string _componentType = string.Empty;
        [SerializeField] private string _nodePath = string.Empty;
        [SerializeField] private string _eventName = string.Empty;
        [SerializeField] private Component _target;

        public string BindingKey => _bindingKey;

        public string ComponentType => _componentType;

        public string NodePath => _nodePath;

        public string EventName => _eventName;

        public Component Target => _target;

        public static UiBindingEntry Create(string bindingKey, Component target, string eventName, string nodePath)
        {
            return new UiBindingEntry
            {
                _bindingKey = bindingKey ?? string.Empty,
                _componentType = target != null ? target.GetType().Name : string.Empty,
                _eventName = eventName ?? string.Empty,
                _nodePath = nodePath ?? string.Empty,
                _target = target,
            };
        }
    }
}
