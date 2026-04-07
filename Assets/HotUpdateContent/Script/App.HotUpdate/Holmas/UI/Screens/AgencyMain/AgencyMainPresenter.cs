using App.HotUpdate.Holmas.Application;
using App.HotUpdate.Holmas.Board;

namespace App.HotUpdate.Holmas.UI.Screens.AgencyMain
{
    /// <summary>
    /// AgencyMain 的最小 Presenter。
    /// 只消费现有运行时状态，不承载玩法规则。
    /// </summary>
    public sealed class AgencyMainPresenter
    {
        private readonly HolmasApplicationContext _context;

        public AgencyMainPresenter(HolmasApplicationContext context)
        {
            _context = context;
        }

        public AgencyMainVm Build(string status = null)
        {
            var viewModel = new AgencyMainVm
            {
                Title = "Agency Main",
                Summary = BuildSummary(),
                TaskSummary = BuildTaskSummary(),
                BoardSummary = BuildBoardSummary(),
                Status = string.IsNullOrWhiteSpace(status)
                    ? "v2 UI skeleton ready. TODO: replace placeholder view with generated prefab + binding manifest."
                    : status,
                PrimaryActionLabel = "Open Level",
                PrimaryActionEnabled = _context != null && _context.GameplayRuntime != null,
                IsPlaceholderView = true,
            };

            return viewModel;
        }

        private string BuildSummary()
        {
            if (_context == null)
            {
                return "HolmasApplicationContext unavailable";
            }

            return $"Lv {_context.CurrentPlayerLevel} | Stage {_context.CurrentAgencyStageId} | Gold {_context.CurrentGoldBalance}";
        }

        private string BuildTaskSummary()
        {
            if (_context == null || _context.GameplayRuntime == null || _context.GameplayRuntime.TaskBarState == null)
            {
                return "Task bar unavailable";
            }

            return $"Task Slots {_context.GameplayRuntime.TaskBarState.Tasks.Count}/{_context.GameplayRuntime.TaskBarState.TotalSlots}";
        }

        private string BuildBoardSummary()
        {
            if (_context == null || _context.GameplayRuntime == null)
            {
                return "Board unavailable";
            }

            BoardRuntime board = _context.GameplayRuntime.CurrentBoardRuntime;
            if (board == null)
            {
                return "No active board";
            }

            return $"Board {board.Rows}x{board.Cols} | Cells {board.CellCount} | Completed {board.Completed}";
        }
    }
}
