#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using App.HotUpdate.Holmas.Tasks.Config;
using UnityEditor;
using UnityEngine;

namespace Holmas.EditorTools
{
    public static class HolmasConfigBinaryExporter
    {
        private const string ConfigRoot = "Assets/Config";
        private const string JsonRoot = "Assets/Config/json";
        private const string HotUpdateConfigRoot = "Assets/HotUpdateContent/Config";

        private const string MapTableName = "Holmas_MapTable.xlsx";
        private const string CatTableName = "Holmas_CatTable.xlsx";
        private const string TaskTableName = "Holmas_TaskTable.xlsx";
        private const string PlayerLevelTableName = "Holmas_PlayerLevelTable.xlsx";
        private const string AgencyBuildingTableName = "Holmas_AgencyBuildingTable.xlsx";

        private const string CoreBinaryName = "holmas_core_config.bytes";
        private const string CatBinaryName = "holmas_cat_meta.bytes";
        private const string CorePreviewName = "holmas_core_config.json";
        private const string CatPreviewName = "holmas_cat_meta.json";
        private const string ReportName = "holmas_export_report.json";

        [MenuItem("Holmas/配置/备用：Editor内Xlsx导出二进制")]
        public static void ExportFromMenu()
        {
            var report = ExportAll();
            string summary = report.Success
                ? $"备用 Editor 导出完成，已写入 {report.BinaryWrittenCount} 个二进制文件。"
                : $"备用 Editor 导出完成，但存在 {report.ErrorCount} 个错误，已阻止正式 bytes 覆盖。";

            if (report.Success)
            {
                Debug.Log(summary);
                EditorUtility.DisplayDialog("备用：Editor内Xlsx导出二进制", summary, "OK");
            }
            else
            {
                Debug.LogWarning(summary);
                EditorUtility.DisplayDialog("备用：Editor内Xlsx导出二进制", summary + "\n请查看 holmas_export_report.json。", "OK");
            }
        }

        public static void ExportFromBatchMode()
        {
            HolmasConfigExportReport report = ExportAll();
            if (!report.Success)
            {
                throw new InvalidOperationException("Xlsx导出二进制失败，请检查 holmas_export_report.json。");
            }
        }

        public static HolmasConfigExportReport ExportAll()
        {
            return ExportAll(ConfigRoot, JsonRoot, HotUpdateConfigRoot, refreshAssetDatabase: true);
        }

        public static HolmasConfigExportReport ExportAll(
            string configRoot,
            string jsonRoot,
            string hotUpdateConfigRoot,
            bool refreshAssetDatabase)
        {
            var report = new HolmasConfigExportReport
            {
                ExportedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                SourceFiles = new[]
                {
                    CombineProjectPath(configRoot, MapTableName),
                    CombineProjectPath(configRoot, CatTableName),
                    CombineProjectPath(configRoot, TaskTableName),
                    CombineProjectPath(configRoot, PlayerLevelTableName),
                    CombineProjectPath(configRoot, AgencyBuildingTableName),
                }
            };

            EnsureDirectory(CombineProjectPath(jsonRoot, string.Empty));
            EnsureDirectory(CombineProjectPath(hotUpdateConfigRoot, string.Empty));

            var mapTable = LoadMapTable(report, configRoot);
            var catTable = LoadCatTable(report, configRoot);
            var taskTable = LoadTaskTable(report, configRoot);
            var playerLevelTable = LoadPlayerLevelTable(report, configRoot);
            var agencyBuildingTable = LoadAgencyBuildingTable(report, configRoot);

            var catLookup = BuildAliasLookup(catTable.Rows.Select((row, index) => new KeyValuePair<string, int>(row.CatId, index)));
            var taskLookup = BuildAliasLookup(taskTable.Rows.Select((row, index) => new KeyValuePair<string, int>(row.TaskTypeId, index)));
            var mapLookup = BuildAliasLookup(mapTable.Rows.Select((row, index) => new KeyValuePair<string, int>(row.MapId, index)));

            NormalizeRows(report, catTable, catLookup, taskLookup, mapLookup);
            NormalizeRows(report, taskTable, catLookup, taskLookup, mapLookup);
            NormalizeRows(report, playerLevelTable, catLookup, taskLookup, mapLookup);
            NormalizeRows(report, mapTable, catLookup, taskLookup, mapLookup);
            WarnUnreferencedMaps(report, mapTable, playerLevelTable);
            ValidatePlayerLevelTable(report, playerLevelTable);
            ValidateAgencyBuildingTable(report, agencyBuildingTable);

            HolmasCoreConfigPackage corePackage = BuildCorePackage(mapTable, taskTable, playerLevelTable, agencyBuildingTable);
            HolmasCatMetaPackage catPackage = BuildCatMetaPackage(catTable);

            report.BundleReports = new[]
            {
                new HolmasConfigBundleReport
                {
                    BundleName = "core",
                    SourceTableNames = new[] { MapTableName, TaskTableName, PlayerLevelTableName, AgencyBuildingTableName },
                    PreviewJsonPath = CombineProjectPath(jsonRoot, CorePreviewName),
                    BinaryPath = CombineProjectPath(hotUpdateConfigRoot, CoreBinaryName),
                    RowCount = mapTable.Rows.Count + taskTable.Rows.Count + playerLevelTable.Rows.Count + agencyBuildingTable.Rows.Count,
                    WarningCount = report.Warnings.Count,
                    ErrorCount = report.Errors.Count,
                },
                new HolmasConfigBundleReport
                {
                    BundleName = "cat_meta",
                    SourceTableNames = new[] { CatTableName },
                    PreviewJsonPath = CombineProjectPath(jsonRoot, CatPreviewName),
                    BinaryPath = CombineProjectPath(hotUpdateConfigRoot, CatBinaryName),
                    RowCount = catTable.Rows.Count,
                    WarningCount = report.Warnings.Count,
                    ErrorCount = report.Errors.Count,
                }
            };

            File.WriteAllText(
                CombineProjectPath(jsonRoot, CorePreviewName),
                HolmasConfigBinaryCodec.ToCoreJson(corePackage, prettyPrint: true),
                new UTF8Encoding(true));
            File.WriteAllText(
                CombineProjectPath(jsonRoot, CatPreviewName),
                HolmasConfigBinaryCodec.ToCatMetaJson(catPackage, prettyPrint: true),
                new UTF8Encoding(true));

            bool hasFatalErrors = report.Errors.Count > 0;
            if (!hasFatalErrors)
            {
                File.WriteAllBytes(
                    CombineProjectPath(hotUpdateConfigRoot, CoreBinaryName),
                    HolmasConfigBinaryCodec.WriteCorePackage(corePackage));
                File.WriteAllBytes(
                    CombineProjectPath(hotUpdateConfigRoot, CatBinaryName),
                    HolmasConfigBinaryCodec.WriteCatMetaPackage(catPackage));
                report.BinaryWrittenCount = 2;
            }
            else
            {
                report.BinaryWrittenCount = 0;
            }

            report.Success = !hasFatalErrors;

            WriteJsonPreview(CombineProjectPath(jsonRoot, ReportName), report);
            if (refreshAssetDatabase)
            {
                AssetDatabase.Refresh();
            }
            return report;
        }

