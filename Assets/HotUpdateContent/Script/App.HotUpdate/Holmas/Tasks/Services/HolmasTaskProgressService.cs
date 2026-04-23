using System;
using System.Collections.Generic;
using System.Linq;
using App.Shared.Holmas.RuntimeData;
using App.HotUpdate.Holmas.Progression;
using App.HotUpdate.Holmas.Tasks.Config;
using App.HotUpdate.Holmas.Tasks.Runtime;

namespace App.HotUpdate.Holmas.Tasks.Services
{
    /// <summary>
    /// 单次任务抽取结果。
    /// </summary>
    [Serializable]
    public sealed class HolmasTaskGenerationResult
    {
        public bool Success;
        public string FailureReason = string.Empty;
        public TaskInstanceData Task;
        public int SlotIndex = -1;
    }

    /// <summary>
    /// 单次领奖结果。
    /// </summary>
    [Serializable]
    public sealed class HolmasTaskClaimResult
    {
        public bool Success;
        public string FailureReason = string.Empty;
        public string TaskInstanceId = string.Empty;
        public int SlotIndex = -1;
        public int Reward;
        public TaskInstanceData RefilledTask;
    }

    /// <summary>
    /// 地图结算后，对任务栏的推进结果。
    /// </summary>
    [Serializable]
    public sealed class HolmasTaskProgressResult
    {
        public readonly List<string> ProgressedTaskIds = new List<string>();
        public readonly List<string> NewlyCompletedTaskIds = new List<string>();
        public readonly List<int> NewlyCompletedSlotIndices = new List<int>();
    }

    /// <summary>
    /// 任务栏补位结果。
    /// </summary>
    [Serializable]
    public sealed class HolmasTaskRefillResult
    {
        public readonly List<HolmasTaskGenerationResult> GeneratedTasks = new List<HolmasTaskGenerationResult>();
    }

    /// <summary>
    /// 广告解锁单槽结果。
    /// </summary>
    [Serializable]
    public sealed class HolmasTaskSlotUnlockResult
    {
        public bool Success;
        public string FailureReason = string.Empty;
        public int SlotIndex = -1;
        public long UnlockExpireAt;
        public HolmasTaskGenerationResult GeneratedTask;
    }

    /// <summary>
    /// 任务与进度服务。
    /// 这层只处理运行时任务状态，不碰 UI 和地图本体。
    /// </summary>
    public sealed class HolmasTaskProgressService
    {
        private readonly IHolmasTaskCatalog _catalog;
        private readonly IHolmasRandomSource _randomSource;
        private readonly IHolmasUtcClock _clock;

        public HolmasTaskProgressService(
            IHolmasTaskCatalog catalog,
            IHolmasRandomSource randomSource,
            IHolmasUtcClock clock)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _randomSource = randomSource ?? throw new ArgumentNullException(nameof(randomSource));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public HolmasTaskBarState CreateDefaultTaskBarState()
        {
            return new HolmasTaskBarState();
        }

        public bool RefreshExpiredAdSlots(HolmasTaskBarState taskBarState, long utcNowMilliseconds)
        {
            bool changed = false;
            if (taskBarState == null)
            {
                return false;
            }

            foreach (var slot in taskBarState.Slots)
            {
                if (slot == null || !slot.IsUnlocked)
                {
                    continue;
                }

                if (slot.UnlockExpireAt > 0 && utcNowMilliseconds >= slot.UnlockExpireAt)
                {
                    if (string.IsNullOrEmpty(slot.TaskInstanceId))
                    {
                        taskBarState.ClearSlot(slot.SlotIndex, true);
                        taskBarState.LockSlot(slot.SlotIndex);
                    }
                    else
                    {
                        taskBarState.MarkPendingRelockAfterTaskCompletion(slot.SlotIndex);
                    }

                    changed = true;
                }
            }

            return changed;
        }

        public bool RefreshExpiredAdSlots(HolmasTaskBarState taskBarState)
        {
            return RefreshExpiredAdSlots(taskBarState, _clock.UtcNowMilliseconds);
        }

