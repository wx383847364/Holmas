using System.Collections.Generic;
using App.Shared.Holmas.RuntimeData;
using App.HotUpdate.Holmas.Meta;
using App.HotUpdate.Holmas.Tasks.Runtime;
using App.HotUpdate.Holmas.Tasks.Services;

namespace App.HotUpdate.Holmas.Progression
{
    /// <summary>
    /// 地图完成后的统一推进结果。
    /// </summary>
    public sealed class HolmasProgressionAdvanceResult
    {
        public long TaskExperienceGained;
        public long MetaExperienceGained;
        public long OfflineRewardGained;
        public readonly List<string> CompletedTaskIds = new List<string>();
        public readonly List<string> ProgressedTaskIds = new List<string>();
    }

    /// <summary>
    /// 将任务推进和长期成长收拢在一起的协调器。
    /// 后续 Agent 2 的地图结算可以直接调用它，避免到处散逻辑。
    /// </summary>
    public sealed class HolmasProgressionCoordinator
    {
        private readonly HolmasTaskProgressService _taskProgressService;
        private readonly HolmasMetaProgressionService _metaProgressionService;

        public HolmasProgressionCoordinator(
            HolmasTaskProgressService taskProgressService,
            HolmasMetaProgressionService metaProgressionService)
        {
            _taskProgressService = taskProgressService;
            _metaProgressionService = metaProgressionService;
        }

        public HolmasProgressionAdvanceResult ApplyMapCompletion(
            HolmasTaskBarState taskBarState,
            HolmasMetaProgressionState metaState,
            IEnumerable<SpawnedCatData> spawnedCats)
        {
            var result = new HolmasProgressionAdvanceResult();

            if (_taskProgressService != null)
            {
                var taskResult = _taskProgressService.ApplyMapCompletion(taskBarState, spawnedCats);
                if (taskResult != null)
                {
                    result.ProgressedTaskIds.AddRange(taskResult.ProgressedTaskIds);
                    result.CompletedTaskIds.AddRange(taskResult.NewlyCompletedTaskIds);
                }
            }

            if (_metaProgressionService != null)
            {
                result.MetaExperienceGained = _metaProgressionService.ApplyMapCompletion(metaState, spawnedCats);
            }

            return result;
        }

        public long ApplyTaskClaim(HolmasTaskRuntimeInstance task, HolmasMetaProgressionState metaState)
        {
            if (_metaProgressionService == null || task == null)
            {
                return 0L;
            }

            return _metaProgressionService.ApplyTaskClaim(metaState, task.Task);
        }
    }
}
