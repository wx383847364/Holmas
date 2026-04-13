using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using App.HotUpdate.Holmas.Application;
using App.HotUpdate.Holmas.UI.Core;
using App.HotUpdate.Holmas.Levels;
using App.HotUpdate.Holmas.Meta;
using App.HotUpdate.Holmas.PlayerData;
using App.HotUpdate.Holmas.Progression;
using App.HotUpdate.Holmas.Tasks.Config;
using App.HotUpdate.Holmas.Tasks.Services;
using App.Shared.Contracts;
using App.Shared.Holmas.PlayerData;
using App.Shared.Holmas.RuntimeData;
using NUnit.Framework;
using UnityEngine;
using TerrainAssetPathUtility = App.HotUpdate.Holmas.Terrain.HolmasTerrainAssetPathUtility;

namespace Holmas.Tests
{
    public sealed class HolmasPlayerArchiveTests
    {
        [Test]
        public void HolmasLocalMockServerGateway_SaveThenLoad_RoundTripsArchive()
        {
            var persistence = new InMemoryPersistence();
            var gateway = new HolmasLocalMockServerGateway(persistence, new NullLogger(), new FixedUtcClock { UtcNowMilliseconds = 5678 });
            var mapper = new HolmasPlayerArchiveMapper();
            HolmasPlayerArchiveRoot archive = mapper.CreateDefaultArchive();
            archive.Progression.GoldBalance = 42;
            archive.TaskBar.Tasks = new[]
            {
                new HolmasTaskRuntimeArchiveData
                {
                    Task = new TaskInstanceData
                    {
                        TaskInstanceId = "task-1",
                        SourceTaskTypeId = "task-normal",
                        TaskKind = "Money",
                        CatId = "cat-a",
                        TargetCount = 2,
                        CurrentCount = 1,
                        Reward = 20,
                        SlotIndex = 0,
                    }
                }
            };

            HolmasPlayerArchiveSaveResult saveResult = gateway.SaveAsync(archive).GetAwaiter().GetResult();
            HolmasPlayerArchiveLoadResult loadResult = gateway.LoadAsync().GetAwaiter().GetResult();

            Assert.That(saveResult.Success, Is.True, saveResult.FailureReason);
            Assert.That(loadResult.Success, Is.True, loadResult.FailureReason);
            Assert.That(loadResult.Archive.Progression.GoldBalance, Is.EqualTo(42));
            Assert.That(loadResult.Archive.SavedAtUtcMilliseconds, Is.EqualTo(5678));
            Assert.That(loadResult.Archive.TaskBar.Tasks, Has.Length.EqualTo(1));
            Assert.That(loadResult.Archive.TaskBar.Tasks[0].Task.TaskInstanceId, Is.EqualTo("task-1"));
        }

        [Test]
        public void HolmasLocalMockServerGateway_LoadAsync_UsesBackupWhenPrimaryIsInvalid()
        {
            var persistence = new InMemoryPersistence();
            var gateway = new HolmasLocalMockServerGateway(persistence, new NullLogger(), new FixedUtcClock { UtcNowMilliseconds = 1000 });
            HolmasPlayerArchiveRoot archive = new HolmasPlayerArchiveMapper().CreateDefaultArchive();

            HolmasPlayerArchiveSaveResult saveResult = gateway.SaveAsync(archive).GetAwaiter().GetResult();
            Assert.That(saveResult.Success, Is.True, saveResult.FailureReason);

            persistence.SetRaw("holmas/player_archive", System.Text.Encoding.UTF8.GetBytes("{broken json"));

            HolmasPlayerArchiveLoadResult loadResult = gateway.LoadAsync().GetAwaiter().GetResult();

            Assert.That(loadResult.Success, Is.True, loadResult.FailureReason);
            Assert.That(loadResult.Archive.SchemaVersion, Is.EqualTo(HolmasLocalMockServerGateway.DefaultSchemaVersion));
        }

