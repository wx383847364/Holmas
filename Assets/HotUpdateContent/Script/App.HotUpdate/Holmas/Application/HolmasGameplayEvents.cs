namespace App.HotUpdate.Holmas.Application
{
    /// <summary>
    /// Holmas 玩法运行时的通用状态变化事件。
    /// </summary>
    /// <remarks>
    /// 每次 HolmasGameplayRuntime.NotifyStateChanged 被调用时都会发布这个事件。
    /// 它相当于旧 StateChanged(reason) 的“事件总线版”，但额外带了一组轻量快照字段。
    ///
    /// 使用场景：
    /// - 后续 UI 或调试面板只想知道“玩法状态变了”，不关心具体子事件时可以订阅它。
    /// - 新旧系统迁移期间，它可以作为兼容总事件，避免一开始就为每个 reason 都定义专用事件。
    ///
    /// 设计边界：
    /// - 这里不放完整 Runtime、BoardRuntime、TaskBarState 等大对象，避免订阅者拿到可变业务对象后互相影响。
    /// - 只提供 UI 和调试常用的轻快照字段；需要更精确语义时订阅下面的专用事件。
    /// </remarks>
    public sealed class HolmasGameplayStateChangedEvent
    {
        /// <summary>
        /// 触发本次状态变化的原始 reason，和旧 StateChanged 参数保持一致。
        /// </summary>
        public HolmasGameplayRuntimeStateChangeReason Reason { get; set; }

        /// <summary>
        /// 当前体力值。
        /// </summary>
        public int CurrentEnergy { get; set; }

        /// <summary>
        /// 当前体力自然恢复上限。
        /// </summary>
        public int EnergyRecoveryLimit { get; set; }

        /// <summary>
        /// UI 可直接显示的体力文本，例如 50/50。
        /// </summary>
        public string EnergyLabel { get; set; } = string.Empty;

        /// <summary>
        /// 最近一次任务奖励提示文案。
        /// </summary>
        public string TaskRewardTip { get; set; } = string.Empty;

        /// <summary>
        /// 任务奖励提示版本号，用于 UI 判断提示是否更新。
        /// </summary>
        public int TaskRewardTipVersion { get; set; }

        /// <summary>
        /// 当前任务栏内活跃任务数量。
        /// </summary>
        public int TaskTotalCount { get; set; }

        /// <summary>
        /// 当前可领奖任务数量。
        /// </summary>
        public int TaskClaimableCount { get; set; }

        /// <summary>
        /// 当前已解锁任务槽数量。
        /// </summary>
        public int TaskUnlockedSlotCount { get; set; }

        /// <summary>
        /// 当前关卡地图 ID；没有关卡时为空字符串。
        /// </summary>
        public string LevelMapId { get; set; } = string.Empty;

        /// <summary>
        /// 当前关卡 seed；没有关卡时为 0。
        /// </summary>
        public int LevelSeed { get; set; }

        /// <summary>
        /// 当前关卡是否已完成。
        /// </summary>
        public bool LevelCompleted { get; set; }
    }

    /// <summary>
    /// 体力变化专用事件。
    /// Battle 页面首轮试迁移订阅这个事件，用它刷新体力显示。
    /// </summary>
    public sealed class HolmasEnergyChangedEvent
    {
        /// <summary>
        /// 触发体力变化的 reason，当前通常是 EnergyChanged。
        /// </summary>
        public HolmasGameplayRuntimeStateChangeReason Reason { get; set; }

        /// <summary>
        /// 当前体力值。
        /// </summary>
        public int CurrentEnergy { get; set; }

        /// <summary>
        /// 当前体力自然恢复上限。
        /// </summary>
        public int EnergyRecoveryLimit { get; set; }

        /// <summary>
        /// UI 可直接显示的体力文本。
        /// </summary>
        public string EnergyLabel { get; set; } = string.Empty;
    }

    /// <summary>
    /// 任务奖励提示变化事件。
    /// 当任务自动领奖或结算领奖产生新提示时发布，UI 可直接拿 Tip 刷新状态文案。
    /// </summary>
    public sealed class HolmasTaskRewardTipChangedEvent
    {
        /// <summary>
        /// 触发奖励提示变化的 reason，当前通常是 TaskRewardClaimed。
        /// </summary>
        public HolmasGameplayRuntimeStateChangeReason Reason { get; set; }

        /// <summary>
        /// 奖励提示文案，例如“任务完成，金币 +10”。
        /// </summary>
        public string Tip { get; set; } = string.Empty;

        /// <summary>
        /// 提示版本号，递增后代表有新提示。
        /// </summary>
        public int Version { get; set; }
    }

    public sealed class HolmasLeaderboardTaskRewardClaimedEvent
    {
        public HolmasGameplayRuntimeStateChangeReason Reason { get; set; }
        public int RewardGold { get; set; }
    }

    public sealed class HolmasLeaderboardCatsFoundEvent
    {
        public HolmasGameplayRuntimeStateChangeReason Reason { get; set; }
        public int FoundCatCount { get; set; }
    }

    /// <summary>
    /// 任务栏结构或可领奖状态变化事件。
    /// 任务补齐、广告槽解锁/过期刷新、任务领奖后补位等场景会发布它。
    /// </summary>
    public sealed class HolmasTaskBarChangedEvent
    {
        /// <summary>
        /// 触发任务栏变化的 reason。
        /// </summary>
        public HolmasGameplayRuntimeStateChangeReason Reason { get; set; }

        /// <summary>
        /// 当前活跃任务数量。
        /// </summary>
        public int TotalTaskCount { get; set; }

        /// <summary>
        /// 当前可领奖任务数量。
        /// </summary>
        public int ClaimableTaskCount { get; set; }

        /// <summary>
        /// 当前已解锁任务槽数量。
        /// </summary>
        public int UnlockedSlotCount { get; set; }
    }

    /// <summary>
    /// 关卡状态变化事件。
    /// 关卡启动、翻格、标记、完成、结束当前 session 等场景会发布它。
    /// </summary>
    public sealed class HolmasLevelStateChangedEvent
    {
        /// <summary>
        /// 触发关卡状态变化的 reason。
        /// </summary>
        public HolmasGameplayRuntimeStateChangeReason Reason { get; set; }

        /// <summary>
        /// 当前地图 ID；没有关卡时为空。
        /// </summary>
        public string MapId { get; set; } = string.Empty;

        /// <summary>
        /// 当前关卡 seed；没有关卡时为 0。
        /// </summary>
        public int Seed { get; set; }

        /// <summary>
        /// 当前关卡是否已完成。
        /// </summary>
        public bool Completed { get; set; }
    }
}
