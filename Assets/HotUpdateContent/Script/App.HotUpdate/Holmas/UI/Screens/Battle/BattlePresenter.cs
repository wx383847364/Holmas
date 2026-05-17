using System;
using System.Collections.Generic;
using System.Linq;
using App.HotUpdate.Holmas.Application;
using App.HotUpdate.Holmas.Meta;

namespace App.HotUpdate.Holmas.UI.Screens.Battle
{
    public sealed class BattlePresenter
    {
        public const int VisibleStageCount = 5;
        public const int VisibleStageBarCount = VisibleStageCount - 1;

        private readonly HolmasApplicationContext _context;

        public BattlePresenter(HolmasApplicationContext context)
        {
            _context = context;
        }

        public BattleVm Build(int selectedStageId, string status = null)
        {
            IHolmasAgencyCatalog catalog = GetCatalog();
            IReadOnlyList<HolmasAgencyStageDefinition> stages = catalog != null
                ? catalog.GetStagesInOrder()
                : Array.Empty<HolmasAgencyStageDefinition>();
            int activeBuildStageId = Math.Max(1, _context?.CurrentAgencyStageId ?? 1);
            int normalizedViewedStageId = NormalizeSelectedStageId(stages, selectedStageId, activeBuildStageId);
            BattleStageVm[] stageVms = BuildStages(stages, normalizedViewedStageId, activeBuildStageId);
            BattleStageBarVm[] stageBars = BuildStageBars(stageVms, activeBuildStageId);
            BattlePromotionSlotVm[] promotionSlots = BuildPromotionSlots(catalog, activeBuildStageId, activeBuildStageId);
            BattlePromotionSlotVm nextPromotionSlot = promotionSlots.FirstOrDefault(item => item != null && item.CanBuild);

            return new BattleVm
            {
                LevelLabel = $"Lv {_context?.CurrentPlayerLevel ?? 1}",
                GoldLabel = $"Gold {_context?.CurrentGoldBalance ?? 0L}",
                EnergyLabel = _context?.EnergyLabel ?? "50/50",
                Summary = BuildSummary(catalog, normalizedViewedStageId, activeBuildStageId),
                Status = string.IsNullOrWhiteSpace(status) ? "选择城市阶段，推进当前宣传建设。" : status,
                BuildButtonLabel = BuildPromotionSummaryLabel(catalog, activeBuildStageId, activeBuildStageId, nextPromotionSlot),
                BuildButtonEnabled = nextPromotionSlot != null,
                SelectedStageId = normalizedViewedStageId,
                PromotionSlots = promotionSlots,
                Stages = stageVms,
                StageBars = stageBars,
            };
        }

        public string GetPromotionIdForSlot(int promotionSlotIndex)
        {
            IHolmasAgencyCatalog catalog = GetCatalog();
            int activeBuildStageId = Math.Max(1, _context?.CurrentAgencyStageId ?? 1);
            if (catalog == null ||
                promotionSlotIndex < 0)
            {
                return string.Empty;
            }

            IReadOnlyList<HolmasAgencyBuildingDefinition> promotions = catalog.GetPromotionsForStage(activeBuildStageId);
            if (promotions == null || promotionSlotIndex >= promotions.Count)
            {
                return string.Empty;
            }

            HolmasAgencyBuildingDefinition definition = promotions[promotionSlotIndex];
            return definition != null && GetPromotionLevel(definition) < definition.PromotionLevelCap
                ? definition.PromotionId ?? string.Empty
                : string.Empty;
        }

        private IHolmasAgencyCatalog GetCatalog()
        {
            return _context?.ServiceContainer != null
                ? _context.ServiceContainer.Get<IHolmasAgencyCatalog>()
                : null;
        }

        private int NormalizeSelectedStageId(
            IReadOnlyList<HolmasAgencyStageDefinition> stages,
            int selectedStageId,
            int currentStageId)
        {
            if (stages == null || stages.Count == 0)
            {
                return currentStageId;
            }

            int selectedIndex = FindStageIndex(stages, selectedStageId);
            int currentPageStartIndex = CalculateStagePageStartIndex(stages, currentStageId);
            if (selectedIndex >= currentPageStartIndex &&
                selectedIndex < currentPageStartIndex + VisibleStageCount)
            {
                return selectedStageId;
            }

            HolmasAgencyStageDefinition currentStage = stages.FirstOrDefault(stage => stage != null && stage.AgencyStageId == currentStageId);
            return currentStage != null ? currentStage.AgencyStageId : stages[0].AgencyStageId;
        }

        private BattleStageVm[] BuildStages(
            IReadOnlyList<HolmasAgencyStageDefinition> stages,
            int selectedStageId,
            int currentStageId)
        {
            var result = new BattleStageVm[VisibleStageCount];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = new BattleStageVm { SlotIndex = i, Visible = false };
            }

            if (stages == null || stages.Count == 0)
            {
                return result;
            }

            int startIndex = CalculateStagePageStartIndex(stages, selectedStageId);

