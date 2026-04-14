using App.HotUpdate.Holmas.Application;
using App.HotUpdate.Holmas.Board;

namespace App.HotUpdate.Holmas.UI.Screens.Battle
{
    public sealed class BattlePresenter
    {
        private readonly HolmasApplicationContext _context;

        public BattlePresenter(HolmasApplicationContext context)
        {
            _context = context;
        }

        public BattleVm Build(string status = null)
        {
            BoardRuntime board = _context?.GameplayRuntime?.CurrentBoardRuntime;
            return new BattleVm
            {
                LevelLabel = $"Lv {_context?.CurrentPlayerLevel ?? 1}",
                GoldLabel = $"Gold {_context?.CurrentGoldBalance ?? 0L}",
                Summary = BuildSummary(board),
                Status = string.IsNullOrWhiteSpace(status)
                    ? "点击格子翻开，右键插旗。"
                    : status,
                Rows = board != null ? board.Rows : 0,
                Cols = board != null ? board.Cols : 0,
                Cells = board != null ? board.GetAllCellStates() : new BoardCellState[0],
            };
        }

        private string BuildSummary(BoardRuntime board)
        {
            if (board == null)
            {
                return "当前还没有活动棋盘。";
            }

            string mapId = _context?.GameplayRuntime?.CurrentLevelSnapshot != null
                ? _context.GameplayRuntime.CurrentLevelSnapshot.MapId
                : "unknown";
            return $"Map {mapId} | Cats {board.FoundCatCount}/{board.TotalCatCount} | Board {board.Rows}x{board.Cols}";
        }
    }
}
