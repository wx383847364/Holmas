using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using App.HotUpdate.Holmas.Application;
using App.HotUpdate.Holmas.Board;
using App.HotUpdate.Holmas.Meta;
using App.HotUpdate.Holmas.PlayerData;
using App.HotUpdate.Holmas.Progression;
using App.HotUpdate.Holmas.Tutorial;
using App.HotUpdate.Holmas.Tasks.Services;
using App.HotUpdate.Holmas.UI.Screens.Main;
using App.HotUpdate.Holmas.UI.Screens.Tutorial;
using App.Shared.Contracts;
using App.Shared.Holmas.RuntimeData;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.TestTools;

namespace Holmas.Tests
{
    public sealed class HolmasTutorialTests
    {
        [Test]
        public void CoreFindCatTutorialLevelService_CreateTutorialSnapshot_UsesFixedValidCellsAndDemoCats()
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
        public void CoreFindCatTutorialSession_RevealOnlyMutatesTutorialSnapshot()
        {
            HolmasGameplayRuntime runtime = CreateRuntimeWithNormalBoard();
            BoardTemplate tutorialTemplate = HolmasTestSupport.CreateBoardTemplate(8, 8);
            LevelSnapshot tutorialSnapshot = CoreFindCatTutorialLevelService.CreateTutorialSnapshot(
                tutorialTemplate,
                new[] { "tutorial-cat-a", "tutorial-cat-b" });
            var session = new CoreFindCatTutorialSession(tutorialTemplate, tutorialSnapshot);

            session.RevealCell(CoreFindCatTutorialBoardDefinition.FirstCatCellIndex);

            Assert.That(session.Snapshot.RevealedCells[CoreFindCatTutorialBoardDefinition.FirstCatCellIndex], Is.True);
            Assert.That(runtime.CurrentLevelSnapshot.MapId, Is.EqualTo("normal-map"));
            Assert.That(runtime.CurrentLevelSnapshot.RevealedCells[0], Is.False);
            Assert.That(runtime.CurrentLevelSnapshot.Completed, Is.False);
        }

        [UnityTest]
        public IEnumerator CoreFindCatTutorialSessionService_RevealNotifiesAndPresenterUsesInjectedSession()
        {
            return RunAsync(async () =>
            {
                HolmasGameplayRuntime runtime = CreateRuntimeWithNormalBoard();
                var sessionService = new CoreFindCatTutorialSessionService(new NullLogger());
                await sessionService.StartSessionAsync(new FakeAssetsRuntime(HolmasTestSupport.CreateTerrain(11, 8)));
                var presenter = new MainPresenter(CreateContext(runtime, null), sessionService);
                int changeCount = 0;
                sessionService.StateChanged += () => changeCount++;

                MainVm vm = presenter.Build();
                BoardRevealResult reveal = sessionService.RevealCell(CoreFindCatTutorialBoardDefinition.FirstCatCellIndex);

                Assert.That(vm.UseTutorialBoardLayer, Is.True);
                Assert.That(vm.Rows, Is.EqualTo(8));
                Assert.That(vm.Cols, Is.EqualTo(8));
                Assert.That(vm.PromotionButtonEnabled, Is.False);
                Assert.That(vm.AddEnergyButtonEnabled, Is.False);
                Assert.That(vm.TaskItems, Is.Not.Null);
                Assert.That(vm.TaskItems, Is.All.Matches<MainTaskItemVm>(item => item == null || !item.ButtonEnabled));
                Assert.That(reveal.IsValidAction, Is.True);
                Assert.That(changeCount, Is.EqualTo(1));
                Assert.That(sessionService.ActiveSession.IsTutorialCatRevealed(CoreFindCatTutorialBoardDefinition.FirstCatCellIndex), Is.True);
                Assert.That(runtime.CurrentLevelSnapshot.RevealedCells[0], Is.False);
            });
        }