            for (int slotIndex = 0; slotIndex < VisibleStageCount; slotIndex++)
            {
                int stageIndex = startIndex + slotIndex;
                if (stageIndex < 0 || stageIndex >= stages.Count || stages[stageIndex] == null)
                {
                    continue;
                }

                HolmasAgencyStageDefinition stage = stages[stageIndex];
                StageProgress progress = CalculateStageProgress(stage.AgencyStageId);
                if (stage.AgencyStageId < currentStageId && progress.Cap > 0)
                {
                    progress.Current = progress.Cap;
                }

                bool unlocked = stage.AgencyStageId == 1 || stage.AgencyStageId <= currentStageId;
                result[slotIndex] = new BattleStageVm
                {
                    SlotIndex = slotIndex,
                    AgencyStageId = stage.AgencyStageId,
                    StageName = stage.StageName ?? string.Empty,
                    StageImage = stage.StageImage ?? string.Empty,
                    ProgressLabel = $"{progress.Current}/{progress.Cap}",
                    ProgressCurrent = progress.Current,
                    ProgressCap = progress.Cap,
                    StarCount = CalculateStarCount(progress),
                    Visible = true,
                    Unlocked = unlocked,
                    Selected = stage.AgencyStageId == selectedStageId,
                    Current = stage.AgencyStageId == currentStageId,
                    Completed = stage.AgencyStageId < currentStageId || (stage.AgencyStageId == currentStageId && progress.IsComplete),
                };
            }

            return result;
        }

        private BattlePromotionSlotVm[] BuildPromotionSlots(
            IHolmasAgencyCatalog catalog,
            int selectedStageId,
            int currentStageId)
        {
            IReadOnlyList<HolmasAgencyBuildingDefinition> promotions = catalog?.GetPromotionsForStage(selectedStageId);
            if (promotions == null || promotions.Count == 0)
            {
                return Array.Empty<BattlePromotionSlotVm>();
            }

            bool unlocked = selectedStageId == 1 || selectedStageId <= currentStageId;
            bool current = selectedStageId == currentStageId;
            bool completed = selectedStageId < currentStageId || CalculateStageProgress(selectedStageId).IsComplete;
            string stageName = string.Empty;
            string stageImage = string.Empty;
            if (catalog != null && catalog.TryGetStage(selectedStageId, out HolmasAgencyStageDefinition stage) && stage != null)
            {
                stageName = stage.StageName ?? string.Empty;
                stageImage = stage.StageImage ?? string.Empty;
            }

            var result = new BattlePromotionSlotVm[promotions.Count];
            for (int i = 0; i < result.Length; i++)
            {
                HolmasAgencyBuildingDefinition promotion = promotions[i];
                int starCap = Math.Max(0, promotion?.PromotionLevelCap ?? 0);
                int starCount = Math.Min(GetPromotionLevel(promotion), starCap);
                bool canBuild = current &&
                    !completed &&
                    unlocked &&
                    promotion != null &&
                    !string.IsNullOrWhiteSpace(promotion.PromotionId) &&
                    starCount < starCap &&
                    GetPromotionUpgradeCost(promotion, starCount) > 0;
                result[i] = new BattlePromotionSlotVm
                {
                    SlotIndex = i,
                    AgencyStageId = selectedStageId,
                    PromotionId = promotion?.PromotionId ?? string.Empty,
                    StageName = stageName,
                    StageImage = stageImage,
                    ButtonImage = !string.IsNullOrWhiteSpace(promotion?.ButtonImage) ? promotion.ButtonImage : stageImage,
                    ProgressLabel = $"{starCount}/{starCap}",
                    StarCap = starCap,
                    StarCount = starCount,
                    Visible = true,
                    Unlocked = unlocked,
                    Current = current,
                    Completed = completed,
                    CanBuild = canBuild,
                    ActionLabel = BuildPromotionActionLabel(promotion, stageName, selectedStageId, currentStageId, unlocked, current, completed),
                };
            }

            return result;
        }

        private BattleStageBarVm[] BuildStageBars(BattleStageVm[] stages, int currentStageId)
        {
            var result = new BattleStageBarVm[VisibleStageBarCount];
            for (int i = 0; i < result.Length; i++)
            {
                BattleStageVm left = stages != null && i < stages.Length ? stages[i] : null;
                BattleStageVm right = stages != null && i + 1 < stages.Length ? stages[i + 1] : null;
                float progress = 0f;
                bool visible = left != null && right != null && left.Visible && right.Visible;
                if (visible)
                {
                    if (left.AgencyStageId < currentStageId)
                    {
                        progress = 1f;
                    }
                    else if (left.AgencyStageId == currentStageId)
                    {
                        progress = CalculateStageProgress(left.AgencyStageId).Ratio;
                    }
                }

                result[i] = new BattleStageBarVm
                {
                    SlotIndex = i,
                    Visible = visible,
                    Progress = Math.Max(0f, Math.Min(1f, progress)),
                };
            }

            return result;
        }

