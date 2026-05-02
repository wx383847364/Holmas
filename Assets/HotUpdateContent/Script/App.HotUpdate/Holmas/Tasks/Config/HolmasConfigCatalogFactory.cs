using System;
using System.Collections.Generic;
using System.Linq;

namespace App.HotUpdate.Holmas.Tasks.Config
{
    /// <summary>
    /// 从导出包恢复运行时 Catalog 的工厂。
    /// 这里负责把表镜像配置恢复为运行时 Catalog，同时保持导出协议命名不被业务包装反向改写。
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

            if (!TryBuildCats(catMetaPackage.Holmas_CatTable, out var cats, report))
            {
                return false;
            }

            if (!TryBuildMaps(corePackage.Holmas_MapTable, out var maps, report))
            {
                return false;
            }

            if (!TryBuildTasks(corePackage.Holmas_TaskTable, cats, out var tasks, report))
            {
                return false;
            }

            if (!TryBuildPlayerLevels(
                corePackage.Holmas_PlayerLevelTable,
                tasks,
                maps,
                out var playerLevels,
                report))
            {
                return false;
            }

            if (!TryBuildAgencyBuildingTable(corePackage.Holmas_AgencyBuildingTable, out var holmasAgencyBuildingTable, report))
            {
                return false;
            }

            if (!TryBuildLeaderboards(corePackage.Holmas_LeaderboardTable, out var leaderboards, report))
            {
                return false;
            }