        private static SheetTable<HolmasMapSheetRow> LoadMapTable(HolmasConfigExportReport report, string configRoot)
        {
            string tableName = MapTableName;
            string path = CombineProjectPath(configRoot, tableName);
            if (!File.Exists(path))
            {
                report.Errors.Add($"找不到 xlsx 文件: {path}");
                return new SheetTable<HolmasMapSheetRow>(tableName);
            }

            List<string[]> rows = ReadWorksheetRows(report, path);
            if (rows.Count < 2)
            {
                report.Errors.Add($"xlsx 结构不完整: {path}");
                return new SheetTable<HolmasMapSheetRow>(tableName);
            }

            var fieldRow = rows[1];
            var headerMap = BuildHeaderMap(report, path, fieldRow);
            return new SheetTable<HolmasMapSheetRow>(tableName, ParseMaps(report, path, rows, headerMap));
        }

        private static SheetTable<HolmasCatSheetRow> LoadCatTable(HolmasConfigExportReport report, string configRoot)
        {
            string tableName = CatTableName;
            string path = CombineProjectPath(configRoot, tableName);
            if (!File.Exists(path))
            {
                report.Errors.Add($"找不到 xlsx 文件: {path}");
                return new SheetTable<HolmasCatSheetRow>(tableName);
            }

            List<string[]> rows = ReadWorksheetRows(report, path);
            if (rows.Count < 2)
            {
                report.Errors.Add($"xlsx 结构不完整: {path}");
                return new SheetTable<HolmasCatSheetRow>(tableName);
            }

            var headerMap = BuildHeaderMap(report, path, rows[1]);
            return new SheetTable<HolmasCatSheetRow>(tableName, ParseCats(report, path, rows, headerMap));
        }

        private static SheetTable<HolmasTaskSheetRow> LoadTaskTable(HolmasConfigExportReport report, string configRoot)
        {
            string tableName = TaskTableName;
            string path = CombineProjectPath(configRoot, tableName);
            if (!File.Exists(path))
            {
                report.Errors.Add($"找不到 xlsx 文件: {path}");
                return new SheetTable<HolmasTaskSheetRow>(tableName);
            }

            List<string[]> rows = ReadWorksheetRows(report, path);
            if (rows.Count < 2)
            {
                report.Errors.Add($"xlsx 结构不完整: {path}");
                return new SheetTable<HolmasTaskSheetRow>(tableName);
            }

            var headerMap = BuildHeaderMap(report, path, rows[1]);
            return new SheetTable<HolmasTaskSheetRow>(tableName, ParseTasks(report, path, rows, headerMap));
        }

        private static SheetTable<HolmasPlayerLevelSheetRow> LoadPlayerLevelTable(HolmasConfigExportReport report, string configRoot)
        {
            string tableName = PlayerLevelTableName;
            string path = CombineProjectPath(configRoot, tableName);
            if (!File.Exists(path))
            {
                report.Errors.Add($"找不到 xlsx 文件: {path}");
                return new SheetTable<HolmasPlayerLevelSheetRow>(tableName);
            }

            List<string[]> rows = ReadWorksheetRows(report, path);
            if (rows.Count < 2)
            {
                report.Errors.Add($"xlsx 结构不完整: {path}");
                return new SheetTable<HolmasPlayerLevelSheetRow>(tableName);
            }

            var headerMap = BuildHeaderMap(report, path, rows[1]);
            return new SheetTable<HolmasPlayerLevelSheetRow>(tableName, ParsePlayerLevels(report, path, rows, headerMap));
        }

        private static SheetTable<HolmasAgencyBuildingSheetRow> LoadAgencyBuildingTable(HolmasConfigExportReport report, string configRoot)
        {
            string tableName = AgencyBuildingTableName;
            string path = CombineProjectPath(configRoot, tableName);
            if (!File.Exists(path))
            {
                report.Errors.Add($"找不到 xlsx 文件: {path}");
                return new SheetTable<HolmasAgencyBuildingSheetRow>(tableName);
            }

            List<string[]> rows = ReadWorksheetRows(report, path);
            if (rows.Count < 2)
            {
                report.Errors.Add($"xlsx 结构不完整: {path}");
                return new SheetTable<HolmasAgencyBuildingSheetRow>(tableName);
            }

            var headerMap = BuildHeaderMap(report, path, rows[1]);
            return new SheetTable<HolmasAgencyBuildingSheetRow>(tableName, ParseAgencyBuildings(report, path, rows, headerMap));
        }

        private static List<HolmasMapSheetRow> ParseMaps(HolmasConfigExportReport report, string sourcePath, List<string[]> rows, Dictionary<string, int> headerMap)
        {
            int mapIdCol = RequireColumn(report, sourcePath, headerMap, "mapId");
            int terrainPathCol = RequireColumn(report, sourcePath, headerMap, "terrainPath");
            int catCountMaxCol = RequireColumn(report, sourcePath, headerMap, "catCountMax");
            int catCountMinCol = RequireColumn(report, sourcePath, headerMap, "catCountMin");

            var list = new List<HolmasMapSheetRow>();
            for (int rowIndex = 2; rowIndex < rows.Count; rowIndex++)
            {
                var row = rows[rowIndex];
                if (IsBlankRow(row))
                {
                    continue;
                }

                int catCountMax;
                bool maxOk = TryParseInt(GetCell(row, catCountMaxCol), out catCountMax);
                int catCountMin;
                bool minOk = TryParseInt(GetCell(row, catCountMinCol), out catCountMin);

                var item = new HolmasMapSheetRow
                {
                    RowIndex = list.Count,
                    MapId = GetCell(row, mapIdCol),
                    TerrainPath = GetCell(row, terrainPathCol),
                    CatCountMax = catCountMax,
                    CatCountMin = catCountMin,
                };

                if (string.IsNullOrWhiteSpace(item.MapId))
                {
                    report.Errors.Add($"{sourcePath} 第 {rowIndex + 1} 行缺少 mapId。");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(item.TerrainPath))
                {
                    report.Errors.Add($"{sourcePath} 第 {rowIndex + 1} 行缺少 terrainPath。");
                    continue;
                }

                if (!maxOk)
                {
                    report.Errors.Add($"{sourcePath} 第 {rowIndex + 1} 行 catCountMax 无法解析。");
                    continue;
                }

                if (!minOk)
                {
                    report.Errors.Add($"{sourcePath} 第 {rowIndex + 1} 行 catCountMin 无法解析。");
                    continue;
                }

                if (item.CatCountMin > item.CatCountMax)
                {
                    report.Errors.Add($"{sourcePath} 第 {rowIndex + 1} 行猫数量范围非法: min > max。");
                    continue;
                }

                list.Add(item);
            }

            return list;
        }

