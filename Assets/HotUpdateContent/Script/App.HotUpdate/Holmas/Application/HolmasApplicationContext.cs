using System.Threading.Tasks;
using App.HotUpdate.Holmas.Board;
using App.HotUpdate.Holmas.Levels;
using App.HotUpdate.Holmas.Meta;
using App.HotUpdate.Holmas.Progression;
using App.HotUpdate.Holmas.Tasks.Runtime;
using App.HotUpdate.Holmas.Tasks.Services;
using App.Shared.Contracts;
using HolmasAgencyPromotionUpgradeResult = App.HotUpdate.Holmas.Meta.HolmasAgencyUpgradeResult;

namespace App.HotUpdate.Holmas.Application
{
    /// <summary>
    /// Holmas 业务骨架的应用上下文。
    /// 这层只集中保存当前已经确认的跨层基础设施依赖，供后续地图线、任务线继续挂接。
    /// </summary>
    public sealed class HolmasApplicationContext
    {
        /// <summary>
        /// 初始化 Holmas 应用上下文。
        /// 这些依赖都来自 AOT 提供的基础设施，本轮先只做接线，不实现具体玩法。
        /// </summary>
        public HolmasApplicationContext(
            IServiceContainer serviceContainer,
            IAppLogger logger,
            ITickManager tickManager,
            IEventBus eventBus,
            IAssetsRuntime assetsRuntime,
            HolmasGameplayRuntime gameplayRuntime)
        {
            ServiceContainer = serviceContainer;
            Logger = logger;
            TickManager = tickManager;
            EventBus = eventBus;
            AssetsRuntime = assetsRuntime;
            GameplayRuntime = gameplayRuntime;
        }

        /// <summary>
        /// 跨层服务容器。
        /// 后续模块若要继续扩展服务挂接关系，可以从这里统一取基础依赖。
        /// </summary>
        public IServiceContainer ServiceContainer { get; }

        /// <summary>
        /// 全局日志接口。
        /// HotUpdate 层通过它输出业务启动和运行信息，而不是自己持有 AOT 具体实现。
        /// </summary>
        public IAppLogger Logger { get; }

        /// <summary>
        /// Tick 管理器。
        /// 后续地图、任务等运行时服务若需要逐帧更新，可以在骨架稳定后从这里接入。
        /// </summary>
        public ITickManager TickManager { get; }

        /// <summary>
        /// 事件总线接口。
        /// 后续地图完成、任务推进等跨模块通知，会在统一事件设计后继续挂到这里。
        /// </summary>
        public IEventBus EventBus { get; }

        /// <summary>
        /// 正式运行时资源入口。
        /// terrain、图标等正式资源后续都应该通过它加载，不直接绕过到 Resources。
        /// </summary>
        public IAssetsRuntime AssetsRuntime { get; }

        /// <summary>
        /// 当前阶段的 Holmas 运行时编排入口。
        /// 在不接 UI 的前提下，外层也可以通过它驱动关卡、任务和长期进度。
        /// </summary>
        public HolmasGameplayRuntime GameplayRuntime { get; }

        /// <summary>
        /// 当前玩家等级。
        /// </summary>
        public int CurrentPlayerLevel => GameplayRuntime?.CurrentPlayerLevel ?? 1;

        /// <summary>
        /// 当前侦探社阶段。
        /// </summary>
        public int CurrentAgencyStageId => GameplayRuntime?.CurrentAgencyStageId ?? 1;

        /// <summary>
        /// 当前金币余额。
        /// </summary>
        public long CurrentGoldBalance => GameplayRuntime?.CurrentGoldBalance ?? 0L;

        /// <summary>
        /// 当前体力。
        /// </summary>
        public int CurrentEnergy => GameplayRuntime?.CurrentEnergy ?? HolmasGameplayRuntime.DefaultEnergyRecoveryLimit;

        /// <summary>
        /// 当前体力自然恢复上限。
        /// </summary>
        public int EnergyRecoveryLimit => GameplayRuntime?.EnergyRecoveryLimit ?? HolmasGameplayRuntime.DefaultEnergyRecoveryLimit;

        /// <summary>
        /// 体力显示文本。
        /// </summary>
        public string EnergyLabel => GameplayRuntime?.EnergyLabel ?? $"{HolmasGameplayRuntime.DefaultEnergyRecoveryLimit}/{HolmasGameplayRuntime.DefaultEnergyRecoveryLimit}";

