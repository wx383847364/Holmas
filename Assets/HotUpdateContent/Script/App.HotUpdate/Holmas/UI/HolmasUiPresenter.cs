using App.HotUpdate.Holmas.Application;

namespace App.HotUpdate.Holmas.UI
{
    /// <summary>
    /// 旧 HolmasUiRoot 的过渡态 ViewModel。
    /// v2 正式页面已迁移到 Screens/*，这里仅保留兼容壳，避免激进删除。
    /// </summary>
    public sealed class HolmasUiScreenViewModel
    {
        public string SummaryText = string.Empty;
        public string StatusText = string.Empty;
    }

    /// <summary>
    /// 旧类型名继续保留，但不再承载正式页面逻辑。
    /// </summary>
    public sealed class HolmasUiTaskSlotViewModel
    {
        public string Title = string.Empty;
    }

    /// <summary>
    /// 过渡态 Presenter，仅提供最小摘要。
    /// 正式结构请使用 Screens/AgencyMain。
    /// </summary>
    public sealed class HolmasUiPresenter
    {
        private readonly HolmasApplicationContext _context;

        public HolmasUiPresenter(HolmasApplicationContext context)
        {
            _context = context;
        }

        public HolmasUiScreenViewModel Build()
        {
            return new HolmasUiScreenViewModel
            {
                SummaryText = BuildSummaryText(),
                StatusText = "HolmasUiRoot is now a transitional shell. Use UiRoot + AgencyMainPageController.",
            };
        }

        private string BuildSummaryText()
        {
            if (_context == null || _context.GameplayRuntime == null)
            {
                return "Holmas runtime unavailable";
            }

            return $"Lv {_context.CurrentPlayerLevel} | Stage {_context.CurrentAgencyStageId} | Gold {_context.CurrentGoldBalance}";
        }
    }
}
