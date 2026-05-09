using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using App.HotUpdate.Holmas.Board;
using App.HotUpdate.Holmas.Levels;
using App.HotUpdate.Holmas.Meta;
using App.HotUpdate.Holmas.Progression;
using App.HotUpdate.Holmas.Terrain;
using App.HotUpdate.Holmas.Tasks.Runtime;
using App.HotUpdate.Holmas.Tasks.Services;
using App.Shared.Contracts;
using App.Shared.Holmas.RuntimeData;
using UnityEngine;
using TerrainAssetPathUtility = App.HotUpdate.Holmas.Terrain.HolmasTerrainAssetPathUtility;
using HolmasAgencyPromotionUpgradeResult = App.HotUpdate.Holmas.Meta.HolmasAgencyUpgradeResult;

namespace App.HotUpdate.Holmas.Application
{
    [Serializable]
    public sealed class HolmasTaskSettlementResult
    {
        public readonly List<int> ClaimedSlotIndices = new List<int>();
        public int TotalReward;
        public int RefilledTaskCount;
        public HolmasTaskRefillResult RefillResult;

        public int ClaimedTaskCount => ClaimedSlotIndices.Count;
    }

    public enum HolmasGameplayRuntimeStateChangeReason
    {
        TasksRefilled,
        AdSlotUnlocked,
        ExpiredAdSlotsRefreshed,
        LevelStarted,
        LevelRevealed,
        LevelFlagToggled,
        LevelCompleted,
        OfflineSettlementApplied,
        TaskRewardClaimed,
        PromotionUpgraded,
        EnergyChanged,
        DebugGoldChanged,
        CurrentLevelSessionEnded,
    }

    /// <summary>
    /// Holmas 当前阶段的运行时编排入口。
    /// 在不接 UI 的前提下，把关卡、任务栏和长期进度串成一条稳定的应用内调用链。
    /// </summary>
    public sealed class HolmasGameplayRuntime : ITickable
    {
        public const int DefaultEnergyRecoveryLimit = 50;
        public const int DebugEnergyGrantAmount = 5;
        public const int DebugGoldGrantAmount = 1_000_000;
        public const int WalkCatEnergyCost = 2;
        public const int FindRevealEnergyCost = 1;
        public const long EnergyRecoveryIntervalMilliseconds = 12L * 60L * 1000L;
        private const string CoreFindCatTutorialMapId = "tutorial_core_find_cat_v1";

        private readonly HolmasTaskProgressService _taskProgressService;
        private readonly HolmasMetaProgressionService _metaProgressionService;
        private readonly HolmasProgressionCoordinator _progressionCoordinator;
        private readonly HolmasAgencyProgressionService _promotionProgressionService;
        private readonly IHolmasUtcClock _clock;
        private readonly IAppLogger _logger;
        private readonly IAssetsRuntime _assetsRuntime;

        // 可选事件总线。
        // Runtime 仍然能在没有事件总线的纯逻辑测试里独立运行；
        // Bootstrap 正式启动时会注入 AOT EventBus，让 UI、教程、调试工具可以订阅领域事件。
        private readonly IEventBus _eventBus;
        private bool _currentLevelCompletionApplied;
        private float _energyRefreshAccumulator;

        public HolmasGameplayRuntime(
            HolmasTaskProgressService taskProgressService,
            HolmasMetaProgressionService metaProgressionService,
            HolmasProgressionCoordinator progressionCoordinator,
            IAppLogger logger)
            : this(taskProgressService, metaProgressionService, progressionCoordinator, null, logger, null, null, null, null)
        {
        }

        public HolmasGameplayRuntime(
            HolmasTaskProgressService taskProgressService,
            HolmasMetaProgressionService metaProgressionService,
            HolmasProgressionCoordinator progressionCoordinator,
            IAppLogger logger,
            IAssetsRuntime assetsRuntime)
            : this(taskProgressService, metaProgressionService, progressionCoordinator, null, logger, assetsRuntime, null, null, null)
        {
        }

        public HolmasGameplayRuntime(
            HolmasTaskProgressService taskProgressService,
            HolmasMetaProgressionService metaProgressionService,
            HolmasProgressionCoordinator progressionCoordinator,
            HolmasAgencyProgressionService agencyProgressionService,
            IAppLogger logger,
            IAssetsRuntime assetsRuntime)
            : this(taskProgressService, metaProgressionService, progressionCoordinator, agencyProgressionService, logger, assetsRuntime, null, null, null)
        {
        }

        public HolmasGameplayRuntime(
            HolmasTaskProgressService taskProgressService,
            HolmasMetaProgressionService metaProgressionService,
            HolmasProgressionCoordinator progressionCoordinator,
            HolmasAgencyProgressionService agencyProgressionService,
            IAppLogger logger,
            IAssetsRuntime assetsRuntime,
            HolmasTaskBarState initialTaskBarState,
            HolmasMetaProgressionState initialMetaProgressionState,
            IHolmasUtcClock clock = null,
            IEventBus eventBus = null)
        {
            _taskProgressService = taskProgressService ?? throw new ArgumentNullException(nameof(taskProgressService));
            _metaProgressionService = metaProgressionService ?? throw new ArgumentNullException(nameof(metaProgressionService));
            _progressionCoordinator = progressionCoordinator ?? throw new ArgumentNullException(nameof(progressionCoordinator));
            _promotionProgressionService = agencyProgressionService ?? new HolmasAgencyProgressionService(new HolmasAgencyCatalog(), _metaProgressionService);
            _clock = clock ?? new HolmasSystemUtcClock();
            _logger = logger;
            _assetsRuntime = assetsRuntime;
            _eventBus = eventBus;

            TaskBarState = initialTaskBarState ?? _taskProgressService.CreateDefaultTaskBarState();
            MetaProgressionState = initialMetaProgressionState ?? _metaProgressionService.CreateState();
            if (MetaProgressionState.PlayerLevel <= 0)
            {
                MetaProgressionState.PlayerLevel = 1;
            }
            if (MetaProgressionState.AgencyStageId <= 0)
            {
                MetaProgressionState.AgencyStageId = 1;
            }
            EnsureEnergyState(_clock.UtcNowMilliseconds);
            RefreshEnergyRecovery();
        }

        public event Action<HolmasGameplayRuntimeStateChangeReason> StateChanged;

        /// <summary>
        /// 当前任务栏运行时状态。
        /// </summary>
        public HolmasTaskBarState TaskBarState { get; private set; }

        /// <summary>
        /// 当前长期进度运行时状态。
        /// </summary>
        public HolmasMetaProgressionState MetaProgressionState { get; }

        /// <summary>
        /// 当前玩家等级，供上层无 UI 组合层直接读取。
        /// </summary>
        public int CurrentPlayerLevel => MetaProgressionState?.PlayerLevel ?? 1;

