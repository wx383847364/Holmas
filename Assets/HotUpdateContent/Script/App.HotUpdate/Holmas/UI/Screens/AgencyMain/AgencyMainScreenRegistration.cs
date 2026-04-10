using App.HotUpdate.Holmas.UI.Core;
using App.HotUpdate.Holmas.UI.Generated;

namespace App.HotUpdate.Holmas.UI.Screens.AgencyMain
{
    /// <summary>
    /// AgencyMain 页面自己的运行时注册入口。
    /// 页面级元数据与业务 controller 保持同目录收口，避免全局大表。
    /// </summary>
    public static class AgencyMainScreenRegistration
    {
        public const string ScreenId = "agency.main";

        public static UiScreenDefinition CreateDefinition()
        {
            UiRuntimeScreenDescriptor descriptor = AgencyMainGeneratedBindings.Descriptor;
            return new UiScreenDefinition(
                ScreenId,
                descriptor.PrefabAssetPath,
                UiScreenKind.Page,
                typeof(AgencyMainPageController))
            {
                CachePolicy = UiCachePolicy.KeepInstance,
                BlockInputDuringTransition = true,
                PreloadOnBootstrap = true,
                BindingManifest = descriptor.BindingManifest,
            };
        }
    }
}
