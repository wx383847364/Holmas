using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
using App.Shared.Holmas.RuntimeData;
using UnityEditor;
using UnityEngine;
using App.HotUpdate.Holmas.Bootstrap;
using TerrainAssetPathUtility = App.HotUpdate.Holmas.Terrain.HolmasTerrainAssetPathUtility;

public static class HolmasCoreValidationMenu
{
    [MenuItem("Holmas/Validation/Run Core Logic Smoke Test")]
    public static void RunCoreLogicSmokeTest()
    {
        ResetBootstrapContext();
        HolmasConfigCatalogBundle configBundle = LoadExportedConfigBundle();
        var serviceContainer = new RecordingServiceContainer();
        var assetsRuntime = new ProjectAssetsRuntime();
        serviceContainer.RegisterSingleton<IAppLogger>(new NullLogger());
        serviceContainer.RegisterSingleton<ITickManager>(new FakeTickManager());
        serviceContainer.RegisterSingleton<IEventBus>(new FakeEventBus());
        serviceContainer.RegisterSingleton<IAssetsRuntime>(assetsRuntime);
        serviceContainer.RegisterSingleton<IPersistence>(new InMemoryPersistence());

        try
        {
            HolmasGameBootstrap.Start(serviceContainer);
            HolmasApplicationContext context = HolmasGameBootstrap.Context;
            if (context == null)
            {
                throw new InvalidOperationException("Holmas smoke test failed to initialize the application context from exported bytes.");
            }

            var gateway = serviceContainer.Get<IHolmasLevelLaunchGateway>();
            if (gateway == null)
            {
                throw new InvalidOperationException("Holmas smoke test could not recover the formal level launch gateway from bootstrap.");
            }

            var promotionCatalog = serviceContainer.Get<IHolmasAgencyCatalog>();
            if (promotionCatalog == null)
            {
                throw new InvalidOperationException("Holmas smoke test could not recover the formal promotion catalog from bootstrap.");
            }

            int smokeStageId = configBundle.Holmas_AgencyBuildingTable
                .Where(row => row != null)
                .Select(row => row.agencyStageId)
                .DefaultIfEmpty(1)
                .Max();
            IReadOnlyList<HolmasAgencyBuildingDefinition> smokeStagePromotions = promotionCatalog.GetPromotionsForStage(smokeStageId);
            if (smokeStagePromotions == null || smokeStagePromotions.Count == 0)
            {
                throw new InvalidOperationException($"Holmas smoke test failed to recover formal promotions for stage {smokeStageId}.");
            }

            context.RefillAvailableTasks();
            var firstTask = context.GameplayRuntime.TaskBarState.GetTaskBySlot(0);
            if (firstTask == null || firstTask.Task == null || string.IsNullOrWhiteSpace(firstTask.Task.CatId))
            {
                throw new InvalidOperationException("Holmas smoke test failed to recover an active task from the exported config bundle.");
            }

            HolmasTaskRuntimeInstance activeTask = context.GameplayRuntime.TaskBarState.GetTaskBySlot(0);
            if (activeTask == null || activeTask.Task == null)
            {
                throw new InvalidOperationException("Holmas smoke test could not recover the active task from the task bar.");
            }

            int expectedAutoReward = activeTask.Task.Reward;
            var reveal = CompleteTaskThroughFormalMapProgression(context, gateway, 0, out HolmasProgressionAdvanceResult progressionResult);
            activeTask = context.GameplayRuntime.TaskBarState.GetTaskBySlot(0);
            if (context.CurrentGoldBalance < expectedAutoReward)
            {
                throw new InvalidOperationException("Holmas smoke test failed to auto-claim the active task through formal map completion.");
            }

            if (context.GameplayRuntime.MetaProgressionState.ClaimedTaskCount <= 0)
            {
                throw new InvalidOperationException("Holmas smoke test did not record the automatic task claim.");
            }

            long requiredGold = CalculatePromotionTotalCost(smokeStagePromotions);
            long currentGold = context.CurrentGoldBalance;
            int rewardRatePerHour = ResolveOfflineRewardPerHour(configBundle, context.CurrentPlayerLevel);
            if (rewardRatePerHour <= 0)
            {
                throw new InvalidOperationException("Holmas smoke test recovered an invalid offline reward rate from exported config.");
            }

            if (currentGold < requiredGold)
            {
                long goldGap = requiredGold - currentGold;
                long offlineHours = (goldGap + rewardRatePerHour - 1) / rewardRatePerHour;
                HolmasProgressionAdvanceResult offline = context.ApplyOfflineSettlement(offlineHours * 3_600_000L);
                if (offline.OfflineRewardGained < goldGap)
                {
                    throw new InvalidOperationException($"Holmas smoke test failed to gain enough offline gold: required={goldGap}, gained={offline.OfflineRewardGained}.");
                }
            }

            long goldBeforePromotionUpgrades = context.CurrentGoldBalance;
            long experienceBeforePromotionUpgrades = context.GameplayRuntime.MetaProgressionState.Experience;
            int expectedPromotionUpgradeCount = CalculatePromotionUpgradeCount(smokeStagePromotions);
            long expectedExperience = experienceBeforePromotionUpgrades + expectedPromotionUpgradeCount;
            int expectedPlayerLevel = ResolvePlayerLevel(configBundle, expectedExperience);
            context.GameplayRuntime.MetaProgressionState.AgencyStageId = smokeStageId;
            UpgradePromotions(context.GameplayRuntime, smokeStagePromotions);

            if (context.CurrentAgencyStageId != smokeStageId)
            {
                throw new InvalidOperationException($"Holmas smoke test unexpectedly changed stage after fully upgrading the exported stage. currentStage={context.CurrentAgencyStageId}, expectedStage={smokeStageId}");
            }

            if (context.CurrentPlayerLevel != expectedPlayerLevel)
            {
                throw new InvalidOperationException($"Holmas smoke test reached unexpected player level after growth chain: {context.CurrentPlayerLevel}.");
            }

            if (context.GameplayRuntime.MetaProgressionState.Experience != expectedExperience)
            {
                throw new InvalidOperationException($"Holmas smoke test reached unexpected experience after growth chain: {context.GameplayRuntime.MetaProgressionState.Experience}.");
            }

            int expectedPromotionStateCount = CountTrackedPromotions(smokeStagePromotions);
            if (context.GameplayRuntime.MetaProgressionState.PromotionLevels.Count != expectedPromotionStateCount)
            {
                throw new InvalidOperationException($"Holmas smoke test did not record all upgraded promotions: {context.GameplayRuntime.MetaProgressionState.PromotionLevels.Count}.");
            }

            ValidatePromotionUpgradeState(context.GameplayRuntime.MetaProgressionState, smokeStagePromotions);

            long expectedGoldBalance = goldBeforePromotionUpgrades - requiredGold;
            if (context.CurrentGoldBalance != expectedGoldBalance)
            {
                throw new InvalidOperationException($"Holmas smoke test reached unexpected gold balance after promotion upgrades: current={context.CurrentGoldBalance}, expected={expectedGoldBalance}.");
            }

            List<string> promotionLevelEntries = new List<string>();
            foreach (var entry in context.GameplayRuntime.MetaProgressionState.PromotionLevels.OrderBy(item => item.Key))
            {
                promotionLevelEntries.Add(entry.Key + ":" + entry.Value);
            }

            string promotionLevels = string.Join(",", promotionLevelEntries);
            string taskProgress = activeTask != null && activeTask.Task != null
                ? $"{activeTask.Task.CurrentCount}/{activeTask.Task.TargetCount}"
                : "auto-claimed";
            Debug.Log($"Holmas smoke test passed. revealCompleted={reveal.Completed}, taskProgress={taskProgress}, taskAutoClaimed={context.GameplayRuntime.MetaProgressionState.ClaimedTaskCount > 0}, gold={context.CurrentGoldBalance}, experience={context.GameplayRuntime.MetaProgressionState.Experience}, playerLevel={context.CurrentPlayerLevel}, agencyStageId={context.CurrentAgencyStageId}, promotionLevels={promotionLevels}");
        }
        finally
        {
            ResetBootstrapContext();
        }
    }

