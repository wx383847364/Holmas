using System;
using System.Threading.Tasks;
using App.HotUpdate.Holmas.Application;
using App.HotUpdate.Holmas.Progression;
using App.HotUpdate.Holmas.UI.Screens.Battle;
using App.HotUpdate.Holmas.UI.Screens.Loading;
using App.HotUpdate.Holmas.UI.Screens.Main;
using App.Shared.Holmas.RuntimeData;
using UnityEngine;

namespace App.HotUpdate.Holmas.UI.Core
{
    /// <summary>
    /// 收口启动页和 Main / Battle 主链切换，避免页面控制器直接编排跨屏状态。
    /// </summary>
    public sealed class HolmasFlowCoordinator
    {
        public const float MinimumLoadingVisibleSeconds = 2f;
        private const float ProgressStepAnimationSeconds = 0.25f;
        private const float CompletionHoldSeconds = 0.18f;

        private readonly UiRoot _root;
        private readonly IBattleWorldHost _battleWorldHost;
        private bool _startupCompleted;
        private bool _transitionInProgress;

        public HolmasFlowCoordinator(UiRoot root, IBattleWorldHost battleWorldHost)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            _battleWorldHost = battleWorldHost ?? throw new ArgumentNullException(nameof(battleWorldHost));
        }

        /// <summary>
        /// 启动主链：
        /// LoadingPage -> MainPage。
        /// </summary>
        public async Task EnterStartupAsync()
        {
            if (_startupCompleted)
            {
                return;
            }

            await RunExclusiveAsync(async () =>
            {
                string loadingScreenId = LoadingScreenRegistration.StartupPageScreenId;
                float loadingStartedAt = await OpenStartupLoadingAsync("正在进入侦探社...", 0.08f);

                try
                {
                    await UpdateLoadingProgressAsync(loadingScreenId, "正在加载界面资源...", 0.18f, 0.55f);
                    await PreloadStartupScreensAsync();
                    await UpdateLoadingProgressAsync(loadingScreenId, "正在整理侦探社状态...", 0.55f, 0.72f);
                    string startupStatus = PrepareStartupHomeStatus();
                    await UpdateLoadingProgressAsync(loadingScreenId, "正在准备进入侦探社...", 0.72f, 0.95f);
                    await WaitForMinimumLoadingVisibleSecondsAsync(loadingStartedAt);
                    await CompleteLoadingAsync(loadingScreenId, "侦探社已就绪。");
                    await _root.ScreenService.CloseAsync(loadingScreenId);
                    await _root.ScreenService.OpenPageAsync(MainScreenRegistration.ScreenId, startupStatus);
                    _startupCompleted = true;
                }
                catch (Exception ex)
                {
                    await ShowStartupFailureAsync(ex.Message);
                    throw;
                }
            });
        }

        private string PrepareStartupHomeStatus()
        {
            HolmasApplicationContext context = _root.Context;
            if (context == null || context.GameplayRuntime == null)
            {
                return "侦探社已就绪。";
            }

            context.GameplayRuntime.RefreshExpiredAdSlots();
            HolmasTaskSettlementResult settlementResult = context.SettleClaimableTasksAndRefill();
            string rewardTip = settlementResult != null && settlementResult.ClaimedTaskCount > 0
                ? context.GameplayRuntime.LastTaskRewardTip
                : string.Empty;

            if (context.GameplayRuntime.HasActiveUncompletedLevel)
            {
                int resumedTaskCount = context.GameplayRuntime.TaskBarState != null &&
                                       context.GameplayRuntime.TaskBarState.Tasks != null
                    ? context.GameplayRuntime.TaskBarState.Tasks.Count
                    : 0;
                string resumeStatus = resumedTaskCount > 0
                    ? $"侦探社已就绪。已恢复未完成棋盘，任务栏 {resumedTaskCount} 条已准备。"
                    : "侦探社已就绪。已恢复未完成棋盘。";
                return MergeRewardTip(rewardTip, resumeStatus);
            }

            int activeTaskCount = context.GameplayRuntime.TaskBarState != null &&
                                  context.GameplayRuntime.TaskBarState.Tasks != null
                ? context.GameplayRuntime.TaskBarState.Tasks.Count
                : 0;

            context.Logger?.LogInfo(
                "HolmasFlowCoordinator: 启动阶段完成任务栏整理，新增 {0} 条任务，当前活跃 {1} 条。",
                settlementResult != null ? settlementResult.RefilledTaskCount : 0,
                activeTaskCount);

            string readyStatus = activeTaskCount > 0
                ? $"侦探社已就绪。任务栏 {activeTaskCount} 条已准备。"
                : "侦探社已就绪。当前没有活跃任务。";
            return MergeRewardTip(rewardTip, readyStatus);
        }