        /// <summary>
        /// 当前侦探社阶段。
        /// </summary>
        public int CurrentAgencyStageId => MetaProgressionState?.AgencyStageId ?? 1;

        /// <summary>
        /// 当前金币余额。
        /// </summary>
        public long CurrentGoldBalance => MetaProgressionState?.GoldBalance ?? 0L;

        public int CurrentEnergy => MetaProgressionState?.EnergyCurrent ?? DefaultEnergyRecoveryLimit;

        public int EnergyRecoveryLimit => MetaProgressionState != null && MetaProgressionState.EnergyRecoveryLimit > 0
            ? MetaProgressionState.EnergyRecoveryLimit
            : DefaultEnergyRecoveryLimit;

        public string EnergyLabel => $"{CurrentEnergy}/{EnergyRecoveryLimit}";

        /// <summary>
        /// 最近一次任务领奖提示，只作为 UI 即时状态文案，不写入存档。
        /// </summary>
        public string LastTaskRewardTip { get; private set; } = string.Empty;

        public int LastTaskRewardTipVersion { get; private set; }

        /// <summary>
        /// 当前关卡模板。
        /// </summary>
        public BoardTemplate CurrentBoardTemplate { get; private set; }

        /// <summary>
        /// 当前关卡快照。
        /// </summary>
        public LevelSnapshot CurrentLevelSnapshot { get; private set; }

        /// <summary>
        /// 当前关卡棋盘运行时。
        /// </summary>
        public BoardRuntime CurrentBoardRuntime { get; private set; }

        /// <summary>
        /// 按当前等级补齐所有已解锁空槽位。
        /// </summary>
        public HolmasTaskRefillResult RefillAvailableTasks(int playerLevel)
        {
            HolmasTaskRefillResult result = _taskProgressService.RefillUnlockedEmptySlots(TaskBarState, playerLevel);
            _logger?.LogInfo("HolmasGameplayRuntime: 已尝试补齐任务栏，生成 {0} 条任务结果。", result.GeneratedTasks.Count);
            if (HasSuccessfulTaskGeneration(result))
            {
                NotifyStateChanged(HolmasGameplayRuntimeStateChangeReason.TasksRefilled);
            }
            return result;
        }

        /// <summary>
        /// 按当前玩家等级补齐所有已解锁空槽位。
        /// </summary>
        public HolmasTaskRefillResult RefillAvailableTasks()
        {
            return RefillAvailableTasks(CurrentPlayerLevel);
        }

        public HolmasTaskSettlementResult SettleClaimableTasksAndRefill(int playerLevel)
        {
            var result = new HolmasTaskSettlementResult();
            var claimableSlotIndices = new List<int>();
            if (TaskBarState != null && TaskBarState.Tasks != null)
            {
                for (int i = 0; i < TaskBarState.Tasks.Count; i++)
                {
                    HolmasTaskRuntimeInstance runtimeTask = TaskBarState.Tasks[i];
                    if (runtimeTask != null && runtimeTask.Task != null && runtimeTask.CanClaimReward)
                    {
                        claimableSlotIndices.Add(runtimeTask.Task.SlotIndex);
                    }
                }
            }

            foreach (int slotIndex in claimableSlotIndices.Distinct())
            {
                HolmasTaskRuntimeInstance taskBeforeClaim = TaskBarState.GetTaskBySlot(slotIndex);
                if (taskBeforeClaim == null || !taskBeforeClaim.CanClaimReward)
                {
                    continue;
                }

                HolmasTaskClaimResult claimResult = _taskProgressService.ClaimTaskReward(
                    TaskBarState,
                    slotIndex,
                    playerLevel,
                    refillEmptySlotImmediately: false);
                if (claimResult == null || !claimResult.Success)
                {
                    continue;
                }

                _progressionCoordinator.ApplyTaskClaim(taskBeforeClaim, MetaProgressionState);
                result.TotalReward += claimResult.Reward;
                result.ClaimedSlotIndices.Add(slotIndex);
                _logger?.LogInfo(
                    "HolmasGameplayRuntime: 任务槽位 {0} 兜底自动领奖成功，金币 +{1}。",
                    slotIndex,
                    claimResult.Reward);
            }

            result.RefillResult = RefillAvailableTasks(playerLevel);
            result.RefilledTaskCount = CountSuccessfulTaskGeneration(result.RefillResult);
            if (result.ClaimedTaskCount > 0)
            {
                SetLastTaskRewardTip(BuildTaskRewardTip(result.ClaimedSlotIndices, result.TotalReward, result.RefilledTaskCount));
                _logger?.LogInfo("HolmasGameplayRuntime: {0}", LastTaskRewardTip);
                PublishLeaderboardTaskRewardClaimed(result.TotalReward);
                NotifyStateChanged(HolmasGameplayRuntimeStateChangeReason.TaskRewardClaimed);
            }

            return result;
        }

        public HolmasTaskSettlementResult SettleClaimableTasksAndRefill()
        {
            return SettleClaimableTasksAndRefill(CurrentPlayerLevel);
        }

        /// <summary>
        /// 解锁一个广告槽位，并在需要时立即补任务。
        /// </summary>
        public HolmasTaskSlotUnlockResult UnlockAdSlot(int slotIndex, int playerLevel, long unlockExpireAtUtcMilliseconds)
        {
            HolmasTaskSlotUnlockResult result = _taskProgressService.UnlockAdSlot(TaskBarState, slotIndex, playerLevel, unlockExpireAtUtcMilliseconds);
            _logger?.LogInfo("HolmasGameplayRuntime: 尝试解锁任务槽位 {0}，成功={1}。", slotIndex, result.Success);
            if (result.Success)
            {
                NotifyStateChanged(HolmasGameplayRuntimeStateChangeReason.AdSlotUnlocked);
            }
            return result;
        }

        /// <summary>
        /// 使用当前玩家等级与当前成长配置解锁一个广告槽位。
        /// </summary>
        public HolmasTaskSlotUnlockResult UnlockAdSlot(int slotIndex, long nowUtcMilliseconds)
        {
            long unlockExpireAtUtcMilliseconds = GetAdUnlockExpireAt(nowUtcMilliseconds);
            return UnlockAdSlot(slotIndex, CurrentPlayerLevel, unlockExpireAtUtcMilliseconds);
        }

        /// <summary>
        /// 使用当前运行时时钟、玩家等级与成长配置解锁一个广告槽位。
        /// </summary>
        public HolmasTaskSlotUnlockResult UnlockAdSlot(int slotIndex)
        {
            return UnlockAdSlot(slotIndex, _clock.UtcNowMilliseconds);
        }

