using App.HotUpdate.Holmas.UI.Core;

namespace App.HotUpdate.Holmas.UI.Screens.GmTool
{
    public static class GmToolScreenRegistration
    {
        public const string ScreenId = "gm.tool.popup";

        public static UiScreenDefinition CreateDefinition()
        {
            return new UiScreenDefinition(
                ScreenId,
                string.Empty,
                UiScreenKind.Popup,
                typeof(GmToolPopupController))
            {
                CachePolicy = UiCachePolicy.KeepInstance,
                ClickOutsideToClose = true,
                BlockInputDuringTransition = false,
            };
        }
    }
}
