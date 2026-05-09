using System;
using System.Threading.Tasks;
using App.HotUpdate.Holmas.Application;
using App.HotUpdate.Holmas.Tutorial;
using App.HotUpdate.Holmas.UI.Core;
using App.HotUpdate.Holmas.UI.Screens.Main;
using App.HotUpdate.Holmas.UI.Screens.Tutorial;
using App.Shared.Contracts;

namespace App.HotUpdate.Holmas.UI.Screens.GmTool
{
    public sealed class GmToolPopupController : UiPopupController
    {
        private GmToolView _view;
        private HolmasGameplayRuntime _runtime;
        private MainPresenter _mainPresenter;
        private CoreFindCatTutorialProgressService _progressService;
        private CoreFindCatTutorialCoordinator _tutorialCoordinator;
        private bool _isBusy;
        private string _status = "GM 工具已就绪。";

        protected override void OnCreate()
        {
            Root?.Context?.Logger?.LogInfo("GmToolPopupController: OnCreate.");
            _view = RootObject != null ? RootObject.GetComponent<GmToolView>() : null;
            if (_view == null && RootObject != null)
            {
                _view = RootObject.AddComponent<GmToolView>();
            }

            _view?.EnsureSurface();

            IPersistence persistence = Root?.Context?.ServiceContainer != null
                ? Root.Context.ServiceContainer.Get<IPersistence>()
                : null;
            var progressStore = new CoreFindCatTutorialProgressStore(persistence);
            _progressService = new CoreFindCatTutorialProgressService(progressStore);
            CoreFindCatTutorialSessionService sessionService = Root?.Context?.ServiceContainer != null
                ? Root.Context.ServiceContainer.Get<CoreFindCatTutorialSessionService>()
                : null;
            _tutorialCoordinator = new CoreFindCatTutorialCoordinator(
                _progressService,
                new CoreFindCatTutorialLevelService(),
                sessionService);
            _mainPresenter = new MainPresenter(Root != null ? Root.Context : null);
        }

        protected override void OnOpen(object payload)
        {
            Root?.Context?.Logger?.LogInfo(
                "GmToolPopupController: OnOpen. payloadType={0}, rootObject={1}",
                payload != null ? payload.GetType().Name : "null",
                RootObject != null ? RootObject.name : "null");
            _runtime = Root != null && Root.Context != null ? Root.Context.GameplayRuntime : null;
            if (_runtime != null)
            {
                _runtime.StateChanged += OnRuntimeStateChanged;
            }

            _view?.SetActions(
                OnCloseClicked,
                OnAddEnergyClicked,
                OnAddGoldClicked,
                OnReplayHelpClicked,
                OnStartTutorialAtStepClicked);
            _ = RefreshAsync();
        }

        protected override void OnClose()
        {
            if (_runtime != null)
            {
                _runtime.StateChanged -= OnRuntimeStateChanged;
            }

            _runtime = null;
        }

        protected override void OnDestroy()
        {
            if (_runtime != null)
            {
                _runtime.StateChanged -= OnRuntimeStateChanged;
            }

            _tutorialCoordinator?.Dispose();
        }

        private void OnRuntimeStateChanged(HolmasGameplayRuntimeStateChangeReason reason)
        {
            _ = RefreshAsync();
        }

        private void OnCloseClicked()
        {
            if (ScreenService != null)
            {
                _ = ScreenService.CloseAsync(GmToolScreenRegistration.ScreenId);
            }
        }

        private void OnAddEnergyClicked()
        {
            if (_isBusy)
            {
                return;
            }

            HolmasApplicationContext context = Root != null ? Root.Context : null;
            if (context == null)
            {
                _status = "应用上下文不可用，无法补充体力。";
                _ = RefreshAsync();
                return;
            }

            context.AddEnergy();
            _status = $"体力 +{HolmasGameplayRuntime.DebugEnergyGrantAmount}。";
            _ = RefreshAsync();
        }