        public HolmasTaskRefillResult RefillUnlockedEmptySlots(HolmasTaskBarState taskBarState, int playerLevel)
        {
            var result = new HolmasTaskRefillResult();
            if (taskBarState == null)
            {
                return result;
            }

            RefreshExpiredAdSlots(taskBarState, _clock.UtcNowMilliseconds);
            EnsurePlayerLevelExists(playerLevel, out var levelDefinition);

            if (levelDefinition == null)
            {
                return result;
            }

            for (int i = 0; i < taskBarState.Slots.Count; i++)
            {
                var slot = taskBarState.Slots[i];
                if (slot == null || !slot.IsUnlocked || slot.PendingRelockAfterTaskCompletion || !string.IsNullOrEmpty(slot.TaskInstanceId))
                {
                    continue;
                }

                var generation = TryGenerateTask(taskBarState, levelDefinition, i);
                result.GeneratedTasks.Add(generation);
            }

            return result;
        }

        public HolmasTaskSlotUnlockResult UnlockAdSlot(
            HolmasTaskBarState taskBarState,
            int slotIndex,
            int playerLevel,
            long unlockExpireAtUtcMilliseconds)
        {
            var result = new HolmasTaskSlotUnlockResult
            {
                SlotIndex = slotIndex,
                UnlockExpireAt = unlockExpireAtUtcMilliseconds
            };

            if (taskBarState == null)
            {
                result.FailureReason = "任务栏状态为空。";
                return result;
            }

            var slot = taskBarState.GetSlot(slotIndex);
            if (slot == null)
            {
                result.FailureReason = "槽位索引无效。";
                return result;
            }

            if (slotIndex < 0 || slotIndex >= taskBarState.TotalSlots)
            {
                result.FailureReason = "槽位索引无效。";
                return result;
            }

            taskBarState.UnlockSlot(slotIndex, unlockExpireAtUtcMilliseconds);
            result.Success = true;

            if (string.IsNullOrEmpty(slot.TaskInstanceId))
            {
                result.GeneratedTask = TryGenerateTask(taskBarState, playerLevel, slotIndex);
            }

            return result;
        }

        public HolmasTaskGenerationResult TryGenerateTask(HolmasTaskBarState taskBarState, int playerLevel, int slotIndex)
        {
            if (!EnsurePlayerLevelExists(playerLevel, out var levelDefinition))
            {
                return new HolmasTaskGenerationResult
                {
                    Success = false,
                    FailureReason = "找不到玩家等级配置。"
                };
            }

            return TryGenerateTask(taskBarState, levelDefinition, slotIndex);
        }

        public HolmasTaskClaimResult ClaimTaskReward(
            HolmasTaskBarState taskBarState,
            int slotIndex,
            int playerLevel,
            bool refillEmptySlotImmediately = false)
        {
            var result = new HolmasTaskClaimResult
            {
                SlotIndex = slotIndex
            };

            if (taskBarState == null)
            {
                result.FailureReason = "任务栏状态为空。";
                return result;
            }

            var slot = taskBarState.GetSlot(slotIndex);
            if (slot == null)
            {
                result.FailureReason = "槽位索引无效。";
                return result;
            }

            if (!slot.IsUnlocked)
            {
                result.FailureReason = "当前槽位未解锁。";
                return result;
            }

            var runtimeTask = taskBarState.GetTaskBySlot(slotIndex);
            if (runtimeTask == null)
            {
                result.FailureReason = "该槽位没有可领奖的任务。";
                return result;
            }

            if (!runtimeTask.CanClaimReward)
            {
                result.FailureReason = runtimeTask.IsRewardClaimed
                    ? "任务奖励已经领取。"
                    : "任务尚未完成，不能领奖。";
                return result;
            }

            result.Success = true;
            result.TaskInstanceId = runtimeTask.Task.TaskInstanceId;
            result.Reward = runtimeTask.ClaimReward();

            taskBarState.ClearSlot(slotIndex, true);

            bool shouldRelockAfterClaim = slot != null && slot.PendingRelockAfterTaskCompletion;
            if (shouldRelockAfterClaim)
            {
                taskBarState.LockSlot(slotIndex);
            }
            else if (refillEmptySlotImmediately && slot != null && slot.IsUnlocked)
            {
                var refill = TryGenerateTask(taskBarState, playerLevel, slotIndex);
                if (refill.Success)
                {
                    result.RefilledTask = refill.Task;
                }
            }

            return result;
        }

        public HolmasTaskProgressResult ApplyFoundCats(HolmasTaskBarState taskBarState, IEnumerable<SpawnedCatData> foundCats)
        {
            var result = new HolmasTaskProgressResult();
            if (taskBarState == null || foundCats == null)
            {
                return result;
            }

            var catCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var cat in foundCats)
            {
                if (cat == null || string.IsNullOrEmpty(cat.CatId))
                {
                    continue;
                }

                int currentCount;
                catCounts.TryGetValue(cat.CatId, out currentCount);
                catCounts[cat.CatId] = currentCount + 1;
            }

