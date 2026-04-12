using System.Linq;
using App.HotUpdate.Holmas.Application;
using App.HotUpdate.Holmas.Meta;
using App.HotUpdate.Holmas.Tasks.Runtime;

namespace App.HotUpdate.Holmas.UI.Screens.Main
{
    /// <summary>
    /// MainPage 的展示数据组装器。
    /// 它只把业务运行时状态翻译成 UI 可直接消费的文案和按钮状态。
    /// </summary>
    public sealed class MainPresenter
    {
        private readonly HolmasApplicationContext _context;

        public MainPresenter(HolmasApplicationContext context)
        {
            _context = context;
        }

        public MainVm Build(string status = null)
        {
            bool hasRuntime = _context != null && _context.GameplayRuntime != null;
            string promotionId = GetPrimaryPromotionId();
            // MainVm 是纯展示数据，不直接暴露底层运行时对象给 View。
            return new MainVm
            {
                LevelLabel = $"Lv {_context?.CurrentPlayerLevel ?? 1}",
                GoldLabel = $"Gold {_context?.CurrentGoldBalance ?? 0L}",
                Summary = BuildSummary(),
                Status = string.IsNullOrWhiteSpace(status) ? "主界面已就绪。" : status,
                StartButtonLabel = "开始找猫",
                StartButtonEnabled = hasRuntime,
                PromotionButtonLabel = string.IsNullOrWhiteSpace(promotionId)
                    ? "宣传待开放"
                    : $"升级 {promotionId}",
                PromotionButtonEnabled = hasRuntime && !string.IsNullOrWhiteSpace(promotionId),
                PromotionId = promotionId ?? string.Empty,
            };
        }

        public string GetPrimaryPromotionId()
        {
            // 主界面只挑一个“当前最值得升级”的宣传项给主按钮展示。
            if (_context == null || _context.GameplayRuntime == null)
            {
                return string.Empty;
            }

            IHolmasAgencyCatalog catalog = _context.ServiceContainer != null
                ? _context.ServiceContainer.Get<IHolmasAgencyCatalog>()
                : null;
            if (catalog == null)
            {
                return string.Empty;
            }

            var definitions = catalog.GetPromotionsForStage(_context.CurrentAgencyStageId);
            if (definitions == null || definitions.Count == 0)
            {
                return string.Empty;
            }

            var currentState = _context.GameplayRuntime.MetaProgressionState;
            HolmasAgencyBuildingDefinition preferred = definitions
                .FirstOrDefault(item => item != null &&
                                        !string.IsNullOrWhiteSpace(item.PromotionId) &&
                                        currentState != null &&
                                        currentState.GetPromotionLevel(item.PromotionId) < item.PromotionLevelCap);

            if (preferred != null)
            {
                return preferred.PromotionId;
            }

            return definitions[0] != null ? definitions[0].PromotionId ?? string.Empty : string.Empty;
        }

        private string BuildSummary()
        {
            if (_context == null || _context.GameplayRuntime == null)
            {
                return "当前玩法运行时不可用。";
            }

            // Summary 文案分两段：
            // 上半段看任务栏状态，下半段看当前是否已经进入棋盘。
            string taskSummary;
            if (_context.GameplayRuntime.TaskBarState == null ||
                _context.GameplayRuntime.TaskBarState.Tasks == null ||
                _context.GameplayRuntime.TaskBarState.Tasks.Count == 0)
            {
                taskSummary = "当前没有活跃任务";
            }
            else
            {
                HolmasTaskRuntimeInstance firstTask = _context.GameplayRuntime.TaskBarState.Tasks
                    .FirstOrDefault(item => item != null && item.Task != null);
                taskSummary = firstTask == null
                    ? $"任务槽 {_context.GameplayRuntime.TaskBarState.Tasks.Count}/{_context.GameplayRuntime.TaskBarState.TotalSlots}"
                    : $"任务 {firstTask.Task.CatId} {firstTask.Task.CurrentCount}/{firstTask.Task.TargetCount}";
            }

            string boardSummary = _context.GameplayRuntime.CurrentBoardRuntime == null
                ? "未进入棋盘"
                : $"棋盘 {_context.GameplayRuntime.CurrentBoardRuntime.Rows}x{_context.GameplayRuntime.CurrentBoardRuntime.Cols}";
            return taskSummary + "\n" + boardSummary;
        }
    }
}