        private void OnAddGoldClicked()
        {
            if (_isBusy)
            {
                return;
            }

            HolmasApplicationContext context = Root != null ? Root.Context : null;
            if (context == null)
            {
                _status = "应用上下文不可用，无法增加金币。";
                _ = RefreshAsync();
                return;
            }

            context.AddGold();
            _status = $"金币 +{HolmasGameplayRuntime.DebugGoldGrantAmount}。";
            _ = RefreshAsync();
        }

        private void OnReplayHelpClicked()
        {
            _ = ReplayHelpAsync();
        }

        private void OnStartTutorialAtStepClicked()
        {
            int stepIndex = 0;
            if (_view != null && !int.TryParse(_view.GetRequestedStepText(), out stepIndex))
            {
                stepIndex = 0;
            }

            stepIndex = Math.Max(0, stepIndex);
            _ = StartTutorialAsync(stepIndex, stepIndex > 0);
        }

        private async Task StartTutorialAsync(int stepIndex, bool debugForceStep)
        {
            if (_isBusy)
            {
                return;
            }

            MainView mainView = ResolveCurrentMainView();
            if (mainView == null)
            {
                _status = "请先回到主界面，再使用教程调试。";
                await RefreshAsync();
                return;
            }

            HolmasApplicationContext context = Root != null ? Root.Context : null;
            if (context == null || _tutorialCoordinator == null || ScreenService == null)
            {
                _status = "GM 工具上下文不可用，无法启动引导。";
                await RefreshAsync();
                return;
            }

            bool keepOpen = true;
            _isBusy = true;
            _status = "正在准备新手引导...";
            await RefreshAsync();
            try
            {
                CoreFindCatTutorialLaunchResult result = await _tutorialCoordinator.PrepareManualStartAsync(
                    context,
                    stepIndex,
                    debugForceStep);
                if (result != null && result.ShouldShowOverlay)
                {
                    keepOpen = false;
                    TutorialOverlayPayload payload = result.Payload ?? new TutorialOverlayPayload();
                    ConfigureMainTutorialPayload(payload, mainView);
                    await ScreenService.CloseAsync(GmToolScreenRegistration.ScreenId);
                    await ScreenService.ShowOverlayAsync(TutorialScreenRegistration.ScreenId, payload);
                }
            }
            catch (Exception ex)
            {
                _status = $"新手引导启动失败：{ex.Message}";
            }
            finally
            {
                _isBusy = false;
                if (keepOpen)
                {
                    await RefreshAsync();
                }
            }
        }

        private async Task ReplayHelpAsync()
        {
            if (_isBusy)
            {
                return;
            }

            MainView mainView = ResolveCurrentMainView();
            if (mainView == null)
            {
                _status = "请先回到主界面，再重看帮助。";
                await RefreshAsync();
                return;
            }

            HolmasApplicationContext context = Root != null ? Root.Context : null;
            if (context == null || _tutorialCoordinator == null || ScreenService == null)
            {
                _status = "GM 工具上下文不可用，无法打开帮助。";
                await RefreshAsync();
                return;
            }

            bool keepOpen = true;
            _isBusy = true;
            _status = "正在打开帮助说明...";
            await RefreshAsync();
            try
            {
                CoreFindCatTutorialSessionService sessionService = Root?.Context?.ServiceContainer != null
                    ? Root.Context.ServiceContainer.Get<CoreFindCatTutorialSessionService>()
                    : null;
                int stepIndex = sessionService?.ActiveSession != null
                    ? 0
                    : CoreFindCatTutorialSteps.IndexOf(CoreFindCatTutorialSteps.TaskBarStepId);
                TutorialOverlayPayload payload = await _tutorialCoordinator.CreateReplayPayloadAsync(context, stepIndex);
                ConfigureMainTutorialPayload(payload, mainView);
                keepOpen = false;
                await ScreenService.CloseAsync(GmToolScreenRegistration.ScreenId);
                await ScreenService.ShowOverlayAsync(TutorialScreenRegistration.ScreenId, payload);
            }
            catch (Exception ex)
            {
                _status = $"帮助打开失败：{ex.Message}";
            }
            finally
            {
                _isBusy = false;
                if (keepOpen)
                {
                    await RefreshAsync();
                }
            }
        }

