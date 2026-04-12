using System;
using System.Collections.Generic;
using System.Linq;
using App.HotUpdate.Holmas.Application;
using App.HotUpdate.Holmas.Meta;
using App.HotUpdate.Holmas.Tasks.Runtime;
using App.Shared.Holmas.PlayerData;
using App.Shared.Holmas.RuntimeData;

namespace App.HotUpdate.Holmas.PlayerData
{
    public sealed class HolmasTaskBarRestoreResult
    {
        public bool Success;
        public string FailureReason = string.Empty;
        public HolmasTaskBarState State = new HolmasTaskBarState();
    }

    /// <summary>
    /// archive 与运行时状态之间的双向映射器。
    /// </summary>
    public sealed class HolmasPlayerArchiveMapper
    {
        public HolmasPlayerArchiveRoot CreateDefaultArchive(
            string playerId = HolmasLocalMockServerGateway.DefaultPlayerId,
            string schemaVersion = HolmasLocalMockServerGateway.DefaultSchemaVersion)
        {
            var taskBarState = new HolmasTaskBarState();
            return new HolmasPlayerArchiveRoot
            {
                PlayerId = string.IsNullOrWhiteSpace(playerId) ? HolmasLocalMockServerGateway.DefaultPlayerId : playerId,
                SchemaVersion = string.IsNullOrWhiteSpace(schemaVersion) ? HolmasLocalMockServerGateway.DefaultSchemaVersion : schemaVersion,
                Progression = new HolmasProgressionArchiveData
                {
                    PlayerLevel = 1,
                    AgencyStageId = 1,
                },
                TaskBar = ExportTaskBar(taskBarState),
                CurrentLevel = null,
            };
        }

        public HolmasPlayerArchiveRoot CreateArchiveWithResetTaskBar(HolmasPlayerArchiveRoot source)
        {
            HolmasPlayerArchiveRoot archive = CreateDefaultArchive(
                source != null ? source.PlayerId : HolmasLocalMockServerGateway.DefaultPlayerId,
                source != null ? source.SchemaVersion : HolmasLocalMockServerGateway.DefaultSchemaVersion);
            archive.Revision = Math.Max(0L, source?.Revision ?? 0L);
            archive.SavedAtUtcMilliseconds = Math.Max(0L, source?.SavedAtUtcMilliseconds ?? 0L);
            archive.Progression = CloneProgression(source?.Progression);
            archive.CurrentLevel = source != null && source.CurrentLevel != null && !source.CurrentLevel.Completed
                ? CloneLevelSnapshot(source.CurrentLevel)
                : null;
            return archive;
        }

        public HolmasMetaProgressionState RestoreProgression(HolmasPlayerArchiveRoot archive)
        {
            HolmasProgressionArchiveData source = archive != null && archive.Progression != null
                ? archive.Progression
                : new HolmasProgressionArchiveData();

            var state = new HolmasMetaProgressionState
            {
                Experience = source.Experience,
                PlayerLevel = Math.Max(1, source.PlayerLevel),
                AgencyStageId = Math.Max(1, source.AgencyStageId),
                GoldBalance = source.GoldBalance,
                CompletedMapCount = source.CompletedMapCount,
                ClaimedTaskCount = source.ClaimedTaskCount,
                OfflineRewardTotal = source.OfflineRewardTotal,
                LastOfflineSettlementAtUtcMilliseconds = source.LastOfflineSettlementAtUtcMilliseconds,
            };

            if (source.CatDiscoveryCounts != null)
            {
                foreach (HolmasArchiveCounterEntry entry in source.CatDiscoveryCounts)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.Key))
                    {
                        continue;
                    }

