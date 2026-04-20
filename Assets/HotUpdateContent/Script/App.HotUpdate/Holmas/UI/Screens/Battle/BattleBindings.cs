using App.HotUpdate.Holmas.UI.Binding;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace App.HotUpdate.Holmas.UI.Screens.Battle
{
    public sealed class BattleBindings
    {
        public const string RootPanelKey = "battle/root_panel";
        public const string BackButtonKey = "battle/back_button";
        public const string LevelTextKey = "battle/level_text";
        public const string GoldTextKey = "battle/gold_text";
        public const string EnergyTextKey = "battle/energy_text";
        public const string SummaryTextKey = "battle/summary_text";
        public const string StatusTextKey = "battle/status_text";
        public const string AddEnergyButtonKey = "battle/add_energy_button";
        public const string BoardContainerKey = "battle/board_container";
        public const string ButtonClickEvent = "on_click";

        public const string RootNodePath = "BattlePanel";
        public const string RuntimeOverlayNodeName = "RuntimeOverlay";
        public const string RuntimeOverlayNodePath = RootNodePath + "/" + RuntimeOverlayNodeName;
        public const string BackButtonNodePath = RuntimeOverlayNodePath + "/BackButton";
        public const string LevelTextNodePath = RuntimeOverlayNodePath + "/LevelText";
        public const string GoldTextNodePath = RuntimeOverlayNodePath + "/GoldText";
        public const string EnergyTextNodePath = RuntimeOverlayNodePath + "/EnergyText";
        public const string SummaryTextNodePath = RuntimeOverlayNodePath + "/SummaryText";
        public const string StatusTextNodePath = RuntimeOverlayNodePath + "/StatusText";
        public const string AddEnergyButtonNodePath = RuntimeOverlayNodePath + "/AddEnergyButton";
        public const string BoardContainerNodePath = RuntimeOverlayNodePath + "/BoardContainer";

        public RectTransform RootPanel;
        public Button BackButton;
        public TextMeshProUGUI LevelText;
        public TextMeshProUGUI GoldText;
        public TextMeshProUGUI EnergyText;
        public TextMeshProUGUI SummaryText;
        public TextMeshProUGUI StatusText;
        public Button AddEnergyButton;
        public RectTransform BoardContainer;

        public bool HasRequiredBindings =>
            RootPanel != null &&
            BackButton != null &&
            LevelText != null &&
            GoldText != null &&
            EnergyText != null &&
            SummaryText != null &&
            StatusText != null &&
            AddEnergyButton != null &&
            BoardContainer != null;

        public static BattleBindings Resolve(UiBindingResolver resolver)
        {
            var bindings = new BattleBindings();
            if (resolver == null || !resolver.HasCollector)
            {
                return bindings;
            }

            resolver.TryResolve(RootPanelKey, out bindings.RootPanel, nodePath: RootNodePath);
            resolver.TryResolve(BackButtonKey, out bindings.BackButton, ButtonClickEvent, BackButtonNodePath);
            resolver.TryResolve(LevelTextKey, out bindings.LevelText, nodePath: LevelTextNodePath);
            resolver.TryResolve(GoldTextKey, out bindings.GoldText, nodePath: GoldTextNodePath);
            resolver.TryResolve(EnergyTextKey, out bindings.EnergyText, nodePath: EnergyTextNodePath);
            resolver.TryResolve(SummaryTextKey, out bindings.SummaryText, nodePath: SummaryTextNodePath);
            resolver.TryResolve(StatusTextKey, out bindings.StatusText, nodePath: StatusTextNodePath);
            resolver.TryResolve(AddEnergyButtonKey, out bindings.AddEnergyButton, ButtonClickEvent, AddEnergyButtonNodePath);
            resolver.TryResolve(BoardContainerKey, out bindings.BoardContainer, nodePath: BoardContainerNodePath);
            return bindings;
        }
    }
}
