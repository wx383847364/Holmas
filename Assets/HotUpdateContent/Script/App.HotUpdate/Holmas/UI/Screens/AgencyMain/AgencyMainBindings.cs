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

        public RectTransform RootPanel;
        public Text TitleText;
        public Text SummaryText;
        public Text TaskSummaryText;
        public Text BoardSummaryText;
        public Text StatusText;
        public Button PrimaryActionButton;

        public static AgencyMainBindings Resolve(UiBindingResolver resolver)
        {
            var bindings = new AgencyMainBindings();
            if (resolver == null || !resolver.HasCollector)
            {
                return bindings;
            }

            resolver.TryResolve(RootPanelKey, out bindings.RootPanel);
            resolver.TryResolve(TitleTextKey, out bindings.TitleText);
            resolver.TryResolve(SummaryTextKey, out bindings.SummaryText);
            resolver.TryResolve(TaskSummaryTextKey, out bindings.TaskSummaryText);
            resolver.TryResolve(BoardSummaryTextKey, out bindings.BoardSummaryText);
            resolver.TryResolve(StatusTextKey, out bindings.StatusText);
            resolver.TryResolve(PrimaryActionButtonKey, out bindings.PrimaryActionButton, PrimaryActionButtonClickEvent);
            return bindings;
        }
    }
}
