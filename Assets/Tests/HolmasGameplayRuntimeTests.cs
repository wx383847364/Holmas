using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using App.AOT.Infrastructure.EventBus;
using App.HotUpdate.Holmas.Application;
using App.HotUpdate.Holmas.Board;
using App.HotUpdate.Holmas.Levels;
using App.HotUpdate.Holmas.Meta;
using App.HotUpdate.Holmas.Progression;
using App.HotUpdate.Holmas.Tasks.Config;
using App.HotUpdate.Holmas.Tasks.Runtime;
using App.HotUpdate.Holmas.Tasks.Services;
using App.HotUpdate.Holmas.Terrain;
using App.HotUpdate.Holmas.UI.Core;
using App.HotUpdate.Holmas.UI.Screens.AgencyMain;
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
            Assert.That(progressionResult.ProgressedTaskIds, Has.Count.EqualTo(0), "任务进度应在翻到猫当下写入 runtime，而不是等到整图结算时再重复累计。");
            Assert.That(progressionResult.CompletedTaskIds, Has.Count.EqualTo(0));
            Assert.That(runtime.CurrentGoldBalance, Is.EqualTo(20));
            Assert.That(runtime.TaskBarState.GetTaskBySlot(0), Is.Not.Null);
            Assert.That(runtime.TaskBarState.GetTaskBySlot(0).Task.CurrentCount, Is.EqualTo(0));
            Assert.That(progressionResult.TaskExperienceGained, Is.EqualTo(0));
            Assert.That(progressionResult.MetaExperienceGained, Is.EqualTo(0));
            Assert.That(runtime.MetaProgressionState.Experience, Is.EqualTo(0));
            Assert.That(runtime.CurrentPlayerLevel, Is.EqualTo(1));
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
        public void HolmasGameplayRuntime_AddEnergy_PublishesDomainEventsAfterLegacyStateChanged()
        {
            var clock = new FixedUtcClock { UtcNowMilliseconds = 1000 };
            var eventBus = new EventBus();
            HolmasGameplayRuntime runtime = CreateEnergyRuntime(clock, eventBus: eventBus);
            var order = new List<string>();
            HolmasEnergyChangedEvent energyEvent = null;

            runtime.StateChanged += reason => order.Add("legacy:" + reason);
            eventBus.SubscribeScoped<HolmasGameplayStateChangedEvent>(eventData => order.Add("domain:" + eventData.Reason));
            eventBus.SubscribeScoped<HolmasEnergyChangedEvent>(eventData =>
            {
                energyEvent = eventData;
                order.Add("energy:" + eventData.Reason);
            });

            runtime.AddEnergy();

            CollectionAssert.AreEqual(
                new[]
                {
                    "legacy:EnergyChanged",
                    "domain:EnergyChanged",
                    "energy:EnergyChanged",
                },
                order);
            Assert.That(energyEvent, Is.Not.Null);
            Assert.That(energyEvent.CurrentEnergy, Is.EqualTo(55));
            Assert.That(energyEvent.EnergyRecoveryLimit, Is.EqualTo(50));
            Assert.That(energyEvent.EnergyLabel, Is.EqualTo("55/50"));
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
            Assert.That(runtime.CurrentGoldBalance, Is.EqualTo(20));
            Assert.That(runtime.TaskBarState.GetTaskBySlot(0).Task.CurrentCount, Is.EqualTo(0));
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
            Assert.That(viewModel.PromotionButtonLabel, Is.EqualTo("城市宣传"));
            Assert.That(viewModel.PromotionButtonEnabled, Is.True);
        }

        [Test]
        public void MainPresenter_AllowsPublicityMapEntryWhenCurrentStageIsFullyUpgraded()
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
            Assert.That(viewModel.PromotionButtonEnabled, Is.True);
            Assert.That(viewModel.PromotionButtonLabel, Is.EqualTo("城市宣传"));
            Assert.That(viewModel.PromotionId, Is.Empty);
        }

        [Test]
        public void BattlePresenter_BuildsPublicityMapFromAgencyStages()
        {
            HolmasApplicationContext context = CreatePromotionMapTestContext(out _);
            context.GameplayRuntime.MetaProgressionState.GoldBalance = 25;

            BattleVm viewModel = new BattlePresenter(context).Build(selectedStageId: 1);

            Assert.That(viewModel.SelectedStageId, Is.EqualTo(1));
            Assert.That(viewModel.Stages, Has.Length.EqualTo(BattlePresenter.VisibleStageCount));
            Assert.That(viewModel.Stages[0].Visible, Is.True);
            Assert.That(viewModel.Stages[0].Unlocked, Is.True);
            Assert.That(viewModel.Stages[0].Current, Is.True);
            Assert.That(viewModel.Stages[0].Selected, Is.True);
            Assert.That(viewModel.Stages[0].StageImage, Is.EqualTo("Assets/HotUpdateContent/Res/Textures/buildings/building01.png"));
            Assert.That(viewModel.Stages[1].Visible, Is.True);
            Assert.That(viewModel.Stages[1].Unlocked, Is.False);
            Assert.That(viewModel.BuildStages, Has.Length.EqualTo(BattlePresenter.VisibleStageCount));
            Assert.That(viewModel.BuildStages[0].AgencyStageId, Is.EqualTo(viewModel.Stages[0].AgencyStageId));
            Assert.That(viewModel.BuildStages[0].StageImage, Is.EqualTo(viewModel.Stages[0].StageImage));
            Assert.That(viewModel.BuildStages[0].ProgressLabel, Is.EqualTo(viewModel.Stages[0].ProgressLabel));
            Assert.That(viewModel.BuildStages[0].Selected, Is.True);
            Assert.That(viewModel.BuildStages[1].Unlocked, Is.False);
            Assert.That(viewModel.BuildButtonEnabled, Is.True);
            Assert.That(viewModel.BuildButtonLabel, Is.EqualTo("stage-1\n宣传 0->1/2\n金币 -10"));
            Assert.That(viewModel.BuildStages[0].StarCount, Is.EqualTo(0));
            Assert.That(viewModel.StageBars[0].Visible, Is.True);
            Assert.That(viewModel.StageBars[0].Progress, Is.EqualTo(0f));
        }

        [Test]
        public void BattlePresenter_BuildCardStarsFollowSelectedStageProgress()
        {
            HolmasApplicationContext context = CreatePromotionMapTestContext(out _);
            HolmasAgencyPromotionStateKey.SetLevel(context.GameplayRuntime.MetaProgressionState, 1, "lobby", 1);

            BattleVm viewModel = new BattlePresenter(context).Build(selectedStageId: 1);

            Assert.That(viewModel.BuildStages[0].StarCount, Is.EqualTo(2));
            Assert.That(viewModel.BuildButtonLabel, Is.EqualTo("stage-1\n宣传 1->2/2\n金币 -20"));
        }

        [Test]
        public void BattlePresenter_BuildCardShowsFiveStarsOnlyWhenStageComplete()
        {
            HolmasAgencyCatalog agencyCatalog = CreateFifteenCapPromotionCatalog();
            HolmasApplicationContext context = CreatePromotionMapTestContext(agencyCatalog);

            HolmasAgencyPromotionStateKey.SetLevel(context.GameplayRuntime.MetaProgressionState, 1, "district", 14);
            BattleVm almostComplete = new BattlePresenter(context).Build(selectedStageId: 1);

            Assert.That(almostComplete.BuildStages[0].StarCount, Is.EqualTo(4));

            HolmasAgencyPromotionStateKey.SetLevel(context.GameplayRuntime.MetaProgressionState, 1, "district", 15);
            BattleVm complete = new BattlePresenter(context).Build(selectedStageId: 1);

            Assert.That(complete.BuildStages[0].StarCount, Is.EqualTo(5));
        }

        [Test]
        public void BattlePresenter_DisablesBuildForHistoryAndLockedStages()
        {
            HolmasApplicationContext context = CreatePromotionMapTestContext(out _);

            BattleVm lockedStage = new BattlePresenter(context).Build(selectedStageId: 2);
            Assert.That(lockedStage.BuildButtonEnabled, Is.False);
            Assert.That(lockedStage.BuildButtonLabel, Is.EqualTo("城市尚未解锁"));
            Assert.That(lockedStage.BuildStages[1].StageImage, Is.EqualTo("Assets/HotUpdateContent/Res/Textures/buildings/building02.png"));
            Assert.That(lockedStage.BuildStages[1].Unlocked, Is.False);
            Assert.That(lockedStage.BuildStages[1].StarCount, Is.EqualTo(0));

            context.GameplayRuntime.MetaProgressionState.AgencyStageId = 2;
            BattleVm historyStage = new BattlePresenter(context).Build(selectedStageId: 1);
            Assert.That(historyStage.BuildButtonEnabled, Is.False);
            Assert.That(historyStage.BuildButtonLabel, Is.EqualTo("stage-1\n已完成/仅回看"));
            Assert.That(historyStage.SelectedStageId, Is.EqualTo(1));
            Assert.That(historyStage.Stages[0].Completed, Is.True);
            Assert.That(historyStage.Stages[0].Selected, Is.True);
            Assert.That(historyStage.Stages[0].ProgressLabel, Is.EqualTo("2/2"));
            Assert.That(historyStage.BuildStages[0].Selected, Is.True);
            Assert.That(historyStage.BuildStages[0].StarCount, Is.EqualTo(5));
        }

        [Test]
        public void MainPresenter_RevealFoundCat_RefreshesTaskProgressImmediately()
        {
            var catalog = CreateSingleCatTaskCatalog(targetCount: 2);
            var clock = new FixedUtcClock { UtcNowMilliseconds = 1000 };
            var taskService = new HolmasTaskProgressService(catalog, new ScriptedRandomSource(0, 0, 1), clock);
            var metaService = new HolmasMetaProgressionService(
                HolmasTestSupport.CreateMetaCatalog(),
                catalog,
                new HolmasDefaultMetaExperienceSource(),
                new HolmasDefaultMetaExperienceSource(),
                clock);
            var coordinator = new HolmasProgressionCoordinator(taskService, metaService);
            var runtime = new HolmasGameplayRuntime(taskService, metaService, coordinator, new NullLogger(), null);
            runtime.RefillAvailableTasks(1);

            var request = HolmasTestSupport.CreateRequest(
                "map-progress",
                TerrainAssetPathUtility.BuildAssetPath("1"),
                1,
                1,
                1,
                new BoardSpawnEntry { CatId = "cat-a", Weight = 1 });

            runtime.StartLevel(HolmasTestSupport.CreateTerrain(1, 1), request);

            var serviceContainer = new FakeServiceContainer();
            serviceContainer.RegisterSingleton<IHolmasTaskCatalog>(catalog);
            var context = new HolmasApplicationContext(
                serviceContainer,
                new NullLogger(),
                new FakeTickManager(),
                new FakeEventBus(),
                null,
                runtime);

            BoardRevealResult reveal = runtime.RevealCell(0, out HolmasProgressionAdvanceResult progressionResult);
            MainVm viewModel = new MainPresenter(context).Build();

            Assert.That(reveal.IsValidAction, Is.True);
            Assert.That(reveal.FoundCat, Is.True);
            Assert.That(reveal.Completed, Is.True, "单猫棋盘应在找到猫后完成，但任务目标仍可保留为跨图累计。");
            Assert.That(progressionResult, Is.Not.Null);
            Assert.That(runtime.TaskBarState.GetTaskBySlot(0).Task.CurrentCount, Is.EqualTo(1));
            Assert.That(runtime.CurrentLevelSnapshot.SpawnedCats[0].CatId, Is.EqualTo("cat-a"));
            Assert.That(viewModel.TaskItems[0].Progress, Is.EqualTo("1/2"));
        }

        [Test]
        public void HolmasGameplayRuntime_RevealCell_AutoClaimsCompletedTaskDuringActiveLevel()
        {
            var catalog = CreateMultiCatTaskCatalog("cat-a", "cat-b", "cat-c");
            var clock = new FixedUtcClock { UtcNowMilliseconds = 1000 };
            var taskService = new HolmasTaskProgressService(catalog, new ScriptedRandomSource(0, 0, 0, 0, 0, 0, 0, 0), clock);
            var metaService = new HolmasMetaProgressionService(
                HolmasTestSupport.CreateMetaCatalog(),
                catalog,
                new HolmasDefaultMetaExperienceSource(),
                new HolmasDefaultMetaExperienceSource(),
                clock);
            var coordinator = new HolmasProgressionCoordinator(taskService, metaService);
            var runtime = new HolmasGameplayRuntime(taskService, metaService, coordinator, new NullLogger(), null);
            int rewardClaimEventCount = 0;
            runtime.StateChanged += reason =>
            {
                if (reason == HolmasGameplayRuntimeStateChangeReason.TaskRewardClaimed)
                {
                    rewardClaimEventCount++;
                }
            };

            runtime.RefillAvailableTasks(1);
            HolmasTaskRuntimeInstance originalTask = runtime.TaskBarState.GetTaskBySlot(0);
            Assert.That(originalTask, Is.Not.Null);
            runtime.StartLevel(
                HolmasTestSupport.CreateTerrain(1, 3),
                HolmasTestSupport.CreateRequest(
                    "map-active-claim",
                    TerrainAssetPathUtility.BuildAssetPath("1"),
                    23,
                    3,
                    3,
                    new BoardSpawnEntry { CatId = "cat-a", Weight = 1 }));

            int firstCellIndex = runtime.CurrentLevelSnapshot.SpawnedCats[0].CellIndex;
            BoardRevealResult reveal = runtime.RevealCell(firstCellIndex, out _);

            Assert.That(reveal.IsValidAction, Is.True);
            Assert.That(runtime.CurrentLevelSnapshot.SpawnedCats.Single(item => item.CellIndex == firstCellIndex).CatId, Is.EqualTo(originalTask.Task.CatId));
            Assert.That(runtime.HasActiveUncompletedLevel, Is.True);
            Assert.That(runtime.CurrentGoldBalance, Is.EqualTo(10));
            Assert.That(runtime.MetaProgressionState.Experience, Is.EqualTo(0));
            Assert.That(runtime.CurrentPlayerLevel, Is.EqualTo(1));
            Assert.That(runtime.MetaProgressionState.ClaimedTaskCount, Is.EqualTo(1));
            Assert.That(rewardClaimEventCount, Is.EqualTo(1));
            Assert.That(runtime.LastTaskRewardTip, Does.Contain("金币 +10"));
            Assert.That(runtime.TaskBarState.GetTaskBySlot(0), Is.Not.Null);
            Assert.That(runtime.TaskBarState.GetUnlockedEmptySlotCount(), Is.EqualTo(0));
            Assert.That(runtime.TaskBarState.GetTaskBySlot(0).Task.CurrentCount, Is.EqualTo(0));
            Assert.That(runtime.TaskBarState.GetTaskBySlot(0).Task.TaskInstanceId, Is.Not.EqualTo(originalTask.Task.TaskInstanceId));

            int secondCellIndex = runtime.CurrentLevelSnapshot.SpawnedCats.First(item => string.IsNullOrEmpty(item.CatId)).CellIndex;
            var uncompletedCatsBeforeSecondReveal = new HashSet<string>(
                runtime.TaskBarState.Tasks
                    .Where(item => item != null && item.Task != null && !item.IsRewardClaimed && item.Task.CurrentCount < item.Task.TargetCount)
                    .Select(item => item.Task.CatId));

            BoardRevealResult secondReveal = runtime.RevealCell(secondCellIndex, out _);

            Assert.That(secondReveal.IsValidAction, Is.True);
            string secondResolvedCatId = runtime.CurrentLevelSnapshot.SpawnedCats.Single(item => item.CellIndex == secondCellIndex).CatId;
            Assert.That(uncompletedCatsBeforeSecondReveal.Contains(secondResolvedCatId), Is.True);
        }

        [Test]
        public void HolmasGameplayRuntime_RevealCell_SingleUncompletedTaskResolvesAllBlindBoxCatsToThatCat()
        {
            var catalog = CreateSingleCatTaskCatalog(targetCount: 3);
            var clock = new FixedUtcClock { UtcNowMilliseconds = 1000 };
            var taskService = new HolmasTaskProgressService(catalog, new ScriptedRandomSource(0, 0, 1), clock);
            var metaService = new HolmasMetaProgressionService(
                HolmasTestSupport.CreateMetaCatalog(),
                catalog,
                new HolmasDefaultMetaExperienceSource(),
                new HolmasDefaultMetaExperienceSource(),
                clock);
            var coordinator = new HolmasProgressionCoordinator(taskService, metaService);
            var runtime = new HolmasGameplayRuntime(taskService, metaService, coordinator, new NullLogger(), null);

            runtime.RefillAvailableTasks(1);
            runtime.StartLevel(
                HolmasTestSupport.CreateTerrain(1, 2),
                HolmasTestSupport.CreateRequest(
                    "map-single-task-blind-box",
                    TerrainAssetPathUtility.BuildAssetPath("1"),
                    23,
                    2,
                    2));

            foreach (SpawnedCatData spawnedCat in runtime.CurrentLevelSnapshot.SpawnedCats.ToArray())
            {
                BoardRevealResult reveal = runtime.RevealCell(spawnedCat.CellIndex, out _);
                Assert.That(reveal.IsValidAction, Is.True);
            }

            Assert.That(runtime.CurrentLevelSnapshot.SpawnedCats.Select(item => item.CatId), Is.All.EqualTo("cat-a"));
            Assert.That(runtime.TaskBarState.GetTaskBySlot(0).Task.CurrentCount, Is.EqualTo(2));
            Assert.That(runtime.CurrentGoldBalance, Is.EqualTo(0));
        }

        [Test]
        public void HolmasGameplayRuntime_StartLevel_ClearsUnrevealedOrdinaryCatIdBeforeReveal()
        {
            var catalog = CreateSingleCatTaskCatalog(targetCount: 2);
            var clock = new FixedUtcClock { UtcNowMilliseconds = 1000 };
            var taskService = new HolmasTaskProgressService(catalog, new ScriptedRandomSource(0), clock);
            var metaService = new HolmasMetaProgressionService(
                HolmasTestSupport.CreateMetaCatalog(),
                catalog,
                new HolmasDefaultMetaExperienceSource(),
                new HolmasDefaultMetaExperienceSource(),
                clock);
            var coordinator = new HolmasProgressionCoordinator(taskService, metaService);
            var runtime = new HolmasGameplayRuntime(taskService, metaService, coordinator, new NullLogger(), null);

            runtime.RefillAvailableTasks(1);
            var snapshot = new LevelSnapshot
            {
                MapId = "map-legacy-preassigned-cat",
                TerrainPath = TerrainAssetPathUtility.BuildAssetPath("legacy"),
                Seed = 1,
                RevealedCells = new bool[1],
                SpawnedCats = new List<SpawnedCatData>
                {
                    new SpawnedCatData
                    {
                        CellIndex = 0,
                        CatId = "cat-legacy",
                    }
                },
            };

            runtime.StartLevel(HolmasTestSupport.CreateBoardTemplate(1, 1), snapshot);
            Assert.That(runtime.CurrentLevelSnapshot.SpawnedCats[0].CatId, Is.Empty);
            Assert.That(runtime.CurrentBoardRuntime.GetCellState(0).CatId, Is.Empty);

            BoardRevealResult reveal = runtime.RevealCell(0, out _);

            Assert.That(reveal.IsValidAction, Is.True);
            Assert.That(runtime.CurrentLevelSnapshot.SpawnedCats[0].CatId, Is.EqualTo("cat-a"));
            Assert.That(runtime.CurrentBoardRuntime.GetCellState(0).CatId, Is.EqualTo("cat-a"));
            Assert.That(runtime.TaskBarState.GetTaskBySlot(0).Task.CurrentCount, Is.EqualTo(1));
        }

        [Test]
        public void HolmasGameplayRuntime_StartLevel_KeepsRevealedOrdinaryCatIdForDisplay()
        {
            var catalog = CreateSingleCatTaskCatalog(targetCount: 2);
            var clock = new FixedUtcClock { UtcNowMilliseconds = 1000 };
            var taskService = new HolmasTaskProgressService(catalog, new ScriptedRandomSource(0), clock);
            var metaService = new HolmasMetaProgressionService(
                HolmasTestSupport.CreateMetaCatalog(),
                catalog,
                new HolmasDefaultMetaExperienceSource(),
                new HolmasDefaultMetaExperienceSource(),
                clock);
            var coordinator = new HolmasProgressionCoordinator(taskService, metaService);
            var runtime = new HolmasGameplayRuntime(taskService, metaService, coordinator, new NullLogger(), null);

            var snapshot = new LevelSnapshot
            {
                MapId = "map-restored-revealed-cat",
                TerrainPath = TerrainAssetPathUtility.BuildAssetPath("legacy"),
                Seed = 1,
                RevealedCells = new[] { true, false },
                SpawnedCats = new List<SpawnedCatData>
                {
                    new SpawnedCatData
                    {
                        CellIndex = 0,
                        CatId = "cat-shown",
                    },
                    new SpawnedCatData
                    {
                        CellIndex = 1,
                        CatId = "cat-unrevealed-legacy",
                    },
                },
            };

            runtime.StartLevel(HolmasTestSupport.CreateBoardTemplate(1, 2), snapshot);

            Assert.That(runtime.CurrentLevelSnapshot.SpawnedCats[0].CatId, Is.EqualTo("cat-shown"));
            Assert.That(runtime.CurrentBoardRuntime.GetCellState(0).CatId, Is.EqualTo("cat-shown"));
            Assert.That(runtime.CurrentLevelSnapshot.SpawnedCats[1].CatId, Is.Empty);
            Assert.That(runtime.CurrentBoardRuntime.GetCellState(1).CatId, Is.Empty);
        }

        [Test]
        public void HolmasGameplayRuntime_RevealCell_WithoutUncompletedTasksRevealsCatWithoutProgress()
        {
            var catalog = CreateSingleCatTaskCatalog(targetCount: 1);
            var clock = new FixedUtcClock { UtcNowMilliseconds = 1000 };
            var taskService = new HolmasTaskProgressService(catalog, new ScriptedRandomSource(), clock);
            var metaService = new HolmasMetaProgressionService(
                HolmasTestSupport.CreateMetaCatalog(),
                catalog,
                new HolmasDefaultMetaExperienceSource(),
                new HolmasDefaultMetaExperienceSource(),
                clock);
            var coordinator = new HolmasProgressionCoordinator(taskService, metaService);
            var runtime = new HolmasGameplayRuntime(taskService, metaService, coordinator, new NullLogger(), null);

            runtime.StartLevel(
                HolmasTestSupport.CreateTerrain(1, 1),
                HolmasTestSupport.CreateRequest(
                    "map-no-task-blind-box",
                    TerrainAssetPathUtility.BuildAssetPath("1"),
                    23,
                    1,
                    1));

            BoardRevealResult reveal = runtime.RevealCell(runtime.CurrentLevelSnapshot.SpawnedCats[0].CellIndex, out HolmasProgressionAdvanceResult progression);

            Assert.That(reveal.IsValidAction, Is.True);
            Assert.That(reveal.Completed, Is.True);
            Assert.That(progression, Is.Not.Null);
            Assert.That(runtime.CurrentLevelSnapshot.SpawnedCats[0].CatId, Is.Empty);
            Assert.That(runtime.CurrentGoldBalance, Is.EqualTo(0));
            Assert.That(runtime.MetaProgressionState.ClaimedTaskCount, Is.EqualTo(0));
        }

        [Test]
        public void HolmasGameplayRuntime_ApplyFoundCatProgress_MultipleCompletedTasks_EmitsOneRewardTip()
        {
            var catalog = CreateMultiCatTaskCatalog("cat-a", "cat-b", "cat-c", "cat-d");
            var clock = new FixedUtcClock { UtcNowMilliseconds = 1000 };
            var taskService = new HolmasTaskProgressService(catalog, new ScriptedRandomSource(0, 0, 0, 0, 0, 0, 0, 0, 0, 0), clock);
            var metaService = new HolmasMetaProgressionService(
                HolmasTestSupport.CreateMetaCatalog(),
                catalog,
                new HolmasDefaultMetaExperienceSource(),
                new HolmasDefaultMetaExperienceSource(),
                clock);
            var coordinator = new HolmasProgressionCoordinator(taskService, metaService);
            var runtime = new HolmasGameplayRuntime(taskService, metaService, coordinator, new NullLogger(), null);
            int rewardClaimEventCount = 0;
            runtime.StateChanged += reason =>
            {
                if (reason == HolmasGameplayRuntimeStateChangeReason.TaskRewardClaimed)
                {
                    rewardClaimEventCount++;
                }
            };

            runtime.RefillAvailableTasks(1);
            Assert.That(runtime.TaskBarState.GetTaskBySlot(0).Task.CatId, Is.EqualTo("cat-a"));
            Assert.That(runtime.TaskBarState.GetTaskBySlot(1).Task.CatId, Is.EqualTo("cat-b"));
            runtime.StartLevel(
                HolmasTestSupport.CreateBoardTemplate(1, 2),
                new LevelSnapshot
                {
                    MapId = "multi-claim",
                    TerrainPath = "multi-claim",
                    RevealedCells = new[] { true, true },
                    SpawnedCats = new List<SpawnedCatData>
                    {
                        new SpawnedCatData { CatId = "cat-a", CellIndex = 0 },
                        new SpawnedCatData { CatId = "cat-b", CellIndex = 1 },
                    },
                });

            var revealResult = new BoardRevealResult(0)
            {
                IsValidAction = true,
                FoundCat = true,
            };
            revealResult.FoundCatCellIndices.Add(0);
            revealResult.FoundCatCellIndices.Add(1);

            MethodInfo applyFoundCatProgress = typeof(HolmasGameplayRuntime)
                .GetMethod("ApplyFoundCatProgress", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(applyFoundCatProgress, Is.Not.Null);
            applyFoundCatProgress.Invoke(runtime, new object[] { revealResult });

            Assert.That(runtime.CurrentGoldBalance, Is.EqualTo(21));
            Assert.That(runtime.MetaProgressionState.ClaimedTaskCount, Is.EqualTo(2));
            Assert.That(runtime.MetaProgressionState.Experience, Is.EqualTo(0));
            Assert.That(rewardClaimEventCount, Is.EqualTo(1));
            Assert.That(runtime.LastTaskRewardTip, Does.Contain("完成 2 个任务"));
            Assert.That(runtime.LastTaskRewardTip, Does.Contain("金币 +21"));
            Assert.That(runtime.TaskBarState.GetTaskBySlot(0).Task.CurrentCount, Is.EqualTo(0));
            Assert.That(runtime.TaskBarState.GetTaskBySlot(1).Task.CurrentCount, Is.EqualTo(0));
        }

        [Test]
        public void HolmasGameplayRuntime_SettleClaimableTasksAndRefill_ClaimsExistingFullTasks()
        {
            var catalog = CreateMultiCatTaskCatalog("cat-a", "cat-b", "cat-c", "cat-d");
            var clock = new FixedUtcClock { UtcNowMilliseconds = 1000 };
            var taskService = new HolmasTaskProgressService(catalog, new ScriptedRandomSource(0, 0, 0, 0, 0, 0, 0, 0, 0, 0), clock);
            var metaService = new HolmasMetaProgressionService(
                HolmasTestSupport.CreateMetaCatalog(),
                catalog,
                new HolmasDefaultMetaExperienceSource(),
                new HolmasDefaultMetaExperienceSource(),
                clock);
            var coordinator = new HolmasProgressionCoordinator(taskService, metaService);
            var runtime = new HolmasGameplayRuntime(taskService, metaService, coordinator, new NullLogger(), null);
            int rewardClaimEventCount = 0;
            runtime.StateChanged += reason =>
            {
                if (reason == HolmasGameplayRuntimeStateChangeReason.TaskRewardClaimed)
                {
                    rewardClaimEventCount++;
                }
            };

            runtime.RefillAvailableTasks(1);
            HolmasTaskRuntimeInstance firstTask = runtime.TaskBarState.GetTaskBySlot(0);
            HolmasTaskRuntimeInstance secondTask = runtime.TaskBarState.GetTaskBySlot(1);
            string firstTaskId = firstTask.Task.TaskInstanceId;
            string secondTaskId = secondTask.Task.TaskInstanceId;
            int expectedReward = firstTask.Task.Reward + secondTask.Task.Reward;
            firstTask.Task.CurrentCount = firstTask.Task.TargetCount;
            secondTask.Task.CurrentCount = secondTask.Task.TargetCount;

            HolmasTaskSettlementResult result = runtime.SettleClaimableTasksAndRefill(1);

            Assert.That(result.ClaimedTaskCount, Is.EqualTo(2));
            Assert.That(result.TotalReward, Is.EqualTo(expectedReward));
            Assert.That(result.RefilledTaskCount, Is.EqualTo(2));
            Assert.That(runtime.CurrentGoldBalance, Is.EqualTo(expectedReward));
            Assert.That(runtime.MetaProgressionState.ClaimedTaskCount, Is.EqualTo(2));
            Assert.That(runtime.MetaProgressionState.Experience, Is.EqualTo(0));
            Assert.That(runtime.CurrentPlayerLevel, Is.EqualTo(1));
            Assert.That(rewardClaimEventCount, Is.EqualTo(1));
            Assert.That(runtime.LastTaskRewardTip, Does.Contain("完成 2 个任务"));
            Assert.That(runtime.LastTaskRewardTip, Does.Contain($"金币 +{expectedReward}"));
            Assert.That(runtime.TaskBarState.GetTaskBySlot(0).Task.TaskInstanceId, Is.Not.EqualTo(firstTaskId));
            Assert.That(runtime.TaskBarState.GetTaskBySlot(1).Task.TaskInstanceId, Is.Not.EqualTo(secondTaskId));
            Assert.That(runtime.TaskBarState.GetTaskBySlot(0).Task.CurrentCount, Is.EqualTo(0));
            Assert.That(runtime.TaskBarState.GetTaskBySlot(1).Task.CurrentCount, Is.EqualTo(0));
        }

        [Test]
        public void HolmasGameplayRuntime_SettleClaimableTasksAndRefill_PendingRelockSlotLocksAfterClaim()
        {
            var catalog = CreateMultiCatTaskCatalog("cat-a", "cat-b");
            var clock = new FixedUtcClock { UtcNowMilliseconds = 1000 };
            var taskService = new HolmasTaskProgressService(catalog, new ScriptedRandomSource(0, 0, 0, 0), clock);
            var metaService = new HolmasMetaProgressionService(
                HolmasTestSupport.CreateMetaCatalog(),
                catalog,
                new HolmasDefaultMetaExperienceSource(),
                new HolmasDefaultMetaExperienceSource(),
                clock);
            var coordinator = new HolmasProgressionCoordinator(taskService, metaService);
            var runtime = new HolmasGameplayRuntime(taskService, metaService, coordinator, new NullLogger(), null);

            HolmasTaskSlotUnlockResult unlock = runtime.UnlockAdSlot(2, 1, 2000);
            HolmasTaskRuntimeInstance task = runtime.TaskBarState.GetTaskBySlot(2);
            Assert.That(unlock.Success, Is.True);
            Assert.That(task, Is.Not.Null);
            task.Task.CurrentCount = task.Task.TargetCount;
            runtime.TaskBarState.MarkPendingRelockAfterTaskCompletion(2);

            HolmasTaskSettlementResult result = runtime.SettleClaimableTasksAndRefill(1);

            Assert.That(result.ClaimedTaskCount, Is.EqualTo(1));
            Assert.That(runtime.CurrentGoldBalance, Is.EqualTo(task.Task.Reward));
            Assert.That(runtime.MetaProgressionState.ClaimedTaskCount, Is.EqualTo(1));
            Assert.That(runtime.MetaProgressionState.Experience, Is.EqualTo(0));
            Assert.That(runtime.TaskBarState.GetSlot(2).IsUnlocked, Is.False);
            Assert.That(runtime.TaskBarState.GetSlot(2).PendingRelockAfterTaskCompletion, Is.False);
            Assert.That(runtime.TaskBarState.GetTaskBySlot(2), Is.Null);
        }

        [Test]
        public void HolmasGameplayRuntime_RevealCell_AutoClaimPendingRelockSlot_LocksAfterReward()
        {
            var catalog = CreateMultiCatTaskCatalog("cat-a", "cat-b");
            var clock = new FixedUtcClock { UtcNowMilliseconds = 1000 };
            var taskService = new HolmasTaskProgressService(catalog, new ScriptedRandomSource(0, 0, 0, 0), clock);
            var metaService = new HolmasMetaProgressionService(
                HolmasTestSupport.CreateMetaCatalog(),
                catalog,
                new HolmasDefaultMetaExperienceSource(),
                new HolmasDefaultMetaExperienceSource(),
                clock);
            var coordinator = new HolmasProgressionCoordinator(taskService, metaService);
            var runtime = new HolmasGameplayRuntime(taskService, metaService, coordinator, new NullLogger(), null);

            var unlock = runtime.UnlockAdSlot(2, 1, 2000);
            HolmasTaskRuntimeInstance task = runtime.TaskBarState.GetTaskBySlot(2);
            Assert.That(unlock.Success, Is.True);
            Assert.That(task, Is.Not.Null);

            taskService.RefreshExpiredAdSlots(runtime.TaskBarState, 3000);
            Assert.That(runtime.TaskBarState.GetSlot(2).PendingRelockAfterTaskCompletion, Is.True);
            runtime.StartLevel(
                HolmasTestSupport.CreateTerrain(1, 1),
                HolmasTestSupport.CreateRequest(
                    "map-pending-relock-auto-claim",
                    TerrainAssetPathUtility.BuildAssetPath("1"),
                    41,
                    1,
                    1,
                    new BoardSpawnEntry { CatId = task.Task.CatId, Weight = 1 }));

            BoardRevealResult reveal = runtime.RevealCell(runtime.CurrentLevelSnapshot.SpawnedCats[0].CellIndex, out _);

            Assert.That(reveal.IsValidAction, Is.True);
            Assert.That(runtime.CurrentGoldBalance, Is.EqualTo(task.Task.Reward));
            Assert.That(runtime.TaskBarState.GetSlot(2).IsUnlocked, Is.False);
            Assert.That(runtime.TaskBarState.GetSlot(2).PendingRelockAfterTaskCompletion, Is.False);
            Assert.That(runtime.TaskBarState.GetTaskBySlot(2), Is.Null);
            Assert.That(runtime.LastTaskRewardTip, Does.Contain("金币 +"));
        }

        [Test]
        public void HolmasGameplayRuntime_UnlockFourthAndFifthAdSlots_RefillsImmediately()
        {
            var catalog = CreateMultiCatTaskCatalog("cat-a", "cat-b", "cat-c", "cat-d", "cat-e");
            var clock = new FixedUtcClock { UtcNowMilliseconds = 1000 };
            var taskService = new HolmasTaskProgressService(catalog, new ScriptedRandomSource(), clock);
            var metaService = new HolmasMetaProgressionService(
                HolmasTestSupport.CreateMetaCatalog(),
                catalog,
                new HolmasDefaultMetaExperienceSource(),
                new HolmasDefaultMetaExperienceSource(),
                clock);
            var coordinator = new HolmasProgressionCoordinator(taskService, metaService);
            var runtime = new HolmasGameplayRuntime(taskService, metaService, coordinator, null, new NullLogger(), null, null, null, clock);

            runtime.RefillAvailableTasks(1);
            runtime.UnlockAdSlot(2);
            HolmasTaskSlotUnlockResult fourthSlot = runtime.UnlockAdSlot(3);
            HolmasTaskSlotUnlockResult fifthSlot = runtime.UnlockAdSlot(4);

            Assert.That(fourthSlot.Success, Is.True);
            Assert.That(fourthSlot.GeneratedTask, Is.Not.Null);
            Assert.That(fourthSlot.GeneratedTask.Success, Is.True, fourthSlot.GeneratedTask?.FailureReason);
            Assert.That(fifthSlot.Success, Is.True);
            Assert.That(fifthSlot.GeneratedTask, Is.Not.Null);
            Assert.That(fifthSlot.GeneratedTask.Success, Is.True, fifthSlot.GeneratedTask?.FailureReason);
            Assert.That(runtime.TaskBarState.GetTaskBySlot(3), Is.Not.Null);
            Assert.That(runtime.TaskBarState.GetTaskBySlot(4), Is.Not.Null);
            Assert.That(runtime.TaskBarState.Tasks, Has.Count.EqualTo(5));
        }

        [Test]
        public void HolmasGameplayRuntime_UnlockAdSlot_UsesCurrentPlayerLevelAdUnlockHours()
        {
            var catalog = CreateMultiCatTaskCatalog("cat-a", "cat-b", "cat-c");
            var clock = new FixedUtcClock { UtcNowMilliseconds = 1234 };
            var taskService = new HolmasTaskProgressService(catalog, new ScriptedRandomSource(), clock);
            var metaService = new HolmasMetaProgressionService(
                CreateGrowthMetaCatalog(),
                catalog,
                new HolmasDefaultMetaExperienceSource(),
                new HolmasDefaultMetaExperienceSource(),
                clock);
            var coordinator = new HolmasProgressionCoordinator(taskService, metaService);
            var runtime = new HolmasGameplayRuntime(taskService, metaService, coordinator, null, new NullLogger(), null, null, null, clock);
            runtime.MetaProgressionState.PlayerLevel = 2;

            HolmasTaskSlotUnlockResult unlock = runtime.UnlockAdSlot(2);

            Assert.That(unlock.Success, Is.True);
            Assert.That(unlock.UnlockExpireAt, Is.EqualTo(1234 + 12L * 60L * 60L * 1000L));
            Assert.That(runtime.TaskBarState.GetSlot(2).UnlockExpireAt, Is.EqualTo(unlock.UnlockExpireAt));
            Assert.That(runtime.TaskBarState.GetTaskBySlot(2), Is.Not.Null);
        }

        [Test]
        public void HolmasGameplayRuntime_IsCurrentLevelCompatibleWithTaskBar_AllowsUnlockedEmptySlotSession()
        {
            var catalog = CreateMultiCatTaskCatalog("cat-a", "cat-b");
            var clock = new FixedUtcClock { UtcNowMilliseconds = 1000 };
            var taskService = new HolmasTaskProgressService(catalog, new ScriptedRandomSource(0, 0, 0, 0, 0, 0), clock);
            var metaService = new HolmasMetaProgressionService(
                HolmasTestSupport.CreateMetaCatalog(),
                catalog,
                new HolmasDefaultMetaExperienceSource(),
                new HolmasDefaultMetaExperienceSource(),
                clock);
            var coordinator = new HolmasProgressionCoordinator(taskService, metaService);
            var runtime = new HolmasGameplayRuntime(taskService, metaService, coordinator, new NullLogger(), null);

            runtime.RefillAvailableTasks(1);
            runtime.StartLevel(
                HolmasTestSupport.CreateTerrain(1, 2),
                HolmasTestSupport.CreateRequest(
                    "map-compatible-check",
                    TerrainAssetPathUtility.BuildAssetPath("1"),
                    31,
                    2,
                    2,
                    new BoardSpawnEntry { CatId = "cat-a", Weight = 1 }));

            runtime.TaskBarState.ClearSlot(0, true);

            Assert.That(runtime.HasActiveUncompletedLevel, Is.True);
            Assert.That(runtime.IsCurrentLevelCompatibleWithTaskBar(), Is.True);
        }

        [Test]
        public void HolmasGameplayRuntime_IsCurrentLevelCompatibleWithTaskBar_AllowsMissingActiveCatCoverage()
        {
            var catalog = CreateMultiCatTaskCatalog("cat-a", "cat-b");
            var clock = new FixedUtcClock { UtcNowMilliseconds = 1000 };
            var taskService = new HolmasTaskProgressService(catalog, new ScriptedRandomSource(0, 0, 0, 0, 0, 0), clock);
            var metaService = new HolmasMetaProgressionService(
                HolmasTestSupport.CreateMetaCatalog(),
                catalog,
                new HolmasDefaultMetaExperienceSource(),
                new HolmasDefaultMetaExperienceSource(),
                clock);
            var coordinator = new HolmasProgressionCoordinator(taskService, metaService);
            var runtime = new HolmasGameplayRuntime(taskService, metaService, coordinator, new NullLogger(), null);

            runtime.RefillAvailableTasks(1);
            runtime.StartLevel(
                HolmasTestSupport.CreateTerrain(1, 2),
                HolmasTestSupport.CreateRequest(
                    "map-partial-active-cats",
                    TerrainAssetPathUtility.BuildAssetPath("1"),
                    37,
                    2,
                    2,
                    new BoardSpawnEntry { CatId = "cat-a", Weight = 1 }));

            Assert.That(runtime.TaskBarState.GetTaskBySlot(0), Is.Not.Null);
            Assert.That(runtime.TaskBarState.GetTaskBySlot(1), Is.Not.Null);
            Assert.That(runtime.TaskBarState.GetTaskBySlot(0).Task.CatId, Is.EqualTo("cat-a"));
            Assert.That(runtime.TaskBarState.GetTaskBySlot(1).Task.CatId, Is.EqualTo("cat-b"));
            Assert.That(runtime.IsCurrentLevelCompatibleWithTaskBar(), Is.True);
        }

        [Test]
        public void MainView_RenderLockedTaskSlot_ClearsLegacyProgressText()
        {
            var root = new GameObject("MainRoot", typeof(RectTransform), typeof(MainView));
            try
            {
                CreateTaskSlot(root.transform, "Task1");
                CreateTaskSlot(root.transform, "Task2");
                CreateTaskSlot(root.transform, "Task3");
                CreateTaskSlot(root.transform, "Task4", withLegacyProgressText: true);
                TextMeshProUGUI legacyTask5Text = CreateTaskSlot(root.transform, "Task5", withLegacyProgressText: true);

                MainView view = root.GetComponent<MainView>();
                view.Render(new MainVm
                {
                    TaskItems = new[]
                    {
                        new MainTaskItemVm { Title = "A", Progress = "1/1", Reward = "R1", ProgressNormalized = 1f },
                        new MainTaskItemVm { Title = "B", Progress = "0/2", Reward = "R2", ProgressNormalized = 0f },
                        new MainTaskItemVm { Title = "C", Progress = "未解锁", Reward = "广告位后续接入", ProgressNormalized = 0f, IsLocked = true, ButtonEnabled = false },
                        new MainTaskItemVm { Title = "D", Progress = "未解锁", Reward = "广告位后续接入", ProgressNormalized = 0f, IsLocked = true, ButtonEnabled = false },
                        new MainTaskItemVm { Title = "E", Progress = "未解锁", Reward = "广告位后续接入", ProgressNormalized = 0f, IsLocked = true, ButtonEnabled = false },
                    }
                });

                Assert.That(legacyTask5Text.text, Is.EqualTo(string.Empty));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MainPresenter_BuildTaskItems_IncludesFifthLockedSlot()
        {
            var catalog = CreateMultiCatTaskCatalog("cat-a", "cat-b");
            var clock = new FixedUtcClock { UtcNowMilliseconds = 1000 };
            var taskService = new HolmasTaskProgressService(catalog, new ScriptedRandomSource(0, 0, 0, 0), clock);
            var metaService = new HolmasMetaProgressionService(
                HolmasTestSupport.CreateMetaCatalog(),
                catalog,
                new HolmasDefaultMetaExperienceSource(),
                new HolmasDefaultMetaExperienceSource(),
                clock);
            var coordinator = new HolmasProgressionCoordinator(taskService, metaService);
            var runtime = new HolmasGameplayRuntime(taskService, metaService, coordinator, new NullLogger(), null);
            runtime.RefillAvailableTasks(1);

            var serviceContainer = new FakeServiceContainer();
            serviceContainer.RegisterSingleton<IHolmasTaskCatalog>(catalog);
            var context = new HolmasApplicationContext(
                serviceContainer,
                new NullLogger(),
                new FakeTickManager(),
                new FakeEventBus(),
                null,
                runtime);

            MainVm viewModel = new MainPresenter(context).Build();

            Assert.That(viewModel.TaskItems, Has.Length.EqualTo(5));
            Assert.That(viewModel.TaskItems[4].SlotIndex, Is.EqualTo(4));
            Assert.That(viewModel.TaskItems[4].IsLocked, Is.True);
            Assert.That(viewModel.TaskItems[4].Progress, Is.EqualTo("未解锁"));
            Assert.That(viewModel.TaskItems[4].Reward, Does.Contain("任务或广告解锁"));
            Assert.That(viewModel.TaskItems[4].ButtonEnabled, Is.False);
        }

        [Test]
        public void MainView_RenderWithoutBoard_HidesStaticPlaceholderTiles()
        {
            var root = new GameObject("MainRoot", typeof(RectTransform), typeof(MainView));
            try
            {
                var minesGroup = new GameObject("MinesGroup", typeof(RectTransform), typeof(GridLayoutGroup));
                minesGroup.transform.SetParent(root.transform, false);
                var placeholderTile = new GameObject("PlaceholderTile", typeof(RectTransform), typeof(Image));
                placeholderTile.transform.SetParent(minesGroup.transform, false);
                var boardContainer = new GameObject("BoardContainer", typeof(RectTransform));
                boardContainer.transform.SetParent(minesGroup.transform, false);

                MainView view = root.GetComponent<MainView>();
                view.Bind(new MainBindings
                {
                    MinesGroup = minesGroup.GetComponent<RectTransform>(),
                    BoardContainer = boardContainer.GetComponent<RectTransform>(),
                });

                view.Render(new MainVm { BoardVisible = false });

                Assert.That(placeholderTile.activeSelf, Is.False);
                Assert.That(boardContainer.activeSelf, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MainView_RenderBoard_SwitchesBetweenNormalAndTutorialContainers()
        {
            var root = new GameObject("MainRoot", typeof(RectTransform), typeof(MainView));
            try
            {
                var minesGroup = new GameObject("MinesGroup", typeof(RectTransform), typeof(GridLayoutGroup));
                minesGroup.transform.SetParent(root.transform, false);
                var boardContainer = new GameObject("BoardContainer", typeof(RectTransform));
                boardContainer.transform.SetParent(minesGroup.transform, false);
                var tutorialBoardContainer = new GameObject("TutorialBoardContainer", typeof(RectTransform));
                tutorialBoardContainer.transform.SetParent(minesGroup.transform, false);

                MainView view = root.GetComponent<MainView>();
                view.Bind(new MainBindings
                {
                    MinesGroup = minesGroup.GetComponent<RectTransform>(),
                    BoardContainer = boardContainer.GetComponent<RectTransform>(),
                    TutorialBoardContainer = tutorialBoardContainer.GetComponent<RectTransform>(),
                });

                var cells = new[]
                {
                    new BoardCellState(0, true, false, false, false, string.Empty, 0, new Color32(32, 48, 64, 255)),
                };

                view.Render(new MainVm { BoardVisible = true, UseTutorialBoardLayer = true, Rows = 1, Cols = 1, Cells = cells });
                Assert.That(boardContainer.activeSelf, Is.False);
                Assert.That(tutorialBoardContainer.activeSelf, Is.True);

                view.Render(new MainVm { BoardVisible = true, UseTutorialBoardLayer = false, Rows = 1, Cols = 1, Cells = cells });
                Assert.That(boardContainer.activeSelf, Is.True);
                Assert.That(tutorialBoardContainer.activeSelf, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MainView_HideTutorialBoardLayer_DeactivatesTutorialContainerBeforeFormalBoardStarts()
        {
            var root = new GameObject("MainRoot", typeof(RectTransform), typeof(MainView));
            try
            {
                var minesGroup = new GameObject("MinesGroup", typeof(RectTransform), typeof(GridLayoutGroup));
                minesGroup.transform.SetParent(root.transform, false);
                var boardContainer = new GameObject("BoardContainer", typeof(RectTransform));
                boardContainer.transform.SetParent(minesGroup.transform, false);
                var tutorialBoardContainer = new GameObject("TutorialBoardContainer", typeof(RectTransform));
                tutorialBoardContainer.transform.SetParent(minesGroup.transform, false);

                MainView view = root.GetComponent<MainView>();
                view.Bind(new MainBindings
                {
                    MinesGroup = minesGroup.GetComponent<RectTransform>(),
                    BoardContainer = boardContainer.GetComponent<RectTransform>(),
                    TutorialBoardContainer = tutorialBoardContainer.GetComponent<RectTransform>(),
                });

                var cells = new[]
                {
                    new BoardCellState(0, true, false, false, false, string.Empty, 0, new Color32(32, 48, 64, 255)),
                };

                view.Render(new MainVm { BoardVisible = true, UseTutorialBoardLayer = true, Rows = 1, Cols = 1, Cells = cells });

                view.HideTutorialBoardLayer();

                Assert.That(tutorialBoardContainer.activeSelf, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MainView_EnsureBindingSurface_CreatesMinesGroupAtBindingPath()
        {
            var root = new GameObject("MainRoot", typeof(RectTransform), typeof(MainView));
            try
            {
                MainView view = root.GetComponent<MainView>();

                view.EnsureBindingSurface();

                Assert.That(root.transform.Find("MinesGroup"), Is.Not.Null);
                Assert.That(root.transform.Find("RuntimeOverlay/MinesGroup"), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MainPageController_RepairIncompatibleLevelSession_DoesNotClearActiveBoard()
        {
            var catalog = CreateMultiCatTaskCatalog("cat-a", "cat-b");
            var clock = new FixedUtcClock { UtcNowMilliseconds = 1000 };
            var taskService = new HolmasTaskProgressService(catalog, new ScriptedRandomSource(0, 0, 0, 0, 0, 0), clock);
            var metaService = new HolmasMetaProgressionService(
                HolmasTestSupport.CreateMetaCatalog(),
                catalog,
                new HolmasDefaultMetaExperienceSource(),
                new HolmasDefaultMetaExperienceSource(),
                clock);
            var coordinator = new HolmasProgressionCoordinator(taskService, metaService);
            var runtime = new HolmasGameplayRuntime(taskService, metaService, coordinator, new NullLogger(), null);

            runtime.RefillAvailableTasks(1);
            runtime.StartLevel(
                HolmasTestSupport.CreateTerrain(1, 2),
                HolmasTestSupport.CreateRequest(
                    "map-bad-archive",
                    TerrainAssetPathUtility.BuildAssetPath("1"),
                    17,
                    2,
                    2,
                    new BoardSpawnEntry { CatId = "cat-a", Weight = 1 }));
            runtime.TaskBarState.ClearSlot(0, true);

            MainPageController controller = new MainPageController();
            typeof(MainPageController)
                .GetField("_runtime", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(controller, runtime);

            string status = (string)typeof(MainPageController)
                .GetMethod("RepairIncompatibleLevelSession", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(controller, null);

            Assert.That(status, Is.Null);
            Assert.That(runtime.HasActiveUncompletedLevel, Is.True);
            Assert.That(runtime.TaskBarState.GetUnlockedEmptySlotCount(), Is.EqualTo(1));
        }

        [Test]
        public void MainPresenter_BoardAndTaskUseSameCatVisualMapping()
        {
            var catalog = CreateIconTaskCatalog("Assets/HotUpdateContent/Res/Icons/cat_01.png");
            var clock = new FixedUtcClock { UtcNowMilliseconds = 1000 };
            var taskService = new HolmasTaskProgressService(catalog, new ScriptedRandomSource(0, 0, 1), clock);
            var metaService = new HolmasMetaProgressionService(
                HolmasTestSupport.CreateMetaCatalog(),
                catalog,
                new HolmasDefaultMetaExperienceSource(),
                new HolmasDefaultMetaExperienceSource(),
                clock);
            var coordinator = new HolmasProgressionCoordinator(taskService, metaService);
            var runtime = new HolmasGameplayRuntime(taskService, metaService, coordinator, new NullLogger(), null);
            runtime.RefillAvailableTasks(1);
            runtime.TaskBarState.GetTaskBySlot(0).Task.TargetCount = 2;

            var request = HolmasTestSupport.CreateRequest(
                "map-visual",
                TerrainAssetPathUtility.BuildAssetPath("1"),
                7,
                1,
                1,
                new BoardSpawnEntry { CatId = "cat-a", Weight = 1 });
            runtime.StartLevel(HolmasTestSupport.CreateTerrain(1, 1), request);

            var serviceContainer = new FakeServiceContainer();
            serviceContainer.RegisterSingleton<IHolmasTaskCatalog>(catalog);
            var context = new HolmasApplicationContext(
                serviceContainer,
                new NullLogger(),
                new FakeTickManager(),
                new FakeEventBus(),
                null,
                runtime);

            MainVm viewModel = new MainPresenter(context).Build();

            Assert.That(viewModel.Cells[0].CatId, Is.Empty);
            Assert.That(viewModel.TaskItems[0].CatId, Is.EqualTo("cat-a"));
            Assert.That(viewModel.CatVisuals.ContainsKey("cat-a"), Is.True);
            Assert.That(viewModel.CatVisuals["cat-a"].IconPath, Is.EqualTo(viewModel.TaskItems[0].CatIconPath));
            Assert.That(viewModel.TaskItems[0].CatIconPath, Is.EqualTo("Assets/HotUpdateContent/Res/Icons/cat_01.png"));

            runtime.RevealCell(0, out _);
            MainVm revealedViewModel = new MainPresenter(context).Build();

            Assert.That(revealedViewModel.Cells[0].CatId, Is.EqualTo("cat-a"));
            Assert.That(revealedViewModel.TaskItems[0].CatId, Is.EqualTo("cat-a"));
            Assert.That(revealedViewModel.CatVisuals.ContainsKey("cat-a"), Is.True);
            Assert.That(revealedViewModel.CatVisuals["cat-a"].IconPath, Is.EqualTo(revealedViewModel.TaskItems[0].CatIconPath));
        }

        [Test]
        public void MainAndAgencyPresenters_UseAutoClaimTaskCopy()
        {
            var catalog = CreateMultiCatTaskCatalog("cat-a", "cat-b");
            var clock = new FixedUtcClock { UtcNowMilliseconds = 1000 };
            var taskService = new HolmasTaskProgressService(catalog, new ScriptedRandomSource(0, 0, 0, 0), clock);
            var metaService = new HolmasMetaProgressionService(
                HolmasTestSupport.CreateMetaCatalog(),
                catalog,
                new HolmasDefaultMetaExperienceSource(),
                new HolmasDefaultMetaExperienceSource(),
                clock);
            var coordinator = new HolmasProgressionCoordinator(taskService, metaService);
            var runtime = new HolmasGameplayRuntime(taskService, metaService, coordinator, new NullLogger(), null);
            runtime.RefillAvailableTasks(1);
            runtime.TaskBarState.MarkPendingRelockAfterTaskCompletion(1);

            var serviceContainer = new FakeServiceContainer();
            serviceContainer.RegisterSingleton<IHolmasTaskCatalog>(catalog);
            var context = new HolmasApplicationContext(
                serviceContainer,
                new NullLogger(),
                new FakeTickManager(),
                new FakeEventBus(),
                null,
                runtime);

            MainVm mainVm = new MainPresenter(context).Build();
            AgencyMainVm agencyVm = new AgencyMainPresenter(context).Build();

            Assert.That(mainVm.Summary, Does.Not.Contain("可领奖"));
            Assert.That(mainVm.TaskItems[0].Reward, Does.Contain("自动领奖"));
            Assert.That(mainVm.TaskItems[1].Reward, Does.Contain("自动领奖并锁定"));
            Assert.That(agencyVm.TaskSummary, Does.Not.Contain("可领奖"));
            Assert.That(agencyVm.TaskItems[0].ClaimButtonLabel, Is.EqualTo("查看状态"));
            Assert.That(agencyVm.TaskItems[1].Reward, Does.Contain("自动领奖并锁定"));
            Assert.That(agencyVm.TaskItems[4].IsLocked, Is.True);
            Assert.That(agencyVm.TaskItems[4].ClaimButtonEnabled, Is.False);
            Assert.That(agencyVm.TaskItems[4].ClaimButtonLabel, Is.EqualTo("待解锁"));
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
            MainVm initialViewModel = new MainPresenter(context).Build();

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

            MainVm completedViewModel = new MainPresenter(context).Build();
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
        public void HolmasCatSpriteLoader_MissingIconPath_UsesFallbackWithoutCrash()
        {
            var iconObject = new GameObject("icon", typeof(RectTransform), typeof(Image));
            try
            {
                Image image = iconObject.GetComponent<Image>();
                var loader = new HolmasCatSpriteLoader(null);

                loader.Bind(image, HolmasCatVisualVm.CreateFallback("cat-a"));

                Assert.That(image.enabled, Is.True);
                Assert.That(image.sprite, Is.Not.Null);
                Assert.That(image.color.a, Is.GreaterThan(0f));
            }
            finally
            {
                Object.DestroyImmediate(iconObject);
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

            public IEventSubscription SubscribeScoped<T>(
                System.Action<T> handler,
                int priority = 0,
                System.Predicate<T> condition = null) where T : class
            {
                return new NullSubscription();
            }
        }

        private sealed class NullSubscription : IEventSubscription
        {
            public void Dispose()
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

        private static HolmasTaskCatalog CreateIconTaskCatalog(string iconPath)
        {
            return new HolmasTaskCatalog(
                new[]
                {
                    new HolmasCatDefinition
                    {
                        CatId = "cat-a",
                        CatName = "测试猫",
                        IconPath = iconPath,
                        Price = 10,
                        Weight = 1,
                    },
                },
                new[]
                {
                    new HolmasTaskTemplateDefinition
                    {
                        TaskTypeId = "task-icon",
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
                        TaskTypeIds = new[] { "task-icon" },
                        TaskTypeWeights = new[] { 1 },
                        MapIds = new[] { "map-visual" },
                        MapWeights = new[] { 1 },
                    }
                });
        }

        private static HolmasTaskCatalog CreateMultiCatTaskCatalog(params string[] catIds)
        {
            HolmasCatDefinition[] cats = catIds
                .Select((catId, index) => new HolmasCatDefinition
                {
                    CatId = catId,
                    CatName = $"Cat {index + 1}",
                    Price = 10 + index,
                    Weight = 1,
                })
                .ToArray();

            return new HolmasTaskCatalog(
                cats,
                new[]
                {
                    new HolmasTaskTemplateDefinition
                    {
                        TaskTypeId = "task-multi",
                        CatIdList = catIds,
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
                        TaskTypeIds = new[] { "task-multi" },
                        TaskTypeWeights = new[] { 1 },
                        MapIds = new[] { "map-active-claim" },
                        MapWeights = new[] { 1 },
                    },
                    new HolmasPlayerLevelDefinition
                    {
                        PlayerLevel = 2,
                        UpgradeExp = 0,
                        TaskTypeIds = new[] { "task-multi" },
                        TaskTypeWeights = new[] { 1 },
                        MapIds = new[] { "map-active-claim" },
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
                    StageImage = "Textures/buildings/building01.png",
                    PromotionId = "lobby",
                    PromotionLevelCap = 1,
                    PromotionUpgradeCosts = new[] { 10 },
                },
                new HolmasAgencyBuildingDefinition
                {
                    AgencyStageId = 1,
                    StageName = "stage-1",
                    StageImage = "Textures/buildings/building01.png",
                    PromotionId = "desk",
                    PromotionLevelCap = 1,
                    PromotionUpgradeCosts = new[] { 20 },
                },
                new HolmasAgencyBuildingDefinition
                {
                    AgencyStageId = 2,
                    StageName = "stage-2",
                    StageImage = "Textures/buildings/building02.png",
                    PromotionId = "archive",
                    PromotionLevelCap = 2,
                    PromotionUpgradeCosts = new[] { 30, 40 },
                },
            });
        }

        private static HolmasAgencyCatalog CreateFifteenCapPromotionCatalog()
        {
            return new HolmasAgencyCatalog(new[]
            {
                new HolmasAgencyBuildingDefinition
                {
                    AgencyStageId = 1,
                    StageName = "stage-15",
                    StageImage = "Textures/buildings/building01.png",
                    PromotionId = "district",
                    PromotionLevelCap = 15,
                    PromotionUpgradeCosts = Enumerable.Repeat(10, 15).ToArray(),
                },
            });
        }

        private static HolmasApplicationContext CreatePromotionMapTestContext(out HolmasAgencyCatalog agencyCatalog)
        {
            agencyCatalog = CreatePromotionCatalog();
            return CreatePromotionMapTestContext(agencyCatalog);
        }

        private static HolmasApplicationContext CreatePromotionMapTestContext(HolmasAgencyCatalog agencyCatalog)
        {
            var taskCatalog = CreateDifficultyFlowTaskCatalog();
            var metaCatalog = CreateGrowthMetaCatalog();
            var metaService = new HolmasMetaProgressionService(
                metaCatalog,
                taskCatalog,
                new HolmasDefaultMetaExperienceSource(metaCatalog),
                new HolmasDefaultMetaExperienceSource(metaCatalog),
                new FixedUtcClock { UtcNowMilliseconds = 777_000 });
            var agencyService = new HolmasAgencyProgressionService(agencyCatalog, metaService);
            var taskService = new HolmasTaskProgressService(taskCatalog, new ScriptedRandomSource(0), new FixedUtcClock { UtcNowMilliseconds = 1000 });
            var coordinator = new HolmasProgressionCoordinator(taskService, metaService);
            var runtime = new HolmasGameplayRuntime(taskService, metaService, coordinator, agencyService, new NullLogger(), null);
            var serviceContainer = new FakeServiceContainer();
            serviceContainer.RegisterSingleton<IHolmasTaskCatalog>(taskCatalog);
            serviceContainer.RegisterSingleton<IHolmasAgencyCatalog>(agencyCatalog);
            return new HolmasApplicationContext(
                serviceContainer,
                new NullLogger(),
                new FakeTickManager(),
                new FakeEventBus(),
                null,
                runtime);
        }

        private static TextMeshProUGUI CreateTaskSlot(Transform parent, string name, bool withLegacyProgressText = false)
        {
            var slot = new GameObject(name, typeof(RectTransform));
            slot.transform.SetParent(parent, false);

            var count = new GameObject("Count", typeof(RectTransform), typeof(TextMeshProUGUI));
            count.transform.SetParent(slot.transform, false);

            var slider = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            slider.transform.SetParent(slot.transform, false);

            TextMeshProUGUI legacyText = null;
            if (withLegacyProgressText)
            {
                var legacy = new GameObject("LegacyProgress", typeof(RectTransform), typeof(TextMeshProUGUI));
                legacy.transform.SetParent(slot.transform, false);
                legacyText = legacy.GetComponent<TextMeshProUGUI>();
                legacyText.text = "10/10";
            }

            return legacyText;
        }

        private static HolmasGameplayRuntime CreateEnergyRuntime(
            FixedUtcClock clock,
            HolmasMetaProgressionState state = null,
            IEventBus eventBus = null)
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
                clock,
                eventBus);
        }
    }
}
