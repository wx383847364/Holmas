using System;
using System.Collections.Generic;
using System.Linq;
using App.HotUpdate.Holmas.Application;
using App.HotUpdate.Holmas.Board;
using App.HotUpdate.Holmas.Levels;
using App.HotUpdate.Holmas.Meta;
using App.HotUpdate.Holmas.Progression;
using App.HotUpdate.Holmas.Tasks.Config;
using App.HotUpdate.Holmas.Tasks.Runtime;
using App.HotUpdate.Holmas.Tasks.Services;
using App.HotUpdate.Holmas.Terrain;
using App.Shared.Contracts;
using UnityEditor;
using UnityEngine;
using TerrainAssetPathUtility = App.HotUpdate.Holmas.Terrain.HolmasTerrainAssetPathUtility;

public static class HolmasCoreValidationMenu
{
    [MenuItem("Holmas/Validation/Run Core Logic Smoke Test")]
    public static void RunCoreLogicSmokeTest()
    {
        var catalog = CreateCatalog();
        var taskService = new HolmasTaskProgressService(
            catalog,
            new ScriptedRandomSource(0, 0, 1, 0, 1, 1),
            new FixedUtcClock { UtcNowMilliseconds = 1000 });
        var metaCatalog = CreateMetaCatalog();
        var metaService = new HolmasMetaProgressionService(
            metaCatalog,
            new HolmasDefaultMetaExperienceSource(metaCatalog),
            new HolmasDefaultMetaExperienceSource(metaCatalog));
        var agencyService = new HolmasAgencyProgressionService(CreateAgencyCatalog(), metaService);
        var coordinator = new HolmasProgressionCoordinator(taskService, metaService);
        var terrain = CreateTerrain(1, 1);
        var assetsRuntime = new FakeAssetsRuntime(terrain);
        var runtime = new HolmasGameplayRuntime(taskService, metaService, coordinator, agencyService, new NullLogger(), assetsRuntime);
        var context = new HolmasApplicationContext(
            new FakeServiceContainer(),
            new NullLogger(),
            new FakeTickManager(),
            new FakeEventBus(),
            assetsRuntime,
            runtime);
        var mapCatalog = new HolmasMapCatalog(
            new[]
            {
                new HolmasMapDefinition
                {
                    MapId = "map-1",
                    TerrainPath = "validation-map",
                    CatCountMin = 1,
                    CatCountMax = 1,
                }
            });
        var gateway = new HolmasLevelLaunchGateway(context, new HolmasLevelRequestGenerator(catalog, mapCatalog, new ScriptedRandomSource(0)));

        runtime.RefillAvailableTasks(1);

        gateway.StartLevelForPlayerAsync(
            1,
            1,
            new[]
            {
                new BoardSpawnEntry
                {
                    CatId = "cat-a",
                    Weight = 1,
                }
            }).GetAwaiter().GetResult();
        var reveal = runtime.RevealCell(0, out HolmasProgressionAdvanceResult progressionResult);
        var claim = runtime.ClaimTaskReward(0, 1);
        var firstUpgrade = runtime.TryUpgradeBuilding("lobby");
        var secondUpgrade = runtime.TryUpgradeBuilding("desk");
        var offline = runtime.ApplyOfflineSettlement(3_600_000L);

        if (!claim.Success || !firstUpgrade.Success || !secondUpgrade.Success)
        {
            throw new InvalidOperationException("Holmas smoke test failed to complete task claim or building upgrades.");
        }

        if (runtime.CurrentAgencyStageId != 1 || runtime.CurrentPlayerLevel != 3)
        {
            throw new InvalidOperationException($"Holmas smoke test reached unexpected growth state: playerLevel={runtime.CurrentPlayerLevel}, agencyStageId={runtime.CurrentAgencyStageId}.");
        }

        Debug.Log($"Holmas smoke test passed. revealCompleted={reveal.Completed}, taskProgressed={progressionResult?.ProgressedTaskIds.Count ?? 0}, taskClaimSuccess={claim.Success}, gold={runtime.CurrentGoldBalance}, playerLevel={runtime.CurrentPlayerLevel}, agencyStageId={runtime.CurrentAgencyStageId}, offlineReward={offline.OfflineRewardGained}");
    }

    private static HolmasTaskCatalog CreateCatalog()
    {
        return new HolmasTaskCatalog(
            new[]
            {
                new HolmasCatDefinition { CatId = "cat-a", Price = 10 },
                new HolmasCatDefinition { CatId = "cat-b", Price = 20 },
            },
            new[]
            {
                new HolmasTaskTemplateDefinition
                {
                    TaskTypeId = "task-normal",
                    CatIdList = new[] { "cat-a", "cat-b" },
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
                    MapIds = new[] { "map-1" },
                    MapWeights = new[] { 1 },
                }
            });
    }

    private static HolmasMetaCatalog CreateMetaCatalog()
    {
        return new HolmasMetaCatalog(
            new[]
            {
                new HolmasMetaProgressionDefinition
                {
                    AgencyLevel = 1,
                    MinExperience = 0,
                    OfflineRewardPerHour = 6,
                    AdUnlockHours = 24,
                },
                new HolmasMetaProgressionDefinition
                {
                    AgencyLevel = 2,
                    MinExperience = 1,
                    OfflineRewardPerHour = 8,
                    AdUnlockHours = 12,
                },
                new HolmasMetaProgressionDefinition
                {
                    AgencyLevel = 3,
                    MinExperience = 2,
                    OfflineRewardPerHour = 10,
                    AdUnlockHours = 24,
                }
            });
    }