        /// <summary>
        /// 从首页进入棋盘：
        /// MainPage -> LoadingOverlay -> BattlePage。
        /// </summary>
        public async Task StartBattleAsync()
        {
            await RunExclusiveAsync(async () =>
            {
                if (_root.LevelLaunchGateway == null)
                {
                    throw new InvalidOperationException("关卡启动网关不可用。");
                }

                bool sessionStarted = false;
                string loadingScreenId = LoadingScreenRegistration.TransitionOverlayScreenId;
                float loadingStartedAt = await OpenTransitionLoadingAsync("正在准备棋盘...", 0.08f);

                try
                {
                    await UpdateLoadingProgressAsync(loadingScreenId, "正在读取关卡状态...", 0.08f, 0.2f);
                    HolmasGameplayRuntime runtime = _root.Context != null ? _root.Context.GameplayRuntime : null;
                    bool continueExistingLevel = runtime != null && runtime.HasActiveUncompletedLevel;
                    string battleStatus;
                    if (continueExistingLevel)
                    {
                        battleStatus = $"已恢复未完成关卡，map={runtime.CurrentLevelSnapshot?.MapId ?? "unknown"}";
                        await UpdateLoadingProgressAsync(loadingScreenId, "正在恢复未完成棋盘...", 0.2f, 0.45f);
                    }
                    else
                    {
                        // 这里的 seed 是当前局的随机入口，后续关卡数据会围绕它展开。
                        int seed = Environment.TickCount;
                        await _root.LevelLaunchGateway.StartLevelForCurrentPlayerAsync(seed);
                        sessionStarted = true;
                        battleStatus = $"关卡已启动，seed={seed}";
                        await UpdateLoadingProgressAsync(loadingScreenId, "正在生成关卡数据...", 0.2f, 0.45f);
                    }

                    await _battleWorldHost.PrepareAsync(_root.Context != null && _root.Context.GameplayRuntime != null
                        ? _root.Context.GameplayRuntime.CurrentLevelSnapshot
                        : null);
                    _battleWorldHost.Show();
                    await UpdateLoadingProgressAsync(loadingScreenId, "正在摆放棋盘...", 0.45f, 0.82f);

                    await _root.ScreenService.OpenPageAsync(BattleScreenRegistration.ScreenId, battleStatus);
                    await UpdateLoadingProgressAsync(loadingScreenId, "正在打开棋盘界面...", 0.82f, 0.95f);
                    await WaitForMinimumLoadingVisibleSecondsAsync(loadingStartedAt);
                    await CompleteLoadingAsync(loadingScreenId, "棋盘已准备。");
                }
                catch
                {
                    // 进入棋盘失败时，既要回收 3D/战斗世界，也要把运行时 session 收掉。
                    _battleWorldHost.Release();
                    if (sessionStarted && _root.Context != null && _root.Context.GameplayRuntime != null)
                    {
                        _root.Context.GameplayRuntime.EndCurrentLevelSession();
                    }

                    throw;
                }
                finally
                {
                    await _root.ScreenService.CloseAsync(LoadingScreenRegistration.TransitionOverlayScreenId);
                }
            });
        }

