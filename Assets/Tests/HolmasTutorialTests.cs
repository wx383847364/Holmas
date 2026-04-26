using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using App.HotUpdate.Holmas.Application;
using App.HotUpdate.Holmas.Meta;
using App.HotUpdate.Holmas.Progression;
using App.HotUpdate.Holmas.Tutorial;
using App.HotUpdate.Holmas.Tasks.Services;
using App.HotUpdate.Holmas.Tasks.Runtime;
using App.HotUpdate.Holmas.UI.Screens.Main;
using App.HotUpdate.Holmas.UI.Screens.Tutorial;
using App.Shared.Contracts;
using App.Shared.Holmas.RuntimeData;
using NUnit.Framework;
using UnityEditor;

namespace Holmas.Tests
{
    public sealed class HolmasTutorialTests
    {
        [Test]
        public void CoreFindCatTutorialLevelService_CreateTutorialSnapshot_UsesFixedValidCellsAndTaskCats()
        {
            BoardTemplate template = HolmasTestSupport.CreateBoardTemplate(8, 8);

            LevelSnapshot snapshot = CoreFindCatTutorialLevelService.CreateTutorialSnapshot(
                template,
                new[] { "cat-a", "cat-b" });

            Assert.That(snapshot.MapId, Is.EqualTo(CoreFindCatTutorialBoardDefinition.MapId));
            Assert.That(snapshot.TerrainPath, Is.EqualTo(CoreFindCatTutorialBoardDefinition.TerrainPath));
            Assert.That(snapshot.SpawnedCats, Has.Count.EqualTo(2));
            Assert.That(snapshot.SpawnedCats[0].CellIndex, Is.EqualTo(CoreFindCatTutorialBoardDefinition.FirstCatCellIndex));
            Assert.That(snapshot.SpawnedCats[0].CatId, Is.EqualTo("cat-a"));
            Assert.That(snapshot.SpawnedCats[1].CellIndex, Is.EqualTo(CoreFindCatTutorialBoardDefinition.SecondCatCellIndex));
            Assert.That(snapshot.SpawnedCats[1].CatId, Is.EqualTo("cat-b"));
            Assert.That(snapshot.RevealedCells, Has.Length.EqualTo(64));
            Assert.That(snapshot.Completed, Is.False);
        }

        [Test]
        public void CoreFindCatTutorialLevelService_CreateTutorialSnapshot_ReusesOnlyAvailableCat()
        {
            BoardTemplate template = HolmasTestSupport.CreateBoardTemplate(8, 8);

            LevelSnapshot snapshot = CoreFindCatTutorialLevelService.CreateTutorialSnapshot(
                template,
                new[] { "cat-a" });

            Assert.That(snapshot.SpawnedCats[0].CatId, Is.EqualTo("cat-a"));
            Assert.That(snapshot.SpawnedCats[1].CatId, Is.EqualTo("cat-a"));
        }

        [Test]
        public void CoreFindCatTutorialLevelService_ResolveTutorialCatIds_UsesUnlockedSlotOrder()
        {
            var taskBar = new HolmasTaskBarState();
            taskBar.BindTask(1, CreateTask("task-b", "cat-b", slotIndex: 1));
            taskBar.BindTask(0, CreateTask("task-a", "cat-a", slotIndex: 0));

            IReadOnlyList<string> catIds = CoreFindCatTutorialLevelService.ResolveTutorialCatIds(taskBar);

            Assert.That(catIds, Is.EqualTo(new[] { "cat-a", "cat-b" }));
        }

        [Test]
        public void CoreFindCatTutorialLevelService_ResolveTutorialCatIds_UsesOnlyUncompletedUnclaimedTasks()
        {
            var taskBar = new HolmasTaskBarState();
            taskBar.BindTask(0, CreateTask("task-completed", "cat-done", slotIndex: 0, currentCount: 1, targetCount: 1));
            HolmasTaskRuntimeInstance claimedTask = CreateTask("task-claimed", "cat-claimed", slotIndex: 1, currentCount: 1, targetCount: 1);
            claimedTask.ClaimReward();
            taskBar.BindTask(1, claimedTask);
            taskBar.BindTask(2, CreateTask("task-active", "cat-active", slotIndex: 2));

            taskBar.UnlockSlot(2, 1000L);
            IReadOnlyList<string> catIds = CoreFindCatTutorialLevelService.ResolveTutorialCatIds(taskBar);

            Assert.That(catIds, Is.EqualTo(new[] { "cat-active" }));
        }

