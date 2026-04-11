using App.HotUpdate.Holmas.UI.Binding;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace App.HotUpdate.Holmas.UI.Screens.Main
{
    public sealed class MainBindings
    {
        public const string RootPanelKey = "main/root_panel";
        public const string LevelTextKey = "main/level_text";
        public const string GoldTextKey = "main/gold_text";
        public const string SummaryTextKey = "main/summary_text";
        public const string StatusTextKey = "main/status_text";
        public const string StartButtonKey = "main/start_button";
        public const string PromotionButtonKey = "main/promotion_button";
        public const string ButtonClickEvent = "on_click";

        public const string RootNodePath = "MainPanel";
        public const string RuntimeOverlayNodeName = "RuntimeOverlay";
        public const string RuntimeOverlayNodePath = RootNodePath + "/" + RuntimeOverlayNodeName;
        public const string LevelTextNodePath = RuntimeOverlayNodePath + "/LevelText";
        public const string GoldTextNodePath = RuntimeOverlayNodePath + "/GoldText";
        public const string SummaryTextNodePath = RuntimeOverlayNodePath + "/SummaryText";
        public const string StatusTextNodePath = RuntimeOverlayNodePath + "/StatusText";
        public const string StartButtonNodePath = RuntimeOverlayNodePath + "/StartButton";
        public const string PromotionButtonNodePath = RuntimeOverlayNodePath + "/PromotionButton";

        public RectTransform RootPanel;
        public TextMeshProUGUI LevelText;
        public TextMeshProUGUI GoldText;
        public TextMeshProUGUI SummaryText;
        public TextMeshProUGUI StatusText;
        public Button StartButton;
        public Button PromotionButton;

        public bool HasRequiredBindings =>
            RootPanel != null &&
            LevelText != null &&
            GoldText != null &&
            SummaryText != null &&
            StatusText != null &&
            StartButton != null &&
            PromotionButton != null;

        public static MainBindings Resolve(UiBindingResolver resolver)
        {
            var bindings = new MainBindings();
            if (resolver == null || !resolver.HasCollector)
            {
                return bindings;
            }

            resolver.TryResolve(RootPanelKey, out bindings.RootPanel, nodePath: RootNodePath);
            resolver.TryResolve(LevelTextKey, out bindings.LevelText, nodePath: LevelTextNodePath);
            resolver.TryResolve(GoldTextKey, out bindings.GoldText, nodePath: GoldTextNodePath);
            resolver.TryResolve(SummaryTextKey, out bindings.SummaryText, nodePath: SummaryTextNodePath);
            resolver.TryResolve(StatusTextKey, out bindings.StatusText, nodePath: StatusTextNodePath);
            resolver.TryResolve(StartButtonKey, out bindings.StartButton, ButtonClickEvent, StartButtonNodePath);
            resolver.TryResolve(PromotionButtonKey, out bindings.PromotionButton, ButtonClickEvent, PromotionButtonNodePath);
            return bindings;
        }
    }
}