        /// <summary>
        /// 用地形资产和关卡生成请求启动一局新关卡。
        /// </summary>
        public BoardRuntime StartLevel(UnityEngine.Object terrainAsset, LevelGenerationRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            CurrentBoardTemplate = TerrainBoardTemplateConverter.Convert(terrainAsset);
            CurrentLevelSnapshot = LevelSnapshotFactory.Create(CurrentBoardTemplate, request);
            NormalizeOrdinaryBlindBoxCatIds(CurrentLevelSnapshot);
            CurrentBoardRuntime = new BoardRuntime(CurrentBoardTemplate, CurrentLevelSnapshot);
            _currentLevelCompletionApplied = false;
            _logger?.LogInfo("HolmasGameplayRuntime: 已启动地图 {0}，本局猫数量={1}。", CurrentLevelSnapshot.MapId, CurrentBoardRuntime.TotalCatCount);
            NotifyStateChanged(HolmasGameplayRuntimeStateChangeReason.LevelStarted);
            return CurrentBoardRuntime;
        }

        /// <summary>
        /// 直接用模板和快照启动一局新关卡。
        /// </summary>
        public BoardRuntime StartLevel(BoardTemplate template, LevelSnapshot snapshot)
        {
            CurrentBoardTemplate = template ?? throw new ArgumentNullException(nameof(template));
            CurrentLevelSnapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            NormalizeOrdinaryBlindBoxCatIds(CurrentLevelSnapshot);
            CurrentBoardRuntime = new BoardRuntime(CurrentBoardTemplate, CurrentLevelSnapshot);
            _currentLevelCompletionApplied = false;
            _logger?.LogInfo("HolmasGameplayRuntime: 已基于现有模板与快照恢复关卡，地图={0}。", CurrentLevelSnapshot.MapId);
            NotifyStateChanged(HolmasGameplayRuntimeStateChangeReason.LevelStarted);
            return CurrentBoardRuntime;
        }

        /// <summary>
        /// 按已保存的 LevelSnapshot 恢复当前关卡。
        /// 本阶段只恢复猫分布和揭示进度，不恢复旗标状态。
        /// </summary>
        public async Task<BoardRuntime> RestoreLevelAsync(LevelSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (_assetsRuntime == null)
            {
                throw new InvalidOperationException("HolmasGameplayRuntime: 当前实例没有接入 IAssetsRuntime，无法恢复已保存关卡。");
            }

            BoardTemplate template = await HolmasTerrainAssetLoader.LoadBoardTemplateAsync(_assetsRuntime, snapshot.TerrainPath);
            CurrentBoardTemplate = template ?? throw new InvalidOperationException("HolmasGameplayRuntime: 恢复关卡时未能得到有效 BoardTemplate。");
            CurrentLevelSnapshot = CloneLevelSnapshot(snapshot);
            NormalizeOrdinaryBlindBoxCatIds(CurrentLevelSnapshot);
            CurrentBoardRuntime = new BoardRuntime(CurrentBoardTemplate, CurrentLevelSnapshot);
            _currentLevelCompletionApplied = CurrentBoardRuntime.Completed;
            _logger?.LogInfo("HolmasGameplayRuntime: 已恢复未完成关卡，地图={0}。", CurrentLevelSnapshot.MapId);
            return CurrentBoardRuntime;
        }

        /// <summary>
        /// 按关卡请求中的 TerrainPath 先从正式资源入口加载地形，再启动一局新关卡。
        /// </summary>
        public async Task<BoardRuntime> StartLevelAsync(LevelGenerationRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (_assetsRuntime == null)
            {
                throw new InvalidOperationException("HolmasGameplayRuntime: 当前实例没有接入 IAssetsRuntime，无法按 TerrainPath 启动地图。");
            }

            string terrainLocation = TerrainAssetPathUtility.NormalizeStoredTerrainPath(request.TerrainPath);
            if (string.IsNullOrWhiteSpace(terrainLocation))
            {
                throw new InvalidOperationException("HolmasGameplayRuntime: 关卡请求缺少可加载的 TerrainPath。");
            }

            Task<IAssetHandle> terrainLoadTask = _assetsRuntime.LoadAssetAsync(terrainLocation);
            if (terrainLoadTask == null)
            {
                throw new InvalidOperationException($"HolmasGameplayRuntime: 资源入口返回了空的加载任务 '{terrainLocation}'。");
            }

            IAssetHandle terrainHandle = await terrainLoadTask;
            if (terrainHandle == null || terrainHandle.AssetObject == null)
            {
                throw new InvalidOperationException($"HolmasGameplayRuntime: 无法从资源入口加载地形 '{terrainLocation}'。");
            }

            try
            {
                _logger?.LogInfo("HolmasGameplayRuntime: 正在按 TerrainPath {0} 启动地图 {1}。", terrainLocation, request.MapId);
                return StartLevel(terrainHandle.AssetObject, request);
            }
            finally
            {
                terrainHandle.Release();
            }
        }

        /// <summary>
        /// 结束当前关卡会话，释放本局状态。
        /// </summary>
        public void EndCurrentLevelSession()
        {
            bool hadSession = CurrentBoardRuntime != null || CurrentBoardTemplate != null || CurrentLevelSnapshot != null;
            CurrentBoardRuntime = null;
            CurrentBoardTemplate = null;
            CurrentLevelSnapshot = null;
            _currentLevelCompletionApplied = false;
            _logger?.LogInfo("HolmasGameplayRuntime: 已清理当前关卡会话。");
            if (hadSession)
            {
                NotifyStateChanged(HolmasGameplayRuntimeStateChangeReason.CurrentLevelSessionEnded);
            }
        }

        public void RestoreTaskBarState(HolmasTaskBarState state)
        {
            TaskBarState = state ?? throw new ArgumentNullException(nameof(state));
            _logger?.LogInfo("HolmasGameplayRuntime: 已恢复任务栏快照。");
            NotifyStateChanged(HolmasGameplayRuntimeStateChangeReason.TasksRefilled);
        }

        public bool HasActiveUncompletedLevel =>
            CurrentLevelSnapshot != null &&
            CurrentBoardRuntime != null &&
            !CurrentLevelSnapshot.Completed &&
            !CurrentBoardRuntime.Completed;

        public bool IsCurrentLevelCompatibleWithTaskBar()
        {
            return true;
        }

        public bool RefreshExpiredAdSlots()
        {
            bool changed = _taskProgressService.RefreshExpiredAdSlots(TaskBarState);
            if (changed)
            {
                _logger?.LogInfo("HolmasGameplayRuntime: 启动或刷新阶段清理了过期广告槽位。");
                NotifyStateChanged(HolmasGameplayRuntimeStateChangeReason.ExpiredAdSlotsRefreshed);
            }

            return changed;
        }

