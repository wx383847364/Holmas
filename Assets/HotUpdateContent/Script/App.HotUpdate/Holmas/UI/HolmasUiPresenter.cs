using System;
using System.Collections.Generic;
using System.Text;
using App.HotUpdate.Holmas.Application;
using App.HotUpdate.Holmas.Board;
using App.HotUpdate.Holmas.Meta;
using App.HotUpdate.Holmas.Tasks.Runtime;

namespace App.HotUpdate.Holmas.UI
{
    public sealed class HolmasUiScreenViewModel
    {
        public string SummaryText = string.Empty;
        public string TaskSummaryText = string.Empty;
        public string PromotionSummaryText = string.Empty;
        public string BoardSummaryText = string.Empty;
        public List<HolmasUiTaskSlotViewModel> TaskSlots = new List<HolmasUiTaskSlotViewModel>();
    }

    public sealed class HolmasUiTaskSlotViewModel
    {
        public int SlotIndex;
        public string Title = string.Empty;
        public string ActionLabel = string.Empty;
        public bool ActionEnabled;
    }

    public sealed class HolmasUiPresenter
    {
        private readonly HolmasApplicationContext _context;
        private readonly IHolmasAgencyCatalog _agencyCatalog;

        public HolmasUiPresenter(HolmasApplicationContext context, IHolmasAgencyCatalog agencyCatalog)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _agencyCatalog = agencyCatalog;
        }

        public HolmasUiScreenViewModel Build()
        {
            var viewModel = new HolmasUiScreenViewModel
            {
                SummaryText = BuildSummaryText(),
                TaskSummaryText = BuildTaskSummaryText(),
                PromotionSummaryText = BuildPromotionSummaryText(),
                BoardSummaryText = BuildBoardSummaryText(),
                TaskSlots = BuildTaskSlots(),
            };

            return viewModel;
        }

        public string GetFirstActionablePromotionId()
        {
            if (_agencyCatalog == null || _context.GameplayRuntime == null)
            {
                return string.Empty;
            }

            IReadOnlyList<HolmasAgencyBuildingDefinition> promotions = _agencyCatalog.GetPromotionsForStage(_context.CurrentAgencyStageId);
            if (promotions == null || promotions.Count == 0)
            {
                return string.Empty;
            }

            for (int i = 0; i < promotions.Count; i++)
            {
                HolmasAgencyBuildingDefinition definition = promotions[i];
                if (definition == null || string.IsNullOrWhiteSpace(definition.PromotionId) || definition.PromotionLevelCap <= 0)
                {
                    continue;
                }

                int currentLevel = _context.GameplayRuntime.MetaProgressionState.GetPromotionLevel(definition.PromotionId);
                if (currentLevel < definition.PromotionLevelCap)
                {
                    return definition.PromotionId;
                }
            }

            return string.Empty;
        }

        private string BuildSummaryText()
        {
            if (_context.GameplayRuntime == null)
            {
                return "Holmas runtime unavailable";
            }

            HolmasMetaProgressionState state = _context.GameplayRuntime.MetaProgressionState;
            return $"Lv {_context.CurrentPlayerLevel} | Stage {_context.CurrentAgencyStageId} | Gold {_context.CurrentGoldBalance} | Exp {state.Experience} | Maps {state.CompletedMapCount} | Claims {state.ClaimedTaskCount}";
        }

        private string BuildTaskSummaryText()
        {
            if (_context.GameplayRuntime == null)
            {
                return "任务栏未初始化";
            }

            HolmasTaskBarState taskBarState = _context.GameplayRuntime.TaskBarState;
            int activeTasks = taskBarState.Tasks != null ? taskBarState.Tasks.Count : 0;
            int claimableTasks = taskBarState.GetClaimableTasks().Count;
            return $"任务栏 {activeTasks}/{taskBarState.TotalSlots} | 可领奖 {claimableTasks}";
        }