        [Test]
        public void CoreFindCatTutorialProgressStore_BrokenDataLoadsAsIncomplete()
        {
            var persistence = new InMemoryPersistence();
            persistence.SaveAsync(CoreFindCatTutorialProgressStore.PersistenceKey, Encoding.UTF8.GetBytes("{bad json"))
                .GetAwaiter()
                .GetResult();
            var store = new CoreFindCatTutorialProgressStore(persistence);

            CoreFindCatTutorialProgress progress = store.LoadAsync().GetAwaiter().GetResult();

            Assert.That(progress.completed, Is.False);
            Assert.That(progress.lastStepId, Is.Empty);
            Assert.That(progress.currentStepIndex, Is.EqualTo(-1));
        }

        [Test]
        public void CoreFindCatTutorialProgressStore_SaveCompletedRoundTrips()
        {
            var persistence = new InMemoryPersistence();
            var store = new CoreFindCatTutorialProgressStore(persistence);

            store.SaveCompletedAsync(123L, "help").GetAwaiter().GetResult();
            CoreFindCatTutorialProgress progress = store.LoadAsync().GetAwaiter().GetResult();

            Assert.That(progress.completed, Is.True);
            Assert.That(progress.completedAtUtcMilliseconds, Is.EqualTo(123L));
            Assert.That(progress.lastStepId, Is.EqualTo("help"));
        }

        [Test]
        public void CoreFindCatTutorialProgressService_CompletedCannotBeDowngradedByLaterStepSave()
        {
            var persistence = new InMemoryPersistence();
            var store = new CoreFindCatTutorialProgressStore(persistence);
            var service = new CoreFindCatTutorialProgressService(store);

            service.MarkCompletedAsync(CoreFindCatTutorialSteps.LastIndex, CoreFindCatTutorialSteps.HelpStepId, 200L)
                .GetAwaiter()
                .GetResult();
            service.MarkCurrentStepAsync(0, CoreFindCatTutorialSteps.FindFirstCatStepId)
                .GetAwaiter()
                .GetResult();

            CoreFindCatTutorialProgress progress = service.LoadAsync().GetAwaiter().GetResult();
            Assert.That(progress.completed, Is.True);
            Assert.That(progress.completedStepIndex, Is.EqualTo(CoreFindCatTutorialSteps.LastIndex));
            Assert.That(progress.currentStepIndex, Is.EqualTo(CoreFindCatTutorialSteps.LastIndex));
            Assert.That(progress.currentStepId, Is.EqualTo(CoreFindCatTutorialSteps.HelpStepId));
        }

        [Test]
        public void CoreFindCatTutorialProgressStore_SaveAsync_DoesNotDowngradeExistingCompletedProgress()
        {
            var persistence = new InMemoryPersistence();
            var store = new CoreFindCatTutorialProgressStore(persistence);

            store.SaveCompletedAsync(200L, CoreFindCatTutorialSteps.HelpStepId)
                .GetAwaiter()
                .GetResult();
            store.SaveAsync(new CoreFindCatTutorialProgress
            {
                started = true,
                completed = false,
                currentStepIndex = 0,
                currentStepId = CoreFindCatTutorialSteps.FindFirstCatStepId,
            }).GetAwaiter().GetResult();

            CoreFindCatTutorialProgress progress = store.LoadAsync().GetAwaiter().GetResult();
            Assert.That(progress.completed, Is.True);
            Assert.That(progress.currentStepId, Is.EqualTo(CoreFindCatTutorialSteps.HelpStepId));
            Assert.That(progress.completedAtUtcMilliseconds, Is.EqualTo(200L));
        }

        [Test]
        public void CoreFindCatTutorialProgressService_StepIndexOnlyMovesForwardUnlessForced()
        {
            var persistence = new InMemoryPersistence();
            var store = new CoreFindCatTutorialProgressStore(persistence);
            var service = new CoreFindCatTutorialProgressService(store);

            service.MarkStartedAsync(4, CoreFindCatTutorialSteps.EnergyStepId, force: false)
                .GetAwaiter()
                .GetResult();
            service.MarkCurrentStepAsync(1, CoreFindCatTutorialSteps.TaskBarStepId)
                .GetAwaiter()
                .GetResult();
            service.MarkStartedAsync(1, CoreFindCatTutorialSteps.TaskBarStepId, force: true)
                .GetAwaiter()
                .GetResult();

            CoreFindCatTutorialProgress progress = service.LoadAsync().GetAwaiter().GetResult();
            Assert.That(progress.completed, Is.False);
            Assert.That(progress.currentStepIndex, Is.EqualTo(1));
            Assert.That(progress.currentStepId, Is.EqualTo(CoreFindCatTutorialSteps.TaskBarStepId));
        }

        [Test]
        public void CoreFindCatTutorialProgressService_NormalBoardHintDismissDoesNotCompleteTutorial()
        {
            var persistence = new InMemoryPersistence();
            var store = new CoreFindCatTutorialProgressStore(persistence);
            var service = new CoreFindCatTutorialProgressService(store);

            service.MarkNormalBoardHintDismissedAsync().GetAwaiter().GetResult();

            CoreFindCatTutorialProgress progress = service.LoadAsync().GetAwaiter().GetResult();
            Assert.That(progress.dismissedNormalBoardHint, Is.True);
            Assert.That(progress.completed, Is.False);
            Assert.That(progress.skipped, Is.False);
        }

