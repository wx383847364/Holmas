using System;
using System.Text;
using System.Threading.Tasks;
using App.Shared.Contracts;
using UnityEngine;

namespace App.HotUpdate.Holmas.Tutorial
{
    [Serializable]
    public sealed class CoreFindCatTutorialProgress
    {
        public string tutorialId = CoreFindCatTutorialBoardDefinition.MapId;
        public int version = 2;
        public bool started;
        public bool completed;
        public bool skipped;
        public bool dismissedNormalBoardHint;
        public int currentStepIndex = -1;
        public string currentStepId = string.Empty;
        public int completedStepIndex = -1;
        public string completedStepId = string.Empty;
        public long startedAtUtcMilliseconds;
        public long updatedAtUtcMilliseconds;
        public long completedAtUtcMilliseconds;

        // Kept for old local records and older tests. Mirrors currentStepId.
        public string lastStepId = string.Empty;
    }

    public sealed class CoreFindCatTutorialProgressStore
    {
        public const string PersistenceKey = "holmas.tutorial.core_find_cat.v1";

        private readonly IPersistence _persistence;

        public CoreFindCatTutorialProgressStore(IPersistence persistence)
        {
            _persistence = persistence;
        }

        public async Task<CoreFindCatTutorialProgress> LoadAsync()
        {
            if (_persistence == null)
            {
                return new CoreFindCatTutorialProgress();
            }

            try
            {
                byte[] bytes = await _persistence.LoadAsync(PersistenceKey);
                if (bytes == null || bytes.Length == 0)
                {
                    return new CoreFindCatTutorialProgress();
                }

                string json = Encoding.UTF8.GetString(bytes);
                CoreFindCatTutorialProgress progress = JsonUtility.FromJson<CoreFindCatTutorialProgress>(json);
                return CoreFindCatTutorialProgressService.Normalize(progress);
            }
            catch
            {
                return new CoreFindCatTutorialProgress();
            }
        }

        public Task<bool> SaveAsync(CoreFindCatTutorialProgress progress)
        {
            return SaveMergedAsync(CoreFindCatTutorialProgressService.Normalize(progress));
        }

        public Task<bool> SaveLastStepAsync(string stepId)
        {
            var service = new CoreFindCatTutorialProgressService(this);
            return service.MarkCurrentStepAsync(-1, stepId);
        }

        public Task<bool> SaveCompletedAsync(long completedAtUtcMilliseconds, string stepId)
        {
            var service = new CoreFindCatTutorialProgressService(this);
            return service.MarkCompletedAsync(-1, stepId, completedAtUtcMilliseconds);
        }

