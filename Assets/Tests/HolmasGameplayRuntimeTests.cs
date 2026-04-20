using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using App.HotUpdate.Holmas.Application;
using App.HotUpdate.Holmas.Board;
using App.HotUpdate.Holmas.Levels;
using App.HotUpdate.Holmas.Meta;
using App.HotUpdate.Holmas.Progression;
using App.HotUpdate.Holmas.Tasks.Config;
using App.HotUpdate.Holmas.Tasks.Runtime;
using App.HotUpdate.Holmas.Tasks.Services;
using App.HotUpdate.Holmas.Terrain;
using App.HotUpdate.Holmas.UI.Screens.Battle;
using App.HotUpdate.Holmas.UI.Screens.Main;
using App.Shared.Contracts;
using App.Shared.Holmas.RuntimeData;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TerrainAssetPathUtility = App.HotUpdate.Holmas.Terrain.HolmasTerrainAssetPathUtility;

namespace Holmas.Tests
{
    public sealed class HolmasGameplayRuntimeTests
    {
        [Test]
        public void HolmasGameplayRuntime_ConnectsMapCompletionTaskProgressAndMetaProgress()
        {
            var catalog = HolmasTestSupport.CreateStandardTaskCatalog();
            var randomSource = new ScriptedRandomSource(0, 0, 1, 0, 1, 1);
            var clock = new FixedUtcClock { UtcNowMilliseconds = 1000 };
            var taskService = new HolmasTaskProgressService(catalog, randomSource, clock);
            var metaService = new HolmasMetaProgressionService(
                HolmasTestSupport.CreateMetaCatalog(),
                catalog,
                new HolmasDefaultMetaExperienceSource(),
                new HolmasDefaultMetaExperienceSource());
            var coordinator = new HolmasProgressionCoordinator(taskService, metaService);
            var terrain = HolmasTestSupport.CreateTerrain(1, 1);
            var assetsRuntime = new FakeAssetsRuntime(terrain);
            var runtime = new HolmasGameplayRuntime(taskService, metaService, coordinator, new NullLogger(), assetsRuntime);

            runtime.RefillAvailableTasks(1);

            var request = HolmasTestSupport.CreateRequest(
                "map-1",
                TerrainAssetPathUtility.BuildAssetPath("1"),
                1,
                1,
                1,
                new BoardSpawnEntry { CatId = "cat-a", Weight = 1 });

            BoardRuntime boardRuntime = runtime.StartLevelAsync(request).GetAwaiter().GetResult();
            Assert.That(boardRuntime.TotalCatCount, Is.EqualTo(1));

            var reveal = runtime.RevealCell(0, out HolmasProgressionAdvanceResult progressionResult);

            Assert.That(reveal.IsValidAction, Is.True);
            Assert.That(reveal.Completed, Is.True);
            Assert.That(progressionResult, Is.Not.Null);
            Assert.That(progressionResult.ProgressedTaskIds, Has.Count.EqualTo(1));
            Assert.That(progressionResult.CompletedTaskIds, Has.Count.EqualTo(1));
            Assert.That(runtime.TaskBarState.GetTaskBySlot(0).Task.CurrentCount, Is.EqualTo(1));
            Assert.That(progressionResult.TaskExperienceGained, Is.EqualTo(0));
            Assert.That(progressionResult.MetaExperienceGained, Is.EqualTo(0));
            Assert.That(runtime.MetaProgressionState.Experience, Is.EqualTo(0));
            Assert.That(runtime.CurrentPlayerLevel, Is.EqualTo(1));

            var claim = runtime.ClaimTaskReward(0, 1);

            Assert.That(claim.Success, Is.True);
            Assert.That(claim.Reward, Is.EqualTo(20));
            Assert.That(runtime.CurrentGoldBalance, Is.EqualTo(20));
            Assert.That(runtime.MetaProgressionState.Experience, Is.EqualTo(0));
            Assert.That(runtime.MetaProgressionState.PlayerLevel, Is.EqualTo(1));
            Assert.That(runtime.MetaProgressionState.AgencyStageId, Is.EqualTo(1));
            Assert.That(runtime.MetaProgressionState.ClaimedTaskCount, Is.EqualTo(1));
        }

        [Test]
        public void HolmasGameplayRuntime_EnergyDefaultsToFullRecoveryLimit()
        {
            var clock = new FixedUtcClock { UtcNowMilliseconds = 1000 };
            HolmasGameplayRuntime runtime = CreateEnergyRuntime(clock);

            Assert.That(runtime.CurrentEnergy, Is.EqualTo(50));
            Assert.That(runtime.EnergyRecoveryLimit, Is.EqualTo(50));
            Assert.That(runtime.EnergyLabel, Is.EqualTo("50/50"));
            Assert.That(runtime.MetaProgressionState.EnergyInitialized, Is.True);
        }

        [Test]
        public void HolmasGameplayRuntime_RefreshEnergyRecovery_RestoresFivePerHourAndStopsAtLimit()
        {
            var clock = new FixedUtcClock { UtcNowMilliseconds = 1000 };
            var state = new HolmasMetaProgressionState
            {
                EnergyInitialized = true,
                EnergyCurrent = 45,
                EnergyRecoveryLimit = 50,
                EnergyLastRecoveryAtUtcMilliseconds = 1000,
            };
            HolmasGameplayRuntime runtime = CreateEnergyRuntime(clock, state);

            clock.UtcNowMilliseconds += 60L * 60L * 1000L;
            bool changed = runtime.RefreshEnergyRecovery();

            Assert.That(changed, Is.True);
            Assert.That(runtime.CurrentEnergy, Is.EqualTo(50));
            Assert.That(runtime.EnergyRecoveryLimit, Is.EqualTo(50));
        }

        [Test]
        public void HolmasGameplayRuntime_EnergyAboveLimitPausesRecoveryUntilConsumedBelowLimit()
        {
            var clock = new FixedUtcClock { UtcNowMilliseconds = 1000 };
            var state = new HolmasMetaProgressionState
            {
                EnergyInitialized = true,
                EnergyCurrent = 55,
                EnergyRecoveryLimit = 50,
                EnergyLastRecoveryAtUtcMilliseconds = 1000,
            };
            HolmasGameplayRuntime runtime = CreateEnergyRuntime(clock, state);
            runtime.StartLevel(
                HolmasTestSupport.CreateBoardTemplate(1, 12),
                new LevelSnapshot
                {
                    MapId = "energy",
                    TerrainPath = "energy",
                    RevealedCells = new bool[12],
                    SpawnedCats = new List<SpawnedCatData>
                    {
                        new SpawnedCatData { CatId = "cat-a", CellIndex = 1 },
                        new SpawnedCatData { CatId = "cat-a", CellIndex = 3 },
                        new SpawnedCatData { CatId = "cat-a", CellIndex = 5 },
                        new SpawnedCatData { CatId = "cat-a", CellIndex = 7 },
                        new SpawnedCatData { CatId = "cat-a", CellIndex = 9 },
                        new SpawnedCatData { CatId = "cat-a", CellIndex = 11 },
                    },
                });

            clock.UtcNowMilliseconds += 60L * 60L * 1000L;
            Assert.That(runtime.RefreshEnergyRecovery(), Is.False);
            Assert.That(runtime.CurrentEnergy, Is.EqualTo(55));

            for (int i = 0; i < 6; i++)
            {
                runtime.RevealCell(i * 2, HolmasBoardInteractionMode.Find, out _);
            }

            Assert.That(runtime.CurrentEnergy, Is.EqualTo(49));

            clock.UtcNowMilliseconds += 11L * 60L * 1000L;
            Assert.That(runtime.RefreshEnergyRecovery(), Is.False);
            Assert.That(runtime.CurrentEnergy, Is.EqualTo(49));

            clock.UtcNowMilliseconds += 60L * 1000L;
            Assert.That(runtime.RefreshEnergyRecovery(), Is.True);
            Assert.That(runtime.CurrentEnergy, Is.EqualTo(50));
        }

