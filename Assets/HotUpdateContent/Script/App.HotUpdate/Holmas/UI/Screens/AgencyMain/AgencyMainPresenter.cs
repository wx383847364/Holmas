using System.Collections.Generic;
using System.Linq;
using App.HotUpdate.Holmas.Application;
using App.HotUpdate.Holmas.Board;
using App.HotUpdate.Holmas.Tasks.Runtime;
using App.Shared.Holmas.RuntimeData;

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

        public AgencyMainVm Build(string status = null, bool isFallbackView = false)
        {
            var viewModel = new AgencyMainVm
            {
                Title = "侦探社",
                Summary = BuildSummary(),
                TaskSummary = BuildTaskSummary(),
                BoardSummary = BuildBoardSummary(),
                Status = string.IsNullOrWhiteSpace(status)
                    ? (isFallbackView
                        ? "侦探社页正在使用兼容布局。"
                        : "侦探社页已就绪。")
                    : status,
                PrimaryActionLabel = "开始找猫",
                PrimaryActionEnabled = _context != null && _context.GameplayRuntime != null,
                IsPlaceholderView = isFallbackView,
                TaskItems = BuildTaskItems(),
            };

            return viewModel;
        }

        private string BuildSummary()
        {
            if (_context == null)
            {
                return "应用上下文不可用";
            }

            return $"Lv {_context.CurrentPlayerLevel} | Stage {_context.CurrentAgencyStageId} | Gold {_context.CurrentGoldBalance}";
        }

        private string BuildTaskSummary()
        {
            if (_context == null || _context.GameplayRuntime == null || _context.GameplayRuntime.TaskBarState == null)
            {
                return "任务栏不可用";
            }

            HolmasTaskBarState taskBarState = _context.GameplayRuntime.TaskBarState;
            int unlockedCount = taskBarState.Slots != null ? taskBarState.Slots.Count(slot => slot != null && slot.IsUnlocked) : 0;
            int activeCount = taskBarState.Tasks != null ? taskBarState.Tasks.Count(item => item != null && item.Task != null) : 0;
            int claimableCount = taskBarState.GetClaimableTasks().Count;
            return $"任务槽 {activeCount}/{unlockedCount}/{taskBarState.TotalSlots} | 可领奖 {claimableCount}";
        }

        private string BuildBoardSummary()
        {
            if (_context == null || _context.GameplayRuntime == null)
            {
                return "棋盘不可用";
            }

            BoardRuntime board = _context.GameplayRuntime.CurrentBoardRuntime;
            if (board == null)
            {
                return "当前未进入棋盘";
            }

            return $"棋盘 {board.Rows}x{board.Cols} | 格子 {board.CellCount} | 已完成 {board.Completed}";
        }

        private AgencyMainTaskItemVm[] BuildTaskItems()
        {
            if (_context == null || _context.GameplayRuntime == null || _context.GameplayRuntime.TaskBarState == null)
            {
                return System.Array.Empty<AgencyMainTaskItemVm>();
            }

            HolmasTaskBarState taskBarState = _context.GameplayRuntime.TaskBarState;
            if (taskBarState.Slots == null || taskBarState.Slots.Count == 0)
            {
                return System.Array.Empty<AgencyMainTaskItemVm>();
            }

            var items = new List<AgencyMainTaskItemVm>(taskBarState.Slots.Count);
            for (int i = 0; i < taskBarState.Slots.Count; i++)
            {
                TaskSlotState slot = taskBarState.Slots[i];
                HolmasTaskRuntimeInstance runtimeTask = taskBarState.GetTaskBySlot(i);
                items.Add(BuildTaskItem(slot, runtimeTask, i));
            }

            return items.ToArray();
        }

        private static AgencyMainTaskItemVm BuildTaskItem(TaskSlotState slot, HolmasTaskRuntimeInstance runtimeTask, int slotIndex)
        {
            AgencyMainTaskItemVm item = new AgencyMainTaskItemVm
            {
                SlotIndex = slotIndex,
                Title = $"任务槽 {slotIndex + 1}",
                ClaimButtonLabel = "领取",
            };

            if (slot == null || !slot.IsUnlocked)
            {
                item.Progress = "未解锁";
                item.Reward = "观看广告后开启";
                item.IsLocked = true;
                item.ClaimButtonEnabled = false;
                item.ClaimButtonLabel = "未开启";
                return item;
            }

            if (runtimeTask == null || runtimeTask.Task == null)
            {
                item.Progress = "当前空槽";
                item.Reward = "等待补任务";
                item.ClaimButtonEnabled = false;
                item.ClaimButtonLabel = "暂无任务";
                return item;
            }

            item.Title = $"任务槽 {slotIndex + 1} | 猫 {runtimeTask.Task.CatId}";
            item.Progress = runtimeTask.CanClaimReward
                ? $"已完成 {runtimeTask.Task.CurrentCount}/{runtimeTask.Task.TargetCount}"
                : $"进度 {runtimeTask.Task.CurrentCount}/{runtimeTask.Task.TargetCount}";
            item.Reward = $"奖励 {runtimeTask.Task.Reward} Gold";
            item.ClaimButtonEnabled = runtimeTask.CanClaimReward;
            item.ClaimButtonLabel = runtimeTask.CanClaimReward ? "领取奖励" : "进行中";
            return item;
        }
    }
}
