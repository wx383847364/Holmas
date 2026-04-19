using System;
using System.Collections.Generic;
using System.Linq;

namespace App.HotUpdate.Holmas.Tasks.Config
{
    /// <summary>
    /// 从导出包恢复运行时 Catalog 的工厂。
    /// 这里负责把 int 索引折回字符串外部接口，同时保留索引信息供后续优化使用。
    /// </summary>
    public static class HolmasConfigCatalogFactory
    {
        public static bool TryCreateFromBinary(
            byte[] coreBytes,
            byte[] catMetaBytes,
            out HolmasConfigCatalogBundle bundle,
            out HolmasConfigReport report)
        {
            bundle = null;
            report = new HolmasConfigReport();

            if (!HolmasConfigBinaryCodec.TryReadCorePackage(coreBytes, out var corePackage, out var coreError))
            {
                report.AddError(coreError);
                return false;
            }

            if (!HolmasConfigBinaryCodec.TryReadCatMetaPackage(catMetaBytes, out var catMetaPackage, out var catError))
            {
                report.AddError(catError);
                return false;
            }

            return TryCreate(corePackage, catMetaPackage, out bundle, out report);
        }

        public static bool TryCreateFromJson(
            string coreJson,
            string catMetaJson,
            out HolmasConfigCatalogBundle bundle,
            out HolmasConfigReport report)
        {
            bundle = null;
            report = new HolmasConfigReport();

            if (!HolmasConfigBinaryCodec.TryReadCoreJson(coreJson, out var corePackage, out var coreError))
            {
                report.AddError(coreError);
                return false;
            }

            if (!HolmasConfigBinaryCodec.TryReadCatMetaJson(catMetaJson, out var catMetaPackage, out var catError))
            {
                report.AddError(catError);
                return false;
            }

            return TryCreate(corePackage, catMetaPackage, out bundle, out report);
        }

        public static bool TryCreate(
            HolmasCoreConfigPackage corePackage,
            HolmasCatMetaPackage catMetaPackage,
            out HolmasConfigCatalogBundle bundle,
            out HolmasConfigReport report)
        {
            bundle = null;
            report = new HolmasConfigReport();

            if (corePackage == null)
            {
                report.AddError("核心配置包为空。");
                return false;
            }

            if (catMetaPackage == null)
            {
                report.AddError("猫元数据包为空。");
                return false;
            }

            if (!TryBuildCats(catMetaPackage.Cats, out var cats, out var catIdByIndex, report))
            {
                return false;
            }

            if (!TryBuildMaps(corePackage.Maps, out var maps, out var mapIdByIndex, report))
            {
                return false;
            }

            if (!TryBuildTasks(corePackage.Tasks, catIdByIndex, out var tasks, out var taskTypeIdByIndex, report))
            {
                return false;
            }

            if (!TryBuildPlayerLevels(
                corePackage.PlayerLevels,
                taskTypeIdByIndex,
                mapIdByIndex,
                out var playerLevels,
                report))
            {
                return false;
            }

            if (!TryBuildAgencyBuildings(corePackage.AgencyBuildings, out var agencyBuildings, report))
            {
                return false;
            }

            var mapCatalog = new HolmasMapCatalog(maps);
            var taskCatalog = new HolmasTaskCatalog(cats, tasks, playerLevels);
            bundle = new HolmasConfigCatalogBundle(mapCatalog, taskCatalog, cats, maps, tasks, playerLevels, agencyBuildings, report);
            report.MarkSuccess("配置包已成功恢复为运行时 Catalog。");
            return true;
        }

        private static bool TryBuildCats(
            IReadOnlyList<HolmasCatMetaRow> rows,
            out List<HolmasCatDefinition> cats,
            out Dictionary<int, string> catIdByIndex,
            HolmasConfigReport report)
        {
            cats = new List<HolmasCatDefinition>();
            catIdByIndex = new Dictionary<int, string>();

            if (rows == null || rows.Count == 0)
            {
                report.AddError("猫元数据包没有任何猫条目。");
                return false;
            }

            var seenCatIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row == null)
                {
                    report.AddError($"猫元数据第 {i} 行为空。");
                    return false;
                }