        [Test]
        public void CoreFindCatTutorialCoordinator_PrepareAutoStart_ResumesPostTutorialBoardStepsOnNormalBoard()
        {
            var persistence = new InMemoryPersistence();
            var store = new CoreFindCatTutorialProgressStore(persistence);
            var service = new CoreFindCatTutorialProgressService(store);
            service.MarkCurrentStepAsync(
                    CoreFindCatTutorialSteps.IndexOf(CoreFindCatTutorialSteps.EnergyStepId),
                    CoreFindCatTutorialSteps.EnergyStepId)
                .GetAwaiter()
                .GetResult();
            HolmasGameplayRuntime runtime = CreateRuntimeWithNormalBoard();
            var context = new HolmasApplicationContext(
                null,
                new NullLogger(),
                null,
                null,
                null,
                runtime);
            var coordinator = new CoreFindCatTutorialCoordinator(service, new CoreFindCatTutorialLevelService());

            CoreFindCatTutorialLaunchResult result = coordinator.PrepareAutoStartAsync(context).GetAwaiter().GetResult();

            Assert.That(result.ShouldShowOverlay, Is.True);
            Assert.That(result.ShouldAutoStartNormal, Is.False);
            Assert.That(result.Payload.RunMode, Is.EqualTo(TutorialRunMode.FullTutorial));
            Assert.That(result.Payload.CanWriteCompletion, Is.True);
            Assert.That(result.Payload.InitialStepIndex, Is.EqualTo(CoreFindCatTutorialSteps.IndexOf(CoreFindCatTutorialSteps.EnergyStepId)));
            Assert.That(result.Payload.TutorialBoardObjectiveSatisfied, Is.True);
        }

        [Test]
        public void CoreFindCatTutorialCoordinator_PrepareAutoStart_ForcesTutorialBoardOverActiveNormalBoard()
        {
            var persistence = new InMemoryPersistence();
            var store = new CoreFindCatTutorialProgressStore(persistence);
            var service = new CoreFindCatTutorialProgressService(store);
            HolmasGameplayRuntime runtime = CreateRuntimeWithNormalBoard();
            var context = CreateContext(runtime, new FakeAssetsRuntime(HolmasTestSupport.CreateTerrain(11, 8)));
            var coordinator = new CoreFindCatTutorialCoordinator(service, new CoreFindCatTutorialLevelService());

            CoreFindCatTutorialLaunchResult result = coordinator.PrepareAutoStartAsync(context).GetAwaiter().GetResult();

            Assert.That(result.ShouldShowOverlay, Is.True);
            Assert.That(result.ShouldAutoStartNormal, Is.False);
            Assert.That(result.Payload.RunMode, Is.EqualTo(TutorialRunMode.FullTutorial));
            Assert.That(result.Payload.CanWriteCompletion, Is.True);
            Assert.That(result.Payload.InitialStepIndex, Is.EqualTo(0));
            Assert.That(runtime.CurrentLevelSnapshot.MapId, Is.EqualTo(CoreFindCatTutorialBoardDefinition.MapId));
            Assert.That(new MainPresenter(context).Build().UseTutorialBoardLayer, Is.True);
        }

        [Test]
        public void CoreFindCatTutorialCoordinator_PrepareManualStart_StartsTutorialBoardForEarlySteps()
        {
            var persistence = new InMemoryPersistence();
            var store = new CoreFindCatTutorialProgressStore(persistence);
            var service = new CoreFindCatTutorialProgressService(store);
            HolmasGameplayRuntime runtime = CreateRuntimeWithNormalBoard();
            var context = CreateContext(runtime, new FakeAssetsRuntime(HolmasTestSupport.CreateTerrain(11, 8)));
            var coordinator = new CoreFindCatTutorialCoordinator(service, new CoreFindCatTutorialLevelService());
            int stepIndex = CoreFindCatTutorialSteps.IndexOf(CoreFindCatTutorialSteps.TaskBarStepId);

            CoreFindCatTutorialLaunchResult result = coordinator.PrepareManualStartAsync(context, stepIndex, debugForceStep: true)
                .GetAwaiter()
                .GetResult();

            Assert.That(result.ShouldShowOverlay, Is.True);
            Assert.That(result.Payload.RunMode, Is.EqualTo(TutorialRunMode.DebugStartAtStep));
            Assert.That(result.Payload.InitialStepIndex, Is.EqualTo(stepIndex));
            Assert.That(runtime.CurrentLevelSnapshot.MapId, Is.EqualTo(CoreFindCatTutorialBoardDefinition.MapId));
        }