        [Test]
        public void HolmasLocalMockServerGateway_LoadAsync_PrefersNewerBackupAndHealsPrimary()
        {
            var persistence = new SelectiveFailPersistence();
            var clock = new FixedUtcClock { UtcNowMilliseconds = 1000 };
            var gateway = new HolmasLocalMockServerGateway(persistence, new NullLogger(), clock);
            var mapper = new HolmasPlayerArchiveMapper();
            HolmasPlayerArchiveRoot archive = mapper.CreateDefaultArchive();
            archive.Revision = 1;
            archive.Progression.GoldBalance = 10;

            HolmasPlayerArchiveSaveResult firstSave = gateway.SaveAsync(archive).GetAwaiter().GetResult();
            Assert.That(firstSave.Success, Is.True, firstSave.FailureReason);

            archive.Revision = 2;
            archive.Progression.GoldBalance = 99;
            clock.UtcNowMilliseconds = 2000;
            persistence.FailNextSave("holmas/player_archive");

            HolmasPlayerArchiveSaveResult secondSave = gateway.SaveAsync(archive).GetAwaiter().GetResult();
            Assert.That(secondSave.Success, Is.False);

            HolmasPlayerArchiveLoadResult loadResult = gateway.LoadAsync().GetAwaiter().GetResult();

            Assert.That(loadResult.Success, Is.True, loadResult.FailureReason);
            Assert.That(loadResult.Archive.Revision, Is.EqualTo(2));
            Assert.That(loadResult.Archive.Progression.GoldBalance, Is.EqualTo(99));

            byte[] primaryBytes = persistence.GetRaw("holmas/player_archive");
            byte[] backupBytes = persistence.GetRaw("holmas/player_archive.backup");
            Assert.That(primaryBytes, Is.Not.Null);
            Assert.That(backupBytes, Is.Not.Null);
            Assert.That(System.Text.Encoding.UTF8.GetString(primaryBytes), Is.EqualTo(System.Text.Encoding.UTF8.GetString(backupBytes)));
        }

        [Test]
        public void HolmasPlayerArchiveSyncService_FlushIfNeeded_CoalescesPendingDirtyToLatestState()
        {
            var catalog = HolmasTestSupport.CreateStandardTaskCatalog();
            var clock = new FixedUtcClock { UtcNowMilliseconds = 1000 };
            var taskService = new HolmasTaskProgressService(catalog, new ScriptedRandomSource(0, 0, 1), clock);
            var metaService = new HolmasMetaProgressionService(
                HolmasTestSupport.CreateMetaCatalog(),
                catalog,
                new HolmasDefaultMetaExperienceSource(),
                new HolmasDefaultMetaExperienceSource(),
                clock);
            var coordinator = new HolmasProgressionCoordinator(taskService, metaService);
            var runtime = new HolmasGameplayRuntime(taskService, metaService, coordinator, new NullLogger());
            var persistence = new BlockingPersistence();
            var gateway = new HolmasLocalMockServerGateway(persistence, new NullLogger(), clock);
            var mapper = new HolmasPlayerArchiveMapper();
            var syncService = new HolmasPlayerArchiveSyncService(
                runtime,
                gateway,
                mapper,
                clock,
                new NullLogger(),
                HolmasLocalMockServerGateway.DefaultPlayerId,
                HolmasLocalMockServerGateway.DefaultSchemaVersion,
                0L);

            Task<bool> firstFlush = syncService.MarkDirtyAndSaveAsync("initial");
            Assert.That(persistence.SaveCallCount, Is.EqualTo(1));

            runtime.MetaProgressionState.GoldBalance = 99;
            syncService.MarkDirty("gold_changed");
            persistence.AllowSaves();
            Assert.That(firstFlush.GetAwaiter().GetResult(), Is.True);

            Assert.That(persistence.SaveCallCount, Is.EqualTo(4));
            HolmasPlayerArchiveLoadResult loadResult = gateway.LoadAsync().GetAwaiter().GetResult();
            Assert.That(loadResult.Success, Is.True, loadResult.FailureReason);
            Assert.That(loadResult.Archive.Revision, Is.EqualTo(2));
            Assert.That(loadResult.Archive.Progression.GoldBalance, Is.EqualTo(99));
        }