                string catId = row.CatId ?? string.Empty;
                if (string.IsNullOrWhiteSpace(catId))
                {
                    report.AddError($"猫元数据第 {i} 行缺少 CatId。");
                    return false;
                }

                if (!seenCatIds.Add(catId))
                {
                    report.AddError($"猫元数据存在重复 CatId: {catId}。");
                    return false;
                }

                if (string.IsNullOrWhiteSpace(row.CatName))
                {
                    report.AddError($"猫元数据缺少 CatName: {catId}。");
                    return false;
                }

                if (string.IsNullOrWhiteSpace(row.IconPath))
                {
                    report.AddWarning($"猫元数据缺少 IconPath: {catId}。当前阶段先保留为空字符串。");
                }

                if (row.Weight < 0)
                {
                    report.AddError($"猫权重不能为负: {catId}。");
                    return false;
                }

                if (row.Price < 0)
                {
                    report.AddError($"猫价格不能为负: {catId}。");
                    return false;
                }

                if (row.Rarity < 0)
                {
                    report.AddError($"猫稀有度不能为负: {catId}。");
                    return false;
                }

                catIdByIndex[i] = catId;
                cats.Add(new HolmasCatDefinition
                {
                    CatIndex = i,
                    CatId = catId,
                    CatName = row.CatName,
                    IconPath = row.IconPath,
                    Rarity = row.Rarity,
                    Weight = row.Weight,
                    Price = row.Price,
                });
            }

