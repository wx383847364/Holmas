using System;
using App.HotUpdate.Holmas.UI.Core;
using App.HotUpdate.Holmas.UI.Screens.AgencyMain;
using App.HotUpdate.Holmas.UI.Screens.Battle;
using App.HotUpdate.Holmas.UI.Screens.Loading;
using App.HotUpdate.Holmas.UI.Screens.Main;

namespace App.HotUpdate.Holmas.UI
{
    /// <summary>
    /// 运行时只保留一个很薄的聚合注册入口。
    /// 具体页面定义下沉到各自目录，避免形成全局冲突热点。
    /// </summary>
    public static class HolmasUiScreenCatalog
    {
        public static string DefaultStartupScreenId => MainScreenRegistration.ScreenId;

        public static void RegisterAll(UiScreenService screenService)
        {
            if (screenService == null)
            {
                throw new ArgumentNullException(nameof(screenService));
            }

            screenService.RegisterDefinition(MainScreenRegistration.CreateDefinition());
            screenService.RegisterDefinition(BattleScreenRegistration.CreateDefinition());
            screenService.RegisterDefinition(LoadingScreenRegistration.CreateDefinition());
            screenService.RegisterDefinition(AgencyMainScreenRegistration.CreateDefinition());
        }
    }
}
