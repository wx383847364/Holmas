using System;
using System.Collections.Generic;
using System.Linq;
using App.HotUpdate.Holmas.Application;
using App.HotUpdate.Holmas.Levels;
using App.HotUpdate.Holmas.Meta;
using App.HotUpdate.Holmas.PlayerData;
using App.HotUpdate.Holmas.Progression;
using App.HotUpdate.Holmas.Tasks.Config;
using App.HotUpdate.Holmas.Tasks.Runtime;
using App.HotUpdate.Holmas.Tasks.Services;
using App.HotUpdate.Holmas.UI;
using App.Shared.Contracts;
using App.Shared.Holmas.PlayerData;
using IHolmasPromotionCatalog = App.HotUpdate.Holmas.Meta.IHolmasAgencyCatalog;
using HolmasAgencyPromotionDefinition = App.HotUpdate.Holmas.Meta.HolmasAgencyBuildingDefinition;
using HolmasAgencyPromotionUpgradeResult = App.HotUpdate.Holmas.Meta.HolmasAgencyUpgradeResult;

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
            var persistence = serviceContainer.Get<IPersistence>();

            if (logger == null || tickManager == null || eventBus == null || assetsRuntime == null || persistence == null)
            {
                throw new InvalidOperationException("HolmasGameBootstrap: 启动失败，AOT 提供的基础设施依赖不完整。");
            }

            HolmasConfigCatalogBundle configBundle = TryLoadConfigBundle(assetsRuntime, logger);
            if (configBundle == null)
            {
                throw new InvalidOperationException("HolmasGameBootstrap: 未能加载正式导出配置，拒绝以错误成长协议启动。");
            }

            var taskCatalog = configBundle.TaskCatalog;
            var mapCatalog = configBundle.MapCatalog;
            var metaCatalog = CreateMetaCatalog(configBundle);
            var promotionCatalog = CreatePromotionCatalog(configBundle);
            var clock = new HolmasSystemUtcClock();
            var archiveMapper = new HolmasPlayerArchiveMapper();
            var archiveGateway = new HolmasLocalMockServerGateway(persistence, logger, clock);
            HolmasPlayerArchiveLoadResult archiveLoadResult = archiveGateway.LoadAsync().GetAwaiter().GetResult();
            bool archiveNeedsSave = false;
            if (!archiveLoadResult.Success)
            {
                logger?.LogWarning(
                    "HolmasGameBootstrap: 本地模拟服务器档案不可用，已回退默认新号。status={0}, reason={1}",
                    archiveLoadResult.Status,
                    archiveLoadResult.FailureReason);
                archiveLoadResult = new HolmasPlayerArchiveLoadResult
                {
                    Status = HolmasPlayerArchiveLoadStatus.Success,
                    Archive = archiveMapper.CreateDefaultArchive(),
                };
                archiveNeedsSave = true;
            }

            HolmasTaskBarRestoreResult taskBarRestoreResult = archiveMapper.TryRestoreTaskBar(archiveLoadResult.Archive);
            if (!taskBarRestoreResult.Success)
            {
                logger?.LogWarning(
                    "HolmasGameBootstrap: 本地模拟服务器任务栏档案不一致，已重建任务栏并保留其他进度。reason={0}",
                    taskBarRestoreResult.FailureReason);
                archiveLoadResult = new HolmasPlayerArchiveLoadResult
                {
                    Status = HolmasPlayerArchiveLoadStatus.Success,
                    Archive = archiveMapper.CreateArchiveWithResetTaskBar(archiveLoadResult.Archive),
                };
                archiveNeedsSave = true;
                taskBarRestoreResult = archiveMapper.TryRestoreTaskBar(archiveLoadResult.Archive);
            }

            HolmasGameplayRuntime gameplayRuntime = CreateGameplayRuntime(
                logger,
                assetsRuntime,
                taskCatalog,
                metaCatalog,
                promotionCatalog,
                clock,
                taskBarRestoreResult.State,
                archiveMapper.RestoreProgression(archiveLoadResult.Archive));
            serviceContainer.RegisterSingleton(gameplayRuntime);
            tickManager.Register(gameplayRuntime);

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
            serviceContainer.RegisterSingleton(promotionCatalog);
            serviceContainer.RegisterSingleton<IHolmasPromotionCatalog>(promotionCatalog);
            serviceContainer.RegisterSingleton<IHolmasAgencyCatalog>(promotionCatalog);
            serviceContainer.RegisterSingleton(levelLaunchGateway);
            serviceContainer.RegisterSingleton<IHolmasLevelLaunchGateway>(levelLaunchGateway);
            var archiveSyncService = new HolmasPlayerArchiveSyncService(
                gameplayRuntime,
                archiveGateway,
                archiveMapper,
                clock,
                logger,
                archiveLoadResult.Archive != null ? archiveLoadResult.Archive.PlayerId : HolmasLocalMockServerGateway.DefaultPlayerId,
                archiveLoadResult.Archive != null ? archiveLoadResult.Archive.SchemaVersion : HolmasLocalMockServerGateway.DefaultSchemaVersion,
                archiveLoadResult.Archive != null ? archiveLoadResult.Archive.Revision : 0L);
            serviceContainer.RegisterSingleton(archiveGateway);
            serviceContainer.RegisterSingleton<IHolmasPlayerArchiveGateway>(archiveGateway);
            serviceContainer.RegisterSingleton(archiveMapper);
            serviceContainer.RegisterSingleton(archiveSyncService);
            serviceContainer.RegisterSingleton<IHolmasPlayerArchiveDrain>(archiveSyncService);

            if (archiveLoadResult.Archive != null &&
                archiveLoadResult.Archive.CurrentLevel != null &&
                !archiveLoadResult.Archive.CurrentLevel.Completed)
            {
                try
                {
                    gameplayRuntime.RestoreLevelAsync(archiveLoadResult.Archive.CurrentLevel).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    logger?.LogWarning("HolmasGameBootstrap: 恢复当前关卡失败，已清理损坏的 currentLevel。{0}", ex.Message);
                    gameplayRuntime.EndCurrentLevelSession();
                    archiveNeedsSave = true;
                }
            }

            if (archiveNeedsSave)
            {
                archiveSyncService.MarkDirty("bootstrap_recover_default_archive");
                archiveSyncService.FlushAsync().GetAwaiter().GetResult();
            }

            HolmasUiBootstrap.EnsureCreated(Context, levelLaunchGateway);

            // 这轮已经把地图完成 -> 任务推进 -> 长期进度的运行时编排接入组合层，但仍不提前接 UI。
            Context.Logger.LogInfo("HolmasGameBootstrap: Holmas 业务骨架已启动，城市宣传编排入口已就位。");
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
                        "HolmasGameBootstrap: 未能加载导出的配置二进制包，回退到内置样例。原因={0}",
                        result.FailureReason);
                    return null;
                }

                logger?.LogInfo(
                    "HolmasGameBootstrap: 已加载导出的配置二进制包。maps={0}, tasks={1}, levels={2}, cats={3}",
                    result.Bundle.Maps.Count,
                    result.Bundle.TaskTemplates.Count,
                    result.Bundle.PlayerLevels.Count,
                    result.Bundle.Cats.Count);
                return result.Bundle;
            }
            catch (Exception ex)
            {
                logger?.LogWarning("HolmasGameBootstrap: 加载导出的配置二进制包失败，回退到内置样例。{0}", ex.Message);
                return null;
            }
        }

        private static HolmasGameplayRuntime CreateGameplayRuntime(
            IAppLogger logger,
            IAssetsRuntime assetsRuntime,
            HolmasTaskCatalog taskCatalog,
            HolmasMetaCatalog metaCatalog,
            HolmasAgencyCatalog promotionCatalog,
            IHolmasUtcClock clock,
            HolmasTaskBarState initialTaskBarState,
            HolmasMetaProgressionState initialMetaProgressionState)
        {
            var randomSource = new HolmasSystemRandomSource();
            var metaSource = new HolmasDefaultMetaExperienceSource(metaCatalog);
            var taskProgressService = new HolmasTaskProgressService(taskCatalog, randomSource, clock);
            var metaProgressionService = new HolmasMetaProgressionService(metaCatalog, taskCatalog, metaSource, metaSource, clock);
            var promotionProgressionService = new HolmasAgencyProgressionService(promotionCatalog, metaProgressionService);
            var progressionCoordinator = new HolmasProgressionCoordinator(taskProgressService, metaProgressionService);

            return new HolmasGameplayRuntime(
                taskProgressService,
                metaProgressionService,
                progressionCoordinator,
                promotionProgressionService,
                logger,
                assetsRuntime,
                initialTaskBarState,
                initialMetaProgressionState,
                clock);
        }

        private static HolmasMetaCatalog CreateMetaCatalog(HolmasConfigCatalogBundle configBundle)
        {
            if (configBundle?.PlayerLevels == null || configBundle.PlayerLevels.Count == 0)
            {
                throw new InvalidOperationException("HolmasGameBootstrap: 配置包缺少 PlayerLevels 成长参数，无法组装正式成长服务。");
            }

            return new HolmasMetaCatalog(configBundle.PlayerLevels.Select(row => new HolmasMetaProgressionDefinition
            {
                PlayerLevel = row.PlayerLevel,
                MinExperience = row.UpgradeExp,
                OfflineRewardPerHour = row.OfflineRewardPerHour,
                AdUnlockHours = row.AdUnlockHours,
            }));
        }

        private static HolmasAgencyCatalog CreatePromotionCatalog(HolmasConfigCatalogBundle configBundle)
        {
            if (configBundle?.AgencyBuildings == null || configBundle.AgencyBuildings.Count == 0)
            {
                throw new InvalidOperationException("HolmasGameBootstrap: 配置包缺少 AgencyBuildings，无法组装正式城市宣传成长服务。");
            }

            return new HolmasAgencyCatalog(BuildPromotionDefinitions(configBundle.AgencyBuildings));
        }

        private static IEnumerable<HolmasAgencyPromotionDefinition> BuildPromotionDefinitions(IReadOnlyList<HolmasAgencyBuildingRow> rows)
        {
            if (rows == null || rows.Count == 0)
            {
                yield break;
            }

            for (int stageIndex = 0; stageIndex < rows.Count; stageIndex++)
            {
                HolmasAgencyBuildingRow stageRow = rows[stageIndex];
                if (stageRow == null || stageRow.PromotionIds == null)
                {
                    continue;
                }

                for (int promotionIndex = 0; promotionIndex < stageRow.PromotionIds.Length; promotionIndex++)
                {
                    string promotionId = stageRow.PromotionIds[promotionIndex];
                    if (string.IsNullOrWhiteSpace(promotionId))
                    {
                        continue;
                    }

                    int levelCap = stageRow.PromotionLevelCaps != null && promotionIndex < stageRow.PromotionLevelCaps.Length
                        ? stageRow.PromotionLevelCaps[promotionIndex]
                        : 0;
                    int[] costs = Array.Empty<int>();
                    if (stageRow.PromotionUpgradeCosts != null && promotionIndex < stageRow.PromotionUpgradeCosts.Length)
                    {
                        costs = stageRow.PromotionUpgradeCosts[promotionIndex]?.Costs ?? Array.Empty<int>();
                    }

                    yield return new HolmasAgencyPromotionDefinition
                    {
                        AgencyStageId = stageRow.AgencyStageId,
                        StageName = stageRow.StageName,
                        PromotionId = promotionId,
                        PromotionLevelCap = levelCap,
                        PromotionUpgradeCosts = costs,
                    };
                }
            }
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
