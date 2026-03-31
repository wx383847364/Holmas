using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using App.HotUpdate.Holmas.Levels;
using App.HotUpdate.Holmas.Progression;
using App.HotUpdate.Holmas.Tasks.Config;
using App.HotUpdate.Holmas.Tasks.Runtime;
using App.HotUpdate.Holmas.Tasks.Services;
using App.Shared.Holmas.RuntimeData;
using NUnit.Framework;
using UnityEngine;

namespace Holmas.Tests
{
    public sealed class HolmasCsvConfigTests
    {
        [Test]
        public void HolmasCsvSampleTables_AreConsistentAndMoneyOnly()
        {
            CsvConfigTables tables = LoadSampleTables();
            IReadOnlyList<string> errors = CsvConfigValidator.Validate(tables);

            Assert.That(errors, Is.Empty, string.Join(Environment.NewLine, errors));
            Assert.That(tables.Cats, Has.Count.EqualTo(49));
            Assert.That(tables.Maps, Has.Count.EqualTo(3));
            Assert.That(tables.Tasks, Has.Count.EqualTo(3));
            Assert.That(tables.Levels, Has.Count.EqualTo(2));
            Assert.That(tables.Tasks.All(item => string.Equals(item.TaskKind, "Money", StringComparison.Ordinal)), Is.True);
            Assert.That(tables.Cats.All(item => item.Price > 0 && item.Weight > 0 && item.Rarity > 0), Is.True);
        }

        [Test]
        public void HolmasCsvSampleTables_CanDriveRuntimeGeneration()
        {
            CsvConfigTables tables = LoadSampleTables();

            HolmasTaskCatalog taskCatalog = BuildTaskCatalog(tables);
            HolmasMapCatalog mapCatalog = BuildMapCatalog(tables);
            var requestGenerator = new HolmasLevelRequestGenerator(taskCatalog, mapCatalog, new ScriptedRandomSource(0, 0, 0, 0, 0, 0));
            var catPool = BuildSpawnPool(tables.Cats.Take(4));

            HolmasLevelRequestGenerationResult requestResult = requestGenerator.TryGenerateForPlayerLevel(1, 99, catPool);

            Assert.That(requestResult.Success, Is.True, requestResult.FailureReason);
            Assert.That(requestResult.SelectedMapId, Is.EqualTo("map_001"));
            Assert.That(requestResult.Request.TerrainPath, Is.EqualTo("Assets/HotUpdateContent/Res/1.asset"));
            Assert.That(requestResult.Request.CatCountMin, Is.EqualTo(1));
            Assert.That(requestResult.Request.CatCountMax, Is.EqualTo(3));

            var terrain = HolmasTestSupport.CreateTerrain(2, 2, (_, _) => true);
            LevelSnapshot snapshot = LevelSnapshotFactory.CreateFromTerrain(terrain, requestResult.Request);

            Assert.That(snapshot.MapId, Is.EqualTo("map_001"));
            Assert.That(snapshot.TerrainPath, Is.EqualTo("Assets/HotUpdateContent/Res/1.asset"));
            Assert.That(snapshot.SpawnedCats.Count, Is.InRange(1, 3));
            Assert.That(snapshot.SpawnedCats.Select(item => item.CatId).All(catId => catPool.Any(pool => pool.CatId == catId)), Is.True);

            var taskService = new HolmasTaskProgressService(taskCatalog, new ScriptedRandomSource(0, 0, 0, 0, 0, 0), new FixedUtcClock { UtcNowMilliseconds = 1000 });
            HolmasTaskBarState taskBar = taskService.CreateDefaultTaskBarState();
            HolmasTaskRefillResult refill = taskService.RefillUnlockedEmptySlots(taskBar, 1);

            Assert.That(refill.GeneratedTasks.Count, Is.EqualTo(2));
            Assert.That(refill.GeneratedTasks.All(item => item.Success), Is.True);
            Assert.That(taskBar.GetActiveCatIds().Count, Is.EqualTo(taskBar.GetActiveCatIds().Distinct(StringComparer.Ordinal).Count()));

            foreach (HolmasTaskGenerationResult generated in refill.GeneratedTasks)
            {
                Assert.That(generated.Task, Is.Not.Null);
                CsvTaskRow taskRow = tables.Tasks.Single(item => string.Equals(item.TaskTypeId, generated.Task.SourceTaskTypeId, StringComparison.Ordinal));
                CsvCatRow catRow = tables.Cats.Single(item => string.Equals(item.CatId, generated.Task.CatId, StringComparison.Ordinal));
                int expectedReward = (int)Math.Round(catRow.Price * generated.Task.TargetCount * taskRow.LevelRewardFactor, MidpointRounding.AwayFromZero);
                Assert.That(generated.Task.Reward, Is.EqualTo(expectedReward));
            }
        }