        private static List<HolmasCatSheetRow> ParseCats(HolmasConfigExportReport report, string sourcePath, List<string[]> rows, Dictionary<string, int> headerMap)
        {
            int catIdCol = RequireColumn(report, sourcePath, headerMap, "catId");
            int catNameCol = RequireColumn(report, sourcePath, headerMap, "catName");
            int iconPathCol = GetOptionalColumn(headerMap, "iconPath");
            int rarityCol = GetOptionalColumn(headerMap, "rarity");
            int weightCol = GetOptionalColumn(headerMap, "weight");
            int priceCol = GetOptionalColumn(headerMap, "price");

            bool hasAnyIconMissing = false;
            bool hasAnyRarityMissing = false;
            bool hasAnyWeightMissing = false;
            bool hasAnyPriceMissing = false;

            var list = new List<HolmasCatSheetRow>();
            for (int rowIndex = 2; rowIndex < rows.Count; rowIndex++)
            {
                var row = rows[rowIndex];
                if (IsBlankRow(row))
                {
                    continue;
                }

                var item = new HolmasCatSheetRow
                {
                    RowIndex = list.Count,
                    CatId = GetCell(row, catIdCol),
                    CatName = GetCell(row, catNameCol),
                    IconPath = GetCell(row, iconPathCol),
                    Rarity = ParseByte(GetCell(row, rarityCol), defaultValue: 0, out bool rarityOk),
                    Weight = ParseInt(GetCell(row, weightCol), defaultValue: 1, out bool weightOk),
                    Price = ParseInt(GetCell(row, priceCol), defaultValue: 0, out bool priceOk),
                };

                if (string.IsNullOrWhiteSpace(item.CatId))
                {
                    report.Errors.Add($"{sourcePath} 第 {rowIndex + 1} 行缺少 catId。");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(item.CatName))
                {
                    report.Errors.Add($"{sourcePath} 第 {rowIndex + 1} 行缺少 catName。");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(item.IconPath))
                {
                    hasAnyIconMissing = true;
                }

                if (!rarityOk)
                {
                    hasAnyRarityMissing = true;
                }

                if (!weightOk)
                {
                    hasAnyWeightMissing = true;
                }

                if (!priceOk)
                {
                    hasAnyPriceMissing = true;
                }

                list.Add(item);
            }

            if (hasAnyIconMissing)
            {
                report.Warnings.Add($"{sourcePath}: 有猫表行缺少 iconPath，已保留为空字符串。");
            }

            if (hasAnyRarityMissing)
            {
                report.Warnings.Add($"{sourcePath}: 有猫表行缺少 rarity，已默认填 0。");
            }

            if (hasAnyWeightMissing)
            {
                report.Warnings.Add($"{sourcePath}: 有猫表行缺少 weight，已默认填 1。");
            }

            if (hasAnyPriceMissing)
            {
                report.Warnings.Add($"{sourcePath}: 有猫表行缺少 price，已默认填 0。");
            }

            return list;
        }

        private static List<HolmasTaskSheetRow> ParseTasks(HolmasConfigExportReport report, string sourcePath, List<string[]> rows, Dictionary<string, int> headerMap)
        {
            int taskTypeIdCol = RequireColumn(report, sourcePath, headerMap, "taskTypeId");
            int taskKindCol = GetOptionalColumn(headerMap, "taskKind");
            int catIdListCol = RequireColumn(report, sourcePath, headerMap, "catIdList");
            int countMaxCol = RequireColumn(report, sourcePath, headerMap, "countMax");
            int countMinCol = RequireColumn(report, sourcePath, headerMap, "countMin");
            int rewardArrayCol = GetOptionalColumn(headerMap, "rewardArray");
            int levelRewardFactorCol = RequireColumn(report, sourcePath, headerMap, "levelRewardFactor");

            if (taskKindCol < 0)
            {
                report.Warnings.Add($"{sourcePath}: 缺少 taskKind 列，已默认全部视为 Money。");
            }

            var list = new List<HolmasTaskSheetRow>();
            for (int rowIndex = 2; rowIndex < rows.Count; rowIndex++)
            {
                var row = rows[rowIndex];
                if (IsBlankRow(row))
                {
                    continue;
                }

                string taskKindText = taskKindCol >= 0 ? GetCell(row, taskKindCol) : string.Empty;
                byte taskKindCode;
                string normalizedTaskKind = NormalizeTaskKind(taskKindText, out bool validTaskKind, out bool missingTaskKind);
                if (!validTaskKind)
                {
                    report.Errors.Add($"{sourcePath} 第 {rowIndex + 1} 行 taskKind 不受支持: {taskKindText}");
                    continue;
                }

                if (missingTaskKind)
                {
                    report.Warnings.Add($"{sourcePath}: 第 {rowIndex + 1} 行 taskKind 为空，已默认 Money。");
                }

                taskKindCode = GetTaskKindCode(normalizedTaskKind);

                int countMax;
                bool countMaxOk = TryParseInt(GetCell(row, countMaxCol), out countMax);
                int countMin;
                bool countMinOk = TryParseInt(GetCell(row, countMinCol), out countMin);
                float levelRewardFactor = ParseFloat(GetCell(row, levelRewardFactorCol), defaultValue: 1f, out bool factorOk);

                var item = new HolmasTaskSheetRow
                {
                    RowIndex = list.Count,
                    TaskTypeId = GetCell(row, taskTypeIdCol),
                    TaskKind = normalizedTaskKind,
                    TaskKindCode = taskKindCode,
                    CatIdList = SplitArray(GetCell(row, catIdListCol)),
                    CountMax = countMax,
                    CountMin = countMin,
                    RewardArray = ParseIntArray(GetCell(row, rewardArrayCol), sourcePath, rowIndex + 1, report),
                    LevelRewardFactor = levelRewardFactor,
                };

                if (string.IsNullOrWhiteSpace(item.TaskTypeId))
                {
                    report.Errors.Add($"{sourcePath} 第 {rowIndex + 1} 行缺少 taskTypeId。");
                    continue;
                }

                if (item.CatIdList.Length == 0)
                {
                    report.Errors.Add($"{sourcePath} 第 {rowIndex + 1} 行缺少 catIdList。");
                    continue;
                }

                if (!countMaxOk)
                {
                    report.Errors.Add($"{sourcePath} 第 {rowIndex + 1} 行 countMax 无法解析。");
                    continue;
                }

                if (!countMinOk)
                {
                    report.Errors.Add($"{sourcePath} 第 {rowIndex + 1} 行 countMin 无法解析。");
                    continue;
                }

                if (item.CountMin > item.CountMax)
                {
                    report.Errors.Add($"{sourcePath} 第 {rowIndex + 1} 行数量范围非法: min > max。");
                    continue;
                }

                if (!factorOk)
                {
                    report.Warnings.Add($"{sourcePath} 第 {rowIndex + 1} 行 levelRewardFactor 无法解析，已默认 1。");
                }

                if (item.RewardArray == null)
                {
                    item.RewardArray = Array.Empty<int>();
                }

                list.Add(item);
            }

            return list;
        }