    private static long CalculatePromotionTotalCost(IReadOnlyList<HolmasAgencyBuildingDefinition> promotions)
    {
        if (promotions == null || promotions.Count == 0)
        {
            throw new InvalidOperationException("Holmas smoke test recovered an empty promotion list.");
        }

        long totalCost = 0;
        foreach (var promotion in promotions)
        {
            if (promotion == null)
            {
                continue;
            }

            if (promotion.PromotionUpgradeCosts == null)
            {
                continue;
            }

            foreach (int cost in promotion.PromotionUpgradeCosts)
            {
                totalCost += cost;
            }
        }

        return totalCost;
    }

    private static int CalculatePromotionUpgradeCount(IReadOnlyList<HolmasAgencyBuildingDefinition> promotions)
    {
        if (promotions == null)
        {
            return 0;
        }

        int totalCount = 0;
        foreach (HolmasAgencyBuildingDefinition promotion in promotions)
        {
            if (promotion == null)
            {
                continue;
            }

            int costCount = promotion.PromotionUpgradeCosts != null ? promotion.PromotionUpgradeCosts.Length : 0;
            totalCount += Math.Min(Math.Max(0, promotion.PromotionLevelCap), costCount);
        }

        return totalCount;
    }

    private static int CountTrackedPromotions(IReadOnlyList<HolmasAgencyBuildingDefinition> promotions)
    {
        if (promotions == null)
        {
            return 0;
        }

        return promotions.Count(item => item != null && !string.IsNullOrWhiteSpace(item.PromotionId));
    }