        private async Task RefreshAsync()
        {
            if (_view == null)
            {
                return;
            }

            CoreFindCatTutorialProgress progress = _progressService != null
                ? await _progressService.LoadAsync()
                : new CoreFindCatTutorialProgress();

            bool tutorialAvailable = ResolveCurrentMainView() != null;
            MainPageController mainController = ResolveCurrentMainPageController();
            MainVm mainVm = _mainPresenter != null
                ? _mainPresenter.Build(mainController?.CurrentStatusText)
                : null;
            string runtimeSummary = mainVm != null
                ? mainVm.Summary
                : "玩法运行时不可用。";
            string mainStatus = mainVm != null && !string.IsNullOrWhiteSpace(mainVm.Status)
                ? mainVm.Status
                : "主界面状态不可用。";

            _view.Render(new GmToolVm
            {
                Status = _status,
                TutorialProgressSummary = BuildTutorialProgressSummary(progress),
                TutorialActionHint = tutorialAvailable
                    ? "当前页面：主界面。教程相关按钮可直接使用。"
                    : "当前不在主界面。教程相关按钮已禁用。",
                RuntimeSummary = string.IsNullOrWhiteSpace(runtimeSummary) ? "暂无运行时信息。" : runtimeSummary,
                MainStatus = $"主界面状态：{mainStatus}",
                StepInputText = _view.GetRequestedStepTextOrDefault("0"),
                AddEnergyEnabled = !_isBusy && Root?.Context?.GameplayRuntime != null,
                AddGoldEnabled = !_isBusy && Root?.Context?.GameplayRuntime != null,
                ReplayHelpEnabled = !_isBusy && tutorialAvailable,
                StartAtStepEnabled = !_isBusy && tutorialAvailable,
            });
        }

        private MainView ResolveCurrentMainView()
        {
            UiPageController currentPage = ScreenService != null ? ScreenService.NavigationState.CurrentPage : null;
            if (currentPage == null || currentPage.RootObject == null)
            {
                return null;
            }

            return currentPage.RootObject.GetComponent<MainView>();
        }

        private void ConfigureMainTutorialPayload(TutorialOverlayPayload payload, MainView mainView)
        {
            if (payload == null)
            {
                return;
            }

            payload.MainView = mainView;
            MainPageController mainController = ResolveCurrentMainPageController();
            Root?.Context?.Logger?.LogInfo(
                "GmToolPopupController: ConfigureMainTutorialPayload. hasMainView={0}, hasMainController={1}, runMode={2}, canWriteCompletion={3}",
                mainView != null,
                mainController != null,
                payload.RunMode,
                payload.CanWriteCompletion);
            if (mainController != null)
            {
                payload.OnTutorialExitedAsync = mainController.HandleTutorialExitedAsync;
            }

            payload.TutorialSessionService ??= Root?.Context?.ServiceContainer != null
                ? Root.Context.ServiceContainer.Get<CoreFindCatTutorialSessionService>()
                : null;
        }

        private MainPageController ResolveCurrentMainPageController()
        {
            return ScreenService != null ? ScreenService.NavigationState.CurrentPage as MainPageController : null;
        }

        private static string BuildTutorialProgressSummary(CoreFindCatTutorialProgress progress)
        {
            progress = CoreFindCatTutorialProgressService.Normalize(progress);
            string currentStep = progress.currentStepIndex >= 0
                ? $"{progress.currentStepIndex} / {progress.currentStepId}"
                : "未开始";
            string completedStep = progress.completedStepIndex >= 0
                ? $"{progress.completedStepIndex} / {progress.completedStepId}"
                : "无";
            return $"started: {progress.started}\ncompleted: {progress.completed}\nskipped: {progress.skipped}\ncurrent: {currentStep}\ncompleted: {completedStep}";
        }
    }
}