        [Test]
        public void HolmasGameplayRuntime_AddEnergy_IncreasesCurrentValueWithoutRaisingLimit()
        {
            var clock = new FixedUtcClock { UtcNowMilliseconds = 1000 };
            HolmasGameplayRuntime runtime = CreateEnergyRuntime(clock);

            runtime.AddEnergy();

            Assert.That(runtime.CurrentEnergy, Is.EqualTo(55));
            Assert.That(runtime.EnergyRecoveryLimit, Is.EqualTo(50));
            Assert.That(runtime.EnergyLabel, Is.EqualTo("55/50"));
        }

        [Test]
        public void HolmasGameplayRuntime_RevealCell_WalkConsumesEnergyOnlyWhenCatFound()
        {
            var clock = new FixedUtcClock { UtcNowMilliseconds = 1000 };
            HolmasGameplayRuntime runtime = CreateEnergyRuntime(clock);
            runtime.StartLevel(
                HolmasTestSupport.CreateBoardTemplate(1, 2),
                new LevelSnapshot
                {
                    MapId = "energy",
                    TerrainPath = "energy",
                    RevealedCells = new bool[2],
                    SpawnedCats = new List<SpawnedCatData>
                    {
                        new SpawnedCatData { CatId = "cat-a", CellIndex = 1 },
                    },
                });

            BoardRevealResult emptyReveal = runtime.RevealCell(0, HolmasBoardInteractionMode.Walk, out _);
            Assert.That(emptyReveal.IsValidAction, Is.True);
            Assert.That(runtime.CurrentEnergy, Is.EqualTo(50));

            BoardRevealResult catReveal = runtime.RevealCell(1, HolmasBoardInteractionMode.Walk, out _);
            Assert.That(catReveal.IsValidAction, Is.True);
            Assert.That(catReveal.FoundCat, Is.True);
            Assert.That(runtime.CurrentEnergy, Is.EqualTo(48));

            BoardRevealResult duplicateReveal = runtime.RevealCell(1, HolmasBoardInteractionMode.Walk, out _);
            Assert.That(duplicateReveal.IsValidAction, Is.False);
            Assert.That(runtime.CurrentEnergy, Is.EqualTo(48));

            BoardRevealResult invalidReveal = runtime.RevealCell(-1, HolmasBoardInteractionMode.Walk, out _);
            Assert.That(invalidReveal.IsValidAction, Is.False);
            Assert.That(runtime.CurrentEnergy, Is.EqualTo(48));
        }

        [Test]
        public void HolmasGameplayRuntime_RevealCell_FindAlwaysConsumesOneForValidReveal()
        {
            var clock = new FixedUtcClock { UtcNowMilliseconds = 1000 };
            HolmasGameplayRuntime runtime = CreateEnergyRuntime(clock);
            runtime.StartLevel(
                HolmasTestSupport.CreateBoardTemplate(1, 2),
                new LevelSnapshot
                {
                    MapId = "energy",
                    TerrainPath = "energy",
                    RevealedCells = new bool[2],
                    SpawnedCats = new List<SpawnedCatData>
                    {
                        new SpawnedCatData { CatId = "cat-a", CellIndex = 1 },
                    },
                });

            BoardRevealResult emptyReveal = runtime.RevealCell(0, HolmasBoardInteractionMode.Find, out _);
            Assert.That(emptyReveal.IsValidAction, Is.True);
            Assert.That(runtime.CurrentEnergy, Is.EqualTo(49));

            BoardRevealResult catReveal = runtime.RevealCell(1, HolmasBoardInteractionMode.Find, out _);
            Assert.That(catReveal.IsValidAction, Is.True);
            Assert.That(catReveal.FoundCat, Is.True);
            Assert.That(runtime.CurrentEnergy, Is.EqualTo(48));

            BoardRevealResult invalidReveal = runtime.RevealCell(-1, HolmasBoardInteractionMode.Find, out _);
            Assert.That(invalidReveal.IsValidAction, Is.False);
            Assert.That(runtime.CurrentEnergy, Is.EqualTo(48));
        }

        [Test]
        public void HolmasGameplayRuntime_RevealCell_NewInteractionIgnoresOldFlags()
        {
            var clock = new FixedUtcClock { UtcNowMilliseconds = 1000 };
            HolmasGameplayRuntime runtime = CreateEnergyRuntime(clock);
            runtime.StartLevel(
                HolmasTestSupport.CreateBoardTemplate(1, 2),
                new LevelSnapshot
                {
                    MapId = "energy",
                    TerrainPath = "energy",
                    RevealedCells = new bool[2],
                    SpawnedCats = new List<SpawnedCatData>
                    {
                        new SpawnedCatData { CatId = "cat-a", CellIndex = 1 },
                    },
                });

            BoardRevealResult flag = runtime.ToggleFlag(0);
            Assert.That(flag.IsValidAction, Is.True);
            Assert.That(runtime.CurrentBoardRuntime.IsFlagged(0), Is.True);

            BoardRevealResult reveal = runtime.RevealCell(0, HolmasBoardInteractionMode.Walk, out _);

            Assert.That(reveal.IsValidAction, Is.True);
            Assert.That(runtime.CurrentBoardRuntime.IsRevealed(0), Is.True);
            Assert.That(runtime.CurrentBoardRuntime.IsFlagged(0), Is.False);
            Assert.That(runtime.CurrentEnergy, Is.EqualTo(50));
        }

        [Test]
        public void HolmasGameplayRuntime_RevealCell_RejectsWhenEnergyInsufficient()
        {
            var clock = new FixedUtcClock { UtcNowMilliseconds = 1000 };
            var state = new HolmasMetaProgressionState
            {
                EnergyInitialized = true,
                EnergyCurrent = 1,
                EnergyRecoveryLimit = 50,
                EnergyLastRecoveryAtUtcMilliseconds = 1000,
            };
            HolmasGameplayRuntime runtime = CreateEnergyRuntime(clock, state);
            runtime.StartLevel(
                HolmasTestSupport.CreateBoardTemplate(1, 2),
                new LevelSnapshot
                {
                    MapId = "energy",
                    TerrainPath = "energy",
                    RevealedCells = new bool[2],
                    SpawnedCats = new List<SpawnedCatData>
                    {
                        new SpawnedCatData { CatId = "cat-a", CellIndex = 1 },
                    },
                });

            BoardRevealResult reveal = runtime.RevealCell(1, HolmasBoardInteractionMode.Walk, out _);

            Assert.That(reveal.IsValidAction, Is.False);
            Assert.That(reveal.FailureReason, Is.EqualTo("体力不足。"));
            Assert.That(runtime.CurrentEnergy, Is.EqualTo(1));
            Assert.That(runtime.CurrentBoardRuntime.IsRevealed(1), Is.False);
        }