        /// <summary>
        /// 按 TerrainPath 启动一局地图。
        /// 组合层先通过正式资源入口加载地形，再交给 HotUpdate 业务逻辑生成运行时棋盘。
        /// </summary>
        public Task<BoardRuntime> StartLevelAsync(LevelGenerationRequest request)
        {
            if (AssetsRuntime == null)
            {
                throw new System.InvalidOperationException("HolmasApplicationContext: 当前没有可用的 IAssetsRuntime。");
            }

            if (GameplayRuntime == null)
            {
                throw new System.InvalidOperationException("HolmasApplicationContext: 当前没有可用的 HolmasGameplayRuntime。");
            }

            return GameplayRuntime.StartLevelAsync(request);
        }

        /// <summary>
        /// 按当前玩家等级补齐所有已解锁空槽位。
        /// </summary>
        public HolmasTaskRefillResult RefillAvailableTasks()
        {
            if (GameplayRuntime == null)
            {
                throw new System.InvalidOperationException("HolmasApplicationContext: 当前没有可用的 HolmasGameplayRuntime。");
            }

            return GameplayRuntime.RefillAvailableTasks();
        }

        public HolmasTaskSettlementResult SettleClaimableTasksAndRefill()
        {
            if (GameplayRuntime == null)
            {
                throw new System.InvalidOperationException("HolmasApplicationContext: 当前没有可用的 HolmasGameplayRuntime。");
            }

            return GameplayRuntime.SettleClaimableTasksAndRefill();
        }

        /// <summary>
        /// 使用当前玩家等级与当前成长配置解锁一个广告槽位。
        /// </summary>
        public HolmasTaskSlotUnlockResult UnlockAdSlot(int slotIndex, long nowUtcMilliseconds)
        {
            if (GameplayRuntime == null)
            {
                throw new System.InvalidOperationException("HolmasApplicationContext: 当前没有可用的 HolmasGameplayRuntime。");
            }

            return GameplayRuntime.UnlockAdSlot(slotIndex, nowUtcMilliseconds);
        }

        /// <summary>
        /// 按当前玩家等级领取任务奖励。
        /// </summary>
        public HolmasTaskClaimResult ClaimTaskReward(int slotIndex)
        {
            if (GameplayRuntime == null)
            {
                throw new System.InvalidOperationException("HolmasApplicationContext: 当前没有可用的 HolmasGameplayRuntime。");
            }

            return GameplayRuntime.ClaimTaskReward(slotIndex);
        }

        /// <summary>
        /// 按 UTC 时间刷新自然恢复体力。
        /// </summary>
        public bool RefreshEnergyRecovery()
        {
            if (GameplayRuntime == null)
            {
                throw new System.InvalidOperationException("HolmasApplicationContext: 当前没有可用的 HolmasGameplayRuntime。");
            }

            return GameplayRuntime.RefreshEnergyRecovery();
        }

        /// <summary>
        /// 当前验证阶段的体力补给入口。
        /// </summary>
        public void AddEnergy(int amount = HolmasGameplayRuntime.DebugEnergyGrantAmount)
        {
            if (GameplayRuntime == null)
            {
                throw new System.InvalidOperationException("HolmasApplicationContext: 当前没有可用的 HolmasGameplayRuntime。");
            }

            GameplayRuntime.AddEnergy(amount);
        }

        /// <summary>
        /// 按当前成长配置升级城市宣传功能。
        /// </summary>
        public HolmasAgencyPromotionUpgradeResult TryUpgradePromotion(string promotionId)
        {
            if (GameplayRuntime == null)
            {
                throw new System.InvalidOperationException("HolmasApplicationContext: 当前没有可用的 HolmasGameplayRuntime。");
            }

            return GameplayRuntime.TryUpgradePromotion(promotionId);
        }

        /// <summary>
        /// 结算离线收益，当前版本只增加金币。
        /// </summary>
        public HolmasProgressionAdvanceResult ApplyOfflineSettlement(long offlineMilliseconds)
        {
            if (GameplayRuntime == null)
            {
                throw new System.InvalidOperationException("HolmasApplicationContext: 当前没有可用的 HolmasGameplayRuntime。");
            }

            return GameplayRuntime.ApplyOfflineSettlement(offlineMilliseconds);
        }

        /// <summary>
        /// 读取当前成长配置下，广告槽位解锁的到期时间。
        /// </summary>
        public long GetAdUnlockExpireAt(long nowUtcMilliseconds)
        {
            if (GameplayRuntime == null)
            {
                throw new System.InvalidOperationException("HolmasApplicationContext: 当前没有可用的 HolmasGameplayRuntime。");
            }

            return GameplayRuntime.GetAdUnlockExpireAt(nowUtcMilliseconds);
        }
    }
}