        [Test]
        public void HolmasCsvSampleTables_CanRoundTripThroughBinaryCatalogFactory()
        {
            CsvConfigTables tables = LoadSampleTables();
            HolmasCoreConfigPackage corePackage = BuildCorePackage(tables);
            HolmasCatMetaPackage catPackage = BuildCatPackage(tables);

            byte[] coreBytes = HolmasConfigBinaryCodec.WriteCorePackage(corePackage);
            byte[] catBytes = HolmasConfigBinaryCodec.WriteCatMetaPackage(catPackage);

            bool success = HolmasConfigCatalogFactory.TryCreateFromBinary(coreBytes, catBytes, out HolmasConfigCatalogBundle bundle, out HolmasConfigReport report);

            Assert.That(success, Is.True, report == null ? "catalog build failed" : string.Join(Environment.NewLine, report.Errors));
            Assert.That(bundle, Is.Not.Null);
            Assert.That(bundle.Cats.Count, Is.EqualTo(tables.Cats.Count));
            Assert.That(bundle.TaskTemplates.Count, Is.EqualTo(tables.Tasks.Count));
            Assert.That(bundle.PlayerLevels.Count, Is.EqualTo(tables.Levels.Count));
            Assert.That(bundle.Maps.Count, Is.EqualTo(tables.Maps.Count));
            Assert.That(bundle.Report.Success, Is.True);
            Assert.That(bundle.Report.Errors, Is.Empty);
        }

        [Test]
        public void HolmasCsvValidator_RejectsMissingCatReference()
        {
            CsvConfigTables tables = LoadSampleTables();
            tables.Tasks[0].CatIdList = new[] { "999" };

            IReadOnlyList<string> errors = CsvConfigValidator.Validate(tables);

            Assert.That(errors.Any(item => item.Contains("找不到猫配置")), Is.True);
        }

        [Test]
        public void HolmasCsvValidator_RejectsWeightLengthMismatch()
        {
            CsvConfigTables tables = LoadSampleTables();
            tables.Levels[0].TaskTypeWeights = new[] { 100 };

            IReadOnlyList<string> errors = CsvConfigValidator.Validate(tables);

            Assert.That(errors.Any(item => item.Contains("长度不一致")), Is.True);
        }

        [Test]
        public void HolmasCsvValidator_RejectsMinGreaterThanMax()
        {
            CsvConfigTables tables = LoadSampleTables();
            tables.Maps[0].CatCountMin = 5;
            tables.Maps[0].CatCountMax = 3;

            IReadOnlyList<string> errors = CsvConfigValidator.Validate(tables);

            Assert.That(errors.Any(item => item.Contains("猫数范围非法")), Is.True);
        }

        [Test]
        public void HolmasCsvValidator_RejectsEmptyTerrainPath()
        {
            CsvConfigTables tables = LoadSampleTables();
            tables.Maps[1].TerrainPath = string.Empty;

            IReadOnlyList<string> errors = CsvConfigValidator.Validate(tables);

            Assert.That(errors.Any(item => item.Contains("TerrainPath")), Is.True);
        }