    private static int ResolvePlayerLevel(HolmasConfigCatalogBundle configBundle, long experience)
    {
        if (configBundle?.PlayerLevels == null || configBundle.PlayerLevels.Count == 0)
        {
            return 1;
        }

        int resolvedLevel = 1;
        foreach (HolmasPlayerLevelDefinition definition in configBundle.PlayerLevels
                     .Where(item => item != null)
                     .OrderBy(item => item.PlayerLevel))
        {
            if (experience < Math.Max(0, definition.UpgradeExp))
            {
                break;
            }

            resolvedLevel = Math.Max(resolvedLevel, definition.PlayerLevel);
        }

        return resolvedLevel;
    }

    private static void ValidatePromotionUpgradeState(
        HolmasMetaProgressionState state,
        IReadOnlyList<HolmasAgencyBuildingDefinition> promotions)
    {
        if (state == null || promotions == null)
        {
            return;
        }

        foreach (HolmasAgencyBuildingDefinition promotion in promotions)
        {
            if (promotion == null || string.IsNullOrWhiteSpace(promotion.PromotionId))
            {
                continue;
            }

            int scopedLevel = HolmasAgencyPromotionStateKey.GetLevel(state, promotion.AgencyStageId, promotion.PromotionId);
            if (scopedLevel != promotion.PromotionLevelCap)
            {
                throw new InvalidOperationException(
                    $"Holmas smoke test did not fully upgrade promotion {promotion.AgencyStageId}::{promotion.PromotionId}. current={scopedLevel}, expected={promotion.PromotionLevelCap}.");
            }
        }
    }

    private static BoardRevealResult CompleteTaskThroughFormalMapProgression(
        HolmasApplicationContext context,
        IHolmasLevelLaunchGateway gateway,
        int slotIndex,
        out HolmasProgressionAdvanceResult progressionResult)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (gateway == null)
        {
            throw new ArgumentNullException(nameof(gateway));
        }

        progressionResult = null;
        BoardRevealResult finalReveal = null;

        HolmasTaskRuntimeInstance runtimeTask = context.GameplayRuntime.TaskBarState.GetTaskBySlot(slotIndex);
        if (runtimeTask == null || runtimeTask.Task == null)
        {
            throw new InvalidOperationException($"Holmas smoke test could not recover the active task from slot {slotIndex}.");
        }

        int maxAttempts = Math.Max(runtimeTask.Task.TargetCount, 1) * 4;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            runtimeTask = context.GameplayRuntime.TaskBarState.GetTaskBySlot(slotIndex);
            if (runtimeTask == null || runtimeTask.Task == null)
            {
                throw new InvalidOperationException($"Holmas smoke test lost the active task from slot {slotIndex} before completion.");
            }

            string taskInstanceId = runtimeTask.Task.TaskInstanceId;
            int progressBefore = runtimeTask.Task.CurrentCount;

            gateway.StartLevelForCurrentPlayerAsync(
                attempt + 1,
                new[]
                {
                    new BoardSpawnEntry
                    {
                        CatId = runtimeTask.Task.CatId,
                        Weight = 1,
                    }
                }).GetAwaiter().GetResult();

            finalReveal = CompleteCurrentLevelByRevealingAllCats(context, out progressionResult);

            if (progressionResult == null)
            {
                throw new InvalidOperationException("Holmas smoke test did not produce a completion result for the formal map.");
            }

            runtimeTask = context.GameplayRuntime.TaskBarState.GetTaskBySlot(slotIndex);
            if (runtimeTask == null || runtimeTask.Task == null)
            {
                return finalReveal ?? new BoardRevealResult(-1) { IsValidAction = true, Completed = true };
            }

            if (!string.Equals(runtimeTask.Task.TaskInstanceId, taskInstanceId, StringComparison.Ordinal))
            {
                return finalReveal ?? new BoardRevealResult(-1) { IsValidAction = true, Completed = true };
            }

