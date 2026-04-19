using System.Collections.Generic;
using System.Linq;
using App.HotUpdate.Holmas.Application;
using App.HotUpdate.Holmas.Meta;
using App.HotUpdate.Holmas.Tasks.Runtime;
using App.Shared.Holmas.RuntimeData;

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
            bool hasUncompletedLevel = _context != null &&
                                       _context.GameplayRuntime != null &&
                                       _context.GameplayRuntime.HasActiveUncompletedLevel;
            return new MainVm
            {
                LevelLabel = $"Lv {_context?.CurrentPlayerLevel ?? 1}",
                GoldLabel = $"Gold {_context?.CurrentGoldBalance ?? 0L}",
                Summary = BuildSummary(),
                Status = string.IsNullOrWhiteSpace(status) ? "主界面已就绪。" : status,
                StartButtonLabel = hasUncompletedLevel ? "继续找猫" : "开始找猫",
                StartButtonEnabled = _context != null && _context.GameplayRuntime != null,
                PromotionButtonLabel = string.IsNullOrWhiteSpace(promotionId)
                    ? "宣传待开放"
                    : $"升级 {promotionId}",
                PromotionButtonEnabled = !string.IsNullOrWhiteSpace(promotionId),
                PromotionId = promotionId ?? string.Empty,
                TaskItems = BuildTaskItems(),
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
            int unlockedCount = taskBar != null && taskBar.Slots != null
                ? taskBar.Slots.Count(item => item != null && item.IsUnlocked)
                : 0;
            int activeTaskCount = taskBar != null && taskBar.Tasks != null
                ? taskBar.Tasks.Count(item => item != null && item.Task != null)
                : 0;
            int claimableCount = taskBar != null ? taskBar.GetClaimableTasks().Count : 0;
            string boardSummary = _context.GameplayRuntime.CurrentBoardRuntime == null
                ? "未进入棋盘"
                : $"棋盘 {_context.GameplayRuntime.CurrentBoardRuntime.Rows}x{_context.GameplayRuntime.CurrentBoardRuntime.Cols}";

            return $"Stage {_context.CurrentAgencyStageId} | 任务 {activeTaskCount}/{unlockedCount} | 可领奖 {claimableCount}\n{boardSummary}";
        }

        private MainTaskItemVm[] BuildTaskItems()
        {
            if (_context == null || _context.GameplayRuntime == null || _context.GameplayRuntime.TaskBarState == null)
            {
                return System.Array.Empty<MainTaskItemVm>();
            }

            HolmasTaskBarState taskBar = _context.GameplayRuntime.TaskBarState;
            if (taskBar.Slots == null || taskBar.Slots.Count == 0)
            {
                return System.Array.Empty<MainTaskItemVm>();
            }

            int count = System.Math.Min(taskBar.Slots.Count, 4);
            var items = new List<MainTaskItemVm>(count);
            for (int i = 0; i < count; i++)
            {
                items.Add(BuildTaskItem(taskBar.Slots[i], taskBar.GetTaskBySlot(i), i));
            }

            return items.ToArray();
        }

        private static MainTaskItemVm BuildTaskItem(TaskSlotState slot, HolmasTaskRuntimeInstance runtimeTask, int slotIndex)
        {
            var item = new MainTaskItemVm
            {
                SlotIndex = slotIndex,
                Title = $"任务槽 {slotIndex + 1}",
                ButtonEnabled = true,
            };

            if (slot == null || !slot.IsUnlocked)
            {
                item.Progress = "未解锁";
                item.Reward = "广告位后续接入";
                item.ProgressNormalized = 0f;
                item.IsLocked = true;
                item.ButtonEnabled = false;
                return item;
            }

            if (runtimeTask == null || runtimeTask.Task == null)
            {
                item.Progress = "空槽";
                item.Reward = "点击后补任务";
                item.ProgressNormalized = 0f;
                item.IsEmpty = true;
                return item;
            }

            item.Title = runtimeTask.Task.CatId;
            item.Progress = $"{runtimeTask.Task.CurrentCount}/{runtimeTask.Task.TargetCount}";
            item.Reward = runtimeTask.CanClaimReward
                ? $"奖励 {runtimeTask.Task.Reward} Gold - 点击领取"
                : $"奖励 {runtimeTask.Task.Reward} Gold";
            item.ProgressNormalized = runtimeTask.Task.TargetCount > 0
                ? UnityEngine.Mathf.Clamp01((float)runtimeTask.Task.CurrentCount / runtimeTask.Task.TargetCount)
                : 0f;
            item.IsClaimable = runtimeTask.CanClaimReward;
            return item;
        }
    }
}
