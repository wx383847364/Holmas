using System;
using System.Collections.Generic;
using System.Linq;
using App.Shared.Holmas.RuntimeData;

namespace App.HotUpdate.Holmas.Tasks.Runtime
{
    /// <summary>
    /// 单条任务实例的运行时包装。
    /// Shared DTO 负责跨层传递基础字段，这里额外维护“奖励是否已领取”的状态。
    /// </summary>
    [Serializable]
    public sealed class HolmasTaskRuntimeInstance
    {
        public HolmasTaskRuntimeInstance(TaskInstanceData task)
            : this(task, false)
        {
        }

        public HolmasTaskRuntimeInstance(TaskInstanceData task, bool isRewardClaimed)
        {
            Task = task ?? throw new ArgumentNullException(nameof(task));
            IsRewardClaimed = isRewardClaimed;
        }

        public TaskInstanceData Task { get; private set; }

        public bool IsRewardClaimed { get; private set; }

        public bool IsCompleted
        {
            get { return Task.CurrentCount >= Task.TargetCount; }
        }

        public bool CanClaimReward
        {
            get { return IsCompleted && !IsRewardClaimed; }
        }

        public int ApplyProgress(int amount)
        {
            if (amount <= 0 || IsRewardClaimed)
            {
                return Task.CurrentCount;
            }

            Task.CurrentCount = Math.Min(Task.TargetCount, Task.CurrentCount + amount);
            return Task.CurrentCount;
        }

        public int ClaimReward()
        {
            if (!CanClaimReward)
            {
                return 0;
            }

            IsRewardClaimed = true;
            return Task.Reward;
        }
    }

    /// <summary>
    /// 任务栏运行时状态。
    /// 负责维护槽位、任务绑定和 ad 解锁计时，但不负责生成规则本身。
    /// </summary>
    [Serializable]
    public sealed class HolmasTaskBarState
    {
        public const int DefaultTotalSlots = 5;
        public const int DefaultInitialOpenSlots = 2;

        public HolmasTaskBarState()
            : this(DefaultTotalSlots, DefaultInitialOpenSlots)
        {
        }

        public HolmasTaskBarState(int totalSlots, int defaultOpenSlots)
        {
            if (totalSlots <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(totalSlots));
            }

            if (defaultOpenSlots < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(defaultOpenSlots));
            }

            TotalSlots = totalSlots;
            DefaultOpenSlots = Math.Min(defaultOpenSlots, totalSlots);
            Slots = new List<TaskSlotState>(totalSlots);
            Tasks = new List<HolmasTaskRuntimeInstance>();
            for (int i = 0; i < totalSlots; i++)
            {
                Slots.Add(new TaskSlotState
                {
                    SlotIndex = i,
                    IsUnlocked = i < DefaultOpenSlots,
                    UnlockExpireAt = 0L,
                    TaskInstanceId = string.Empty
                });
            }
        }

        public int TotalSlots { get; private set; }

        public int DefaultOpenSlots { get; private set; }

        public List<TaskSlotState> Slots { get; private set; }

        public List<HolmasTaskRuntimeInstance> Tasks { get; private set; }

        public TaskSlotState GetSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= Slots.Count)
            {
                return null;
            }

            return Slots[slotIndex];
        }

        public HolmasTaskRuntimeInstance GetTaskById(string taskInstanceId)
        {
            if (string.IsNullOrEmpty(taskInstanceId))
            {
                return null;
            }

            return Tasks.FirstOrDefault(item => item.Task != null && string.Equals(item.Task.TaskInstanceId, taskInstanceId, StringComparison.Ordinal));
        }

        public HolmasTaskRuntimeInstance GetTaskBySlot(int slotIndex)
        {
            var slot = GetSlot(slotIndex);
            if (slot == null || string.IsNullOrEmpty(slot.TaskInstanceId))
            {
                return null;
            }

            return GetTaskById(slot.TaskInstanceId);
        }

        public void UnlockSlot(int slotIndex, long unlockExpireAtUtcMs)
        {
            var slot = GetSlot(slotIndex);
            if (slot == null)
            {
                return;
            }

            slot.IsUnlocked = true;
            slot.UnlockExpireAt = unlockExpireAtUtcMs;
        }

        public void LockSlot(int slotIndex)
        {
            var slot = GetSlot(slotIndex);
            if (slot == null)
            {
                return;
            }

            slot.IsUnlocked = false;
            slot.UnlockExpireAt = 0L;
        }

        public void BindTask(int slotIndex, HolmasTaskRuntimeInstance runtimeTask)
        {
            var slot = GetSlot(slotIndex);
            if (slot == null || runtimeTask == null)
            {
                return;
            }

            if (!Tasks.Contains(runtimeTask))
            {
                Tasks.Add(runtimeTask);
            }

            slot.TaskInstanceId = runtimeTask.Task.TaskInstanceId;
            runtimeTask.Task.SlotIndex = slotIndex;
        }

        public void ClearSlot(int slotIndex, bool removeTask)
        {
            var slot = GetSlot(slotIndex);
            if (slot == null)
            {
                return;
            }

            if (removeTask && !string.IsNullOrEmpty(slot.TaskInstanceId))
            {
                Tasks.RemoveAll(item => item.Task != null && string.Equals(item.Task.TaskInstanceId, slot.TaskInstanceId, StringComparison.Ordinal));
            }

            slot.TaskInstanceId = string.Empty;
        }

        public IReadOnlyCollection<string> GetActiveCatIds()
        {
            var active = new HashSet<string>(StringComparer.Ordinal);
            foreach (var task in Tasks)
            {
                if (task == null || task.Task == null)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(task.Task.CatId))
                {
                    active.Add(task.Task.CatId);
                }
            }

            return active;
        }

        public IReadOnlyList<HolmasTaskRuntimeInstance> GetClaimableTasks()
        {
            return Tasks.Where(item => item != null && item.CanClaimReward).ToList();
        }

        public int GetUnlockedEmptySlotCount()
        {
            int count = 0;
            for (int i = 0; i < Slots.Count; i++)
            {
                TaskSlotState slot = Slots[i];
                if (slot != null && slot.IsUnlocked && string.IsNullOrEmpty(slot.TaskInstanceId))
                {
                    count++;
                }
            }

            return count;
        }
    }
}
