using System;
using System.Collections.Generic;
using System.Linq;

namespace App.HotUpdate.Holmas.Meta
{
    /// <summary>
    /// 单个城市阶段下的宣传功能配置。
    /// 当前 v1 不接 UI，只保留升级所需的纯逻辑数据。
    /// </summary>
    [Serializable]
    public sealed class HolmasAgencyBuildingDefinition
    {
        public int AgencyStageId;
        public string StageName = string.Empty;
        public string StageImage = string.Empty;
        public string ButtonImage = string.Empty;
        public string PromotionId = string.Empty;
        public int PromotionLevelCap;
        public int[] PromotionUpgradeCosts = Array.Empty<int>();
    }

    [Serializable]
    public sealed class HolmasAgencyStageDefinition
    {
        public int AgencyStageId;
        public string StageName = string.Empty;
        public string StageImage = string.Empty;
    }

    /// <summary>
    /// 城市宣传配置仓库接口。
    /// </summary>
    public interface IHolmasAgencyCatalog
    {
        bool TryGetPromotion(string promotionId, out HolmasAgencyBuildingDefinition definition);
        IReadOnlyList<HolmasAgencyBuildingDefinition> GetPromotionsForStage(int agencyStageId);
        IReadOnlyList<HolmasAgencyStageDefinition> GetStagesInOrder();
        bool TryGetStage(int agencyStageId, out HolmasAgencyStageDefinition definition);
    }

    /// <summary>
    /// 纯内存版城市宣传配置仓库。
    /// </summary>
    public sealed class HolmasAgencyCatalog : IHolmasAgencyCatalog
    {
        private readonly Dictionary<string, HolmasAgencyBuildingDefinition> _promotions = new Dictionary<string, HolmasAgencyBuildingDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<int, List<HolmasAgencyBuildingDefinition>> _promotionsByStage = new Dictionary<int, List<HolmasAgencyBuildingDefinition>>();
        private readonly Dictionary<int, HolmasAgencyStageDefinition> _stagesById = new Dictionary<int, HolmasAgencyStageDefinition>();
        private readonly List<HolmasAgencyStageDefinition> _stagesInOrder = new List<HolmasAgencyStageDefinition>();

        public HolmasAgencyCatalog()
        {
        }

        public HolmasAgencyCatalog(IEnumerable<HolmasAgencyBuildingDefinition> promotions)
        {
            SetPromotions(promotions);
        }

        public void SetPromotions(IEnumerable<HolmasAgencyBuildingDefinition> promotions)
        {
            _promotions.Clear();
            _promotionsByStage.Clear();
            _stagesById.Clear();
            _stagesInOrder.Clear();

            if (promotions == null)
            {
                return;
            }

            foreach (HolmasAgencyBuildingDefinition definition in promotions.Where(item => item != null))
            {
                string promotionId = definition.PromotionId ?? string.Empty;
                if (string.IsNullOrWhiteSpace(promotionId))
                {
                    continue;
                }

                definition.StageImage = NormalizeResourcePath(definition.StageImage);
                definition.ButtonImage = NormalizeResourcePath(definition.ButtonImage);
                _promotions[promotionId] = definition;

                if (!_promotionsByStage.TryGetValue(definition.AgencyStageId, out List<HolmasAgencyBuildingDefinition> stagePromotions))
                {
                    stagePromotions = new List<HolmasAgencyBuildingDefinition>();
                    _promotionsByStage[definition.AgencyStageId] = stagePromotions;
                }

                stagePromotions.Add(definition);

                if (!_stagesById.ContainsKey(definition.AgencyStageId))
                {
                    var stage = new HolmasAgencyStageDefinition
                    {
                        AgencyStageId = definition.AgencyStageId,
                        StageName = definition.StageName ?? string.Empty,
                        StageImage = definition.StageImage ?? string.Empty,
                    };
                    _stagesById[definition.AgencyStageId] = stage;
                    _stagesInOrder.Add(stage);
                }
            }

            _stagesInOrder.Sort((left, right) => left.AgencyStageId.CompareTo(right.AgencyStageId));
        }

        public bool TryGetPromotion(string promotionId, out HolmasAgencyBuildingDefinition definition)
        {
            return _promotions.TryGetValue(promotionId ?? string.Empty, out definition);
        }

        public IReadOnlyList<HolmasAgencyBuildingDefinition> GetPromotionsForStage(int agencyStageId)
        {
            if (!_promotionsByStage.TryGetValue(agencyStageId, out List<HolmasAgencyBuildingDefinition> stagePromotions) || stagePromotions == null)
            {
                return Array.Empty<HolmasAgencyBuildingDefinition>();
            }

            return stagePromotions;
        }

        public IReadOnlyList<HolmasAgencyStageDefinition> GetStagesInOrder()
        {
            return _stagesInOrder;
        }

        public bool TryGetStage(int agencyStageId, out HolmasAgencyStageDefinition definition)
        {
            return _stagesById.TryGetValue(agencyStageId, out definition);
        }

        private static string NormalizeResourcePath(string resourcePath)
        {
            string normalized = (resourcePath ?? string.Empty).Replace('\\', '/').Trim();
            if (string.IsNullOrWhiteSpace(normalized) ||
                normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                return normalized;
            }

            return "Assets/HotUpdateContent/Res/" + normalized.TrimStart('/');
        }
    }

    /// <summary>
    /// 单次宣传升级结果。
    /// </summary>
    [Serializable]
    public sealed class HolmasAgencyUpgradeResult
    {
        public bool Success;
        public string FailureReason = string.Empty;
        public int AgencyStageId;
        public string PromotionId = string.Empty;
        public int PreviousLevel;
        public int NewLevel;
        public long GoldSpent;
        public long ExperienceGained;
        public int PlayerLevelAfter;
        public bool StageAdvanced;
    }

