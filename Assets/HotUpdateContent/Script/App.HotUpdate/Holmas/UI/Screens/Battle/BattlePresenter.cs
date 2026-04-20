using System;
using System.Collections.Generic;
using System.Linq;
using App.HotUpdate.Holmas.Application;
using App.HotUpdate.Holmas.Board;
using App.HotUpdate.Holmas.Tasks.Runtime;

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
                EnergyLabel = _context?.EnergyLabel ?? "50/50",
                Summary = BuildSummary(board),
                Status = string.IsNullOrWhiteSpace(status)
                    ? "点击格子翻开，右键插旗。"
                    : status,
                AddEnergyButtonLabel = "+5体力",
                AddEnergyButtonEnabled = _context != null && _context.GameplayRuntime != null,
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
            string terrainName = _context?.GameplayRuntime?.CurrentLevelSnapshot != null
                ? GetTerrainFileName(_context.GameplayRuntime.CurrentLevelSnapshot.TerrainPath)
                : "unknown";
            int hiddenCatCount = Math.Max(0, board.TotalCatCount - board.FoundCatCount);
            return $"Map {mapId} | Terrain {terrainName} | Board {board.Rows}x{board.Cols}\nBoard Cats {board.FoundCatCount}/{board.TotalCatCount} | Hidden {hiddenCatCount} | {BuildTaskProgressSummary()}";
        }

        private static string GetTerrainFileName(string terrainPath)
        {
            if (string.IsNullOrWhiteSpace(terrainPath))
            {
                return "unknown";
            }

            int slashIndex = terrainPath.LastIndexOf('/');
            int backslashIndex = terrainPath.LastIndexOf('\\');
            int index = Math.Max(slashIndex, backslashIndex);
            return index >= 0 && index + 1 < terrainPath.Length
                ? terrainPath.Substring(index + 1)
                : terrainPath;
        }

        private string BuildTaskProgressSummary()
        {
            HolmasTaskBarState taskBar = _context?.GameplayRuntime?.TaskBarState;
            if (taskBar == null || taskBar.Tasks == null || taskBar.Tasks.Count == 0)
            {
                return "Task none";
            }

            List<HolmasTaskRuntimeInstance> activeTasks = taskBar.Tasks
                .Where(item => item != null && item.Task != null && !item.IsRewardClaimed)
                .OrderBy(item => item.Task.SlotIndex)
                .ToList();
            if (activeTasks.Count == 0)
            {
                return "Task none";
            }

            if (activeTasks.Count == 1)
            {
                return BuildSingleTaskProgress("Task", activeTasks[0], includeCatId: false);
            }

            return "Tasks " + string.Join("; ", activeTasks.Select(item => BuildTaskProgressEntry(item)));
        }

        private static string BuildTaskProgressEntry(HolmasTaskRuntimeInstance task)
        {
            if (task == null || task.Task == null)
            {
                return "unknown 0/0";
            }

            string catId = string.IsNullOrWhiteSpace(task.Task.CatId) ? "unknown" : task.Task.CatId;
            return $"{catId} {Math.Max(0, task.Task.CurrentCount)}/{Math.Max(0, task.Task.TargetCount)}";
        }

        private static string BuildSingleTaskProgress(string label, HolmasTaskRuntimeInstance task, bool includeCatId)
        {
            if (task == null || task.Task == null)
            {
                return $"{label} 0/0";
            }

            string prefix = includeCatId && !string.IsNullOrWhiteSpace(task.Task.CatId)
                ? $"{label} {task.Task.CatId}"
                : label;
            return $"{prefix} {Math.Max(0, task.Task.CurrentCount)}/{Math.Max(0, task.Task.TargetCount)}";
        }
    }
}