        [Test]
        public void HolmasGameplayRuntime_RejectsSettlementBeforeLevelCompleted()
        {
            var catalog = HolmasTestSupport.CreateStandardTaskCatalog();
            var randomSource = new ScriptedRandomSource(0, 0, 1, 0, 1, 1);
            var clock = new FixedUtcClock { UtcNowMilliseconds = 1000 };
            var taskService = new HolmasTaskProgressService(catalog, randomSource, clock);
            var metaService = new HolmasMetaProgressionService(
                HolmasTestSupport.CreateMetaCatalog(),
                catalog,
                new HolmasDefaultMetaExperienceSource(),
                new HolmasDefaultMetaExperienceSource());
            var coordinator = new HolmasProgressionCoordinator(taskService, metaService);
            var terrain = HolmasTestSupport.CreateTerrain(1, 2);
            var assetsRuntime = new FakeAssetsRuntime(terrain);
            var runtime = new HolmasGameplayRuntime(taskService, metaService, coordinator, new NullLogger(), assetsRuntime);

            runtime.RefillAvailableTasks(1);

            var request = HolmasTestSupport.CreateRequest(
                "map-2",
                TerrainAssetPathUtility.BuildAssetPath("2"),
                1,
                1,
                1,
                new BoardSpawnEntry { CatId = "cat-a", Weight = 1 });

            runtime.StartLevelAsync(request).GetAwaiter().GetResult();

            Assert.Throws<System.InvalidOperationException>(() => runtime.ApplyCurrentLevelCompletion());
            Assert.That(runtime.MetaProgressionState.Experience, Is.EqualTo(0));
            Assert.That(runtime.TaskBarState.GetTaskBySlot(0).Task.CurrentCount, Is.EqualTo(0));
        }

        [Test]
        public void HolmasGameplayRuntime_DoesNotApplyCompletionTwice()
        {
            var catalog = HolmasTestSupport.CreateStandardTaskCatalog();
            var randomSource = new ScriptedRandomSource(0, 0, 1, 0, 1, 1);
            var clock = new FixedUtcClock { UtcNowMilliseconds = 1000 };
            var taskService = new HolmasTaskProgressService(catalog, randomSource, clock);
            var metaService = new HolmasMetaProgressionService(
                HolmasTestSupport.CreateMetaCatalog(),
                catalog,
                new HolmasDefaultMetaExperienceSource(),
                new HolmasDefaultMetaExperienceSource());
            var coordinator = new HolmasProgressionCoordinator(taskService, metaService);
            var terrain = HolmasTestSupport.CreateTerrain(1, 1);
            var assetsRuntime = new FakeAssetsRuntime(terrain);
            var runtime = new HolmasGameplayRuntime(taskService, metaService, coordinator, new NullLogger(), assetsRuntime);

            runtime.RefillAvailableTasks(1);

            var request = HolmasTestSupport.CreateRequest(
                "map-repeat",
                TerrainAssetPathUtility.BuildAssetPath("3"),
                1,
                1,
                1,
                new BoardSpawnEntry { CatId = "cat-a", Weight = 1 });

            runtime.StartLevelAsync(request).GetAwaiter().GetResult();
            var reveal = runtime.RevealCell(0, out HolmasProgressionAdvanceResult firstResult);

            Assert.That(reveal.Completed, Is.True);
            Assert.That(firstResult, Is.Not.Null);
            Assert.That(firstResult.MetaExperienceGained, Is.EqualTo(0));
            Assert.That(runtime.MetaProgressionState.Experience, Is.EqualTo(0));

            HolmasProgressionAdvanceResult secondResult = runtime.ApplyCurrentLevelCompletion();

            Assert.That(secondResult.ProgressedTaskIds, Is.Empty);
            Assert.That(secondResult.CompletedTaskIds, Is.Empty);
            Assert.That(secondResult.MetaExperienceGained, Is.EqualTo(0));
            Assert.That(runtime.MetaProgressionState.Experience, Is.EqualTo(0));
            Assert.That(runtime.TaskBarState.GetTaskBySlot(0).Task.CurrentCount, Is.EqualTo(1));
        }

        [Test]
        public void HolmasGameplayRuntime_EndCurrentLevelSession_ClearsCurrentBoardState()
        {
            var catalog = HolmasTestSupport.CreateStandardTaskCatalog();
            var randomSource = new ScriptedRandomSource(0, 0, 1, 0, 1, 1);
            var clock = new FixedUtcClock { UtcNowMilliseconds = 1000 };
            var taskService = new HolmasTaskProgressService(catalog, randomSource, clock);
            var metaService = new HolmasMetaProgressionService(
                HolmasTestSupport.CreateMetaCatalog(),
                catalog,
                new HolmasDefaultMetaExperienceSource(),
                new HolmasDefaultMetaExperienceSource());
            var coordinator = new HolmasProgressionCoordinator(taskService, metaService);
            var terrain = HolmasTestSupport.CreateTerrain(1, 1);
            var assetsRuntime = new FakeAssetsRuntime(terrain);
            var runtime = new HolmasGameplayRuntime(taskService, metaService, coordinator, new NullLogger(), assetsRuntime);

            var request = HolmasTestSupport.CreateRequest(
                "map-reset",
                TerrainAssetPathUtility.BuildAssetPath("4"),
                1,
                1,
                1,
                new BoardSpawnEntry { CatId = "cat-a", Weight = 1 });

            runtime.StartLevelAsync(request).GetAwaiter().GetResult();
            runtime.EndCurrentLevelSession();

            Assert.That(runtime.CurrentBoardTemplate, Is.Null);
            Assert.That(runtime.CurrentLevelSnapshot, Is.Null);
            Assert.That(runtime.CurrentBoardRuntime, Is.Null);
            Assert.Throws<System.InvalidOperationException>(() => runtime.RevealCell(0, out _));
        }

        [Test]
        public void HolmasGameplayRuntime_ApplyOfflineSettlement_AddsGoldOnly()
        {
            var catalog = HolmasTestSupport.CreateStandardTaskCatalog();
            var metaCatalog = CreateGrowthMetaCatalog();
            var metaService = new HolmasMetaProgressionService(
                metaCatalog,
                CreateGrowthTaskCatalog(),
                new HolmasDefaultMetaExperienceSource(metaCatalog),
                new HolmasDefaultMetaExperienceSource(metaCatalog),
                new FixedUtcClock { UtcNowMilliseconds = 777_000 });
            var taskService = new HolmasTaskProgressService(catalog, new ScriptedRandomSource(0, 0, 1, 0, 1, 1), new FixedUtcClock { UtcNowMilliseconds = 1000 });
            var coordinator = new HolmasProgressionCoordinator(taskService, metaService);
            var runtime = new HolmasGameplayRuntime(taskService, metaService, coordinator, new NullLogger());

            runtime.MetaProgressionState.PlayerLevel = 2;

            HolmasProgressionAdvanceResult result = runtime.ApplyOfflineSettlement(3_600_000L);

            Assert.That(result.OfflineRewardGained, Is.EqualTo(8));
            Assert.That(result.MetaExperienceGained, Is.EqualTo(0));
            Assert.That(runtime.MetaProgressionState.GoldBalance, Is.EqualTo(8));
            Assert.That(runtime.MetaProgressionState.OfflineRewardTotal, Is.EqualTo(8));
            Assert.That(runtime.MetaProgressionState.LastOfflineSettlementAtUtcMilliseconds, Is.EqualTo(777_000));
            Assert.That(runtime.MetaProgressionState.Experience, Is.EqualTo(0));
            Assert.That(runtime.CurrentPlayerLevel, Is.EqualTo(2));
        }

