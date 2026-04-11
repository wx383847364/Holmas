using System;
using System.Threading.Tasks;
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

        public async Task EnterStartupAsync()
        {
            if (_startupCompleted)
            {
                return;
            }

            await RunExclusiveAsync(async () =>
            {
                await _root.ScreenService.OpenPageAsync(
                    LoadingScreenRegistration.StartupPageScreenId,
                    CreateLoadingVm("正在进入侦探社...", 0.12f, true));

                try
                {
                    await _root.ScreenService.OpenPageAsync(MainScreenRegistration.ScreenId, "主界面已就绪。");
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
                    int seed = Environment.TickCount;
                    await _root.LevelLaunchGateway.StartLevelForCurrentPlayerAsync(seed);
                    sessionStarted = true;

                    await _battleWorldHost.PrepareAsync(_root.Context != null && _root.Context.GameplayRuntime != null
                        ? _root.Context.GameplayRuntime.CurrentLevelSnapshot
                        : null);
                    _battleWorldHost.Show();

                    await _root.ScreenService.OpenPageAsync(BattleScreenRegistration.ScreenId, $"关卡已启动，seed={seed}");
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
        }

        public async Task ExitBattleToMainAsync()
        {
            await RunExclusiveAsync(async () =>
            {
                await _root.ScreenService.ShowOverlayAsync(
                    LoadingScreenRegistration.TransitionOverlayScreenId,
                    CreateLoadingVm("正在返回主界面...", 0.1f, true));

                try
                {
                    if (_root.ScreenService.IsOpen(BattleScreenRegistration.ScreenId))
                    {
                        await _root.ScreenService.CloseAsync(BattleScreenRegistration.ScreenId);
                    }

                    if (!_root.ScreenService.IsOpen(MainScreenRegistration.ScreenId) ||
                        _root.ScreenService.NavigationState.CurrentPage == null)
                    {
                        await _root.ScreenService.OpenPageAsync(MainScreenRegistration.ScreenId, "已返回主界面。");
                    }

                    if (_root.Context != null && _root.Context.GameplayRuntime != null)
                    {
                        _root.Context.GameplayRuntime.EndCurrentLevelSession();
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
            return new LoadingVm
            {
                Status = status,
                Progress = progress,
                Animate = animate,
            };
        }
    }
}
