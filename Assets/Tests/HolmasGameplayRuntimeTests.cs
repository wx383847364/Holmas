using System.Collections.Generic;
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
using App.Shared.Contracts;
using App.Shared.Holmas.RuntimeData;
using NUnit.Framework;
using UnityEngine;
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
            public void RegisterSingleton<T>(T instance) where T : class
            {
            }

            public T Get<T>() where T : class
            {
                return null;
            }

            public bool IsRegistered<T>()
            {
                return false;
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
    }
}