            var mapCatalog = new HolmasMapCatalog(maps);
            var taskCatalog = new HolmasTaskCatalog(cats, tasks, playerLevels);
            bundle = new HolmasConfigCatalogBundle(mapCatalog, taskCatalog, cats, maps, tasks, playerLevels, holmasAgencyBuildingTable, leaderboards, report);
            report.MarkSuccess("配置包已成功恢复为运行时 Catalog。");
            return true;
        }

        private static bool TryBuildCats(
            IReadOnlyList<HolmasCatTableRow> rows,
            out List<HolmasCatDefinition> cats,
            HolmasConfigReport report)
        {
            cats = new List<HolmasCatDefinition>();

            if (rows == null || rows.Count == 0)
            {
                report.AddError("Holmas_CatTable 没有任何猫条目。");
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

                string catId = row.catId ?? string.Empty;
                if (string.IsNullOrWhiteSpace(catId))
                {
                    report.AddError($"Holmas_CatTable 第 {i} 行缺少 catId。");
                    return false;
                }

                if (!seenCatIds.Add(catId))
                {
                    report.AddError($"猫元数据存在重复 CatId: {catId}。");
                    return false;
                }

                if (string.IsNullOrWhiteSpace(row.catName))
                {
                    report.AddError($"Holmas_CatTable 缺少 catName: {catId}。");
                    return false;
                }

                if (string.IsNullOrWhiteSpace(row.iconPath))
                {
                    report.AddWarning($"Holmas_CatTable 缺少 iconPath: {catId}。当前阶段先保留为空字符串。");
                }

                if (row.weight < 0)
                {
                    report.AddError($"猫权重不能为负: {catId}。");
                    return false;
                }

                if (row.price < 0)
                {
                    report.AddError($"猫价格不能为负: {catId}。");
                    return false;
                }

                if (row.rarity < 0)
                {
                    report.AddError($"猫稀有度不能为负: {catId}。");
                    return false;
                }

                cats.Add(new HolmasCatDefinition
                {
                    CatIndex = i,
                    CatId = catId,
                    CatName = row.catName,
                    IconPath = row.iconPath,
                    Rarity = row.rarity,
                    Weight = row.weight,
                    Price = row.price,
                });
            }

            return true;
        }

        private static bool TryBuildMaps(
            IReadOnlyList<HolmasMapTableRow> rows,
            out List<HolmasMapDefinition> maps,
            HolmasConfigReport report)
        {
            maps = new List<HolmasMapDefinition>();

            if (rows == null || rows.Count == 0)
            {
                report.AddError("Holmas_MapTable 没有任何地图条目。");
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

                string mapId = row.mapId ?? string.Empty;
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

                if (string.IsNullOrWhiteSpace(row.terrainPath))
                {
                    report.AddError($"Holmas_MapTable 缺少 terrainPath: {mapId}。");
                    return false;
                }

                if (row.catCountMin < 0 || row.catCountMax < row.catCountMin)
                {
                    report.AddError($"地图配置的猫数范围非法: {mapId}。");
                    return false;
                }

                maps.Add(new HolmasMapDefinition
                {
                    MapIndex = i,
                    MapId = mapId,
                    TerrainPath = row.terrainPath,
                    CatCountMin = row.catCountMin,
                    CatCountMax = row.catCountMax,
                });
            }

            return true;
        }

        private static bool TryBuildTasks(
            IReadOnlyList<HolmasTaskTableRow> rows,
            IReadOnlyList<HolmasCatDefinition> cats,
            out List<HolmasTaskTemplateDefinition> tasks,
            HolmasConfigReport report)
        {
            tasks = new List<HolmasTaskTemplateDefinition>();

            if (rows == null || rows.Count == 0)
            {
                report.AddError("Holmas_TaskTable 没有任何任务条目。");
                return false;
            }

            var seenTaskTypeIds = new HashSet<string>(StringComparer.Ordinal);
            var knownCatIds = new HashSet<string>((cats ?? Array.Empty<HolmasCatDefinition>()).Select(item => item.CatId), StringComparer.Ordinal);
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row == null)
                {
                    report.AddError($"任务配置第 {i} 行为空。");
                    return false;
                }

                string taskTypeId = row.taskTypeId ?? string.Empty;
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

                if (row.taskKind != HolmasTaskKind.Money)
                {
                    report.AddError($"当前阶段不支持的任务类型: {taskTypeId} ({row.taskKind})。");
                    return false;
                }

                if (row.catIdList == null || row.catIdList.Length == 0)
                {
                    report.AddError($"Holmas_TaskTable 缺少 catIdList: {taskTypeId}。");
                    return false;
                }

                if (row.countMin < 0 || row.countMax < row.countMin)
                {
                    report.AddError($"任务配置的数量范围非法: {taskTypeId}。");
                    return false;
                }

                if (row.levelRewardFactor < 0f)
                {
                    report.AddError($"任务配置的等级奖励系数不能为负: {taskTypeId}。");
                    return false;
                }

                var catIds = row.catIdList ?? Array.Empty<string>();
                for (int j = 0; j < catIds.Length; j++)
                {
                    string catId = catIds[j] ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(catId) || !knownCatIds.Contains(catId))
                    {
                        report.AddError($"任务 {taskTypeId} 引用了不存在的猫 ID: {catId}。");
                        return false;
                    }
                }

                var rewardValues = row.rewardArray ?? Array.Empty<int>();
                tasks.Add(new HolmasTaskTemplateDefinition
                {
                    TaskIndex = i,
                    TaskTypeId = taskTypeId,
                    TaskKind = row.taskKind,
                    CatIdList = catIds,
                    CatIndices = Array.Empty<int>(),
                    CountMin = row.countMin,
                    CountMax = row.countMax,
                    RewardArray = Array.ConvertAll(rewardValues, value => value.ToString()),
                    RewardValues = rewardValues.ToArray(),
                    LevelRewardFactor = row.levelRewardFactor,
                });
            }

            return true;
        }

        private static bool TryBuildPlayerLevels(
            IReadOnlyList<HolmasPlayerLevelTableRow> rows,
            IReadOnlyList<HolmasTaskTemplateDefinition> tasks,
            IReadOnlyList<HolmasMapDefinition> maps,
            out List<HolmasPlayerLevelDefinition> playerLevels,
            HolmasConfigReport report)
        {
            playerLevels = new List<HolmasPlayerLevelDefinition>();

            if (rows == null || rows.Count == 0)
            {
                report.AddError("Holmas_PlayerLevelTable 没有任何玩家等级条目。");
                return false;
            }

            var seenLevels = new HashSet<int>();
            var taskTypeIds = new HashSet<string>((tasks ?? Array.Empty<HolmasTaskTemplateDefinition>()).Select(item => item.TaskTypeId), StringComparer.Ordinal);
            var mapIds = new HashSet<string>((maps ?? Array.Empty<HolmasMapDefinition>()).Select(item => item.MapId), StringComparer.Ordinal);
            int expectedLevel = 1;

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row == null)
                {
                    report.AddError($"玩家等级配置第 {i} 行为空。");
                    return false;
                }

                if (row.playerLevel != expectedLevel)
                {
                    report.AddError($"玩家等级必须从 1 连续递增，当前第 {i + 1} 行是 {row.playerLevel}。");
                    return false;
                }

                expectedLevel++;

                if (row.playerLevel < 0)
                {
                    report.AddError($"玩家等级不能为负: {row.playerLevel}。");
                    return false;
                }

                if (!seenLevels.Add(row.playerLevel))
                {
                    report.AddError($"玩家等级存在重复定义: {row.playerLevel}。");
                    return false;
                }

                if (row.minExperience < 0)
                {
                    report.AddError($"玩家等级的 minExperience 不能为负: {row.playerLevel}。");
                    return false;
                }

                if (i > 0 && row.minExperience <= rows[i - 1].minExperience)
                {
                    report.AddError($"玩家等级的 minExperience 必须严格递增: {row.playerLevel}。");
                    return false;
                }

                if (row.offlineRewardPerHour < 0)
                {
                    report.AddError($"玩家等级的 offlineRewardPerHour 不能为负: {row.playerLevel}。");
                    return false;
                }

                if (row.adUnlockHours <= 0)
                {
                    report.AddError($"玩家等级的 adUnlockHours 必须大于 0: {row.playerLevel}。");
                    return false;
                }

                if (!TryValidateWeightedIds(row.taskTypeIds, row.taskTypeWeights, taskTypeIds, $"玩家等级 {row.playerLevel} 的任务组", report))
                {
                    return false;
                }

                if (!TryValidateWeightedIds(row.mapIds, row.mapWeights, mapIds, $"玩家等级 {row.playerLevel} 的地图组", report))
                {
                    return false;
                }

                playerLevels.Add(new HolmasPlayerLevelDefinition
                {
                    PlayerLevelIndex = i,
                    PlayerLevel = row.playerLevel,
                    UpgradeExp = row.minExperience,
                    OfflineRewardPerHour = row.offlineRewardPerHour,
                    AdUnlockHours = row.adUnlockHours,
                    TaskTypeIds = row.taskTypeIds ?? Array.Empty<string>(),
                    TaskTypeWeights = row.taskTypeWeights ?? Array.Empty<int>(),
                    TaskTypeIndices = Array.Empty<int>(),
                    MapIds = row.mapIds ?? Array.Empty<string>(),
                    MapWeights = row.mapWeights ?? Array.Empty<int>(),
                    MapIndices = Array.Empty<int>(),
                });
            }

            return true;
        }

        private static bool TryBuildAgencyBuildingTable(
            IReadOnlyList<HolmasAgencyBuildingTableRow> rows,
            out List<HolmasAgencyBuildingTableRow> holmasAgencyBuildingTable,
            HolmasConfigReport report)
        {
            holmasAgencyBuildingTable = new List<HolmasAgencyBuildingTableRow>();

            if (rows == null || rows.Count == 0)
            {
                report.AddError("核心配置包缺少 Holmas_AgencyBuildingTable。");
                return false;
            }

            var seenStageIds = new HashSet<int>();
            var seenStageNames = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row == null)
                {
                    report.AddError($"Holmas_AgencyBuildingTable 第 {i + 1} 行为空。");
                    return false;
                }

                if (row.agencyStageId <= 0)
                {
                    report.AddError($"Holmas_AgencyBuildingTable 第 {i + 1} 行的 agencyStageId 必须是正整数。");
                    return false;
                }

                if (row.agencyStageId != i + 1)
                {
                    report.AddError($"Holmas_AgencyBuildingTable 的 agencyStageId 必须按 1..N 连续递增，当前第 {i + 1} 行为 {row.agencyStageId}。");
                    return false;
                }

                if (!seenStageIds.Add(row.agencyStageId))
                {
                    report.AddError($"Holmas_AgencyBuildingTable 存在重复 agencyStageId: {row.agencyStageId}。");
                    return false;
                }

                string stageName = row.stageName ?? string.Empty;
                if (string.IsNullOrWhiteSpace(stageName))
                {
                    report.AddError($"Holmas_AgencyBuildingTable {row.agencyStageId} 缺少 stageName。");
                    return false;
                }

                if (!seenStageNames.Add(stageName))
                {
                    report.AddError($"Holmas_AgencyBuildingTable 存在重复 stageName: {stageName}。");
                    return false;
                }

                if (row.promotionIds == null || row.promotionIds.Length == 0)
                {
                    report.AddError($"Holmas_AgencyBuildingTable {row.agencyStageId} 缺少 promotionIds。");
                    return false;
                }

                if (row.promotionLevelCaps == null || row.promotionLevelCaps.Length != row.promotionIds.Length)
                {
                    report.AddError($"Holmas_AgencyBuildingTable {row.agencyStageId} 的 promotionIds 与 promotionLevelCaps 长度不一致。");
                    return false;
                }

                if (row.promotionUpgradeCosts == null || row.promotionUpgradeCosts.Length != row.promotionIds.Length)
                {
                    report.AddError($"Holmas_AgencyBuildingTable {row.agencyStageId} 的 promotionIds 与 promotionUpgradeCosts 长度不一致。");
                    return false;
                }

                for (int promotionIndex = 0; promotionIndex < row.promotionIds.Length; promotionIndex++)
                {
                    string promotionId = row.promotionIds[promotionIndex] ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(promotionId))
                    {
                        report.AddError($"Holmas_AgencyBuildingTable {row.agencyStageId} 的第 {promotionIndex + 1} 个 promotionId 为空。");
                        return false;
                    }

                    int levelCap = row.promotionLevelCaps[promotionIndex];
                    if (levelCap <= 0)
                    {
                        report.AddError($"Holmas_AgencyBuildingTable {row.agencyStageId}/{promotionId} 的 promotionLevelCaps 必须大于 0。");
                        return false;
                    }

                    int[] costs = row.promotionUpgradeCosts[promotionIndex]?.costs ?? Array.Empty<int>();
                    if (costs.Length != levelCap)
                    {
                        report.AddError($"Holmas_AgencyBuildingTable {row.agencyStageId}/{promotionId} 的 promotionUpgradeCosts 长度必须等于 cap={levelCap}，当前为 {costs.Length}。");
                        return false;
                    }

                    if (costs.Any(cost => cost <= 0))
                    {
                        report.AddError($"Holmas_AgencyBuildingTable {row.agencyStageId}/{promotionId} 的升级费用必须全部大于 0。");
                        return false;
                    }
                }

                holmasAgencyBuildingTable.Add(new HolmasAgencyBuildingTableRow
                {
                    agencyStageId = row.agencyStageId,
                    stageName = stageName,
                    promotionIds = row.promotionIds.ToArray(),
                    promotionLevelCaps = row.promotionLevelCaps.ToArray(),
                    promotionUpgradeCosts = (row.promotionUpgradeCosts ?? Array.Empty<HolmasAgencyBuildingTableCostRow>())
                        .Select(costRow => new HolmasAgencyBuildingTableCostRow
                        {
                            costs = costRow?.costs ?? Array.Empty<int>(),
                        })
                        .ToArray(),
                    notes = row.notes ?? string.Empty,
                });
            }

            return true;
        }

        private static bool TryBuildLeaderboards(
            IReadOnlyList<HolmasLeaderboardTableRow> rows,
            out List<HolmasLeaderboardDefinition> leaderboards,
            HolmasConfigReport report)
        {
            leaderboards = new List<HolmasLeaderboardDefinition>();

            if (rows == null || rows.Count == 0)
            {
                report.AddError("缺少 Holmas_LeaderboardTable。");
                return false;
            }

            var seenTypes = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row == null)
                {
                    report.AddError($"Holmas_LeaderboardTable 第 {i + 1} 行为空。");
                    return false;
                }

                string leaderboardType = row.leaderboardType ?? string.Empty;
                if (string.IsNullOrWhiteSpace(leaderboardType))
                {
                    report.AddError($"Holmas_LeaderboardTable 第 {i + 1} 行缺少 leaderboardType。");
                    return false;
                }

                if (!seenTypes.Add(leaderboardType))
                {
                    report.AddError($"Holmas_LeaderboardTable 存在重复 leaderboardType: {leaderboardType}。");
                    return false;
                }

                if (string.IsNullOrWhiteSpace(row.displayName))
                {
                    report.AddError($"Holmas_LeaderboardTable {leaderboardType} 缺少 displayName。");
                    return false;
                }

                if (!Enum.TryParse(row.periodType ?? string.Empty, true, out HolmasLeaderboardPeriodType periodType))
                {
                    report.AddError($"Holmas_LeaderboardTable {leaderboardType} 的 periodType 非法: {row.periodType}。");
                    return false;
                }

                string timeZoneId = row.timeZoneId ?? string.Empty;
                if (string.IsNullOrWhiteSpace(timeZoneId))
                {
                    report.AddError($"Holmas_LeaderboardTable {leaderboardType} 缺少 timeZoneId。");
                    return false;
                }

                if (row.resetDayOfWeek < 0 || row.resetDayOfWeek > 7)
                {
                    report.AddError($"Holmas_LeaderboardTable {leaderboardType} 的 resetDayOfWeek 必须在 0..7 内。");
                    return false;
                }

                if (row.resetHour < 0 || row.resetHour > 23)
                {
                    report.AddError($"Holmas_LeaderboardTable {leaderboardType} 的 resetHour 必须在 0..23 内。");
                    return false;
                }

                if (row.resetMinute < 0 || row.resetMinute > 59)
                {
                    report.AddError($"Holmas_LeaderboardTable {leaderboardType} 的 resetMinute 必须在 0..59 内。");
                    return false;
                }

                if (row.topEntryCount <= 0)
                {
                    report.AddError($"Holmas_LeaderboardTable {leaderboardType} 的 topEntryCount 必须大于 0。");
                    return false;
                }

                if (row.mockEntryCount < row.topEntryCount)
                {
                    report.AddError($"Holmas_LeaderboardTable {leaderboardType} 的 mockEntryCount 不能小于 topEntryCount。");
                    return false;
                }

                leaderboards.Add(new HolmasLeaderboardDefinition
                {
                    LeaderboardIndex = i,
                    LeaderboardType = leaderboardType,
                    DisplayName = row.displayName,
                    PeriodType = periodType,
                    TimeZoneId = timeZoneId,
                    ResetDayOfWeek = row.resetDayOfWeek,
                    ResetHour = row.resetHour,
                    ResetMinute = row.resetMinute,
                    TopEntryCount = row.topEntryCount,
                    MockEntryCount = row.mockEntryCount,
                    IsEnabled = row.isEnabled,
                    Notes = row.notes ?? string.Empty,
                });
            }

            return true;
        }

        private static bool TryValidateWeightedIds(
            string[] ids,
            int[] weights,
            HashSet<string> knownIds,
            string context,
            HolmasConfigReport report)
        {
            if (ids == null || weights == null)
            {
                report.AddError($"{context} 的 ID 或权重为空。");
                return false;
            }

            if (ids.Length == 0 || weights.Length == 0)
            {
                report.AddError($"{context} 缺少可用项。");
                return false;
            }

            if (ids.Length != weights.Length)
            {
                report.AddError($"{context} 的 ID 和权重长度不一致。");
                return false;
            }

            for (int i = 0; i < ids.Length; i++)
            {
                if (weights[i] < 0)
                {
                    report.AddError($"{context} 的权重不能为负。");
                    return false;
                }

                string id = ids[i] ?? string.Empty;
                if (string.IsNullOrWhiteSpace(id) || knownIds == null || !knownIds.Contains(id))
                {
                    report.AddError($"{context} 引用了不存在的 ID: {id}。");
                    return false;
                }
            }

            return true;
        }
    }
}