        /// <summary>
        /// 从首页启动或恢复棋盘，但保持 MainPage 为当前页面，由 MainPanel 内嵌棋盘承载交互。
        /// </summary>
        public async Task<string> StartBattleInMainAsync()
        {
            string finalStatus = "棋盘已准备。";
            await RunExclusiveAsync(async () =>
            {
                if (_root.LevelLaunchGateway == null)
                {
                    throw new InvalidOperationException("关卡启动网关不可用。");
                }

                bool sessionStarted = false;
                string loadingScreenId = LoadingScreenRegistration.TransitionOverlayScreenId;
                float loadingStartedAt = await OpenTransitionLoadingAsync("正在准备棋盘...", 0.08f);

                try
                {
                    await UpdateLoadingProgressAsync(loadingScreenId, "正在读取关卡状态...", 0.08f, 0.2f);
                    HolmasGameplayRuntime runtime = _root.Context != null ? _root.Context.GameplayRuntime : null;
                    bool continueExistingLevel = runtime != null && runtime.HasActiveUncompletedLevel;
                    if (continueExistingLevel)
                    {
                        finalStatus = $"已恢复未完成关卡，map={runtime.CurrentLevelSnapshot?.MapId ?? "unknown"}";
                        await UpdateLoadingProgressAsync(loadingScreenId, "正在恢复未完成棋盘...", 0.2f, 0.45f);
                    }
                    else
                    {
                        int seed = Environment.TickCount;
                        await _root.LevelLaunchGateway.StartLevelForCurrentPlayerAsync(seed);
                        sessionStarted = true;
                        finalStatus = $"关卡已启动，seed={seed}";
                        await UpdateLoadingProgressAsync(loadingScreenId, "正在生成关卡数据...", 0.2f, 0.45f);
                    }

                    runtime = _root.Context != null ? _root.Context.GameplayRuntime : runtime;
                    runtime?.CurrentBoardRuntime?.ClearFlags();

                    await _battleWorldHost.PrepareAsync(_root.Context != null && _root.Context.GameplayRuntime != null
                        ? _root.Context.GameplayRuntime.CurrentLevelSnapshot
                        : null);
                    _battleWorldHost.Show();
                    await UpdateLoadingProgressAsync(loadingScreenId, "正在摆放棋盘...", 0.45f, 0.95f);
                    await WaitForMinimumLoadingVisibleSecondsAsync(loadingStartedAt);
                    await CompleteLoadingAsync(loadingScreenId, "棋盘已准备。");
                }
                catch
                {
                    _battleWorldHost.Release();
                    if (sessionStarted && _root.Context != null && _root.Context.GameplayRuntime != null)
                    {
                        _root.Context.GameplayRuntime.EndCurrentLevelSession();
                    }

                    throw;
                }
                finally
                {
                    await _root.ScreenService.CloseAsync(LoadingScreenRegistration.TransitionOverlayScreenId);
                }
            });

            return finalStatus;
        }