        private static List<HolmasPlayerLevelSheetRow> ParsePlayerLevels(HolmasConfigExportReport report, string sourcePath, List<string[]> rows, Dictionary<string, int> headerMap)
        {
            int playerLevelCol = RequireColumn(report, sourcePath, headerMap, "playerLevel");
            int upgradeExpCol = GetOptionalColumn(headerMap, "upgradeExp");
            int minExperienceCol = GetOptionalColumn(headerMap, "minExperience");
            if (upgradeExpCol < 0)
            {
                upgradeExpCol = minExperienceCol;
            }
            int offlineRewardPerHourCol = RequireColumn(report, sourcePath, headerMap, "offlineRewardPerHour");
            int adUnlockHoursCol = RequireColumn(report, sourcePath, headerMap, "adUnlockHours");
            int notesCol = GetOptionalColumn(headerMap, "notes");
            int taskTypeIdsCol = RequireColumn(report, sourcePath, headerMap, "taskTypeIds");
            int taskTypeWeightsCol = RequireColumn(report, sourcePath, headerMap, "taskTypeWeights");
            int mapIdsCol = RequireColumn(report, sourcePath, headerMap, "mapIds");
            int mapWeightsCol = RequireColumn(report, sourcePath, headerMap, "mapWeights");

            if (upgradeExpCol < 0)
            {
                report.Errors.Add($"{sourcePath}: 缺少 upgradeExp/minExperience 列。Holmas_PlayerLevelTable 合并成长字段后必须提供升级门槛列。");
                return new List<HolmasPlayerLevelSheetRow>();
            }

            var list = new List<HolmasPlayerLevelSheetRow>();
            for (int rowIndex = 2; rowIndex < rows.Count; rowIndex++)
            {
                var row = rows[rowIndex];
                if (IsBlankRow(row))
                {
                    continue;
                }

                int playerLevel;
                bool levelOk = TryParseInt(GetCell(row, playerLevelCol), out playerLevel);
                int upgradeExp;
                bool upgradeExpOk = TryParseInt(GetCell(row, upgradeExpCol), out upgradeExp);
                int minExperience;
                bool minExperienceOk = TryParseInt(GetCell(row, minExperienceCol), out minExperience);
                int offlineRewardPerHour;
                bool offlineOk = TryParseInt(GetCell(row, offlineRewardPerHourCol), out offlineRewardPerHour);
                int adUnlockHours;
                bool adOk = TryParseInt(GetCell(row, adUnlockHoursCol), out adUnlockHours);

                var item = new HolmasPlayerLevelSheetRow
                {
                    RowIndex = list.Count,
                    PlayerLevel = playerLevel,
                    UpgradeExp = upgradeExp,
                    OfflineRewardPerHour = offlineRewardPerHour,
                    AdUnlockHours = adUnlockHours,
                    Notes = GetCell(row, notesCol),
                    TaskTypeIds = SplitArray(GetCell(row, taskTypeIdsCol)),
                    TaskTypeWeights = ParseIntArrayStrict(GetCell(row, taskTypeWeightsCol), sourcePath, rowIndex + 1, report),
                    MapIds = SplitArray(GetCell(row, mapIdsCol)),
                    MapWeights = ParseIntArrayStrict(GetCell(row, mapWeightsCol), sourcePath, rowIndex + 1, report),
                };

                if (!levelOk)
                {
                    report.Errors.Add($"{sourcePath} 第 {rowIndex + 1} 行 playerLevel 无法解析。");
                    continue;
                }

                if (!upgradeExpOk)
                {
                    report.Errors.Add($"{sourcePath} 第 {rowIndex + 1} 行 upgradeExp 无法解析。");
                    continue;
                }

                if (upgradeExpCol >= 0 && minExperienceCol >= 0)
                {
                    if (!minExperienceOk)
                    {
                        report.Errors.Add($"{sourcePath} 第 {rowIndex + 1} 行 minExperience 无法解析。");
                        continue;
                    }

                    if (upgradeExp != minExperience)
                    {
                        report.Errors.Add($"{sourcePath} 第 {rowIndex + 1} 行 upgradeExp 与 minExperience 不一致。");
                        continue;
                    }
                }

                if (!offlineOk)
                {
                    report.Errors.Add($"{sourcePath} 第 {rowIndex + 1} 行 offlineRewardPerHour 无法解析。");
                    continue;
                }

                if (!adOk)
                {
                    report.Errors.Add($"{sourcePath} 第 {rowIndex + 1} 行 adUnlockHours 无法解析。");
                    continue;
                }

                if (item.TaskTypeIds.Length == 0)
                {
                    report.Errors.Add($"{sourcePath} 第 {rowIndex + 1} 行 taskTypeIds 为空。");
                    continue;
                }

                if (item.TaskTypeIds.Length != item.TaskTypeWeights.Length)
                {
                    report.Errors.Add($"{sourcePath} 第 {rowIndex + 1} 行 taskTypeIds 与 taskTypeWeights 长度不一致。");
                    continue;
                }

                if (item.MapIds.Length == 0)
                {
                    report.Errors.Add($"{sourcePath} 第 {rowIndex + 1} 行 mapIds 为空。");
                    continue;
                }

                if (item.MapIds.Length != item.MapWeights.Length)
                {
                    report.Errors.Add($"{sourcePath} 第 {rowIndex + 1} 行 mapIds 与 mapWeights 长度不一致。");
                    continue;
                }

                list.Add(item);
            }

            return list;
        }