        /// <summary>
        /// 当前长期成长配置下，广告槽位的到期时间。
        /// </summary>
        public long GetAdUnlockExpireAt(long nowUtcMilliseconds)
        {
            return _metaProgressionService.GetUnlockExpireAt(MetaProgressionState, nowUtcMilliseconds);
        }

        /// <summary>
        /// 翻开格子；若因此完成当前地图，则立即推进任务与长期进度。
        /// </summary>
        public BoardRevealResult RevealCell(int cellIndex, out HolmasProgressionAdvanceResult progressionResult)
        {
            return RevealCell(cellIndex, HolmasBoardInteractionMode.Walk, out progressionResult);
        }

        /// <summary>
        /// 按找猫交互模式翻开格子；Walk 只在踩到猫时扣 2，Find 固定扣 1。
        /// </summary>
        public BoardRevealResult RevealCell(
            int cellIndex,
            HolmasBoardInteractionMode interactionMode,
            out HolmasProgressionAdvanceResult progressionResult)
        {
            progressionResult = null;

            if (CurrentBoardRuntime == null)
            {
                throw new InvalidOperationException("HolmasGameplayRuntime: 当前还没有启动中的关卡。");
            }

            RefreshEnergyRecovery();
            int energyCost = GetRevealEnergyCost(cellIndex, interactionMode);
            if (energyCost > 0 && !TryConsumeRevealEnergy(energyCost))
            {
                return new BoardRevealResult(cellIndex)
                {
                    IsValidAction = false,
                    IsIgnored = true,
                    FailureReason = "体力不足。",
                };
            }

            BoardRevealResult revealResult = CurrentBoardRuntime.Reveal(cellIndex, ignoreFlag: true);
            if (revealResult.IsValidAction && revealResult.FoundCat)
            {
                AssignCatIdsForRevealedBlindBoxes(revealResult);
            }

            if (revealResult.IsValidAction && revealResult.Completed)
            {
                ApplyFoundCatProgress(revealResult);
                progressionResult = ApplyCurrentLevelCompletion();
            }
            else if (revealResult.IsValidAction)
            {
                ApplyFoundCatProgress(revealResult);
                NotifyStateChanged(HolmasGameplayRuntimeStateChangeReason.LevelRevealed);
            }

            return revealResult;
        }

        public bool RefreshEnergyRecovery()
        {
            long nowUtcMilliseconds = _clock.UtcNowMilliseconds;
            EnsureEnergyState(nowUtcMilliseconds);

            if (MetaProgressionState.EnergyCurrent >= MetaProgressionState.EnergyRecoveryLimit)
            {
                return false;
            }

            long lastRecoveryAt = MetaProgressionState.EnergyLastRecoveryAtUtcMilliseconds;
            if (lastRecoveryAt <= 0L || nowUtcMilliseconds <= lastRecoveryAt)
            {
                MetaProgressionState.EnergyLastRecoveryAtUtcMilliseconds = nowUtcMilliseconds;
                return false;
            }

            long elapsed = nowUtcMilliseconds - lastRecoveryAt;
            int recovered = (int)(elapsed / EnergyRecoveryIntervalMilliseconds);
            if (recovered <= 0)
            {
                return false;
            }

            int missing = MetaProgressionState.EnergyRecoveryLimit - MetaProgressionState.EnergyCurrent;
            int applied = Math.Min(missing, recovered);
            MetaProgressionState.EnergyCurrent += applied;
            MetaProgressionState.EnergyLastRecoveryAtUtcMilliseconds =
                MetaProgressionState.EnergyCurrent >= MetaProgressionState.EnergyRecoveryLimit
                    ? nowUtcMilliseconds
                    : lastRecoveryAt + recovered * EnergyRecoveryIntervalMilliseconds;

            _logger?.LogInfo("HolmasGameplayRuntime: 体力自然恢复 +{0}，当前={1}/{2}。", applied, CurrentEnergy, EnergyRecoveryLimit);
            NotifyStateChanged(HolmasGameplayRuntimeStateChangeReason.EnergyChanged);
            return true;
        }

        public void AddEnergy(int amount = DebugEnergyGrantAmount)
        {
            if (amount <= 0)
            {
                return;
            }

            RefreshEnergyRecovery();
            long nowUtcMilliseconds = _clock.UtcNowMilliseconds;
            EnsureEnergyState(nowUtcMilliseconds);
            MetaProgressionState.EnergyCurrent = Math.Max(0, MetaProgressionState.EnergyCurrent) + amount;
            if (MetaProgressionState.EnergyCurrent >= MetaProgressionState.EnergyRecoveryLimit)
            {
                MetaProgressionState.EnergyLastRecoveryAtUtcMilliseconds = nowUtcMilliseconds;
            }

            _logger?.LogInfo("HolmasGameplayRuntime: 体力增加 +{0}，当前={1}/{2}。", amount, CurrentEnergy, EnergyRecoveryLimit);
            NotifyStateChanged(HolmasGameplayRuntimeStateChangeReason.EnergyChanged);
        }

        public void AddGold(int amount = DebugGoldGrantAmount)
        {
            if (amount <= 0 || MetaProgressionState == null)
            {
                return;
            }

            MetaProgressionState.GoldBalance = Math.Max(0L, MetaProgressionState.GoldBalance) + amount;
            _logger?.LogInfo("HolmasGameplayRuntime: GM 金币增加 +{0}，当前={1}。", amount, CurrentGoldBalance);
            NotifyStateChanged(HolmasGameplayRuntimeStateChangeReason.DebugGoldChanged);
        }

        public void Tick(float deltaTime)
        {
            _energyRefreshAccumulator += Math.Max(0f, deltaTime);
            if (_energyRefreshAccumulator < 1f)
            {
                return;
            }

            _energyRefreshAccumulator = 0f;
            RefreshEnergyRecovery();
        }

        /// <summary>
        /// 切换一个格子的旗标状态。
        /// UI 通过运行时门面操作棋盘，而不是直接决定棋盘权威状态。
        /// </summary>
        public BoardRevealResult ToggleFlag(int cellIndex)
        {
            if (CurrentBoardRuntime == null)
            {
                throw new InvalidOperationException("HolmasGameplayRuntime: 当前还没有启动中的关卡。");
            }

            BoardRevealResult result = CurrentBoardRuntime.ToggleFlag(cellIndex);
            if (result.IsValidAction)
            {
                NotifyStateChanged(HolmasGameplayRuntimeStateChangeReason.LevelFlagToggled);
            }

            return result;
        }

