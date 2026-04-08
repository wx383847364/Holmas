using App.HotUpdate.Holmas.UI.Binding;
using UnityEngine;
using UnityEngine.UI;

namespace App.HotUpdate.Holmas.UI.Screens.AgencyMain
{
    public sealed class AgencyMainBindings
    {
        public const string RootPanelKey = "agency_main/root_panel";
        public const string TitleTextKey = "agency_main/title_text";
        public const string SummaryTextKey = "agency_main/summary_text";
        public const string TaskSummaryTextKey = "agency_main/task_summary_text";
        public const string BoardSummaryTextKey = "agency_main/board_summary_text";
        public const string StatusTextKey = "agency_main/status_text";
        public const string PrimaryActionButtonKey = "agency_main/primary_action_button";
        public const string PrimaryActionButtonClickEvent = "on_click";
        public const string RootNodePath = "AgencyMainPanel";
        public const string ContentNodeName = "SafeAreaContent";
        public const string ContentNodePath = RootNodePath + "/" + ContentNodeName;
        public const string TitleTextNodePath = ContentNodePath + "/TitleText";
        public const string SummaryTextNodePath = ContentNodePath + "/SummaryText";
        public const string TaskSummaryTextNodePath = ContentNodePath + "/TaskSummaryText";
        public const string BoardSummaryTextNodePath = ContentNodePath + "/BoardSummaryText";
        public const string StatusTextNodePath = ContentNodePath + "/StatusText";
        public const string PrimaryActionButtonNodePath = ContentNodePath + "/PrimaryActionButton";

        public RectTransform RootPanel;
        public Text TitleText;
        public Text SummaryText;
        public Text TaskSummaryText;
        public Text BoardSummaryText;
        public Text StatusText;
        public Button PrimaryActionButton;

        public bool HasRequiredBindings =>
            RootPanel != null &&
            TitleText != null &&
            SummaryText != null &&
            TaskSummaryText != null &&
            BoardSummaryText != null &&
            StatusText != null &&
            PrimaryActionButton != null;

        public static bool HasCompleteBindings(UiBindingResolver resolver)
        {
            if (resolver == null)
            {
                return false;
            }

            return resolver.HasExplicitBinding<RectTransform>(RootPanelKey, nodePath: RootNodePath) &&
                   resolver.HasExplicitBinding<Text>(TitleTextKey, nodePath: TitleTextNodePath) &&
                   resolver.HasExplicitBinding<Text>(SummaryTextKey, nodePath: SummaryTextNodePath) &&
                   resolver.HasExplicitBinding<Text>(TaskSummaryTextKey, nodePath: TaskSummaryTextNodePath) &&
                   resolver.HasExplicitBinding<Text>(BoardSummaryTextKey, nodePath: BoardSummaryTextNodePath) &&
                   resolver.HasExplicitBinding<Text>(StatusTextKey, nodePath: StatusTextNodePath) &&
                   resolver.HasExplicitBinding<Button>(
                       PrimaryActionButtonKey,
                       PrimaryActionButtonClickEvent,
                       PrimaryActionButtonNodePath);
        }

        public static AgencyMainBindings Resolve(UiBindingResolver resolver)
        {
            var bindings = new AgencyMainBindings();
            if (resolver == null || !resolver.HasCollector)
            {
                return bindings;
            }

            resolver.TryResolve(RootPanelKey, out bindings.RootPanel, nodePath: RootNodePath);
            resolver.TryResolve(TitleTextKey, out bindings.TitleText, nodePath: TitleTextNodePath);
            resolver.TryResolve(SummaryTextKey, out bindings.SummaryText, nodePath: SummaryTextNodePath);
            resolver.TryResolve(TaskSummaryTextKey, out bindings.TaskSummaryText, nodePath: TaskSummaryTextNodePath);
            resolver.TryResolve(BoardSummaryTextKey, out bindings.BoardSummaryText, nodePath: BoardSummaryTextNodePath);
            resolver.TryResolve(StatusTextKey, out bindings.StatusText, nodePath: StatusTextNodePath);
            resolver.TryResolve(
                PrimaryActionButtonKey,
                out bindings.PrimaryActionButton,
                PrimaryActionButtonClickEvent,
                PrimaryActionButtonNodePath);
            return bindings;
        }
    }
}