        /// <summary>
        /// 当前棋盘完成后立即进入下一局。
        /// 任务推进和结算已经由 HolmasGameplayRuntime 完成，这里只负责新关卡加载与页面刷新。
        /// </summary>
        public async Task AdvanceToNextBattleAsync(HolmasProgressionAdvanceResult progressionResult)
        {
            await RunExclusiveAsync(async () =>
            {
                if (_root.LevelLaunchGateway == null)
                {
                    throw new InvalidOperationException("关卡启动网关不可用。");
                }

                HolmasGameplayRuntime runtime = _root.Context != null ? _root.Context.GameplayRuntime : null;
                if (runtime == null || runtime.CurrentLevelSnapshot == null || !runtime.CurrentLevelSnapshot.Completed)
                {
                    throw new InvalidOperationException("当前棋盘尚未完成，不能进入下一关。");
                }

                string completedMapId = runtime.CurrentLevelSnapshot.MapId ?? "unknown";
                bool sessionStarted = false;
                string loadingScreenId = LoadingScreenRegistration.TransitionOverlayScreenId;
                float loadingStartedAt = await OpenTransitionLoadingAsync("正在进入下一关...", 0.08f);

                try
                {
                    await UpdateLoadingProgressAsync(loadingScreenId, "正在生成下一关...", 0.08f, 0.35f);
                    int seed = Environment.TickCount;
                    await _root.LevelLaunchGateway.StartLevelForCurrentPlayerAsync(seed);
                    sessionStarted = true;

                    LevelSnapshot nextSnapshot = runtime.CurrentLevelSnapshot;
                    await UpdateLoadingProgressAsync(loadingScreenId, "正在加载下一关棋盘...", 0.35f, 0.62f);
                    await _battleWorldHost.PrepareAsync(nextSnapshot);
                    _battleWorldHost.Show();
                    await UpdateLoadingProgressAsync(loadingScreenId, "正在刷新棋盘界面...", 0.62f, 0.82f);

                    await _root.ScreenService.OpenPageAsync(
                        BattleScreenRegistration.ScreenId,
                        BuildNextBattleStatus(completedMapId, nextSnapshot, seed, progressionResult));
                    await UpdateLoadingProgressAsync(loadingScreenId, "正在完成切换...", 0.82f, 0.95f);
                    await WaitForMinimumLoadingVisibleSecondsAsync(loadingStartedAt);
                    await CompleteLoadingAsync(loadingScreenId, "下一关已准备。");
                }
                catch
                {
                    if (sessionStarted && runtime != null)
                    {
                        runtime.EndCurrentLevelSession();
                    }

                    _battleWorldHost.Release();
                    throw;
                }
                finally
                {
                    await _root.ScreenService.CloseAsync(LoadingScreenRegistration.TransitionOverlayScreenId);
                }
            });
        }

        /// <summary>
        /// 当前内嵌棋盘完成后立即启动下一局，并保持 MainPage 不跳转。
        /// </summary>
        public async Task<string> AdvanceToNextBattleInMainAsync(HolmasProgressionAdvanceResult progressionResult)
        {
            string finalStatus = "已进入下一关。";
            await RunExclusiveAsync(async () =>
            {
                if (_root.LevelLaunchGateway == null)
                {
                    throw new InvalidOperationException("关卡启动网关不可用。");
                }

                HolmasGameplayRuntime runtime = _root.Context != null ? _root.Context.GameplayRuntime : null;
                if (runtime == null || runtime.CurrentLevelSnapshot == null || !runtime.CurrentLevelSnapshot.Completed)
                {
                    throw new InvalidOperationException("当前棋盘尚未完成，不能进入下一关。");
                }

                string completedMapId = runtime.CurrentLevelSnapshot.MapId ?? "unknown";
                bool sessionStarted = false;
                string loadingScreenId = LoadingScreenRegistration.TransitionOverlayScreenId;
                float loadingStartedAt = await OpenTransitionLoadingAsync("正在进入下一关...", 0.08f);

                try
                {
                    await UpdateLoadingProgressAsync(loadingScreenId, "正在生成下一关...", 0.08f, 0.35f);
                    int seed = Environment.TickCount;
                    await _root.LevelLaunchGateway.StartLevelForCurrentPlayerAsync(seed);
                    sessionStarted = true;

                    LevelSnapshot nextSnapshot = runtime.CurrentLevelSnapshot;
                    await UpdateLoadingProgressAsync(loadingScreenId, "正在加载下一关棋盘...", 0.35f, 0.68f);
                    await _battleWorldHost.PrepareAsync(nextSnapshot);
                    _battleWorldHost.Show();
                    await UpdateLoadingProgressAsync(loadingScreenId, "正在刷新棋盘界面...", 0.68f, 0.95f);

                    finalStatus = BuildNextBattleStatus(completedMapId, nextSnapshot, seed, progressionResult);
                    await WaitForMinimumLoadingVisibleSecondsAsync(loadingStartedAt);
                    await CompleteLoadingAsync(loadingScreenId, "下一关已准备。");
                }
                catch
                {
                    if (sessionStarted && runtime != null)
                    {
                        runtime.EndCurrentLevelSession();
                    }

                    _battleWorldHost.Release();
                    throw;
                }
                finally
                {
                    await _root.ScreenService.CloseAsync(LoadingScreenRegistration.TransitionOverlayScreenId);
                }
            });

            return finalStatus;
        }

