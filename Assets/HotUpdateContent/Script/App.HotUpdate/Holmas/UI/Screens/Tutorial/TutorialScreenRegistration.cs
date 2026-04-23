using App.HotUpdate.Holmas.UI.Core;

namespace App.HotUpdate.Holmas.UI.Screens.Tutorial
{
    public static class TutorialScreenRegistration
    {
        public const string ScreenId = "tutorial.core_find_cat.overlay";

        public static UiScreenDefinition CreateDefinition()
        {
            return new UiScreenDefinition(
                ScreenId,
                string.Empty,
                UiScreenKind.Overlay,
                typeof(TutorialOverlayController))
            {
                CachePolicy = UiCachePolicy.KeepInstance,
                BlockInputDuringTransition = false,
            };
        }
    }
}
