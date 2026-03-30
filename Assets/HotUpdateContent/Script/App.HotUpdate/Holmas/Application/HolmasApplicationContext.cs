using System.Threading.Tasks;
using App.HotUpdate.Holmas.Board;
using App.HotUpdate.Holmas.Levels;
using App.Shared.Contracts;

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
    }
}
