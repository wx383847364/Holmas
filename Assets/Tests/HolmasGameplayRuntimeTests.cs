using System.Collections;
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
using App.HotUpdate.Holmas.Tutorial;
using App.HotUpdate.Holmas.UI.Core;
using App.HotUpdate.Holmas.UI.Screens.AgencyMain;
using App.HotUpdate.Holmas.UI.Screens.Battle;
using App.HotUpdate.Holmas.UI.Screens.FindCat;
using App.HotUpdate.Holmas.UI.Screens.Main;
using App.Shared.Contracts;
using App.Shared.Holmas.RuntimeData;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
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
        public void HolmasGameplayRuntime_AddGold_IncreasesBalanceWithoutTaskRewardEvent()
        {
            var clock = new FixedUtcClock { UtcNowMilliseconds = 1000 };
            var eventBus = new EventBus();
            var state = new HolmasMetaProgressionState { GoldBalance = 25 };
            HolmasGameplayRuntime runtime = CreateEnergyRuntime(clock, state, eventBus);
            var order = new List<string>();
            int leaderboardRewardEvents = 0;

            runtime.StateChanged += reason => order.Add("legacy:" + reason);
            eventBus.SubscribeScoped<HolmasGameplayStateChangedEvent>(eventData => order.Add("domain:" + eventData.Reason));
            eventBus.SubscribeScoped<HolmasLeaderboardTaskRewardClaimedEvent>(_ => leaderboardRewardEvents++);

            runtime.AddGold();

            Assert.That(runtime.CurrentGoldBalance, Is.EqualTo(1_000_025));
            CollectionAssert.AreEqual(
                new[]
                {
                    "legacy:DebugGoldChanged",
                    "domain:DebugGoldChanged",
                },
                order);
            Assert.That(leaderboardRewardEvents, Is.EqualTo(0), "GM 加金币不应计入每日收入榜任务奖励事件。");
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
            HolmasApplicationContext context = CreatePromotionMapTestContext(CreateFiveStagePromotionCatalog());
            context.GameplayRuntime.MetaProgressionState.GoldBalance = 25;

            BattleVm viewModel = new BattlePresenter(context).Build(selectedStageId: 1);

            Assert.That(viewModel.SelectedStageId, Is.EqualTo(1));
            Assert.That(viewModel.Stages, Has.Length.EqualTo(BattlePresenter.VisibleStageCount));
            Assert.That(viewModel.Stages.Count(item => item != null && item.Visible), Is.EqualTo(BattlePresenter.VisibleStageCount));
            Assert.That(viewModel.Stages[0].Visible, Is.True);
            Assert.That(viewModel.Stages[0].Unlocked, Is.True);
            Assert.That(viewModel.Stages[0].Current, Is.True);
            Assert.That(viewModel.Stages[0].Selected, Is.True);
            Assert.That(viewModel.Stages[0].StageImage, Is.EqualTo("Assets/HotUpdateContent/Res/Textures/buildings/building01.png"));
            Assert.That(viewModel.Stages[1].Visible, Is.True);
            Assert.That(viewModel.Stages[1].Unlocked, Is.False);

            Assert.That(viewModel.PromotionSlots, Has.Length.EqualTo(3));
            Assert.That(viewModel.PromotionSlots.Select(item => item.StarCap).ToArray(), Is.EqualTo(new[] { 3, 5, 2 }));
            Assert.That(viewModel.PromotionSlots.Select(item => item.StarCount).ToArray(), Is.EqualTo(new[] { 0, 0, 0 }));
            Assert.That(viewModel.PromotionSlots[0].AgencyStageId, Is.EqualTo(viewModel.Stages[0].AgencyStageId));
            Assert.That(viewModel.PromotionSlots[0].StageImage, Is.EqualTo(viewModel.Stages[0].StageImage));
            Assert.That(viewModel.BuildButtonEnabled, Is.True);
            Assert.That(viewModel.BuildButtonLabel, Is.EqualTo("stage-1\n选择宣传项升级"));
            Assert.That(viewModel.StageBars[0].Visible, Is.True);
            Assert.That(viewModel.StageBars[0].Progress, Is.EqualTo(0f));
        }

        [Test]
        public void BattlePresenter_PromotionSlotStarsFollowPromotionLevelCaps()
        {
            HolmasApplicationContext context = CreatePromotionMapTestContext(CreateFiveStagePromotionCatalog());
            HolmasAgencyPromotionStateKey.SetLevel(context.GameplayRuntime.MetaProgressionState, 1, "leaflet", 3);
            HolmasAgencyPromotionStateKey.SetLevel(context.GameplayRuntime.MetaProgressionState, 1, "radio", 2);
            HolmasAgencyPromotionStateKey.SetLevel(context.GameplayRuntime.MetaProgressionState, 1, "tv", 1);

            BattleVm viewModel = new BattlePresenter(context).Build(selectedStageId: 1);

            Assert.That(viewModel.PromotionSlots, Has.Length.EqualTo(3));
            Assert.That(viewModel.PromotionSlots.Select(item => item.StarCap).ToArray(), Is.EqualTo(new[] { 3, 5, 2 }));
            Assert.That(viewModel.PromotionSlots.Select(item => item.StarCount).ToArray(), Is.EqualTo(new[] { 3, 2, 1 }));
            Assert.That(viewModel.PromotionSlots[1].ProgressLabel, Is.EqualTo("2/5"));
            Assert.That(viewModel.PromotionSlots[1].CanBuild, Is.True);
        }

        [Test]
        public void BattlePresenter_PromotionSlotStarCountsFollowCurrentLevelUntilCap()
        {
            HolmasApplicationContext context = CreatePromotionMapTestContext(CreateFiveStagePromotionCatalog());
            BattlePresenter presenter = new BattlePresenter(context);

            for (int level = 0; level <= 5; level++)
            {
                HolmasAgencyPromotionStateKey.SetLevel(context.GameplayRuntime.MetaProgressionState, 1, "radio", level);

                BattleVm viewModel = presenter.Build(selectedStageId: 1);

                Assert.That(viewModel.PromotionSlots[1].StarCap, Is.EqualTo(5));
                Assert.That(viewModel.PromotionSlots[1].StarCount, Is.EqualTo(level));
                Assert.That(viewModel.PromotionSlots[1].ProgressLabel, Is.EqualTo($"{level}/5"));
            }
        }

        [Test]
        public void BattlePresenter_KeepsAllPromotionSlotsWhenCapsExceedStaticFive()
        {
            HolmasApplicationContext context = CreatePromotionMapTestContext(CreateSixPromotionStageCatalog());

            BattleVm viewModel = new BattlePresenter(context).Build(selectedStageId: 1);

            Assert.That(viewModel.PromotionSlots, Has.Length.EqualTo(6));
            Assert.That(viewModel.PromotionSlots.Select(item => item.StarCap).ToArray(), Is.EqualTo(new[] { 1, 2, 3, 4, 5, 6 }));
            Assert.That(viewModel.PromotionSlots.Select(item => item.PromotionId).ToArray(), Is.EqualTo(new[] { "promo-1", "promo-2", "promo-3", "promo-4", "promo-5", "promo-6" }));
        }

        [Test]
        public void BattlePresenter_ClickingPromotionSlotTargetsThatPromotion()
        {
            HolmasApplicationContext context = CreatePromotionMapTestContext(CreateFiveStagePromotionCatalog());
            context.GameplayRuntime.MetaProgressionState.GoldBalance = 500;
            BattlePresenter presenter = new BattlePresenter(context);

            string promotionId = presenter.GetPromotionIdForSlot(promotionSlotIndex: 1);
            HolmasAgencyUpgradeResult result = context.TryUpgradePromotion(promotionId);

            Assert.That(promotionId, Is.EqualTo("radio"));
            Assert.That(result.Success, Is.True);
            Assert.That(HolmasAgencyPromotionStateKey.GetLevel(context.GameplayRuntime.MetaProgressionState, 1, "leaflet"), Is.EqualTo(0));
            Assert.That(HolmasAgencyPromotionStateKey.GetLevel(context.GameplayRuntime.MetaProgressionState, 1, "radio"), Is.EqualTo(1));
            Assert.That(new BattlePresenter(context).Build(selectedStageId: 1).SelectedStageId, Is.EqualTo(1));
        }

        [Test]
        public void BattlePresenter_StageCompletionUnlocksNextStageButKeepsSelectedStage()
        {
            HolmasApplicationContext context = CreatePromotionMapTestContext(CreateFiveStagePromotionCatalog());
            context.GameplayRuntime.MetaProgressionState.GoldBalance = 500;
            HolmasAgencyPromotionStateKey.SetLevel(context.GameplayRuntime.MetaProgressionState, 1, "leaflet", 3);
            HolmasAgencyPromotionStateKey.SetLevel(context.GameplayRuntime.MetaProgressionState, 1, "radio", 5);
            HolmasAgencyPromotionStateKey.SetLevel(context.GameplayRuntime.MetaProgressionState, 1, "tv", 1);

            HolmasAgencyUpgradeResult result = context.TryUpgradePromotion("tv");
            BattleVm viewModel = new BattlePresenter(context).Build(selectedStageId: 1);

            Assert.That(result.Success, Is.True);
            Assert.That(result.StageAdvanced, Is.True);
            Assert.That(context.CurrentAgencyStageId, Is.EqualTo(2));
            Assert.That(viewModel.SelectedStageId, Is.EqualTo(1));
            Assert.That(viewModel.Stages[0].Selected, Is.True);
            Assert.That(viewModel.Stages[1].Unlocked, Is.True);
            Assert.That(viewModel.PromotionSlots.Select(item => item.AgencyStageId).ToArray(), Is.EqualTo(new[] { 2, 2, 2 }));
            Assert.That(viewModel.PromotionSlots.Select(item => item.StarCap).ToArray(), Is.EqualTo(new[] { 4, 4, 4 }));
            Assert.That(viewModel.PromotionSlots.Select(item => item.StarCount).ToArray(), Is.EqualTo(new[] { 0, 0, 0 }));
            Assert.That(viewModel.PromotionSlots.All(item => item.CanBuild), Is.True);
        }

        [Test]
        public void BattlePresenter_ViewingStageDoesNotRefreshLatestPromotionSlots()
        {
            HolmasApplicationContext context = CreatePromotionMapTestContext(CreateFiveStagePromotionCatalog());

            BattleVm lockedStage = new BattlePresenter(context).Build(selectedStageId: 2);
            Assert.That(lockedStage.SelectedStageId, Is.EqualTo(2));
            Assert.That(lockedStage.Stages[1].Selected, Is.True);
            Assert.That(lockedStage.BuildButtonEnabled, Is.True);
            Assert.That(lockedStage.BuildButtonLabel, Is.EqualTo("stage-1\n选择宣传项升级"));
            Assert.That(lockedStage.PromotionSlots, Has.Length.EqualTo(3));
            Assert.That(lockedStage.PromotionSlots.Select(item => item.AgencyStageId).ToArray(), Is.EqualTo(new[] { 1, 1, 1 }));
            Assert.That(lockedStage.PromotionSlots.Select(item => item.StarCap).ToArray(), Is.EqualTo(new[] { 3, 5, 2 }));
            Assert.That(lockedStage.PromotionSlots.All(item => item.Unlocked), Is.True);
            Assert.That(lockedStage.PromotionSlots.Any(item => item.CanBuild), Is.True);

            context.GameplayRuntime.MetaProgressionState.AgencyStageId = 2;
            HolmasAgencyPromotionStateKey.SetLevel(context.GameplayRuntime.MetaProgressionState, 1, "leaflet", 3);
            HolmasAgencyPromotionStateKey.SetLevel(context.GameplayRuntime.MetaProgressionState, 1, "radio", 5);
            HolmasAgencyPromotionStateKey.SetLevel(context.GameplayRuntime.MetaProgressionState, 1, "tv", 2);
            BattleVm historyStage = new BattlePresenter(context).Build(selectedStageId: 1);
            Assert.That(historyStage.BuildButtonEnabled, Is.True);
            Assert.That(historyStage.BuildButtonLabel, Is.EqualTo("stage-2\n选择宣传项升级"));
            Assert.That(historyStage.SelectedStageId, Is.EqualTo(1));
            Assert.That(historyStage.Stages[0].Completed, Is.True);
            Assert.That(historyStage.Stages[0].Selected, Is.True);
            Assert.That(historyStage.Stages[0].ProgressLabel, Is.EqualTo("10/10"));
            Assert.That(historyStage.PromotionSlots.Select(item => item.AgencyStageId).ToArray(), Is.EqualTo(new[] { 2, 2, 2 }));
            Assert.That(historyStage.PromotionSlots.Select(item => item.StarCap).ToArray(), Is.EqualTo(new[] { 4, 4, 4 }));
            Assert.That(historyStage.PromotionSlots.Select(item => item.StarCount).ToArray(), Is.EqualTo(new[] { 0, 0, 0 }));
            Assert.That(historyStage.PromotionSlots.All(item => item.CanBuild), Is.True);
        }

        [Test]
        public void BattlePresenter_StageBarsFollowConnectionProgress()
        {
            HolmasApplicationContext context = CreatePromotionMapTestContext(CreateFiveStagePromotionCatalog());
            context.GameplayRuntime.MetaProgressionState.AgencyStageId = 3;
            HolmasAgencyPromotionStateKey.SetLevel(context.GameplayRuntime.MetaProgressionState, 3, "leaflet", 2);
            HolmasAgencyPromotionStateKey.SetLevel(context.GameplayRuntime.MetaProgressionState, 3, "radio", 3);

            BattleVm viewModel = new BattlePresenter(context).Build(selectedStageId: 3);

            Assert.That(viewModel.StageBars, Has.Length.EqualTo(BattlePresenter.VisibleStageBarCount));
            Assert.That(viewModel.StageBars.Select(item => item.Visible).ToArray(), Is.EqualTo(new[] { true, true, true, true }));
            Assert.That(viewModel.StageBars[0].Progress, Is.EqualTo(1f));
            Assert.That(viewModel.StageBars[1].Progress, Is.EqualTo(1f));
            Assert.That(viewModel.StageBars[2].Progress, Is.EqualTo(5f / 15f).Within(0.0001f));
            Assert.That(viewModel.StageBars[3].Progress, Is.EqualTo(0f));
        }

        [Test]
        public void BattlePresenter_KeepsFiveStagePageSlotsFixedWithinMapPage()
        {
            HolmasApplicationContext context = CreatePromotionMapTestContext(CreateTenStagePromotionCatalog());
            context.GameplayRuntime.MetaProgressionState.AgencyStageId = 4;

            BattleVm viewModel = new BattlePresenter(context).Build(selectedStageId: 4);

            Assert.That(viewModel.Stages.Select(item => item.AgencyStageId).ToArray(), Is.EqualTo(new[] { 1, 2, 3, 4, 5 }));
            Assert.That(viewModel.Stages[3].Current, Is.True, "Stage4 应保持在当前地图第 4 个静态 slot，而不是挪到 Stage3 位置。");
            Assert.That(viewModel.Stages[3].Selected, Is.True);
            Assert.That(viewModel.Stages[2].AgencyStageId, Is.EqualTo(3));
        }

        [Test]
        public void BattlePresenter_SwitchesMapPageAfterCurrentPageCompleted()
        {
            HolmasApplicationContext context = CreatePromotionMapTestContext(CreateTenStagePromotionCatalog());
            context.GameplayRuntime.MetaProgressionState.AgencyStageId = 6;

            BattleVm viewModel = new BattlePresenter(context).Build(selectedStageId: 5);

            Assert.That(viewModel.SelectedStageId, Is.EqualTo(6), "当前 5 城市页全部完成后，地图应刷新到最新推进城市所在页。");
            Assert.That(viewModel.Stages.Select(item => item.AgencyStageId).ToArray(), Is.EqualTo(new[] { 6, 7, 8, 9, 10 }));
            Assert.That(viewModel.Stages[0].Selected, Is.True);
            Assert.That(viewModel.Stages[0].Current, Is.True);
            Assert.That(viewModel.PromotionSlots.Select(item => item.AgencyStageId).ToArray(), Is.EqualTo(new[] { 6, 6, 6 }));
            Assert.That(viewModel.PromotionSlots.Any(item => item.CanBuild), Is.True);
            Assert.That(viewModel.StageBars.Select(item => item.Visible).ToArray(), Is.EqualTo(new[] { true, true, true, true }));
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
        public void MainView_RenderLockedTaskSlot_UsesStaticTaskSlotBindings()
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
                view.Bind(CreateMainTaskBindings(root.transform));
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

                Assert.That(root.transform.Find("Task5/Count")?.GetComponent<Text>()?.text, Is.EqualTo("未解锁"));
                Assert.That(legacyTask5Text.text, Is.EqualTo("10/10"), "未纳入静态绑定的旧节点不应再由运行时递归清理。");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MainView_RenderTaskSlots_PreservesPrefabTaskSlotImageColors()
        {
            var root = new GameObject("MainRoot", typeof(RectTransform), typeof(MainView));
            try
            {
                Image task1Image = CreateTaskSlotImage(root.transform, "Task1", new Color(0.2f, 0.4f, 0.8f, 0.6f));
                Image task2Image = CreateTaskSlotImage(root.transform, "Task2", new Color(0.9f, 0.7f, 0.3f, 0.85f));
                Image task1RewardIcon = CreateTaskRewardIcon(task1Image.transform, new Color(0.1f, 0.8f, 0.3f, 0.5f));
                Image task2RewardIcon = CreateTaskRewardIcon(task2Image.transform, new Color(0.7f, 0.2f, 0.9f, 0.35f));
                Image task1CatIcon = CreateTaskCatIcon(task1Image.transform, new Color(0.6f, 0.4f, 0.2f, 0.45f));
                GameObject task1Lock = CreateTaskLock(task1Image.transform);
                GameObject task2Lock = CreateTaskLock(task2Image.transform);
                CreateTaskSlot(root.transform, "Task3");
                CreateTaskSlot(root.transform, "Task4");
                CreateTaskSlot(root.transform, "Task5");

                MainView view = root.GetComponent<MainView>();
                view.Bind(CreateMainTaskBindings(root.transform));
                view.SetAssetsRuntime(null);
                view.Render(new MainVm
                {
                    TaskItems = new[]
                    {
                        new MainTaskItemVm { Title = "A", Progress = "1/1", Reward = "R1", ProgressNormalized = 1f, IsClaimable = true, ButtonEnabled = true, CatId = "cat-a" },
                        new MainTaskItemVm { Title = "B", Progress = "未解锁", Reward = "广告位后续接入", ProgressNormalized = 0f, IsLocked = true, ButtonEnabled = false },
                    }
                });

                Assert.That(task1Image.color, Is.EqualTo(new Color(0.2f, 0.4f, 0.8f, 0.6f)));
                Assert.That(task2Image.color, Is.EqualTo(new Color(0.9f, 0.7f, 0.3f, 0.85f)));
                Assert.That(task1RewardIcon.color, Is.EqualTo(new Color(0.1f, 0.8f, 0.3f, 0.5f)));
                Assert.That(task2RewardIcon.color, Is.EqualTo(new Color(0.7f, 0.2f, 0.9f, 0.35f)));
                Assert.That(task1CatIcon.color, Is.EqualTo(new Color(0.6f, 0.4f, 0.2f, 0.45f)));

                Button task1Button = task1Image.GetComponent<Button>();
                Button task2Button = task2Image.GetComponent<Button>();
                Assert.That(task1Button.transition, Is.EqualTo(Selectable.Transition.None));
                Assert.That(task1Button.targetGraphic, Is.Null);
                Assert.That(task2Button.transition, Is.EqualTo(Selectable.Transition.None));
                Assert.That(task2Button.targetGraphic, Is.Null);
                Assert.That(task1Lock.activeSelf, Is.False, "解锁任务槽应隐藏 prefab 内 lock 节点。");
                Assert.That(task2Lock.activeSelf, Is.True, "锁定任务槽应显示 prefab 内 lock 节点。");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MainView_RenderTaskSlots_WritesProgressIntoPrefabCountText()
        {
            var root = new GameObject("MainRoot", typeof(RectTransform), typeof(MainView));
            try
            {
                Text task1Count = CreateTaskSlotWithCount(root.transform, "Task1");
                CreateTaskSlot(root.transform, "Task2");
                CreateTaskSlot(root.transform, "Task3");
                CreateTaskSlot(root.transform, "Task4");
                CreateTaskSlot(root.transform, "Task5");

                MainView view = root.GetComponent<MainView>();
                view.Bind(CreateMainTaskBindings(root.transform));
                view.Render(new MainVm
                {
                    TaskItems = new[]
                    {
                        new MainTaskItemVm { Title = "A", Progress = "查找猫 2/5", Reward = "R1", ProgressNormalized = 0.4f, ButtonEnabled = true },
                    }
                });

                Assert.That(task1Count.text, Is.EqualTo("查找猫 2/5"));
                Assert.That(root.transform.Find("Task1/RuntimeTaskProgress"), Is.Null);
                Assert.That(root.transform.Find("Task1/RuntimeTaskTitle"), Is.Null);
                Assert.That(root.transform.Find("Task1/RuntimeTaskReward"), Is.Null);
                Assert.That(root.transform.Find("Task1/TaskTitle")?.GetComponent<TextMeshProUGUI>()?.text, Is.EqualTo("A"));
                Assert.That(root.transform.Find("Task1/TaskReward")?.GetComponent<TextMeshProUGUI>()?.text, Is.EqualTo("R1"));
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
        public void MainPresenter_Build_IncludesBoardFrameSettingsFromCurrentMap()
        {
            var catalog = CreateMultiCatTaskCatalog("cat-a");
            var taskService = new HolmasTaskProgressService(catalog, new ScriptedRandomSource(0), new FixedUtcClock { UtcNowMilliseconds = 1000 });
            var metaService = new HolmasMetaProgressionService(
                HolmasTestSupport.CreateMetaCatalog(),
                catalog,
                new HolmasDefaultMetaExperienceSource(),
                new HolmasDefaultMetaExperienceSource());
            var coordinator = new HolmasProgressionCoordinator(taskService, metaService);
            var runtime = new HolmasGameplayRuntime(taskService, metaService, coordinator, new NullLogger(), null);
            var request = HolmasTestSupport.CreateRequest(
                "map-bg",
                TerrainAssetPathUtility.BuildAssetPath("1"),
                1,
                1,
                1,
                new BoardSpawnEntry { CatId = "cat-a", Weight = 1 });
            runtime.StartLevel(HolmasTestSupport.CreateTerrain(1, 1), request);

            var serviceContainer = new FakeServiceContainer();
            serviceContainer.RegisterSingleton<IHolmasTaskCatalog>(catalog);
            serviceContainer.RegisterSingleton<IHolmasMapCatalog>(new HolmasMapCatalog(new[]
            {
                new HolmasMapDefinition
                {
                    MapId = "map-bg",
                    TerrainPath = request.TerrainPath,
                    BoardBackgroundPath = "Assets/HotUpdateContent/Res/Textures/NewUIRes/kuang.png",
                    BoardFrameOverlayPath = "Assets/HotUpdateContent/Res/Textures/NewUIRes/kuang_overlay.png",
                    BoardContentInset = new Vector4(12f, 8f, 16f, 10f),
                    MinCellSpacing = 0f,
                },
            }));
            var context = new HolmasApplicationContext(
                serviceContainer,
                new NullLogger(),
                new FakeTickManager(),
                new FakeEventBus(),
                null,
                runtime);

            MainVm viewModel = new MainPresenter(context).Build();

            Assert.That(viewModel.BoardBackgroundPath, Is.EqualTo("Assets/HotUpdateContent/Res/Textures/NewUIRes/kuang.png"));
            Assert.That(viewModel.BoardFrameOverlayPath, Is.EqualTo("Assets/HotUpdateContent/Res/Textures/NewUIRes/kuang_overlay.png"));
            Assert.That(viewModel.BoardContentInset, Is.EqualTo(new Vector4(12f, 8f, 16f, 10f)));
            Assert.That(viewModel.MinCellSpacing, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void MainPresenter_Build_UsesBoardFrameDefaultsWhenMapConfigMissing()
        {
            var catalog = CreateMultiCatTaskCatalog("cat-a");
            var taskService = new HolmasTaskProgressService(catalog, new ScriptedRandomSource(0), new FixedUtcClock { UtcNowMilliseconds = 1000 });
            var metaService = new HolmasMetaProgressionService(
                HolmasTestSupport.CreateMetaCatalog(),
                catalog,
                new HolmasDefaultMetaExperienceSource(),
                new HolmasDefaultMetaExperienceSource());
            var coordinator = new HolmasProgressionCoordinator(taskService, metaService);
            var runtime = new HolmasGameplayRuntime(taskService, metaService, coordinator, new NullLogger(), null);
            var request = HolmasTestSupport.CreateRequest(
                "map-without-config",
                TerrainAssetPathUtility.BuildAssetPath("1"),
                1,
                1,
                1,
                new BoardSpawnEntry { CatId = "cat-a", Weight = 1 });
            runtime.StartLevel(HolmasTestSupport.CreateTerrain(1, 1), request);

            var serviceContainer = new FakeServiceContainer();
            serviceContainer.RegisterSingleton<IHolmasTaskCatalog>(catalog);
            serviceContainer.RegisterSingleton<IHolmasMapCatalog>(new HolmasMapCatalog());
            var context = new HolmasApplicationContext(
                serviceContainer,
                new NullLogger(),
                new FakeTickManager(),
                new FakeEventBus(),
                null,
                runtime);

            MainVm viewModel = new MainPresenter(context).Build();

            Assert.That(viewModel.BoardBackgroundPath, Is.Empty);
            Assert.That(viewModel.BoardFrameOverlayPath, Is.Empty);
            Assert.That(viewModel.BoardContentInset, Is.EqualTo(Vector4.zero));
            Assert.That(viewModel.MinCellSpacing, Is.EqualTo(4f).Within(0.001f));
        }

        [Test]
        public void MainPresenter_Build_UsesBoardFrameDefaultsDuringTutorialSession()
        {
            var catalog = CreateMultiCatTaskCatalog("cat-a");
            var taskService = new HolmasTaskProgressService(catalog, new ScriptedRandomSource(0), new FixedUtcClock { UtcNowMilliseconds = 1000 });
            var metaService = new HolmasMetaProgressionService(
                HolmasTestSupport.CreateMetaCatalog(),
                catalog,
                new HolmasDefaultMetaExperienceSource(),
                new HolmasDefaultMetaExperienceSource());
            var coordinator = new HolmasProgressionCoordinator(taskService, metaService);
            var runtime = new HolmasGameplayRuntime(taskService, metaService, coordinator, new NullLogger(), null);
            var tutorialSessionService = new CoreFindCatTutorialSessionService(new NullLogger());
            tutorialSessionService.StartSessionAsync(new FakeAssetsRuntime(HolmasTestSupport.CreateTerrain(11, 8)))
                .GetAwaiter()
                .GetResult();

            var serviceContainer = new FakeServiceContainer();
            serviceContainer.RegisterSingleton<IHolmasTaskCatalog>(catalog);
            serviceContainer.RegisterSingleton<IHolmasMapCatalog>(new HolmasMapCatalog(new[]
            {
                new HolmasMapDefinition
                {
                    MapId = "tutorial-map",
                    BoardBackgroundPath = "background",
                    BoardFrameOverlayPath = "overlay",
                    BoardContentInset = new Vector4(1f, 2f, 3f, 4f),
                    MinCellSpacing = 1f,
                },
            }));
            var context = new HolmasApplicationContext(
                serviceContainer,
                new NullLogger(),
                new FakeTickManager(),
                new FakeEventBus(),
                null,
                runtime);

            MainVm viewModel = new MainPresenter(context, tutorialSessionService).Build();

            Assert.That(viewModel.UseTutorialBoardLayer, Is.True);
            Assert.That(viewModel.BoardBackgroundPath, Is.Empty);
            Assert.That(viewModel.BoardFrameOverlayPath, Is.Empty);
            Assert.That(viewModel.BoardContentInset, Is.EqualTo(Vector4.zero));
            Assert.That(viewModel.MinCellSpacing, Is.EqualTo(4f).Within(0.001f));
        }

        [Test]
        public void BoardFrameLayoutCalculator_KeepsSquareCellsAndUsesAxisSpacing()
        {
            BoardFrameLayout layout = BoardFrameLayoutCalculator.Calculate(
                new Vector2(950f, 775f),
                8,
                10);

            Assert.That(layout.IsValid, Is.True);
            Assert.That(layout.CellSize.x, Is.EqualTo(layout.CellSize.y).Within(0.001f));
            Assert.That(layout.CellSize.x, Is.EqualTo(91.4f).Within(0.001f));
            Assert.That(layout.Spacing.x, Is.EqualTo(4f).Within(0.001f));
            Assert.That(layout.Spacing.y, Is.EqualTo(43.8f / 7f).Within(0.001f));
            Assert.That(layout.ContainerOffsetMin, Is.EqualTo(Vector2.zero));
            Assert.That(layout.ContainerOffsetMax, Is.EqualTo(Vector2.zero));
            AssertGridFillsContent(layout, 8, 10);
        }

        [TestCase(8, 8, 720f, 720f, 0f)]
        [TestCase(8, 10, 950f, 775f, 4f)]
        [TestCase(10, 8, 768f, 958f, 3f)]
        [TestCase(6, 12, 1150f, 590f, 5f)]
        [TestCase(12, 6, 570f, 1170f, 5f)]
        public void BoardFrameLayoutCalculator_FillsContentRectWithSquareCells(
            int rows,
            int cols,
            float width,
            float height,
            float minSpacing)
        {
            BoardFrameLayout layout = BoardFrameLayoutCalculator.Calculate(
                new Vector2(width, height),
                rows,
                cols,
                minSpacing);

            Assert.That(layout.IsValid, Is.True);
            Assert.That(layout.CellSize.x, Is.EqualTo(layout.CellSize.y).Within(0.001f));
            Assert.That(layout.Spacing.x, Is.GreaterThanOrEqualTo(minSpacing - 0.001f));
            Assert.That(layout.Spacing.y, Is.GreaterThanOrEqualTo(minSpacing - 0.001f));
            AssertGridFillsContent(layout, rows, cols);
        }

        [Test]
        public void BoardFrameLayoutCalculator_AllowsZeroSpacingForTinyContentRect()
        {
            BoardFrameLayout layout = BoardFrameLayoutCalculator.Calculate(
                new Vector2(30f, 24f),
                6,
                12,
                4f);

            Assert.That(layout.CellSize.x, Is.EqualTo(layout.CellSize.y).Within(0.001f));
            Assert.That(layout.Spacing.x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(layout.Spacing.y, Is.GreaterThanOrEqualTo(0f));
            AssertGridFillsContent(layout, 6, 12);
        }

        [Test]
        public void FindCatBoardView_Render_WithFrameLayout_DoesNotOverwriteSpacing()
        {
            var boardObject = new GameObject("BoardContainer", typeof(RectTransform), typeof(FindCatBoardView));
            try
            {
                var boardRect = boardObject.GetComponent<RectTransform>();
                boardRect.sizeDelta = new Vector2(1000f, 800f);
                var cells = Enumerable.Range(0, 80)
                    .Select(index => new BoardCellState(index, true, false, false, false, string.Empty, 0, new Color32(32, 48, 64, 255)))
                    .ToArray();
                var frameLayout = new BoardFrameLayout(
                    Vector2.zero,
                    Vector2.zero,
                    new Vector2(40f, 40f),
                    new Vector2(3f, 7f),
                    new Vector2(427f, 369f));

                boardObject.GetComponent<FindCatBoardView>().Render(8, 10, cells, null, frameLayout, null);

                GridLayoutGroup layout = boardObject.GetComponent<GridLayoutGroup>();
                Assert.That(layout.cellSize.x, Is.EqualTo(40f).Within(0.001f));
                Assert.That(layout.cellSize.y, Is.EqualTo(40f).Within(0.001f));
                Assert.That(layout.spacing.x, Is.EqualTo(3f).Within(0.001f));
                Assert.That(layout.spacing.y, Is.EqualTo(7f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(boardObject);
            }
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
                var boardContainer = new GameObject("BoardContainer", typeof(RectTransform), typeof(FindCatBoardView));
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
        public void MainView_RenderBoard_UsesBoardContentRectAndKeepsRectMaskPaddingZero()
        {
            var root = new GameObject("MainRoot", typeof(RectTransform), typeof(MainView));
            try
            {
                var minesBg = new GameObject("MinesBg", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
                minesBg.transform.SetParent(root.transform, false);
                var minesBgRect = minesBg.GetComponent<RectTransform>();
                minesBgRect.sizeDelta = new Vector2(1000f, 800f);

                var boardContentRectObject = new GameObject("BoardContentRect", typeof(RectTransform));
                boardContentRectObject.transform.SetParent(minesBg.transform, false);
                var boardContentRect = boardContentRectObject.GetComponent<RectTransform>();
                boardContentRect.anchorMin = Vector2.zero;
                boardContentRect.anchorMax = Vector2.one;
                boardContentRect.offsetMin = new Vector2(40f, 30f);
                boardContentRect.offsetMax = new Vector2(-60f, -50f);

                var minesGroup = new GameObject("MinesGroup", typeof(RectTransform), typeof(GridLayoutGroup));
                minesGroup.transform.SetParent(minesBg.transform, false);
                var placeholderTile = new GameObject("PlaceholderTile", typeof(RectTransform), typeof(Image));
                placeholderTile.transform.SetParent(minesGroup.transform, false);
                var boardContainer = new GameObject("BoardContainer", typeof(RectTransform), typeof(FindCatBoardView));
                boardContainer.transform.SetParent(minesGroup.transform, false);
                var tutorialBoardContainer = new GameObject("TutorialBoardContainer", typeof(RectTransform), typeof(FindCatBoardView));
                tutorialBoardContainer.transform.SetParent(minesGroup.transform, false);

                MainView view = root.GetComponent<MainView>();
                view.Bind(new MainBindings
                {
                    MinesBgImage = minesBg.GetComponent<Image>(),
                    MinesBgMask = minesBg.GetComponent<RectMask2D>(),
                    BoardContentRect = boardContentRect,
                    MinesGroup = minesGroup.GetComponent<RectTransform>(),
                    BoardContainer = boardContainer.GetComponent<RectTransform>(),
                    TutorialBoardContainer = tutorialBoardContainer.GetComponent<RectTransform>(),
                });

                var cells = Enumerable.Range(0, 80)
                    .Select(index => new BoardCellState(index, true, false, false, false, string.Empty, 0, new Color32(32, 48, 64, 255)))
                    .ToArray();

                view.Render(new MainVm
                {
                    BoardVisible = true,
                    Rows = 8,
                    Cols = 10,
                    Cells = cells,
                    BoardContentInset = new Vector4(25f, 12.5f, 25f, 12.5f),
                    MinCellSpacing = 4f,
                });

                var boardRect = boardContainer.GetComponent<RectTransform>();
                GridLayoutGroup boardLayout = boardContainer.GetComponent<GridLayoutGroup>();
                Assert.That(placeholderTile.activeSelf, Is.False);
                Assert.That(minesGroup.GetComponent<GridLayoutGroup>().enabled, Is.False);
                Assert.That(boardRect.anchorMin, Is.EqualTo(Vector2.zero));
                Assert.That(boardRect.anchorMax, Is.EqualTo(Vector2.one));
                Assert.That(boardRect.offsetMin.x, Is.EqualTo(40f).Within(0.001f));
                Assert.That(boardRect.offsetMin.y, Is.EqualTo(30f).Within(0.001f));
                Assert.That(boardRect.offsetMax.x, Is.EqualTo(-60f).Within(0.001f));
                Assert.That(boardRect.offsetMax.y, Is.EqualTo(-50f).Within(0.001f));
                Assert.That(boardLayout.cellSize.x, Is.EqualTo(boardLayout.cellSize.y).Within(0.001f));
                Assert.That(boardLayout.cellSize.x, Is.EqualTo(86.4f).Within(0.001f));
                Assert.That(boardLayout.spacing.x, Is.EqualTo(4f).Within(0.001f));
                Assert.That(boardLayout.spacing.y, Is.EqualTo(28.8f / 7f).Within(0.001f));
                Assert.That(boardLayout.padding.left, Is.EqualTo(0));
                Assert.That(boardLayout.padding.right, Is.EqualTo(0));
                Assert.That(boardLayout.padding.top, Is.EqualTo(0));
                Assert.That(boardLayout.padding.bottom, Is.EqualTo(0));
                Assert.That(minesBg.GetComponent<RectMask2D>().padding, Is.EqualTo(Vector4.zero));

                minesBgRect.sizeDelta = new Vector2(1200f, 900f);
                typeof(MainView)
                    .GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(view, null);

                Assert.That(boardRect.offsetMin.x, Is.EqualTo(40f).Within(0.001f));
                Assert.That(boardRect.offsetMin.y, Is.EqualTo(30f).Within(0.001f));
                Assert.That(boardRect.offsetMax.x, Is.EqualTo(-60f).Within(0.001f));
                Assert.That(boardRect.offsetMax.y, Is.EqualTo(-50f).Within(0.001f));
                Assert.That(boardLayout.cellSize.x, Is.EqualTo(boardLayout.cellSize.y).Within(0.001f));
                float resizedGridWidth = boardLayout.cellSize.x * 10 + boardLayout.spacing.x * 9;
                float resizedGridHeight = boardLayout.cellSize.y * 8 + boardLayout.spacing.y * 7;
                Assert.That(resizedGridWidth, Is.EqualTo(1100f).Within(0.001f));
                Assert.That(resizedGridHeight, Is.EqualTo(820f).Within(0.001f));

                boardContentRect.offsetMin = new Vector2(50f, 40f);
                boardContentRect.offsetMax = new Vector2(-70f, -65f);

                view.Render(new MainVm
                {
                    BoardVisible = true,
                    Rows = 8,
                    Cols = 10,
                    Cells = cells,
                    BoardContentInset = new Vector4(20f, 10f, 30f, 15f),
                    MinCellSpacing = 3f,
                });

                Assert.That(boardRect.offsetMin.x, Is.EqualTo(50f).Within(0.001f));
                Assert.That(boardRect.offsetMin.y, Is.EqualTo(40f).Within(0.001f));
                Assert.That(boardRect.offsetMax.x, Is.EqualTo(-70f).Within(0.001f));
                Assert.That(boardRect.offsetMax.y, Is.EqualTo(-65f).Within(0.001f));
                Assert.That(boardLayout.cellSize.x, Is.EqualTo(boardLayout.cellSize.y).Within(0.001f));
                float gridWidth = boardLayout.cellSize.x * 10 + boardLayout.spacing.x * 9;
                float gridHeight = boardLayout.cellSize.y * 8 + boardLayout.spacing.y * 7;
                Assert.That(gridWidth, Is.EqualTo(1080f).Within(0.001f));
                Assert.That(gridHeight, Is.EqualTo(795f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [UnityTest]
        public IEnumerator MainView_RenderBoard_IgnoresStaleBoardBackgroundLoad()
        {
            var root = new GameObject("MainRoot", typeof(RectTransform), typeof(MainView));
            var defaultTexture = new Texture2D(16, 16);
            var firstTexture = new Texture2D(16, 16);
            var secondTexture = new Texture2D(16, 16);
            Sprite defaultSprite = Sprite.Create(defaultTexture, new Rect(0f, 0f, 16f, 16f), new Vector2(0.5f, 0.5f));
            Sprite firstSprite = Sprite.Create(firstTexture, new Rect(0f, 0f, 16f, 16f), new Vector2(0.5f, 0.5f));
            Sprite secondSprite = Sprite.Create(secondTexture, new Rect(0f, 0f, 16f, 16f), new Vector2(0.5f, 0.5f));
            try
            {
                var minesBg = new GameObject("MinesBg", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
                minesBg.transform.SetParent(root.transform, false);
                minesBg.GetComponent<RectTransform>().sizeDelta = new Vector2(100f, 100f);
                Image minesBgImage = minesBg.GetComponent<Image>();
                minesBgImage.sprite = defaultSprite;
                var overlay = new GameObject("MinesBgFrameOverlayImage", typeof(RectTransform), typeof(Image));
                overlay.transform.SetParent(minesBg.transform, false);
                Image overlayImage = overlay.GetComponent<Image>();
                overlayImage.sprite = defaultSprite;

                var minesGroup = new GameObject("MinesGroup", typeof(RectTransform), typeof(GridLayoutGroup));
                minesGroup.transform.SetParent(minesBg.transform, false);
                var boardContainer = new GameObject("BoardContainer", typeof(RectTransform), typeof(FindCatBoardView));
                boardContainer.transform.SetParent(minesGroup.transform, false);
                var tutorialBoardContainer = new GameObject("TutorialBoardContainer", typeof(RectTransform), typeof(FindCatBoardView));
                tutorialBoardContainer.transform.SetParent(minesGroup.transform, false);

                MainView view = root.GetComponent<MainView>();
                view.Bind(new MainBindings
                {
                    MinesBgImage = minesBgImage,
                    MinesBgMask = minesBg.GetComponent<RectMask2D>(),
                    MinesBgFrameOverlayImage = overlayImage,
                    MinesGroup = minesGroup.GetComponent<RectTransform>(),
                    BoardContainer = boardContainer.GetComponent<RectTransform>(),
                    TutorialBoardContainer = tutorialBoardContainer.GetComponent<RectTransform>(),
                });
                var assetsRuntime = new DeferredAssetsRuntime();
                view.SetAssetsRuntime(assetsRuntime);
                var cells = new[]
                {
                    new BoardCellState(0, true, false, false, false, string.Empty, 0, new Color32(32, 48, 64, 255)),
                };

                view.Render(new MainVm { BoardVisible = true, Rows = 1, Cols = 1, Cells = cells, BoardBackgroundPath = "first" });
                view.Render(new MainVm { BoardVisible = true, Rows = 1, Cols = 1, Cells = cells, BoardBackgroundPath = "second" });
                assetsRuntime.Complete("first", firstSprite);
                yield return null;

                Assert.That(minesBgImage.sprite, Is.SameAs(defaultSprite), "旧请求先返回时不能覆盖当前棋盘背景。");
                Assert.That(overlayImage.sprite, Is.SameAs(defaultSprite), "旧请求先返回时不能覆盖当前棋盘边框。");

                assetsRuntime.Complete("second", secondSprite);
                yield return null;

                Assert.That(minesBgImage.sprite, Is.SameAs(secondSprite));
                Assert.That(overlayImage.sprite, Is.SameAs(secondSprite));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(defaultSprite);
                Object.DestroyImmediate(firstSprite);
                Object.DestroyImmediate(secondSprite);
                Object.DestroyImmediate(defaultTexture);
                Object.DestroyImmediate(firstTexture);
                Object.DestroyImmediate(secondTexture);
            }
        }

        [UnityTest]
        public IEnumerator MainView_RenderBoard_LoadsBackgroundAndOverlayTogether()
        {
            var root = new GameObject("MainRoot", typeof(RectTransform), typeof(MainView));
            var defaultTexture = new Texture2D(16, 16);
            var backgroundTexture = new Texture2D(16, 16);
            var overlayTexture = new Texture2D(16, 16);
            Sprite defaultSprite = Sprite.Create(defaultTexture, new Rect(0f, 0f, 16f, 16f), new Vector2(0.5f, 0.5f));
            Sprite backgroundSprite = Sprite.Create(backgroundTexture, new Rect(0f, 0f, 16f, 16f), new Vector2(0.5f, 0.5f));
            Sprite overlaySprite = Sprite.Create(overlayTexture, new Rect(0f, 0f, 16f, 16f), new Vector2(0.5f, 0.5f));
            try
            {
                var minesBg = new GameObject("MinesBg", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
                minesBg.transform.SetParent(root.transform, false);
                minesBg.GetComponent<RectTransform>().sizeDelta = new Vector2(100f, 100f);
                Image minesBgImage = minesBg.GetComponent<Image>();
                minesBgImage.sprite = defaultSprite;
                var overlay = new GameObject("MinesBgFrameOverlayImage", typeof(RectTransform), typeof(Image));
                overlay.transform.SetParent(minesBg.transform, false);
                Image overlayImage = overlay.GetComponent<Image>();
                overlayImage.sprite = defaultSprite;

                var minesGroup = new GameObject("MinesGroup", typeof(RectTransform), typeof(GridLayoutGroup));
                minesGroup.transform.SetParent(minesBg.transform, false);
                var boardContainer = new GameObject("BoardContainer", typeof(RectTransform), typeof(FindCatBoardView));
                boardContainer.transform.SetParent(minesGroup.transform, false);

                MainView view = root.GetComponent<MainView>();
                view.Bind(new MainBindings
                {
                    MinesBgImage = minesBgImage,
                    MinesBgMask = minesBg.GetComponent<RectMask2D>(),
                    MinesBgFrameOverlayImage = overlayImage,
                    MinesGroup = minesGroup.GetComponent<RectTransform>(),
                    BoardContainer = boardContainer.GetComponent<RectTransform>(),
                });
                var assetsRuntime = new DeferredAssetsRuntime();
                view.SetAssetsRuntime(assetsRuntime);
                var cells = new[]
                {
                    new BoardCellState(0, true, false, false, false, string.Empty, 0, new Color32(32, 48, 64, 255)),
                };

                view.Render(new MainVm
                {
                    BoardVisible = true,
                    Rows = 1,
                    Cols = 1,
                    Cells = cells,
                    BoardBackgroundPath = "background",
                    BoardFrameOverlayPath = "overlay",
                });
                assetsRuntime.Complete("background", backgroundSprite);
                yield return null;
                Assert.That(minesBgImage.sprite, Is.SameAs(defaultSprite), "overlay 尚未返回前不能只更新底图。");

                assetsRuntime.Complete("overlay", overlaySprite);
                yield return null;
                Assert.That(minesBgImage.sprite, Is.SameAs(backgroundSprite));
                Assert.That(overlayImage.sprite, Is.SameAs(overlaySprite));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(defaultSprite);
                Object.DestroyImmediate(backgroundSprite);
                Object.DestroyImmediate(overlaySprite);
                Object.DestroyImmediate(defaultTexture);
                Object.DestroyImmediate(backgroundTexture);
                Object.DestroyImmediate(overlayTexture);
            }
        }

        [UnityTest]
        public IEnumerator MainView_RenderBoard_LoadFailureRestoresDefaultBoardBackground()
        {
            var root = new GameObject("MainRoot", typeof(RectTransform), typeof(MainView));
            var defaultTexture = new Texture2D(16, 16);
            var firstTexture = new Texture2D(16, 16);
            var badAsset = new TextAsset("not a sprite");
            Sprite defaultSprite = Sprite.Create(defaultTexture, new Rect(0f, 0f, 16f, 16f), new Vector2(0.5f, 0.5f));
            Sprite firstSprite = Sprite.Create(firstTexture, new Rect(0f, 0f, 16f, 16f), new Vector2(0.5f, 0.5f));
            try
            {
                var minesBg = new GameObject("MinesBg", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
                minesBg.transform.SetParent(root.transform, false);
                minesBg.GetComponent<RectTransform>().sizeDelta = new Vector2(100f, 100f);
                Image minesBgImage = minesBg.GetComponent<Image>();
                minesBgImage.sprite = defaultSprite;
                var overlay = new GameObject("MinesBgFrameOverlayImage", typeof(RectTransform), typeof(Image));
                overlay.transform.SetParent(minesBg.transform, false);
                Image overlayImage = overlay.GetComponent<Image>();
                overlayImage.sprite = defaultSprite;

                var minesGroup = new GameObject("MinesGroup", typeof(RectTransform), typeof(GridLayoutGroup));
                minesGroup.transform.SetParent(minesBg.transform, false);
                var boardContainer = new GameObject("BoardContainer", typeof(RectTransform), typeof(FindCatBoardView));
                boardContainer.transform.SetParent(minesGroup.transform, false);
                var tutorialBoardContainer = new GameObject("TutorialBoardContainer", typeof(RectTransform), typeof(FindCatBoardView));
                tutorialBoardContainer.transform.SetParent(minesGroup.transform, false);

                MainView view = root.GetComponent<MainView>();
                view.Bind(new MainBindings
                {
                    MinesBgImage = minesBgImage,
                    MinesBgMask = minesBg.GetComponent<RectMask2D>(),
                    MinesBgFrameOverlayImage = overlayImage,
                    MinesGroup = minesGroup.GetComponent<RectTransform>(),
                    BoardContainer = boardContainer.GetComponent<RectTransform>(),
                    TutorialBoardContainer = tutorialBoardContainer.GetComponent<RectTransform>(),
                });
                var assetsRuntime = new DeferredAssetsRuntime();
                view.SetAssetsRuntime(assetsRuntime);
                var cells = new[]
                {
                    new BoardCellState(0, true, false, false, false, string.Empty, 0, new Color32(32, 48, 64, 255)),
                };

                view.Render(new MainVm { BoardVisible = true, Rows = 1, Cols = 1, Cells = cells, BoardBackgroundPath = "first-success" });
                assetsRuntime.Complete("first-success", firstSprite);
                yield return null;
                Assert.That(minesBgImage.sprite, Is.SameAs(firstSprite));
                Assert.That(overlayImage.sprite, Is.SameAs(firstSprite));

                view.Render(new MainVm { BoardVisible = true, Rows = 1, Cols = 1, Cells = cells, BoardBackgroundPath = "bad-texture" });
                LogAssert.Expect(
                    LogType.Warning,
                    "MainView: 棋盘背景或边框资源无法转换为 Sprite，已回退 prefab 默认背景。failed=MainPanel/BackgroundImage/MinesBg(path=bad-texture, assetType=TextAsset), MainPanel/BackgroundImage/MinesBg/MinesBgFrameOverlayImage(path=bad-texture, assetType=TextAsset); background={control=MainPanel/BackgroundImage/MinesBg, path=bad-texture, assetType=TextAsset}; overlay={control=MainPanel/BackgroundImage/MinesBg/MinesBgFrameOverlayImage, path=bad-texture, assetType=TextAsset}");
                assetsRuntime.Complete("bad-texture", badAsset);
                yield return null;

                Assert.That(minesBgImage.sprite, Is.SameAs(defaultSprite));
                Assert.That(overlayImage.sprite, Is.SameAs(defaultSprite));
                Assert.That(assetsRuntime.GetLoadRequestCount("bad-texture"), Is.EqualTo(1));

                view.Render(new MainVm { BoardVisible = true, Rows = 1, Cols = 1, Cells = cells, BoardBackgroundPath = "bad-texture" });
                yield return null;
                Assert.That(assetsRuntime.GetLoadRequestCount("bad-texture"), Is.EqualTo(1), "同一路径已失败时，返回 MainPanel 后不应重复加载刷 warning。");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(defaultSprite);
                Object.DestroyImmediate(firstSprite);
                Object.DestroyImmediate(defaultTexture);
                Object.DestroyImmediate(firstTexture);
                Object.DestroyImmediate(badAsset);
            }
        }

        [UnityTest]
        public IEnumerator MainView_RenderBoard_OverlayLoadFailureRestoresBothSprites()
        {
            var root = new GameObject("MainRoot", typeof(RectTransform), typeof(MainView));
            var defaultTexture = new Texture2D(16, 16);
            var backgroundTexture = new Texture2D(16, 16);
            var badOverlayAsset = new TextAsset("not a sprite");
            Sprite defaultSprite = Sprite.Create(defaultTexture, new Rect(0f, 0f, 16f, 16f), new Vector2(0.5f, 0.5f));
            Sprite backgroundSprite = Sprite.Create(backgroundTexture, new Rect(0f, 0f, 16f, 16f), new Vector2(0.5f, 0.5f));
            try
            {
                var minesBg = new GameObject("MinesBg", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
                minesBg.transform.SetParent(root.transform, false);
                minesBg.GetComponent<RectTransform>().sizeDelta = new Vector2(100f, 100f);
                Image minesBgImage = minesBg.GetComponent<Image>();
                minesBgImage.sprite = defaultSprite;
                var overlay = new GameObject("MinesBgFrameOverlayImage", typeof(RectTransform), typeof(Image));
                overlay.transform.SetParent(minesBg.transform, false);
                Image overlayImage = overlay.GetComponent<Image>();
                overlayImage.sprite = defaultSprite;

                var minesGroup = new GameObject("MinesGroup", typeof(RectTransform), typeof(GridLayoutGroup));
                minesGroup.transform.SetParent(minesBg.transform, false);
                var boardContainer = new GameObject("BoardContainer", typeof(RectTransform), typeof(FindCatBoardView));
                boardContainer.transform.SetParent(minesGroup.transform, false);

                MainView view = root.GetComponent<MainView>();
                view.Bind(new MainBindings
                {
                    MinesBgImage = minesBgImage,
                    MinesBgMask = minesBg.GetComponent<RectMask2D>(),
                    MinesBgFrameOverlayImage = overlayImage,
                    MinesGroup = minesGroup.GetComponent<RectTransform>(),
                    BoardContainer = boardContainer.GetComponent<RectTransform>(),
                });
                var assetsRuntime = new DeferredAssetsRuntime();
                view.SetAssetsRuntime(assetsRuntime);
                var cells = new[]
                {
                    new BoardCellState(0, true, false, false, false, string.Empty, 0, new Color32(32, 48, 64, 255)),
                };

                view.Render(new MainVm
                {
                    BoardVisible = true,
                    Rows = 1,
                    Cols = 1,
                    Cells = cells,
                    BoardBackgroundPath = "background",
                    BoardFrameOverlayPath = "bad-overlay",
                });
                assetsRuntime.Complete("background", backgroundSprite);
                yield return null;
                LogAssert.Expect(
                    LogType.Warning,
                    "MainView: 棋盘背景或边框资源无法转换为 Sprite，已回退 prefab 默认背景。failed=MainPanel/BackgroundImage/MinesBg/MinesBgFrameOverlayImage(path=bad-overlay, assetType=TextAsset); background={control=MainPanel/BackgroundImage/MinesBg, path=background, assetType=Sprite}; overlay={control=MainPanel/BackgroundImage/MinesBg/MinesBgFrameOverlayImage, path=bad-overlay, assetType=TextAsset}");
                assetsRuntime.Complete("bad-overlay", badOverlayAsset);
                yield return null;

                Assert.That(minesBgImage.sprite, Is.SameAs(defaultSprite));
                Assert.That(overlayImage.sprite, Is.SameAs(defaultSprite));
                Assert.That(assetsRuntime.GetLoadRequestCount("background"), Is.EqualTo(1));
                Assert.That(assetsRuntime.GetLoadRequestCount("bad-overlay"), Is.EqualTo(1));

                view.Render(new MainVm
                {
                    BoardVisible = true,
                    Rows = 1,
                    Cols = 1,
                    Cells = cells,
                    BoardBackgroundPath = "background",
                    BoardFrameOverlayPath = "bad-overlay",
                });
                yield return null;
                Assert.That(assetsRuntime.GetLoadRequestCount("background"), Is.EqualTo(1), "同一个 overlay 失败请求不应再次加载背景。");
                Assert.That(assetsRuntime.GetLoadRequestCount("bad-overlay"), Is.EqualTo(1), "同一个 overlay 失败请求不应再次加载 overlay。");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(defaultSprite);
                Object.DestroyImmediate(backgroundSprite);
                Object.DestroyImmediate(defaultTexture);
                Object.DestroyImmediate(backgroundTexture);
                Object.DestroyImmediate(badOverlayAsset);
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
                var boardContainer = new GameObject("BoardContainer", typeof(RectTransform), typeof(FindCatBoardView));
                boardContainer.transform.SetParent(minesGroup.transform, false);
                var tutorialBoardContainer = new GameObject("TutorialBoardContainer", typeof(RectTransform), typeof(FindCatBoardView));
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
                var boardContainer = new GameObject("BoardContainer", typeof(RectTransform), typeof(FindCatBoardView));
                boardContainer.transform.SetParent(minesGroup.transform, false);
                var tutorialBoardContainer = new GameObject("TutorialBoardContainer", typeof(RectTransform), typeof(FindCatBoardView));
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
        public void MainBindings_MinesGroupPath_LivesUnderMinesBg()
        {
            Assert.That(MainBindings.MinesGroupNodePath, Is.EqualTo("MainPanel/BackgroundImage/MinesBg/MinesGroup"));
            Assert.That(MainBindings.MinesBgFrameOverlayNodePath, Is.EqualTo("MainPanel/BackgroundImage/MinesBg/MinesBgFrameOverlayImage"));
            Assert.That(MainBindings.BoardContentRectNodePath, Is.EqualTo("MainPanel/BackgroundImage/MinesBg/BoardContentRect"));
            Assert.That(MainBindings.BoardContainerNodePath, Is.EqualTo("MainPanel/BackgroundImage/MinesBg/MinesGroup/BoardContainer"));
            Assert.That(MainBindings.TutorialBoardContainerNodePath, Is.EqualTo("MainPanel/BackgroundImage/MinesBg/MinesGroup/TutorialBoardContainer"));
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

        private static void AssertGridFillsContent(BoardFrameLayout layout, int rows, int cols)
        {
            float gridWidth = layout.CellSize.x * cols + layout.Spacing.x * Mathf.Max(0, cols - 1);
            float gridHeight = layout.CellSize.y * rows + layout.Spacing.y * Mathf.Max(0, rows - 1);
            Assert.That(gridWidth, Is.EqualTo(layout.ContentSize.x).Within(0.001f), "grid width must fill content rect");
            Assert.That(gridHeight, Is.EqualTo(layout.ContentSize.y).Within(0.001f), "grid height must fill content rect");
        }

        private sealed class DeferredAssetsRuntime : IAssetsRuntime
        {
            private readonly Dictionary<string, TaskCompletionSource<IAssetHandle>> _requests =
                new Dictionary<string, TaskCompletionSource<IAssetHandle>>();
            private readonly Dictionary<string, int> _loadRequestCounts =
                new Dictionary<string, int>();

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
                _loadRequestCounts.TryGetValue(location, out int count);
                _loadRequestCounts[location] = count + 1;
                if (!_requests.TryGetValue(location, out TaskCompletionSource<IAssetHandle> request))
                {
                    request = new TaskCompletionSource<IAssetHandle>();
                    _requests[location] = request;
                }

                return request.Task;
            }

            public void Complete(string location, UnityEngine.Object asset)
            {
                if (!_requests.TryGetValue(location, out TaskCompletionSource<IAssetHandle> request))
                {
                    request = new TaskCompletionSource<IAssetHandle>();
                    _requests[location] = request;
                }

                request.SetResult(new FakeAssetHandle(asset));
            }

            public int GetLoadRequestCount(string location)
            {
                return _loadRequestCounts.TryGetValue(location, out int count) ? count : 0;
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

        private static HolmasAgencyCatalog CreateFiveStagePromotionCatalog()
        {
            return new HolmasAgencyCatalog(new[]
            {
                CreatePromotionStageItem(1, "stage-1", "Textures/buildings/building01.png", "leaflet", 3),
                CreatePromotionStageItem(1, "stage-1", "Textures/buildings/building01.png", "radio", 5),
                CreatePromotionStageItem(1, "stage-1", "Textures/buildings/building01.png", "tv", 2),
                CreatePromotionStageItem(2, "stage-2", "Textures/buildings/building02.png", "leaflet", 4),
                CreatePromotionStageItem(2, "stage-2", "Textures/buildings/building02.png", "radio", 4),
                CreatePromotionStageItem(2, "stage-2", "Textures/buildings/building02.png", "tv", 4),
                CreatePromotionStageItem(3, "stage-3", "Textures/buildings/building03.png", "leaflet", 5),
                CreatePromotionStageItem(3, "stage-3", "Textures/buildings/building03.png", "radio", 5),
                CreatePromotionStageItem(3, "stage-3", "Textures/buildings/building03.png", "tv", 5),
                CreatePromotionStageItem(4, "stage-4", "Textures/buildings/building04.png", "leaflet", 5),
                CreatePromotionStageItem(4, "stage-4", "Textures/buildings/building04.png", "radio", 5),
                CreatePromotionStageItem(4, "stage-4", "Textures/buildings/building04.png", "tv", 5),
                CreatePromotionStageItem(5, "stage-5", "Textures/buildings/building05.png", "leaflet", 5),
                CreatePromotionStageItem(5, "stage-5", "Textures/buildings/building05.png", "radio", 5),
                CreatePromotionStageItem(5, "stage-5", "Textures/buildings/building05.png", "tv", 5),
            });
        }

        private static HolmasAgencyCatalog CreateTenStagePromotionCatalog()
        {
            var definitions = new List<HolmasAgencyBuildingDefinition>();
            for (int stageId = 1; stageId <= 10; stageId++)
            {
                int cap = stageId == 1 ? 3 : stageId == 2 ? 4 : 5;
                string stageName = $"stage-{stageId}";
                string stageImage = $"Textures/buildings/building{stageId:00}.png";
                definitions.Add(CreatePromotionStageItem(stageId, stageName, stageImage, "leaflet", cap));
                definitions.Add(CreatePromotionStageItem(stageId, stageName, stageImage, "radio", cap));
                definitions.Add(CreatePromotionStageItem(stageId, stageName, stageImage, "tv", cap));
            }

            return new HolmasAgencyCatalog(definitions);
        }

        private static HolmasAgencyCatalog CreateSixPromotionStageCatalog()
        {
            var definitions = new List<HolmasAgencyBuildingDefinition>();
            for (int i = 1; i <= 6; i++)
            {
                definitions.Add(CreatePromotionStageItem(1, "stage-1", "Textures/buildings/building01.png", $"promo-{i}", i));
            }

            for (int stageId = 2; stageId <= 5; stageId++)
            {
                definitions.Add(CreatePromotionStageItem(stageId, $"stage-{stageId}", $"Textures/buildings/building{stageId:00}.png", "leaflet", 1));
            }

            return new HolmasAgencyCatalog(definitions);
        }

        private static HolmasAgencyBuildingDefinition CreatePromotionStageItem(
            int agencyStageId,
            string stageName,
            string stageImage,
            string promotionId,
            int levelCap)
        {
            return new HolmasAgencyBuildingDefinition
            {
                AgencyStageId = agencyStageId,
                StageName = stageName,
                StageImage = stageImage,
                PromotionId = promotionId,
                PromotionLevelCap = levelCap,
                PromotionUpgradeCosts = Enumerable.Repeat(10, levelCap).ToArray(),
            };
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

            var count = new GameObject("Count", typeof(RectTransform), typeof(Text));
            count.transform.SetParent(slot.transform, false);
            CreateTaskText(slot.transform, "TaskTitle");
            CreateTaskText(slot.transform, "TaskReward");

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

        private static Image CreateTaskSlotImage(Transform parent, string name, Color color)
        {
            var slot = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            slot.transform.SetParent(parent, false);

            Image image = slot.GetComponent<Image>();
            image.color = color;

            Button button = slot.GetComponent<Button>();
            button.transition = Selectable.Transition.ColorTint;
            button.targetGraphic = image;

            var count = new GameObject("Count", typeof(RectTransform), typeof(Text));
            count.transform.SetParent(slot.transform, false);
            CreateTaskText(slot.transform, "TaskTitle");
            CreateTaskText(slot.transform, "TaskReward");

            return image;
        }

        private static Image CreateTaskRewardIcon(Transform parent, Color color)
        {
            var rewardIcon = new GameObject("RewardIcon", typeof(RectTransform), typeof(Image));
            rewardIcon.transform.SetParent(parent, false);

            Image image = rewardIcon.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static Image CreateTaskCatIcon(Transform parent, Color color)
        {
            var catIcon = new GameObject("CatIcon", typeof(RectTransform), typeof(Image));
            catIcon.transform.SetParent(parent, false);

            Image image = catIcon.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static GameObject CreateTaskLock(Transform parent)
        {
            var lockObject = new GameObject("lock", typeof(RectTransform), typeof(Image));
            lockObject.transform.SetParent(parent, false);
            lockObject.SetActive(false);
            return lockObject;
        }

        private static Text CreateTaskSlotWithCount(Transform parent, string name)
        {
            var slot = new GameObject(name, typeof(RectTransform));
            slot.transform.SetParent(parent, false);

            var count = new GameObject("Count", typeof(RectTransform), typeof(Text));
            count.transform.SetParent(slot.transform, false);
            CreateTaskText(slot.transform, "TaskTitle");
            CreateTaskText(slot.transform, "TaskReward");

            var slider = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            slider.transform.SetParent(slot.transform, false);

            return count.GetComponent<Text>();
        }

        private static TextMeshProUGUI CreateTaskText(Transform parent, string name)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            return textObject.GetComponent<TextMeshProUGUI>();
        }

        private static MainBindings CreateMainTaskBindings(Transform root)
        {
            var bindings = new MainBindings();
            for (int i = 0; i < MainBindings.TaskSlotCount; i++)
            {
                Transform slot = root.Find($"Task{i + 1}");
                if (slot == null)
                {
                    continue;
                }

                bindings.TaskSlotRoots[i] = slot as RectTransform;
                bindings.TaskSlotButtons[i] = slot.GetComponent<Button>();
                bindings.TaskSlotBackgroundImages[i] = slot.GetComponent<Image>();
                bindings.TaskProgressTexts[i] = slot.Find("Count")?.GetComponent<Text>();
                bindings.TaskProgressSliders[i] = slot.Find("Slider")?.GetComponent<Slider>();
                bindings.TaskRewardIcons[i] = slot.Find("RewardIcon")?.GetComponent<Image>();
                bindings.TaskCatIcons[i] = slot.Find("CatIcon")?.GetComponent<Image>();
                bindings.TaskLocks[i] = slot.Find("lock") as RectTransform;
                bindings.TaskTitleTexts[i] = slot.Find("TaskTitle")?.GetComponent<TextMeshProUGUI>();
                bindings.TaskRewardTexts[i] = slot.Find("TaskReward")?.GetComponent<TextMeshProUGUI>();
            }

            return bindings;
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
