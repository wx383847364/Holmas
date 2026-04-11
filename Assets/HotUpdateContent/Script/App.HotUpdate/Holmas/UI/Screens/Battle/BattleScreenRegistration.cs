using App.HotUpdate.Holmas.UI.Core;
using App.HotUpdate.Holmas.UI.Generated;

namespace App.HotUpdate.Holmas.UI.Screens.Battle
{
    public static class BattleScreenRegistration
    {
        public const string ScreenId = "battle.main";

        public static UiScreenDefinition CreateDefinition()
        {
            UiRuntimeScreenDescriptor descriptor = BattleGeneratedBindings.Descriptor;
            return new UiScreenDefinition(
                ScreenId,
                descriptor.PrefabAssetPath,
                UiScreenKind.Page,
                typeof(BattlePageController))
            {
                CachePolicy = UiCachePolicy.KeepInstance,
                BlockInputDuringTransition = true,
                BindingManifest = descriptor.BindingManifest,
            };
        }
    }
}