        [Test]
        public void HolmasPlayerArchiveSyncService_FlushAsync_WaitsForPendingSave()
        {
            var catalog = HolmasTestSupport.CreateStandardTaskCatalog();
            var clock = new FixedUtcClock { UtcNowMilliseconds = 1000 };
            var taskService = new HolmasTaskProgressService(catalog, new ScriptedRandomSource(0, 0, 1), clock);
            var metaService = new HolmasMetaProgressionService(
                HolmasTestSupport.CreateMetaCatalog(),
                catalog,
                new HolmasDefaultMetaExperienceSource(),
                new HolmasDefaultMetaExperienceSource(),
                clock);
            var coordinator = new HolmasProgressionCoordinator(taskService, metaService);
            var runtime = new HolmasGameplayRuntime(taskService, metaService, coordinator, new NullLogger());
            var persistence = new BlockingPersistence();
            var gateway = new HolmasLocalMockServerGateway(persistence, new NullLogger(), clock);
            var mapper = new HolmasPlayerArchiveMapper();
            var syncService = new HolmasPlayerArchiveSyncService(
                runtime,
                gateway,
                mapper,
                clock,
                new NullLogger(),
                HolmasLocalMockServerGateway.DefaultPlayerId,
                HolmasLocalMockServerGateway.DefaultSchemaVersion,
                0L);

            Task<bool> firstFlush = syncService.MarkDirtyAndSaveAsync("initial");
            runtime.MetaProgressionState.GoldBalance = 77;
            syncService.MarkDirty("gold_changed");

            Task<bool> drainTask = syncService.FlushAsync();
            Assert.That(drainTask.IsCompleted, Is.False);

            persistence.AllowSaves();

            Assert.That(firstFlush.GetAwaiter().GetResult(), Is.True);
            Assert.That(drainTask.GetAwaiter().GetResult(), Is.True);

            HolmasPlayerArchiveLoadResult loadResult = gateway.LoadAsync().GetAwaiter().GetResult();
            Assert.That(loadResult.Success, Is.True, loadResult.FailureReason);
            Assert.That(loadResult.Archive.Progression.GoldBalance, Is.EqualTo(77));
            Assert.That(loadResult.Archive.Revision, Is.EqualTo(2));
        }

        [Test]
        public void HolmasPlayerArchiveMapper_TryRestoreTaskBar_FailsOnSlotTaskMismatch()
        {
            var mapper = new HolmasPlayerArchiveMapper();
            HolmasPlayerArchiveRoot archive = mapper.CreateDefaultArchive();
            archive.TaskBar.Slots = new[]
            {
                new TaskSlotState
                {
                    SlotIndex = 0,
                    IsUnlocked = true,
                    UnlockExpireAt = 0L,
                    TaskInstanceId = "task-1",
                },
                new TaskSlotState
                {
                    SlotIndex = 1,
                    IsUnlocked = true,
                    UnlockExpireAt = 0L,
                    TaskInstanceId = string.Empty,
                },
            };
            archive.TaskBar.Tasks = new[]
            {
                new HolmasTaskRuntimeArchiveData
                {
                    Task = new TaskInstanceData
                    {
                        TaskInstanceId = "task-1",
                        SourceTaskTypeId = "task-normal",
                        TaskKind = "Money",
                        CatId = "cat-a",
                        TargetCount = 1,
                        CurrentCount = 0,
                        Reward = 10,
                        SlotIndex = 1,
                    }
                }
            };

            HolmasTaskBarRestoreResult result = mapper.TryRestoreTaskBar(archive);

            Assert.That(result.Success, Is.False);
            StringAssert.Contains("任务槽位与槽位声明不一致", result.FailureReason);
        }

        [Test]
        public void HolmasPlayerArchiveMapper_TryRestoreTaskBar_FailsWhenTaskInstanceIsMissing()
        {
            var mapper = new HolmasPlayerArchiveMapper();
            HolmasPlayerArchiveRoot archive = mapper.CreateDefaultArchive();
            archive.TaskBar.Slots = new[]
            {
                new TaskSlotState
                {
                    SlotIndex = 0,
                    IsUnlocked = true,
                    UnlockExpireAt = 0L,
                    TaskInstanceId = "task-1",
                },
            };
            archive.TaskBar.Tasks = new HolmasTaskRuntimeArchiveData[0];

            HolmasTaskBarRestoreResult result = mapper.TryRestoreTaskBar(archive);

            Assert.That(result.Success, Is.False);
            StringAssert.Contains("任务列表缺失对应实例", result.FailureReason);
        }