        [Test]
        public void HolmasCsvValidator_RejectsGambleTaskKind()
        {
            CsvConfigTables tables = LoadSampleTables();
            tables.Tasks[0].TaskKind = "Gamble";

            IReadOnlyList<string> errors = CsvConfigValidator.Validate(tables);

            Assert.That(errors.Any(item => item.Contains("Gamble")), Is.True);
        }

        private static CsvConfigTables LoadSampleTables()
        {
            return new CsvConfigTables
            {
                Cats = LoadCats("Holmas_CatTable.csv"),
                Maps = LoadMaps("Holmas_MapTable.csv"),
                Tasks = LoadTasks("Holmas_TaskTable.csv"),
                Levels = LoadLevels("Holmas_PlayerLevelTable.csv"),
            };
        }

        private static HolmasTaskCatalog BuildTaskCatalog(CsvConfigTables tables)
        {
            return new HolmasTaskCatalog(
                tables.Cats.Select(item => new HolmasCatDefinition
                {
                    CatId = item.CatId,
                    Price = item.Price,
                }),
                tables.Tasks.Select(item => new HolmasTaskTemplateDefinition
                {
                    TaskTypeId = item.TaskTypeId,
                    CatIdList = item.CatIdList.ToArray(),
                    CountMin = item.CountMin,
                    CountMax = item.CountMax,
                    RewardArray = item.RewardArray.ToArray(),
                    LevelRewardFactor = item.LevelRewardFactor,
                }),
                tables.Levels.Select(item => new HolmasPlayerLevelDefinition
                {
                    PlayerLevel = item.PlayerLevel,
                    UpgradeExp = item.UpgradeExp,
                    TaskTypeIds = item.TaskTypeIds.ToArray(),
                    TaskTypeWeights = item.TaskTypeWeights.ToArray(),
                    MapIds = item.MapIds.ToArray(),
                    MapWeights = item.MapWeights.ToArray(),
                }));
        }

        private static HolmasMapCatalog BuildMapCatalog(CsvConfigTables tables)
        {
            return new HolmasMapCatalog(
                tables.Maps.Select(item => new HolmasMapDefinition
                {
                    MapId = item.MapId,
                    TerrainPath = item.TerrainPath,
                    CatCountMin = item.CatCountMin,
                    CatCountMax = item.CatCountMax,
                }));
        }

        private static IReadOnlyList<BoardSpawnEntry> BuildSpawnPool(IEnumerable<CsvCatRow> cats)
        {
            return cats.Select(item => new BoardSpawnEntry
            {
                CatId = item.CatId,
                Weight = item.Weight,
            }).ToArray();
        }

        private static HolmasCoreConfigPackage BuildCorePackage(CsvConfigTables tables)
        {
            Dictionary<string, int> catIndexById = tables.Cats
                .Select((item, index) => new { item.CatId, Index = index })
                .ToDictionary(item => item.CatId, item => item.Index, StringComparer.Ordinal);
            Dictionary<string, int> taskIndexById = tables.Tasks
                .Select((item, index) => new { item.TaskTypeId, Index = index })
                .ToDictionary(item => item.TaskTypeId, item => item.Index, StringComparer.Ordinal);
            Dictionary<string, int> mapIndexById = tables.Maps
                .Select((item, index) => new { item.MapId, Index = index })
                .ToDictionary(item => item.MapId, item => item.Index, StringComparer.Ordinal);

            return new HolmasCoreConfigPackage
            {
                Maps = tables.Maps.Select(item => new HolmasMapRow
                {
                    MapId = item.MapId,
                    TerrainPath = item.TerrainPath,
                    CatCountMin = item.CatCountMin,
                    CatCountMax = item.CatCountMax,
                }).ToArray(),
                Tasks = tables.Tasks.Select(item => new HolmasTaskRow
                {
                    TaskTypeId = item.TaskTypeId,
                    TaskKind = ParseTaskKind(item.TaskKind),
                    CatIndices = item.CatIdList.Select(catId => catIndexById[catId]).ToArray(),
                    CountMin = item.CountMin,
                    CountMax = item.CountMax,
                    RewardValues = item.RewardArray.Select(ParseInt).ToArray(),
                    LevelRewardFactor = item.LevelRewardFactor,
                }).ToArray(),
                PlayerLevels = tables.Levels.Select(item => new HolmasPlayerLevelRow
                {
                    PlayerLevel = item.PlayerLevel,
                    UpgradeExp = item.UpgradeExp,
                    TaskTypeIndices = item.TaskTypeIds.Select(taskId => taskIndexById[taskId]).ToArray(),
                    TaskTypeWeights = item.TaskTypeWeights.ToArray(),
                    MapIndices = item.MapIds.Select(mapId => mapIndexById[mapId]).ToArray(),
                    MapWeights = item.MapWeights.ToArray(),
                }).ToArray(),
            };
        }