        /// <summary>
        /// 当前地图完成后，把结果推进到任务栏与长期进度。
        /// </summary>
        public HolmasProgressionAdvanceResult ApplyCurrentLevelCompletion()
        {
            if (CurrentLevelSnapshot == null)
            {
                throw new InvalidOperationException("HolmasGameplayRuntime: 当前没有可结算的关卡快照。");
            }

            if (CurrentBoardRuntime == null)
            {
                throw new InvalidOperationException("HolmasGameplayRuntime: 当前没有启动中的关卡棋盘。");
            }

            if (!CurrentBoardRuntime.Completed)
            {
                throw new InvalidOperationException("HolmasGameplayRuntime: 当前地图尚未完成，不能重复或提前结算。");
            }

            if (_currentLevelCompletionApplied)
            {
                _logger?.LogWarning("HolmasGameplayRuntime: 当前地图已完成过一次结算，忽略重复结算请求。");
                return new HolmasProgressionAdvanceResult();
            }

            HolmasProgressionAdvanceResult result = _progressionCoordinator.ApplyMapCompletion(
                TaskBarState,
                MetaProgressionState,
                CurrentLevelSnapshot.SpawnedCats,
                applyTaskProgress: false);
            _currentLevelCompletionApplied = true;

            _logger?.LogInfo(
                "HolmasGameplayRuntime: 已完成地图结算，推进任务 {0} 条，新增完成 {1} 条。",
                result.ProgressedTaskIds.Count,
                result.CompletedTaskIds.Count);
            NotifyStateChanged(HolmasGameplayRuntimeStateChangeReason.LevelCompleted);
            return result;
        }

        private void ApplyFoundCatProgress(BoardRevealResult revealResult)
        {
            if (revealResult == null ||
                !revealResult.IsValidAction ||
                revealResult.FoundCatCellIndices == null ||
                revealResult.FoundCatCellIndices.Count == 0 ||
                CurrentBoardRuntime == null)
            {
                return;
            }

            IReadOnlyList<SpawnedCatData> foundCats = CurrentBoardRuntime.GetFoundCats(revealResult.FoundCatCellIndices);
            if (foundCats == null || foundCats.Count == 0)
            {
                return;
            }

            PublishLeaderboardCatsFound(foundCats.Count);
            HolmasTaskProgressResult progressResult = _taskProgressService.ApplyFoundCats(TaskBarState, foundCats);
            if (progressResult == null)
            {
                return;
            }

            if (progressResult.ProgressedTaskIds.Count == 0)
            {
                _logger?.LogWarning(
                    "HolmasGameplayRuntime: 本次翻格猫种 [{0}] 未推进任何任务；当前未完成任务猫池 [{1}]。请检查是否没有可推进任务猫或处于教程固定猫位。",
                    FormatFoundCatIds(foundCats),
                    FormatUncompletedTaskCatIds(TaskBarState));
            }

            int totalReward = 0;
            var claimedSlotIndices = new List<int>();
            for (int i = 0; i < progressResult.NewlyCompletedSlotIndices.Count; i++)
            {
                int slotIndex = progressResult.NewlyCompletedSlotIndices[i];
                HolmasTaskRuntimeInstance taskBeforeClaim = TaskBarState.GetTaskBySlot(slotIndex);
                if (taskBeforeClaim == null || !taskBeforeClaim.CanClaimReward)
                {
                    continue;
                }

                HolmasTaskClaimResult claimResult = _taskProgressService.ClaimTaskReward(
                    TaskBarState,
                    slotIndex,
                    CurrentPlayerLevel,
                    refillEmptySlotImmediately: false);
                if (claimResult == null || !claimResult.Success)
                {
                    continue;
                }

                _progressionCoordinator.ApplyTaskClaim(taskBeforeClaim, MetaProgressionState);
                totalReward += claimResult.Reward;
                claimedSlotIndices.Add(slotIndex);
                _logger?.LogInfo(
                    "HolmasGameplayRuntime: 任务槽位 {0} 自动领奖成功，金币 +{1}。",
                    slotIndex,
                    claimResult.Reward);
            }

            if (claimedSlotIndices.Count > 0)
            {
                HolmasTaskRefillResult refillResult = RefillAvailableTasks(CurrentPlayerLevel);
                int generatedCount = CountSuccessfulTaskGeneration(refillResult);
                SetLastTaskRewardTip(BuildTaskRewardTip(claimedSlotIndices, totalReward, generatedCount));
                _logger?.LogInfo("HolmasGameplayRuntime: {0}", LastTaskRewardTip);
                PublishLeaderboardTaskRewardClaimed(totalReward);
                NotifyStateChanged(HolmasGameplayRuntimeStateChangeReason.TaskRewardClaimed);
            }

            _logger?.LogInfo(
                "HolmasGameplayRuntime: 本次翻格找到 {0} 只猫，推进任务 {1} 条，新完成 {2} 条。",
                foundCats.Count,
                progressResult.ProgressedTaskIds.Count,
                progressResult.NewlyCompletedTaskIds.Count);
        }

        /// <summary>
        /// 结算离线收益，当前 v1 只增加金币，不再直接给玩家经验。
        /// </summary>
        public HolmasProgressionAdvanceResult ApplyOfflineSettlement(long offlineMilliseconds)
        {
            HolmasProgressionAdvanceResult result = _progressionCoordinator.ApplyOfflineSettlement(MetaProgressionState, offlineMilliseconds);
            _logger?.LogInfo("HolmasGameplayRuntime: 已结算离线收益，金币 +{0}。", result.OfflineRewardGained);
            if (result.OfflineRewardGained > 0)
            {
                NotifyStateChanged(HolmasGameplayRuntimeStateChangeReason.OfflineSettlementApplied);
            }
            return result;
        }

        /// <summary>
        /// 领取一个任务奖励，并同步推进长期进度。
        /// </summary>
        public HolmasTaskClaimResult ClaimTaskReward(int slotIndex, int playerLevel)
        {
            HolmasTaskRuntimeInstance taskBeforeClaim = TaskBarState.GetTaskBySlot(slotIndex);
            HolmasTaskClaimResult result = _taskProgressService.ClaimTaskReward(TaskBarState, slotIndex, playerLevel, refillEmptySlotImmediately: false);
            if (result.Success && taskBeforeClaim != null)
            {
                _progressionCoordinator.ApplyTaskClaim(taskBeforeClaim, MetaProgressionState);
                HolmasTaskRefillResult refillResult = RefillAvailableTasks(playerLevel);
                result.RefilledTask = GetGeneratedTaskForSlot(refillResult, slotIndex);
                SetLastTaskRewardTip(BuildTaskRewardTip(new[] { slotIndex }, result.Reward, CountSuccessfulTaskGeneration(refillResult)));
            }

            _logger?.LogInfo("HolmasGameplayRuntime: 尝试领取槽位 {0} 任务奖励，成功={1}，金币 +{2}。", slotIndex, result.Success, result.Reward);
            if (result.Success)
            {
                PublishLeaderboardTaskRewardClaimed(result.Reward);
                NotifyStateChanged(HolmasGameplayRuntimeStateChangeReason.TaskRewardClaimed);
            }
            return result;
        }