        [Test]
        public void HolmasPlayerArchiveMapper_CreateArchiveWithResetTaskBar_PreservesProgressionAndClearsCurrentLevel()
        {
            var mapper = new HolmasPlayerArchiveMapper();
            HolmasPlayerArchiveRoot archive = mapper.CreateDefaultArchive();
            archive.Revision = 7L;
            archive.SavedAtUtcMilliseconds = 1234L;
            archive.Progression.PlayerLevel = 3;
            archive.Progression.AgencyStageId = 2;
            archive.Progression.GoldBalance = 88;
            archive.CurrentLevel = new LevelSnapshot
            {
                MapId = "map-keep",
                TerrainPath = "terrain/keep",
                Seed = 99,
                RevealedCells = new[] { true, false, true },
                Completed = false,
                SpawnedCats = new List<SpawnedCatData>
                {
                    new SpawnedCatData
                    {
                        CatId = "cat-a",
                        CellIndex = 1,
                    }
                }
            };
            archive.TaskBar.Slots = new[]
            {
                new TaskSlotState
                {
                    SlotIndex = 0,
                    IsUnlocked = true,
                    UnlockExpireAt = 0L,
                    TaskInstanceId = "broken-task",
                },
            };

            HolmasPlayerArchiveRoot recovered = mapper.CreateArchiveWithResetTaskBar(archive);

            Assert.That(recovered.Revision, Is.EqualTo(7L));
            Assert.That(recovered.SavedAtUtcMilliseconds, Is.EqualTo(1234L));
            Assert.That(recovered.Progression.PlayerLevel, Is.EqualTo(3));
            Assert.That(recovered.Progression.AgencyStageId, Is.EqualTo(2));
            Assert.That(recovered.Progression.GoldBalance, Is.EqualTo(88));
            Assert.That(recovered.CurrentLevel, Is.Null);
            Assert.That(recovered.TaskBar.Tasks, Is.Empty);
            Assert.That(recovered.TaskBar.Slots[0].TaskInstanceId, Is.Empty);
            Assert.That(recovered.TaskBar.Slots[0].IsUnlocked, Is.True);
        }

        [Test]
        public void HolmasFlowCoordinator_PrepareStartupHomeStatus_DoesNotRefillTasksWhenLevelIsActive()
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
            var assetsRuntime = new ArchiveFakeAssetsRuntime(terrain);
            var runtime = new HolmasGameplayRuntime(taskService, metaService, coordinator, new NullLogger(), assetsRuntime);
            LevelGenerationRequest request = HolmasTestSupport.CreateRequest(
                "map-startup",
                TerrainAssetPathUtility.BuildAssetPath("startup"),
                13,
                1,
                1,
                new BoardSpawnEntry { CatId = "cat-a", Weight = 1 });

            runtime.StartLevelAsync(request).GetAwaiter().GetResult();
            Assert.That(runtime.HasActiveUncompletedLevel, Is.True);
            Assert.That(runtime.TaskBarState.Tasks, Is.Empty);