        [Test]
        public void HolmasPlayerArchiveMapper_CreateTutorialSuspendedSession_ClonesFormalBoard()
        {
            HolmasGameplayRuntime runtime = CreateRuntimeWithNormalBoard();
            var mapper = new HolmasPlayerArchiveMapper();

            var suspended = mapper.CreateTutorialSuspendedSession(
                runtime,
                "schema-test",
                "reason-test",
                "source-test",
                123L);

            Assert.That(suspended, Is.Not.Null);
            Assert.That(suspended.CurrentLevel, Is.Not.SameAs(runtime.CurrentLevelSnapshot));
            Assert.That(suspended.CurrentLevel.MapId, Is.EqualTo("normal-map"));
            Assert.That(suspended.CurrentLevel.RevealedCells, Is.Not.SameAs(runtime.CurrentLevelSnapshot.RevealedCells));
            Assert.That(suspended.TaskBar, Is.Not.Null);
            Assert.That(suspended.SchemaVersion, Is.EqualTo("schema-test"));
            Assert.That(suspended.CreatedAtUtcMilliseconds, Is.EqualTo(123L));
        }

        [UnityTest]
        public IEnumerator CoreFindCatTutorialProgressStore_BrokenDataLoadsAsIncomplete()
        {
            return RunAsync(async () =>
            {
                var persistence = new InMemoryPersistence();
                await persistence.SaveAsync(CoreFindCatTutorialProgressStore.PersistenceKey, Encoding.UTF8.GetBytes("{bad json"));
                var store = new CoreFindCatTutorialProgressStore(persistence);

                CoreFindCatTutorialProgress progress = await store.LoadAsync();

                Assert.That(progress.completed, Is.False);
                Assert.That(progress.lastStepId, Is.Empty);
                Assert.That(progress.currentStepIndex, Is.EqualTo(-1));
            });
        }

        [UnityTest]
        public IEnumerator CoreFindCatTutorialProgressStore_SaveCompletedRoundTrips()
        {
            return RunAsync(async () =>
            {
                var persistence = new InMemoryPersistence();
                var store = new CoreFindCatTutorialProgressStore(persistence);

                await store.SaveCompletedAsync(123L, "help");
                CoreFindCatTutorialProgress progress = await store.LoadAsync();

                Assert.That(progress.completed, Is.True);
                Assert.That(progress.completedAtUtcMilliseconds, Is.EqualTo(123L));
                Assert.That(progress.lastStepId, Is.EqualTo("help"));
            });
        }

        [UnityTest]
        public IEnumerator CoreFindCatTutorialProgressService_CompletedCannotBeDowngradedByLaterStepSave()
        {
            return RunAsync(async () =>
            {
                var persistence = new InMemoryPersistence();
                var store = new CoreFindCatTutorialProgressStore(persistence);
                var service = new CoreFindCatTutorialProgressService(store);

                await service.MarkCompletedAsync(CoreFindCatTutorialSteps.LastIndex, CoreFindCatTutorialSteps.HelpStepId, 200L);
                await service.MarkCurrentStepAsync(0, CoreFindCatTutorialSteps.FindFirstCatStepId);

                CoreFindCatTutorialProgress progress = await service.LoadAsync();
                Assert.That(progress.completed, Is.True);
                Assert.That(progress.completedStepIndex, Is.EqualTo(CoreFindCatTutorialSteps.LastIndex));
                Assert.That(progress.currentStepIndex, Is.EqualTo(CoreFindCatTutorialSteps.LastIndex));
                Assert.That(progress.currentStepId, Is.EqualTo(CoreFindCatTutorialSteps.HelpStepId));
            });
        }

        [UnityTest]
        public IEnumerator CoreFindCatTutorialProgressStore_SaveAsync_DoesNotDowngradeExistingCompletedProgress()
        {
            return RunAsync(async () =>
            {
                var persistence = new InMemoryPersistence();
                var store = new CoreFindCatTutorialProgressStore(persistence);

                await store.SaveCompletedAsync(200L, CoreFindCatTutorialSteps.HelpStepId);
                await store.SaveAsync(new CoreFindCatTutorialProgress
                {
                    started = true,
                    completed = false,
                    currentStepIndex = 0,
                    currentStepId = CoreFindCatTutorialSteps.FindFirstCatStepId,
                });

                CoreFindCatTutorialProgress progress = await store.LoadAsync();
                Assert.That(progress.completed, Is.True);
                Assert.That(progress.currentStepId, Is.EqualTo(CoreFindCatTutorialSteps.HelpStepId));
                Assert.That(progress.completedAtUtcMilliseconds, Is.EqualTo(200L));
            });
        }

