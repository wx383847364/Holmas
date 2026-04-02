using System;
using System.Collections.Generic;
using System.Linq;

namespace App.HotUpdate.Holmas.Meta
{
    /// <summary>
    /// 侦探社建筑配置定义。
    /// 当前 v1 只需要纯逻辑数据，不依赖 UI 或 Unity 资源。
    /// </summary>
    [Serializable]
    public sealed class HolmasAgencyBuildingDefinition
    {
        public int AgencyStageId;
        public string BuildingId = string.Empty;
        public int LevelCap;
        public int[] UpgradeCosts = Array.Empty<int>();
    }

    /// <summary>
    /// 侦探社建筑配置仓库接口。
    /// </summary>
    public interface IHolmasAgencyCatalog
    {
        bool TryGetBuilding(string buildingId, out HolmasAgencyBuildingDefinition definition);
        IReadOnlyList<HolmasAgencyBuildingDefinition> GetBuildingsForStage(int agencyStageId);
    }

    /// <summary>
    /// 纯内存版侦探社建筑配置仓库。
    /// </summary>
    public sealed class HolmasAgencyCatalog : IHolmasAgencyCatalog
    {
        private readonly Dictionary<string, HolmasAgencyBuildingDefinition> _buildings = new Dictionary<string, HolmasAgencyBuildingDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<int, List<HolmasAgencyBuildingDefinition>> _buildingsByStage = new Dictionary<int, List<HolmasAgencyBuildingDefinition>>();

        public HolmasAgencyCatalog()
        {
        }

        public HolmasAgencyCatalog(IEnumerable<HolmasAgencyBuildingDefinition> buildings)
        {
            SetBuildings(buildings);
        }

        public void SetBuildings(IEnumerable<HolmasAgencyBuildingDefinition> buildings)
        {
            _buildings.Clear();
            _buildingsByStage.Clear();

            if (buildings == null)
            {
                return;
            }

            foreach (var definition in buildings.Where(item => item != null))
            {
                string buildingId = definition.BuildingId ?? string.Empty;
                if (string.IsNullOrWhiteSpace(buildingId))
                {
                    continue;
                }

                _buildings[buildingId] = definition;

                List<HolmasAgencyBuildingDefinition> stageBuildings;
                if (!_buildingsByStage.TryGetValue(definition.AgencyStageId, out stageBuildings))
                {
                    stageBuildings = new List<HolmasAgencyBuildingDefinition>();
                    _buildingsByStage[definition.AgencyStageId] = stageBuildings;
                }

                stageBuildings.Add(definition);
            }
        }

        public bool TryGetBuilding(string buildingId, out HolmasAgencyBuildingDefinition definition)
        {
            return _buildings.TryGetValue(buildingId ?? string.Empty, out definition);
        }

        public IReadOnlyList<HolmasAgencyBuildingDefinition> GetBuildingsForStage(int agencyStageId)
        {
            List<HolmasAgencyBuildingDefinition> stageBuildings;
            if (!_buildingsByStage.TryGetValue(agencyStageId, out stageBuildings) || stageBuildings == null)
            {
                return Array.Empty<HolmasAgencyBuildingDefinition>();
            }

            return stageBuildings;
        }
    }

    /// <summary>
    /// 单次建筑升级的结果。
    /// </summary>
    [Serializable]
    public sealed class HolmasAgencyUpgradeResult
    {
        public bool Success;
        public string FailureReason = string.Empty;
        public int AgencyStageId;
        public string BuildingId = string.Empty;
        public int PreviousLevel;
        public int NewLevel;
        public long GoldSpent;
        public long ExperienceGained;
        public int PlayerLevelAfter;
        public bool StageAdvanced;
    }

    /// <summary>
    /// 侦探社建筑升级纯逻辑服务。
    /// 不依赖 UI，负责阶段校验、金币校验、等级上限校验和成长推进。
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

        public HolmasAgencyUpgradeResult TryUpgradeBuilding(HolmasMetaProgressionState state, string buildingId)
        {
            var result = new HolmasAgencyUpgradeResult
            {
                BuildingId = buildingId ?? string.Empty
            };

            if (state == null)
            {
                result.FailureReason = "长期进度状态为空。";
                return result;
            }

            if (string.IsNullOrWhiteSpace(buildingId))
            {
                result.FailureReason = "建筑标识为空。";
                return result;
            }

            if (!_catalog.TryGetBuilding(buildingId, out var definition) || definition == null)
            {
                result.FailureReason = $"找不到建筑配置: {buildingId}。";
                return result;
            }

            int currentStageId = Math.Max(1, state.AgencyStageId);
            result.AgencyStageId = currentStageId;

            if (definition.AgencyStageId != currentStageId)
            {
                result.FailureReason = $"建筑 {buildingId} 不属于当前阶段 {currentStageId}。";
                return result;
            }

            if (definition.LevelCap <= 0)
            {
                result.FailureReason = $"建筑 {buildingId} 的等级上限非法。";
                return result;
            }

            int currentLevel = state.GetBuildingLevel(buildingId);
            result.PreviousLevel = currentLevel;

            if (currentLevel >= definition.LevelCap)
            {
                result.FailureReason = $"建筑 {buildingId} 已达到当前阶段等级上限。";
                return result;
            }

            if (definition.UpgradeCosts == null || definition.UpgradeCosts.Length == 0)
            {
                result.FailureReason = $"建筑 {buildingId} 缺少升级费用配置。";
                return result;
            }

            if (currentLevel >= definition.UpgradeCosts.Length)
            {
                result.FailureReason = $"建筑 {buildingId} 缺少第 {currentLevel + 1} 级升级费用。";
                return result;
            }

            long goldCost = Math.Max(0, definition.UpgradeCosts[currentLevel]);
            if (goldCost <= 0)
            {
                result.FailureReason = $"建筑 {buildingId} 的升级费用非法。";
                return result;
            }

            if (state.GoldBalance < goldCost)
            {
                result.FailureReason = $"金币不足，无法升级建筑 {buildingId}。";
                return result;
            }

            state.GoldBalance -= goldCost;
            state.SetBuildingLevel(buildingId, currentLevel + 1);
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
            var buildings = _catalog.GetBuildingsForStage(agencyStageId);
            if (buildings == null || buildings.Count == 0)
            {
                return false;
            }

            foreach (var definition in buildings)
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.BuildingId))
                {
                    continue;
                }

                if (definition.LevelCap <= 0)
                {
                    return false;
                }

                if (state.GetBuildingLevel(definition.BuildingId) < definition.LevelCap)
                {
                    return false;
                }
            }

            return true;
        }

        private bool HasStage(int agencyStageId)
        {
            var buildings = _catalog.GetBuildingsForStage(agencyStageId);
            return buildings != null && buildings.Count > 0;
        }
    }
}