    /// <summary>
    /// 城市宣传升级纯逻辑服务。
    /// 负责阶段校验、金币校验、等级上限校验和成长推进。
    /// </summary>
    public sealed class HolmasAgencyProgressionService
    {
        private readonly IHolmasAgencyCatalog _catalog;
        private readonly HolmasMetaProgressionService _metaProgressionService;

        public HolmasAgencyProgressionService(
            IHolmasAgencyCatalog catalog,
            HolmasMetaProgressionService metaProgressionService)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _metaProgressionService = metaProgressionService ?? throw new ArgumentNullException(nameof(metaProgressionService));
        }

        public HolmasAgencyUpgradeResult TryUpgradePromotion(HolmasMetaProgressionState state, string promotionId)
        {
            var result = new HolmasAgencyUpgradeResult
            {
                PromotionId = promotionId ?? string.Empty
            };

            if (state == null)
            {
                result.FailureReason = "长期进度状态为空。";
                return result;
            }

            if (string.IsNullOrWhiteSpace(promotionId))
            {
                result.FailureReason = "宣传标识为空。";
                return result;
            }

            int currentStageId = Math.Max(1, state.AgencyStageId);
            result.AgencyStageId = currentStageId;

            HolmasAgencyBuildingDefinition definition = ResolvePromotionDefinition(currentStageId, promotionId);
            if (definition == null)
            {
                result.FailureReason = $"找不到当前城市阶段 {currentStageId} 的宣传配置: {promotionId}。";
                return result;
            }

            if (definition.AgencyStageId != currentStageId)
            {
                result.FailureReason = $"宣传 {promotionId} 不属于当前城市阶段 {currentStageId}。";
                return result;
            }

            if (definition.PromotionLevelCap <= 0)
            {
                result.FailureReason = $"宣传 {promotionId} 的等级上限非法。";
                return result;
            }

            int currentLevel = HolmasAgencyPromotionStateKey.GetLevel(state, currentStageId, promotionId);
            result.PreviousLevel = currentLevel;

            if (currentLevel >= definition.PromotionLevelCap)
            {
                result.FailureReason = $"宣传 {promotionId} 已达到当前阶段等级上限。";
                return result;
            }

            if (definition.PromotionUpgradeCosts == null || definition.PromotionUpgradeCosts.Length == 0)
            {
                result.FailureReason = $"宣传 {promotionId} 缺少升级费用配置。";
                return result;
            }

            if (currentLevel >= definition.PromotionUpgradeCosts.Length)
            {
                result.FailureReason = $"宣传 {promotionId} 缺少第 {currentLevel + 1} 级升级费用。";
                return result;
            }

            long goldCost = Math.Max(0, definition.PromotionUpgradeCosts[currentLevel]);
            if (goldCost <= 0)
            {
                result.FailureReason = $"宣传 {promotionId} 的升级费用非法。";
                return result;
            }

            if (state.GoldBalance < goldCost)
            {
                result.FailureReason = $"金币不足，无法升级宣传 {promotionId}。";
                return result;
            }

            state.GoldBalance -= goldCost;
            HolmasAgencyPromotionStateKey.SetLevel(state, currentStageId, promotionId, currentLevel + 1);
            _metaProgressionService.ApplyExperience(state, 1L);

            result.Success = true;
            result.GoldSpent = goldCost;
            result.ExperienceGained = 1L;
            result.NewLevel = currentLevel + 1;
            result.PlayerLevelAfter = state.PlayerLevel;

            if (IsStageCompleted(state, currentStageId))
            {
                int nextStageId = currentStageId + 1;
                if (HasStage(nextStageId))
                {
                    state.AgencyStageId = nextStageId;
                    result.StageAdvanced = true;
                }
            }

            return result;
        }
        private bool IsStageCompleted(HolmasMetaProgressionState state, int agencyStageId)
        {
            IReadOnlyList<HolmasAgencyBuildingDefinition> promotions = _catalog.GetPromotionsForStage(agencyStageId);
            if (promotions == null || promotions.Count == 0)
            {
                return false;
            }

            foreach (HolmasAgencyBuildingDefinition definition in promotions)
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.PromotionId))
                {
                    continue;
                }

                if (definition.PromotionLevelCap <= 0)
                {
                    return false;
                }

                if (HolmasAgencyPromotionStateKey.GetLevel(state, agencyStageId, definition.PromotionId) < definition.PromotionLevelCap)
                {
                    return false;
                }
            }

            return true;
        }

        private bool HasStage(int agencyStageId)
        {
            IReadOnlyList<HolmasAgencyBuildingDefinition> promotions = _catalog.GetPromotionsForStage(agencyStageId);
            return promotions != null && promotions.Count > 0;
        }

        private HolmasAgencyBuildingDefinition ResolvePromotionDefinition(int agencyStageId, string promotionId)
        {
            if (string.IsNullOrWhiteSpace(promotionId))
            {
                return null;
            }

            IReadOnlyList<HolmasAgencyBuildingDefinition> promotions = _catalog.GetPromotionsForStage(agencyStageId);
            if (promotions == null || promotions.Count == 0)
            {
                return null;
            }

            for (int i = 0; i < promotions.Count; i++)
            {
                HolmasAgencyBuildingDefinition definition = promotions[i];
                if (definition != null && string.Equals(definition.PromotionId, promotionId, StringComparison.Ordinal))
                {
                    return definition;
                }
            }

            return null;
        }
    }
}
