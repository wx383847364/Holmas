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
    public sealed class HolmasConfigExportTests
    {
        [Test]
        public void HolmasExportedTables_AreConsistentAndMoneyOnly()
        {
            ExportConfigTables tables = LoadSampleTables();
            IReadOnlyList<string> errors = ExportConfigValidator.Validate(tables);

            Assert.That(errors, Is.Empty, string.Join(Environment.NewLine, errors));
            Assert.That(tables.Cats, Has.Count.EqualTo(49));
            Assert.That(tables.Maps, Is.Not.Empty);
            Assert.That(tables.Tasks, Has.Count.EqualTo(20));
            Assert.That(tables.Levels, Has.Count.EqualTo(100));
            Assert.That(tables.AgencyBuildings, Has.Count.EqualTo(100));
            Assert.That(tables.Tasks.All(item => string.Equals(item.TaskKind, "Money", StringComparison.Ordinal)), Is.True);
            Assert.That(tables.Cats.All(item => item.Price > 0 && item.Weight > 0 && item.Rarity > 0), Is.True);
            Assert.That(tables.Maps.All(item => item.CatCountMin > 0 && item.CatCountMax >= item.CatCountMin), Is.True);
            Assert.That(tables.Maps.All(item => !string.IsNullOrWhiteSpace(item.TerrainPath)), Is.True);
            Assert.That(tables.Maps.Select(item => item.MapId).Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(tables.Maps.Count));
            Assert.That(tables.AgencyBuildings.All(item => item.PromotionIds != null && item.PromotionIds.Length == 4), Is.True);
            Assert.That(tables.AgencyBuildings.All(item => item.PromotionLevelCaps != null && item.PromotionLevelCaps.Length == 4), Is.True);
            Assert.That(tables.AgencyBuildings.Select(item => item.AgencyStageId), Is.EqualTo(Enumerable.Range(1, 100).ToArray()));
        }

        [Test]
        public void HolmasExportedTables_CanDriveRuntimeGeneration()
        {
            ExportConfigTables tables = LoadSampleTables();

            HolmasTaskCatalog taskCatalog = BuildTaskCatalog(tables);
            HolmasMapCatalog mapCatalog = BuildMapCatalog(tables);
            var requestGenerator = new HolmasLevelRequestGenerator(taskCatalog, mapCatalog, new ScriptedRandomSource(0, 0, 0, 0, 0, 0));
            ExportLevelRow levelOne = tables.Levels.Single(item => item.PlayerLevel == 1);

            HolmasLevelRequestGenerationResult requestResult = requestGenerator.TryGenerateForPlayerLevel(1, 99);

            Assert.That(requestResult.Success, Is.True, requestResult.FailureReason);
            Assert.That(levelOne.MapIds, Contains.Item(requestResult.SelectedMapId), "等级 1 抽到了未开放地图。");
            ExportMapRow selectedMap = tables.Maps.Single(item => string.Equals(item.MapId, requestResult.SelectedMapId, StringComparison.Ordinal));
            Assert.That(requestResult.Request.TerrainPath, Is.EqualTo(selectedMap.TerrainPath));
            Assert.That(requestResult.Request.CatCountMin, Is.EqualTo(selectedMap.CatCountMin));
            Assert.That(requestResult.Request.CatCountMax, Is.EqualTo(selectedMap.CatCountMax));

            var terrain = HolmasTestSupport.CreateTerrain(5, 5, (_, _) => true);
            LevelSnapshot snapshot = LevelSnapshotFactory.CreateFromTerrain(terrain, requestResult.Request);

            Assert.That(snapshot.MapId, Is.EqualTo(selectedMap.MapId));
            Assert.That(snapshot.TerrainPath, Is.EqualTo(selectedMap.TerrainPath));
            Assert.That(snapshot.SpawnedCats.Count, Is.InRange(selectedMap.CatCountMin, selectedMap.CatCountMax));
            Assert.That(snapshot.SpawnedCats.Select(item => item.CatId), Is.All.Empty);

            var taskService = new HolmasTaskProgressService(taskCatalog, new ScriptedRandomSource(0, 0, 0, 0, 0, 0), new FixedUtcClock { UtcNowMilliseconds = 1000 });
            HolmasTaskBarState taskBar = taskService.CreateDefaultTaskBarState();
            HolmasTaskRefillResult refill = taskService.RefillUnlockedEmptySlots(taskBar, 1);

            Assert.That(refill.GeneratedTasks.Count, Is.EqualTo(2));
            Assert.That(refill.GeneratedTasks.All(item => item.Success), Is.True);
            Assert.That(taskBar.GetActiveCatIds().Count, Is.EqualTo(taskBar.GetActiveCatIds().Distinct(StringComparer.Ordinal).Count()));

            foreach (HolmasTaskGenerationResult generated in refill.GeneratedTasks)
            {
                Assert.That(generated.Task, Is.Not.Null);
                ExportTaskRow taskRow = tables.Tasks.Single(item => string.Equals(item.TaskTypeId, generated.Task.SourceTaskTypeId, StringComparison.Ordinal));
                ExportCatRow catRow = tables.Cats.Single(item => string.Equals(item.CatId, generated.Task.CatId, StringComparison.Ordinal));
                int expectedReward = (int)Math.Round(catRow.Price * generated.Task.TargetCount * taskRow.LevelRewardFactor, MidpointRounding.AwayFromZero);
                Assert.That(generated.Task.Reward, Is.EqualTo(expectedReward));
            }
        }

        [Test]
        public void HolmasExportedTables_CanRoundTripThroughBinaryCatalogFactory()
        {
            ExportConfigTables tables = LoadSampleTables();
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
            Assert.That(bundle.AgencyBuildings.Count, Is.EqualTo(tables.AgencyBuildings.Count));
            Assert.That(bundle.Report.Success, Is.True);
            Assert.That(bundle.Report.Errors, Is.Empty);
        }

        [Test]
        public void HolmasConfigCatalogFactory_RejectsMissingAgencyPromotions()
        {
            ExportConfigTables tables = LoadSampleTables();
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
        public void HolmasConfigCatalogFactory_RejectsUnsupportedCoreJsonVersion()
        {
            string json = File.ReadAllText(Path.Combine(Application.dataPath, "Config", "json", "holmas_core_config.json")).TrimStart('\uFEFF');
            json = json.Replace("\"Version\": 6", "\"Version\": 5");

            bool success = HolmasConfigBinaryCodec.TryReadCoreJson(json, out _, out string error);

            Assert.That(success, Is.False);
            Assert.That(error.Contains("版本不支持"), Is.True, error);
        }

        [Test]
        public void HolmasExportedTables_FollowRarityBandsAndMapUnlockPlan()
        {
            ExportConfigTables tables = LoadSampleTables();

            Assert.That(tables.Cats.Count(item => item.Rarity == 1), Is.EqualTo(20));
            Assert.That(tables.Cats.Count(item => item.Rarity == 2), Is.EqualTo(14));
            Assert.That(tables.Cats.Count(item => item.Rarity == 3), Is.EqualTo(10));
            Assert.That(tables.Cats.Count(item => item.Rarity == 4), Is.EqualTo(5));

            Assert.That(tables.Levels, Has.Count.EqualTo(100));
            Assert.That(tables.Levels.Select(item => item.PlayerLevel).ToArray(), Is.EqualTo(Enumerable.Range(1, 100).ToArray()));
            Assert.That(tables.AgencyBuildings.Select(item => item.AgencyStageId), Is.EqualTo(Enumerable.Range(1, 100).ToArray()));
            Assert.That(tables.AgencyBuildings.All(item => item.PromotionIds.SequenceEqual(new[] { "leaflet", "radio", "online", "tv" })), Is.True);
            Assert.That(tables.AgencyBuildings.All(item => item.PromotionLevelCaps.SequenceEqual(new[] { 5, 5, 5, 5 })), Is.True);
            Assert.That(tables.Levels.First().UpgradeExp, Is.EqualTo(0));
            Assert.That(tables.Levels[9].UpgradeExp, Is.EqualTo(126));
            Assert.That(tables.Levels.Last().UpgradeExp, Is.EqualTo(1476));
            Assert.That(tables.Levels.Select(item => item.UpgradeExp).Distinct().Count(), Is.EqualTo(tables.Levels.Count));

            var referencedMapIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (ExportLevelRow level in tables.Levels)
            {
                Assert.That(level.TaskTypeIds.Length, Is.EqualTo(level.TaskTypeWeights.Length), $"等级 {level.PlayerLevel} 的任务组长度应一致。");
                Assert.That(level.MapIds, Is.Not.Empty, $"等级 {level.PlayerLevel} 至少应配置一张地图。");
                Assert.That(level.MapIds.Length, Is.EqualTo(level.MapWeights.Length), $"等级 {level.PlayerLevel} 的地图组长度应一致。");
                Assert.That(level.TaskTypeWeights.All(weight => weight > 0), Is.True, $"等级 {level.PlayerLevel} 的任务权重必须为正数。");
                Assert.That(level.MapWeights.All(weight => weight > 0), Is.True, $"等级 {level.PlayerLevel} 的地图权重必须为正数。");
                Assert.That(level.MapIds.Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(level.MapIds.Length), $"等级 {level.PlayerLevel} 的地图池不应重复。");
                referencedMapIds.UnionWith(level.MapIds);
            }

            Assert.That(referencedMapIds, Is.SupersetOf(tables.Maps.Select(item => item.MapId)), "所有 MapTable 地图都应至少在一个玩家等级池中可达。");
            Assert.That(tables.Levels.Skip(1).SelectMany(item => item.MapIds).Except(tables.Levels[0].MapIds, StringComparer.Ordinal).Any(), Is.True, "高等级应逐步解锁等级 1 之外的新地图。");
        }

        [Test]
        public void HolmasConfigCatalogFactory_RejectsLegacyMetaLevelsJson()
        {
            string json = File.ReadAllText(Path.Combine(Application.dataPath, "Config", "json", "holmas_core_config.json")).TrimStart('\uFEFF');
            json = json.Replace(
                "\"AgencyBuildings\": [",
                "\"MetaLevels\": [],\n    \"AgencyBuildings\": [",
                StringComparison.Ordinal);

            bool success = HolmasConfigBinaryCodec.TryReadCoreJson(json, out _, out string error);

            Assert.That(success, Is.False);
            Assert.That(error.Contains("MetaLevels"), Is.True, error);
        }

        [Test]
        public void HolmasConfigCatalogFactory_RejectsUnsupportedCoreBinaryCodecVersion()
        {
            ExportConfigTables tables = LoadSampleTables();
            HolmasCoreConfigPackage corePackage = BuildCorePackage(tables);
            HolmasCatMetaPackage catPackage = BuildCatPackage(tables);

            byte[] coreBytes = HolmasConfigBinaryCodec.WriteCorePackage(corePackage);
            OverwriteInt32(coreBytes, sizeof(int), HolmasConfigBinaryFormat.CurrentVersion - 1);
            byte[] catBytes = HolmasConfigBinaryCodec.WriteCatMetaPackage(catPackage);

            bool success = HolmasConfigCatalogFactory.TryCreateFromBinary(coreBytes, catBytes, out _, out HolmasConfigReport report);

            Assert.That(success, Is.False);
            Assert.That(report.Errors.Any(item => item.Contains("编解码版本不支持")), Is.True, string.Join(Environment.NewLine, report.Errors));
        }

        [Test]
        public void HolmasExportedTables_AllLevelsCanGenerateRequestsAndFillFiveUniqueTasks()
        {
            ExportConfigTables tables = LoadSampleTables();
            HolmasTaskCatalog taskCatalog = BuildTaskCatalog(tables);
            HolmasMapCatalog mapCatalog = BuildMapCatalog(tables);
            var requestGenerator = new HolmasLevelRequestGenerator(taskCatalog, mapCatalog, new ScriptedRandomSource());

            foreach (ExportLevelRow levelRow in tables.Levels)
            {
                var taskService = new HolmasTaskProgressService(taskCatalog, new ScriptedRandomSource(), new FixedUtcClock { UtcNowMilliseconds = 1000 });
                HolmasTaskBarState taskBar = taskService.CreateDefaultTaskBarState();

                HolmasLevelRequestGenerationResult requestResult = requestGenerator.TryGenerateForPlayerLevel(levelRow.PlayerLevel, levelRow.PlayerLevel * 17);
                Assert.That(requestResult.Success, Is.True, $"等级 {levelRow.PlayerLevel} 地图请求生成失败：{requestResult.FailureReason}");
                Assert.That(levelRow.MapIds, Contains.Item(requestResult.SelectedMapId), $"等级 {levelRow.PlayerLevel} 抽到了未开放地图。");

                ExportMapRow selectedMap = tables.Maps.Single(item => string.Equals(item.MapId, requestResult.SelectedMapId, StringComparison.Ordinal));
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
        public void HolmasExportedTables_DifficultyFlowMakesNewTerrainsReachable()
        {
            ExportConfigTables tables = LoadSampleTables();
            HolmasTaskCatalog taskCatalog = BuildTaskCatalog(tables);
            HolmasMapCatalog mapCatalog = BuildMapCatalog(tables);
            var requestGenerator = new HolmasLevelRequestGenerator(taskCatalog, mapCatalog, new ScriptedRandomSource(0));
            var mapById = tables.Maps.ToDictionary(item => item.MapId, item => item, StringComparer.Ordinal);
            ExportLevelRow levelThree = tables.Levels.Single(item => item.PlayerLevel == 3);
            ExportLevelRow levelSix = tables.Levels.Single(item => item.PlayerLevel == 6);
            ExportLevelRow levelNine = tables.Levels.Single(item => item.PlayerLevel == 9);

            HolmasLevelRequestGenerationResult levelThreeRequest = requestGenerator.TryGenerateForPlayerLevel(3, 300);
            HolmasLevelRequestGenerationResult levelSixRequest = requestGenerator.TryGenerateForPlayerLevel(6, 600);
            HolmasLevelRequestGenerationResult levelNineRequest = requestGenerator.TryGenerateForPlayerLevel(9, 900);

            Assert.That(levelThreeRequest.Success, Is.True, levelThreeRequest.FailureReason);
            Assert.That(levelThree.MapIds, Contains.Item(levelThreeRequest.SelectedMapId), "等级 3 抽到了未开放地图。");
            Assert.That(levelThreeRequest.Request.TerrainPath, Is.EqualTo(mapById[levelThreeRequest.SelectedMapId].TerrainPath));

            Assert.That(levelSixRequest.Success, Is.True, levelSixRequest.FailureReason);
            Assert.That(levelSix.MapIds, Contains.Item(levelSixRequest.SelectedMapId), "等级 6 抽到了未开放地图。");
            Assert.That(levelSixRequest.Request.TerrainPath, Is.EqualTo(mapById[levelSixRequest.SelectedMapId].TerrainPath));

            Assert.That(levelNineRequest.Success, Is.True, levelNineRequest.FailureReason);
            Assert.That(levelNine.MapIds, Contains.Item(levelNineRequest.SelectedMapId), "等级 9 抽到了未开放地图。");
            Assert.That(levelNineRequest.Request.TerrainPath, Is.EqualTo(mapById[levelNineRequest.SelectedMapId].TerrainPath));

            var levelThreeTerrainPaths = new HashSet<string>(levelThree.MapIds.Select(mapId => mapById[mapId].TerrainPath), StringComparer.Ordinal);
            var levelSixTerrainPaths = new HashSet<string>(levelSix.MapIds.Select(mapId => mapById[mapId].TerrainPath), StringComparer.Ordinal);
            var levelNineTerrainPaths = new HashSet<string>(levelNine.MapIds.Select(mapId => mapById[mapId].TerrainPath), StringComparer.Ordinal);

            Assert.That(levelSixTerrainPaths.Except(levelThreeTerrainPaths).Any(), Is.True, "等级 6 应能接触到等级 3 地图池之外的新地形。");
            Assert.That(levelNineTerrainPaths.Except(levelSixTerrainPaths).Any(), Is.True, "等级 9 应能接触到等级 6 地图池之外的新地形。");
        }

        [Test]
        public void HolmasExportedTables_CurrentTaskPoolCanResolveBlindBoxCats()
        {
            ExportConfigTables tables = LoadSampleTables();
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
            Assert.That(snapshot.SpawnedCats.Select(item => item.CatId), Is.All.Empty, "普通地图生成只布猫位，猫种应留到揭示时解析。");
            Assert.That(taskService.TryPickUncompletedTaskCatId(taskBar, out string pickedCatId), Is.True);
            Assert.That(activeCatIds.Contains(pickedCatId), Is.True, "揭示时应从当前未完成任务猫池中解析猫种。");
        }

        [Test]
        public void HolmasExportValidator_RejectsMissingCatReference()
        {
            ExportConfigTables tables = LoadSampleTables();
            tables.Tasks[0].CatIdList = new[] { "999" };

            IReadOnlyList<string> errors = ExportConfigValidator.Validate(tables);

            Assert.That(errors.Any(item => item.Contains("找不到猫配置")), Is.True);
        }

        [Test]
        public void HolmasExportValidator_RejectsNonIncreasingUpgradeExp()
        {
            ExportConfigTables tables = LoadSampleTables();
            tables.Levels[1].UpgradeExp = tables.Levels[0].UpgradeExp;

            IReadOnlyList<string> errors = ExportConfigValidator.Validate(tables);

            Assert.That(errors.Any(item => item.Contains("升级经验") || item.Contains("严格递增")), Is.True, string.Join(Environment.NewLine, errors));
        }

        [Test]
        public void HolmasExportValidator_RejectsPromotionShapeMismatch()
        {
            ExportConfigTables tables = LoadSampleTables();
            tables.AgencyBuildings[0].PromotionLevelCaps = new[] { 5, 5 };

            IReadOnlyList<string> errors = ExportConfigValidator.Validate(tables);

            Assert.That(
                errors.Any(item => item.Contains("宣传 ID 与等级上限数量不一致") || item.Contains("宣传 ID 与升级费用数量不一致")),
                Is.True,
                string.Join(Environment.NewLine, errors));
        }

        [Test]
        public void HolmasExportValidator_RejectsNonPositivePromotionCost()
        {
            ExportConfigTables tables = LoadSampleTables();
            tables.AgencyBuildings[0].PromotionUpgradeCosts[0][0] = 0;

            IReadOnlyList<string> errors = ExportConfigValidator.Validate(tables);

            Assert.That(errors.Any(item => item.Contains("非正费用")), Is.True, string.Join(Environment.NewLine, errors));
        }

        [Test]
        public void HolmasExportValidator_RejectsWeightLengthMismatch()
        {
            ExportConfigTables tables = LoadSampleTables();
            tables.Levels[0].TaskTypeWeights = new[] { 100 };

            IReadOnlyList<string> errors = ExportConfigValidator.Validate(tables);

            Assert.That(errors.Any(item => item.Contains("长度不一致")), Is.True);
        }

        [Test]
        public void HolmasExportValidator_RejectsMinGreaterThanMax()
        {
            ExportConfigTables tables = LoadSampleTables();
            tables.Maps[0].CatCountMin = 5;
            tables.Maps[0].CatCountMax = 3;

            IReadOnlyList<string> errors = ExportConfigValidator.Validate(tables);

            Assert.That(errors.Any(item => item.Contains("猫数范围非法")), Is.True);
        }

        [Test]
        public void HolmasExportValidator_RejectsEmptyTerrainPath()
        {
            ExportConfigTables tables = LoadSampleTables();
            tables.Maps[1].TerrainPath = string.Empty;

            IReadOnlyList<string> errors = ExportConfigValidator.Validate(tables);

            Assert.That(errors.Any(item => item.Contains("TerrainPath")), Is.True);
        }

        [Test]
        public void HolmasExportValidator_RejectsGambleTaskKind()
        {
            ExportConfigTables tables = LoadSampleTables();
            tables.Tasks[0].TaskKind = "Gamble";

            IReadOnlyList<string> errors = ExportConfigValidator.Validate(tables);

            Assert.That(errors.Any(item => item.Contains("Gamble")), Is.True);
        }

        private static ExportConfigTables LoadSampleTables()
        {
            string corePath = Path.Combine(Application.dataPath, "Config", "json", "holmas_core_config.json");
            string catPath = Path.Combine(Application.dataPath, "Config", "json", "holmas_cat_meta.json");
            Assert.That(File.Exists(corePath), Is.True, $"找不到导出配置: {corePath}");
            Assert.That(File.Exists(catPath), Is.True, $"找不到导出配置: {catPath}");

            string coreJson = File.ReadAllText(corePath).TrimStart('\uFEFF');
            string catJson = File.ReadAllText(catPath).TrimStart('\uFEFF');
            bool coreOk = HolmasConfigBinaryCodec.TryReadCoreJson(coreJson, out HolmasCoreConfigPackage corePackage, out string coreError);
            bool catOk = HolmasConfigBinaryCodec.TryReadCatMetaJson(catJson, out HolmasCatMetaPackage catPackage, out string catError);

            Assert.That(coreOk, Is.True, coreError);
            Assert.That(catOk, Is.True, catError);

            return new ExportConfigTables
            {
                Cats = (catPackage.Cats ?? Array.Empty<HolmasCatMetaRow>()).Select(item => new ExportCatRow
                {
                    CatId = item.CatId,
                    CatName = item.CatName,
                    IconPath = item.IconPath,
                    Rarity = item.Rarity,
                    Weight = item.Weight,
                    Price = item.Price,
                }).ToList(),
                Maps = (corePackage.Maps ?? Array.Empty<HolmasMapRow>()).Select(item => new ExportMapRow
                {
                    MapId = item.MapId,
                    TerrainPath = item.TerrainPath,
                    CatCountMax = item.CatCountMax,
                    CatCountMin = item.CatCountMin,
                }).ToList(),
                Tasks = BuildTaskRows(corePackage, catPackage),
                Levels = BuildLevelRows(corePackage),
                AgencyBuildings = (corePackage.AgencyBuildings ?? Array.Empty<HolmasAgencyBuildingRow>()).Select(item => new ExportAgencyBuildingRow
                {
                    AgencyStageId = item.AgencyStageId,
                    StageName = item.StageName,
                    PromotionIds = item.PromotionIds.ToArray(),
                    PromotionLevelCaps = item.PromotionLevelCaps.ToArray(),
                    PromotionUpgradeCosts = item.PromotionUpgradeCosts.Select(costs => (costs?.Costs ?? Array.Empty<int>()).ToArray()).ToArray(),
                    Notes = item.Notes,
                }).ToList(),
            };
        }

        private static List<ExportTaskRow> BuildTaskRows(HolmasCoreConfigPackage corePackage, HolmasCatMetaPackage catPackage)
        {
            string[] catIds = (catPackage.Cats ?? Array.Empty<HolmasCatMetaRow>()).Select(item => item.CatId).ToArray();
            return (corePackage.Tasks ?? Array.Empty<HolmasTaskRow>()).Select(item => new ExportTaskRow
            {
                TaskTypeId = item.TaskTypeId,
                TaskKind = item.TaskKind.ToString(),
                CatIdList = (item.CatIndices ?? Array.Empty<int>())
                    .Where(index => index >= 0 && index < catIds.Length)
                    .Select(index => catIds[index])
                    .ToArray(),
                CountMax = item.CountMax,
                CountMin = item.CountMin,
                RewardArray = (item.RewardValues ?? Array.Empty<int>()).Select(value => value.ToString(CultureInfo.InvariantCulture)).ToArray(),
                LevelRewardFactor = item.LevelRewardFactor,
            }).ToList();
        }

        private static List<ExportLevelRow> BuildLevelRows(HolmasCoreConfigPackage corePackage)
        {
            string[] taskIds = (corePackage.Tasks ?? Array.Empty<HolmasTaskRow>()).Select(item => item.TaskTypeId).ToArray();
            string[] mapIds = (corePackage.Maps ?? Array.Empty<HolmasMapRow>()).Select(item => item.MapId).ToArray();
            return (corePackage.PlayerLevels ?? Array.Empty<HolmasPlayerLevelRow>()).Select(item => new ExportLevelRow
            {
                PlayerLevel = item.PlayerLevel,
                UpgradeExp = item.UpgradeExp,
                OfflineRewardPerHour = item.OfflineRewardPerHour,
                AdUnlockHours = item.AdUnlockHours,
                TaskTypeIds = (item.TaskTypeIndices ?? Array.Empty<int>())
                    .Where(index => index >= 0 && index < taskIds.Length)
                    .Select(index => taskIds[index])
                    .ToArray(),
                TaskTypeWeights = item.TaskTypeWeights.ToArray(),
                MapIds = (item.MapIndices ?? Array.Empty<int>())
                    .Where(index => index >= 0 && index < mapIds.Length)
                    .Select(index => mapIds[index])
                    .ToArray(),
                MapWeights = item.MapWeights.ToArray(),
            }).ToList();
        }

        private static HolmasTaskCatalog BuildTaskCatalog(ExportConfigTables tables)
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
                    OfflineRewardPerHour = item.OfflineRewardPerHour,
                    AdUnlockHours = item.AdUnlockHours,
                    TaskTypeIds = item.TaskTypeIds.ToArray(),
                    TaskTypeWeights = item.TaskTypeWeights.ToArray(),
                    MapIds = item.MapIds.ToArray(),
                    MapWeights = item.MapWeights.ToArray(),
                }));
        }

        private static HolmasMapCatalog BuildMapCatalog(ExportConfigTables tables)
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

        private static void AssertBundlePlayerLevelsMatchTables(HolmasConfigCatalogBundle bundle, IReadOnlyList<ExportLevelRow> expectedLevels)
        {
            Assert.That(bundle, Is.Not.Null);
            Assert.That(bundle.PlayerLevels.Count, Is.EqualTo(expectedLevels.Count));

            for (int i = 0; i < expectedLevels.Count; i++)
            {
                ExportLevelRow expected = expectedLevels[i];
                HolmasPlayerLevelDefinition actual = bundle.PlayerLevels[i];

                Assert.That(actual.PlayerLevel, Is.EqualTo(expected.PlayerLevel));
                Assert.That(actual.UpgradeExp, Is.EqualTo(expected.UpgradeExp));
                Assert.That(actual.OfflineRewardPerHour, Is.EqualTo(expected.OfflineRewardPerHour));
                Assert.That(actual.AdUnlockHours, Is.EqualTo(expected.AdUnlockHours));
                Assert.That(actual.TaskTypeIds, Is.EqualTo(expected.TaskTypeIds), $"等级 {expected.PlayerLevel} 的任务组不一致。");
                Assert.That(actual.TaskTypeWeights, Is.EqualTo(expected.TaskTypeWeights), $"等级 {expected.PlayerLevel} 的任务权重不一致。");
                Assert.That(actual.MapIds, Is.EqualTo(expected.MapIds), $"等级 {expected.PlayerLevel} 的地图组不一致。");
                Assert.That(actual.MapWeights, Is.EqualTo(expected.MapWeights), $"等级 {expected.PlayerLevel} 的地图权重不一致。");
            }
        }

        private static void AssertTaskBarHasUniqueCats(HolmasTaskBarState taskBar, int playerLevel)
        {
            IReadOnlyCollection<string> activeCatIds = taskBar.GetActiveCatIds();
            Assert.That(activeCatIds.Count, Is.EqualTo(taskBar.Tasks.Count), $"等级 {playerLevel} 的任务栏猫种不应重复。");
            Assert.That(activeCatIds.Count, Is.EqualTo(activeCatIds.Distinct(StringComparer.Ordinal).Count()), $"等级 {playerLevel} 的任务栏存在重复猫种。");
        }

        private static void AssertTaskRewardsMatchConfig(ExportConfigTables tables, IEnumerable<TaskInstanceData> tasks)
        {
            foreach (TaskInstanceData task in tasks)
            {
                Assert.That(task, Is.Not.Null);
                ExportTaskRow taskRow = tables.Tasks.Single(item => string.Equals(item.TaskTypeId, task.SourceTaskTypeId, StringComparison.Ordinal));
                ExportCatRow catRow = tables.Cats.Single(item => string.Equals(item.CatId, task.CatId, StringComparison.Ordinal));
                int expectedReward = (int)Math.Round(catRow.Price * task.TargetCount * taskRow.LevelRewardFactor, MidpointRounding.AwayFromZero);
                Assert.That(task.Reward, Is.EqualTo(expectedReward), $"任务 {task.SourceTaskTypeId}/{task.CatId} 奖励公式不匹配。");
            }
        }

        private static HolmasCoreConfigPackage BuildCorePackage(ExportConfigTables tables)
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
                    OfflineRewardPerHour = item.OfflineRewardPerHour,
                    AdUnlockHours = item.AdUnlockHours,
                    TaskTypeIndices = item.TaskTypeIds.Select(taskId => taskIndexById[taskId]).ToArray(),
                    TaskTypeWeights = item.TaskTypeWeights.ToArray(),
                    MapIndices = item.MapIds.Select(mapId => mapIndexById[mapId]).ToArray(),
                    MapWeights = item.MapWeights.ToArray(),
                }).ToArray(),
                AgencyBuildings = tables.AgencyBuildings.Select(item => new HolmasAgencyBuildingRow
                {
                    AgencyStageId = item.AgencyStageId,
                    StageName = item.StageName,
                    PromotionIds = item.PromotionIds.ToArray(),
                    PromotionLevelCaps = item.PromotionLevelCaps.ToArray(),
                    PromotionUpgradeCosts = item.PromotionUpgradeCosts.Select(costs => new HolmasAgencyBuildingCostRow
                    {
                        Costs = costs.ToArray(),
                    }).ToArray(),
                    Notes = item.Notes,
                }).ToArray(),
            };
        }

        private static HolmasCatMetaPackage BuildCatPackage(ExportConfigTables tables)
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

        private static string NormalizeTaskKindText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "Money" : value.Trim();
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

        private sealed class ExportConfigTables
        {
            public List<ExportCatRow> Cats { get; set; } = new List<ExportCatRow>();
            public List<ExportMapRow> Maps { get; set; } = new List<ExportMapRow>();
            public List<ExportTaskRow> Tasks { get; set; } = new List<ExportTaskRow>();
            public List<ExportLevelRow> Levels { get; set; } = new List<ExportLevelRow>();
            public List<ExportAgencyBuildingRow> AgencyBuildings { get; set; } = new List<ExportAgencyBuildingRow>();
        }

        private sealed class ExportCatRow
        {
            public string CatId = string.Empty;
            public string CatName = string.Empty;
            public string IconPath = string.Empty;
            public int Rarity;
            public int Weight;
            public int Price;
        }

        private sealed class ExportMapRow
        {
            public string MapId = string.Empty;
            public string TerrainPath = string.Empty;
            public int CatCountMax;
            public int CatCountMin;
        }

        private sealed class ExportTaskRow
        {
            public string TaskTypeId = string.Empty;
            public string TaskKind = string.Empty;
            public string[] CatIdList = Array.Empty<string>();
            public int CountMax;
            public int CountMin;
            public string[] RewardArray = Array.Empty<string>();
            public float LevelRewardFactor = 1f;
        }

        private sealed class ExportLevelRow
        {
            public int PlayerLevel;
            public int UpgradeExp;
            public int OfflineRewardPerHour;
            public int AdUnlockHours;
            public string[] TaskTypeIds = Array.Empty<string>();
            public int[] TaskTypeWeights = Array.Empty<int>();
            public string[] MapIds = Array.Empty<string>();
            public int[] MapWeights = Array.Empty<int>();
        }

        private sealed class ExportAgencyBuildingRow
        {
            public int AgencyStageId;
            public string StageName = string.Empty;
            public string[] PromotionIds = Array.Empty<string>();
            public int[] PromotionLevelCaps = Array.Empty<int>();
            public int[][] PromotionUpgradeCosts = Array.Empty<int[]>();
            public string Notes = string.Empty;
        }

        private static class ExportConfigValidator
        {
            public static List<string> Validate(ExportConfigTables tables)
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
                ValidateAgencyPromotions(tables.AgencyBuildings, errors);
                return errors;
            }

            private static void ValidateCats(IEnumerable<ExportCatRow> cats, List<string> errors)
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var cat in cats ?? Array.Empty<ExportCatRow>())
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

            private static void ValidateMaps(IEnumerable<ExportMapRow> maps, List<string> errors)
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var map in maps ?? Array.Empty<ExportMapRow>())
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

            private static void ValidateTasks(IEnumerable<ExportTaskRow> tasks, IReadOnlyList<ExportCatRow> cats, List<string> errors)
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                var catIds = new HashSet<string>((cats ?? Array.Empty<ExportCatRow>()).Where(item => item != null).Select(item => item.CatId), StringComparer.Ordinal);

                foreach (var task in tasks ?? Array.Empty<ExportTaskRow>())
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

            private static void ValidateLevels(IEnumerable<ExportLevelRow> levels, IReadOnlyList<ExportTaskRow> tasks, IReadOnlyList<ExportMapRow> maps, List<string> errors)
            {
                var taskIds = new HashSet<string>((tasks ?? Array.Empty<ExportTaskRow>()).Where(item => item != null).Select(item => item.TaskTypeId), StringComparer.Ordinal);
                var mapIds = new HashSet<string>((maps ?? Array.Empty<ExportMapRow>()).Where(item => item != null).Select(item => item.MapId), StringComparer.Ordinal);
                var seen = new HashSet<int>();
                ExportLevelRow previousLevel = null;

                foreach (var level in levels ?? Array.Empty<ExportLevelRow>())
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

                    if (level.OfflineRewardPerHour < 0)
                    {
                        errors.Add($"玩家等级离线奖励非法: {level.PlayerLevel}");
                    }

                    if (level.AdUnlockHours <= 0)
                    {
                        errors.Add($"玩家等级广告解锁时长非法: {level.PlayerLevel}");
                    }

                    if (previousLevel != null && level.UpgradeExp <= previousLevel.UpgradeExp)
                    {
                        errors.Add($"升级经验必须严格递增: {level.PlayerLevel}");
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

                    previousLevel = level;
                }
            }

            private static void ValidateAgencyPromotions(IEnumerable<ExportAgencyBuildingRow> buildings, List<string> errors)
            {
                var seenStages = new HashSet<int>();
                var seenStageNames = new HashSet<string>(StringComparer.Ordinal);
                int expectedStage = 1;
                string[] expectedPromotionIds = { "leaflet", "radio", "online", "tv" };
                int[] expectedPromotionCaps = { 5, 5, 5, 5 };

                foreach (var buildingRow in buildings ?? Array.Empty<ExportAgencyBuildingRow>())
                {
                    if (buildingRow == null)
                    {
                        errors.Add("宣传表存在空行。");
                        continue;
                    }

                    if (buildingRow.AgencyStageId <= 0)
                    {
                        errors.Add("宣传表的阶段 ID 非法。");
                        continue;
                    }

                    if (buildingRow.AgencyStageId != expectedStage)
                    {
                        errors.Add($"宣传表的阶段 ID 不连续: {buildingRow.AgencyStageId}");
                    }

                    if (!seenStages.Add(buildingRow.AgencyStageId))
                    {
                        errors.Add($"宣传表存在重复阶段 ID: {buildingRow.AgencyStageId}");
                    }

                    if (string.IsNullOrWhiteSpace(buildingRow.StageName))
                    {
                        errors.Add($"宣传表缺少城市名称: {buildingRow.AgencyStageId}");
                        continue;
                    }

                    if (!seenStageNames.Add(buildingRow.StageName))
                    {
                        errors.Add($"宣传表存在重复城市名称: {buildingRow.StageName}");
                    }

                    if (buildingRow.PromotionIds == null || buildingRow.PromotionIds.Length != 4)
                    {
                        errors.Add($"宣传表的宣传 ID 数量不正确: {buildingRow.AgencyStageId}");
                    }

                    if (buildingRow.PromotionLevelCaps == null || buildingRow.PromotionLevelCaps.Length != 4)
                    {
                        errors.Add($"宣传表的宣传 ID 与等级上限数量不一致: {buildingRow.AgencyStageId}");
                    }

                    if (buildingRow.PromotionUpgradeCosts == null || buildingRow.PromotionUpgradeCosts.Length != 4)
                    {
                        errors.Add($"宣传表的宣传 ID 与升级费用数量不一致: {buildingRow.AgencyStageId}");
                    }

                    int promotionCount = buildingRow.PromotionIds?.Length ?? 0;
                    int validationCount = Math.Min(promotionCount, expectedPromotionIds.Length);
                    for (int i = 0; i < validationCount; i++)
                    {
                        string promotionId = buildingRow.PromotionIds[i] ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(promotionId))
                        {
                            errors.Add($"宣传表存在空宣传 ID: {buildingRow.AgencyStageId}");
                            continue;
                        }

                        if (!string.Equals(promotionId, expectedPromotionIds[i], StringComparison.Ordinal))
                        {
                            errors.Add($"宣传表的宣传 ID 顺序不正确: {buildingRow.AgencyStageId}/{promotionId}");
                        }

                        if (buildingRow.PromotionLevelCaps != null && i < buildingRow.PromotionLevelCaps.Length && buildingRow.PromotionLevelCaps[i] != expectedPromotionCaps[i])
                        {
                            errors.Add($"宣传表等级上限非法: {buildingRow.AgencyStageId}/{promotionId}");
                        }

                        int[] costs = buildingRow.PromotionUpgradeCosts != null && i < buildingRow.PromotionUpgradeCosts.Length
                            ? buildingRow.PromotionUpgradeCosts[i]
                            : Array.Empty<int>();

                        int cap = buildingRow.PromotionLevelCaps != null && i < buildingRow.PromotionLevelCaps.Length
                            ? buildingRow.PromotionLevelCaps[i]
                            : 0;

                        if (costs == null || costs.Length != cap)
                        {
                            errors.Add($"宣传表升级费用数量与等级上限不一致: {buildingRow.AgencyStageId}/{promotionId}");
                        }
                        else
                        {
                            foreach (int cost in costs)
                            {
                                if (cost <= 0)
                                {
                                    errors.Add($"宣传表存在非正费用: {buildingRow.AgencyStageId}/{promotionId}");
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

        private static void OverwriteInt32(byte[] bytes, int offset, int value)
        {
            byte[] valueBytes = BitConverter.GetBytes(value);
            Buffer.BlockCopy(valueBytes, 0, bytes, offset, sizeof(int));
        }
    }
}