        private string BuildPromotionActionLabel(
            HolmasAgencyBuildingDefinition promotion,
            string stageName,
            int selectedStageId,
            int currentStageId,
            bool unlocked,
            bool current,
            bool completed)
        {
            if (promotion == null)
            {
                return "宣传待开放";
            }

            if (!unlocked || selectedStageId > currentStageId)
            {
                return "城市尚未解锁";
            }

            if (!current)
            {
                return $"{promotion.PromotionId}\n已完成/仅回看";
            }

            int currentLevel = GetPromotionLevel(promotion);
            int cap = Math.Max(0, promotion.PromotionLevelCap);
            if (completed || currentLevel >= cap)
            {
                return $"{promotion.PromotionId}\n宣传已满级";
            }

            int nextCost = GetPromotionUpgradeCost(promotion, currentLevel);
            if (nextCost <= 0)
            {
                return $"{promotion.PromotionId}\n宣传已满级";
            }

            int nextLevel = Math.Min(cap, currentLevel + 1);
            return $"{promotion.PromotionId}\n{currentLevel}->{nextLevel}/{cap}\n金币 -{nextCost}";
        }

        private string BuildPromotionSummaryLabel(
            IHolmasAgencyCatalog catalog,
            int selectedStageId,
            int currentStageId,
            BattlePromotionSlotVm nextPromotionSlot)
        {
            if (catalog == null)
            {
                return "城市宣传";
            }

            if (!catalog.TryGetStage(selectedStageId, out HolmasAgencyStageDefinition stage) || stage == null)
            {
                return "城市宣传";
            }

            if (selectedStageId > currentStageId)
            {
                return "城市尚未解锁";
            }

            if (selectedStageId < currentStageId)
            {
                return $"{stage.StageName}\n已完成/仅回看";
            }

            return nextPromotionSlot != null
                ? $"{stage.StageName}\n选择宣传项升级"
                : $"{stage.StageName}\n宣传已满级";
        }

        private string BuildSummary(IHolmasAgencyCatalog catalog, int selectedStageId, int currentStageId)
        {
            if (catalog == null)
            {
                return "城市宣传配置不可用。";
            }

            if (!catalog.TryGetStage(selectedStageId, out HolmasAgencyStageDefinition stage) || stage == null)
            {
                return $"当前推进阶段 Stage {currentStageId}。";
            }

            StageProgress progress = CalculateStageProgress(selectedStageId);
            string viewing = selectedStageId == currentStageId
                ? "当前推进"
                : selectedStageId < currentStageId ? "历史回看" : "尚未解锁";
            return $"{stage.StageName} | Stage {selectedStageId} | {viewing} | 宣传 {progress.Current}/{progress.Cap}";
        }

        private StageProgress CalculateStageProgress(int agencyStageId)
        {
            IHolmasAgencyCatalog catalog = GetCatalog();
            IReadOnlyList<HolmasAgencyBuildingDefinition> promotions = catalog?.GetPromotionsForStage(agencyStageId);
            if (promotions == null || promotions.Count == 0)
            {
                return new StageProgress();
            }

            int current = 0;
            int cap = 0;
            foreach (HolmasAgencyBuildingDefinition promotion in promotions)
            {
                if (promotion == null)
                {
                    continue;
                }

                int promotionCap = Math.Max(0, promotion.PromotionLevelCap);
                current += Math.Min(GetPromotionLevel(promotion), promotionCap);
                cap += promotionCap;
            }

            return new StageProgress
            {
                Current = current,
                Cap = cap,
            };
        }

        private int GetPromotionLevel(HolmasAgencyBuildingDefinition promotion)
        {
            return _context?.GameplayRuntime?.MetaProgressionState != null && promotion != null
                ? HolmasAgencyPromotionStateKey.GetLevel(_context.GameplayRuntime.MetaProgressionState, promotion.AgencyStageId, promotion.PromotionId)
                : 0;
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

            return Math.Max(0, promotion.PromotionUpgradeCosts[currentLevel]);
        }

        private static int CalculateStarCount(StageProgress progress)
        {
            if (progress.Cap <= 0 || progress.Current <= 0)
            {
                return 0;
            }

            if (progress.Current >= progress.Cap)
            {
                return 5;
            }

            double ratio = (double)progress.Current / progress.Cap;
            return Math.Max(1, Math.Min(4, (int)Math.Ceiling(ratio * 4d)));
        }

        private static int FindStageIndex(IReadOnlyList<HolmasAgencyStageDefinition> stages, int agencyStageId)
        {
            for (int i = 0; i < stages.Count; i++)
            {
                if (stages[i] != null && stages[i].AgencyStageId == agencyStageId)
                {
                    return i;
                }
            }

            return -1;
        }

        private static int CalculateStagePageStartIndex(IReadOnlyList<HolmasAgencyStageDefinition> stages, int currentStageId)
        {
            if (stages == null || stages.Count == 0)
            {
                return 0;
            }

            int currentIndex = FindStageIndex(stages, currentStageId);
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            return Math.Max(0, currentIndex / VisibleStageCount * VisibleStageCount);
        }

        private struct StageProgress
        {
            public int Current;
            public int Cap;
            public bool IsComplete => Cap > 0 && Current >= Cap;
            public float Ratio => Cap > 0 ? (float)Current / Cap : 0f;
        }

    }
}