        [Test]
        public void HolmasGameplayRuntime_TryUpgradePromotion_ConsumesGoldAndAdvancesStage()
        {
            var catalog = HolmasTestSupport.CreateStandardTaskCatalog();
            var metaCatalog = CreateGrowthMetaCatalog();
            var metaService = new HolmasMetaProgressionService(
                metaCatalog,
                CreateGrowthTaskCatalog(),
                new HolmasDefaultMetaExperienceSource(metaCatalog),
                new HolmasDefaultMetaExperienceSource(metaCatalog),
                new FixedUtcClock { UtcNowMilliseconds = 777_000 });
            var agencyService = new HolmasAgencyProgressionService(CreatePromotionCatalog(), metaService);
            var taskService = new HolmasTaskProgressService(catalog, new ScriptedRandomSource(0, 0, 1, 0, 1, 1), new FixedUtcClock { UtcNowMilliseconds = 1000 });
            var coordinator = new HolmasProgressionCoordinator(taskService, metaService);
            var runtime = new HolmasGameplayRuntime(taskService, metaService, coordinator, agencyService, new NullLogger(), null);

            runtime.MetaProgressionState.GoldBalance = 30;

            HolmasAgencyUpgradeResult first = runtime.TryUpgradePromotion("lobby");

            Assert.That(first.Success, Is.True, first.FailureReason);
            Assert.That(first.GoldSpent, Is.EqualTo(10));
            Assert.That(first.PlayerLevelAfter, Is.EqualTo(2));
            Assert.That(first.ExperienceGained, Is.EqualTo(1));
            Assert.That(runtime.MetaProgressionState.Experience, Is.EqualTo(1));
            Assert.That(runtime.CurrentGoldBalance, Is.EqualTo(20));
            Assert.That(runtime.CurrentPlayerLevel, Is.EqualTo(2));
            Assert.That(runtime.CurrentAgencyStageId, Is.EqualTo(1));

            HolmasAgencyUpgradeResult second = runtime.TryUpgradePromotion("desk");

            Assert.That(second.Success, Is.True, second.FailureReason);
            Assert.That(second.GoldSpent, Is.EqualTo(20));
            Assert.That(second.StageAdvanced, Is.True);
            Assert.That(second.PlayerLevelAfter, Is.EqualTo(3));
            Assert.That(runtime.CurrentGoldBalance, Is.EqualTo(0));
            Assert.That(runtime.CurrentPlayerLevel, Is.EqualTo(3));
            Assert.That(runtime.CurrentAgencyStageId, Is.EqualTo(2));
        }

        [Test]
        public void HolmasGameplayRuntime_PromotionLevelUpChangesCurrentMapPool()
        {
            var catalog = CreateDifficultyFlowTaskCatalog();
            var metaCatalog = CreateGrowthMetaCatalog();
            var metaService = new HolmasMetaProgressionService(
                metaCatalog,
                catalog,
                new HolmasDefaultMetaExperienceSource(metaCatalog),
                new HolmasDefaultMetaExperienceSource(metaCatalog),
                new FixedUtcClock { UtcNowMilliseconds = 777_000 });
            var agencyService = new HolmasAgencyProgressionService(CreatePromotionCatalog(), metaService);
            var taskService = new HolmasTaskProgressService(catalog, new ScriptedRandomSource(0), new FixedUtcClock { UtcNowMilliseconds = 1000 });
            var coordinator = new HolmasProgressionCoordinator(taskService, metaService);
            var terrain = HolmasTestSupport.CreateTerrain(1, 2);
            var assetsRuntime = new FakeAssetsRuntime(terrain);
            var runtime = new HolmasGameplayRuntime(taskService, metaService, coordinator, agencyService, new NullLogger(), assetsRuntime);
            var context = new HolmasApplicationContext(
                new FakeServiceContainer(),
                new NullLogger(),
                new FakeTickManager(),
                new FakeEventBus(),
                assetsRuntime,
                runtime);
            var mapCatalog = new HolmasMapCatalog(new[]
            {
                new HolmasMapDefinition
                {
                    MapId = "map-lv1",
                    TerrainPath = "1",
                    CatCountMin = 1,
                    CatCountMax = 1,
                },
                new HolmasMapDefinition
                {
                    MapId = "map-lv2",
                    TerrainPath = "2",
                    CatCountMin = 1,
                    CatCountMax = 1,
                },
            });
            var requestGenerator = new HolmasLevelRequestGenerator(catalog, mapCatalog, new ScriptedRandomSource(0));
            var gateway = new HolmasLevelLaunchGateway(context, requestGenerator);

            runtime.MetaProgressionState.GoldBalance = 10;
            HolmasAgencyUpgradeResult upgrade = runtime.TryUpgradePromotion("lobby");
            BoardRuntime board = gateway.StartLevelForCurrentPlayerAsync(
                202,
                new[] { new BoardSpawnEntry { CatId = "cat-a", Weight = 1 } }).GetAwaiter().GetResult();

            Assert.That(upgrade.Success, Is.True, upgrade.FailureReason);
            Assert.That(runtime.CurrentPlayerLevel, Is.EqualTo(2));
            Assert.That(runtime.CurrentLevelSnapshot.MapId, Is.EqualTo("map-lv2"));
            Assert.That(runtime.CurrentLevelSnapshot.TerrainPath, Is.EqualTo(TerrainAssetPathUtility.BuildAssetPath("2")));
            Assert.That(board.TotalCatCount, Is.EqualTo(1));
        }

        [Test]
        public void MainPresenter_ShowsProgressionAndPromotionUpgradeCost()
        {
            var catalog = CreateDifficultyFlowTaskCatalog();
            var metaCatalog = CreateGrowthMetaCatalog();
            var metaService = new HolmasMetaProgressionService(
                metaCatalog,
                catalog,
                new HolmasDefaultMetaExperienceSource(metaCatalog),
                new HolmasDefaultMetaExperienceSource(metaCatalog),
                new FixedUtcClock { UtcNowMilliseconds = 777_000 });
            var agencyCatalog = CreatePromotionCatalog();
            var agencyService = new HolmasAgencyProgressionService(agencyCatalog, metaService);
            var taskService = new HolmasTaskProgressService(catalog, new ScriptedRandomSource(0), new FixedUtcClock { UtcNowMilliseconds = 1000 });
            var coordinator = new HolmasProgressionCoordinator(taskService, metaService);
            var runtime = new HolmasGameplayRuntime(taskService, metaService, coordinator, agencyService, new NullLogger(), null);
            var serviceContainer = new FakeServiceContainer();
            serviceContainer.RegisterSingleton<IHolmasTaskCatalog>(catalog);
            serviceContainer.RegisterSingleton<IHolmasAgencyCatalog>(agencyCatalog);
            var context = new HolmasApplicationContext(
                serviceContainer,
                new NullLogger(),
                new FakeTickManager(),
                new FakeEventBus(),
                null,
                runtime);

            runtime.MetaProgressionState.GoldBalance = 25;
            MainVm viewModel = new MainPresenter(context).Build();

            Assert.That(viewModel.Summary, Does.Contain("Lv 1 | Exp 0/1 | Gold 25 | Stage 1"));
            Assert.That(viewModel.Summary, Does.Contain("宣传 lobby Lv 0/1 | Next Cost 10 | +1 Exp"));
            Assert.That(viewModel.PromotionButtonLabel, Is.EqualTo("升级 lobby Lv 0->1 (10 Gold)"));
        }

        [Test]
        public void MainPresenter_DisablesPromotionButtonWhenCurrentStageIsFullyUpgraded()
        {
            var catalog = CreateDifficultyFlowTaskCatalog();
            var metaCatalog = CreateGrowthMetaCatalog();
            var metaService = new HolmasMetaProgressionService(
                metaCatalog,
                catalog,
                new HolmasDefaultMetaExperienceSource(metaCatalog),
                new HolmasDefaultMetaExperienceSource(metaCatalog),
                new FixedUtcClock { UtcNowMilliseconds = 777_000 });
            var agencyCatalog = CreatePromotionCatalog();
            var agencyService = new HolmasAgencyProgressionService(agencyCatalog, metaService);
            var taskService = new HolmasTaskProgressService(catalog, new ScriptedRandomSource(0), new FixedUtcClock { UtcNowMilliseconds = 1000 });
            var coordinator = new HolmasProgressionCoordinator(taskService, metaService);
            var runtime = new HolmasGameplayRuntime(taskService, metaService, coordinator, agencyService, new NullLogger(), null);
            var serviceContainer = new FakeServiceContainer();
            serviceContainer.RegisterSingleton<IHolmasTaskCatalog>(catalog);
            serviceContainer.RegisterSingleton<IHolmasAgencyCatalog>(agencyCatalog);
            var context = new HolmasApplicationContext(
                serviceContainer,
                new NullLogger(),
                new FakeTickManager(),
                new FakeEventBus(),
                null,
                runtime);

            runtime.MetaProgressionState.AgencyStageId = 2;
            HolmasAgencyPromotionStateKey.SetLevel(runtime.MetaProgressionState, 2, "archive", 2);

            MainVm viewModel = new MainPresenter(context).Build();

            Assert.That(viewModel.Summary, Does.Contain("宣传 暂无可升级项"));
            Assert.That(viewModel.PromotionButtonEnabled, Is.False);
            Assert.That(viewModel.PromotionButtonLabel, Is.EqualTo("宣传已满级"));
            Assert.That(viewModel.PromotionId, Is.Empty);
        }

