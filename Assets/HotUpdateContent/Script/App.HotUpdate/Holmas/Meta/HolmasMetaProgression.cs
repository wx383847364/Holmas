using System;
using System.Collections.Generic;
using System.Linq;
using App.HotUpdate.Holmas.Progression;
using App.HotUpdate.Holmas.Tasks.Config;
using App.Shared.Holmas.RuntimeData;

namespace App.HotUpdate.Holmas.Meta
{
    /// <summary>
    /// 长期进度运行时状态。
    /// 当前 v1 以玩家等级、城市宣传阶段、金币和宣传等级为主。
    /// </summary>
    [Serializable]
    public sealed class HolmasMetaProgressionState
    {
        public HolmasMetaProgressionState()
        {
            PlayerLevel = 1;
            AgencyStageId = 1;
        }

        public long Experience;
        public int PlayerLevel;
        public int AgencyStageId;
        public long GoldBalance;
        public int CompletedMapCount;
        public int ClaimedTaskCount;
        public long OfflineRewardTotal;
        public long LastOfflineSettlementAtUtcMilliseconds;
        public bool EnergyInitialized;
        public int EnergyCurrent;
        public int EnergyRecoveryLimit;
        public long EnergyLastRecoveryAtUtcMilliseconds;
        public readonly Dictionary<string, int> CatDiscoveryCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        public readonly Dictionary<string, int> PromotionLevels = new Dictionary<string, int>(StringComparer.Ordinal);

        public int GetPromotionLevel(string promotionId)
        {
            if (string.IsNullOrWhiteSpace(promotionId))
            {
                return 0;
            }

            return PromotionLevels.TryGetValue(promotionId, out int currentLevel) ? currentLevel : 0;
        }

        public void SetPromotionLevel(string promotionId, int level)
        {
            if (string.IsNullOrWhiteSpace(promotionId))
            {
                return;
            }

            if (level <= 0)
            {
                PromotionLevels.Remove(promotionId);
                return;
            }

            PromotionLevels[promotionId] = level;
        }
    }

    public static class HolmasAgencyPromotionStateKey
    {
        private const string Separator = "::";

        public static string Build(int agencyStageId, string promotionId)
        {
            return Math.Max(1, agencyStageId) + Separator + (promotionId ?? string.Empty);
        }

        public static int GetLevel(HolmasMetaProgressionState state, int agencyStageId, string promotionId)
        {
            if (state == null)
            {
                return 0;
            }

            int scopedLevel = state.GetPromotionLevel(Build(agencyStageId, promotionId));
            if (scopedLevel > 0)
            {
                return scopedLevel;
            }

            return Math.Max(1, agencyStageId) == 1
                ? state.GetPromotionLevel(promotionId)
                : 0;
        }

        public static void SetLevel(HolmasMetaProgressionState state, int agencyStageId, string promotionId, int level)
        {
            if (state == null)
            {
                return;
            }

            state.SetPromotionLevel(Build(agencyStageId, promotionId), level);
            if (Math.Max(1, agencyStageId) == 1)
            {
                state.SetPromotionLevel(promotionId, 0);
            }
        }
    }

    /// <summary>
    /// 玩家等级长期成长配置。
    /// </summary>
    [Serializable]
    public sealed class HolmasMetaProgressionDefinition
    {
        public int PlayerLevel;
        public int MinExperience;
        public int OfflineRewardPerHour;
        public int AdUnlockHours = 24;
    }

    /// <summary>
    /// 长期成长配置仓库。
    /// </summary>
    public interface IHolmasMetaCatalog
    {
        bool TryGetPlayerLevel(int playerLevel, out HolmasMetaProgressionDefinition definition);
    }

    /// <summary>
    /// 纯内存版长期成长配置仓库。
    /// </summary>
    public sealed class HolmasMetaCatalog : IHolmasMetaCatalog
    {
        private readonly Dictionary<int, HolmasMetaProgressionDefinition> _playerLevels = new Dictionary<int, HolmasMetaProgressionDefinition>();

        public HolmasMetaCatalog()
        {
        }

        public HolmasMetaCatalog(IEnumerable<HolmasMetaProgressionDefinition> playerLevels)
        {
            SetPlayerLevels(playerLevels);
        }