        [Test]
        public void TutorialVisualConfig_FindReturnsConfiguredStep()
        {
            var config = UnityEngine.ScriptableObject.CreateInstance<TutorialVisualConfig>();
            try
            {
                config.ReplaceSteps(new[]
                {
                    new TutorialStepVisualDefinition
                    {
                        StepId = CoreFindCatTutorialSteps.FindFirstCatStepId,
                        MainImagePath = TutorialVisualConfig.PlaceholderSpritePath,
                    },
                });

                TutorialStepVisualDefinition visual = config.Find(CoreFindCatTutorialSteps.FindFirstCatStepId);

                Assert.That(visual, Is.Not.Null);
                Assert.That(visual.MainImagePath, Is.EqualTo(TutorialVisualConfig.PlaceholderSpritePath));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void TutorialVisualConfig_AssetCoversEveryTutorialStep()
        {
            TutorialVisualConfig config =
                AssetDatabase.LoadAssetAtPath<TutorialVisualConfig>(TutorialVisualConfig.DefaultAssetPath);

            Assert.That(config, Is.Not.Null);
            foreach (TutorialStepDefinition step in CoreFindCatTutorialSteps.All)
            {
                TutorialStepVisualDefinition visual = config.Find(step.StepId);
                Assert.That(visual, Is.Not.Null, $"Missing tutorial visual entry for {step.StepId}.");
                Assert.That(visual.MainImagePath, Does.StartWith("Assets/HotUpdateContent/Res/"));
                Assert.That(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(visual.MainImagePath), Is.Not.Null);
            }
        }

        private static HolmasTaskRuntimeInstance CreateTask(
            string taskId,
            string catId,
            int slotIndex,
            int currentCount = 0,
            int targetCount = 1)
        {
            return new HolmasTaskRuntimeInstance(new TaskInstanceData
            {
                TaskInstanceId = taskId,
                SourceTaskTypeId = "task-normal",
                CatId = catId,
                SlotIndex = slotIndex,
                TargetCount = targetCount,
                CurrentCount = currentCount,
                Reward = 10,
            });
        }

        private static HolmasGameplayRuntime CreateRuntimeWithNormalBoard()
        {
            var catalog = HolmasTestSupport.CreateStandardTaskCatalog();
            var clock = new FixedUtcClock { UtcNowMilliseconds = 1000 };
            var taskService = new HolmasTaskProgressService(catalog, new ScriptedRandomSource(), clock);
            var metaService = new HolmasMetaProgressionService(
                HolmasTestSupport.CreateMetaCatalog(),
                catalog,
                new HolmasDefaultMetaExperienceSource(),
                new HolmasDefaultMetaExperienceSource(),
                clock);
            var progressionCoordinator = new HolmasProgressionCoordinator(taskService, metaService);
            var runtime = new HolmasGameplayRuntime(
                taskService,
                metaService,
                progressionCoordinator,
                new NullLogger(),
                null);
            runtime.StartLevel(
                HolmasTestSupport.CreateBoardTemplate(1, 1),
                new LevelSnapshot
                {
                    MapId = "normal-map",
                    TerrainPath = "normal-terrain",
                    Seed = 1,
                    RevealedCells = new bool[1],
                    Completed = false,
                    SpawnedCats = new List<SpawnedCatData>
                    {
                        new SpawnedCatData
                        {
                            CatId = "cat-a",
                            CellIndex = 0,
                        },
                    },
                });
            return runtime;
        }

        private static HolmasApplicationContext CreateContext(
            HolmasGameplayRuntime runtime,
            IAssetsRuntime assetsRuntime)
        {
            return new HolmasApplicationContext(
                null,
                new NullLogger(),
                null,
                null,
                assetsRuntime,
                runtime);
        }

        private sealed class InMemoryPersistence : IPersistence
        {
            private readonly Dictionary<string, byte[]> _data = new Dictionary<string, byte[]>();

            public Task<bool> SaveAsync(string key, byte[] data)
            {
                _data[key] = data;
                return Task.FromResult(true);
            }

            public Task<byte[]> LoadAsync(string key)
            {
                _data.TryGetValue(key, out byte[] data);
                return Task.FromResult(data);
            }

            public Task<bool> DeleteAsync(string key)
            {
                _data.Remove(key);
                return Task.FromResult(true);
            }

            public bool Exists(string key)
            {
                return _data.ContainsKey(key);
            }
        }

        private sealed class FakeAssetsRuntime : IAssetsRuntime
        {
            private readonly UnityEngine.Object _asset;

            public FakeAssetsRuntime(UnityEngine.Object asset)
            {
                _asset = asset;
            }

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
                return Task.FromResult<IAssetHandle>(new FakeAssetHandle(_asset));
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

            public void Release()
            {
            }
        }
    }
}