        /// <summary>
        /// 按当前玩家等级领取任务奖励。
        /// </summary>
        public HolmasTaskClaimResult ClaimTaskReward(int slotIndex)
        {
            return ClaimTaskReward(slotIndex, CurrentPlayerLevel);
        }

        /// <summary>
        /// 按当前成长配置升级城市宣传功能。
        /// </summary>
        public HolmasAgencyPromotionUpgradeResult TryUpgradePromotion(string promotionId)
        {
            if (_promotionProgressionService == null)
            {
                throw new InvalidOperationException("HolmasGameplayRuntime: 当前没有可用的宣传成长服务。");
            }

            HolmasAgencyPromotionUpgradeResult result = _promotionProgressionService.TryUpgradePromotion(MetaProgressionState, promotionId);
            if (result.Success)
            {
                _logger?.LogInfo(
                    "HolmasGameplayRuntime: 宣传 {0} 升级到 {1}，金币 -{2}，玩家等级={3}，阶段推进={4}。",
                    result.PromotionId,
                    result.NewLevel,
                    result.GoldSpent,
                    result.PlayerLevelAfter,
                    result.StageAdvanced);
            }
            else
            {
                _logger?.LogWarning("HolmasGameplayRuntime: 宣传 {0} 升级失败，原因={1}", promotionId, result.FailureReason);
            }

            if (result.Success)
            {
                MetaProgressionState.CurrentLevelRankUpdatedAtUtcMilliseconds = _clock.UtcNowMilliseconds;
                NotifyStateChanged(HolmasGameplayRuntimeStateChangeReason.PromotionUpgraded);
            }

            return result;
        }

        private void AssignCatIdsForRevealedBlindBoxes(BoardRevealResult revealResult)
        {
            if (revealResult == null ||
                revealResult.FoundCatCellIndices == null ||
                revealResult.FoundCatCellIndices.Count == 0 ||
                CurrentBoardRuntime == null)
            {
                return;
            }

            bool isTutorialLevel = IsCoreFindCatTutorialLevel(CurrentLevelSnapshot);
            for (int i = 0; i < revealResult.FoundCatCellIndices.Count; i++)
            {
                int foundCellIndex = revealResult.FoundCatCellIndices[i];
                bool hasExistingCatId = CurrentBoardRuntime.TryGetCatIdAt(foundCellIndex, out _);
                if (hasExistingCatId && isTutorialLevel)
                {
                    continue;
                }

                if (!_taskProgressService.TryPickUncompletedTaskCatId(TaskBarState, out string catId))
                {
                    _logger?.LogWarning(
                        "HolmasGameplayRuntime: 猫位 {0} 已揭示，但当前没有未完成任务猫可用于解析猫种。",
                        foundCellIndex);
                    continue;
                }

                bool allowOverwriteExisting = hasExistingCatId && !isTutorialLevel;
                if (!CurrentBoardRuntime.TryAssignCatIdAt(foundCellIndex, catId, overwriteExisting: allowOverwriteExisting))
                {
                    _logger?.LogWarning(
                        "HolmasGameplayRuntime: 猫位 {0} 解析猫种 {1} 失败。",
                        foundCellIndex,
                        catId);
                    continue;
                }
            }
        }

        private static string FormatFoundCatIds(IReadOnlyList<SpawnedCatData> foundCats)
        {
            if (foundCats == null || foundCats.Count == 0)
            {
                return "none";
            }

            string summary = string.Join(
                ",",
                foundCats
                    .Where(item => item != null && !string.IsNullOrWhiteSpace(item.CatId))
                    .Select(item => $"{item.CellIndex}:{item.CatId}"));
            return string.IsNullOrWhiteSpace(summary) ? "none" : summary;
        }

        private static bool IsCoreFindCatTutorialLevel(LevelSnapshot snapshot)
        {
            return snapshot != null &&
                   string.Equals(snapshot.MapId, CoreFindCatTutorialMapId, StringComparison.Ordinal);
        }

        private static void NormalizeOrdinaryBlindBoxCatIds(LevelSnapshot snapshot)
        {
            if (snapshot == null ||
                IsCoreFindCatTutorialLevel(snapshot) ||
                snapshot.SpawnedCats == null ||
                snapshot.SpawnedCats.Count == 0)
            {
                return;
            }

            bool[] revealedCells = snapshot.RevealedCells ?? Array.Empty<bool>();
            for (int i = 0; i < snapshot.SpawnedCats.Count; i++)
            {
                SpawnedCatData spawnedCat = snapshot.SpawnedCats[i];
                if (spawnedCat == null)
                {
                    continue;
                }

                bool isRevealed = spawnedCat.CellIndex >= 0 &&
                                  spawnedCat.CellIndex < revealedCells.Length &&
                                  revealedCells[spawnedCat.CellIndex];
                if (!isRevealed)
                {
                    spawnedCat.CatId = string.Empty;
                }
            }
        }

        private static string FormatUncompletedTaskCatIds(HolmasTaskBarState taskBarState)
        {
            if (taskBarState == null || taskBarState.Tasks == null || taskBarState.Tasks.Count == 0)
            {
                return "none";
            }

            string[] catIds = taskBarState.Tasks
                .Where(item => item != null &&
                               item.Task != null &&
                               !item.IsRewardClaimed &&
                               item.Task.CurrentCount < item.Task.TargetCount &&
                               !string.IsNullOrWhiteSpace(item.Task.CatId))
                .Select(item => $"slot{item.Task.SlotIndex}:{item.Task.CatId}:{item.Task.CurrentCount}/{item.Task.TargetCount}")
                .ToArray();

            return catIds.Length > 0 ? string.Join(",", catIds) : "none";
        }

        private static bool HasSuccessfulTaskGeneration(HolmasTaskRefillResult result)
        {
            if (result == null || result.GeneratedTasks == null)
            {
                return false;
            }

            for (int i = 0; i < result.GeneratedTasks.Count; i++)
            {
                if (result.GeneratedTasks[i] != null && result.GeneratedTasks[i].Success)
                {
                    return true;
                }
            }

            return false;
        }

        private static int CountSuccessfulTaskGeneration(HolmasTaskRefillResult result)
        {
            if (result == null || result.GeneratedTasks == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < result.GeneratedTasks.Count; i++)
            {
                if (result.GeneratedTasks[i] != null && result.GeneratedTasks[i].Success)
                {
                    count++;
                }
            }

            return count;
        }