        [Test]
        public void HolmasGameplayRuntime_TryUpgradePromotion_RejectsWhenGoldInsufficient()
        {
            var catalog = HolmasTestSupport.CreateStandardTaskCatalog();
            var metaCatalog = CreateGrowthMetaCatalog();
            var metaService = new HolmasMetaProgressionService(
                metaCatalog,
                CreateGrowthTaskCatalog(),
                new HolmasDefaultMetaExperienceSource(metaCatalog),
                new HolmasDefaultMetaExperienceSource(metaCatalog),
                new FixedUtcClock { UtcNowMilliseconds = 777_000 });
            var agencyService = new HolmasAgencyProgressionService(CreatePromotionCatalog(), metaService);
            var taskService = new HolmasTaskProgressService(catalog, new ScriptedRandomSource(0, 0, 1, 0, 1, 1), new FixedUtcClock { UtcNowMilliseconds = 1000 });
            var coordinator = new HolmasProgressionCoordinator(taskService, metaService);
            var runtime = new HolmasGameplayRuntime(taskService, metaService, coordinator, agencyService, new NullLogger(), null);

            runtime.MetaProgressionState.GoldBalance = 5;

            HolmasAgencyUpgradeResult result = runtime.TryUpgradePromotion("lobby");

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureReason, Does.Contain("金币不足"));
            Assert.That(runtime.CurrentGoldBalance, Is.EqualTo(5));
            Assert.That(runtime.CurrentPlayerLevel, Is.EqualTo(1));
            Assert.That(runtime.CurrentAgencyStageId, Is.EqualTo(1));
        }

        [Test]
        public void HolmasApplicationContext_StartLevelAsync_LoadsTerrainFromAssetsRuntime()
        {
            var catalog = HolmasTestSupport.CreateStandardTaskCatalog();
            var randomSource = new ScriptedRandomSource(0, 0, 1, 0, 1, 1);
            var clock = new FixedUtcClock { UtcNowMilliseconds = 1000 };
            var taskService = new HolmasTaskProgressService(catalog, randomSource, clock);
            var metaService = new HolmasMetaProgressionService(
                HolmasTestSupport.CreateMetaCatalog(),
                catalog,
                new HolmasDefaultMetaExperienceSource(),
                new HolmasDefaultMetaExperienceSource());
            var coordinator = new HolmasProgressionCoordinator(taskService, metaService);
            var terrain = HolmasTestSupport.CreateTerrain(1, 1);
            var assetsRuntime = new FakeAssetsRuntime(terrain);
            var runtime = new HolmasGameplayRuntime(taskService, metaService, coordinator, new NullLogger(), assetsRuntime);
            var context = new HolmasApplicationContext(
                new FakeServiceContainer(),
                new NullLogger(),
                new FakeTickManager(),
                new FakeEventBus(),
                assetsRuntime,
                runtime);
            var request = HolmasTestSupport.CreateRequest(
                "map-async",
                TerrainAssetPathUtility.BuildAssetPath("1"),
                1,
                1,
                1,
                new BoardSpawnEntry { CatId = "cat-a", Weight = 1 });

            BoardRuntime boardRuntime = context.StartLevelAsync(request).GetAwaiter().GetResult();

            Assert.That(assetsRuntime.LastRequestedLocation, Is.EqualTo(TerrainAssetPathUtility.BuildAssetPath("1")));
            Assert.That(assetsRuntime.LastHandle.ReleaseCount, Is.EqualTo(1));
            Assert.That(boardRuntime.TotalCatCount, Is.EqualTo(1));
            Assert.That(runtime.CurrentLevelSnapshot.TerrainPath, Is.EqualTo(TerrainAssetPathUtility.BuildAssetPath("1")));
            Assert.That(runtime.CurrentLevelSnapshot.MapId, Is.EqualTo("map-async"));
        }

        [Test]
        public void HolmasLevelLaunchGateway_StartLevelAsync_DelegatesToApplicationContext()
        {
            var catalog = HolmasTestSupport.CreateStandardTaskCatalog();
            var randomSource = new ScriptedRandomSource(0, 0, 1, 0, 1, 1);
            var clock = new FixedUtcClock { UtcNowMilliseconds = 1000 };
            var taskService = new HolmasTaskProgressService(catalog, randomSource, clock);
            var metaService = new HolmasMetaProgressionService(
                HolmasTestSupport.CreateMetaCatalog(),
                catalog,
                new HolmasDefaultMetaExperienceSource(),
                new HolmasDefaultMetaExperienceSource());
            var coordinator = new HolmasProgressionCoordinator(taskService, metaService);
            var terrain = HolmasTestSupport.CreateTerrain(1, 1);
            var assetsRuntime = new FakeAssetsRuntime(terrain);
            var runtime = new HolmasGameplayRuntime(taskService, metaService, coordinator, new NullLogger(), assetsRuntime);
            var context = new HolmasApplicationContext(
                new FakeServiceContainer(),
                new NullLogger(),
                new FakeTickManager(),
                new FakeEventBus(),
                assetsRuntime,
                runtime);
            var gateway = new HolmasLevelLaunchGateway(context);
            var request = HolmasTestSupport.CreateRequest(
                "map-gateway",
                TerrainAssetPathUtility.BuildAssetPath("1"),
                1,
                1,
                1,
                new BoardSpawnEntry { CatId = "cat-a", Weight = 1 });

            BoardRuntime boardRuntime = gateway.StartLevelAsync(request).GetAwaiter().GetResult();

            Assert.That(assetsRuntime.LastRequestedLocation, Is.EqualTo(TerrainAssetPathUtility.BuildAssetPath("1")));
            Assert.That(assetsRuntime.LastHandle.ReleaseCount, Is.EqualTo(1));
            Assert.That(boardRuntime.TotalCatCount, Is.EqualTo(1));
            Assert.That(runtime.CurrentLevelSnapshot.MapId, Is.EqualTo("map-gateway"));
        }

        [Test]
        public void HolmasLevelLaunchGateway_StartLevelForPlayerAsync_GeneratesRequestAndLaunchesMap()
        {
            var catalog = HolmasTestSupport.CreateStandardTaskCatalog();
            var mapCatalog = new HolmasMapCatalog(
                new[]
                {
                    new HolmasMapDefinition
                    {
                        MapId = "map-1",
                        TerrainPath = "2",
                        CatCountMin = 1,
                        CatCountMax = 2,
                    }
                });
            var randomSource = new ScriptedRandomSource(0, 0, 1, 0, 1, 1);
            var clock = new FixedUtcClock { UtcNowMilliseconds = 1000 };
            var taskService = new HolmasTaskProgressService(catalog, randomSource, clock);
            var metaService = new HolmasMetaProgressionService(
                HolmasTestSupport.CreateMetaCatalog(),
                catalog,
                new HolmasDefaultMetaExperienceSource(),
                new HolmasDefaultMetaExperienceSource());
            var coordinator = new HolmasProgressionCoordinator(taskService, metaService);
            var terrain = HolmasTestSupport.CreateTerrain(1, 2);
            var assetsRuntime = new FakeAssetsRuntime(terrain);
            var runtime = new HolmasGameplayRuntime(taskService, metaService, coordinator, new NullLogger(), assetsRuntime);
            var context = new HolmasApplicationContext(
                new FakeServiceContainer(),
                new NullLogger(),
                new FakeTickManager(),
                new FakeEventBus(),
                assetsRuntime,
                runtime);
            var requestGenerator = new HolmasLevelRequestGenerator(catalog, mapCatalog, new ScriptedRandomSource(0));
            var gateway = new HolmasLevelLaunchGateway(context, requestGenerator);

            BoardRuntime boardRuntime = gateway.StartLevelForPlayerAsync(
                1,
                11,
                new[]
                {
                    new BoardSpawnEntry { CatId = "cat-a", Weight = 1 },
                }).GetAwaiter().GetResult();

            Assert.That(assetsRuntime.LastRequestedLocation, Is.EqualTo(TerrainAssetPathUtility.BuildAssetPath("2")));
            Assert.That(runtime.CurrentLevelSnapshot.MapId, Is.EqualTo("map-1"));
            Assert.That(runtime.CurrentLevelSnapshot.Seed, Is.EqualTo(11));
            Assert.That(boardRuntime.TotalCatCount, Is.EqualTo(1));
        }