        public void SetPlayerLevels(IEnumerable<HolmasMetaProgressionDefinition> playerLevels)
        {
            _playerLevels.Clear();
            if (playerLevels == null)
            {
                return;
            }

            foreach (HolmasMetaProgressionDefinition item in playerLevels.Where(level => level != null))
            {
                if (item.PlayerLevel <= 0)
                {
                    continue;
                }

                _playerLevels[item.PlayerLevel] = item;
            }
        }

        public bool TryGetPlayerLevel(int playerLevel, out HolmasMetaProgressionDefinition definition)
        {
            return _playerLevels.TryGetValue(playerLevel, out definition);
        }
    }

    /// <summary>
    /// 默认的长期成长策略。
    /// 当前 v1 不再把任务/地图直接换算成玩家经验。
    /// 任务领奖和离线结算改为金币来源，玩家经验只由宣传升级驱动。
    /// </summary>
    public sealed class HolmasDefaultMetaExperienceSource : IHolmasExperienceSource, IHolmasOfflineRewardSource, IHolmasAdUnlockPolicy
    {
        private const int DefaultOfflineRewardPerHour = 6;
        private readonly IHolmasMetaCatalog _catalog;

        public HolmasDefaultMetaExperienceSource(IHolmasMetaCatalog catalog = null)
        {
            _catalog = catalog;
        }

        public long GetTaskClaimExperience(TaskInstanceData task)
        {
            return 0L;
        }

        public long GetMapCompletionExperience(HolmasMapCompletionReport report)
        {
            return 0L;
        }

        public long GetOfflineExperience(HolmasMetaProgressionState state, long offlineMilliseconds)
        {
            return 0L;
        }

        public long GetOfflineReward(HolmasMetaProgressionState state, long offlineMilliseconds)
        {
            if (offlineMilliseconds <= 0)
            {
                return 0L;
            }

            HolmasMetaProgressionDefinition definition = ResolveDefinition(state);
            int rewardPerHour = definition != null
                ? Math.Max(0, definition.OfflineRewardPerHour)
                : DefaultOfflineRewardPerHour;
            return (offlineMilliseconds * rewardPerHour) / (1000L * 60L * 60L);
        }

        public long GetUnlockExpireAt(HolmasMetaProgressionState state, long nowUtcMilliseconds)
        {
            HolmasMetaProgressionDefinition definition = ResolveDefinition(state);
            int unlockHours = definition != null
                ? Math.Max(1, definition.AdUnlockHours)
                : 24;
            return nowUtcMilliseconds + unlockHours * 60L * 60L * 1000L;
        }

        public long GetUnlockExpireAt(long nowUtcMilliseconds)
        {
            return GetUnlockExpireAt(null, nowUtcMilliseconds);
        }

        private HolmasMetaProgressionDefinition ResolveDefinition(HolmasMetaProgressionState state)
        {
            if (_catalog == null)
            {
                return null;
            }

            int playerLevel = 1;
            if (state != null)
            {
                playerLevel = Math.Max(1, state.PlayerLevel);
            }

            if (_catalog.TryGetPlayerLevel(playerLevel, out HolmasMetaProgressionDefinition definition) && definition != null)
            {
                return definition;
            }

            return null;
        }
    }

    /// <summary>
    /// 长期进度服务。
    /// 负责玩家等级、金币、城市宣传成长、离线收益与地图结算累计。
    /// </summary>
    public sealed class HolmasMetaProgressionService
    {
        private IHolmasMetaCatalog _catalog;
        private readonly IHolmasExperienceSource _experienceSource;
        private readonly IHolmasOfflineRewardSource _offlineRewardSource;
        private readonly IHolmasUtcClock _clock;

        public HolmasMetaProgressionService(
            IHolmasMetaCatalog catalog,
            IHolmasTaskCatalog playerLevelCatalog,
            IHolmasExperienceSource experienceSource,
            IHolmasOfflineRewardSource offlineRewardSource,
            IHolmasUtcClock clock = null)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _ = playerLevelCatalog ?? throw new ArgumentNullException(nameof(playerLevelCatalog));
            _experienceSource = experienceSource ?? throw new ArgumentNullException(nameof(experienceSource));
            _offlineRewardSource = offlineRewardSource ?? throw new ArgumentNullException(nameof(offlineRewardSource));
            _clock = clock;
        }