        private static HolmasCatMetaPackage BuildCatPackage(CsvConfigTables tables)
        {
            return new HolmasCatMetaPackage
            {
                Cats = tables.Cats.Select(item => new HolmasCatMetaRow
                {
                    CatId = item.CatId,
                    CatName = item.CatName,
                    IconPath = item.IconPath,
                    Rarity = item.Rarity,
                    Weight = item.Weight,
                    Price = item.Price,
                }).ToArray(),
            };
        }

        private static HolmasTaskKind ParseTaskKind(string value)
        {
            if (string.Equals(value, "Money", StringComparison.Ordinal))
            {
                return HolmasTaskKind.Money;
            }

            if (string.Equals(value, "Gamble", StringComparison.Ordinal))
            {
                return HolmasTaskKind.Gamble;
            }

            return HolmasTaskKind.Money;
        }

        private static List<CsvCatRow> LoadCats(string fileName)
        {
            var rows = ReadCsvTable(fileName);
            var result = new List<CsvCatRow>();
            foreach (var row in rows.Skip(2))
            {
                if (row.Length < 6)
                {
                    continue;
                }

                result.Add(new CsvCatRow
                {
                    CatId = row[0],
                    CatName = row[1],
                    IconPath = row[2],
                    Rarity = ParseInt(row[3]),
                    Weight = ParseInt(row[4]),
                    Price = ParseInt(row[5]),
                });
            }

            return result;
        }

        private static List<CsvMapRow> LoadMaps(string fileName)
        {
            var rows = ReadCsvTable(fileName);
            var result = new List<CsvMapRow>();
            foreach (var row in rows.Skip(2))
            {
                if (row.Length < 4)
                {
                    continue;
                }

                result.Add(new CsvMapRow
                {
                    MapId = row[0],
                    TerrainPath = row[1],
                    CatCountMax = ParseInt(row[2]),
                    CatCountMin = ParseInt(row[3]),
                });
            }

            return result;
        }

        private static List<CsvTaskRow> LoadTasks(string fileName)
        {
            var rows = ReadCsvTable(fileName);
            var result = new List<CsvTaskRow>();
            foreach (var row in rows.Skip(2))
            {
                if (row.Length < 7)
                {
                    continue;
                }

                result.Add(new CsvTaskRow
                {
                    TaskTypeId = row[0],
                    TaskKind = row[1],
                    CatIdList = SplitCsvList(row[2]),
                    CountMax = ParseInt(row[3]),
                    CountMin = ParseInt(row[4]),
                    RewardArray = SplitCsvList(row[5]),
                    LevelRewardFactor = ParseFloat(row[6]),
                });
            }

            return result;
        }

        private static List<CsvLevelRow> LoadLevels(string fileName)
        {
            var rows = ReadCsvTable(fileName);
            var result = new List<CsvLevelRow>();
            foreach (var row in rows.Skip(2))
            {
                if (row.Length < 6)
                {
                    continue;
                }

                result.Add(new CsvLevelRow
                {
                    PlayerLevel = ParseInt(row[0]),
                    UpgradeExp = ParseInt(row[1]),
                    TaskTypeIds = SplitCsvList(row[2]),
                    TaskTypeWeights = SplitIntList(row[3]),
                    MapIds = SplitCsvList(row[4]),
                    MapWeights = SplitIntList(row[5]),
                });
            }

            return result;
        }