        private static List<HolmasAgencyBuildingSheetRow> ParseAgencyBuildings(HolmasConfigExportReport report, string sourcePath, List<string[]> rows, Dictionary<string, int> headerMap)
        {
            int stageIdCol = RequireColumn(report, sourcePath, headerMap, "agencyStageId");
            int stageNameCol = RequireColumn(report, sourcePath, headerMap, "stageName");
            int promotionIdsCol = RequireColumn(report, sourcePath, headerMap, "promotionIds");
            int promotionCapsCol = RequireColumn(report, sourcePath, headerMap, "promotionLevelCaps");
            int promotionCostsCol = RequireColumn(report, sourcePath, headerMap, "promotionUpgradeCosts");
            int notesCol = GetOptionalColumn(headerMap, "notes");

            var list = new List<HolmasAgencyBuildingSheetRow>();
            for (int rowIndex = 2; rowIndex < rows.Count; rowIndex++)
            {
                var row = rows[rowIndex];
                if (IsBlankRow(row))
                {
                    continue;
                }

                int stageId;
                bool stageIdOk = TryParseInt(GetCell(row, stageIdCol), out stageId);
                string stageName = GetCell(row, stageNameCol);
                string[] promotionIds = SplitArray(GetCell(row, promotionIdsCol));
                int[] levelCaps = ParseIntArrayStrict(GetCell(row, promotionCapsCol), sourcePath, rowIndex + 1, report);
                var upgradeCosts = ParseNestedIntArrays(GetCell(row, promotionCostsCol), sourcePath, rowIndex + 1, report);

                var item = new HolmasAgencyBuildingSheetRow
                {
                    RowIndex = list.Count,
                    AgencyStageId = stageId,
                    StageName = stageName,
                    PromotionIds = promotionIds,
                    PromotionLevelCaps = levelCaps,
                    PromotionUpgradeCosts = upgradeCosts,
                    Notes = GetCell(row, notesCol),
                };

                if (!stageIdOk || stageId <= 0)
                {
                    report.Errors.Add($"{sourcePath} 第 {rowIndex + 1} 行的 agencyStageId 必须是正整数。");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(item.StageName))
                {
                    report.Errors.Add($"{sourcePath} 第 {rowIndex + 1} 行缺少 stageName。");
                    continue;
                }

                int expectedStageId = list.Count + 1;
                if (stageId != expectedStageId)
                {
                    report.Errors.Add($"{sourcePath} 第 {rowIndex + 1} 行的 agencyStageId 必须按 1..N 连续递增，当前值为 {stageId}，期望值为 {expectedStageId}。");
                    continue;
                }

                if (item.PromotionIds.Length == 0)
                {
                    report.Errors.Add($"{sourcePath} 第 {rowIndex + 1} 行 promotionIds 为空。");
                    continue;
                }

                if (item.PromotionIds.Length != item.PromotionLevelCaps.Length)
                {
                    report.Errors.Add($"{sourcePath} 第 {rowIndex + 1} 行 promotionIds 与 promotionLevelCaps 长度不一致。");
                    continue;
                }

                if (item.PromotionIds.Length != item.PromotionUpgradeCosts.Length)
                {
                    report.Errors.Add($"{sourcePath} 第 {rowIndex + 1} 行 promotionIds 与 promotionUpgradeCosts 长度不一致。");
                    continue;
                }

                list.Add(item);
            }

            return list;
        }

        private static void ValidatePlayerLevelTable(
            HolmasConfigExportReport report,
            SheetTable<HolmasPlayerLevelSheetRow> playerLevelTable)
        {
            var playerRows = playerLevelTable?.Rows ?? new List<HolmasPlayerLevelSheetRow>();

            if (playerRows.Count == 0)
            {
                report.Errors.Add("缺少 Holmas_PlayerLevelTable 数据。");
                return;
            }

            var seenPlayerLevels = new HashSet<int>();
            int expectedPlayerLevel = 1;
            for (int i = 0; i < playerRows.Count; i++)
            {
                var playerRow = playerRows[i];
                if (playerRow == null)
                {
                    report.Errors.Add($"Holmas_PlayerLevelTable 第 {i + 1} 行为空。");
                    return;
                }

                if (playerRow.PlayerLevel != expectedPlayerLevel)
                {
                    report.Errors.Add($"Holmas_PlayerLevelTable 的 playerLevel 必须从 1 连续递增，当前第 {i + 1} 行是 {playerRow.PlayerLevel}。");
                    return;
                }

                expectedPlayerLevel++;

                if (!seenPlayerLevels.Add(playerRow.PlayerLevel))
                {
                    report.Errors.Add($"Holmas_PlayerLevelTable 存在重复 playerLevel: {playerRow.PlayerLevel}。");
                    return;
                }

                if (playerRow.UpgradeExp < 0)
                {
                    report.Errors.Add($"Holmas_PlayerLevelTable 的 upgradeExp 不能为负: level={playerRow.PlayerLevel}。");
                    return;
                }

                if (i > 0 && playerRows[i - 1] != null && playerRow.UpgradeExp <= playerRows[i - 1].UpgradeExp)
                {
                    report.Errors.Add($"Holmas_PlayerLevelTable 的 upgradeExp 必须严格递增: level={playerRow.PlayerLevel}。");
                    return;
                }

                if (playerRow.OfflineRewardPerHour < 0)
                {
                    report.Errors.Add($"Holmas_PlayerLevelTable 的 offlineRewardPerHour 不能为负: level={playerRow.PlayerLevel}。");
                    return;
                }

                if (playerRow.AdUnlockHours <= 0)
                {
                    report.Errors.Add($"Holmas_PlayerLevelTable 的 adUnlockHours 必须大于 0: level={playerRow.PlayerLevel}。");
                    return;
                }
            }
        }

        private static void ValidateAgencyBuildingTable(HolmasConfigExportReport report, SheetTable<HolmasAgencyBuildingSheetRow> agencyBuildingTable)
        {
            var rows = agencyBuildingTable?.Rows ?? new List<HolmasAgencyBuildingSheetRow>();
            if (rows.Count == 0)
            {
                report.Errors.Add("缺少 Holmas_AgencyBuildingTable 数据。");
                return;
            }

            if (rows.Count != 100)
            {
                report.Errors.Add($"Holmas_AgencyBuildingTable 行数必须为 100，当前为 {rows.Count}。");
                return;
            }

            var seenStageIds = new HashSet<int>();
            var seenStageNames = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row == null)
                {
                    report.Errors.Add($"Holmas_AgencyBuildingTable 第 {i + 1} 行为空。");
                    return;
                }

                if (row.AgencyStageId <= 0)
                {
                    report.Errors.Add($"Holmas_AgencyBuildingTable 第 {i + 1} 行的 agencyStageId 必须是正整数。");
                    return;
                }

                if (row.AgencyStageId != i + 1)
                {
                    report.Errors.Add($"Holmas_AgencyBuildingTable 的 agencyStageId 必须按 1..N 连续递增，当前第 {i + 1} 行为 {row.AgencyStageId}。");
                    return;
                }

                if (!seenStageIds.Add(row.AgencyStageId))
                {
                    report.Errors.Add($"Holmas_AgencyBuildingTable 存在重复 agencyStageId: {row.AgencyStageId}。");
                    return;
                }

                if (string.IsNullOrWhiteSpace(row.StageName))
                {
                    report.Errors.Add($"Holmas_AgencyBuildingTable {row.AgencyStageId} 缺少 stageName。");
                    return;
                }

                if (!seenStageNames.Add(row.StageName))
                {
                    report.Errors.Add($"Holmas_AgencyBuildingTable 存在重复 stageName: {row.StageName}。");
                    return;
                }

                if (row.PromotionIds == null || row.PromotionIds.Length == 0)
                {
                    report.Errors.Add($"Holmas_AgencyBuildingTable {row.AgencyStageId} 缺少 promotionIds。");
                    return;
                }

                if (row.PromotionLevelCaps == null || row.PromotionLevelCaps.Length != row.PromotionIds.Length)
                {
                    report.Errors.Add($"Holmas_AgencyBuildingTable {row.AgencyStageId} 的 promotionIds 与 promotionLevelCaps 长度不一致。");
                    return;
                }

