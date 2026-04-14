using App.HotUpdate.Holmas.UI.Core;
using App.HotUpdate.Holmas.UI.Generated;

namespace App.HotUpdate.Holmas.UI.Screens.Loading
{
    public static class LoadingScreenRegistration
    {
        public const string ScreenId = "loading.overlay";

        public static UiScreenDefinition CreateDefinition()
        {
            UiRuntimeScreenDescriptor descriptor = LoadingGeneratedBindings.Descriptor;
            return new UiScreenDefinition(
                ScreenId,
                descriptor.PrefabAssetPath,
                UiScreenKind.Overlay,
                typeof(LoadingOverlayController))
            {
                CachePolicy = UiCachePolicy.KeepInstance,
                BlockInputDuringTransition = true,
                BindingManifest = descriptor.BindingManifest,
            };
        }
    }
}