        private static string[][] ReadCsvTable(string fileName)
        {
            string path = Path.Combine(Application.dataPath, "Config", fileName);
            Assert.That(File.Exists(path), Is.True, $"找不到配置文件: {path}");

            var lines = File.ReadAllLines(path)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(ParseCsvLine)
                .ToArray();
            Assert.That(lines.Length, Is.GreaterThanOrEqualTo(2), $"配置文件行数不足: {path}");
            return lines;
        }

        private static string[] ParseCsvLine(string line)
        {
            if (line == null)
            {
                return Array.Empty<string>();
            }

            var values = new List<string>();
            var current = new System.Text.StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char ch = line[i];
                if (ch == '\"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '\"')
                    {
                        current.Append('\"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (ch == ',' && !inQuotes)
                {
                    values.Add(TrimCell(current.ToString()));
                    current.Clear();
                }
                else
                {
                    current.Append(ch);
                }
            }

            values.Add(TrimCell(current.ToString()));
            return values.ToArray();
        }

        private static string TrimCell(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            string trimmed = value.Trim();
            if (trimmed.Length > 0 && trimmed[0] == '\uFEFF')
            {
                trimmed = trimmed.Substring(1);
            }

            if (trimmed.Length >= 2 && trimmed[0] == '\"' && trimmed[trimmed.Length - 1] == '\"')
            {
                trimmed = trimmed.Substring(1, trimmed.Length - 2);
            }

            return trimmed;
        }

        private static string[] SplitCsvList(string cell)
        {
            if (string.IsNullOrWhiteSpace(cell))
            {
                return Array.Empty<string>();
            }

            return cell.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Trim())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToArray();
        }

        private static int[] SplitIntList(string cell)
        {
            return SplitCsvList(cell)
                .Select(ParseInt)
                .ToArray();
        }

        private static int ParseInt(string cell)
        {
            int value;
            if (!int.TryParse(cell, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                return 0;
            }

            return value;
        }

        private static float ParseFloat(string cell)
        {
            float value;
            if (!float.TryParse(cell, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                return 0f;
            }

            return value;
        }

        private sealed class CsvConfigTables
        {
            public List<CsvCatRow> Cats { get; set; } = new List<CsvCatRow>();
            public List<CsvMapRow> Maps { get; set; } = new List<CsvMapRow>();
            public List<CsvTaskRow> Tasks { get; set; } = new List<CsvTaskRow>();
            public List<CsvLevelRow> Levels { get; set; } = new List<CsvLevelRow>();
        }

        private sealed class CsvCatRow
        {
            public string CatId = string.Empty;
            public string CatName = string.Empty;
            public string IconPath = string.Empty;
            public int Rarity;
            public int Weight;
            public int Price;
        }

        private sealed class CsvMapRow
        {
            public string MapId = string.Empty;
            public string TerrainPath = string.Empty;
            public int CatCountMax;
            public int CatCountMin;
        }

        private sealed class CsvTaskRow
        {
            public string TaskTypeId = string.Empty;
            public string TaskKind = string.Empty;
            public string[] CatIdList = Array.Empty<string>();
            public int CountMax;
            public int CountMin;
            public string[] RewardArray = Array.Empty<string>();
            public float LevelRewardFactor = 1f;
        }

        private sealed class CsvLevelRow
        {
            public int PlayerLevel;
            public int UpgradeExp;
            public string[] TaskTypeIds = Array.Empty<string>();
            public int[] TaskTypeWeights = Array.Empty<int>();
            public string[] MapIds = Array.Empty<string>();
            public int[] MapWeights = Array.Empty<int>();
        }

        private static class CsvConfigValidator
        {
            public static List<string> Validate(CsvConfigTables tables)
            {
                var errors = new List<string>();
                if (tables == null)
                {
                    errors.Add("配置表为空。");
                    return errors;
                }

                ValidateCats(tables.Cats, errors);
                ValidateMaps(tables.Maps, errors);
                ValidateTasks(tables.Tasks, tables.Cats, errors);
                ValidateLevels(tables.Levels, tables.Tasks, tables.Maps, errors);
                return errors;
            }

            private static void ValidateCats(IEnumerable<CsvCatRow> cats, List<string> errors)
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var cat in cats ?? Array.Empty<CsvCatRow>())
                {
                    if (cat == null)
                    {
                        errors.Add("猫表存在空行。");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(cat.CatId))
                    {
                        errors.Add("猫表存在空的 catId。");
                        continue;
                    }

                    if (!seen.Add(cat.CatId))
                    {
                        errors.Add($"猫表存在重复 catId: {cat.CatId}");
                    }

                    if (string.IsNullOrWhiteSpace(cat.CatName))
                    {
                        errors.Add($"猫表存在空的猫名称: {cat.CatId}");
                    }

                    if (cat.Rarity <= 0)
                    {
                        errors.Add($"猫表稀有度非法: {cat.CatId}");
                    }

                    if (cat.Weight <= 0)
                    {
                        errors.Add($"猫表权重非法: {cat.CatId}");
                    }

                    if (cat.Price <= 0)
                    {
                        errors.Add($"猫表价格非法: {cat.CatId}");
                    }
                }
            }

            private static void ValidateMaps(IEnumerable<CsvMapRow> maps, List<string> errors)
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var map in maps ?? Array.Empty<CsvMapRow>())
                {
                    if (map == null)
                    {
                        errors.Add("地图表存在空行。");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(map.MapId))
                    {
                        errors.Add("地图表存在空的 mapId。");
                        continue;
                    }

                    if (!seen.Add(map.MapId))
                    {
                        errors.Add($"地图表存在重复 mapId: {map.MapId}");
                    }

                    if (string.IsNullOrWhiteSpace(map.TerrainPath))
                    {
                        errors.Add($"地图配置缺少 TerrainPath: {map.MapId}");
                    }

                    if (map.CatCountMin < 0 || map.CatCountMax < map.CatCountMin)
                    {
                        errors.Add($"地图配置的猫数范围非法: {map.MapId}");
                    }
                }
            }

