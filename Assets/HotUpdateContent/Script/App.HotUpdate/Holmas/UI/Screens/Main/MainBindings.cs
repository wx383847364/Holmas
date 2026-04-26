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
        public const string EnergyTextKey = "main/energy_text";
        public const string SummaryTextKey = "main/summary_text";
        public const string StatusTextKey = "main/status_text";
        public const string PromotionButtonKey = "main/promotion_button";
        public const string AddEnergyButtonKey = "main/add_energy_button";
        public const string HelpButtonKey = "main/help_button";
        public const string GmButtonKey = "main/gm_button";
        public const string TutorialStepInputKey = "main/tutorial_step_input";
        public const string MinesGroupKey = "main/mines_group";
        public const string BoardContainerKey = "main/board_container";
        public const string TutorialBoardContainerKey = "main/tutorial_board_container";
        public const string WalkToggleKey = "main/walk_toggle";
        public const string FindToggleKey = "main/find_toggle";
        public const string ButtonClickEvent = "on_click";
        public const string ToggleChangedEvent = "on_value_changed";

        public const string RootNodePath = "MainPanel";
        public const string RuntimeOverlayNodeName = "RuntimeOverlay";
        public const string RuntimeOverlayNodePath = RootNodePath + "/" + RuntimeOverlayNodeName;
        public const string BottomToolsNodeName = "BottomTools";
        public const string BottomToolsNodePath = RuntimeOverlayNodePath + "/" + BottomToolsNodeName;
        public const string TopToolsNodeName = "TopTools";
        public const string TopToolsNodePath = RuntimeOverlayNodePath + "/" + TopToolsNodeName;
        public const string LevelTextNodePath = RuntimeOverlayNodePath + "/LevelText";
        public const string GoldTextNodePath = RuntimeOverlayNodePath + "/GoldText";
        public const string EnergyTextNodePath = RuntimeOverlayNodePath + "/EnergyText";
        public const string SummaryTextNodePath = RuntimeOverlayNodePath + "/SummaryText";
        public const string StatusTextNodePath = RuntimeOverlayNodePath + "/StatusText";
        public const string PromotionButtonNodePath = RuntimeOverlayNodePath + "/PromotionButton";
        public const string AddEnergyButtonNodePath = RuntimeOverlayNodePath + "/AddEnergyButton";
        public const string HelpButtonNodePath = TopToolsNodePath + "/HelpButton";
        public const string GmButtonNodePath = TopToolsNodePath + "/GmButton";
        public const string TutorialStepInputNodePath = RuntimeOverlayNodePath + "/TutorialStepInput";
        public const string MinesGroupNodePath = "MainPanel/MinesGroup";
        public const string BoardContainerNodePath = MinesGroupNodePath + "/BoardContainer";
        public const string TutorialBoardContainerNodePath = MinesGroupNodePath + "/TutorialBoardContainer";
        public const string WalkToggleNodePath = "MainPanel/WalkToggle";
        public const string FindToggleNodePath = "MainPanel/FindToggle";

        public RectTransform RootPanel;
        public TextMeshProUGUI LevelText;
        public TextMeshProUGUI GoldText;
        public TextMeshProUGUI EnergyText;
        public TextMeshProUGUI SummaryText;
        public TextMeshProUGUI StatusText;
        public Button PromotionButton;
        public Button AddEnergyButton;
        public Button HelpButton;
        public Button GmButton;
        public TMP_InputField TutorialStepInput;
        public RectTransform MinesGroup;
        public RectTransform BoardContainer;
        public RectTransform TutorialBoardContainer;
        public Toggle WalkToggle;
        public Toggle FindToggle;

        public bool HasRequiredBindings =>
            RootPanel != null &&
            LevelText != null &&
            GoldText != null &&
            EnergyText != null &&
            PromotionButton != null &&
            HelpButton != null &&
            GmButton != null &&
            MinesGroup != null &&
            BoardContainer != null &&
            TutorialBoardContainer != null &&
            WalkToggle != null &&
            FindToggle != null;

        public static bool HasCompleteBindings(UiBindingResolver resolver)
        {
            if (resolver == null)
            {
                return false;
            }

            return resolver.HasExplicitBinding<RectTransform>(RootPanelKey, nodePath: RootNodePath) &&
                   resolver.HasExplicitBinding<TextMeshProUGUI>(LevelTextKey, nodePath: LevelTextNodePath) &&
                   resolver.HasExplicitBinding<TextMeshProUGUI>(GoldTextKey, nodePath: GoldTextNodePath) &&
                   resolver.HasExplicitBinding<TextMeshProUGUI>(EnergyTextKey, nodePath: EnergyTextNodePath) &&
                   resolver.HasExplicitBinding<Button>(PromotionButtonKey, ButtonClickEvent, PromotionButtonNodePath) &&
                   resolver.HasExplicitBinding<Button>(HelpButtonKey, ButtonClickEvent, HelpButtonNodePath) &&
                   resolver.HasExplicitBinding<Button>(GmButtonKey, ButtonClickEvent, GmButtonNodePath) &&
                   resolver.HasExplicitBinding<RectTransform>(MinesGroupKey, nodePath: MinesGroupNodePath) &&
                   resolver.HasExplicitBinding<RectTransform>(BoardContainerKey, nodePath: BoardContainerNodePath) &&
                   resolver.HasExplicitBinding<RectTransform>(TutorialBoardContainerKey, nodePath: TutorialBoardContainerNodePath) &&
                   resolver.HasExplicitBinding<Toggle>(WalkToggleKey, ToggleChangedEvent, WalkToggleNodePath) &&
                   resolver.HasExplicitBinding<Toggle>(FindToggleKey, ToggleChangedEvent, FindToggleNodePath);
        }

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
            resolver.TryResolve(EnergyTextKey, out bindings.EnergyText, nodePath: EnergyTextNodePath);
            resolver.TryResolve(SummaryTextKey, out bindings.SummaryText, nodePath: SummaryTextNodePath);
            resolver.TryResolve(PromotionButtonKey, out bindings.PromotionButton, ButtonClickEvent, PromotionButtonNodePath);
            resolver.TryResolve(AddEnergyButtonKey, out bindings.AddEnergyButton, ButtonClickEvent, AddEnergyButtonNodePath);
            resolver.TryResolve(HelpButtonKey, out bindings.HelpButton, ButtonClickEvent, HelpButtonNodePath);
            resolver.TryResolve(GmButtonKey, out bindings.GmButton, ButtonClickEvent, GmButtonNodePath);
            resolver.TryResolve(TutorialStepInputKey, out bindings.TutorialStepInput, nodePath: TutorialStepInputNodePath);
            resolver.TryResolve(MinesGroupKey, out bindings.MinesGroup, nodePath: MinesGroupNodePath);
            resolver.TryResolve(BoardContainerKey, out bindings.BoardContainer, nodePath: BoardContainerNodePath);
            resolver.TryResolve(TutorialBoardContainerKey, out bindings.TutorialBoardContainer, nodePath: TutorialBoardContainerNodePath);
            resolver.TryResolve(WalkToggleKey, out bindings.WalkToggle, ToggleChangedEvent, WalkToggleNodePath);
            resolver.TryResolve(FindToggleKey, out bindings.FindToggle, ToggleChangedEvent, FindToggleNodePath);
            return bindings;
        }
    }
}