        [UnityTest]
        public IEnumerator CoreFindCatTutorialProgressService_StepIndexOnlyMovesForwardUnlessForced()
        {
            return RunAsync(async () =>
            {
                var persistence = new InMemoryPersistence();
                var store = new CoreFindCatTutorialProgressStore(persistence);
                var service = new CoreFindCatTutorialProgressService(store);

                await service.MarkStartedAsync(4, CoreFindCatTutorialSteps.EnergyStepId, force: false);
                await service.MarkCurrentStepAsync(1, CoreFindCatTutorialSteps.TaskBarStepId);
                await service.MarkStartedAsync(1, CoreFindCatTutorialSteps.TaskBarStepId, force: true);

                CoreFindCatTutorialProgress progress = await service.LoadAsync();
                Assert.That(progress.completed, Is.False);
                Assert.That(progress.currentStepIndex, Is.EqualTo(1));
                Assert.That(progress.currentStepId, Is.EqualTo(CoreFindCatTutorialSteps.TaskBarStepId));
            });
        }

        [UnityTest]
        public IEnumerator CoreFindCatTutorialProgressService_NormalBoardHintDismissDoesNotCompleteTutorial()
        {
            return RunAsync(async () =>
            {
                var persistence = new InMemoryPersistence();
                var store = new CoreFindCatTutorialProgressStore(persistence);
                var service = new CoreFindCatTutorialProgressService(store);

                await service.MarkNormalBoardHintDismissedAsync();

                CoreFindCatTutorialProgress progress = await service.LoadAsync();
                Assert.That(progress.dismissedNormalBoardHint, Is.True);
                Assert.That(progress.completed, Is.False);
                Assert.That(progress.skipped, Is.False);
            });
        }

        [UnityTest]
        public IEnumerator CoreFindCatTutorialCoordinator_PrepareAutoStart_ResumesPostTutorialBoardStepsOnNormalBoard()
        {
            return RunAsync(async () =>
            {
                var persistence = new InMemoryPersistence();
                var store = new CoreFindCatTutorialProgressStore(persistence);
                var service = new CoreFindCatTutorialProgressService(store);
                await service.MarkCurrentStepAsync(
                    CoreFindCatTutorialSteps.IndexOf(CoreFindCatTutorialSteps.EnergyStepId),
                    CoreFindCatTutorialSteps.EnergyStepId);
                HolmasGameplayRuntime runtime = CreateRuntimeWithNormalBoard();
                var context = new HolmasApplicationContext(
                    null,
                    new NullLogger(),
                    null,
                    null,
                    null,
                    runtime);
                var coordinator = new CoreFindCatTutorialCoordinator(service, new CoreFindCatTutorialLevelService());

                CoreFindCatTutorialLaunchResult result = await coordinator.PrepareAutoStartAsync(context);

                Assert.That(result.ShouldShowOverlay, Is.True);
                Assert.That(result.ShouldAutoStartNormal, Is.False);
                Assert.That(result.Payload.RunMode, Is.EqualTo(TutorialRunMode.FullTutorial));
                Assert.That(result.Payload.CanWriteCompletion, Is.True);
                Assert.That(result.Payload.InitialStepIndex, Is.EqualTo(CoreFindCatTutorialSteps.IndexOf(CoreFindCatTutorialSteps.EnergyStepId)));
                Assert.That(result.Payload.TutorialBoardObjectiveSatisfied, Is.True);
            });
        }

        [UnityTest]
        public IEnumerator CoreFindCatTutorialCoordinator_PrepareAutoStart_CompletedTutorialAutoStartsNormalBoard()
        {
            return RunAsync(async () =>
            {
                var persistence = new InMemoryPersistence();
                var store = new CoreFindCatTutorialProgressStore(persistence);
                var service = new CoreFindCatTutorialProgressService(store);
                await service.MarkCompletedAsync(CoreFindCatTutorialSteps.LastIndex, CoreFindCatTutorialSteps.HelpStepId, 300L);
                HolmasGameplayRuntime runtime = CreateRuntimeWithNormalBoard();
                var context = new HolmasApplicationContext(
                    null,
                    new NullLogger(),
                    null,
                    null,
                    null,
                    runtime);
                var coordinator = new CoreFindCatTutorialCoordinator(service, new CoreFindCatTutorialLevelService());

                CoreFindCatTutorialLaunchResult result = await coordinator.PrepareAutoStartAsync(context);

                Assert.That(result.ShouldAutoStartNormal, Is.True);
                Assert.That(result.ShouldShowOverlay, Is.False);
                Assert.That(result.Payload, Is.Null);
            });
        }