        /// <summary>
        /// 从棋盘返回首页：
        /// BattlePage -> LoadingOverlay -> MainPage。
        /// </summary>
        public async Task ExitBattleToMainAsync()
        {
            await RunExclusiveAsync(async () =>
            {
                string loadingScreenId = LoadingScreenRegistration.TransitionOverlayScreenId;
                float loadingStartedAt = await OpenTransitionLoadingAsync("正在返回侦探社...", 0.08f);

                try
                {
                    await UpdateLoadingProgressAsync(loadingScreenId, "正在收起棋盘...", 0.08f, 0.28f);
                    HolmasGameplayRuntime runtime = _root.Context != null ? _root.Context.GameplayRuntime : null;
                    bool shouldClearSession = runtime != null &&
                                              runtime.CurrentLevelSnapshot != null &&
                                              runtime.CurrentLevelSnapshot.Completed;

                    if (_root.ScreenService.IsOpen(BattleScreenRegistration.ScreenId))
                    {
                        await _root.ScreenService.CloseAsync(BattleScreenRegistration.ScreenId);
                    }
                    await UpdateLoadingProgressAsync(loadingScreenId, "正在恢复侦探社界面...", 0.28f, 0.62f);

                    if (!_root.ScreenService.IsOpen(MainScreenRegistration.ScreenId) ||
                        _root.ScreenService.NavigationState.CurrentPage == null)
                    {
                        await _root.ScreenService.OpenPageAsync(
                            MainScreenRegistration.ScreenId,
                            shouldClearSession ? "已返回侦探社，当前棋盘已结算。" : "已返回侦探社，可继续当前棋盘。");
                    }

                    if (shouldClearSession && runtime != null)
                    {
                        runtime.EndCurrentLevelSession();
                    }

                    _battleWorldHost.Release();
                    await UpdateLoadingProgressAsync(loadingScreenId, "正在完成返回...", 0.62f, 0.95f);
                    await WaitForMinimumLoadingVisibleSecondsAsync(loadingStartedAt);
                    await CompleteLoadingAsync(loadingScreenId, "侦探社已准备。");
                }
                finally
                {
                    await _root.ScreenService.CloseAsync(LoadingScreenRegistration.TransitionOverlayScreenId);
                }
            });
        }

        private async Task RunExclusiveAsync(Func<Task> action)
        {
            // 所有跨屏切换串行执行，防止按钮连点把 page / overlay 状态打乱。
            if (_transitionInProgress)
            {
                throw new InvalidOperationException("当前有进行中的界面切换，请稍后再试。");
            }

            _transitionInProgress = true;
            try
            {
                await action();
            }
            finally
            {
                _transitionInProgress = false;
            }
        }

        public static float CalculateRemainingLoadingVisibleSeconds(
            float elapsedSeconds,
            float minimumVisibleSeconds = MinimumLoadingVisibleSeconds)
        {
            if (minimumVisibleSeconds <= 0f)
            {
                return 0f;
            }

            return Math.Max(0f, minimumVisibleSeconds - Math.Max(0f, elapsedSeconds));
        }

        private async Task<float> OpenStartupLoadingAsync(string status, float progress)
        {
            await _root.ScreenService.OpenPageAsync(
                LoadingScreenRegistration.StartupPageScreenId,
                CreateLoadingVm(status, 0f, progress, ProgressStepAnimationSeconds, true));
            await Task.Yield();
            return Time.realtimeSinceStartup;
        }