            private static void ValidateTasks(IEnumerable<CsvTaskRow> tasks, IReadOnlyList<CsvCatRow> cats, List<string> errors)
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                var catIds = new HashSet<string>((cats ?? Array.Empty<CsvCatRow>()).Where(item => item != null).Select(item => item.CatId), StringComparer.Ordinal);

                foreach (var task in tasks ?? Array.Empty<CsvTaskRow>())
                {
                    if (task == null)
                    {
                        errors.Add("任务表存在空行。");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(task.TaskTypeId))
                    {
                        errors.Add("任务表存在空的 taskTypeId。");
                        continue;
                    }

                    if (!seen.Add(task.TaskTypeId))
                    {
                        errors.Add($"任务表存在重复 taskTypeId: {task.TaskTypeId}");
                    }

                    if (!string.Equals(task.TaskKind, "Money", StringComparison.Ordinal))
                    {
                        errors.Add($"不支持的 taskKind={task.TaskKind}，当前阶段只允许 Money。");
                    }

                    if (task.CountMin < 0 || task.CountMax < task.CountMin)
                    {
                        errors.Add($"任务配置的数量范围非法: {task.TaskTypeId}");
                    }

                    if (task.CatIdList == null || task.CatIdList.Length == 0)
                    {
                        errors.Add($"任务配置缺少猫列表: {task.TaskTypeId}");
                    }
                    else
                    {
                        var uniqueCats = new HashSet<string>(StringComparer.Ordinal);
                        foreach (var catId in task.CatIdList)
                        {
                            if (string.IsNullOrWhiteSpace(catId))
                            {
                                errors.Add($"任务配置存在空猫ID: {task.TaskTypeId}");
                                continue;
                            }

                            if (!catIds.Contains(catId))
                            {
                                errors.Add($"找不到猫配置: {catId}");
                            }

                            if (!uniqueCats.Add(catId))
                            {
                                errors.Add($"任务配置中的猫ID重复: {task.TaskTypeId}/{catId}");
                            }
                        }
                    }

                    if (task.RewardArray != null)
                    {
                        foreach (var rewardCell in task.RewardArray)
                        {
                            if (string.IsNullOrWhiteSpace(rewardCell))
                            {
                                continue;
                            }

                            int rewardValue;
                            if (!int.TryParse(rewardCell, NumberStyles.Integer, CultureInfo.InvariantCulture, out rewardValue) || rewardValue < 0)
                            {
                                errors.Add($"任务奖励数组存在非法值: {task.TaskTypeId}");
                            }
                        }
                    }
                }
            }

