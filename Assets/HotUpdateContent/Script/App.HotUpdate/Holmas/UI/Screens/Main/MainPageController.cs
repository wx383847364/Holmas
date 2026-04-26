using System;
using System.Linq;
using System.Threading.Tasks;
using App.HotUpdate.Holmas.Application;
using App.HotUpdate.Holmas.Board;
using App.HotUpdate.Holmas.Progression;
using App.HotUpdate.Holmas.Tasks.Runtime;
using App.HotUpdate.Holmas.Tasks.Services;
using App.HotUpdate.Holmas.Tutorial;
using App.HotUpdate.Holmas.UI.Core;
using App.HotUpdate.Holmas.UI.Screens.GmTool;
using App.HotUpdate.Holmas.UI.Screens.Tutorial;
using App.Shared.Contracts;
using App.Shared.Holmas.RuntimeData;

namespace App.HotUpdate.Holmas.UI.Screens.Main
{
    public sealed class MainPageController : UiPageController
    {
        private MainPresenter _presenter;
        private MainView _view;
        private MainBindings _bindings;
        private bool _isBusy;
        private bool _autoStartInProgress;
        private bool _tutorialCheckInProgress;
        private HolmasGameplayRuntime _runtime;
        private HolmasBoardInteractionMode _interactionMode = HolmasBoardInteractionMode.Walk;
        private CoreFindCatTutorialProgressStore _tutorialProgressStore;
        private CoreFindCatTutorialProgressService _tutorialProgressService;
        private CoreFindCatTutorialCoordinator _tutorialCoordinator;

        private static bool IsTutorialDebugEnabled =>
            UnityEngine.Application.isEditor || UnityEngine.Debug.isDebugBuild;

        protected override void OnCreate()
        {
            _runtime = Root != null && Root.Context != null ? Root.Context.GameplayRuntime : null;
            _presenter = new MainPresenter(Root != null ? Root.Context : null);
            IPersistence persistence = Root?.Context?.ServiceContainer != null
                ? Root.Context.ServiceContainer.Get<IPersistence>()
                : null;
            _tutorialProgressStore = new CoreFindCatTutorialProgressStore(persistence);
            _tutorialProgressService = new CoreFindCatTutorialProgressService(_tutorialProgressStore);
            _tutorialCoordinator = new CoreFindCatTutorialCoordinator(
                _tutorialProgressService,
                new CoreFindCatTutorialLevelService());
            _view = RootObject != null ? RootObject.GetComponent<MainView>() : null;
            if (_view == null && RootObject != null)
            {
                _view = RootObject.AddComponent<MainView>();
            }

            _view?.EnsureBindingSurface();
            if (_runtime != null)
            {
                _runtime.StateChanged += OnRuntimeStateChanged;
            }
        }

        protected override void OnBind()
        {
            _bindings = MainBindings.Resolve(BindingResolver);
            _view?.Bind(_bindings);
            _view?.SetAssetsRuntime(Root != null && Root.Context != null ? Root.Context.AssetsRuntime : null);
            _view?.SetPromotionAction(OnPromotionClicked);
            _view?.SetHelpAction(OnHelpClicked);
            _view?.SetGmAction(IsTutorialDebugEnabled ? OnGmClicked : null);
            _view?.SetTutorialDebugControlsVisible(IsTutorialDebugEnabled);
            _view?.SetModeToggleActions(OnWalkToggleChanged, OnFindToggleChanged);
            _view?.SetTaskSlotAction(OnTaskSlotClicked);
            _view?.SetCellAction(OnCellClicked);
        }

        protected override void OnOpen(object payload)
        {
            string repairedStatus = RepairIncompatibleLevelSession();
            string status = SettleClaimableTasksAndRefill(repairedStatus ?? payload as string);
            Refresh(status);
            RequestTutorialOrAutoStartInMain();
        }

        protected override void OnResume()
        {
            string repairedStatus = RepairIncompatibleLevelSession();
            string status = SettleClaimableTasksAndRefill(repairedStatus ?? "已返回主界面。");
            Refresh(status);
            RequestTutorialOrAutoStartInMain();
        }

