using System.Collections.Generic;
using System.Threading.Tasks;
using App.HotUpdate.Holmas.Application;
using App.HotUpdate.Holmas.Board;
using App.HotUpdate.Holmas.Levels;
using App.HotUpdate.Holmas.Meta;
using App.HotUpdate.Holmas.Progression;
using App.HotUpdate.Holmas.Tasks.Runtime;
using App.HotUpdate.Holmas.Tasks.Services;
using App.HotUpdate.Holmas.Terrain;
using App.Shared.Contracts;
using App.Shared.Holmas.RuntimeData;
using NUnit.Framework;
using UnityEngine;

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
                new HolmasDefaultMetaExperienceSource(),
                new HolmasDefaultMetaExperienceSource());
            var coordinator = new HolmasProgressionCoordinator(taskService, metaService);
            var runtime = new HolmasGameplayRuntime(taskService, metaService, coordinator, new NullLogger());

            runtime.RefillAvailableTasks(1);

            var terrain = HolmasTestSupport.CreateTerrain(1, 1);
            var request = HolmasTestSupport.CreateRequest(
                "map-1",
                "terrain://map-1",
                1,
                1,
                1,
                new BoardSpawnEntry { CatId = "cat-a", Weight = 1 });

            BoardRuntime boardRuntime = runtime.StartLevel(terrain, request);
            Assert.That(boardRuntime.TotalCatCount, Is.EqualTo(1));

            var reveal = runtime.RevealCell(0, out HolmasProgressionAdvanceResult progressionResult);

            Assert.That(reveal.IsValidAction, Is.True);
            Assert.That(reveal.Completed, Is.True);
            Assert.That(progressionResult, Is.Not.Null);
            Assert.That(progressionResult.ProgressedTaskIds, Has.Count.EqualTo(1));
            Assert.That(progressionResult.CompletedTaskIds, Has.Count.EqualTo(1));
            Assert.That(runtime.TaskBarState.GetTaskBySlot(0).Task.CurrentCount, Is.EqualTo(1));
            Assert.That(runtime.MetaProgressionState.Experience, Is.EqualTo(6));

            var claim = runtime.ClaimTaskReward(0, 1);

            Assert.That(claim.Success, Is.True);
            Assert.That(runtime.MetaProgressionState.Experience, Is.EqualTo(8));
            Assert.That(runtime.MetaProgressionState.AgencyLevel, Is.EqualTo(2));
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
                new HolmasDefaultMetaExperienceSource(),
                new HolmasDefaultMetaExperienceSource());
            var coordinator = new HolmasProgressionCoordinator(taskService, metaService);
            var runtime = new HolmasGameplayRuntime(taskService, metaService, coordinator, new NullLogger());

            runtime.RefillAvailableTasks(1);

            var terrain = HolmasTestSupport.CreateTerrain(1, 2);
            var request = HolmasTestSupport.CreateRequest(
                "map-2",
                "terrain://map-2",
                1,
                1,
                1,
                new BoardSpawnEntry { CatId = "cat-a", Weight = 1 });

            runtime.StartLevel(terrain, request);

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
                new HolmasDefaultMetaExperienceSource(),
                new HolmasDefaultMetaExperienceSource());
            var coordinator = new HolmasProgressionCoordinator(taskService, metaService);
            var runtime = new HolmasGameplayRuntime(taskService, metaService, coordinator, new NullLogger());

            runtime.RefillAvailableTasks(1);

            var terrain = HolmasTestSupport.CreateTerrain(1, 1);
            var request = HolmasTestSupport.CreateRequest(
                "map-repeat",
                "terrain://map-repeat",
                1,
                1,
                1,
                new BoardSpawnEntry { CatId = "cat-a", Weight = 1 });

            runtime.StartLevel(terrain, request);
            var reveal = runtime.RevealCell(0, out HolmasProgressionAdvanceResult firstResult);

            Assert.That(reveal.Completed, Is.True);
            Assert.That(firstResult, Is.Not.Null);
            Assert.That(runtime.MetaProgressionState.Experience, Is.EqualTo(6));

            HolmasProgressionAdvanceResult secondResult = runtime.ApplyCurrentLevelCompletion();

            Assert.That(secondResult.ProgressedTaskIds, Is.Empty);
            Assert.That(secondResult.CompletedTaskIds, Is.Empty);
            Assert.That(secondResult.MetaExperienceGained, Is.EqualTo(0));
            Assert.That(runtime.MetaProgressionState.Experience, Is.EqualTo(6));
            Assert.That(runtime.TaskBarState.GetTaskBySlot(0).Task.CurrentCount, Is.EqualTo(1));
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
                HolmasTerrainAssetPathUtility.BuildAssetPath("1"),
                1,
                1,
                1,
                new BoardSpawnEntry { CatId = "cat-a", Weight = 1 });

            BoardRuntime boardRuntime = context.StartLevelAsync(request).GetAwaiter().GetResult();

            Assert.That(assetsRuntime.LastRequestedLocation, Is.EqualTo(HolmasTerrainAssetPathUtility.BuildAssetPath("1")));
            Assert.That(assetsRuntime.LastHandle.ReleaseCount, Is.EqualTo(1));
            Assert.That(boardRuntime.TotalCatCount, Is.EqualTo(1));
            Assert.That(runtime.CurrentLevelSnapshot.TerrainPath, Is.EqualTo(HolmasTerrainAssetPathUtility.BuildAssetPath("1")));
            Assert.That(runtime.CurrentLevelSnapshot.MapId, Is.EqualTo("map-async"));
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
                new HolmasDefaultMetaExperienceSource(),
                new HolmasDefaultMetaExperienceSource());
            var coordinator = new HolmasProgressionCoordinator(taskService, metaService);
            var runtime = new HolmasGameplayRuntime(taskService, metaService, coordinator, new NullLogger());
            var request = HolmasTestSupport.CreateRequest(
                "map-missing-loader",
                HolmasTerrainAssetPathUtility.BuildAssetPath("1"),
                1,
                1,
                1,
                new BoardSpawnEntry { CatId = "cat-a", Weight = 1 });

            var ex = Assert.Throws<System.InvalidOperationException>(() => runtime.StartLevelAsync(request).GetAwaiter().GetResult());
            Assert.That(ex.Message, Does.Contain("IAssetsRuntime"));
        }

        private sealed class FakeAssetsRuntime : IAssetsRuntime
        {
            private readonly UnityEngine.Object _asset;

            public FakeAssetsRuntime(UnityEngine.Object asset)
            {
                _asset = asset;
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
    }
}