        [Test]
        public void HolmasLevelLaunchGateway_CanLoadNextLevelImmediatelyAfterAllCatsFound()
        {
            var catalog = HolmasTestSupport.CreateStandardTaskCatalog();
            var mapCatalog = new HolmasMapCatalog(
                new[]
                {
                    new HolmasMapDefinition
                    {
                        MapId = "map-1",
                        TerrainPath = "2",
                        CatCountMin = 1,
                        CatCountMax = 1,
                    }
                });
            var randomSource = new ScriptedRandomSource(0, 0, 1, 0, 1, 1);
            var clock = new FixedUtcClock { UtcNowMilliseconds = 1000 };
            var taskService = new HolmasTaskProgressService(catalog, randomSource, clock);
            var metaService = new HolmasMetaProgressionService(
                HolmasTestSupport.CreateMetaCatalog(),
                catalog,
                new HolmasDefaultMetaExperienceSource(),
                new HolmasDefaultMetaExperienceSource());
            var coordinator = new HolmasProgressionCoordinator(taskService, metaService);
            var terrain = HolmasTestSupport.CreateTerrain(1, 2);
            var assetsRuntime = new FakeAssetsRuntime(terrain);
            var runtime = new HolmasGameplayRuntime(taskService, metaService, coordinator, new NullLogger(), assetsRuntime);
            var context = new HolmasApplicationContext(
                new FakeServiceContainer(),
                new NullLogger(),
                new FakeTickManager(),
                new FakeEventBus(),
                assetsRuntime,
                runtime);
            var requestGenerator = new HolmasLevelRequestGenerator(catalog, mapCatalog, new ScriptedRandomSource(0, 0));
            var gateway = new HolmasLevelLaunchGateway(context, requestGenerator);

            BoardRuntime firstBoard = gateway.StartLevelForCurrentPlayerAsync(101).GetAwaiter().GetResult();
            int firstCatCellIndex = runtime.CurrentLevelSnapshot.SpawnedCats[0].CellIndex;
            BoardRevealResult reveal = runtime.RevealCell(firstCatCellIndex, out HolmasProgressionAdvanceResult progressionResult);

            Assert.That(firstBoard.TotalCatCount, Is.EqualTo(1));
            Assert.That(reveal.Completed, Is.True);
            Assert.That(progressionResult, Is.Not.Null);
            Assert.That(runtime.CurrentLevelSnapshot.Completed, Is.True);

            BoardRuntime nextBoard = gateway.StartLevelForCurrentPlayerAsync(202).GetAwaiter().GetResult();

            Assert.That(nextBoard, Is.Not.SameAs(firstBoard));
            Assert.That(runtime.CurrentLevelSnapshot.MapId, Is.EqualTo("map-1"));
            Assert.That(runtime.CurrentLevelSnapshot.Seed, Is.EqualTo(202));
            Assert.That(runtime.CurrentLevelSnapshot.Completed, Is.False);
            Assert.That(nextBoard.TotalCatCount, Is.EqualTo(1));
            Assert.That(nextBoard.FoundCatCount, Is.EqualTo(0));
            Assert.That(assetsRuntime.LastRequestedLocation, Is.EqualTo(TerrainAssetPathUtility.BuildAssetPath("2")));
        }

        [Test]
        public void HolmasGameplayRuntime_MapCompletionAdvancesTaskPartiallyAndAllowsNextBoard()
        {
            var catalog = CreateSingleCatTaskCatalog(targetCount: 13);
            var mapCatalog = new HolmasMapCatalog(
                new[]
                {
                    new HolmasMapDefinition
                    {
                        MapId = "map-partial-task",
                        TerrainPath = "2",
                        CatCountMin = 11,
                        CatCountMax = 11,
                    }
                });
            var randomSource = new ScriptedRandomSource(0, 0, 1, 0, 1, 1);
            var clock = new FixedUtcClock { UtcNowMilliseconds = 1000 };
            var taskService = new HolmasTaskProgressService(catalog, randomSource, clock);
            var metaService = new HolmasMetaProgressionService(
                HolmasTestSupport.CreateMetaCatalog(),
                catalog,
                new HolmasDefaultMetaExperienceSource(),
                new HolmasDefaultMetaExperienceSource());
            var coordinator = new HolmasProgressionCoordinator(taskService, metaService);
            var terrain = HolmasTestSupport.CreateTerrain(1, 11);
            var assetsRuntime = new FakeAssetsRuntime(terrain);
            var runtime = new HolmasGameplayRuntime(taskService, metaService, coordinator, new NullLogger(), assetsRuntime);
            var context = new HolmasApplicationContext(
                new FakeServiceContainer(),
                new NullLogger(),
                new FakeTickManager(),
                new FakeEventBus(),
                assetsRuntime,
                runtime);
            var requestGenerator = new HolmasLevelRequestGenerator(catalog, mapCatalog, new ScriptedRandomSource(0, 0));
            var gateway = new HolmasLevelLaunchGateway(context, requestGenerator);

            runtime.RefillAvailableTasks(1);
            BoardRuntime firstBoard = gateway.StartLevelForCurrentPlayerAsync(101).GetAwaiter().GetResult();
            BattleVm initialViewModel = new BattlePresenter(context).Build();

            Assert.That(firstBoard.TotalCatCount, Is.EqualTo(11));
            Assert.That(runtime.TaskBarState.GetTaskBySlot(0).Task.TargetCount, Is.EqualTo(13));
            Assert.That(initialViewModel.Summary, Does.Contain("Map map-partial-task | Terrain 2.asset"));
            Assert.That(initialViewModel.Summary, Does.Contain("Board Cats 0/11"));
            Assert.That(initialViewModel.Summary, Does.Contain("Hidden 11"));
            Assert.That(initialViewModel.Summary, Does.Contain("Task 0/13"));

            BoardRevealResult finalReveal = null;
            HolmasProgressionAdvanceResult finalProgression = null;
            foreach (SpawnedCatData spawnedCat in runtime.CurrentLevelSnapshot.SpawnedCats)
            {
                finalReveal = runtime.RevealCell(spawnedCat.CellIndex, out finalProgression);
            }

            BattleVm completedViewModel = new BattlePresenter(context).Build();
            HolmasTaskRuntimeInstance task = runtime.TaskBarState.GetTaskBySlot(0);

            Assert.That(finalReveal, Is.Not.Null);
            Assert.That(finalReveal.Completed, Is.True);
            Assert.That(finalProgression, Is.Not.Null);
            Assert.That(firstBoard.Completed, Is.True);
            Assert.That(task.Task.CurrentCount, Is.EqualTo(11));
            Assert.That(task.Task.TargetCount, Is.EqualTo(13));
            Assert.That(task.IsCompleted, Is.False);
            Assert.That(completedViewModel.Summary, Does.Contain("Board Cats 11/11"));
            Assert.That(completedViewModel.Summary, Does.Contain("Hidden 0"));
            Assert.That(completedViewModel.Summary, Does.Contain("Task 11/13"));

            BoardRuntime nextBoard = gateway.StartLevelForCurrentPlayerAsync(202).GetAwaiter().GetResult();

            Assert.That(nextBoard, Is.Not.SameAs(firstBoard));
            Assert.That(runtime.CurrentLevelSnapshot.Completed, Is.False);
            Assert.That(nextBoard.TotalCatCount, Is.EqualTo(11));
            Assert.That(task.Task.CurrentCount, Is.EqualTo(11));
        }