        private static TaskInstanceData GetGeneratedTaskForSlot(HolmasTaskRefillResult result, int slotIndex)
        {
            if (result == null || result.GeneratedTasks == null)
            {
                return null;
            }

            for (int i = 0; i < result.GeneratedTasks.Count; i++)
            {
                HolmasTaskGenerationResult generation = result.GeneratedTasks[i];
                if (generation != null && generation.Success && generation.SlotIndex == slotIndex)
                {
                    return generation.Task;
                }
            }

            return null;
        }

        private static string BuildTaskRewardTip(IReadOnlyCollection<int> slotIndices, int totalReward, int generatedTaskCount)
        {
            int claimedCount = slotIndices != null ? slotIndices.Count : 0;
            string rewardText = $"金币 +{Math.Max(0, totalReward)}";
            string refillText = generatedTaskCount > 0
                ? $"已刷新 {generatedTaskCount} 个新任务。"
                : "对应任务栏已清空。";
            if (claimedCount <= 1)
            {
                int slotIndex = slotIndices != null && slotIndices.Count > 0 ? slotIndices.First() + 1 : 0;
                return slotIndex > 0
                    ? $"任务槽 {slotIndex} 完成，{rewardText}。{refillText}"
                    : $"任务完成，{rewardText}。{refillText}";
            }

            return $"完成 {claimedCount} 个任务，{rewardText}。{refillText}";
        }

        private static LevelSnapshot CloneLevelSnapshot(LevelSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return null;
            }

            return new LevelSnapshot
            {
                MapId = snapshot.MapId ?? string.Empty,
                TerrainPath = snapshot.TerrainPath ?? string.Empty,
                Seed = snapshot.Seed,
                SpawnedCats = snapshot.SpawnedCats != null
                    ? snapshot.SpawnedCats
                        .ConvertAll(item => item == null
                            ? null
                            : new SpawnedCatData
                            {
                                CatId = item.CatId ?? string.Empty,
                                CellIndex = item.CellIndex,
                            })
                    : new List<SpawnedCatData>(),
                RevealedCells = snapshot.RevealedCells != null
                    ? (bool[])snapshot.RevealedCells.Clone()
                    : Array.Empty<bool>(),
                Completed = snapshot.Completed,
            };
        }

        private void NotifyStateChanged(HolmasGameplayRuntimeStateChangeReason reason)
        {
            // 顺序固定为先旧 StateChanged、再新领域事件。
            // HolmasPlayerArchiveSyncService 等已有服务依赖旧 StateChanged 做存档 dirty 标记，
            // 所以这里不能改成只发布 EventBus，也不能把新事件放到旧事件之前。
            StateChanged?.Invoke(reason);
            PublishDomainEvents(reason);
        }

        private void PublishLeaderboardTaskRewardClaimed(int rewardGold)
        {
            if (_eventBus == null || rewardGold <= 0)
            {
                return;
            }

            _eventBus.Publish(new HolmasLeaderboardTaskRewardClaimedEvent
            {
                Reason = HolmasGameplayRuntimeStateChangeReason.TaskRewardClaimed,
                RewardGold = rewardGold,
            });
        }

        private void PublishLeaderboardCatsFound(int foundCatCount)
        {
            if (_eventBus == null || foundCatCount <= 0)
            {
                return;
            }

            _eventBus.Publish(new HolmasLeaderboardCatsFoundEvent
            {
                Reason = HolmasGameplayRuntimeStateChangeReason.LevelRevealed,
                FoundCatCount = foundCatCount,
            });
        }

        /// <summary>
        /// 把 Runtime 内部的 reason 映射为外部可订阅的 Holmas 领域事件。
        /// </summary>
        /// <remarks>
        /// 这层是新 Game Event System 和旧 Runtime 之间的桥。
        /// Runtime 内部仍然用 NotifyStateChanged(reason) 表达“状态变了”，
        /// 这里再根据 reason 发布更具体的事件 DTO，避免 UI 到处 switch reason 或直接读取 Runtime 大对象。
        ///
        /// 所有 reason 都会发布 HolmasGameplayStateChangedEvent。
        /// 只有首轮已确认有明确使用场景的 reason 才额外发布专用事件。
        /// </remarks>
        private void PublishDomainEvents(HolmasGameplayRuntimeStateChangeReason reason)
        {
            if (_eventBus == null)
            {
                return;
            }

            // 先构造轻量任务栏摘要，避免把 TaskBarState 这种可变业务对象直接暴露给监听者。
            HolmasTaskBarSummary taskSummary = BuildTaskBarSummary();

            // 通用总事件：给调试面板、未来的全局 UI 刷新或兼容监听使用。
            _eventBus.Publish(new HolmasGameplayStateChangedEvent
            {
                Reason = reason,
                CurrentEnergy = CurrentEnergy,
                EnergyRecoveryLimit = EnergyRecoveryLimit,
                EnergyLabel = EnergyLabel,
                TaskRewardTip = LastTaskRewardTip,
                TaskRewardTipVersion = LastTaskRewardTipVersion,
                TaskTotalCount = taskSummary.TotalTaskCount,
                TaskClaimableCount = taskSummary.ClaimableTaskCount,
                TaskUnlockedSlotCount = taskSummary.UnlockedSlotCount,
                LevelMapId = CurrentLevelSnapshot != null ? CurrentLevelSnapshot.MapId ?? string.Empty : string.Empty,
                LevelSeed = CurrentLevelSnapshot != null ? CurrentLevelSnapshot.Seed : 0,
                LevelCompleted = CurrentLevelSnapshot != null && CurrentLevelSnapshot.Completed,
            });

            switch (reason)
            {
                case HolmasGameplayRuntimeStateChangeReason.EnergyChanged:
                    // 体力变化事件：首轮由 BattlePageController 订阅，用于替代旧 StateChanged 分支。
                    _eventBus.Publish(new HolmasEnergyChangedEvent
                    {
                        Reason = reason,
                        CurrentEnergy = CurrentEnergy,
                        EnergyRecoveryLimit = EnergyRecoveryLimit,
                        EnergyLabel = EnergyLabel,
                    });
                    break;

                case HolmasGameplayRuntimeStateChangeReason.TaskRewardClaimed:
                    // 任务领奖会同时影响“提示文案”和“任务栏结构/计数”，所以拆成两个专用事件。
                    _eventBus.Publish(new HolmasTaskRewardTipChangedEvent
                    {
                        Reason = reason,
                        Tip = LastTaskRewardTip,
                        Version = LastTaskRewardTipVersion,
                    });
                    PublishTaskBarChangedEvent(reason, taskSummary);
                    break;

                case HolmasGameplayRuntimeStateChangeReason.TasksRefilled:
                case HolmasGameplayRuntimeStateChangeReason.AdSlotUnlocked:
                case HolmasGameplayRuntimeStateChangeReason.ExpiredAdSlotsRefreshed:
                    // 这些 reason 只表示任务栏槽位或任务集合变化，不一定有新的奖励提示。
                    PublishTaskBarChangedEvent(reason, taskSummary);
                    break;

                case HolmasGameplayRuntimeStateChangeReason.LevelStarted:
                case HolmasGameplayRuntimeStateChangeReason.LevelRevealed:
                case HolmasGameplayRuntimeStateChangeReason.LevelFlagToggled:
                case HolmasGameplayRuntimeStateChangeReason.LevelCompleted:
                case HolmasGameplayRuntimeStateChangeReason.CurrentLevelSessionEnded:
                    // 关卡类事件只暴露地图 ID、seed、完成状态这些轻字段。
                    // 棋盘格子详情仍由 Runtime / BoardRuntime 管理，不在事件 DTO 中深拷。
                    _eventBus.Publish(new HolmasLevelStateChangedEvent
                    {
                        Reason = reason,
                        MapId = CurrentLevelSnapshot != null ? CurrentLevelSnapshot.MapId ?? string.Empty : string.Empty,
                        Seed = CurrentLevelSnapshot != null ? CurrentLevelSnapshot.Seed : 0,
                        Completed = CurrentLevelSnapshot != null && CurrentLevelSnapshot.Completed,
                    });
                    break;
            }
        }