            var context = new HolmasApplicationContext(null, new NullLogger(), null, null, assetsRuntime, runtime);
            var rootObject = new GameObject("UiRootStartupRecoveryTest");
            try
            {
                var uiRoot = rootObject.AddComponent<UiRoot>();
                typeof(UiRoot)
                    .GetField("_context", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(uiRoot, context);
                var flowCoordinator = new HolmasFlowCoordinator(uiRoot, new TestBattleWorldHost());
                MethodInfo method = typeof(HolmasFlowCoordinator).GetMethod(
                    "PrepareStartupHomeStatus",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.That(method, Is.Not.Null);
                string status = method.Invoke(flowCoordinator, null) as string;

                Assert.That(runtime.TaskBarState.Tasks, Is.Empty);
                StringAssert.Contains("已恢复未完成棋盘", status);
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void HolmasGameplayRuntime_RestoreLevelAsync_RestoresRevealedCellsAndResetsFlags()
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
            var assetsRuntime = new ArchiveFakeAssetsRuntime(terrain);
            var runtime = new HolmasGameplayRuntime(taskService, metaService, coordinator, new NullLogger(), assetsRuntime);
            LevelGenerationRequest request = HolmasTestSupport.CreateRequest(
                "map-restore",
                TerrainAssetPathUtility.BuildAssetPath("restore"),
                7,
                1,
                1,
                new BoardSpawnEntry { CatId = "cat-a", Weight = 1 });

            runtime.StartLevelAsync(request).GetAwaiter().GetResult();
            runtime.ToggleFlag(0);
            runtime.RevealCell(1, out _);
            LevelSnapshot savedSnapshot = new HolmasPlayerArchiveMapper().CloneLevelSnapshot(runtime.CurrentLevelSnapshot);

            var restoredRuntime = new HolmasGameplayRuntime(taskService, metaService, coordinator, new NullLogger(), assetsRuntime);
            restoredRuntime.RestoreLevelAsync(savedSnapshot).GetAwaiter().GetResult();

            Assert.That(restoredRuntime.CurrentBoardRuntime.IsRevealed(1), Is.True);
            Assert.That(restoredRuntime.CurrentBoardRuntime.IsFlagged(0), Is.False);
            Assert.That(restoredRuntime.CurrentLevelSnapshot.RevealedCells[1], Is.True);
        }

        private class InMemoryPersistence : IPersistence
        {
            protected readonly Dictionary<string, byte[]> Store = new Dictionary<string, byte[]>();

            public virtual Task<bool> SaveAsync(string key, byte[] data)
            {
                Store[key] = data;
                return Task.FromResult(true);
            }

            public Task<byte[]> LoadAsync(string key)
            {
                Store.TryGetValue(key, out byte[] value);
                return Task.FromResult(value);
            }

            public Task<bool> DeleteAsync(string key)
            {
                Store.Remove(key);
                return Task.FromResult(true);
            }

            public bool Exists(string key)
            {
                return Store.ContainsKey(key);
            }

            public void SetRaw(string key, byte[] data)
            {
                Store[key] = data;
            }

            public byte[] GetRaw(string key)
            {
                Store.TryGetValue(key, out byte[] value);
                return value;
            }
        }

        private sealed class SelectiveFailPersistence : InMemoryPersistence
        {
            private readonly HashSet<string> _failNextKeys = new HashSet<string>();

            public override Task<bool> SaveAsync(string key, byte[] data)
            {
                if (_failNextKeys.Remove(key))
                {
                    return Task.FromResult(false);
                }

                return base.SaveAsync(key, data);
            }

            public void FailNextSave(string key)
            {
                _failNextKeys.Add(key);
            }
        }

        private sealed class BlockingPersistence : InMemoryPersistence
        {
            private readonly TaskCompletionSource<bool> _saveGate = new TaskCompletionSource<bool>();

            public int SaveCallCount { get; private set; }

            public override async Task<bool> SaveAsync(string key, byte[] data)
            {
                SaveCallCount++;
                await _saveGate.Task;
                return await base.SaveAsync(key, data);
            }

            public void AllowSaves()
            {
                _saveGate.TrySetResult(true);
            }
        }

        private sealed class ArchiveFakeAssetsRuntime : IAssetsRuntime
        {
            private readonly UnityEngine.Object _asset;

            public ArchiveFakeAssetsRuntime(UnityEngine.Object asset)
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
                return Task.FromResult<IAssetHandle>(new ArchiveFakeAssetHandle(_asset));
            }

            public void Shutdown()
            {
            }
        }

        private sealed class ArchiveFakeAssetHandle : IAssetHandle
        {
            public ArchiveFakeAssetHandle(UnityEngine.Object asset)
            {
                AssetObject = asset;
            }

            public UnityEngine.Object AssetObject { get; }

            public void Release()
            {
            }
        }

        private sealed class TestBattleWorldHost : IBattleWorldHost
        {
            public Task PrepareAsync(LevelSnapshot snapshot)
            {
                return Task.CompletedTask;
            }

            public void Show()
            {
            }

            public void Hide()
            {
            }

            public void Release()
            {
            }
        }
    }
}
