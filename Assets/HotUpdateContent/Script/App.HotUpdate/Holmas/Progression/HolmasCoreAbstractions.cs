using System;

namespace App.HotUpdate.Holmas.Progression
{
    /// <summary>
    /// 统一的 UTC 毫秒时钟接口。
    /// 任务到期、广告解锁和离线结算都依赖它，避免直接散落 DateTime.UtcNow。
    /// </summary>
    public interface IHolmasUtcClock
    {
        long UtcNowMilliseconds { get; }
    }

    /// <summary>
    /// 默认系统时钟实现。
    /// </summary>
    public sealed class HolmasSystemUtcClock : IHolmasUtcClock
    {
        public long UtcNowMilliseconds => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    /// <summary>
    /// 统一的随机源接口。
    /// 任务抽取和权重选择都通过这个接口注入，方便后续测试和回放。
    /// </summary>
    public interface IHolmasRandomSource
    {
        int Next(int maxExclusive);
        int Next(int minInclusive, int maxExclusive);
        double NextDouble();
    }

    /// <summary>
    /// 默认随机源实现。
    /// </summary>
    public sealed class HolmasSystemRandomSource : IHolmasRandomSource
    {
        private readonly Random _random;

        public HolmasSystemRandomSource()
            : this(Environment.TickCount)
        {
        }

        public HolmasSystemRandomSource(int seed)
        {
            _random = new Random(seed);
        }

        public int Next(int maxExclusive)
        {
            return _random.Next(maxExclusive);
        }

        public int Next(int minInclusive, int maxExclusive)
        {
            return _random.Next(minInclusive, maxExclusive);
        }

        public double NextDouble()
        {
            return _random.NextDouble();
        }
    }

    /// <summary>
    /// 权重抽取的通用工具。
    /// </summary>
    public static class HolmasWeightedPicker
    {
        public static int PickIndex(int[] weights, IHolmasRandomSource randomSource)
        {
            if (weights == null || weights.Length == 0 || randomSource == null)
            {
                return -1;
            }

            int total = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                if (weights[i] > 0)
                {
                    total += weights[i];
                }
            }

            if (total <= 0)
            {
                return -1;
            }

            int roll = randomSource.Next(total);
            int accumulated = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                if (weights[i] <= 0)
                {
                    continue;
                }

                accumulated += weights[i];
                if (roll < accumulated)
                {
                    return i;
                }
            }

            return weights.Length - 1;
        }
    }

    /// <summary>
    /// 广告解锁的到期策略。
    /// </summary>
    public interface IHolmasAdUnlockPolicy
    {
        long GetUnlockExpireAt(App.HotUpdate.Holmas.Meta.HolmasMetaProgressionState state, long nowUtcMilliseconds);
    }

    /// <summary>
    /// 经验来源接口。
    /// 当前 v1 里保留为兼容口径，真正玩家经验由建筑升级驱动。
    /// 任务领奖和地图结算的实现应返回 0，避免继续沿旧路径直接增长玩家经验。
    /// </summary>
    public interface IHolmasExperienceSource
    {
        long GetTaskClaimExperience(App.Shared.Holmas.RuntimeData.TaskInstanceData task);
        long GetMapCompletionExperience(HolmasMapCompletionReport report);
        long GetOfflineExperience(App.HotUpdate.Holmas.Meta.HolmasMetaProgressionState state, long offlineMilliseconds);
    }

    /// <summary>
    /// 离线收益接口。
    /// 当前 v1 里返回的是金币收益，不再直接提供玩家经验。
    /// </summary>
    public interface IHolmasOfflineRewardSource
    {
        long GetOfflineReward(App.HotUpdate.Holmas.Meta.HolmasMetaProgressionState state, long offlineMilliseconds);
    }

    /// <summary>
    /// 地图结算的汇总信息。
    /// </summary>
    [Serializable]
    public sealed class HolmasMapCompletionReport
    {
        public int CompletedMapCount;
        public int SpawnedCatCount;
        public int UniqueCatCount;
        public long UtcMilliseconds;
    }
}
