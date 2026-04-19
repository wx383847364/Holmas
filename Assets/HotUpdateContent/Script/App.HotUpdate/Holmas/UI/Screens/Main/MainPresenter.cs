using System.Collections.Generic;
using System.Linq;
using App.HotUpdate.Holmas.Application;
using App.HotUpdate.Holmas.Meta;
using App.HotUpdate.Holmas.Tasks.Config;
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
            HolmasAgencyBuildingDefinition promotion = GetPrimaryPromotionDefinition();
            string promotionId = promotion != null ? promotion.PromotionId : string.Empty;
            bool hasConfiguredPromotions = HasConfiguredPromotionsForCurrentStage();
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
                    ? (hasConfiguredPromotions ? "宣传已满级" : "宣传待开放")
                    : BuildPromotionButtonLabel(promotion),
                PromotionButtonEnabled = !string.IsNullOrWhiteSpace(promotionId),
                PromotionId = promotionId ?? string.Empty,
                TaskItems = BuildTaskItems(),
            };
        }

        public string GetPrimaryPromotionId()
        {
            return GetPrimaryPromotionDefinition()?.PromotionId ?? string.Empty;
        }

        private HolmasAgencyBuildingDefinition GetPrimaryPromotionDefinition()
        {
            IHolmasAgencyCatalog catalog = _context?.ServiceContainer != null
                ? _context.ServiceContainer.Get<IHolmasAgencyCatalog>()
                : null;
            if (catalog == null)
            {
                return null;
            }

            var definitions = catalog.GetPromotionsForStage(_context.CurrentAgencyStageId);
            if (definitions == null || definitions.Count == 0)
            {
                return null;
            }

            var currentState = _context.GameplayRuntime?.MetaProgressionState;
            HolmasAgencyBuildingDefinition preferred = definitions
                .FirstOrDefault(item => item != null &&
                                        !string.IsNullOrWhiteSpace(item.PromotionId) &&
                                        currentState != null &&
                                        HolmasAgencyPromotionStateKey.GetLevel(currentState, item.AgencyStageId, item.PromotionId) < item.PromotionLevelCap);

            if (preferred != null)
            {
                return preferred;
            }

            return currentState == null ? definitions[0] : null;
        }

        private bool HasConfiguredPromotionsForCurrentStage()
        {
            IHolmasAgencyCatalog catalog = _context?.ServiceContainer != null
                ? _context.ServiceContainer.Get<IHolmasAgencyCatalog>()
                : null;
            if (catalog == null)
            {
                return false;
            }

            var definitions = catalog.GetPromotionsForStage(_context.CurrentAgencyStageId);
            return definitions != null && definitions.Any(item => item != null && !string.IsNullOrWhiteSpace(item.PromotionId));
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

            return $"{BuildProgressionSummary()} | 任务 {activeTaskCount}/{unlockedCount} | 可领奖 {claimableCount}\n{BuildPromotionSummary()} | {boardSummary}";
        }

        private string BuildProgressionSummary()
        {
            if (_context == null || _context.GameplayRuntime == null)
            {
                return "Lv 1 | Exp 0/0 | Gold 0 | Stage 1";
            }

            long experience = _context.GameplayRuntime.MetaProgressionState != null
                ? _context.GameplayRuntime.MetaProgressionState.Experience
                : 0L;
            int nextLevel = _context.CurrentPlayerLevel + 1;
            string nextLevelExp = "MAX";

            IHolmasTaskCatalog taskCatalog = _context.ServiceContainer != null
                ? _context.ServiceContainer.Get<IHolmasTaskCatalog>()
                : null;
            if (taskCatalog != null &&
                taskCatalog.TryGetPlayerLevel(nextLevel, out HolmasPlayerLevelDefinition nextDefinition) &&
                nextDefinition != null)
            {
                nextLevelExp = nextDefinition.UpgradeExp.ToString();
            }

            return $"Lv {_context.CurrentPlayerLevel} | Exp {experience}/{nextLevelExp} | Gold {_context.CurrentGoldBalance} | Stage {_context.CurrentAgencyStageId}";
        }

        private string BuildPromotionSummary()
        {
            HolmasAgencyBuildingDefinition promotion = GetPrimaryPromotionDefinition();
            if (promotion == null || string.IsNullOrWhiteSpace(promotion.PromotionId))
            {
                return "宣传 暂无可升级项";
            }

            int currentLevel = _context?.GameplayRuntime?.MetaProgressionState != null
                ? HolmasAgencyPromotionStateKey.GetLevel(_context.GameplayRuntime.MetaProgressionState, promotion.AgencyStageId, promotion.PromotionId)
                : 0;
            int nextCost = GetPromotionUpgradeCost(promotion, currentLevel);
            return nextCost > 0
                ? $"宣传 {promotion.PromotionId} Lv {currentLevel}/{promotion.PromotionLevelCap} | Next Cost {nextCost} | +1 Exp"
                : $"宣传 {promotion.PromotionId} Lv {currentLevel}/{promotion.PromotionLevelCap} | 已满级";
        }

        private string BuildPromotionButtonLabel(HolmasAgencyBuildingDefinition promotion)
        {
            if (promotion == null || string.IsNullOrWhiteSpace(promotion.PromotionId))
            {
                return "宣传待开放";
            }

            int currentLevel = _context?.GameplayRuntime?.MetaProgressionState != null
                ? HolmasAgencyPromotionStateKey.GetLevel(_context.GameplayRuntime.MetaProgressionState, promotion.AgencyStageId, promotion.PromotionId)
                : 0;
            int nextCost = GetPromotionUpgradeCost(promotion, currentLevel);
            return nextCost > 0
                ? $"升级 {promotion.PromotionId} Lv {currentLevel}->{currentLevel + 1} ({nextCost} Gold)"
                : $"{promotion.PromotionId} 已满级";
        }

        private static int GetPromotionUpgradeCost(HolmasAgencyBuildingDefinition promotion, int currentLevel)
        {
            if (promotion == null ||
                promotion.PromotionUpgradeCosts == null ||
                currentLevel < 0 ||
                currentLevel >= promotion.PromotionUpgradeCosts.Length)
            {
                return 0;
            }

            return System.Math.Max(0, promotion.PromotionUpgradeCosts[currentLevel]);
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