        private void PublishTaskBarChangedEvent(
            HolmasGameplayRuntimeStateChangeReason reason,
            HolmasTaskBarSummary taskSummary)
        {
            _eventBus.Publish(new HolmasTaskBarChangedEvent
            {
                Reason = reason,
                TotalTaskCount = taskSummary.TotalTaskCount,
                ClaimableTaskCount = taskSummary.ClaimableTaskCount,
                UnlockedSlotCount = taskSummary.UnlockedSlotCount,
            });
        }

        /// <summary>
        /// 从当前任务栏计算事件 DTO 需要的轻量摘要。
        /// </summary>
        /// <remarks>
        /// 事件监听者通常只需要“有多少任务、多少可领奖、多少槽已解锁”来决定是否刷新 UI。
        /// 不把完整任务对象放进事件，可以减少跨模块耦合，也避免监听者误改 Runtime 内部状态。
        /// </remarks>
        private HolmasTaskBarSummary BuildTaskBarSummary()
        {
            var summary = new HolmasTaskBarSummary();
            if (TaskBarState == null)
            {
                return summary;
            }

            if (TaskBarState.Tasks != null)
            {
                summary.TotalTaskCount = TaskBarState.Tasks.Count;
                for (int i = 0; i < TaskBarState.Tasks.Count; i++)
                {
                    if (TaskBarState.Tasks[i] != null && TaskBarState.Tasks[i].CanClaimReward)
                    {
                        summary.ClaimableTaskCount++;
                    }
                }
            }

            if (TaskBarState.Slots != null)
            {
                for (int i = 0; i < TaskBarState.Slots.Count; i++)
                {
                    if (TaskBarState.Slots[i] != null && TaskBarState.Slots[i].IsUnlocked)
                    {
                        summary.UnlockedSlotCount++;
                    }
                }
            }

            return summary;
        }

        /// <summary>
        /// 任务栏轻量摘要，只在 Runtime 内部组装事件 payload 时使用。
        /// </summary>
        private struct HolmasTaskBarSummary
        {
            public int TotalTaskCount;
            public int ClaimableTaskCount;
            public int UnlockedSlotCount;
        }

        private void SetLastTaskRewardTip(string tip)
        {
            LastTaskRewardTip = tip ?? string.Empty;
            LastTaskRewardTipVersion++;
        }

        private void EnsureEnergyState(long nowUtcMilliseconds)
        {
            if (MetaProgressionState == null)
            {
                return;
            }

            if (!MetaProgressionState.EnergyInitialized || MetaProgressionState.EnergyRecoveryLimit <= 0)
            {
                MetaProgressionState.EnergyInitialized = true;
                MetaProgressionState.EnergyRecoveryLimit = DefaultEnergyRecoveryLimit;
                MetaProgressionState.EnergyCurrent = DefaultEnergyRecoveryLimit;
                MetaProgressionState.EnergyLastRecoveryAtUtcMilliseconds = nowUtcMilliseconds;
                return;
            }

            MetaProgressionState.EnergyCurrent = Math.Max(0, MetaProgressionState.EnergyCurrent);
            MetaProgressionState.EnergyRecoveryLimit = Math.Max(1, MetaProgressionState.EnergyRecoveryLimit);
            if (MetaProgressionState.EnergyLastRecoveryAtUtcMilliseconds <= 0L)
            {
                MetaProgressionState.EnergyLastRecoveryAtUtcMilliseconds = nowUtcMilliseconds;
            }
        }

        private int GetRevealEnergyCost(int cellIndex, HolmasBoardInteractionMode interactionMode)
        {
            if (CurrentBoardRuntime == null || CurrentBoardRuntime.Completed)
            {
                return 0;
            }

            BoardCellState state = CurrentBoardRuntime.GetCellState(cellIndex);
            if (!state.IsValid || state.IsRevealed)
            {
                return 0;
            }

            return interactionMode == HolmasBoardInteractionMode.Find
                ? FindRevealEnergyCost
                : (state.HasCat ? WalkCatEnergyCost : 0);
        }

        private bool TryConsumeRevealEnergy(int amount)
        {
            long nowUtcMilliseconds = _clock.UtcNowMilliseconds;
            EnsureEnergyState(nowUtcMilliseconds);
            if (amount <= 0)
            {
                return true;
            }

            if (MetaProgressionState.EnergyCurrent < amount)
            {
                return false;
            }

            int previousEnergy = MetaProgressionState.EnergyCurrent;
            MetaProgressionState.EnergyCurrent = Math.Max(0, MetaProgressionState.EnergyCurrent - amount);
            if (previousEnergy >= MetaProgressionState.EnergyRecoveryLimit ||
                MetaProgressionState.EnergyCurrent < MetaProgressionState.EnergyRecoveryLimit)
            {
                MetaProgressionState.EnergyLastRecoveryAtUtcMilliseconds = nowUtcMilliseconds;
            }

            _logger?.LogInfo("HolmasGameplayRuntime: 翻格消耗体力 {0}，当前={1}/{2}。", amount, CurrentEnergy, EnergyRecoveryLimit);
            NotifyStateChanged(HolmasGameplayRuntimeStateChangeReason.EnergyChanged);
            return true;
        }

    }
}
