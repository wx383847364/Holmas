using App.HotUpdate.Holmas.UI.Core;
using App.HotUpdate.Holmas.UI.Generated;

namespace App.HotUpdate.Holmas.UI.Screens.Loading
{
    public static class LoadingScreenRegistration
    {
        public const string StartupPageScreenId = "loading.startup.page";
        public const string TransitionOverlayScreenId = "loading.transition.overlay";

        public static UiScreenDefinition CreateStartupPageDefinition()
        {
            UiRuntimeScreenDescriptor descriptor = LoadingGeneratedBindings.Descriptor;
            return new UiScreenDefinition(
                StartupPageScreenId,
                descriptor.PrefabAssetPath,
                UiScreenKind.Page,
                typeof(LoadingPageController))
            {
                CachePolicy = UiCachePolicy.DestroyOnClose,
                BlockInputDuringTransition = true,
                PreloadOnBootstrap = true,
                BindingManifest = descriptor.BindingManifest,
            };
        }

        public static UiScreenDefinition CreateTransitionOverlayDefinition()
        {
            UiRuntimeScreenDescriptor descriptor = LoadingGeneratedBindings.Descriptor;
            return new UiScreenDefinition(
                TransitionOverlayScreenId,
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
