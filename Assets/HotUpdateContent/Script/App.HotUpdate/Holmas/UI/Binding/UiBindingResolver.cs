using UnityEngine.Events;
using FoundationManifest = WX.Foundation.UI.Binding.UiBindingManifest;
using FoundationResolver = WX.Foundation.UI.Binding.UiBindingResolver;

namespace App.HotUpdate.Holmas.UI.Binding
{
    /// <summary>
    /// Thin compatibility adapter while Holmas prefabs still serialize the legacy collector component.
    /// </summary>
    public sealed class UiBindingResolver
    {
        private readonly FoundationResolver _resolver;

        public UiBindingResolver(UiReferenceCollector collector, FoundationManifest manifest = null)
        {
            _resolver = new FoundationResolver(collector != null ? collector.GetOrCreateFoundationCollector() : null, manifest);
        }

        public bool HasCollector => _resolver.HasCollector;

        public bool HasManifest => _resolver.HasManifest;

        public bool TryResolve<T>(string bindingKey, out T component, string eventName = null, string nodePath = null)
            where T : UnityEngine.Component
        {
            return _resolver.TryResolve(bindingKey, out component, eventName, nodePath);
        }

        public bool HasExplicitBinding<T>(string bindingKey, string eventName = null, string nodePath = null)
            where T : UnityEngine.Component
        {
            return TryResolve(bindingKey, out T _, eventName, nodePath);
        }

        public T ResolveRequired<T>(string bindingKey, string eventName = null, string nodePath = null)
            where T : UnityEngine.Component
        {
            return _resolver.ResolveRequired<T>(bindingKey, eventName, nodePath);
        }

        public bool BindButtonClick(string bindingKey, string eventName, UnityAction handler, string nodePath = null)
        {
            return _resolver.BindButtonClick(bindingKey, eventName, handler, nodePath);
        }
    }
}