        public void SetCatalog(IHolmasMetaCatalog catalog)
        {
            _catalog = catalog ?? _catalog;
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
            long gold = Math.Max(0L, task.Reward);
            state.GoldBalance += gold;
            return gold;
        }

        public long ApplyMapCompletion(HolmasMetaProgressionState state, IEnumerable<SpawnedCatData> spawnedCats)
        {
            if (state == null)
            {
                return 0L;
            }

            SpawnedCatData[] spawnedList = spawnedCats == null
                ? Array.Empty<SpawnedCatData>()
                : spawnedCats.Where(item => item != null).ToArray();

            var report = new HolmasMapCompletionReport
            {
                CompletedMapCount = 1,
                SpawnedCatCount = spawnedList.Length,
                UniqueCatCount = spawnedList.Where(item => !string.IsNullOrEmpty(item.CatId)).Select(item => item.CatId).Distinct(StringComparer.Ordinal).Count()
            };

            state.CompletedMapCount += 1;
            foreach (SpawnedCatData cat in spawnedList)
            {
                if (cat == null || string.IsNullOrEmpty(cat.CatId))
                {
                    continue;
                }

                state.CatDiscoveryCounts.TryGetValue(cat.CatId, out int current);
                state.CatDiscoveryCounts[cat.CatId] = current + 1;
            }

            _experienceSource.GetMapCompletionExperience(report);
            return 0L;
        }

        public long ApplyOfflineSettlement(HolmasMetaProgressionState state, long offlineMilliseconds)
        {
            if (state == null)
            {
                return 0L;
            }

            long reward = GetOfflineReward(state, offlineMilliseconds);
            state.OfflineRewardTotal += reward;
            state.GoldBalance += reward;
            if (_clock != null && reward > 0)
            {
                state.LastOfflineSettlementAtUtcMilliseconds = _clock.UtcNowMilliseconds;
            }

            return reward;
        }

        public long GetOfflineReward(HolmasMetaProgressionState state, long offlineMilliseconds)
        {
            if (state == null || offlineMilliseconds <= 0)
            {
                return 0L;
            }

            if (TryGetLevelDefinition(state, out HolmasMetaProgressionDefinition definition))
            {
                long rewardPerHour = Math.Max(0, definition.OfflineRewardPerHour);
                return (offlineMilliseconds * rewardPerHour) / (1000L * 60L * 60L);
            }

            return _offlineRewardSource?.GetOfflineReward(state, offlineMilliseconds) ?? 0L;
        }

        public long GetUnlockExpireAt(HolmasMetaProgressionState state, long nowUtcMilliseconds)
        {
            if (state != null && TryGetLevelDefinition(state, out HolmasMetaProgressionDefinition definition))
            {
                int unlockHours = Math.Max(1, definition.AdUnlockHours);
                return nowUtcMilliseconds + unlockHours * 60L * 60L * 1000L;
            }

            if (_offlineRewardSource is IHolmasAdUnlockPolicy unlockPolicy)
            {
                return unlockPolicy.GetUnlockExpireAt(state, nowUtcMilliseconds);
            }

            return nowUtcMilliseconds + 24L * 60L * 60L * 1000L;
        }

        public void ApplyExperience(HolmasMetaProgressionState state, long experience)
        {
            if (state == null || experience <= 0)
            {
                return;
            }

            state.Experience += experience;
            RecalculatePlayerLevel(state);
        }

        public void RecalculatePlayerLevel(HolmasMetaProgressionState state)
        {
            if (state == null)
            {
                return;
            }

            int currentLevel = Math.Max(1, state.PlayerLevel);
            int nextLevel = currentLevel;

            while (_catalog.TryGetPlayerLevel(nextLevel + 1, out HolmasMetaProgressionDefinition nextDefinition))
            {
                if (nextDefinition == null || state.Experience < Math.Max(0, nextDefinition.MinExperience))
                {
                    break;
                }

                nextLevel++;
            }

            state.PlayerLevel = nextLevel;
        }

        private bool TryGetLevelDefinition(HolmasMetaProgressionState state, out HolmasMetaProgressionDefinition definition)
        {
            definition = null;

            if (state == null || _catalog == null)
            {
                return false;
            }

            int effectiveLevel = Math.Max(1, state.PlayerLevel);
            return _catalog.TryGetPlayerLevel(effectiveLevel, out definition) && definition != null;
        }
    }
}