        private async Task<float> OpenTransitionLoadingAsync(string status, float progress)
        {
            await _root.ScreenService.ShowOverlayAsync(
                LoadingScreenRegistration.TransitionOverlayScreenId,
                CreateLoadingVm(status, 0f, progress, ProgressStepAnimationSeconds, true));
            await Task.Yield();
            return Time.realtimeSinceStartup;
        }

        private async Task PreloadStartupScreensAsync()
        {
            foreach (UiScreenDefinition definition in _root.ScreenService.Definitions)
            {
                if (definition != null &&
                    definition.PreloadOnBootstrap &&
                    definition.Id != LoadingScreenRegistration.StartupPageScreenId)
                {
                    await _root.ScreenService.PreloadAsync(definition.Id);
                }
            }
        }

        private async Task WaitForMinimumLoadingVisibleSecondsAsync(float loadingStartedAt)
        {
            float elapsedSeconds = Time.realtimeSinceStartup - loadingStartedAt;
            float remainingSeconds = CalculateRemainingLoadingVisibleSeconds(elapsedSeconds);
            if (remainingSeconds <= 0f)
            {
                return;
            }

            await Task.Delay(Mathf.CeilToInt(remainingSeconds * 1000f));
        }

        private async Task CompleteLoadingAsync(string screenId, string status)
        {
            await _root.ScreenService.RefreshAsync(screenId, CreateLoadingVm(status, 0.95f, 1f, CompletionHoldSeconds, true));
            await Task.Delay(Mathf.CeilToInt(CompletionHoldSeconds * 1000f));
            await Task.Yield();
        }

        private async Task UpdateLoadingProgressAsync(
            string screenId,
            string status,
            float currentProgress,
            float targetProgress)
        {
            await _root.ScreenService.RefreshAsync(
                screenId,
                CreateLoadingVm(
                    status,
                    currentProgress,
                    targetProgress,
                    ProgressStepAnimationSeconds,
                    true));
            await Task.Yield();
        }

        private async Task ShowStartupFailureAsync(string message)
        {
            LoadingVm failureVm = CreateLoadingVm("启动失败：" + message, 0f, 0f, MinimumLoadingVisibleSeconds, false);
            if (_root.ScreenService.IsOpen(LoadingScreenRegistration.StartupPageScreenId))
            {
                await _root.ScreenService.RefreshAsync(LoadingScreenRegistration.StartupPageScreenId, failureVm);
                return;
            }

            await _root.ScreenService.OpenPageAsync(LoadingScreenRegistration.StartupPageScreenId, failureVm);
        }

        private static LoadingVm CreateLoadingVm(
            string status,
            float progress,
            float targetProgress,
            float animationDurationSeconds,
            bool animate)
        {
            return new LoadingVm
            {
                Status = status,
                Progress = progress,
                TargetProgress = targetProgress,
                AnimationDurationSeconds = animationDurationSeconds,
                Animate = animate,
            };
        }

        private static string BuildNextBattleStatus(
            string completedMapId,
            LevelSnapshot nextSnapshot,
            int seed,
            HolmasProgressionAdvanceResult progressionResult)
        {
            int progressed = progressionResult != null ? progressionResult.ProgressedTaskIds.Count : 0;
            int completed = progressionResult != null ? progressionResult.CompletedTaskIds.Count : 0;
            string nextMapId = nextSnapshot != null && !string.IsNullOrWhiteSpace(nextSnapshot.MapId)
                ? nextSnapshot.MapId
                : "unknown";
            int nextCatCount = nextSnapshot != null && nextSnapshot.SpawnedCats != null
                ? nextSnapshot.SpawnedCats.Count
                : 0;
            return $"已完成 {completedMapId}，推进任务 {progressed} 条，新完成 {completed} 条。已进入下一关 {nextMapId}，猫 {nextCatCount} 只，seed={seed}";
        }

        private static string MergeRewardTip(string rewardTip, string status)
        {
            if (string.IsNullOrWhiteSpace(rewardTip))
            {
                return status;
            }

            return string.IsNullOrWhiteSpace(status)
                ? rewardTip
                : $"{rewardTip} {status}";
        }
    }
}