        [Test]
        public void BattleCellView_UnrevealedValidCellIsVisiblyClickable()
        {
            var cellObject = new GameObject("cell", typeof(RectTransform), typeof(Image), typeof(BattleCellView));
            try
            {
                BattleCellView view = cellObject.GetComponent<BattleCellView>();
                typeof(BattleCellView)
                    .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(view, null);
                var state = new BoardCellState(
                    3,
                    true,
                    false,
                    false,
                    true,
                    0,
                    new Color32(30, 60, 100, 255));

                view.Bind(state, null);

                Image background = cellObject.GetComponent<Image>();
                Outline outline = cellObject.GetComponent<Outline>();
                TextMeshProUGUI label = cellObject.GetComponentInChildren<TextMeshProUGUI>();

                Assert.That(background.raycastTarget, Is.True);
                Assert.That(outline.enabled, Is.True);
                Assert.That(label.text, Is.EqualTo("?"));
            }
            finally
            {
                Object.DestroyImmediate(cellObject);
            }
        }

        [Test]
        public void HolmasGameplayRuntime_StartLevelAsync_RequiresAssetsRuntime()
        {
            var catalog = HolmasTestSupport.CreateStandardTaskCatalog();
            var randomSource = new ScriptedRandomSource(0, 0, 1, 0, 1, 1);
            var clock = new FixedUtcClock { UtcNowMilliseconds = 1000 };
            var taskService = new HolmasTaskProgressService(catalog, randomSource, clock);
            var metaService = new HolmasMetaProgressionService(
                HolmasTestSupport.CreateMetaCatalog(),
                catalog,
                new HolmasDefaultMetaExperienceSource(),
                new HolmasDefaultMetaExperienceSource());
            var coordinator = new HolmasProgressionCoordinator(taskService, metaService);
            var runtime = new HolmasGameplayRuntime(taskService, metaService, coordinator, new NullLogger());
            var request = HolmasTestSupport.CreateRequest(
                "map-missing-loader",
                TerrainAssetPathUtility.BuildAssetPath("1"),
                1,
                1,
                1,
                new BoardSpawnEntry { CatId = "cat-a", Weight = 1 });

            var ex = Assert.Throws<System.InvalidOperationException>(() => runtime.StartLevelAsync(request).GetAwaiter().GetResult());
            Assert.That(ex.Message, Does.Contain("IAssetsRuntime"));
        }

        [Test]
        public void HolmasGameplayRuntime_StartLevelAsync_RejectsEmptyTerrainPath()
        {
            var catalog = HolmasTestSupport.CreateStandardTaskCatalog();
            var randomSource = new ScriptedRandomSource(0, 0, 1, 0, 1, 1);
            var clock = new FixedUtcClock { UtcNowMilliseconds = 1000 };
            var taskService = new HolmasTaskProgressService(catalog, randomSource, clock);
            var metaService = new HolmasMetaProgressionService(
                HolmasTestSupport.CreateMetaCatalog(),
                catalog,
                new HolmasDefaultMetaExperienceSource(),
                new HolmasDefaultMetaExperienceSource());
            var coordinator = new HolmasProgressionCoordinator(taskService, metaService);
            var terrain = HolmasTestSupport.CreateTerrain(1, 1);
            var assetsRuntime = new FakeAssetsRuntime(terrain);
            var runtime = new HolmasGameplayRuntime(taskService, metaService, coordinator, new NullLogger(), assetsRuntime);
            var request = HolmasTestSupport.CreateRequest(
                "map-empty-path",
                string.Empty,
                1,
                1,
                1,
                new BoardSpawnEntry { CatId = "cat-a", Weight = 1 });

            var ex = Assert.Throws<System.InvalidOperationException>(() => runtime.StartLevelAsync(request).GetAwaiter().GetResult());

            Assert.That(ex.Message, Does.Contain("TerrainPath"));
        }

        [Test]
        public void HolmasGameplayRuntime_StartLevelAsync_FailsWhenTerrainAssetCannotBeLoaded()
        {
            var catalog = HolmasTestSupport.CreateStandardTaskCatalog();
            var randomSource = new ScriptedRandomSource(0, 0, 1, 0, 1, 1);
            var clock = new FixedUtcClock { UtcNowMilliseconds = 1000 };
            var taskService = new HolmasTaskProgressService(catalog, randomSource, clock);
            var metaService = new HolmasMetaProgressionService(
                HolmasTestSupport.CreateMetaCatalog(),
                catalog,
                new HolmasDefaultMetaExperienceSource(),
                new HolmasDefaultMetaExperienceSource());
            var coordinator = new HolmasProgressionCoordinator(taskService, metaService);
            var assetsRuntime = new FakeAssetsRuntime(asset: null, returnNullHandle: true);
            var runtime = new HolmasGameplayRuntime(taskService, metaService, coordinator, new NullLogger(), assetsRuntime);
            var request = HolmasTestSupport.CreateRequest(
                "map-missing-asset",
                TerrainAssetPathUtility.BuildAssetPath("missing"),
                1,
                1,
                1,
                new BoardSpawnEntry { CatId = "cat-a", Weight = 1 });

            var ex = Assert.Throws<System.InvalidOperationException>(() => runtime.StartLevelAsync(request).GetAwaiter().GetResult());

            Assert.That(ex.Message, Does.Contain("无法从资源入口加载地形"));
        }

        [Test]
        public void HolmasGameplayRuntime_StartLevelAsync_RejectsInvalidTerrainAssetType()
        {
            var catalog = HolmasTestSupport.CreateStandardTaskCatalog();
            var randomSource = new ScriptedRandomSource(0, 0, 1, 0, 1, 1);
            var clock = new FixedUtcClock { UtcNowMilliseconds = 1000 };
            var taskService = new HolmasTaskProgressService(catalog, randomSource, clock);
            var metaService = new HolmasMetaProgressionService(
                HolmasTestSupport.CreateMetaCatalog(),
                catalog,
                new HolmasDefaultMetaExperienceSource(),
                new HolmasDefaultMetaExperienceSource());
            var coordinator = new HolmasProgressionCoordinator(taskService, metaService);
            var invalidTerrain = ScriptableObject.CreateInstance<InvalidTerrainAsset>();
            var assetsRuntime = new FakeAssetsRuntime(invalidTerrain);
            var runtime = new HolmasGameplayRuntime(taskService, metaService, coordinator, new NullLogger(), assetsRuntime);
            var request = HolmasTestSupport.CreateRequest(
                "map-invalid-type",
                TerrainAssetPathUtility.BuildAssetPath("invalid"),
                1,
                1,
                1,
                new BoardSpawnEntry { CatId = "cat-a", Weight = 1 });

            var ex = Assert.Throws<System.InvalidOperationException>(() => runtime.StartLevelAsync(request).GetAwaiter().GetResult());

            Assert.That(ex.Message, Does.Contain("does not expose the expected board template API"));
        }

        private sealed class FakeAssetsRuntime : IAssetsRuntime
        {
            private readonly UnityEngine.Object _asset;
            private readonly bool _returnNullHandle;

