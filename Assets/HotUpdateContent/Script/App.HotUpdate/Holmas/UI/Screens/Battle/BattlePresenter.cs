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
            int currentStageId = Math.Max(1, _context?.CurrentAgencyStageId ?? 1);
            int normalizedSelectedStageId = NormalizeSelectedStageId(stages, selectedStageId, currentStageId);
            BattleStageVm[] stageVms = BuildStages(stages, normalizedSelectedStageId, currentStageId);
            BattleStageBarVm[] stageBars = BuildStageBars(stageVms, currentStageId);
            BattleBuildStageVm[] buildStages = BuildBuildStages(catalog, stageVms, currentStageId);
            BattleBuildStageVm selectedBuildStage = buildStages.FirstOrDefault(item => item != null && item.Selected);

            return new BattleVm
            {
                LevelLabel = $"Lv {_context?.CurrentPlayerLevel ?? 1}",
                GoldLabel = $"Gold {_context?.CurrentGoldBalance ?? 0L}",
                EnergyLabel = _context?.EnergyLabel ?? "50/50",
                Summary = BuildSummary(catalog, normalizedSelectedStageId, currentStageId),
                Status = string.IsNullOrWhiteSpace(status) ? "选择城市阶段，推进当前宣传建设。" : status,
                BuildButtonLabel = selectedBuildStage != null ? selectedBuildStage.ActionLabel : "城市宣传",
                BuildButtonEnabled = selectedBuildStage != null && selectedBuildStage.CanBuild,
                SelectedStageId = normalizedSelectedStageId,
                BuildStages = buildStages,
                Stages = stageVms,
                StageBars = stageBars,
            };
        }

        public string GetNextUpgradeablePromotionId(int selectedStageId)
        {
            IHolmasAgencyCatalog catalog = GetCatalog();
            int currentStageId = Math.Max(1, _context?.CurrentAgencyStageId ?? 1);
            if (catalog == null || selectedStageId != currentStageId)
            {
                return string.Empty;
            }

            HolmasAgencyBuildingDefinition definition = GetNextUpgradeablePromotion(catalog, currentStageId);
            return definition != null ? definition.PromotionId ?? string.Empty : string.Empty;
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

            if (selectedStageId > 0 && stages.Any(stage => stage != null && stage.AgencyStageId == selectedStageId))
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

            int currentIndex = FindStageIndex(stages, currentStageId);
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            int selectedIndex = FindStageIndex(stages, selectedStageId);
            int startIndex = Math.Max(0, Math.Min(currentIndex - 2, stages.Count - VisibleStageCount));
            if (selectedIndex >= 0)
            {
                if (selectedIndex < startIndex)
                {
                    startIndex = selectedIndex;
                }
                else if (selectedIndex >= startIndex + VisibleStageCount)
                {
                    startIndex = selectedIndex - VisibleStageCount + 1;
                }

                startIndex = Math.Max(0, Math.Min(startIndex, Math.Max(0, stages.Count - VisibleStageCount)));
            }

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

        private BattleBuildStageVm[] BuildBuildStages(
            IHolmasAgencyCatalog catalog,
            BattleStageVm[] stages,
            int currentStageId)
        {
            var result = new BattleBuildStageVm[VisibleStageCount];
            for (int i = 0; i < result.Length; i++)
            {
                BattleStageVm stage = stages != null && i < stages.Length ? stages[i] : null;
                if (stage == null || !stage.Visible)
                {
                    result[i] = new BattleBuildStageVm { SlotIndex = i, Visible = false };
                    continue;
                }

                bool canBuild = stage.Current && !stage.Completed && GetNextUpgradeablePromotion(catalog, currentStageId) != null;
                result[i] = new BattleBuildStageVm
                {
                    SlotIndex = i,
                    AgencyStageId = stage.AgencyStageId,
                    StageName = stage.StageName ?? string.Empty,
                    StageImage = stage.StageImage ?? string.Empty,
                    ProgressLabel = stage.ProgressLabel ?? string.Empty,
                    StarCount = stage.StarCount,
                    Visible = true,
                    Unlocked = stage.Unlocked,
                    Selected = stage.Selected,
                    Current = stage.Current,
                    Completed = stage.Completed,
                    CanBuild = canBuild,
                    ActionLabel = BuildActionLabel(catalog, stage, currentStageId),
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

        private string BuildActionLabel(
            IHolmasAgencyCatalog catalog,
            BattleStageVm stage,
            int currentStageId)
        {
            if (stage == null || !stage.Visible)
            {
                return "宣传待开放";
            }

            if (!stage.Unlocked || stage.AgencyStageId > currentStageId)
            {
                return "城市尚未解锁";
            }

            if (stage.AgencyStageId < currentStageId)
            {
                return $"{stage.StageName}\n已完成/仅回看";
            }

            HolmasAgencyBuildingDefinition promotion = GetNextUpgradeablePromotion(catalog, currentStageId);
            if (promotion == null)
            {
                return $"{stage.StageName}\n宣传已满级";
            }

            int currentLevel = GetPromotionLevel(promotion);
            int nextCost = GetPromotionUpgradeCost(promotion, currentLevel);
            if (nextCost <= 0)
            {
                return $"{stage.StageName}\n宣传已满级";
            }

            int nextProgress = Math.Min(stage.ProgressCap, stage.ProgressCurrent + 1);
            return $"{stage.StageName}\n宣传 {stage.ProgressCurrent}->{nextProgress}/{stage.ProgressCap}\n金币 -{nextCost}";
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

        private HolmasAgencyBuildingDefinition GetNextUpgradeablePromotion(IHolmasAgencyCatalog catalog, int currentStageId)
        {
            IReadOnlyList<HolmasAgencyBuildingDefinition> promotions = catalog?.GetPromotionsForStage(currentStageId);
            if (promotions == null || promotions.Count == 0)
            {
                return null;
            }

            return promotions.FirstOrDefault(item =>
                item != null &&
                !string.IsNullOrWhiteSpace(item.PromotionId) &&
                GetPromotionLevel(item) < item.PromotionLevelCap);
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

        private struct StageProgress
        {
            public int Current;
            public int Cap;
            public bool IsComplete => Cap > 0 && Current >= Cap;
            public float Ratio => Cap > 0 ? (float)Current / Cap : 0f;
        }

    }
}