                    state.CatDiscoveryCounts[entry.Key] = Math.Max(0, entry.Value);
                }
            }

            if (source.PromotionLevels != null)
            {
                foreach (HolmasPromotionLevelEntry entry in source.PromotionLevels)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.PromotionId) || entry.Level <= 0)
                    {
                        continue;
                    }

                    state.PromotionLevels[entry.PromotionId] = entry.Level;
                }
            }

            return state;
        }

        public HolmasTaskBarState RestoreTaskBar(HolmasPlayerArchiveRoot archive)
        {
            HolmasTaskBarRestoreResult result = TryRestoreTaskBar(archive);
            return result.Success ? result.State : new HolmasTaskBarState();
        }

        public HolmasTaskBarRestoreResult TryRestoreTaskBar(HolmasPlayerArchiveRoot archive)
        {
            HolmasTaskBarArchiveData source = archive != null && archive.TaskBar != null
                ? archive.TaskBar
                : null;
            if (source == null)
            {
                return new HolmasTaskBarRestoreResult
                {
                    Success = true,
                    State = new HolmasTaskBarState(),
                };
            }

            int totalSlots = Math.Max(1, source.TotalSlots);
            int defaultOpenSlots = Math.Max(0, Math.Min(source.DefaultOpenSlots, totalSlots));
            var state = new HolmasTaskBarState(totalSlots, defaultOpenSlots);
            state.Slots.Clear();
            state.Tasks.Clear();
            var slotTaskBindings = new Dictionary<string, int>(StringComparer.Ordinal);
            var unresolvedOccupiedSlots = new HashSet<int>();

            TaskSlotState[] sourceSlots = source.Slots ?? Array.Empty<TaskSlotState>();
            for (int slotIndex = 0; slotIndex < totalSlots; slotIndex++)
            {
                TaskSlotState sourceSlot = slotIndex < sourceSlots.Length ? sourceSlots[slotIndex] : null;
                if (sourceSlot != null && sourceSlot.SlotIndex != slotIndex)
                {
                    return CreateTaskBarRestoreFailure(
                        $"slot 索引不一致。expected={slotIndex}, actual={sourceSlot.SlotIndex}");
                }

                TaskSlotState restoredSlot = CloneSlotState(sourceSlot, slotIndex, defaultOpenSlots);
                if (!string.IsNullOrWhiteSpace(restoredSlot.TaskInstanceId))
                {
                    if (!restoredSlot.IsUnlocked)
                    {
                        return CreateTaskBarRestoreFailure(
                            $"锁定槽位不应绑定任务。slotIndex={slotIndex}, taskId={restoredSlot.TaskInstanceId}");
                    }

                    if (!slotTaskBindings.TryAdd(restoredSlot.TaskInstanceId, slotIndex))
                    {
                        return CreateTaskBarRestoreFailure(
                            $"多个槽位绑定了同一任务。taskId={restoredSlot.TaskInstanceId}");
                    }

                    unresolvedOccupiedSlots.Add(slotIndex);
                }

                restoredSlot.TaskInstanceId = string.Empty;
                state.Slots.Add(restoredSlot);
            }

            foreach (HolmasTaskRuntimeArchiveData archiveTask in source.Tasks ?? Array.Empty<HolmasTaskRuntimeArchiveData>())
            {
                if (archiveTask == null || archiveTask.Task == null)
                {
                    return CreateTaskBarRestoreFailure("任务栏 archive 中存在空任务条目。");
                }

                TaskInstanceData clonedTask = CloneTaskInstance(archiveTask.Task);
                if (string.IsNullOrWhiteSpace(clonedTask.TaskInstanceId))
                {
                    return CreateTaskBarRestoreFailure("任务实例缺少 TaskInstanceId。");
                }

                var runtimeTask = new HolmasTaskRuntimeInstance(clonedTask, archiveTask.IsRewardClaimed);
                int slotIndex = clonedTask.SlotIndex;
                if (slotIndex < 0 || slotIndex >= state.TotalSlots)
                {
                    return CreateTaskBarRestoreFailure($"任务绑定了非法槽位。taskId={clonedTask.TaskInstanceId}, slotIndex={slotIndex}");
                }

                TaskSlotState slot = state.GetSlot(slotIndex);
                if (slot == null || !slot.IsUnlocked)
                {
                    return CreateTaskBarRestoreFailure($"任务绑定到了未解锁槽位。taskId={clonedTask.TaskInstanceId}, slotIndex={slotIndex}");
                }

                if (!slotTaskBindings.TryGetValue(clonedTask.TaskInstanceId, out int expectedSlotIndex))
                {
                    return CreateTaskBarRestoreFailure($"任务未在槽位声明中出现。taskId={clonedTask.TaskInstanceId}");
                }

                if (expectedSlotIndex != slotIndex)
                {
                    return CreateTaskBarRestoreFailure(
                        $"任务槽位与槽位声明不一致。taskId={clonedTask.TaskInstanceId}, slotIndex={slotIndex}, expected={expectedSlotIndex}");
                }

                if (!unresolvedOccupiedSlots.Remove(slotIndex))
                {
                    return CreateTaskBarRestoreFailure($"槽位重复绑定任务。slotIndex={slotIndex}, taskId={clonedTask.TaskInstanceId}");
                }

                state.BindTask(slotIndex, runtimeTask);
            }

            if (unresolvedOccupiedSlots.Count > 0)
            {
                return CreateTaskBarRestoreFailure("槽位声明了任务，但任务列表缺失对应实例。");
            }

            return new HolmasTaskBarRestoreResult
            {
                Success = true,
                State = state,
            };
        }

        public LevelSnapshot CloneLevelSnapshot(LevelSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return null;
            }

            return new LevelSnapshot
            {
                MapId = snapshot.MapId ?? string.Empty,
                TerrainPath = snapshot.TerrainPath ?? string.Empty,
                Seed = snapshot.Seed,
                SpawnedCats = snapshot.SpawnedCats != null
                    ? snapshot.SpawnedCats
                        .Where(item => item != null)
                        .Select(item => new SpawnedCatData
                        {
                            CatId = item.CatId ?? string.Empty,
                            CellIndex = item.CellIndex,
                        })
                        .ToList()
                    : new List<SpawnedCatData>(),
                RevealedCells = snapshot.RevealedCells != null
                    ? (bool[])snapshot.RevealedCells.Clone()
                    : Array.Empty<bool>(),
                Completed = snapshot.Completed,
            };
        }

        public HolmasPlayerArchiveRoot ExportArchive(
            HolmasGameplayRuntime runtime,
            string playerId,
            string schemaVersion,
            long revision,
            long savedAtUtcMilliseconds)
        {
            if (runtime == null)
            {
                throw new ArgumentNullException(nameof(runtime));
            }

            return new HolmasPlayerArchiveRoot
            {
                PlayerId = string.IsNullOrWhiteSpace(playerId) ? HolmasLocalMockServerGateway.DefaultPlayerId : playerId,
                SchemaVersion = string.IsNullOrWhiteSpace(schemaVersion) ? HolmasLocalMockServerGateway.DefaultSchemaVersion : schemaVersion,
                Revision = revision,
                SavedAtUtcMilliseconds = savedAtUtcMilliseconds,
                Progression = ExportProgression(runtime.MetaProgressionState),
                TaskBar = ExportTaskBar(runtime.TaskBarState),
                CurrentLevel = runtime.CurrentLevelSnapshot != null && !runtime.CurrentLevelSnapshot.Completed
                    ? CloneLevelSnapshot(runtime.CurrentLevelSnapshot)
                    : null,
            };
        }

        private static HolmasProgressionArchiveData ExportProgression(HolmasMetaProgressionState state)
        {
            state ??= new HolmasMetaProgressionState();
            return new HolmasProgressionArchiveData
            {
                Experience = state.Experience,
                PlayerLevel = Math.Max(1, state.PlayerLevel),
                AgencyStageId = Math.Max(1, state.AgencyStageId),
                GoldBalance = state.GoldBalance,
                CompletedMapCount = state.CompletedMapCount,
                ClaimedTaskCount = state.ClaimedTaskCount,
                OfflineRewardTotal = state.OfflineRewardTotal,
                LastOfflineSettlementAtUtcMilliseconds = state.LastOfflineSettlementAtUtcMilliseconds,
                CatDiscoveryCounts = state.CatDiscoveryCounts
                    .OrderBy(item => item.Key, StringComparer.Ordinal)
                    .Select(item => new HolmasArchiveCounterEntry
                    {
                        Key = item.Key,
                        Value = item.Value,
                    })
                    .ToArray(),
                PromotionLevels = state.PromotionLevels
                    .OrderBy(item => item.Key, StringComparer.Ordinal)
                    .Select(item => new HolmasPromotionLevelEntry
                    {
                        PromotionId = item.Key,
                        Level = item.Value,
                    })
                    .ToArray(),
            };
        }

        private static HolmasProgressionArchiveData CloneProgression(HolmasProgressionArchiveData source)
        {
            source ??= new HolmasProgressionArchiveData();
            return new HolmasProgressionArchiveData
            {
                Experience = source.Experience,
                PlayerLevel = Math.Max(1, source.PlayerLevel),
                AgencyStageId = Math.Max(1, source.AgencyStageId),
                GoldBalance = source.GoldBalance,
                CompletedMapCount = source.CompletedMapCount,
                ClaimedTaskCount = source.ClaimedTaskCount,
                OfflineRewardTotal = source.OfflineRewardTotal,
                LastOfflineSettlementAtUtcMilliseconds = source.LastOfflineSettlementAtUtcMilliseconds,
                CatDiscoveryCounts = source.CatDiscoveryCounts != null
                    ? source.CatDiscoveryCounts
                        .Where(item => item != null && !string.IsNullOrWhiteSpace(item.Key))
                        .Select(item => new HolmasArchiveCounterEntry
                        {
                            Key = item.Key,
                            Value = Math.Max(0, item.Value),
                        })
                        .ToArray()
                    : Array.Empty<HolmasArchiveCounterEntry>(),
                PromotionLevels = source.PromotionLevels != null
                    ? source.PromotionLevels
                        .Where(item => item != null && !string.IsNullOrWhiteSpace(item.PromotionId) && item.Level > 0)
                        .Select(item => new HolmasPromotionLevelEntry
                        {
                            PromotionId = item.PromotionId,
                            Level = item.Level,
                        })
                        .ToArray()
                    : Array.Empty<HolmasPromotionLevelEntry>(),
            };
        }

        private HolmasTaskBarArchiveData ExportTaskBar(HolmasTaskBarState state)
        {
            state ??= new HolmasTaskBarState();
            return new HolmasTaskBarArchiveData
            {
                TotalSlots = state.TotalSlots,
                DefaultOpenSlots = state.DefaultOpenSlots,
                Slots = state.Slots != null
                    ? state.Slots.Select((slot, index) => CloneSlotState(slot, index, state.DefaultOpenSlots)).ToArray()
                    : Array.Empty<TaskSlotState>(),
                Tasks = state.Tasks != null
                    ? state.Tasks
                        .Where(item => item != null && item.Task != null)
                        .Select(item => new HolmasTaskRuntimeArchiveData
                        {
                            Task = CloneTaskInstance(item.Task),
                            IsRewardClaimed = item.IsRewardClaimed,
                        })
                        .ToArray()
                    : Array.Empty<HolmasTaskRuntimeArchiveData>(),
            };
        }

        private static TaskInstanceData CloneTaskInstance(TaskInstanceData source)
        {
            source ??= new TaskInstanceData();
            return new TaskInstanceData
            {
                TaskInstanceId = source.TaskInstanceId ?? string.Empty,
                SourceTaskTypeId = source.SourceTaskTypeId ?? string.Empty,
                TaskKind = source.TaskKind ?? string.Empty,
                CatId = source.CatId ?? string.Empty,
                TargetCount = source.TargetCount,
                CurrentCount = source.CurrentCount,
                Reward = source.Reward,
                SlotIndex = source.SlotIndex,
                ExpireAt = source.ExpireAt,
            };
        }

        private static TaskSlotState CloneSlotState(TaskSlotState source, int slotIndex, int defaultOpenSlots)
        {
            if (source == null)
            {
                return new TaskSlotState
                {
                    SlotIndex = slotIndex,
                    IsUnlocked = slotIndex < defaultOpenSlots,
                    UnlockExpireAt = 0L,
                    TaskInstanceId = string.Empty,
                };
            }

            return new TaskSlotState
            {
                SlotIndex = slotIndex,
                IsUnlocked = source.IsUnlocked,
                UnlockExpireAt = source.UnlockExpireAt,
                TaskInstanceId = source.TaskInstanceId ?? string.Empty,
            };
        }

        private static HolmasTaskBarRestoreResult CreateTaskBarRestoreFailure(string reason)
        {
            return new HolmasTaskBarRestoreResult
            {
                Success = false,
                FailureReason = reason ?? "unknown",
                State = new HolmasTaskBarState(),
            };
        }
    }
}
