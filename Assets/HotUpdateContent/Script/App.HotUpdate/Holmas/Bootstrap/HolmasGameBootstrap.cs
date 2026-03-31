using System;
using App.HotUpdate.Holmas.Application;
using App.HotUpdate.Holmas.Levels;
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

            HolmasConfigCatalogBundle configBundle = TryLoadConfigBundle(assetsRuntime, logger);
            var taskCatalog = configBundle?.TaskCatalog ?? CreateFallbackTaskCatalog();
            var mapCatalog = configBundle?.MapCatalog ?? CreateFallbackMapCatalog();
            HolmasGameplayRuntime gameplayRuntime = CreateGameplayRuntime(logger, assetsRuntime, taskCatalog);
            serviceContainer.RegisterSingleton(gameplayRuntime);

            // 这轮先把已确认的共享依赖收敛到统一上下文，给后续地图线和任务线提供稳定挂接点。
            Context = new HolmasApplicationContext(
                serviceContainer,
                logger,
                tickManager,
                eventBus,
                assetsRuntime,
                gameplayRuntime);

            // 组合层再提供一层轻量门面，后续真实地图配置生成出的请求可以稳定从这里进入。
            var levelRequestGenerator = new HolmasLevelRequestGenerator(taskCatalog, mapCatalog, new HolmasSystemRandomSource());
            var levelLaunchGateway = new HolmasLevelLaunchGateway(Context, levelRequestGenerator);
            serviceContainer.RegisterSingleton(levelRequestGenerator);
            serviceContainer.RegisterSingleton(taskCatalog);
            serviceContainer.RegisterSingleton<IHolmasTaskCatalog>(taskCatalog);
            serviceContainer.RegisterSingleton<IHolmasMapCatalog>(mapCatalog);
            serviceContainer.RegisterSingleton(levelLaunchGateway);
            serviceContainer.RegisterSingleton<IHolmasLevelLaunchGateway>(levelLaunchGateway);

            // 这轮已经把地图完成 -> 任务推进 -> 长期进度的运行时编排接入组合层，但仍不提前接 UI。
            Context.Logger.LogInfo("HolmasGameBootstrap: Holmas 业务骨架已启动，运行时编排入口已就位。");
        }

        private static HolmasConfigCatalogBundle TryLoadConfigBundle(IAssetsRuntime assetsRuntime, IAppLogger logger)
        {
            try
            {
                var loader = new HolmasConfigRuntimeLoader(assetsRuntime);
                HolmasConfigLoadResult result = loader.LoadDefaultAsync().GetAwaiter().GetResult();
                if (!result.Success || result.Bundle == null)
                {
                    logger?.LogWarning(
                        "HolmasGameBootstrap: 未能加载导出的 CSV 二进制配置，回退到内置样例。原因={0}",
                        result.FailureReason);
                    return null;
                }

                logger?.LogInfo(
                    "HolmasGameBootstrap: 已加载导出的 CSV 二进制配置。maps={0}, tasks={1}, levels={2}, cats={3}",
                    result.Bundle.Maps.Count,
                    result.Bundle.TaskTemplates.Count,
                    result.Bundle.PlayerLevels.Count,
                    result.Bundle.Cats.Count);
                return result.Bundle;
            }
            catch (Exception ex)
            {
                logger?.LogWarning("HolmasGameBootstrap: 加载导出的 CSV 二进制配置失败，回退到内置样例。{0}", ex.Message);
                return null;
            }
        }

        private static HolmasGameplayRuntime CreateGameplayRuntime(
            IAppLogger logger,
            IAssetsRuntime assetsRuntime,
            HolmasTaskCatalog taskCatalog)
        {
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

        private static HolmasTaskCatalog CreateFallbackTaskCatalog()
        {
            return new HolmasTaskCatalog(
                new[]
                {
                    new HolmasCatDefinition { CatId = "cat-a", CatName = "Cat A", Weight = 1, Price = 10 },
                    new HolmasCatDefinition { CatId = "cat-b", CatName = "Cat B", Weight = 1, Price = 20 },
                    new HolmasCatDefinition { CatId = "cat-c", CatName = "Cat C", Weight = 1, Price = 30 },
                },
                new[]
                {
                    new HolmasTaskTemplateDefinition
                    {
                        TaskTypeId = "task-normal",
                        TaskKind = HolmasTaskKind.Money,
                        CatIdList = new[] { "cat-a", "cat-b", "cat-c" },
                        CountMin = 1,
                        CountMax = 1,
                        RewardArray = Array.Empty<string>(),
                        LevelRewardFactor = 2f,
                    }
                },
                new[]
                {
                    new HolmasPlayerLevelDefinition
                    {
                        PlayerLevel = 1,
                        UpgradeExp = 0,
                        TaskTypeIds = new[] { "task-normal" },
                        TaskTypeWeights = new[] { 1 },
                        MapIds = new[] { "map-1", "map-2", "map-3" },
                        MapWeights = new[] { 1, 1, 1 },
                    }
                });
        }

        private static HolmasMapCatalog CreateFallbackMapCatalog()
        {
            return new HolmasMapCatalog(
                new[]
                {
                    new HolmasMapDefinition
                    {
                        MapId = "map-1",
                        TerrainPath = "1",
                        CatCountMin = 1,
                        CatCountMax = 1,
                    },
                    new HolmasMapDefinition
                    {
                        MapId = "map-2",
                        TerrainPath = "2",
                        CatCountMin = 1,
                        CatCountMax = 2,
                    },
                    new HolmasMapDefinition
                    {
                        MapId = "map-3",
                        TerrainPath = "3",
                        CatCountMin = 2,
                        CatCountMax = 3,
                    }
                });
        }
    }
}