        protected override void OnDestroy()
        {
            _view?.SetPromotionAction(null);
            _view?.SetHelpAction(null);
            _view?.SetGmAction(null);
            _view?.SetModeToggleActions(null, null);
            _view?.SetTaskSlotAction(null);
            _view?.SetCellAction(null);
            if (_runtime != null)
            {
                _runtime.StateChanged -= OnRuntimeStateChanged;
            }

            _tutorialCoordinator?.Dispose();
        }

        private string RepairIncompatibleLevelSession()
        {
            return null;
        }

        private string SettleClaimableTasksAndRefill(string fallbackStatus)
        {
            HolmasGameplayRuntime runtime = Root != null && Root.Context != null ? Root.Context.GameplayRuntime : _runtime;
            if (runtime == null)
            {
                return fallbackStatus;
            }

            int rewardTipVersionBeforeSettlement = runtime.LastTaskRewardTipVersion;
            HolmasTaskSettlementResult settlement = runtime.SettleClaimableTasksAndRefill();
            return settlement != null && settlement.ClaimedTaskCount > 0
                ? BuildStatusWithNewRewardTip(runtime, rewardTipVersionBeforeSettlement, fallbackStatus)
                : fallbackStatus;
        }

        private void RequestAutoStartInMain()
        {
            if (_autoStartInProgress || _isBusy)
            {
                return;
            }

            HolmasGameplayRuntime runtime = Root != null && Root.Context != null ? Root.Context.GameplayRuntime : _runtime;
            if (runtime != null && runtime.HasActiveUncompletedLevel && runtime.CurrentBoardRuntime != null)
            {
                return;
            }

            _autoStartInProgress = true;
            _ = HandleStartAsync();
        }

        private void RequestTutorialOrAutoStartInMain()
        {
            if (_tutorialCheckInProgress)
            {
                return;
            }

            _tutorialCheckInProgress = true;
            _ = HandleTutorialOrAutoStartAsync();
        }

        private async Task HandleTutorialOrAutoStartAsync()
        {
            try
            {
                CoreFindCatTutorialLaunchResult result = _tutorialCoordinator != null
                    ? await _tutorialCoordinator.PrepareAutoStartAsync(Root != null ? Root.Context : null)
                    : CoreFindCatTutorialLaunchResult.AutoStartNormal();
                if (result != null && result.ShouldAutoStartNormal)
                {
                    RequestAutoStartInMain();
                    return;
                }

                if (result != null && result.ShouldShowOverlay)
                {
                    await ShowTutorialOverlayAsync(result.Payload);
                }
            }
            catch (Exception ex)
            {
                Refresh($"新手引导检查失败：{ex.Message}");
                RequestAutoStartInMain();
            }
            finally
            {
                _tutorialCheckInProgress = false;
            }
        }

        private void OnPromotionClicked()
        {
            string promotionId = _presenter != null ? _presenter.GetPrimaryPromotionId() : string.Empty;
            if (string.IsNullOrWhiteSpace(promotionId))
            {
                Refresh("当前阶段暂无可升级宣传项。");
                return;
            }

            HolmasApplicationContext context = Root != null ? Root.Context : null;
            if (context == null)
            {
                Refresh("应用上下文不可用，无法升级宣传。");
                return;
            }

            var result = context.TryUpgradePromotion(promotionId);
            Refresh(result != null && result.Success
                ? $"宣传 {promotionId} 升到 Lv {result.NewLevel}，金币 -{result.GoldSpent}。"
                : $"宣传升级失败：{result?.FailureReason ?? "未知错误"}");
        }

        private void OnAddEnergyClicked()
        {
            HolmasApplicationContext context = Root != null ? Root.Context : null;
            if (context == null)
            {
                Refresh("应用上下文不可用，无法补充体力。");
                return;
            }

            context.AddEnergy();
            Refresh($"体力 +{HolmasGameplayRuntime.DebugEnergyGrantAmount}。");
        }