        private async Task<bool> SaveInternalAsync(CoreFindCatTutorialProgress progress)
        {
            if (_persistence == null)
            {
                return false;
            }

            try
            {
                string json = JsonUtility.ToJson(progress ?? new CoreFindCatTutorialProgress());
                byte[] bytes = Encoding.UTF8.GetBytes(json);
                return await _persistence.SaveAsync(PersistenceKey, bytes);
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> SaveMergedAsync(CoreFindCatTutorialProgress progress)
        {
            if (_persistence == null)
            {
                return false;
            }

            CoreFindCatTutorialProgress existing = await LoadAsync();
            CoreFindCatTutorialProgress merged = MergeMonotonic(existing, progress);
            return await SaveInternalAsync(merged);
        }

        private static CoreFindCatTutorialProgress MergeMonotonic(
            CoreFindCatTutorialProgress existing,
            CoreFindCatTutorialProgress incoming)
        {
            existing = CoreFindCatTutorialProgressService.Normalize(existing);
            incoming = CoreFindCatTutorialProgressService.Normalize(incoming);

            if (existing.completed && !incoming.completed)
            {
                incoming.completed = true;
                incoming.completedAtUtcMilliseconds = Math.Max(
                    existing.completedAtUtcMilliseconds,
                    incoming.completedAtUtcMilliseconds);
                incoming.completedStepIndex = Math.Max(existing.completedStepIndex, incoming.completedStepIndex);
                incoming.completedStepId = string.IsNullOrWhiteSpace(incoming.completedStepId)
                    ? existing.completedStepId
                    : incoming.completedStepId;
                if (incoming.currentStepIndex < existing.currentStepIndex ||
                    !string.Equals(incoming.currentStepId, incoming.completedStepId, StringComparison.Ordinal))
                {
                    incoming.currentStepIndex = Math.Max(existing.currentStepIndex, incoming.completedStepIndex);
                    incoming.currentStepId = string.IsNullOrWhiteSpace(incoming.completedStepId)
                        ? existing.currentStepId
                        : incoming.completedStepId;
                    incoming.lastStepId = incoming.currentStepId;
                }
            }

            incoming.started = incoming.started || existing.started || incoming.completed;
            incoming.skipped = incoming.skipped || existing.skipped;
            incoming.dismissedNormalBoardHint = incoming.dismissedNormalBoardHint || existing.dismissedNormalBoardHint;
            incoming.startedAtUtcMilliseconds = incoming.startedAtUtcMilliseconds > 0L
                ? incoming.startedAtUtcMilliseconds
                : existing.startedAtUtcMilliseconds;
            incoming.updatedAtUtcMilliseconds = Math.Max(existing.updatedAtUtcMilliseconds, incoming.updatedAtUtcMilliseconds);
            return CoreFindCatTutorialProgressService.Normalize(incoming);
        }
    }

    public sealed class CoreFindCatTutorialProgressService
    {
        private readonly CoreFindCatTutorialProgressStore _store;
        private readonly object _saveGate = new object();
        private Task _saveTail = Task.CompletedTask;

        public CoreFindCatTutorialProgressService(CoreFindCatTutorialProgressStore store)
        {
            _store = store;
        }

        public Task<CoreFindCatTutorialProgress> LoadAsync()
        {
            return _store != null ? LoadNormalizedAsync() : Task.FromResult(new CoreFindCatTutorialProgress());
        }

        public Task<bool> MarkStartedAsync(int stepIndex, string stepId, bool force)
        {
            return EnqueueMutationAsync(progress =>
            {
                if (force)
                {
                    progress.completed = false;
                    progress.skipped = false;
                    progress.completedStepIndex = -1;
                    progress.completedStepId = string.Empty;
                    progress.completedAtUtcMilliseconds = 0L;
                }

                progress.started = true;
                long now = Now();
                if (progress.startedAtUtcMilliseconds <= 0L || force)
                {
                    progress.startedAtUtcMilliseconds = now;
                }

                SetCurrentStep(progress, stepIndex, stepId, force);
                Touch(progress, now);
            });
        }

        public Task<bool> MarkCurrentStepAsync(int stepIndex, string stepId)
        {
            return EnqueueMutationAsync(progress =>
            {
                if (progress.completed)
                {
                    return;
                }

                progress.started = true;
                SetCurrentStep(progress, stepIndex, stepId, force: false);
                Touch(progress, Now());
            });
        }

        public Task<bool> MarkStepCompletedAsync(int stepIndex, string stepId)
        {
            return EnqueueMutationAsync(progress =>
            {
                if (progress.completed)
                {
                    return;
                }

                progress.started = true;
                if (stepIndex >= progress.completedStepIndex)
                {
                    progress.completedStepIndex = stepIndex;
                    progress.completedStepId = stepId ?? string.Empty;
                }

                Touch(progress, Now());
            });
        }

        public Task<bool> MarkSkippedAsync(int stepIndex, string stepId)
        {
            return EnqueueMutationAsync(progress =>
            {
                progress.started = true;
                progress.skipped = true;
                MarkCompleted(progress, stepIndex, stepId, Now());
            });
        }

        public Task<bool> MarkCompletedAsync(int stepIndex, string stepId)
        {
            return MarkCompletedAsync(stepIndex, stepId, Now());
        }

        public Task<bool> MarkCompletedAsync(int stepIndex, string stepId, long completedAtUtcMilliseconds)
        {
            return EnqueueMutationAsync(progress =>
            {
                progress.started = true;
                MarkCompleted(progress, stepIndex, stepId, completedAtUtcMilliseconds);
            });
        }

        public Task<bool> MarkNormalBoardHintDismissedAsync()
        {
            return EnqueueMutationAsync(progress =>
            {
                if (!progress.completed)
                {
                    progress.dismissedNormalBoardHint = true;
                    Touch(progress, Now());
                }
            });
        }

        public static CoreFindCatTutorialProgress Normalize(CoreFindCatTutorialProgress progress)
        {
            if (progress == null)
            {
                progress = new CoreFindCatTutorialProgress();
            }

            progress.tutorialId = string.IsNullOrWhiteSpace(progress.tutorialId)
                ? CoreFindCatTutorialBoardDefinition.MapId
                : progress.tutorialId;
            progress.version = Math.Max(1, progress.version);
            progress.currentStepId = progress.currentStepId ?? string.Empty;
            progress.completedStepId = progress.completedStepId ?? string.Empty;
            progress.lastStepId = progress.lastStepId ?? string.Empty;

            if (string.IsNullOrWhiteSpace(progress.currentStepId) && !string.IsNullOrWhiteSpace(progress.lastStepId))
            {
                progress.currentStepId = progress.lastStepId;
            }

            if (string.IsNullOrWhiteSpace(progress.lastStepId) && !string.IsNullOrWhiteSpace(progress.currentStepId))
            {
                progress.lastStepId = progress.currentStepId;
            }

            if (progress.completed)
            {
                progress.started = true;
                if (progress.completedAtUtcMilliseconds < 0L)
                {
                    progress.completedAtUtcMilliseconds = 0L;
                }
            }

            progress.currentStepIndex = Math.Max(-1, progress.currentStepIndex);
            progress.completedStepIndex = Math.Max(-1, progress.completedStepIndex);
            progress.startedAtUtcMilliseconds = Math.Max(0L, progress.startedAtUtcMilliseconds);
            progress.updatedAtUtcMilliseconds = Math.Max(0L, progress.updatedAtUtcMilliseconds);
            progress.completedAtUtcMilliseconds = Math.Max(0L, progress.completedAtUtcMilliseconds);
            return progress;
        }

        private async Task<CoreFindCatTutorialProgress> LoadNormalizedAsync()
        {
            CoreFindCatTutorialProgress progress = await _store.LoadAsync();
            return Normalize(progress);
        }

        private Task<bool> EnqueueMutationAsync(Action<CoreFindCatTutorialProgress> mutate)
        {
            lock (_saveGate)
            {
                Task<bool> saveTask = _saveTail
                    .ContinueWith(_ => MutateAndSaveAsync(mutate), TaskScheduler.Default)
                    .Unwrap();
                _saveTail = saveTask.ContinueWith(_ => { }, TaskScheduler.Default);
                return saveTask;
            }
        }

        private async Task<bool> MutateAndSaveAsync(Action<CoreFindCatTutorialProgress> mutate)
        {
            CoreFindCatTutorialProgress progress = await LoadAsync();
            mutate?.Invoke(progress);
            return _store != null && await _store.SaveAsync(progress);
        }

        private static void SetCurrentStep(
            CoreFindCatTutorialProgress progress,
            int stepIndex,
            string stepId,
            bool force)
        {
            int safeIndex = Math.Max(-1, stepIndex);
            if (force || safeIndex >= progress.currentStepIndex)
            {
                progress.currentStepIndex = safeIndex;
                progress.currentStepId = stepId ?? string.Empty;
                progress.lastStepId = progress.currentStepId;
            }
        }

        private static void MarkCompleted(
            CoreFindCatTutorialProgress progress,
            int stepIndex,
            string stepId,
            long completedAtUtcMilliseconds)
        {
            progress.completed = true;
            progress.completedAtUtcMilliseconds = Math.Max(progress.completedAtUtcMilliseconds, completedAtUtcMilliseconds);
            int safeIndex = Math.Max(progress.completedStepIndex, stepIndex);
            progress.completedStepIndex = safeIndex;
            progress.completedStepId = string.IsNullOrWhiteSpace(stepId) ? progress.completedStepId : stepId;
            SetCurrentStep(progress, safeIndex, progress.completedStepId, force: true);
            Touch(progress, progress.completedAtUtcMilliseconds);
        }

        private static void Touch(CoreFindCatTutorialProgress progress, long timestamp)
        {
            progress.updatedAtUtcMilliseconds = Math.Max(progress.updatedAtUtcMilliseconds, timestamp);
        }

        private static long Now()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }
}