            return true;
        }

        private static bool TryBuildMaps(
            IReadOnlyList<HolmasMapRow> rows,
            out List<HolmasMapDefinition> maps,
            out Dictionary<int, string> mapIdByIndex,
            HolmasConfigReport report)
        {
            maps = new List<HolmasMapDefinition>();
            mapIdByIndex = new Dictionary<int, string>();

            if (rows == null || rows.Count == 0)
            {
                report.AddError("核心配置包没有任何地图条目。");
                return false;
            }

            var seenMapIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row == null)
                {
                    report.AddError($"地图配置第 {i} 行为空。");
                    return false;
                }

                string mapId = row.MapId ?? string.Empty;
                if (string.IsNullOrWhiteSpace(mapId))
                {
                    report.AddError($"地图配置第 {i} 行缺少 MapId。");
                    return false;
                }

                if (!seenMapIds.Add(mapId))
                {
                    report.AddError($"地图配置存在重复 MapId: {mapId}。");
                    return false;
                }

                if (string.IsNullOrWhiteSpace(row.TerrainPath))
                {
                    report.AddError($"地图配置缺少 TerrainPath: {mapId}。");
                    return false;
                }

                if (row.CatCountMin < 0 || row.CatCountMax < row.CatCountMin)
                {
                    report.AddError($"地图配置的猫数范围非法: {mapId}。");
                    return false;
                }

                mapIdByIndex[i] = mapId;
                maps.Add(new HolmasMapDefinition
                {
                    MapIndex = i,
                    MapId = mapId,
                    TerrainPath = row.TerrainPath,
                    CatCountMin = row.CatCountMin,
                    CatCountMax = row.CatCountMax,
                });
            }

            return true;
        }

        private static bool TryBuildTasks(
            IReadOnlyList<HolmasTaskRow> rows,
            IReadOnlyDictionary<int, string> catIdByIndex,
            out List<HolmasTaskTemplateDefinition> tasks,
            out Dictionary<int, string> taskTypeIdByIndex,
            HolmasConfigReport report)
        {
            tasks = new List<HolmasTaskTemplateDefinition>();
            taskTypeIdByIndex = new Dictionary<int, string>();

            if (rows == null || rows.Count == 0)
            {
                report.AddError("核心配置包没有任何任务条目。");
                return false;
            }

            var seenTaskTypeIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row == null)
                {
                    report.AddError($"任务配置第 {i} 行为空。");
                    return false;
                }

                string taskTypeId = row.TaskTypeId ?? string.Empty;
                if (string.IsNullOrWhiteSpace(taskTypeId))
                {
                    report.AddError($"任务配置第 {i} 行缺少 TaskTypeId。");
                    return false;
                }

                if (!seenTaskTypeIds.Add(taskTypeId))
                {
                    report.AddError($"任务配置存在重复 TaskTypeId: {taskTypeId}。");
                    return false;
                }

                if (row.TaskKind != HolmasTaskKind.Money)
                {
                    report.AddError($"当前阶段不支持的任务类型: {taskTypeId} ({row.TaskKind})。");
                    return false;
                }

                if (row.CatIndices == null || row.CatIndices.Length == 0)
                {
                    report.AddError($"任务配置缺少 CatIndices: {taskTypeId}。");
                    return false;
                }

                if (row.CountMin < 0 || row.CountMax < row.CountMin)
                {
                    report.AddError($"任务配置的数量范围非法: {taskTypeId}。");
                    return false;
                }

                if (row.LevelRewardFactor < 0f)
                {
                    report.AddError($"任务配置的等级奖励系数不能为负: {taskTypeId}。");
                    return false;
                }

                var catIds = new string[row.CatIndices.Length];
                for (int j = 0; j < row.CatIndices.Length; j++)
                {
                    int catIndex = row.CatIndices[j];
                    if (!catIdByIndex.TryGetValue(catIndex, out string catId))
                    {
                        report.AddError($"任务 {taskTypeId} 引用了不存在的猫索引: {catIndex}。");
                        return false;
                    }

                    catIds[j] = catId;
                }

                var rewardValues = row.RewardValues ?? Array.Empty<int>();
                taskTypeIdByIndex[i] = taskTypeId;
                tasks.Add(new HolmasTaskTemplateDefinition
                {
                    TaskIndex = i,
                    TaskTypeId = taskTypeId,
                    TaskKind = row.TaskKind,
                    CatIdList = catIds,
                    CatIndices = row.CatIndices.ToArray(),
                    CountMin = row.CountMin,
                    CountMax = row.CountMax,
                    RewardArray = Array.ConvertAll(rewardValues, value => value.ToString()),
                    RewardValues = rewardValues.ToArray(),
                    LevelRewardFactor = row.LevelRewardFactor,
                });
            }

            return true;
        }

        private static bool TryBuildPlayerLevels(
            IReadOnlyList<HolmasPlayerLevelRow> rows,
            IReadOnlyDictionary<int, string> taskTypeIdByIndex,
            IReadOnlyDictionary<int, string> mapIdByIndex,
            out List<HolmasPlayerLevelDefinition> playerLevels,
            HolmasConfigReport report)
        {
            playerLevels = new List<HolmasPlayerLevelDefinition>();

            if (rows == null || rows.Count == 0)
            {
                report.AddError("核心配置包没有任何玩家等级条目。");
                return false;
            }

            var seenLevels = new HashSet<int>();
            int expectedLevel = 1;

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row == null)
                {
                    report.AddError($"玩家等级配置第 {i} 行为空。");
                    return false;
                }

                if (row.PlayerLevel != expectedLevel)
                {
                    report.AddError($"玩家等级必须从 1 连续递增，当前第 {i + 1} 行是 {row.PlayerLevel}。");
                    return false;
                }

                expectedLevel++;

                if (row.PlayerLevel < 0)
                {
                    report.AddError($"玩家等级不能为负: {row.PlayerLevel}。");
                    return false;
                }

                if (!seenLevels.Add(row.PlayerLevel))
                {
                    report.AddError($"玩家等级存在重复定义: {row.PlayerLevel}。");
                    return false;
                }

                if (row.UpgradeExp < 0)
                {
                    report.AddError($"玩家等级的升级门槛不能为负: {row.PlayerLevel}。");
                    return false;
                }

                if (i > 0 && row.UpgradeExp <= rows[i - 1].UpgradeExp)
                {
                    report.AddError($"玩家等级的升级门槛必须严格递增: {row.PlayerLevel}。");
                    return false;
                }

                if (row.OfflineRewardPerHour < 0)
                {
                    report.AddError($"玩家等级的 offlineRewardPerHour 不能为负: {row.PlayerLevel}。");
                    return false;
                }

                if (row.AdUnlockHours <= 0)
                {
                    report.AddError($"玩家等级的 adUnlockHours 必须大于 0: {row.PlayerLevel}。");
                    return false;
                }

                if (!TryValidateWeightedIndices(row.TaskTypeIndices, row.TaskTypeWeights, taskTypeIdByIndex, $"玩家等级 {row.PlayerLevel} 的任务组", report))
                {
                    return false;
                }

                if (!TryValidateWeightedIndices(row.MapIndices, row.MapWeights, mapIdByIndex, $"玩家等级 {row.PlayerLevel} 的地图组", report))
                {
                    return false;
                }

                playerLevels.Add(new HolmasPlayerLevelDefinition
                {
                    PlayerLevelIndex = i,
                    PlayerLevel = row.PlayerLevel,
                    UpgradeExp = row.UpgradeExp,
                    OfflineRewardPerHour = row.OfflineRewardPerHour,
                    AdUnlockHours = row.AdUnlockHours,
                    TaskTypeIds = ResolveIds(row.TaskTypeIndices, taskTypeIdByIndex),
                    TaskTypeWeights = row.TaskTypeWeights ?? Array.Empty<int>(),
                    TaskTypeIndices = row.TaskTypeIndices ?? Array.Empty<int>(),
                    MapIds = ResolveIds(row.MapIndices, mapIdByIndex),
                    MapWeights = row.MapWeights ?? Array.Empty<int>(),
                    MapIndices = row.MapIndices ?? Array.Empty<int>(),
                });
            }

            return true;
        }

        private static bool TryBuildAgencyBuildings(
            IReadOnlyList<HolmasAgencyBuildingRow> rows,
            out List<HolmasAgencyBuildingRow> agencyBuildings,
            HolmasConfigReport report)
        {
            agencyBuildings = new List<HolmasAgencyBuildingRow>();

            if (rows == null || rows.Count == 0)
            {
                report.AddError("核心配置包缺少 AgencyBuildings。");
                return false;
            }

            if (rows.Count != 100)
            {
                report.AddError($"AgencyBuildings 行数必须为 100，当前为 {rows.Count}。");
                return false;
            }

            var seenStageIds = new HashSet<int>();
            var seenStageNames = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row == null)
                {
                    report.AddError($"AgencyBuildings 第 {i + 1} 行为空。");
                    return false;
                }

                if (row.AgencyStageId <= 0)
                {
                    report.AddError($"AgencyBuildings 第 {i + 1} 行的 agencyStageId 必须是正整数。");
                    return false;
                }

                if (row.AgencyStageId != i + 1)
                {
                    report.AddError($"AgencyBuildings 的 agencyStageId 必须按 1..N 连续递增，当前第 {i + 1} 行为 {row.AgencyStageId}。");
                    return false;
                }

                if (!seenStageIds.Add(row.AgencyStageId))
                {
                    report.AddError($"AgencyBuildings 存在重复 agencyStageId: {row.AgencyStageId}。");
                    return false;
                }

                string stageName = row.StageName ?? string.Empty;
                if (string.IsNullOrWhiteSpace(stageName))
                {
                    report.AddError($"AgencyBuildings {row.AgencyStageId} 缺少 stageName。");
                    return false;
                }

                if (!seenStageNames.Add(stageName))
                {
                    report.AddError($"AgencyBuildings 存在重复 stageName: {stageName}。");
                    return false;
                }

                if (row.PromotionIds == null || row.PromotionIds.Length == 0)
                {
                    report.AddError($"AgencyBuildings {row.AgencyStageId} 缺少 promotionIds。");
                    return false;
                }

                if (row.PromotionLevelCaps == null || row.PromotionLevelCaps.Length != row.PromotionIds.Length)
                {
                    report.AddError($"AgencyBuildings {row.AgencyStageId} 的 promotionIds 与 promotionLevelCaps 长度不一致。");
                    return false;
                }

                if (row.PromotionUpgradeCosts == null || row.PromotionUpgradeCosts.Length != row.PromotionIds.Length)
                {
                    report.AddError($"AgencyBuildings {row.AgencyStageId} 的 promotionIds 与 promotionUpgradeCosts 长度不一致。");
                    return false;
                }

                if (row.PromotionIds.Length != 4)
                {
                    report.AddError($"AgencyBuildings {row.AgencyStageId} 的 promotionIds 数量必须为 4。");
                    return false;
                }

                if (row.PromotionLevelCaps.Any(cap => cap != 5))
                {
                    report.AddError($"AgencyBuildings {row.AgencyStageId} 的 promotionLevelCaps 必须全部为 5。");
                    return false;
                }

                var seenPromotionIds = new HashSet<string>(StringComparer.Ordinal);
                string[] expectedPromotionIds = { "leaflet", "radio", "online", "tv" };
                for (int promotionIndex = 0; promotionIndex < row.PromotionIds.Length; promotionIndex++)
                {
                    string promotionId = row.PromotionIds[promotionIndex] ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(promotionId))
                    {
                        report.AddError($"AgencyBuildings {row.AgencyStageId} 的第 {promotionIndex + 1} 个 promotionId 为空。");
                        return false;
                    }

                    if (!seenPromotionIds.Add(promotionId))
                    {
                        report.AddError($"AgencyBuildings {row.AgencyStageId} 存在重复 promotionId: {promotionId}。");
                        return false;
                    }

                    if (!string.Equals(promotionId, expectedPromotionIds[promotionIndex], StringComparison.Ordinal))
                    {
                        report.AddError($"AgencyBuildings {row.AgencyStageId} 的 promotionIds 顺序必须固定为 leaflet/radio/online/tv。");
                        return false;
                    }

                    int cap = row.PromotionLevelCaps[promotionIndex];
                    var costRow = row.PromotionUpgradeCosts[promotionIndex];
                    int[] costs = costRow?.Costs ?? Array.Empty<int>();
                    if (costs.Length != cap)
                    {
                        report.AddError($"AgencyBuildings {row.AgencyStageId} 的 promotion {promotionId} 成本档位数量与 cap 不一致。");
                        return false;
                    }

                    for (int costIndex = 0; costIndex < costs.Length; costIndex++)
                    {
                        if (costs[costIndex] <= 0)
                        {
                            report.AddError($"AgencyBuildings {row.AgencyStageId} 的 promotion {promotionId} 存在非正费用。");
                            return false;
                        }
                    }
                }

                agencyBuildings.Add(new HolmasAgencyBuildingRow
                {
                    AgencyStageId = row.AgencyStageId,
                    StageName = stageName,
                    PromotionIds = row.PromotionIds.ToArray(),
                    PromotionLevelCaps = row.PromotionLevelCaps.ToArray(),
                    PromotionUpgradeCosts = (row.PromotionUpgradeCosts ?? Array.Empty<HolmasAgencyBuildingCostRow>())
                        .Select(costRow => new HolmasAgencyBuildingCostRow
                        {
                            Costs = costRow?.Costs ?? Array.Empty<int>(),
                        })
                        .ToArray(),
                    Notes = row.Notes ?? string.Empty,
                });
            }

            return true;
        }

        private static bool TryValidateWeightedIndices(
            int[] indices,
            int[] weights,
            IReadOnlyDictionary<int, string> idByIndex,
            string context,
            HolmasConfigReport report)
        {
            if (indices == null || weights == null)
            {
                report.AddError($"{context} 的索引或权重为空。");
                return false;
            }

            if (indices.Length == 0 || weights.Length == 0)
            {
                report.AddError($"{context} 缺少可用项。");
                return false;
            }

            if (indices.Length != weights.Length)
            {
                report.AddError($"{context} 的索引和权重长度不一致。");
                return false;
            }

            for (int i = 0; i < indices.Length; i++)
            {
                if (weights[i] < 0)
                {
                    report.AddError($"{context} 的权重不能为负。");
                    return false;
                }

                if (!idByIndex.ContainsKey(indices[i]))
                {
                    report.AddError($"{context} 引用了不存在的索引: {indices[i]}。");
                    return false;
                }
            }

            return true;
        }

        private static string[] ResolveIds(int[] indices, IReadOnlyDictionary<int, string> idByIndex)
        {
            if (indices == null || indices.Length == 0)
            {
                return Array.Empty<string>();
            }

            var ids = new string[indices.Length];
            for (int i = 0; i < indices.Length; i++)
            {
                ids[i] = idByIndex.TryGetValue(indices[i], out string id) ? id : string.Empty;
            }

            return ids;
        }
    }
}