        private void OnHelpClicked()
        {
            if (ScreenService?.NavigationState?.CurrentOverlay is TutorialOverlayController tutorialOverlay &&
                tutorialOverlay.HandleHelpButtonClicked())
            {
                return;
            }

            int stepIndex = CoreFindCatTutorialLevelService.IsTutorialLevel(_runtime?.CurrentLevelSnapshot)
                ? 0
                : CoreFindCatTutorialSteps.IndexOf(CoreFindCatTutorialSteps.TaskBarStepId);
            _ = HandleReplayTutorialAsync(stepIndex);
        }

        private void OnGmClicked()
        {
            if (Root == null)
            {
                return;
            }

            Root.Context?.Logger?.LogInfo(
                "MainPageController: 收到 GM 按钮点击。debugEnabled={0}, screenServiceReady={1}",
                IsTutorialDebugEnabled,
                Root.ScreenService != null);
            _ = Root.ToggleGmToolAsync(GmToggleRequestSource.Button);
        }

        private async Task HandleReplayTutorialAsync(int stepIndex)
        {
            if (ScreenService == null || _tutorialCoordinator == null)
            {
                return;
            }

            TutorialOverlayPayload payload = await _tutorialCoordinator.CreateReplayPayloadAsync(
                Root != null ? Root.Context : null,
                stepIndex);
            await ShowTutorialOverlayAsync(payload);
        }

        private Task ShowTutorialOverlayAsync(TutorialOverlayPayload payload)
        {
            if (ScreenService == null)
            {
                return Task.CompletedTask;
            }

            if (payload == null)
            {
                payload = new TutorialOverlayPayload();
            }

            payload.MainView = _view;
            payload.OnTutorialExitedAsync = HandleTutorialExitedAsync;

            return ScreenService.ShowOverlayAsync(
                TutorialScreenRegistration.ScreenId,
                payload);
        }

        private async Task HandleTutorialExitedAsync()
        {
            HolmasGameplayRuntime runtime = Root != null && Root.Context != null ? Root.Context.GameplayRuntime : _runtime;
            if (!CoreFindCatTutorialCoordinator.ShouldEndTutorialLevelAfterExit(runtime))
            {
                Refresh(null);
                return;
            }

            runtime.EndCurrentLevelSession();
            Refresh("新手引导已结束，正在准备正式棋盘...");
            await StartFormalBoardAfterTutorialAsync("新手引导已结束，正式棋盘已准备。");
        }

        private void OnWalkToggleChanged(bool isOn)
        {
            if (_view != null && _view.IsSyncingToggles)
            {
                return;
            }

            if (isOn)
            {
                SetInteractionMode(HolmasBoardInteractionMode.Walk, "已切换为行走模式。");
            }
        }

        private void OnFindToggleChanged(bool isOn)
        {
            if (_view != null && _view.IsSyncingToggles)
            {
                return;
            }

            if (isOn)
            {
                SetInteractionMode(HolmasBoardInteractionMode.Find, "已切换为寻找模式。");
            }
        }

        private void SetInteractionMode(HolmasBoardInteractionMode mode, string status)
        {
            _interactionMode = mode;
            Refresh(status);
        }

        private void OnTaskSlotClicked(int slotIndex)
        {
            _ = HandleTaskSlotAsync(slotIndex);
        }

        private void OnCellClicked(int cellIndex, bool isRightButton)
        {
            _ = HandleCellInteractionAsync(cellIndex, isRightButton);
        }

        private async Task HandleStartAsync()
        {
            if (_isBusy)
            {
                _autoStartInProgress = false;
                return;
            }

            HolmasFlowCoordinator flowCoordinator = Root != null ? Root.FlowCoordinator : null;
            if (flowCoordinator == null)
            {
                _autoStartInProgress = false;
                Refresh("界面流转协调器不可用。");
                return;
            }

            _isBusy = true;
            Refresh("正在准备本局棋盘...");
            string finalStatus = null;

            try
            {
                finalStatus = await flowCoordinator.StartBattleInMainAsync();
            }
            catch (Exception ex)
            {
                finalStatus = $"进入棋盘失败：{ex.Message}";
                Refresh(finalStatus);
            }
            finally
            {
                _isBusy = false;
                _autoStartInProgress = false;
                if (ScreenService != null &&
                    ReferenceEquals(ScreenService.NavigationState.CurrentPage, this))
                {
                    Refresh(finalStatus ?? "主界面已就绪。");
                }
            }
        }

