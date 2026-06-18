using App.HotUpdate.Holmas.UI.Core;
using App.HotUpdate.Holmas.UI.Generated;

namespace App.HotUpdate.Holmas.UI.Screens.Main
{
    public static class MainScreenRegistration
    {
        public const string ScreenId = "main.page";

        public static UiScreenDefinition CreateDefinition()
        {
            var descriptor = MainGeneratedBindings.Descriptor;
            return new UiScreenDefinition(
                ScreenId,
                descriptor.PrefabLocation,
                UiScreenKind.Page,
                typeof(MainPageController))
            {
                CachePolicy = UiCachePolicy.KeepInstance,
                BlockInputDuringTransition = true,
                PreloadOnBootstrap = true,
                BindingManifest = descriptor.BindingManifest,
            };
        }
    }
}
