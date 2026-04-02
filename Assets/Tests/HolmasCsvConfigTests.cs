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
            Assert.That(tables.Tasks, Has.Count.EqualTo(20));
            Assert.That(tables.Levels, Has.Count.EqualTo(20));
            Assert.That(tables.MetaLevels, Has.Count.EqualTo(20));
            Assert.That(tables.AgencyBuildings, Has.Count.EqualTo(4));
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
            Assert.That(requestResult.Request.TerrainPath, Is.EqualTo("Assets/HotUpdateContent/Res/Map/1.asset"));
            Assert.That(requestResult.Request.CatCountMin, Is.EqualTo(12));
            Assert.That(requestResult.Request.CatCountMax, Is.EqualTo(15));

            var terrain = HolmasTestSupport.CreateTerrain(5, 5, (_, _) => true);
            LevelSnapshot snapshot = LevelSnapshotFactory.CreateFromTerrain(terrain, requestResult.Request);

            Assert.That(snapshot.MapId, Is.EqualTo("map_001"));
            Assert.That(snapshot.TerrainPath, Is.EqualTo("Assets/HotUpdateContent/Res/Map/1.asset"));
            Assert.That(snapshot.SpawnedCats.Count, Is.InRange(12, 15));
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
            Assert.That(bundle.MetaLevels.Count, Is.EqualTo(tables.MetaLevels.Count));
            Assert.That(bundle.AgencyBuildings.Count, Is.EqualTo(tables.AgencyBuildings.Count));
            Assert.That(bundle.Report.Success, Is.True);
            Assert.That(bundle.Report.Errors, Is.Empty);
        }

        [Test]
        public void HolmasConfigCatalogFactory_RejectsMissingMetaLevels()
        {
            CsvConfigTables tables = LoadSampleTables();
            HolmasCoreConfigPackage corePackage = BuildCorePackage(tables);
            HolmasCatMetaPackage catPackage = BuildCatPackage(tables);
            corePackage.MetaLevels = Array.Empty<HolmasMetaLevelRow>();

            byte[] coreBytes = HolmasConfigBinaryCodec.WriteCorePackage(corePackage);
            byte[] catBytes = HolmasConfigBinaryCodec.WriteCatMetaPackage(catPackage);

            bool success = HolmasConfigCatalogFactory.TryCreateFromBinary(coreBytes, catBytes, out _, out HolmasConfigReport report);

            Assert.That(success, Is.False);
            Assert.That(report.Errors.Any(item => item.Contains("缺少 MetaLevels")), Is.True, string.Join(Environment.NewLine, report.Errors));
        }

        [Test]
        public void HolmasConfigCatalogFactory_RejectsMissingAgencyBuildings()
        {
            CsvConfigTables tables = LoadSampleTables();
            HolmasCoreConfigPackage corePackage = BuildCorePackage(tables);
            HolmasCatMetaPackage catPackage = BuildCatPackage(tables);
            corePackage.AgencyBuildings = Array.Empty<HolmasAgencyBuildingRow>();

            byte[] coreBytes = HolmasConfigBinaryCodec.WriteCorePackage(corePackage);
            byte[] catBytes = HolmasConfigBinaryCodec.WriteCatMetaPackage(catPackage);

            bool success = HolmasConfigCatalogFactory.TryCreateFromBinary(coreBytes, catBytes, out _, out HolmasConfigReport report);

            Assert.That(success, Is.False);
            Assert.That(report.Errors.Any(item => item.Contains("缺少 AgencyBuildings")), Is.True, string.Join(Environment.NewLine, report.Errors));
        }

        [Test]
        public void HolmasConfigCatalogFactory_RejectsTruncatedMetaLevels()
        {
            CsvConfigTables tables = LoadSampleTables();
            HolmasCoreConfigPackage corePackage = BuildCorePackage(tables);
            HolmasCatMetaPackage catPackage = BuildCatPackage(tables);
            corePackage.MetaLevels = corePackage.MetaLevels.Take(corePackage.MetaLevels.Length - 1).ToArray();

            byte[] coreBytes = HolmasConfigBinaryCodec.WriteCorePackage(corePackage);
            byte[] catBytes = HolmasConfigBinaryCodec.WriteCatMetaPackage(catPackage);

            bool success = HolmasConfigCatalogFactory.TryCreateFromBinary(coreBytes, catBytes, out _, out HolmasConfigReport report);

            Assert.That(success, Is.False);
            Assert.That(report.Errors.Any(item => item.Contains("行数不一致")), Is.True, string.Join(Environment.NewLine, report.Errors));
        }

        [Test]
        public void HolmasCsvSampleTables_FollowRarityBandsAndMapUnlockPlan()
        {
            CsvConfigTables tables = LoadSampleTables();

            Assert.That(tables.Cats.Count(item => item.Rarity == 1), Is.EqualTo(20));
            Assert.That(tables.Cats.Count(item => item.Rarity == 2), Is.EqualTo(14));
            Assert.That(tables.Cats.Count(item => item.Rarity == 3), Is.EqualTo(10));
            Assert.That(tables.Cats.Count(item => item.Rarity == 4), Is.EqualTo(5));

            int[] expectedUpgradeExp = { 100, 120, 140, 160, 180, 210, 240, 270, 300, 340, 380, 420, 470, 520, 580, 640, 710, 780, 860, 950 };
            Assert.That(tables.Levels.Select(item => item.UpgradeExp), Is.EqualTo(expectedUpgradeExp));
            Assert.That(tables.MetaLevels.Select(item => item.MinExperience).ToArray(), Is.EqualTo(expectedUpgradeExp.Select(value => (long)value).ToArray()));
            Assert.That(tables.MetaLevels.Select(item => item.PlayerLevel).ToArray(), Is.EqualTo(Enumerable.Range(1, 20).ToArray()));
            Assert.That(tables.AgencyBuildings.Select(item => item.AgencyStageId), Is.EqualTo(new[] { 1, 2, 3, 4 }));

            foreach (CsvLevelRow level in tables.Levels)
            {
                Assert.That(level.MapWeights.Sum(), Is.EqualTo(100), $"等级 {level.PlayerLevel} 的地图权重和应为 100。");

                if (level.PlayerLevel <= 2)
                {
                    Assert.That(level.MapIds, Is.EqualTo(new[] { "map_001" }), $"等级 {level.PlayerLevel} 只应开放 map_001。");
                }
                else if (level.PlayerLevel <= 4)
                {
                    Assert.That(level.MapIds, Is.EqualTo(new[] { "map_001", "map_002" }), $"等级 {level.PlayerLevel} 应开放 map_001/map_002。");
                }
                else
                {
                    Assert.That(level.MapIds, Is.EqualTo(new[] { "map_001", "map_002", "map_003" }), $"等级 {level.PlayerLevel} 应开放全部 3 张地图。");
                }
            }
        }

        [Test]
        public void HolmasCsvSampleTables_AllLevelsCanGenerateRequestsAndFillFiveUniqueTasks()
        {
            CsvConfigTables tables = LoadSampleTables();
            HolmasTaskCatalog taskCatalog = BuildTaskCatalog(tables);
            HolmasMapCatalog mapCatalog = BuildMapCatalog(tables);
            var requestGenerator = new HolmasLevelRequestGenerator(taskCatalog, mapCatalog, new ScriptedRandomSource());

            foreach (CsvLevelRow levelRow in tables.Levels)
            {
                var taskService = new HolmasTaskProgressService(taskCatalog, new ScriptedRandomSource(), new FixedUtcClock { UtcNowMilliseconds = 1000 });
                HolmasTaskBarState taskBar = taskService.CreateDefaultTaskBarState();

                HolmasLevelRequestGenerationResult requestResult = requestGenerator.TryGenerateForPlayerLevel(levelRow.PlayerLevel, levelRow.PlayerLevel * 17);
                Assert.That(requestResult.Success, Is.True, $"等级 {levelRow.PlayerLevel} 地图请求生成失败：{requestResult.FailureReason}");
                Assert.That(levelRow.MapIds, Contains.Item(requestResult.SelectedMapId), $"等级 {levelRow.PlayerLevel} 抽到了未开放地图。");

                CsvMapRow selectedMap = tables.Maps.Single(item => string.Equals(item.MapId, requestResult.SelectedMapId, StringComparison.Ordinal));
                Assert.That(requestResult.Request.CatCountMin, Is.EqualTo(selectedMap.CatCountMin));
                Assert.That(requestResult.Request.CatCountMax, Is.EqualTo(selectedMap.CatCountMax));

                HolmasTaskRefillResult refill = taskService.RefillUnlockedEmptySlots(taskBar, levelRow.PlayerLevel);
                Assert.That(refill.GeneratedTasks.Count, Is.EqualTo(2), $"等级 {levelRow.PlayerLevel} 默认应补满 2 个槽位。");
                Assert.That(refill.GeneratedTasks.All(item => item.Success), Is.True, $"等级 {levelRow.PlayerLevel} 默认槽位补任务失败。");

                for (int slotIndex = taskBar.DefaultOpenSlots; slotIndex < taskBar.TotalSlots; slotIndex++)
                {
                    HolmasTaskSlotUnlockResult unlock = taskService.UnlockAdSlot(taskBar, slotIndex, levelRow.PlayerLevel, 10_000L + slotIndex);
                    Assert.That(unlock.Success, Is.True, $"等级 {levelRow.PlayerLevel} 解锁槽位 {slotIndex} 失败：{unlock.FailureReason}");
                    Assert.That(unlock.GeneratedTask, Is.Not.Null, $"等级 {levelRow.PlayerLevel} 解锁槽位 {slotIndex} 后未生成任务。");
                    Assert.That(unlock.GeneratedTask.Success, Is.True, $"等级 {levelRow.PlayerLevel} 解锁槽位 {slotIndex} 后补任务失败：{unlock.GeneratedTask?.FailureReason}");
                }

                Assert.That(taskBar.Tasks.Count, Is.EqualTo(taskBar.TotalSlots), $"等级 {levelRow.PlayerLevel} 应能补满 5 个任务槽位。");
                AssertTaskBarHasUniqueCats(taskBar, levelRow.PlayerLevel);
                AssertTaskRewardsMatchConfig(tables, taskBar.Tasks.Select(item => item.Task));
            }
        }

        [Test]
        public void HolmasCsvSampleTables_CurrentTaskPoolCanDriveMapSpawnPool()
        {
            CsvConfigTables tables = LoadSampleTables();
            HolmasTaskCatalog taskCatalog = BuildTaskCatalog(tables);
            HolmasMapCatalog mapCatalog = BuildMapCatalog(tables);
            var taskService = new HolmasTaskProgressService(taskCatalog, new ScriptedRandomSource(), new FixedUtcClock { UtcNowMilliseconds = 1000 });
            var requestGenerator = new HolmasLevelRequestGenerator(taskCatalog, mapCatalog, new ScriptedRandomSource());
            HolmasTaskBarState taskBar = taskService.CreateDefaultTaskBarState();

            taskService.RefillUnlockedEmptySlots(taskBar, 10);
            for (int slotIndex = taskBar.DefaultOpenSlots; slotIndex < taskBar.TotalSlots; slotIndex++)
            {
                taskService.UnlockAdSlot(taskBar, slotIndex, 10, 5_000L + slotIndex);
            }

            IReadOnlyCollection<string> activeCatIds = taskBar.GetActiveCatIds();
            IReadOnlyList<BoardSpawnEntry> catPool = tables.Cats
                .Where(item => activeCatIds.Contains(item.CatId))
                .Select(item => new BoardSpawnEntry
                {
                    CatId = item.CatId,
                    Weight = item.Weight,
                })
                .ToArray();

            HolmasLevelRequestGenerationResult requestResult = requestGenerator.TryGenerateForPlayerLevel(10, 1234, catPool);
            Assert.That(requestResult.Success, Is.True, requestResult.FailureReason);

            var terrain = HolmasTestSupport.CreateTerrain(6, 6, (_, _) => true);
            LevelSnapshot snapshot = LevelSnapshotFactory.CreateFromTerrain(terrain, requestResult.Request);

            Assert.That(snapshot.SpawnedCats, Is.Not.Empty);
            Assert.That(snapshot.SpawnedCats.Select(item => item.CatId).All(activeCatIds.Contains), Is.True, "地图生成应优先使用当前任务栏的猫种池。");
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
        public void HolmasCsvValidator_RejectsMetaLevelMismatch()
        {
            CsvConfigTables tables = LoadSampleTables();
            tables.MetaLevels[0].MinExperience = tables.MetaLevels[1].MinExperience;

            IReadOnlyList<string> errors = CsvConfigValidator.Validate(tables);

            Assert.That(errors.Any(item => item.Contains("长期成长经验门槛") || item.Contains("严格递增")), Is.True, string.Join(Environment.NewLine, errors));
        }

        [Test]
        public void HolmasCsvValidator_RejectsAgencyBuildingShapeMismatch()
        {
            CsvConfigTables tables = LoadSampleTables();
            tables.AgencyBuildings[0].BuildingUpgradeLevelCaps = new[] { 3, 2 };

            IReadOnlyList<string> errors = CsvConfigValidator.Validate(tables);

            Assert.That(
                errors.Any(item => item.Contains("建筑 ID 与等级上限数量不一致") || item.Contains("升级费用数量与等级上限不一致")),
                Is.True,
                string.Join(Environment.NewLine, errors));
        }

        [Test]
        public void HolmasCsvValidator_RejectsNonPositiveAgencyBuildingCost()
        {
            CsvConfigTables tables = LoadSampleTables();
            tables.AgencyBuildings[0].BuildingUpgradeCosts[0][0] = 0;

            IReadOnlyList<string> errors = CsvConfigValidator.Validate(tables);

            Assert.That(errors.Any(item => item.Contains("非正费用")), Is.True, string.Join(Environment.NewLine, errors));
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
                MetaLevels = LoadMetaLevels("Holmas_MetaLevelTable.csv"),
                AgencyBuildings = LoadAgencyBuildings("Holmas_AgencyBuildingTable.csv"),
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

        private static void AssertTaskBarHasUniqueCats(HolmasTaskBarState taskBar, int playerLevel)
        {
            IReadOnlyCollection<string> activeCatIds = taskBar.GetActiveCatIds();
            Assert.That(activeCatIds.Count, Is.EqualTo(taskBar.Tasks.Count), $"等级 {playerLevel} 的任务栏猫种不应重复。");
            Assert.That(activeCatIds.Count, Is.EqualTo(activeCatIds.Distinct(StringComparer.Ordinal).Count()), $"等级 {playerLevel} 的任务栏存在重复猫种。");
        }

        private static void AssertTaskRewardsMatchConfig(CsvConfigTables tables, IEnumerable<TaskInstanceData> tasks)
        {
            foreach (TaskInstanceData task in tasks)
            {
                Assert.That(task, Is.Not.Null);
                CsvTaskRow taskRow = tables.Tasks.Single(item => string.Equals(item.TaskTypeId, task.SourceTaskTypeId, StringComparison.Ordinal));
                CsvCatRow catRow = tables.Cats.Single(item => string.Equals(item.CatId, task.CatId, StringComparison.Ordinal));
                int expectedReward = (int)Math.Round(catRow.Price * task.TargetCount * taskRow.LevelRewardFactor, MidpointRounding.AwayFromZero);
                Assert.That(task.Reward, Is.EqualTo(expectedReward), $"任务 {task.SourceTaskTypeId}/{task.CatId} 奖励公式不匹配。");
            }
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
                MetaLevels = tables.MetaLevels.Select(item => new HolmasMetaLevelRow
                {
                    PlayerLevel = item.PlayerLevel,
                    MinExperience = item.MinExperience,
                    OfflineRewardPerHour = item.OfflineRewardPerHour,
                    AdUnlockHours = item.AdUnlockHours,
                    Notes = item.Notes,
                }).ToArray(),
                AgencyBuildings = tables.AgencyBuildings.Select(item => new HolmasAgencyBuildingRow
                {
                    AgencyStageId = item.AgencyStageId,
                    BuildingIds = item.BuildingIds.ToArray(),
                    BuildingUpgradeLevelCaps = item.BuildingUpgradeLevelCaps.ToArray(),
                    BuildingUpgradeCosts = item.BuildingUpgradeCosts.Select(costs => new HolmasAgencyBuildingCostRow
                    {
                        Costs = costs.ToArray(),
                    }).ToArray(),
                    Notes = item.Notes,
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
            var headerMap = BuildColumnMap(rows[1]);
            int taskTypeIdCol = GetColumnIndex(headerMap, "taskTypeId");
            int taskKindCol = GetOptionalColumnIndex(headerMap, "taskKind");
            int catIdListCol = GetColumnIndex(headerMap, "catIdList");
            int countMaxCol = GetColumnIndex(headerMap, "countMax");
            int countMinCol = GetColumnIndex(headerMap, "countMin");
            int rewardArrayCol = GetOptionalColumnIndex(headerMap, "rewardArray");
            int levelRewardFactorCol = GetColumnIndex(headerMap, "levelRewardFactor");
            var result = new List<CsvTaskRow>();
            foreach (var row in rows.Skip(2))
            {
                if (row.Length == 0)
                {
                    continue;
                }

                result.Add(new CsvTaskRow
                {
                    TaskTypeId = GetRowValue(row, taskTypeIdCol),
                    TaskKind = NormalizeTaskKindText(GetRowValue(row, taskKindCol)),
                    CatIdList = SplitCsvList(GetRowValue(row, catIdListCol)),
                    CountMax = ParseInt(GetRowValue(row, countMaxCol)),
                    CountMin = ParseInt(GetRowValue(row, countMinCol)),
                    RewardArray = SplitCsvList(GetRowValue(row, rewardArrayCol)),
                    LevelRewardFactor = ParseFloat(GetRowValue(row, levelRewardFactorCol)),
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

        private static List<CsvMetaLevelRow> LoadMetaLevels(string fileName)
        {
            var rows = ReadCsvTable(fileName);
            var result = new List<CsvMetaLevelRow>();
            foreach (var row in rows.Skip(2))
            {
                if (row.Length < 5)
                {
                    continue;
                }

                result.Add(new CsvMetaLevelRow
                {
                    PlayerLevel = ParseInt(row[0]),
                    MinExperience = ParseLong(row[1]),
                    OfflineRewardPerHour = ParseInt(row[2]),
                    AdUnlockHours = ParseInt(row[3]),
                    Notes = row[4],
                });
            }

            return result;
        }

        private static List<CsvAgencyBuildingRow> LoadAgencyBuildings(string fileName)
        {
            var rows = ReadCsvTable(fileName);
            var result = new List<CsvAgencyBuildingRow>();
            foreach (var row in rows.Skip(2))
            {
                if (row.Length < 5)
                {
                    continue;
                }

                result.Add(new CsvAgencyBuildingRow
                {
                    AgencyStageId = ParseInt(row[0]),
                    BuildingIds = SplitCsvList(row[1]),
                    BuildingUpgradeLevelCaps = SplitIntList(row[2]),
                    BuildingUpgradeCosts = SplitNestedIntLists(row[3]),
                    Notes = row[4],
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

        private static Dictionary<string, int> BuildColumnMap(string[] headerRow)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (headerRow == null)
            {
                return map;
            }

            for (int i = 0; i < headerRow.Length; i++)
            {
                string key = TrimCell(headerRow[i]);
                if (!string.IsNullOrWhiteSpace(key) && !map.ContainsKey(key))
                {
                    map[key] = i;
                }
            }

            return map;
        }

        private static int GetColumnIndex(Dictionary<string, int> headerMap, string name)
        {
            Assert.That(headerMap.ContainsKey(name), Is.True, $"配置表缺少列: {name}");
            return headerMap[name];
        }

        private static int GetOptionalColumnIndex(Dictionary<string, int> headerMap, string name)
        {
            return headerMap.TryGetValue(name, out int value) ? value : -1;
        }

        private static string GetRowValue(string[] row, int index)
        {
            if (row == null || index < 0 || index >= row.Length)
            {
                return string.Empty;
            }

            return TrimCell(row[index]);
        }

        private static string NormalizeTaskKindText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "Money" : value.Trim();
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

        private static int[][] SplitNestedIntLists(string cell)
        {
            if (string.IsNullOrWhiteSpace(cell))
            {
                return Array.Empty<int[]>();
            }

            return cell.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(item => SplitIntList(item.Trim()))
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

        private static long ParseLong(string cell)
        {
            long value;
            if (!long.TryParse(cell, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                return 0L;
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
            public List<CsvMetaLevelRow> MetaLevels { get; set; } = new List<CsvMetaLevelRow>();
            public List<CsvAgencyBuildingRow> AgencyBuildings { get; set; } = new List<CsvAgencyBuildingRow>();
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

        private sealed class CsvMetaLevelRow
        {
            public int PlayerLevel;
            public long MinExperience;
            public int OfflineRewardPerHour;
            public int AdUnlockHours;
            public string Notes = string.Empty;
        }

        private sealed class CsvAgencyBuildingRow
        {
            public int AgencyStageId;
            public string[] BuildingIds = Array.Empty<string>();
            public int[] BuildingUpgradeLevelCaps = Array.Empty<int>();
            public int[][] BuildingUpgradeCosts = Array.Empty<int[]>();
            public string Notes = string.Empty;
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
                ValidateMetaLevels(tables.MetaLevels, tables.Levels, errors);
                ValidateAgencyBuildings(tables.AgencyBuildings, errors);
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

                    string normalizedTaskKind = NormalizeTaskKindText(task.TaskKind);
                    if (!string.Equals(normalizedTaskKind, "Money", StringComparison.Ordinal))
                    {
                        errors.Add($"不支持的 taskKind={normalizedTaskKind}，当前阶段只允许 Money。");
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

            private static void ValidateMetaLevels(IEnumerable<CsvMetaLevelRow> levels, IReadOnlyList<CsvLevelRow> playerLevels, List<string> errors)
            {
                var seen = new HashSet<int>();
                long previousMinExperience = long.MinValue;
                int expectedLevel = 1;
                int metaLevelCount = 0;

                foreach (var level in levels ?? Array.Empty<CsvMetaLevelRow>())
                {
                    metaLevelCount++;
                    if (level == null)
                    {
                        errors.Add("长期成长表存在空行。");
                        continue;
                    }

                    if (level.PlayerLevel <= 0)
                    {
                        errors.Add("长期成长表的玩家等级非法。");
                        continue;
                    }

                    if (level.PlayerLevel != expectedLevel)
                    {
                        errors.Add($"长期成长表的玩家等级不连续: {level.PlayerLevel}。");
                    }

                    if (!seen.Add(level.PlayerLevel))
                    {
                        errors.Add($"长期成长表存在重复玩家等级: {level.PlayerLevel}");
                    }

                    if (level.MinExperience < 0)
                    {
                        errors.Add($"长期成长表的经验门槛非法: {level.PlayerLevel}");
                    }

                    if (previousMinExperience >= level.MinExperience)
                    {
                        errors.Add($"长期成长经验门槛必须严格递增: {level.PlayerLevel}");
                    }

                    if (level.OfflineRewardPerHour < 0)
                    {
                        errors.Add($"长期成长表的离线奖励非法: {level.PlayerLevel}");
                    }

                    if (level.AdUnlockHours <= 0)
                    {
                        errors.Add($"长期成长表的广告解锁时长非法: {level.PlayerLevel}");
                    }

                    previousMinExperience = level.MinExperience;
                    expectedLevel++;
                }

                if (playerLevels != null && metaLevelCount != playerLevels.Count)
                {
                    errors.Add($"长期成长表与玩家等级表数量不一致: meta={metaLevelCount}, player={playerLevels.Count}");
                }

                if (playerLevels != null)
                {
                    var upgradeExpByLevel = playerLevels.Where(item => item != null).ToDictionary(item => item.PlayerLevel, item => item.UpgradeExp);
                    foreach (var level in levels ?? Array.Empty<CsvMetaLevelRow>())
                    {
                        if (level == null)
                        {
                            continue;
                        }

                        int upgradeExp;
                        if (upgradeExpByLevel.TryGetValue(level.PlayerLevel, out upgradeExp) && upgradeExp != level.MinExperience)
                        {
                            errors.Add($"长期成长表和玩家等级表经验门槛不一致: {level.PlayerLevel}");
                        }
                    }
                }
            }

            private static void ValidateAgencyBuildings(IEnumerable<CsvAgencyBuildingRow> buildings, List<string> errors)
            {
                var seenStages = new HashSet<int>();
                var seenBuildingIds = new HashSet<string>(StringComparer.Ordinal);
                int expectedStage = 1;

                foreach (var buildingRow in buildings ?? Array.Empty<CsvAgencyBuildingRow>())
                {
                    if (buildingRow == null)
                    {
                        errors.Add("建筑表存在空行。");
                        continue;
                    }

                    if (buildingRow.AgencyStageId <= 0)
                    {
                        errors.Add("建筑表的阶段 ID 非法。");
                        continue;
                    }

                    if (buildingRow.AgencyStageId != expectedStage)
                    {
                        errors.Add($"建筑表的阶段 ID 不连续: {buildingRow.AgencyStageId}");
                    }

                    if (!seenStages.Add(buildingRow.AgencyStageId))
                    {
                        errors.Add($"建筑表存在重复阶段 ID: {buildingRow.AgencyStageId}");
                    }

                    if (buildingRow.BuildingIds == null || buildingRow.BuildingIds.Length == 0)
                    {
                        errors.Add($"建筑表缺少建筑 ID: {buildingRow.AgencyStageId}");
                        continue;
                    }

                    if (buildingRow.BuildingUpgradeLevelCaps == null || buildingRow.BuildingUpgradeLevelCaps.Length != buildingRow.BuildingIds.Length)
                    {
                        errors.Add($"建筑表的建筑 ID 与等级上限数量不一致: {buildingRow.AgencyStageId}");
                    }

                    if (buildingRow.BuildingUpgradeCosts == null || buildingRow.BuildingUpgradeCosts.Length != buildingRow.BuildingIds.Length)
                    {
                        errors.Add($"建筑表的建筑 ID 与升级费用数量不一致: {buildingRow.AgencyStageId}");
                    }

                    for (int i = 0; i < buildingRow.BuildingIds.Length; i++)
                    {
                        string buildingId = buildingRow.BuildingIds[i] ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(buildingId))
                        {
                            errors.Add($"建筑表存在空建筑 ID: {buildingRow.AgencyStageId}");
                            continue;
                        }

                        if (!seenBuildingIds.Add(buildingId))
                        {
                            errors.Add($"建筑表存在重复建筑 ID: {buildingId}");
                        }

                        if (buildingRow.BuildingUpgradeLevelCaps != null && i < buildingRow.BuildingUpgradeLevelCaps.Length && buildingRow.BuildingUpgradeLevelCaps[i] <= 0)
                        {
                            errors.Add($"建筑表等级上限非法: {buildingRow.AgencyStageId}/{buildingId}");
                        }

                        int[] costs = buildingRow.BuildingUpgradeCosts != null && i < buildingRow.BuildingUpgradeCosts.Length
                            ? buildingRow.BuildingUpgradeCosts[i]
                            : Array.Empty<int>();

                        int cap = buildingRow.BuildingUpgradeLevelCaps != null && i < buildingRow.BuildingUpgradeLevelCaps.Length
                            ? buildingRow.BuildingUpgradeLevelCaps[i]
                            : 0;

                        if (costs == null || costs.Length != cap)
                        {
                            errors.Add($"建筑表升级费用数量与等级上限不一致: {buildingRow.AgencyStageId}/{buildingId}");
                        }
                        else
                        {
                            foreach (int cost in costs)
                            {
                                if (cost <= 0)
                                {
                                    errors.Add($"建筑表存在非正费用: {buildingRow.AgencyStageId}/{buildingId}");
                                    break;
                                }
                            }
                        }
                    }

                    expectedStage++;
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
