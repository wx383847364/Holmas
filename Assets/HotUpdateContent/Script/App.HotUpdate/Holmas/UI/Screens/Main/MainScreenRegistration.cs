using App.HotUpdate.Holmas.UI.Core;
using App.HotUpdate.Holmas.UI.Generated;

namespace App.HotUpdate.Holmas.UI.Screens.Main
{
    public static class MainScreenRegistration
    {
        public const string ScreenId = "main.page";

        public static UiScreenDefinition CreateDefinition()
        {
            UiRuntimeScreenDescriptor descriptor = MainGeneratedBindings.Descriptor;
            return new UiScreenDefinition(
                ScreenId,
                descriptor.PrefabAssetPath,
                UiScreenKind.Page,
                typeof(MainPageController))
            {
                CachePolicy = UiCachePolicy.KeepInstance,
                BlockInputDuringTransition = true,
                PreloadOnBootstrap = false,
                BindingManifest = descriptor.BindingManifest,
            };
        }
    }
}
