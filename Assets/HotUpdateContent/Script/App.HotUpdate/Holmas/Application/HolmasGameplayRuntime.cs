using System;
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

namespace App.HotUpdate.Holmas.Application
{
    /// <summary>
    /// Holmas 当前阶段的运行时编排入口。
    /// 在不接 UI 的前提下，把关卡、任务栏和长期进度串成一条稳定的应用内调用链。
    /// </summary>
    public sealed class HolmasGameplayRuntime
    {
        private readonly HolmasTaskProgressService _taskProgressService;
        private readonly HolmasMetaProgressionService _metaProgressionService;
        private readonly HolmasProgressionCoordinator _progressionCoordinator;
        private readonly IAppLogger _logger;
        private bool _currentLevelCompletionApplied;

        public HolmasGameplayRuntime(
            HolmasTaskProgressService taskProgressService,
            HolmasMetaProgressionService metaProgressionService,
            HolmasProgressionCoordinator progressionCoordinator,
            IAppLogger logger)
        {
            _taskProgressService = taskProgressService ?? throw new ArgumentNullException(nameof(taskProgressService));
            _metaProgressionService = metaProgressionService ?? throw new ArgumentNullException(nameof(metaProgressionService));
            _progressionCoordinator = progressionCoordinator ?? throw new ArgumentNullException(nameof(progressionCoordinator));
            _logger = logger;

            TaskBarState = _taskProgressService.CreateDefaultTaskBarState();
            MetaProgressionState = _metaProgressionService.CreateState();
            if (MetaProgressionState.AgencyLevel <= 0)
            {
                MetaProgressionState.AgencyLevel = 1;
            }
        }

        /// <summary>
        /// 当前任务栏运行时状态。
        /// </summary>
        public HolmasTaskBarState TaskBarState { get; }

        /// <summary>
        /// 当前长期进度运行时状态。
        /// </summary>
        public HolmasMetaProgressionState MetaProgressionState { get; }

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
            return result;
        }

        /// <summary>
        /// 解锁一个广告槽位，并在需要时立即补任务。
        /// </summary>
        public HolmasTaskSlotUnlockResult UnlockAdSlot(int slotIndex, int playerLevel, long unlockExpireAtUtcMilliseconds)
        {
            HolmasTaskSlotUnlockResult result = _taskProgressService.UnlockAdSlot(TaskBarState, slotIndex, playerLevel, unlockExpireAtUtcMilliseconds);
            _logger?.LogInfo("HolmasGameplayRuntime: 尝试解锁任务槽位 {0}，成功={1}。", slotIndex, result.Success);
            return result;
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
            CurrentBoardRuntime = new BoardRuntime(CurrentBoardTemplate, CurrentLevelSnapshot);
            _currentLevelCompletionApplied = false;
            _logger?.LogInfo("HolmasGameplayRuntime: 已启动地图 {0}，本局猫数量={1}。", CurrentLevelSnapshot.MapId, CurrentBoardRuntime.TotalCatCount);
            return CurrentBoardRuntime;
        }

        /// <summary>
        /// 直接用模板和快照启动一局新关卡。
        /// </summary>
        public BoardRuntime StartLevel(BoardTemplate template, LevelSnapshot snapshot)
        {
            CurrentBoardTemplate = template ?? throw new ArgumentNullException(nameof(template));
            CurrentLevelSnapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            CurrentBoardRuntime = new BoardRuntime(CurrentBoardTemplate, CurrentLevelSnapshot);
            _currentLevelCompletionApplied = false;
            _logger?.LogInfo("HolmasGameplayRuntime: 已基于现有模板与快照恢复关卡，地图={0}。", CurrentLevelSnapshot.MapId);
            return CurrentBoardRuntime;
        }

        /// <summary>
        /// 翻开格子；若因此完成当前地图，则立即推进任务与长期进度。
        /// </summary>
        public BoardRevealResult RevealCell(int cellIndex, out HolmasProgressionAdvanceResult progressionResult)
        {
            progressionResult = null;

            if (CurrentBoardRuntime == null)
            {
                throw new InvalidOperationException("HolmasGameplayRuntime: 当前还没有启动中的关卡。");
            }

            BoardRevealResult revealResult = CurrentBoardRuntime.Reveal(cellIndex);
            if (revealResult.IsValidAction && revealResult.Completed)
            {
                progressionResult = ApplyCurrentLevelCompletion();
            }

            return revealResult;
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
                CurrentLevelSnapshot.SpawnedCats);
            _currentLevelCompletionApplied = true;

            _logger?.LogInfo(
                "HolmasGameplayRuntime: 已完成地图结算，推进任务 {0} 条，新增完成 {1} 条，长期经验 +{2}。",
                result.ProgressedTaskIds.Count,
                result.CompletedTaskIds.Count,
                result.MetaExperienceGained);
            return result;
        }

        /// <summary>
        /// 领取一个任务奖励，并同步推进长期进度。
        /// </summary>
        public HolmasTaskClaimResult ClaimTaskReward(int slotIndex, int playerLevel)
        {
            HolmasTaskRuntimeInstance taskBeforeClaim = TaskBarState.GetTaskBySlot(slotIndex);
            HolmasTaskClaimResult result = _taskProgressService.ClaimTaskReward(TaskBarState, slotIndex, playerLevel);
            if (result.Success && taskBeforeClaim != null)
            {
                _progressionCoordinator.ApplyTaskClaim(taskBeforeClaim, MetaProgressionState);
            }

            _logger?.LogInfo("HolmasGameplayRuntime: 尝试领取槽位 {0} 任务奖励，成功={1}。", slotIndex, result.Success);
            return result;
        }
    }
}
