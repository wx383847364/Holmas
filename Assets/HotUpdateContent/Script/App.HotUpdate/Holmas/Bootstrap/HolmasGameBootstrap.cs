using System;
using App.HotUpdate.Holmas.Application;
using App.HotUpdate.Holmas.Meta;
using App.HotUpdate.Holmas.Progression;
using App.HotUpdate.Holmas.Tasks.Config;
using App.HotUpdate.Holmas.Tasks.Services;
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

            HolmasGameplayRuntime gameplayRuntime = CreateGameplayRuntime(logger, assetsRuntime);
            serviceContainer.RegisterSingleton(gameplayRuntime);

            // 这轮先把已确认的共享依赖收敛到统一上下文，给后续地图线和任务线提供稳定挂接点。
            Context = new HolmasApplicationContext(
                serviceContainer,
                logger,
                tickManager,
                eventBus,
                assetsRuntime,
                gameplayRuntime);

            // 这轮已经把地图完成 -> 任务推进 -> 长期进度的运行时编排接入组合层，但仍不提前接 UI。
            Context.Logger.LogInfo("HolmasGameBootstrap: Holmas 业务骨架已启动，运行时编排入口已就位。");
        }

        private static HolmasGameplayRuntime CreateGameplayRuntime(IAppLogger logger, IAssetsRuntime assetsRuntime)
        {
            var taskCatalog = new HolmasTaskCatalog();
            var metaCatalog = new HolmasMetaCatalog(new[]
            {
                new HolmasMetaProgressionDefinition
                {
                    AgencyLevel = 1,
                    MinExperience = 0L,
                }
            });
            var clock = new HolmasSystemUtcClock();
            var randomSource = new HolmasSystemRandomSource();
            var metaSource = new HolmasDefaultMetaExperienceSource();
            var taskProgressService = new HolmasTaskProgressService(taskCatalog, randomSource, clock);
            var metaProgressionService = new HolmasMetaProgressionService(metaCatalog, metaSource, metaSource);
            var progressionCoordinator = new HolmasProgressionCoordinator(taskProgressService, metaProgressionService);

            return new HolmasGameplayRuntime(
                taskProgressService,
                metaProgressionService,
                progressionCoordinator,
                logger,
                assetsRuntime);
        }
    }
}