                if (row.PromotionUpgradeCosts == null || row.PromotionUpgradeCosts.Length != row.PromotionIds.Length)
                {
                    report.Errors.Add($"Holmas_AgencyBuildingTable {row.AgencyStageId} 的 promotionIds 与 promotionUpgradeCosts 长度不一致。");
                    return;
                }
            }
        }

        private static void NormalizeRows(
            HolmasConfigExportReport report,
            SheetTable<HolmasMapSheetRow> mapTable,
            Dictionary<string, int> catLookup,
            Dictionary<string, int> taskLookup,
            Dictionary<string, int> mapLookup)
        {
            foreach (var row in mapTable.Rows)
            {
                row.MapIdIndex = ResolveIndex(row.MapId, mapLookup, out bool mapResolved);
                row.TerrainPathIndex = -1;
                if (!mapResolved)
                {
                    report.Errors.Add($"地图引用未解析: {row.MapId}。");
                }
            }
        }

        private static void NormalizeRows(
            HolmasConfigExportReport report,
            SheetTable<HolmasCatSheetRow> catTable,
            Dictionary<string, int> catLookup,
            Dictionary<string, int> taskLookup,
            Dictionary<string, int> mapLookup)
        {
            foreach (var row in catTable.Rows)
            {
                row.CatIdIndex = ResolveIndex(row.CatId, catLookup, out bool catResolved);
                row.CatNameIndex = -1;
                row.IconPathIndex = -1;
                if (!catResolved)
                {
                    report.Errors.Add($"猫引用未解析: {row.CatId}。");
                }
            }
        }

        private static void NormalizeRows(
            HolmasConfigExportReport report,
            SheetTable<HolmasTaskSheetRow> taskTable,
            Dictionary<string, int> catLookup,
            Dictionary<string, int> taskLookup,
            Dictionary<string, int> mapLookup)
        {
            var unresolvedCats = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in taskTable.Rows)
            {
                row.TaskTypeIdIndex = ResolveIndex(row.TaskTypeId, taskLookup, out bool taskResolved);
                row.CatIndices = new int[row.CatIdList.Length];
                for (int i = 0; i < row.CatIdList.Length; i++)
                {
                    int catIndex = ResolveIndex(row.CatIdList[i], catLookup, out bool catResolved);
                    row.CatIndices[i] = catIndex;
                    if (!catResolved)
                    {
                        unresolvedCats.Add(row.CatIdList[i]);
                    }
                }

                if (!taskResolved)
                {
                    report.Warnings.Add($"任务模板引用未解析: {row.TaskTypeId}。");
                }
            }

            if (unresolvedCats.Count > 0)
            {
                report.Errors.Add($"任务模板中存在未解析猫引用: {string.Join("; ", unresolvedCats.OrderBy(item => item))}。");
            }
        }

        private static void NormalizeRows(
            HolmasConfigExportReport report,
            SheetTable<HolmasPlayerLevelSheetRow> levelTable,
            Dictionary<string, int> catLookup,
            Dictionary<string, int> taskLookup,
            Dictionary<string, int> mapLookup)
        {
            var unresolvedTasks = new HashSet<string>(StringComparer.Ordinal);
            var unresolvedMaps = new HashSet<string>(StringComparer.Ordinal);

            foreach (var row in levelTable.Rows)
            {
                row.TaskTypeIndices = new int[row.TaskTypeIds.Length];
                for (int i = 0; i < row.TaskTypeIds.Length; i++)
                {
                    int taskIndex = ResolveIndex(row.TaskTypeIds[i], taskLookup, out bool taskResolved);
                    row.TaskTypeIndices[i] = taskIndex;
                    if (!taskResolved)
                    {
                        unresolvedTasks.Add(row.TaskTypeIds[i]);
                    }
                }

                row.MapIndices = new int[row.MapIds.Length];
                for (int i = 0; i < row.MapIds.Length; i++)
                {
                    int mapIndex = ResolveIndex(row.MapIds[i], mapLookup, out bool mapResolved);
                    row.MapIndices[i] = mapIndex;
                    if (!mapResolved)
                    {
                        unresolvedMaps.Add(row.MapIds[i]);
                    }
                }
            }

            if (unresolvedTasks.Count > 0)
            {
                report.Errors.Add($"玩家等级表中存在未解析任务引用: {string.Join("; ", unresolvedTasks.OrderBy(item => item))}。");
            }

            if (unresolvedMaps.Count > 0)
            {
                report.Errors.Add($"玩家等级表中存在未解析地图引用: {string.Join("; ", unresolvedMaps.OrderBy(item => item))}。");
            }
        }

        private static void WarnUnreferencedMaps(
            HolmasConfigExportReport report,
            SheetTable<HolmasMapSheetRow> mapTable,
            SheetTable<HolmasPlayerLevelSheetRow> playerLevelTable)
        {
            var mapRows = mapTable?.Rows ?? new List<HolmasMapSheetRow>();
            if (mapRows.Count == 0)
            {
                return;
            }

            var referencedMapIndices = new HashSet<int>();
            foreach (HolmasPlayerLevelSheetRow levelRow in playerLevelTable?.Rows ?? new List<HolmasPlayerLevelSheetRow>())
            {
                if (levelRow?.MapIndices == null)
                {
                    continue;
                }

                foreach (int mapIndex in levelRow.MapIndices)
                {
                    if (mapIndex >= 0)
                    {
                        referencedMapIndices.Add(mapIndex);
                    }
                }
            }

            var unreferencedMapIds = new List<string>();
            for (int i = 0; i < mapRows.Count; i++)
            {
                HolmasMapSheetRow mapRow = mapRows[i];
                if (mapRow == null || string.IsNullOrWhiteSpace(mapRow.MapId) || referencedMapIndices.Contains(i))
                {
                    continue;
                }

                unreferencedMapIds.Add(mapRow.MapId);
            }

            if (unreferencedMapIds.Count > 0)
            {
                report.Warnings.Add(
                    "MapTable 中存在未被任何 PlayerLevel 引用的地图: "
                    + string.Join("; ", unreferencedMapIds)
                    + "。");
            }
        }

        private static HolmasCoreConfigPackage BuildCorePackage(
            SheetTable<HolmasMapSheetRow> mapTable,
            SheetTable<HolmasTaskSheetRow> taskTable,
            SheetTable<HolmasPlayerLevelSheetRow> playerLevelTable,
            SheetTable<HolmasAgencyBuildingSheetRow> agencyBuildingTable)
        {
            return new HolmasCoreConfigPackage
            {
                Version = HolmasConfigBinaryFormat.CurrentVersion,
                Maps = mapTable.Rows.Select(row => new HolmasMapRow
                {
                    MapId = row.MapId,
                    TerrainPath = row.TerrainPath,
                    CatCountMin = row.CatCountMin,
                    CatCountMax = row.CatCountMax,
                }).ToArray(),
                Tasks = taskTable.Rows.Select(row => new HolmasTaskRow
                {
                    TaskTypeId = row.TaskTypeId,
                    TaskKind = (HolmasTaskKind)row.TaskKindCode,
                    CatIndices = row.CatIndices ?? Array.Empty<int>(),
                    CountMin = row.CountMin,
                    CountMax = row.CountMax,
                    RewardValues = row.RewardArray ?? Array.Empty<int>(),
                    LevelRewardFactor = row.LevelRewardFactor,
                }).ToArray(),
                PlayerLevels = playerLevelTable.Rows.Select(row => new HolmasPlayerLevelRow
                {
                    PlayerLevel = row.PlayerLevel,
                    UpgradeExp = row.UpgradeExp,
                    OfflineRewardPerHour = row.OfflineRewardPerHour,
                    AdUnlockHours = row.AdUnlockHours,
                    TaskTypeIndices = row.TaskTypeIndices ?? Array.Empty<int>(),
                    TaskTypeWeights = row.TaskTypeWeights ?? Array.Empty<int>(),
                    MapIndices = row.MapIndices ?? Array.Empty<int>(),
                    MapWeights = row.MapWeights ?? Array.Empty<int>(),
                }).ToArray(),
                AgencyBuildings = (agencyBuildingTable?.Rows ?? new List<HolmasAgencyBuildingSheetRow>()).Select(row => new HolmasAgencyBuildingRow
                {
                    AgencyStageId = row.AgencyStageId,
                    StageName = row.StageName ?? string.Empty,
                    PromotionIds = row.PromotionIds ?? Array.Empty<string>(),
                    PromotionLevelCaps = row.PromotionLevelCaps ?? Array.Empty<int>(),
                    PromotionUpgradeCosts = (row.PromotionUpgradeCosts ?? Array.Empty<HolmasAgencyBuildingCostSheetRow>())
                        .Select(costRow => new HolmasAgencyBuildingCostRow
                        {
                            Costs = costRow?.Costs ?? Array.Empty<int>(),
                        })
                        .ToArray(),
                    Notes = row.Notes ?? string.Empty,
                }).ToArray(),
            };
        }

        private static HolmasCatMetaPackage BuildCatMetaPackage(SheetTable<HolmasCatSheetRow> catTable)
        {
            return new HolmasCatMetaPackage
            {
                Version = HolmasConfigBinaryFormat.CurrentVersion,
                Cats = catTable.Rows.Select(row => new HolmasCatMetaRow
                {
                    CatId = row.CatId,
                    CatName = row.CatName,
                    IconPath = row.IconPath ?? string.Empty,
                    Rarity = row.Rarity,
                    Weight = row.Weight,
                    Price = row.Price,
                }).ToArray(),
            };
        }

        private static Dictionary<string, int> BuildAliasLookup(IEnumerable<KeyValuePair<string, int>> items)
        {
            var lookup = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var item in items)
            {
                foreach (var candidate in GetLookupCandidates(item.Key))
                {
                    if (!lookup.ContainsKey(candidate))
                    {
                        lookup[candidate] = item.Value;
                    }
                }
            }

            return lookup;
        }

        private static int ResolveIndex(string value, Dictionary<string, int> lookup, out bool resolved)
        {
            resolved = false;
            if (string.IsNullOrWhiteSpace(value) || lookup == null)
            {
                return -1;
            }

            foreach (var candidate in GetLookupCandidates(value))
            {
                if (lookup.TryGetValue(candidate, out int index))
                {
                    resolved = true;
                    return index;
                }
            }

            return -1;
        }

        private static IEnumerable<string> GetLookupCandidates(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                yield break;
            }

            string trimmed = value.Trim();
            yield return trimmed;

            int digitStart = trimmed.Length;
            while (digitStart > 0 && char.IsDigit(trimmed[digitStart - 1]))
            {
                digitStart--;
            }

            if (digitStart < trimmed.Length)
            {
                string suffix = trimmed.Substring(digitStart).TrimStart('0');
                if (suffix.Length == 0)
                {
                    suffix = "0";
                }

                yield return suffix;
            }
        }

        private static Dictionary<string, int> BuildHeaderMap(HolmasConfigExportReport report, string sourcePath, string[] headerRow)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (headerRow == null)
            {
                report.Errors.Add($"{sourcePath} 缺少字段名行。");
                return map;
            }

            for (int i = 0; i < headerRow.Length; i++)
            {
                string header = headerRow[i] == null ? string.Empty : headerRow[i].Trim();
                if (string.IsNullOrWhiteSpace(header))
                {
                    continue;
                }

                if (map.ContainsKey(header))
                {
                    report.Errors.Add($"{sourcePath} 存在重复字段名: {header}");
                    continue;
                }

                map[header] = i;
            }

            return map;
        }

        private static List<string[]> ReadWorksheetRows(HolmasConfigExportReport report, string sourcePath)
        {
            if (HolmasXlsxTableReader.TryReadFirstWorksheet(sourcePath, out List<string[]> rows, out string error))
            {
                return rows;
            }

            if (!string.IsNullOrWhiteSpace(error))
            {
                report.Errors.Add(error);
            }

            return new List<string[]>();
        }

        private static int RequireColumn(HolmasConfigExportReport report, string sourcePath, Dictionary<string, int> headerMap, string columnName)
        {
            int index = GetOptionalColumn(headerMap, columnName);
            if (index < 0)
            {
                report.Errors.Add($"{sourcePath} 缺少必要列: {columnName}");
            }

            return index;
        }

        private static int GetOptionalColumn(Dictionary<string, int> headerMap, string columnName)
        {
            if (headerMap != null && headerMap.TryGetValue(columnName, out int index))
            {
                return index;
            }

            return -1;
        }

        private static string GetCell(string[] row, int index)
        {
            if (row == null || index < 0 || index >= row.Length)
            {
                return string.Empty;
            }

            return row[index]?.Trim() ?? string.Empty;
        }

        private static bool IsBlankRow(string[] row)
        {
            if (row == null || row.Length == 0)
            {
                return true;
            }

            for (int i = 0; i < row.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(row[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryParseInt(string text, out int value)
        {
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static int ParseInt(string text, int defaultValue, out bool ok)
        {
            int value;
            ok = int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
            return ok ? value : defaultValue;
        }

        private static byte ParseByte(string text, byte defaultValue, out bool ok)
        {
            byte value;
            ok = byte.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
            return ok ? value : defaultValue;
        }

        private static float ParseFloat(string text, float defaultValue, out bool ok)
        {
            float value;
            ok = float.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out value);
            return ok ? value : defaultValue;
        }

        private static int[] ParseIntArray(string text, string sourcePath, int rowNumber, HolmasConfigExportReport report)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return Array.Empty<int>();
            }

            var parts = SplitArray(text);
            if (parts.Length == 0)
            {
                return Array.Empty<int>();
            }

            var values = new List<int>(parts.Length);
            for (int i = 0; i < parts.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(parts[i]))
                {
                    continue;
                }

                if (!int.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                {
                    report.Errors.Add($"{sourcePath} 第 {rowNumber} 行数组值无法解析为整数: {parts[i]}");
                    continue;
                }

                values.Add(value);
            }

            return values.ToArray();
        }

        private static int[] ParseIntArrayStrict(string text, string sourcePath, int rowNumber, HolmasConfigExportReport report)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return Array.Empty<int>();
            }

            var parts = SplitArray(text);
            var values = new List<int>(parts.Length);
            for (int i = 0; i < parts.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(parts[i]))
                {
                    report.Errors.Add($"{sourcePath} 第 {rowNumber} 行数组存在空值。");
                    return Array.Empty<int>();
                }

                if (!int.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                {
                    report.Errors.Add($"{sourcePath} 第 {rowNumber} 行数组值无法解析为整数: {parts[i]}");
                    return Array.Empty<int>();
                }

                values.Add(value);
            }

            return values.ToArray();
        }

        private static bool TryParseLong(string text, out long value)
        {
            return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static HolmasAgencyBuildingCostSheetRow[] ParseNestedIntArrays(string text, string sourcePath, int rowNumber, HolmasConfigExportReport report)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return Array.Empty<HolmasAgencyBuildingCostSheetRow>();
            }

            var segments = text
                .Split(new[] { '|', '｜' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Trim())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToArray();

            var rows = new List<HolmasAgencyBuildingCostSheetRow>(segments.Length);
            for (int i = 0; i < segments.Length; i++)
            {
                string segment = segments[i];
                string[] parts = segment
                    .Split(new[] { ';', '；' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(item => item.Trim())
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .ToArray();

                var costs = new List<int>(parts.Length);
                for (int j = 0; j < parts.Length; j++)
                {
                    if (!int.TryParse(parts[j], NumberStyles.Integer, CultureInfo.InvariantCulture, out int cost))
                    {
                        report.Errors.Add($"{sourcePath} 第 {rowNumber} 行第 {i + 1} 段升级费用无法解析为整数: {parts[j]}");
                        continue;
                    }

                    costs.Add(cost);
                }

                rows.Add(new HolmasAgencyBuildingCostSheetRow
                {
                    Costs = costs.ToArray(),
                });
            }

            return rows.ToArray();
        }

        private static string[] SplitArray(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return Array.Empty<string>();
            }

            return text
                .Split(new[] { ';', '；' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Trim())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToArray();
        }

        private static string NormalizeTaskKind(string text, out bool valid, out bool missing)
        {
            string normalized = string.IsNullOrWhiteSpace(text) ? "Money" : text.Trim();
            missing = string.IsNullOrWhiteSpace(text);

            if (string.Equals(normalized, "Money", StringComparison.OrdinalIgnoreCase))
            {
                valid = true;
                return "Money";
            }

            if (string.Equals(normalized, "Gamble", StringComparison.OrdinalIgnoreCase))
            {
                valid = false;
                return normalized;
            }

            valid = false;
            return normalized;
        }

        private static byte GetTaskKindCode(string taskKind)
        {
            if (string.Equals(taskKind, "Gamble", StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            return 0;
        }

        private static void EnsureDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        private static void WriteJsonPreview<T>(string path, T value)
        {
            EnsureDirectory(Path.GetDirectoryName(path));
            string json = JsonUtility.ToJson(value, true);
            File.WriteAllText(path, json, new UTF8Encoding(false));
        }

        private static string CombineProjectPath(string first, string second)
        {
            if (string.IsNullOrEmpty(second))
            {
                return NormalizePath(first);
            }

            string combined = string.IsNullOrEmpty(first) ? second : Path.Combine(first, second);
            return NormalizePath(combined);
        }

        private static string NormalizePath(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace('\\', '/');
        }
    }

    [Serializable]
    public sealed class HolmasConfigExportReport
    {
        public string ExportedAtUtc;
        public bool Success;
        public int BinaryWrittenCount;
        public string[] SourceFiles;
        public HolmasConfigBundleReport[] BundleReports;
        public List<string> Errors = new List<string>();
        public List<string> Warnings = new List<string>();

        public int ErrorCount
        {
            get { return Errors == null ? 0 : Errors.Count; }
        }

        public int WarningCount
        {
            get { return Warnings == null ? 0 : Warnings.Count; }
        }
    }

    [Serializable]
    public sealed class HolmasConfigBundleReport
    {
        public string BundleName;
        public string[] SourceTableNames;
        public string PreviewJsonPath;
        public string BinaryPath;
        public int RowCount;
        public int WarningCount;
        public int ErrorCount;
    }

    [Serializable]
    public sealed class HolmasCorePreviewBundle
    {
        public int Version;
        public string ExportedAtUtc;
        public HolmasMapSheetRow[] Maps;
        public HolmasTaskSheetRow[] Tasks;
        public HolmasPlayerLevelSheetRow[] PlayerLevels;
        public HolmasAgencyBuildingSheetRow[] AgencyBuildings;
    }

    [Serializable]
    public sealed class HolmasCatPreviewBundle
    {
        public int Version;
        public string ExportedAtUtc;
        public HolmasCatSheetRow[] Cats;
    }

    [Serializable]
    public sealed class HolmasMapSheetRow
    {
        public int RowIndex;
        public string MapId;
        public string TerrainPath;
        public int CatCountMin;
        public int CatCountMax;
        public int MapIdIndex = -1;
        public int TerrainPathIndex = -1;
    }

    [Serializable]
    public sealed class HolmasCatSheetRow
    {
        public int RowIndex;
        public string CatId;
        public string CatName;
        public string IconPath;
        public byte Rarity;
        public int Weight;
        public int Price;
        public int CatIdIndex = -1;
        public int CatNameIndex = -1;
        public int IconPathIndex = -1;
    }

    [Serializable]
    public sealed class HolmasTaskSheetRow
    {
        public int RowIndex;
        public string TaskTypeId;
        public string TaskKind;
        public byte TaskKindCode;
        public string[] CatIdList;
        public int[] CatIndices;
        public int CountMin;
        public int CountMax;
        public int[] RewardArray;
        public float LevelRewardFactor;
        public int TaskTypeIdIndex = -1;
    }

    [Serializable]
    public sealed class HolmasPlayerLevelSheetRow
    {
        public int RowIndex;
        public int PlayerLevel;
        public int UpgradeExp;
        public int OfflineRewardPerHour;
        public int AdUnlockHours;
        public string Notes;
        public string[] TaskTypeIds;
        public int[] TaskTypeIndices;
        public int[] TaskTypeWeights;
        public string[] MapIds;
        public int[] MapIndices;
        public int[] MapWeights;
    }

    [Serializable]
    public sealed class HolmasAgencyBuildingCostSheetRow
    {
        public int[] Costs;
    }

    [Serializable]
    public sealed class HolmasAgencyBuildingSheetRow
    {
        public int RowIndex;
        public int AgencyStageId;
        public string StageName;
        public string[] PromotionIds;
        public int[] PromotionLevelCaps;
        public HolmasAgencyBuildingCostSheetRow[] PromotionUpgradeCosts;
        public string Notes;
    }

    [Serializable]
    internal sealed class SheetTable<T>
    {
        public SheetTable(string name)
        {
            Name = name;
            Rows = new List<T>();
        }

        public SheetTable(string name, List<T> rows)
            : this(name)
        {
            if (rows != null)
            {
                Rows = rows;
            }
        }

        public string Name { get; private set; }
        public List<T> Rows { get; private set; }
    }
}
#endif
