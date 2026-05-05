using App.HotUpdate.Holmas.UI.Core;
using App.HotUpdate.Holmas.UI.Generated;

namespace App.HotUpdate.Holmas.UI.Screens.Leaderboard
{
    public static class LeaderboardScreenRegistration
    {
        public const string ScreenId = "leaderboard.page";

        public static UiScreenDefinition CreateDefinition()
        {
            UiRuntimeScreenDescriptor descriptor = LeaderboardGeneratedBindings.Descriptor;
            return new UiScreenDefinition(
                ScreenId,
                descriptor.PrefabAssetPath,
                UiScreenKind.Page,
                typeof(LeaderboardPageController))
            {
                CachePolicy = UiCachePolicy.KeepInstance,
                BlockInputDuringTransition = true,
                BindingManifest = descriptor.BindingManifest,
            };
        }
    }
}