            public FakeAssetsRuntime(UnityEngine.Object asset, bool returnNullHandle = false)
            {
                _asset = asset;
                _returnNullHandle = returnNullHandle;
            }

            public string LastRequestedLocation { get; private set; }

            public FakeAssetHandle LastHandle { get; private set; }

            public Task InitializeAsync()
            {
                return Task.CompletedTask;
            }

            public Task<bool> RunPatchFlowAsync(string packageVersion = null)
            {
                return Task.FromResult(true);
            }

            public Task<IAssetHandle> LoadAssetAsync(string location)
            {
                LastRequestedLocation = location;
                if (_returnNullHandle)
                {
                    LastHandle = null;
                    return Task.FromResult<IAssetHandle>(null);
                }

                LastHandle = new FakeAssetHandle(_asset);
                return Task.FromResult<IAssetHandle>(LastHandle);
            }

            public void Shutdown()
            {
            }
        }

        private sealed class FakeAssetHandle : IAssetHandle
        {
            public FakeAssetHandle(UnityEngine.Object asset)
            {
                AssetObject = asset;
            }

            public UnityEngine.Object AssetObject { get; }

            public int ReleaseCount { get; private set; }

            public void Release()
            {
                ReleaseCount++;
            }
        }

        private sealed class FakeServiceContainer : IServiceContainer
        {
            private readonly Dictionary<System.Type, object> _instances = new Dictionary<System.Type, object>();

            public void RegisterSingleton<T>(T instance) where T : class
            {
                if (instance != null)
                {
                    _instances[typeof(T)] = instance;
                }
            }

            public T Get<T>() where T : class
            {
                return _instances.TryGetValue(typeof(T), out object instance)
                    ? instance as T
                    : null;
            }

            public bool IsRegistered<T>()
            {
                return _instances.ContainsKey(typeof(T));
            }
        }

        private sealed class FakeTickManager : ITickManager
        {
            public void Register(ITickable tickable)
            {
            }

            public void Unregister(ITickable tickable)
            {
            }
        }

        private sealed class FakeEventBus : IEventBus
        {
            public void Subscribe<T>(System.Action<T> handler) where T : class
            {
            }

            public void Unsubscribe<T>(System.Action<T> handler) where T : class
            {
            }

            public void Publish<T>(T eventData) where T : class
            {
            }
        }

        private sealed class InvalidTerrainAsset : ScriptableObject
        {
        }

        private static HolmasTaskCatalog CreateSingleCatTaskCatalog(int targetCount)
        {
            return new HolmasTaskCatalog(
                new[]
                {
                    new HolmasCatDefinition { CatId = "cat-a", Price = 10, Weight = 1 },
                },
                new[]
                {
                    new HolmasTaskTemplateDefinition
                    {
                        TaskTypeId = "task-normal",
                        CatIdList = new[] { "cat-a" },
                        CountMin = targetCount,
                        CountMax = targetCount,
                        RewardArray = System.Array.Empty<string>(),
                        LevelRewardFactor = 2f,
                    }
                },
                new[]
                {
                    new HolmasPlayerLevelDefinition
                    {
                        PlayerLevel = 1,
                        UpgradeExp = 0,
                        TaskTypeIds = new[] { "task-normal" },
                        TaskTypeWeights = new[] { 1 },
                        MapIds = new[] { "map-partial-task" },
                        MapWeights = new[] { 1 },
                    }
                });
        }

        private static HolmasMetaCatalog CreateGrowthMetaCatalog()
        {
            return new HolmasMetaCatalog(new[]
            {
                new HolmasMetaProgressionDefinition
                {
                    PlayerLevel = 1,
                    MinExperience = 0,
                    OfflineRewardPerHour = 6,
                    AdUnlockHours = 24,
                },
                new HolmasMetaProgressionDefinition
                {
                    PlayerLevel = 2,
                    MinExperience = 1,
                    OfflineRewardPerHour = 8,
                    AdUnlockHours = 12,
                },
                new HolmasMetaProgressionDefinition
                {
                    PlayerLevel = 3,
                    MinExperience = 2,
                    OfflineRewardPerHour = 10,
                    AdUnlockHours = 24,
                },
            });
        }

        private static HolmasTaskCatalog CreateGrowthTaskCatalog()
        {
            return new HolmasTaskCatalog(
                null,
                null,
                new[]
                {
                    new HolmasPlayerLevelDefinition { PlayerLevel = 1, UpgradeExp = 900 },
                    new HolmasPlayerLevelDefinition { PlayerLevel = 2, UpgradeExp = 1900 },
                    new HolmasPlayerLevelDefinition { PlayerLevel = 3, UpgradeExp = 2900 },
                });
        }

        private static HolmasTaskCatalog CreateDifficultyFlowTaskCatalog()
        {
            return new HolmasTaskCatalog(
                new[]
                {
                    new HolmasCatDefinition { CatId = "cat-a", Price = 10, Weight = 1 },
                },
                new[]
                {
                    new HolmasTaskTemplateDefinition
                    {
                        TaskTypeId = "task-normal",
                        CatIdList = new[] { "cat-a" },
                        CountMin = 1,
                        CountMax = 1,
                        RewardArray = System.Array.Empty<string>(),
                        LevelRewardFactor = 1f,
                    }
                },
                new[]
                {
                    new HolmasPlayerLevelDefinition
                    {
                        PlayerLevel = 1,
                        UpgradeExp = 0,
                        TaskTypeIds = new[] { "task-normal" },
                        TaskTypeWeights = new[] { 1 },
                        MapIds = new[] { "map-lv1" },
                        MapWeights = new[] { 1 },
                    },
                    new HolmasPlayerLevelDefinition
                    {
                        PlayerLevel = 2,
                        UpgradeExp = 1,
                        TaskTypeIds = new[] { "task-normal" },
                        TaskTypeWeights = new[] { 1 },
                        MapIds = new[] { "map-lv2" },
                        MapWeights = new[] { 1 },
                    },
                });
        }

        private static HolmasAgencyCatalog CreatePromotionCatalog()
        {
            return new HolmasAgencyCatalog(new[]
            {
                new HolmasAgencyBuildingDefinition
                {
                    AgencyStageId = 1,
                    StageName = "stage-1",
                    PromotionId = "lobby",
                    PromotionLevelCap = 1,
                    PromotionUpgradeCosts = new[] { 10 },
                },
                new HolmasAgencyBuildingDefinition
                {
                    AgencyStageId = 1,
                    StageName = "stage-1",
                    PromotionId = "desk",
                    PromotionLevelCap = 1,
                    PromotionUpgradeCosts = new[] { 20 },
                },
                new HolmasAgencyBuildingDefinition
                {
                    AgencyStageId = 2,
                    StageName = "stage-2",
                    PromotionId = "archive",
                    PromotionLevelCap = 2,
                    PromotionUpgradeCosts = new[] { 30, 40 },
                },
            });
        }

        private static HolmasGameplayRuntime CreateEnergyRuntime(
            FixedUtcClock clock,
            HolmasMetaProgressionState state = null)
        {
            var catalog = HolmasTestSupport.CreateStandardTaskCatalog();
            var taskService = new HolmasTaskProgressService(catalog, new ScriptedRandomSource(), clock);
            var metaService = new HolmasMetaProgressionService(
                HolmasTestSupport.CreateMetaCatalog(),
                catalog,
                new HolmasDefaultMetaExperienceSource(),
                new HolmasDefaultMetaExperienceSource(),
                clock);
            var coordinator = new HolmasProgressionCoordinator(taskService, metaService);
            return new HolmasGameplayRuntime(
                taskService,
                metaService,
                coordinator,
                null,
                new NullLogger(),
                null,
                null,
                state,
                clock);
        }
    }
}
