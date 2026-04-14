using System.Linq;
using App.HotUpdate.Holmas.Application;
using App.HotUpdate.Holmas.Meta;
using App.HotUpdate.Holmas.Tasks.Runtime;

namespace App.HotUpdate.Holmas.UI.Screens.Main
{
    public sealed class MainPresenter
    {
        private readonly HolmasApplicationContext _context;

        public MainPresenter(HolmasApplicationContext context)
        {
            _context = context;
        }

        public MainVm Build(string status = null)
        {
            string promotionId = GetPrimaryPromotionId();
            return new MainVm
            {
                LevelLabel = $"Lv {_context?.CurrentPlayerLevel ?? 1}",
                GoldLabel = $"Gold {_context?.CurrentGoldBalance ?? 0L}",
                Summary = BuildSummary(),
                Status = string.IsNullOrWhiteSpace(status) ? "Main screen ready." : status,
                StartButtonLabel = "开始找猫",
                StartButtonEnabled = _context != null && _context.GameplayRuntime != null,
                PromotionButtonLabel = string.IsNullOrWhiteSpace(promotionId)
                    ? "宣传待开放"
                    : $"升级 {promotionId}",
                PromotionButtonEnabled = !string.IsNullOrWhiteSpace(promotionId),
                PromotionId = promotionId ?? string.Empty,
            };
        }

        public string GetPrimaryPromotionId()
        {
            IHolmasAgencyCatalog catalog = _context?.ServiceContainer != null
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

            var currentState = _context.GameplayRuntime?.MetaProgressionState;
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
                return "Holmas gameplay runtime unavailable.";
            }

            HolmasTaskBarState taskBar = _context.GameplayRuntime.TaskBarState;
            string taskSummary;
            if (taskBar == null || taskBar.Tasks == null || taskBar.Tasks.Count == 0)
            {
                taskSummary = "当前没有活跃任务";
            }
            else
            {
                HolmasTaskRuntimeInstance firstTask = taskBar.Tasks.FirstOrDefault(item => item != null && item.Task != null);
                taskSummary = firstTask == null
                    ? $"任务槽 {taskBar.Tasks.Count}/{taskBar.TotalSlots}"
                    : $"任务 {firstTask.Task.CatId} {firstTask.Task.CurrentCount}/{firstTask.Task.TargetCount}";
            }

            string boardSummary = _context.GameplayRuntime.CurrentBoardRuntime == null
                ? "未进入棋盘"
                : $"棋盘 {_context.GameplayRuntime.CurrentBoardRuntime.Rows}x{_context.GameplayRuntime.CurrentBoardRuntime.Cols}";

            return $"{taskSummary}\n{boardSummary}";
        }
    }
}