        private string BuildPromotionSummaryText()
        {
            if (_context.GameplayRuntime == null)
            {
                return "宣传配置未初始化";
            }

            if (_agencyCatalog == null)
            {
                return "宣传配置仓库未注册";
            }

            IReadOnlyList<HolmasAgencyBuildingDefinition> promotions = _agencyCatalog.GetPromotionsForStage(_context.CurrentAgencyStageId);
            if (promotions == null || promotions.Count == 0)
            {
                return $"Stage {_context.CurrentAgencyStageId} 暂无宣传配置";
            }

            var builder = new StringBuilder();
            builder.Append("当前阶段宣传: ");
            bool appended = false;
            for (int i = 0; i < promotions.Count; i++)
            {
                HolmasAgencyBuildingDefinition definition = promotions[i];
                if (definition == null || string.IsNullOrWhiteSpace(definition.PromotionId))
                {
                    continue;
                }

                if (appended)
                {
                    builder.Append(" | ");
                }

                int level = _context.GameplayRuntime.MetaProgressionState.GetPromotionLevel(definition.PromotionId);
                builder.Append(definition.PromotionId);
                builder.Append(' ');
                builder.Append(level);
                builder.Append('/');
                builder.Append(Math.Max(0, definition.PromotionLevelCap));
                appended = true;
            }

            return appended ? builder.ToString() : $"Stage {_context.CurrentAgencyStageId} 暂无宣传配置";
        }

        private string BuildBoardSummaryText()
        {
            if (_context.GameplayRuntime == null)
            {
                return "棋盘未初始化";
            }

            BoardRuntime boardRuntime = _context.GameplayRuntime.CurrentBoardRuntime;
            if (boardRuntime == null)
            {
                return "未开图";
            }

            return $"Map {_context.GameplayRuntime.CurrentLevelSnapshot?.MapId ?? "-"} | Cats {boardRuntime.FoundCatCount}/{boardRuntime.TotalCatCount} | Completed {boardRuntime.Completed}";
        }

        private List<HolmasUiTaskSlotViewModel> BuildTaskSlots()
        {
            var result = new List<HolmasUiTaskSlotViewModel>();
            if (_context.GameplayRuntime == null)
            {
                return result;
            }

            HolmasTaskBarState taskBarState = _context.GameplayRuntime.TaskBarState;
            if (taskBarState?.Slots == null)
            {
                return result;
            }

            for (int i = 0; i < taskBarState.Slots.Count; i++)
            {
                App.Shared.Holmas.RuntimeData.TaskSlotState slot = taskBarState.Slots[i];
                HolmasTaskRuntimeInstance task = taskBarState.GetTaskBySlot(i);
                var viewModel = new HolmasUiTaskSlotViewModel
                {
                    SlotIndex = i,
                    Title = BuildTaskSlotTitle(slot, task),
                    ActionLabel = BuildTaskSlotActionLabel(slot, task),
                    ActionEnabled = IsTaskSlotActionEnabled(slot, task),
                };
                result.Add(viewModel);
            }

            return result;
        }

        private static string BuildTaskSlotTitle(App.Shared.Holmas.RuntimeData.TaskSlotState slot, HolmasTaskRuntimeInstance task)
        {
            if (slot == null)
            {
                return "槽位缺失";
            }

            if (!slot.IsUnlocked)
            {
                return $"槽位 {slot.SlotIndex + 1}: 未解锁";
            }

            if (task == null || task.Task == null)
            {
                return $"槽位 {slot.SlotIndex + 1}: 空";
            }

            return $"槽位 {slot.SlotIndex + 1}: {task.Task.CatId} {task.Task.CurrentCount}/{task.Task.TargetCount} 奖励 {task.Task.Reward}";
        }

        private static string BuildTaskSlotActionLabel(App.Shared.Holmas.RuntimeData.TaskSlotState slot, HolmasTaskRuntimeInstance task)
        {
            if (slot == null)
            {
                return "不可用";
            }

            if (!slot.IsUnlocked)
            {
                return "广告解锁";
            }

            if (task == null || task.Task == null)
            {
                return "补任务";
            }

            if (task.CanClaimReward)
            {
                return "领取";
            }

            return "等待完成";
        }

        private static bool IsTaskSlotActionEnabled(App.Shared.Holmas.RuntimeData.TaskSlotState slot, HolmasTaskRuntimeInstance task)
        {
            if (slot == null)
            {
                return false;
            }

            if (!slot.IsUnlocked)
            {
                return true;
            }

            if (task == null || task.Task == null)
            {
                return true;
            }

            return task.CanClaimReward;
        }
    }
}
