using System;
using App.HotUpdate.Holmas.Application;
using App.Shared.Contracts;

namespace App.HotUpdate.Holmas.Bootstrap
{
    /// <summary>
    /// Holmas 正式业务骨架的启动入口。
    /// 它的职责是把 AOT 提供的基础设施接入 HotUpdate，而不是在这里直接实现地图、任务或 UI 逻辑。
    /// </summary>
    public static class HolmasGameBootstrap
    {
        /// <summary>
        /// 当前已经建立好的应用上下文。
        /// 后续 Agent 2、Agent 3 可以在骨架稳定后继续围绕它挂接业务模块。
        /// </summary>
        public static HolmasApplicationContext Context { get; private set; }

        /// <summary>
        /// 启动 Holmas 业务骨架。
        /// 本轮只冻结契约和依赖入口，不在这里实现具体玩法细节。
        /// </summary>
        public static void Start(IServiceContainer serviceContainer)
        {
            if (serviceContainer == null)
            {
                throw new ArgumentNullException(nameof(serviceContainer));
            }

            if (Context != null)
            {
                Context.Logger?.LogWarning("HolmasGameBootstrap: 业务骨架已启动，跳过重复初始化。");
                return;
            }

            // AOT 负责把基础设施准备好，HotUpdate 只从容器中取稳定契约，不直接依赖宿主实现细节。
            var logger = serviceContainer.Get<IAppLogger>();
            var tickManager = serviceContainer.Get<ITickManager>();
            var eventBus = serviceContainer.Get<IEventBus>();
            var assetsRuntime = serviceContainer.Get<IAssetsRuntime>();

            if (logger == null || tickManager == null || eventBus == null || assetsRuntime == null)
            {
                throw new InvalidOperationException("HolmasGameBootstrap: 启动失败，AOT 提供的基础设施依赖不完整。");
            }

            // 这轮先把已确认的共享依赖收敛到统一上下文，给后续地图线和任务线提供稳定挂接点。
            Context = new HolmasApplicationContext(
                serviceContainer,
                logger,
                tickManager,
                eventBus,
                assetsRuntime);

            // 这里只输出骨架启动日志，不提前塞入地图生成、任务补位、奖励公式等正式玩法逻辑。
            Context.Logger.LogInfo("HolmasGameBootstrap: Holmas 业务骨架已启动。");
        }
    }
}