            private static void ValidateLevels(IEnumerable<CsvLevelRow> levels, IReadOnlyList<CsvTaskRow> tasks, IReadOnlyList<CsvMapRow> maps, List<string> errors)
            {
                var taskIds = new HashSet<string>((tasks ?? Array.Empty<CsvTaskRow>()).Where(item => item != null).Select(item => item.TaskTypeId), StringComparer.Ordinal);
                var mapIds = new HashSet<string>((maps ?? Array.Empty<CsvMapRow>()).Where(item => item != null).Select(item => item.MapId), StringComparer.Ordinal);
                var seen = new HashSet<int>();

                foreach (var level in levels ?? Array.Empty<CsvLevelRow>())
                {
                    if (level == null)
                    {
                        errors.Add("玩家等级表存在空行。");
                        continue;
                    }

                    if (level.PlayerLevel <= 0)
                    {
                        errors.Add("玩家等级非法。");
                        continue;
                    }

                    if (!seen.Add(level.PlayerLevel))
                    {
                        errors.Add($"玩家等级存在重复值: {level.PlayerLevel}");
                    }

                    if (level.UpgradeExp < 0)
                    {
                        errors.Add($"升级经验非法: {level.PlayerLevel}");
                    }

                    if (level.TaskTypeIds == null || level.TaskTypeWeights == null || level.TaskTypeIds.Length != level.TaskTypeWeights.Length)
                    {
                        errors.Add($"玩家等级配置的任务标识和权重长度不一致: {level.PlayerLevel}");
                    }
                    else
                    {
                        ValidateWeightedIds(level.PlayerLevel, level.TaskTypeIds, level.TaskTypeWeights, taskIds, "任务", errors);
                    }

                    if (level.MapIds == null || level.MapWeights == null || level.MapIds.Length != level.MapWeights.Length)
                    {
                        errors.Add($"玩家等级配置的地图标识和权重长度不一致: {level.PlayerLevel}");
                    }
                    else
                    {
                        ValidateWeightedIds(level.PlayerLevel, level.MapIds, level.MapWeights, mapIds, "地图", errors);
                    }
                }
            }

            private static void ValidateWeightedIds(int playerLevel, IReadOnlyList<string> ids, IReadOnlyList<int> weights, HashSet<string> knownIds, string label, List<string> errors)
            {
                bool hasPositiveWeight = false;
                for (int i = 0; i < ids.Count; i++)
                {
                    string id = ids[i] ?? string.Empty;
                    int weight = weights[i];

                    if (string.IsNullOrWhiteSpace(id))
                    {
                        errors.Add($"玩家等级 {playerLevel} 的{label}配置存在空ID。");
                        continue;
                    }

                    if (weight < 0)
                    {
                        errors.Add($"玩家等级 {playerLevel} 的{label}权重不能为负: {id}");
                        continue;
                    }

                    if (weight > 0)
                    {
                        hasPositiveWeight = true;
                    }

                    if (!knownIds.Contains(id))
                    {
                        errors.Add($"找不到{label}配置: {id}");
                    }
                }

                if (!hasPositiveWeight)
                {
                    errors.Add($"玩家等级 {playerLevel} 的{label}权重没有任何正值。");
                }
            }
        }
    }
}