    private static HolmasAgencyCatalog CreateAgencyCatalog()
    {
        return new HolmasAgencyCatalog(
            new[]
            {
                new HolmasAgencyBuildingDefinition
                {
                    AgencyStageId = 1,
                    BuildingId = "lobby",
                    LevelCap = 1,
                    UpgradeCosts = new[] { 10 },
                },
                new HolmasAgencyBuildingDefinition
                {
                    AgencyStageId = 1,
                    BuildingId = "desk",
                    LevelCap = 1,
                    UpgradeCosts = new[] { 10 },
                },
            });
    }

    private static UnityEngine.Object CreateTerrain(int rows, int cols)
    {
        var terrain = ResolveTerrainType<ScriptableObject>("MinesweeperTerrainData");
        if (terrain == null)
        {
            throw new InvalidOperationException("MinesweeperTerrainData was not found or could not be instantiated.");
        }

        InvokeVoid(terrain, "Resize", rows, cols);
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                InvokeVoid(terrain, "SetValid", row, col, true);
                InvokeVoid(terrain, "SetColor", row, col, new Color32(180, 180, 180, 255));
            }
        }

        return terrain;
    }

    private static T ResolveTerrainType<T>(string simpleName) where T : class
    {
        var type = AppDomain.CurrentDomain
            .GetAssemblies()
            .SelectMany(GetTypesSafe)
            .FirstOrDefault(candidate => string.Equals(candidate.Name, simpleName, StringComparison.Ordinal));

        if (type == null)
        {
            throw new InvalidOperationException($"Could not resolve type '{simpleName}' from loaded assemblies.");
        }

        return ScriptableObject.CreateInstance(type) as T;
    }

    private static void InvokeVoid(object target, string methodName, params object[] arguments)
    {
        var method = target.GetType().GetMethod(methodName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        if (method == null)
        {
            throw new InvalidOperationException($"Could not find method '{methodName}' on '{target.GetType().FullName}'.");
        }

        method.Invoke(target, arguments);
    }

    private static IEnumerable<Type> GetTypesSafe(System.Reflection.Assembly assembly)
    {
        if (assembly == null)
        {
            yield break;
        }

        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (System.Reflection.ReflectionTypeLoadException ex)
        {
            types = ex.Types;
        }

        if (types == null)
        {
            yield break;
        }

        foreach (var type in types)
        {
            if (type != null)
            {
                yield return type;
            }
        }
    }

    private sealed class ScriptedRandomSource : IHolmasRandomSource
    {
        private readonly Queue<int> _scriptedInts;

        public ScriptedRandomSource(params int[] scriptedInts)
        {
            _scriptedInts = new Queue<int>(scriptedInts ?? Array.Empty<int>());
        }

        public int Next(int maxExclusive)
        {
            return Next(0, maxExclusive);
        }

        public int Next(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
            {
                return minInclusive;
            }

            int range = maxExclusive - minInclusive;
            int value = _scriptedInts.Count > 0 ? _scriptedInts.Dequeue() : 0;
            value = Math.Abs(value);
            return minInclusive + (value % range);
        }

        public double NextDouble()
        {
            return 0d;
        }
    }

    private sealed class FixedUtcClock : IHolmasUtcClock
    {
        public long UtcNowMilliseconds { get; set; }
    }

    private sealed class FakeAssetsRuntime : IAssetsRuntime
    {
        private readonly UnityEngine.Object _asset;

        public FakeAssetsRuntime(UnityEngine.Object asset)
        {
            _asset = asset;
        }

        public System.Threading.Tasks.Task InitializeAsync()
        {
            return System.Threading.Tasks.Task.CompletedTask;
        }

        public System.Threading.Tasks.Task<bool> RunPatchFlowAsync(string packageVersion = null)
        {
            return System.Threading.Tasks.Task.FromResult(true);
        }

        public System.Threading.Tasks.Task<IAssetHandle> LoadAssetAsync(string location)
        {
            return System.Threading.Tasks.Task.FromResult<IAssetHandle>(new FakeAssetHandle(_asset));
        }

        public void Shutdown()
        {
        }
    }

    private sealed class FakeAssetHandle : IAssetHandle
    {
        public FakeAssetHandle(UnityEngine.Object asset)
        {
            AssetObject = asset;
        }

        public UnityEngine.Object AssetObject { get; }

        public void Release()
        {
        }
    }

    private sealed class FakeServiceContainer : IServiceContainer
    {
        public void RegisterSingleton<T>(T instance) where T : class
        {
        }

        public T Get<T>() where T : class
        {
            return null;
        }

        public bool IsRegistered<T>()
        {
            return false;
        }
    }

    private sealed class FakeTickManager : ITickManager
    {
        public void Register(ITickable tickable)
        {
        }

        public void Unregister(ITickable tickable)
        {
        }
    }

    private sealed class FakeEventBus : IEventBus
    {
        public void Subscribe<T>(Action<T> handler) where T : class
        {
        }

        public void Unsubscribe<T>(Action<T> handler) where T : class
        {
        }

        public void Publish<T>(T eventData) where T : class
        {
        }
    }

    private sealed class NullLogger : IAppLogger
    {
        public void Log(LogLevel level, string message, params object[] args)
        {
        }

        public void LogDebug(string message, params object[] args)
        {
        }

        public void LogInfo(string message, params object[] args)
        {
        }

        public void LogWarning(string message, params object[] args)
        {
        }

        public void LogError(string message, params object[] args)
        {
        }
    }
}
