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

            var mapCatalog = new HolmasMapCatalog(maps);
            var taskCatalog = new HolmasTaskCatalog(cats, tasks, playerLevels);
            bundle = new HolmasConfigCatalogBundle(mapCatalog, taskCatalog, cats, maps, tasks, playerLevels, report);
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
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row == null)
                {
                    report.AddError($"玩家等级配置第 {i} 行为空。");
                    return false;
                }

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