            if (runtimeTask.Task.CurrentCount <= progressBefore)
            {
                throw new InvalidOperationException("Holmas smoke test did not increase task progress after formal map completion.");
            }
        }

        throw new InvalidOperationException($"Holmas smoke test could not finish the active task through formal map progression within {maxAttempts} attempts.");
    }

    private static BoardRevealResult CompleteCurrentLevelByRevealingAllCats(
        HolmasApplicationContext context,
        out HolmasProgressionAdvanceResult progressionResult)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (context.GameplayRuntime?.CurrentLevelSnapshot?.SpawnedCats == null)
        {
            throw new InvalidOperationException("Holmas smoke test could not recover the current level snapshot.");
        }

        BoardRuntime boardRuntime = context.GameplayRuntime.CurrentBoardRuntime;
        if (boardRuntime == null)
        {
            throw new InvalidOperationException("Holmas smoke test could not recover the current board runtime.");
        }

        List<SpawnedCatData> spawnedCats = context.GameplayRuntime.CurrentLevelSnapshot.SpawnedCats
            .Where(item => item != null)
            .OrderBy(item => item.CellIndex)
            .ToList();
        if (spawnedCats.Count == 0)
        {
            throw new InvalidOperationException("Holmas smoke test generated an empty cat snapshot and cannot validate task progression.");
        }

        progressionResult = null;
        BoardRevealResult finalReveal = null;
        foreach (SpawnedCatData spawnedCat in spawnedCats)
        {
            BoardCellState cellState = boardRuntime.GetCellState(spawnedCat.CellIndex);
            if (!cellState.IsValid || !cellState.HasCat)
            {
                throw new InvalidOperationException(
                    $"Holmas smoke test found snapshot/board cat mismatch at cell {spawnedCat.CellIndex}. map={context.GameplayRuntime.CurrentLevelSnapshot.MapId}, cellCount={boardRuntime.CellCount}, totalCats={boardRuntime.TotalCatCount}, valid={cellState.IsValid}, hasCat={cellState.HasCat}.");
            }

            finalReveal = context.GameplayRuntime.RevealCell(
                spawnedCat.CellIndex,
                HolmasBoardInteractionMode.Find,
                out HolmasProgressionAdvanceResult revealProgressionResult);
            if (!finalReveal.IsValidAction || !finalReveal.FoundCat)
            {
                throw new InvalidOperationException(
                    $"Holmas smoke test failed to reveal spawned cat at cell {spawnedCat.CellIndex}. map={context.GameplayRuntime.CurrentLevelSnapshot.MapId}, totalCats={boardRuntime.TotalCatCount}, foundCats={boardRuntime.FoundCatCount}, energy={context.CurrentEnergy}/{context.EnergyRecoveryLimit}, validAction={finalReveal.IsValidAction}, foundCat={finalReveal.FoundCat}, ignored={finalReveal.IsIgnored}, reason={finalReveal.FailureReason}");
            }

            if (revealProgressionResult != null)
            {
                progressionResult = revealProgressionResult;
            }
        }

        if (finalReveal == null || !finalReveal.Completed || progressionResult == null)
        {
            throw new InvalidOperationException("Holmas smoke test failed to complete the current level through formal cat reveals.");
        }

        return finalReveal;
    }

    private static int ResolveOfflineRewardPerHour(HolmasConfigCatalogBundle configBundle, int playerLevel)
    {
        if (configBundle?.PlayerLevels == null)
        {
            return 0;
        }

        int effectiveLevel = Math.Max(1, playerLevel);
        HolmasPlayerLevelDefinition playerLevelDefinition = configBundle.PlayerLevels.FirstOrDefault(
            row => row != null && row.PlayerLevel == effectiveLevel);
        return playerLevelDefinition?.OfflineRewardPerHour ?? 0;
    }

    private static void UpgradePromotions(HolmasGameplayRuntime runtime, IReadOnlyList<HolmasAgencyBuildingDefinition> promotions)
    {
        if (runtime == null)
        {
            throw new ArgumentNullException(nameof(runtime));
        }

        if (promotions == null || promotions.Count == 0)
        {
            throw new InvalidOperationException("Holmas smoke test recovered an empty promotion list to upgrade.");
        }

        foreach (HolmasAgencyBuildingDefinition promotion in promotions)
        {
            if (promotion == null || string.IsNullOrWhiteSpace(promotion.PromotionId))
            {
                continue;
            }

            string promotionId = promotion.PromotionId;
            int levelCap = promotion.PromotionLevelCap;

            if (levelCap <= 0)
            {
                throw new InvalidOperationException($"Holmas smoke test recovered invalid promotion cap for promotion {promotionId}.");
            }

            while (HolmasAgencyPromotionStateKey.GetLevel(runtime.MetaProgressionState, promotion.AgencyStageId, promotionId) < levelCap)
            {
                HolmasAgencyUpgradeResult result = runtime.TryUpgradePromotion(promotionId);
                if (!result.Success)
                {
                    throw new InvalidOperationException($"Holmas smoke test failed to upgrade promotion {promotionId}: {result.FailureReason}");
                }
            }
        }
    }

    private static void ResetBootstrapContext()
    {
        var property = typeof(HolmasGameBootstrap).GetProperty(nameof(HolmasGameBootstrap.Context), BindingFlags.Public | BindingFlags.Static);
        if (property == null)
        {
            throw new InvalidOperationException("Holmas smoke test could not locate HolmasGameBootstrap.Context.");
        }

        var setter = property.GetSetMethod(true);
        if (setter == null)
        {
            throw new InvalidOperationException("Holmas smoke test could not access HolmasGameBootstrap.Context setter.");
        }

        setter.Invoke(null, new object[] { null });
    }

    private static HolmasConfigCatalogBundle LoadExportedConfigBundle()
    {
        string corePath = Path.Combine(Application.dataPath, "HotUpdateContent/Config/holmas_core_config.bytes");
        string catMetaPath = Path.Combine(Application.dataPath, "HotUpdateContent/Config/holmas_cat_meta.bytes");

        if (!File.Exists(corePath))
        {
            throw new InvalidOperationException($"Holmas smoke test could not find exported core config bytes: {corePath}");
        }

        if (!File.Exists(catMetaPath))
        {
            throw new InvalidOperationException($"Holmas smoke test could not find exported cat meta bytes: {catMetaPath}");
        }

        byte[] coreBytes = File.ReadAllBytes(corePath);
        byte[] catBytes = File.ReadAllBytes(catMetaPath);
        if (!HolmasConfigCatalogFactory.TryCreateFromBinary(coreBytes, catBytes, out HolmasConfigCatalogBundle bundle, out HolmasConfigReport report))
        {
            string reportText = report == null
                ? "no report"
                : string.Join("; ", report.Errors.Concat(report.Warnings));
            throw new InvalidOperationException($"Holmas smoke test failed to recover exported config bundle: {reportText}");
        }

        if (bundle == null)
        {
            throw new InvalidOperationException("Holmas smoke test recovered a null config bundle from exported bytes.");
        }

        return bundle;
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

    private sealed class RecordingServiceContainer : IServiceContainer
    {
        private readonly Dictionary<Type, object> _instances = new Dictionary<Type, object>();

        public void RegisterSingleton<T>(T instance) where T : class
        {
            _instances[typeof(T)] = instance;
        }

        public T Get<T>() where T : class
        {
            return _instances.TryGetValue(typeof(T), out object instance) ? instance as T : null;
        }

        public bool IsRegistered<T>()
        {
            return _instances.ContainsKey(typeof(T));
        }
    }

    private sealed class ProjectAssetsRuntime : IAssetsRuntime
    {
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
            var asset = AssetDatabase.LoadMainAssetAtPath(location);
            if (asset == null)
            {
                return System.Threading.Tasks.Task.FromResult<IAssetHandle>(new FakeAssetHandle(null));
            }

            return System.Threading.Tasks.Task.FromResult<IAssetHandle>(new FakeAssetHandle(asset));
        }

        public void Shutdown()
        {
        }
    }

    private sealed class InMemoryPersistence : IPersistence
    {
        private readonly Dictionary<string, byte[]> _store = new Dictionary<string, byte[]>();

        public System.Threading.Tasks.Task<bool> SaveAsync(string key, byte[] data)
        {
            _store[key] = data;
            return System.Threading.Tasks.Task.FromResult(true);
        }

        public System.Threading.Tasks.Task<byte[]> LoadAsync(string key)
        {
            _store.TryGetValue(key, out byte[] value);
            return System.Threading.Tasks.Task.FromResult(value);
        }

        public System.Threading.Tasks.Task<bool> DeleteAsync(string key)
        {
            _store.Remove(key);
            return System.Threading.Tasks.Task.FromResult(true);
        }

        public bool Exists(string key)
        {
            return _store.ContainsKey(key);
        }
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

        public IEventSubscription SubscribeScoped<T>(
            Action<T> handler,
            int priority = 0,
            Predicate<T> condition = null) where T : class
        {
            return new NullSubscription();
        }
    }

    private sealed class NullSubscription : IEventSubscription
    {
        public void Dispose()
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