        [UnityTest]
        public IEnumerator CoreFindCatTutorialCoordinator_PrepareAutoStart_ForcesTutorialBoardOverActiveNormalBoard()
        {
            return RunAsync(async () =>
            {
                var persistence = new InMemoryPersistence();
                var store = new CoreFindCatTutorialProgressStore(persistence);
                var service = new CoreFindCatTutorialProgressService(store);
                HolmasGameplayRuntime runtime = CreateRuntimeWithNormalBoard();
                var context = CreateContext(runtime, new FakeAssetsRuntime(HolmasTestSupport.CreateTerrain(11, 8)));
                var sessionService = new CoreFindCatTutorialSessionService(new NullLogger());
                var coordinator = new CoreFindCatTutorialCoordinator(service, new CoreFindCatTutorialLevelService(), sessionService);
                string originalMapId = runtime.CurrentLevelSnapshot.MapId;

                CoreFindCatTutorialLaunchResult result = await coordinator.PrepareAutoStartAsync(context);

                Assert.That(result.ShouldShowOverlay, Is.True);
                Assert.That(result.ShouldAutoStartNormal, Is.False);
                Assert.That(result.Payload.RunMode, Is.EqualTo(TutorialRunMode.FullTutorial));
                Assert.That(result.Payload.CanWriteCompletion, Is.True);
                Assert.That(result.Payload.InitialStepIndex, Is.EqualTo(0));
                Assert.That(runtime.CurrentLevelSnapshot.MapId, Is.EqualTo(originalMapId));
                Assert.That(sessionService.ActiveSession, Is.Not.Null);
                Assert.That(sessionService.ActiveSession.Snapshot.MapId, Is.EqualTo(CoreFindCatTutorialBoardDefinition.MapId));
            });
        }

        [UnityTest]
        public IEnumerator CoreFindCatTutorialCoordinator_PrepareManualStart_StartsTutorialBoardForEarlySteps()
        {
            return RunAsync(async () =>
            {
                var persistence = new InMemoryPersistence();
                var store = new CoreFindCatTutorialProgressStore(persistence);
                var service = new CoreFindCatTutorialProgressService(store);
                HolmasGameplayRuntime runtime = CreateRuntimeWithNormalBoard();
                var context = CreateContext(runtime, new FakeAssetsRuntime(HolmasTestSupport.CreateTerrain(11, 8)));
                var sessionService = new CoreFindCatTutorialSessionService(new NullLogger());
                var coordinator = new CoreFindCatTutorialCoordinator(service, new CoreFindCatTutorialLevelService(), sessionService);
                int stepIndex = CoreFindCatTutorialSteps.IndexOf(CoreFindCatTutorialSteps.TaskBarStepId);
                string originalMapId = runtime.CurrentLevelSnapshot.MapId;

                CoreFindCatTutorialLaunchResult result =
                    await coordinator.PrepareManualStartAsync(context, stepIndex, debugForceStep: true);

                Assert.That(result.ShouldShowOverlay, Is.True);
                Assert.That(result.Payload.RunMode, Is.EqualTo(TutorialRunMode.DebugStartAtStep));
                Assert.That(result.Payload.InitialStepIndex, Is.EqualTo(stepIndex));
                Assert.That(runtime.CurrentLevelSnapshot.MapId, Is.EqualTo(originalMapId));
                Assert.That(sessionService.ActiveSession, Is.Not.Null);
            });
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

        private static IEnumerator RunAsync(Func<Task> action)
        {
            Task task = action();
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted)
            {
                throw task.Exception?.GetBaseException() ?? task.Exception;
            }

            if (task.IsCanceled)
            {
                throw new OperationCanceledException("Async Unity test was canceled.");
            }
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