        private async Task HandleCellInteractionAsync(int cellIndex, bool isRightButton)
        {
            if (_isBusy)
            {
                return;
            }

            HolmasGameplayRuntime runtime = Root != null && Root.Context != null ? Root.Context.GameplayRuntime : null;
            if (runtime == null)
            {
                Refresh("玩法运行时不可用。");
                return;
            }

            HolmasBoardInteractionMode mode = isRightButton
                ? HolmasBoardInteractionMode.Find
                : _interactionMode;

            _isBusy = true;
            string finalStatus = null;
            try
            {
                int rewardTipVersionBeforeReveal = runtime.LastTaskRewardTipVersion;
                HolmasProgressionAdvanceResult progressionResult;
                BoardRevealResult revealResult = runtime.RevealCell(cellIndex, mode, out progressionResult);
                string revealStatus = BuildRevealStatus(revealResult, progressionResult, mode);
                finalStatus = BuildStatusWithNewRewardTip(runtime, rewardTipVersionBeforeReveal, revealStatus);
                Refresh(finalStatus);
                bool completedTutorialLevel = CoreFindCatTutorialLevelService.IsTutorialLevel(runtime.CurrentLevelSnapshot);
                bool shouldRestoreTutorialOverlay = completedTutorialLevel &&
                                                    ScreenService != null &&
                                                    ScreenService.IsOpen(TutorialScreenRegistration.ScreenId);
                if (revealResult != null && revealResult.IsValidAction && revealResult.Completed)
                {
                    finalStatus = await AdvanceToNextLevelAsync(progressionResult, finalStatus, rewardTipVersionBeforeReveal);
                    if (shouldRestoreTutorialOverlay)
                    {
                        await RestoreTutorialAfterTutorialBoardAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                finalStatus = $"棋盘操作失败：{ex.Message}";
                Refresh(finalStatus);
            }
            finally
            {
                _isBusy = false;
                Refresh(finalStatus);
                await Task.CompletedTask;
            }
        }

        private async Task<string> AdvanceToNextLevelAsync(
            HolmasProgressionAdvanceResult progressionResult,
            string fallbackStatus,
            int rewardTipVersionBeforeReveal)
        {
            HolmasFlowCoordinator flowCoordinator = Root != null ? Root.FlowCoordinator : null;
            if (flowCoordinator == null)
            {
                Refresh(fallbackStatus);
                return fallbackStatus;
            }

            try
            {
                string status = await flowCoordinator.AdvanceToNextBattleInMainAsync(progressionResult);
                status = BuildStatusWithNewRewardTip(_runtime, rewardTipVersionBeforeReveal, status);
                Refresh(status);
                return status;
            }
            catch (Exception ex)
            {
                string status = $"本局完成，但进入下一关失败：{ex.Message}";
                Refresh(status);
                return status;
            }
        }

        private async Task RestoreTutorialAfterTutorialBoardAsync()
        {
            if (_tutorialCoordinator == null || ScreenService == null)
            {
                return;
            }

            TutorialOverlayPayload payload = await _tutorialCoordinator.CreateResumePayloadAsync(
                Root != null ? Root.Context : null,
                CoreFindCatTutorialSteps.IndexOf(CoreFindCatTutorialSteps.EnergyStepId));
            await ShowTutorialOverlayAsync(payload);
        }

        private async Task StartFormalBoardAfterTutorialAsync(string fallbackStatus)
        {
            if (_isBusy)
            {
                Refresh(fallbackStatus);
                return;
            }

            HolmasFlowCoordinator flowCoordinator = Root != null ? Root.FlowCoordinator : null;
            if (flowCoordinator == null)
            {
                Refresh(fallbackStatus);
                return;
            }

            _isBusy = true;
            string finalStatus = fallbackStatus;
            try
            {
                finalStatus = await flowCoordinator.StartBattleInMainAsync();
                Refresh(string.IsNullOrWhiteSpace(finalStatus) ? fallbackStatus : finalStatus);
            }
            catch (Exception ex)
            {
                finalStatus = $"新手引导已结束，但正式棋盘启动失败：{ex.Message}";
                Refresh(finalStatus);
            }
            finally
            {
                _isBusy = false;
                Refresh(finalStatus);
            }
        }

        private async Task HandleTaskSlotAsync(int slotIndex)
        {
            if (_isBusy)
            {
                return;
            }

            HolmasApplicationContext context = Root != null ? Root.Context : null;
            if (context == null || context.GameplayRuntime == null || context.GameplayRuntime.TaskBarState == null)
            {
                Refresh("任务栏不可用。");
                return;
            }

            TaskSlotState slot = context.GameplayRuntime.TaskBarState.GetSlot(slotIndex);
            if (slot == null)
            {
                Refresh($"任务槽 {slotIndex + 1} 不存在。");
                return;
            }

            _isBusy = true;
            string finalStatus = null;

            try
            {
                var runtimeTask = context.GameplayRuntime.TaskBarState.GetTaskBySlot(slotIndex);
                bool hasActiveLevel = context.GameplayRuntime.HasActiveUncompletedLevel;
                if (!slot.IsUnlocked)
                {
                    HolmasTaskSlotUnlockResult unlock = context.UnlockAdSlot(slotIndex);
                    if (!unlock.Success)
                    {
                        finalStatus = $"任务槽 {slotIndex + 1} 解锁失败：{unlock.FailureReason}";
                    }
                    else
                    {
                        runtimeTask = context.GameplayRuntime.TaskBarState.GetTaskBySlot(slotIndex);
                        finalStatus = runtimeTask != null && runtimeTask.Task != null
                            ? $"任务槽 {slotIndex + 1} 已解锁并补入任务。{BuildTaskSlotStatus(slotIndex, slot, runtimeTask, hasActiveLevel)}"
                            : $"任务槽 {slotIndex + 1} 已解锁；当前等级暂无可补任务。";
                    }
                }
                else if (runtimeTask != null && runtimeTask.Task != null)
                {
                    finalStatus = BuildTaskSlotStatus(slotIndex, slot, runtimeTask, hasActiveLevel);
                }
                else
                {
                    finalStatus = hasActiveLevel
                        ? $"任务槽 {slotIndex + 1} 当前为空；后续新任务会在满足抽取条件时直接补入。"
                        : $"任务槽 {slotIndex + 1} 当前为空；当前等级暂无可补任务。";
                }

                Refresh(finalStatus);
            }
            catch (Exception ex)
            {
                finalStatus = $"任务槽操作失败：{ex.Message}";
                Refresh(finalStatus);
            }
            finally
            {
                _isBusy = false;
                if (ScreenService != null &&
                    ReferenceEquals(ScreenService.NavigationState.CurrentPage, this))
                {
                    Refresh(finalStatus ?? "主界面已就绪。");
                }

                await Task.CompletedTask;
            }
        }

        private static string BuildTaskSlotStatus(
            int slotIndex,
            TaskSlotState slot,
            HolmasTaskRuntimeInstance runtimeTask,
            bool hasActiveLevel)
        {
            if (runtimeTask == null || runtimeTask.Task == null)
            {
                return hasActiveLevel
                    ? $"任务槽 {slotIndex + 1} 当前为空；后续新任务会在满足抽取条件时直接补入。"
                    : $"任务槽 {slotIndex + 1} 当前为空；当前等级暂无可补任务。";
            }

            string suffix = slot != null && slot.PendingRelockAfterTaskCompletion
                ? "广告槽已到期，完成当前任务后会重新锁定。"
                : hasActiveLevel
                    ? "继续当前棋盘即可推进。"
                    : "完成后会自动领奖并补新任务。";
            return $"任务槽 {slotIndex + 1} 进度 {runtimeTask.Task.CurrentCount}/{runtimeTask.Task.TargetCount}，{suffix}";
        }

        private void Refresh(string status = null)
        {
            MainVm viewModel = _presenter != null ? _presenter.Build(status) : new MainVm();
            viewModel.WalkToggleIsOn = _interactionMode == HolmasBoardInteractionMode.Walk;
            viewModel.FindToggleIsOn = _interactionMode == HolmasBoardInteractionMode.Find;
            if (viewModel.TaskItems != null)
            {
                for (int i = 0; i < viewModel.TaskItems.Length; i++)
                {
                    if (viewModel.TaskItems[i] != null)
                    {
                        viewModel.TaskItems[i].ButtonEnabled = !_isBusy && viewModel.TaskItems[i].ButtonEnabled;
                    }
                }
            }
            _view?.Render(viewModel);
        }

        private void OnRuntimeStateChanged(HolmasGameplayRuntimeStateChangeReason reason)
        {
            if (ScreenService == null ||
                !ReferenceEquals(ScreenService.NavigationState.CurrentPage, this))
            {
                return;
            }

            switch (reason)
            {
                case HolmasGameplayRuntimeStateChangeReason.TasksRefilled:
                case HolmasGameplayRuntimeStateChangeReason.PromotionUpgraded:
                case HolmasGameplayRuntimeStateChangeReason.EnergyChanged:
                case HolmasGameplayRuntimeStateChangeReason.LevelStarted:
                case HolmasGameplayRuntimeStateChangeReason.LevelRevealed:
                case HolmasGameplayRuntimeStateChangeReason.LevelCompleted:
                case HolmasGameplayRuntimeStateChangeReason.CurrentLevelSessionEnded:
                    Refresh(null);
                    break;
                case HolmasGameplayRuntimeStateChangeReason.TaskRewardClaimed:
                    Refresh(GetCurrentRewardTip(_runtime));
                    break;
            }
        }

        private static string BuildStatusWithNewRewardTip(HolmasGameplayRuntime runtime, int previousTipVersion, string fallbackStatus)
        {
            if (runtime != null &&
                runtime.LastTaskRewardTipVersion != previousTipVersion &&
                !string.IsNullOrWhiteSpace(runtime.LastTaskRewardTip))
            {
                return string.IsNullOrWhiteSpace(fallbackStatus)
                    ? runtime.LastTaskRewardTip
                    : $"{runtime.LastTaskRewardTip} {fallbackStatus}";
            }

            return fallbackStatus;
        }

        private static string GetCurrentRewardTip(HolmasGameplayRuntime runtime)
        {
            return runtime != null && !string.IsNullOrWhiteSpace(runtime.LastTaskRewardTip)
                ? runtime.LastTaskRewardTip
                : null;
        }

        private static string BuildRevealStatus(
            BoardRevealResult result,
            HolmasProgressionAdvanceResult progression,
            HolmasBoardInteractionMode mode)
        {
            if (result == null)
            {
                return "翻格结果为空。";
            }

            if (!result.IsValidAction)
            {
                return string.IsNullOrWhiteSpace(result.FailureReason)
                    ? "该格当前不能翻开。"
                    : result.FailureReason;
            }

            if (result.Completed)
            {
                int progressed = progression != null ? progression.ProgressedTaskIds.Count : 0;
                int completed = progression != null ? progression.CompletedTaskIds.Count : 0;
                return $"本局完成，推进任务 {progressed} 条，新完成 {completed} 条。";
            }

            if (result.FoundCat)
            {
                return mode == HolmasBoardInteractionMode.Find
                    ? $"寻找成功，找到猫，格子 {result.CellIndex}。"
                    : $"行走遇到猫，格子 {result.CellIndex}。";
            }

            return result.ChangedCellIndices.Count > 1
                ? $"已展开 {result.ChangedCellIndices.Count} 个格子。"
                : $"已翻开格子 {result.CellIndex}。";
        }
    }
}
