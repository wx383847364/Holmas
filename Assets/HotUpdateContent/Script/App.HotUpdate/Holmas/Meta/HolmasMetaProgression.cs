using System;
using System.Collections.Generic;
using System.Linq;
using App.Shared.Holmas.RuntimeData;
using App.HotUpdate.Holmas.Progression;

namespace App.HotUpdate.Holmas.Meta
{
    /// <summary>
    /// 长期进度运行时状态。
    /// 这里不承载任务栏本身，只负责侦探社成长、经验、离线收益等长期变量。
    /// </summary>
    [Serializable]
    public sealed class HolmasMetaProgressionState
    {
        public long Experience;
        public int AgencyLevel;
        public int CompletedMapCount;
        public int ClaimedTaskCount;
        public long OfflineRewardTotal;
        public long LastOfflineSettlementAtUtcMilliseconds;
        public readonly Dictionary<string, int> CatDiscoveryCounts = new Dictionary<string, int>(StringComparer.Ordinal);
    }

    /// <summary>
    /// 侦探社长期成长配置。
    /// </summary>
    [Serializable]
    public sealed class HolmasMetaProgressionDefinition
    {
        public int AgencyLevel;
        public long MinExperience;
    }

    /// <summary>
    /// 长期成长配置仓库。
    /// </summary>
    public interface IHolmasMetaCatalog
    {
        bool TryGetAgencyLevel(int agencyLevel, out HolmasMetaProgressionDefinition definition);
    }

    /// <summary>
    /// 纯内存版长期成长配置仓库。
    /// </summary>
    public sealed class HolmasMetaCatalog : IHolmasMetaCatalog
    {
        private readonly Dictionary<int, HolmasMetaProgressionDefinition> _agencyLevels = new Dictionary<int, HolmasMetaProgressionDefinition>();

        public HolmasMetaCatalog()
        {
        }

        public HolmasMetaCatalog(IEnumerable<HolmasMetaProgressionDefinition> agencyLevels)
        {
            SetAgencyLevels(agencyLevels);
        }

        public void SetAgencyLevels(IEnumerable<HolmasMetaProgressionDefinition> agencyLevels)
        {
            _agencyLevels.Clear();
            if (agencyLevels == null)
            {
                return;
            }

            foreach (var item in agencyLevels.Where(level => level != null))
            {
                _agencyLevels[item.AgencyLevel] = item;
            }
        }

        public bool TryGetAgencyLevel(int agencyLevel, out HolmasMetaProgressionDefinition definition)
        {
            return _agencyLevels.TryGetValue(agencyLevel, out definition);
        }
    }

    /// <summary>
    /// 默认的长期成长策略。
    /// 先给出清晰接口，后续可替换成更精细的数值表。
    /// </summary>
    public sealed class HolmasDefaultMetaExperienceSource : IHolmasExperienceSource, IHolmasOfflineRewardSource, IHolmasAdUnlockPolicy
    {
        private const long DefaultAdUnlockDurationMilliseconds = 24L * 60L * 60L * 1000L;

        public long GetTaskClaimExperience(TaskInstanceData task)
        {
            if (task == null)
            {
                return 0L;
            }

            return Math.Max(0L, task.Reward / 10L);
        }

        public long GetMapCompletionExperience(HolmasMapCompletionReport report)
        {
            if (report == null)
            {
                return 0L;
            }

            return Math.Max(0L, report.CompletedMapCount * 5L + report.SpawnedCatCount);
        }

        public long GetOfflineExperience(HolmasMetaProgressionState state, long offlineMilliseconds)
        {
            if (offlineMilliseconds <= 0)
            {
                return 0L;
            }

            return offlineMilliseconds / (1000L * 60L * 30L);
        }

        public long GetOfflineReward(HolmasMetaProgressionState state, long offlineMilliseconds)
        {
            if (offlineMilliseconds <= 0)
            {
                return 0L;
            }

            return offlineMilliseconds / (1000L * 60L * 10L);
        }

        public long GetUnlockExpireAt(long nowUtcMilliseconds)
        {
            return nowUtcMilliseconds + DefaultAdUnlockDurationMilliseconds;
        }
    }

    /// <summary>
    /// 长期进度服务。
    /// 负责经验、侦探社成长、离线收益与地图结算累计。
    /// </summary>
    public sealed class HolmasMetaProgressionService
    {
        private readonly IHolmasMetaCatalog _catalog;
        private readonly IHolmasExperienceSource _experienceSource;
        private readonly IHolmasOfflineRewardSource _offlineRewardSource;

        public HolmasMetaProgressionService(
            IHolmasMetaCatalog catalog,
            IHolmasExperienceSource experienceSource,
            IHolmasOfflineRewardSource offlineRewardSource)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _experienceSource = experienceSource ?? throw new ArgumentNullException(nameof(experienceSource));
            _offlineRewardSource = offlineRewardSource ?? throw new ArgumentNullException(nameof(offlineRewardSource));
        }

        public HolmasMetaProgressionState CreateState()
        {
            return new HolmasMetaProgressionState();
        }

        public long ApplyTaskClaim(HolmasMetaProgressionState state, TaskInstanceData task)
        {
            if (state == null || task == null)
            {
                return 0L;
            }

            state.ClaimedTaskCount++;
            long exp = _experienceSource.GetTaskClaimExperience(task);
            ApplyExperience(state, exp);
            return exp;
        }

        public long ApplyMapCompletion(HolmasMetaProgressionState state, IEnumerable<SpawnedCatData> spawnedCats)
        {
            if (state == null)
            {
                return 0L;
            }

            var spawnedList = spawnedCats == null ? Array.Empty<SpawnedCatData>() : spawnedCats.Where(item => item != null).ToArray();
            var report = new HolmasMapCompletionReport
            {
                CompletedMapCount = 1,
                SpawnedCatCount = spawnedList.Length,
                UniqueCatCount = spawnedList.Where(item => !string.IsNullOrEmpty(item.CatId)).Select(item => item.CatId).Distinct(StringComparer.Ordinal).Count()
            };

            state.CompletedMapCount += 1;
            foreach (var cat in spawnedList)
            {
                if (cat == null || string.IsNullOrEmpty(cat.CatId))
                {
                    continue;
                }

                int current;
                state.CatDiscoveryCounts.TryGetValue(cat.CatId, out current);
                state.CatDiscoveryCounts[cat.CatId] = current + 1;
            }

            long exp = _experienceSource.GetMapCompletionExperience(report);
            ApplyExperience(state, exp);
            return exp;
        }

        public long ApplyOfflineSettlement(HolmasMetaProgressionState state, long offlineMilliseconds)
        {
            if (state == null)
            {
                return 0L;
            }

            long reward = _offlineRewardSource.GetOfflineReward(state, offlineMilliseconds);
            state.OfflineRewardTotal += reward;
            return reward;
        }

        public void ApplyExperience(HolmasMetaProgressionState state, long experience)
        {
            if (state == null || experience <= 0)
            {
                return;
            }

            state.Experience += experience;
            RecalculateAgencyLevel(state);
        }

        public void RecalculateAgencyLevel(HolmasMetaProgressionState state)
        {
            if (state == null)
            {
                return;
            }

            int currentLevel = Math.Max(1, state.AgencyLevel);
            int nextLevel = currentLevel;

            while (_catalog.TryGetAgencyLevel(nextLevel + 1, out var nextDefinition))
            {
                if (nextDefinition == null)
                {
                    break;
                }

                if (state.Experience < nextDefinition.MinExperience)
                {
                    break;
                }

                nextLevel++;
            }

            state.AgencyLevel = nextLevel;
        }
    }
}