            foreach (var task in taskBarState.Tasks)
            {
                if (task == null || task.Task == null || task.IsRewardClaimed)
                {
                    continue;
                }

                int addCount;
                if (!catCounts.TryGetValue(task.Task.CatId, out addCount) || addCount <= 0)
                {
                    continue;
                }

                var before = task.Task.CurrentCount;
                task.ApplyProgress(addCount);
                if (task.Task.CurrentCount > before)
                {
                    result.ProgressedTaskIds.Add(task.Task.TaskInstanceId);
                }

                if (task.IsCompleted && before < task.Task.TargetCount)
                {
                    result.NewlyCompletedTaskIds.Add(task.Task.TaskInstanceId);
                    result.NewlyCompletedSlotIndices.Add(task.Task.SlotIndex);
                }
            }

            return result;
        }

        public HolmasTaskProgressResult ApplyMapCompletion(HolmasTaskBarState taskBarState, IEnumerable<SpawnedCatData> spawnedCats)
        {
            return ApplyFoundCats(taskBarState, spawnedCats);
        }

        public bool TryPickUncompletedTaskCatId(HolmasTaskBarState taskBarState, out string catId)
        {
            catId = string.Empty;
            IReadOnlyList<TaskCatPickCandidate> candidates = BuildUncompletedTaskCatPool(taskBarState);
            if (candidates.Count == 0)
            {
                return false;
            }

            int[] weights = candidates.Select(item => Math.Max(1, item.Weight)).ToArray();
            int pickedIndex = HolmasWeightedPicker.PickIndex(weights, _randomSource);
            if (pickedIndex < 0 || pickedIndex >= candidates.Count)
            {
                pickedIndex = 0;
            }

            catId = candidates[pickedIndex].CatId ?? string.Empty;
            return !string.IsNullOrWhiteSpace(catId);
        }

        public IReadOnlyList<HolmasTaskRuntimeInstance> GetClaimableTasks(HolmasTaskBarState taskBarState)
        {
            if (taskBarState == null)
            {
                return Array.Empty<HolmasTaskRuntimeInstance>();
            }

            return taskBarState.GetClaimableTasks();
        }

        private IReadOnlyList<TaskCatPickCandidate> BuildUncompletedTaskCatPool(HolmasTaskBarState taskBarState)
        {
            if (taskBarState == null || taskBarState.Tasks == null || taskBarState.Tasks.Count == 0)
            {
                return Array.Empty<TaskCatPickCandidate>();
            }

            var candidates = new List<TaskCatPickCandidate>();
            var indexByCatId = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < taskBarState.Tasks.Count; i++)
            {
                HolmasTaskRuntimeInstance runtimeTask = taskBarState.Tasks[i];
                if (runtimeTask == null ||
                    runtimeTask.Task == null ||
                    runtimeTask.IsRewardClaimed ||
                    runtimeTask.Task.CurrentCount >= runtimeTask.Task.TargetCount ||
                    string.IsNullOrWhiteSpace(runtimeTask.Task.CatId))
                {
                    continue;
                }

                string taskCatId = runtimeTask.Task.CatId;
                int weight = ResolveCatWeight(taskCatId);
                if (indexByCatId.TryGetValue(taskCatId, out int existingIndex))
                {
                    candidates[existingIndex].Weight += weight;
                    continue;
                }

                indexByCatId[taskCatId] = candidates.Count;
                candidates.Add(new TaskCatPickCandidate
                {
                    CatId = taskCatId,
                    Weight = weight,
                });
            }

