using System;
using System.Linq;
using System.Threading.Tasks;
using App.HotUpdate.Holmas.Application;
using App.HotUpdate.Holmas.UI.Screens.Battle;
using App.HotUpdate.Holmas.UI.Screens.Loading;
using App.HotUpdate.Holmas.UI.Screens.Main;

namespace App.HotUpdate.Holmas.UI.Core
{
    /// <summary>
    /// 收口启动页和 Main / Battle 主链切换，避免页面控制器直接编排跨屏状态。
    /// </summary>
    public sealed class HolmasFlowCoordinator
    {
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
                // 先让加载页真正显示一帧，再去预加载主页，避免肉眼上像“没有经过 loading”。
                await _root.ScreenService.OpenPageAsync(
                    LoadingScreenRegistration.StartupPageScreenId,
                    CreateLoadingVm("正在进入侦探社...", 0.12f, true));
                await Task.Yield();

                try
                {
                    string startupStatus = PrepareStartupHomeStatus();

                    // 首页显式预加载，避免启动阶段把 loading 直接挤没。
                    await _root.ScreenService.PreloadAsync(MainScreenRegistration.ScreenId);
                    await _root.ScreenService.OpenPageAsync(MainScreenRegistration.ScreenId, startupStatus);
                    await _root.ScreenService.CloseAsync(LoadingScreenRegistration.StartupPageScreenId);
                    _startupCompleted = true;
                }
                catch (Exception ex)
                {
                    await _root.ScreenService.OpenPageAsync(
                        LoadingScreenRegistration.StartupPageScreenId,
                        CreateLoadingVm("启动失败：" + ex.Message, 0f, false));
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

            if (context.GameplayRuntime.HasActiveUncompletedLevel)
            {
                int resumedTaskCount = context.GameplayRuntime.TaskBarState != null &&
                                       context.GameplayRuntime.TaskBarState.Tasks != null
                    ? context.GameplayRuntime.TaskBarState.Tasks.Count
                    : 0;
                return resumedTaskCount > 0
                    ? $"侦探社已就绪。已恢复未完成棋盘，任务栏 {resumedTaskCount} 条已准备。"
                    : "侦探社已就绪。已恢复未完成棋盘。";
            }

            int activeTaskCount = context.GameplayRuntime.TaskBarState != null &&
                                  context.GameplayRuntime.TaskBarState.Tasks != null
                ? context.GameplayRuntime.TaskBarState.Tasks.Count
                : 0;
            int unlockedEmptySlotCount = context.GameplayRuntime.TaskBarState != null
                ? context.GameplayRuntime.TaskBarState.GetUnlockedEmptySlotCount()
                : 0;
            int generatedTaskCount = 0;
            if (activeTaskCount <= 0 && unlockedEmptySlotCount > 0)
            {
                var refillResult = context.RefillAvailableTasks();
                generatedTaskCount = refillResult != null
                    ? refillResult.GeneratedTasks.Count(item => item != null && item.Success)
                    : 0;
                activeTaskCount = context.GameplayRuntime.TaskBarState != null &&
                                  context.GameplayRuntime.TaskBarState.Tasks != null
                    ? context.GameplayRuntime.TaskBarState.Tasks.Count
                    : 0;
            }

            context.Logger?.LogInfo(
                "HolmasFlowCoordinator: 启动阶段完成任务栏整理，新增 {0} 条任务，当前活跃 {1} 条。",
                generatedTaskCount,
                activeTaskCount);

            return activeTaskCount > 0
                ? $"侦探社已就绪。任务栏 {activeTaskCount} 条已准备。"
                : "侦探社已就绪。当前没有活跃任务。";
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
                await _root.ScreenService.ShowOverlayAsync(
                    LoadingScreenRegistration.TransitionOverlayScreenId,
                    CreateLoadingVm("正在准备棋盘...", 0.15f, true));

                try
                {
                    HolmasGameplayRuntime runtime = _root.Context != null ? _root.Context.GameplayRuntime : null;
                    bool continueExistingLevel = runtime != null && runtime.HasActiveUncompletedLevel;
                    string battleStatus;
                    if (continueExistingLevel)
                    {
                        battleStatus = $"已恢复未完成关卡，map={runtime.CurrentLevelSnapshot?.MapId ?? "unknown"}";
                    }
                    else
                    {
                        // 这里的 seed 是当前局的随机入口，后续关卡数据会围绕它展开。
                        int seed = Environment.TickCount;
                        await _root.LevelLaunchGateway.StartLevelForCurrentPlayerAsync(seed);
                        sessionStarted = true;
                        battleStatus = $"关卡已启动，seed={seed}";
                    }

                    await _battleWorldHost.PrepareAsync(_root.Context != null && _root.Context.GameplayRuntime != null
                        ? _root.Context.GameplayRuntime.CurrentLevelSnapshot
                        : null);
                    _battleWorldHost.Show();

                    await _root.ScreenService.OpenPageAsync(BattleScreenRegistration.ScreenId, battleStatus);
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
        /// 从棋盘返回首页：
        /// BattlePage -> LoadingOverlay -> MainPage。
        /// </summary>
        public async Task ExitBattleToMainAsync()
        {
            await RunExclusiveAsync(async () =>
            {
                await _root.ScreenService.ShowOverlayAsync(
                    LoadingScreenRegistration.TransitionOverlayScreenId,
                        CreateLoadingVm("正在返回侦探社...", 0.1f, true));

                try
                {
                    HolmasGameplayRuntime runtime = _root.Context != null ? _root.Context.GameplayRuntime : null;
                    bool shouldClearSession = runtime != null &&
                                              runtime.CurrentLevelSnapshot != null &&
                                              runtime.CurrentLevelSnapshot.Completed;

                    if (_root.ScreenService.IsOpen(BattleScreenRegistration.ScreenId))
                    {
                        await _root.ScreenService.CloseAsync(BattleScreenRegistration.ScreenId);
                    }

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

        private static LoadingVm CreateLoadingVm(string status, float progress, bool animate)
        {
            // LoadingVm 只是纯展示数据，不承载真正的异步任务逻辑。
            return new LoadingVm
            {
                Status = status,
                Progress = progress,
                Animate = animate,
            };
        }
    }
}
