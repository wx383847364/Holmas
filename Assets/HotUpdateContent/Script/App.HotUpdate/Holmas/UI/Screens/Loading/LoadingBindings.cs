using App.HotUpdate.Holmas.UI.Binding;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace App.HotUpdate.Holmas.UI.Screens.Loading
{
    public sealed class LoadingBindings
    {
        public const string RootPanelKey = "loading/root_panel";
        public const string LoadingBarKey = "loading/loading_bar";
        public const string StatusTextKey = "loading/status_text";

        public const string RootNodePath = "LoadingPanel";
        public const string RuntimeOverlayNodeName = "RuntimeOverlay";
        public const string RuntimeOverlayNodePath = RootNodePath + "/" + RuntimeOverlayNodeName;
        public const string LoadingBarNodePath = RuntimeOverlayNodePath + "/LoadingBar";
        public const string StatusTextNodePath = RuntimeOverlayNodePath + "/StatusText";

        public RectTransform RootPanel;
        public Slider LoadingBar;
        public TextMeshProUGUI StatusText;

        public bool HasRequiredBindings => RootPanel != null && LoadingBar != null && StatusText != null;

        public static LoadingBindings Resolve(UiBindingResolver resolver)
        {
            var bindings = new LoadingBindings();
            if (resolver == null || !resolver.HasCollector)
            {
                return bindings;
            }

            resolver.TryResolve(RootPanelKey, out bindings.RootPanel, nodePath: RootNodePath);
            resolver.TryResolve(LoadingBarKey, out bindings.LoadingBar, nodePath: LoadingBarNodePath);
            resolver.TryResolve(StatusTextKey, out bindings.StatusText, nodePath: StatusTextNodePath);
            return bindings;
        }
    }
}