            return candidates;
        }

        private int ResolveCatWeight(string catId)
        {
            if (!string.IsNullOrWhiteSpace(catId) &&
                _catalog.TryGetCat(catId, out HolmasCatDefinition catDefinition) &&
                catDefinition != null &&
                catDefinition.Weight > 0)
            {
                return catDefinition.Weight;
            }

            return 1;
        }

        private sealed class TaskCatPickCandidate
        {
            public string CatId = string.Empty;
            public int Weight = 1;
        }

        private HolmasTaskGenerationResult TryGenerateTask(
            HolmasTaskBarState taskBarState,
            HolmasPlayerLevelDefinition levelDefinition,
            int slotIndex)
        {
            var result = new HolmasTaskGenerationResult
            {
                SlotIndex = slotIndex
            };

            if (taskBarState == null)
            {
                result.FailureReason = "任务栏状态为空。";
                return result;
            }

            var slot = taskBarState.GetSlot(slotIndex);
            if (slot == null)
            {
                result.FailureReason = "槽位索引无效。";
                return result;
            }

            if (!slot.IsUnlocked)
            {
                result.FailureReason = "当前槽位未解锁。";
                return result;
            }

            if (slot.PendingRelockAfterTaskCompletion)
            {
                result.FailureReason = "当前槽位已到期待锁，不能补入新任务。";
                return result;
            }

            if (!string.IsNullOrEmpty(slot.TaskInstanceId))
            {
                result.FailureReason = "当前槽位已有任务。";
                return result;
            }

            var activeCatIds = new HashSet<string>(taskBarState.GetActiveCatIds(), StringComparer.Ordinal);
            var generation = CreateTaskInstance(levelDefinition, activeCatIds, slot.UnlockExpireAt);
            if (generation == null)
            {
                result.FailureReason = "当前等级没有可用的任务模板或猫种。";
                return result;
            }

            taskBarState.BindTask(slotIndex, new HolmasTaskRuntimeInstance(generation));
            result.Success = true;
            result.Task = generation;
            return result;
        }

        private TaskInstanceData CreateTaskInstance(
            HolmasPlayerLevelDefinition levelDefinition,
            HashSet<string> activeCatIds,
            long slotUnlockExpireAt)
        {
            if (levelDefinition == null || levelDefinition.TaskTypeIds == null || levelDefinition.TaskTypeWeights == null)
            {
                return null;
            }

            if (levelDefinition.TaskTypeIds.Length == 0 || levelDefinition.TaskTypeWeights.Length == 0)
            {
                return null;
            }

            if (levelDefinition.TaskTypeIds.Length != levelDefinition.TaskTypeWeights.Length)
            {
                return null;
            }

            var triedTaskTypes = new HashSet<string>(StringComparer.Ordinal);
            int maxAttempts = Math.Max(8, levelDefinition.TaskTypeIds.Length * 4);
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                int taskTypeIndex = HolmasWeightedPicker.PickIndex(levelDefinition.TaskTypeWeights, _randomSource);
                if (taskTypeIndex < 0 || taskTypeIndex >= levelDefinition.TaskTypeIds.Length)
                {
                    return null;
                }

                var taskTypeId = levelDefinition.TaskTypeIds[taskTypeIndex];
                if (string.IsNullOrEmpty(taskTypeId) || !triedTaskTypes.Add(taskTypeId))
                {
                    continue;
                }

                if (!_catalog.TryGetTaskTemplate(taskTypeId, out var template) || template == null)
                {
                    continue;
                }

                if (template.CatIdList == null || template.CatIdList.Length == 0)
                {
                    continue;
                }

                var availableCats = template.CatIdList
                    .Where(catId => !string.IsNullOrEmpty(catId) && !activeCatIds.Contains(catId))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                if (availableCats.Length == 0)
                {
                    continue;
                }

                var catId = availableCats[_randomSource.Next(availableCats.Length)];
                if (!_catalog.TryGetCat(catId, out var catDefinition) || catDefinition == null)
                {
                    continue;
                }

                if (template.TaskKind != HolmasTaskKind.Money)
                {
                    continue;
                }

                int countMin = Math.Min(template.CountMin, template.CountMax);
                int countMax = Math.Max(template.CountMin, template.CountMax);
                int targetCount = countMax <= 0 ? 0 : _randomSource.Next(Math.Max(0, countMin), countMax + 1);
                if (targetCount <= 0)
                {
                    targetCount = Math.Max(1, countMax);
                }

                int reward = (int)Math.Round(catDefinition.Price * targetCount * template.LevelRewardFactor, MidpointRounding.AwayFromZero);

                return new TaskInstanceData
                {
                    TaskInstanceId = Guid.NewGuid().ToString("N"),
                    SourceTaskTypeId = template.TaskTypeId,
                    TaskKind = template.TaskKind.ToString(),
                    CatId = catId,
                    TargetCount = targetCount,
                    CurrentCount = 0,
                    Reward = reward,
                    SlotIndex = -1,
                    ExpireAt = slotUnlockExpireAt > 0 ? slotUnlockExpireAt : 0L
                };
            }

            return null;
        }

        private bool EnsurePlayerLevelExists(int playerLevel, out HolmasPlayerLevelDefinition levelDefinition)
        {
            if (_catalog.TryGetPlayerLevel(playerLevel, out levelDefinition))
            {
                return true;
            }

            levelDefinition = null;
            return false;
        }
    }
}
