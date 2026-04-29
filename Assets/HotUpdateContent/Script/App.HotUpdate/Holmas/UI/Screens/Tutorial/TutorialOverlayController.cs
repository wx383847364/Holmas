using System;
using System.Collections.Generic;
using App.HotUpdate.Holmas.Application;
using App.HotUpdate.Holmas.Tutorial;
using App.HotUpdate.Holmas.UI.Core;
using App.HotUpdate.Holmas.UI.Screens.Main;
using UnityEngine;

namespace App.HotUpdate.Holmas.UI.Screens.Tutorial
{
    public sealed class TutorialOverlayController : UiOverlayController
    {
        public const string FindFirstCatStepId = CoreFindCatTutorialSteps.FindFirstCatStepId;
        public const string TaskBarStepId = CoreFindCatTutorialSteps.TaskBarStepId;
        public const string ModeStepId = CoreFindCatTutorialSteps.ModeStepId;
        public const string ContinueFindStepId = CoreFindCatTutorialSteps.ContinueFindStepId;
        public const string EnergyStepId = CoreFindCatTutorialSteps.EnergyStepId;
        public const string PromotionStepId = CoreFindCatTutorialSteps.PromotionStepId;
        public const string HelpStepId = CoreFindCatTutorialSteps.HelpStepId;

        private readonly IReadOnlyList<TutorialStepDefinition> _steps = CoreFindCatTutorialSteps.All;
        private TutorialOverlayView _view;
        private TutorialOverlayPayload _payload;
        private HolmasGameplayRuntime _runtime;
        private int _stepIndex;
        private int _openedRewardTipVersion;
        private bool _isCollapsed;
        private bool _tutorialBoardObjectiveSatisfied;

        protected override void OnCreate()
        {
            _view = RootObject != null ? RootObject.GetComponent<TutorialOverlayView>() : null;
            if (_view == null && RootObject != null)
            {
                _view = RootObject.AddComponent<TutorialOverlayView>();
            }

            _view?.EnsureSurface();
        }

        protected override void OnOpen(object payload)
        {
            if (_runtime != null)
            {
                _runtime.StateChanged -= OnRuntimeStateChanged;
            }

            if (_payload?.TutorialSessionService != null)
            {
                _payload.TutorialSessionService.StateChanged -= OnTutorialSessionStateChanged;
            }

            _payload = payload as TutorialOverlayPayload ?? new TutorialOverlayPayload();
            if (_payload.TutorialSessionService == null && Root?.Context?.ServiceContainer != null)
            {
                _payload.TutorialSessionService = Root.Context.ServiceContainer.Get<CoreFindCatTutorialSessionService>();
            }
            _runtime = Root != null && Root.Context != null ? Root.Context.GameplayRuntime : null;
            _view?.SetAssetsRuntime(_payload.AssetsRuntime);
            _openedRewardTipVersion = _runtime != null ? _runtime.LastTaskRewardTipVersion : 0;
            _tutorialBoardObjectiveSatisfied = _payload.TutorialBoardObjectiveSatisfied ||
                                               (_payload.RunMode == TutorialRunMode.DebugStartAtStep &&
                                                _payload.InitialStepIndex > CoreFindCatTutorialSteps.IndexOf(ContinueFindStepId));
            if (_runtime != null)
            {
                _runtime.StateChanged += OnRuntimeStateChanged;
            }

            if (_payload.TutorialSessionService != null)
            {
                _payload.TutorialSessionService.StateChanged += OnTutorialSessionStateChanged;
            }

            _stepIndex = ResolveInitialStepIndex(_payload);
            AdvancePastSatisfiedSteps();
            MarkCurrentStep();
            RenderCurrentStep();
        }

        protected override void OnClose()
        {
            if (_runtime != null)
            {
                _runtime.StateChanged -= OnRuntimeStateChanged;
            }

            if (_payload?.TutorialSessionService != null)
            {
                _payload.TutorialSessionService.StateChanged -= OnTutorialSessionStateChanged;
            }
        }

        protected override void OnDestroy()
        {
            if (_runtime != null)
            {
                _runtime.StateChanged -= OnRuntimeStateChanged;
            }

            if (_payload?.TutorialSessionService != null)
            {
                _payload.TutorialSessionService.StateChanged -= OnTutorialSessionStateChanged;
            }
        }

        private void OnRuntimeStateChanged(HolmasGameplayRuntimeStateChangeReason reason)
        {
            if (reason == HolmasGameplayRuntimeStateChangeReason.LevelRevealed ||
                reason == HolmasGameplayRuntimeStateChangeReason.LevelCompleted ||
                reason == HolmasGameplayRuntimeStateChangeReason.TaskRewardClaimed ||
                reason == HolmasGameplayRuntimeStateChangeReason.LevelStarted)
            {
                AdvancePastSatisfiedSteps();
                MarkCurrentStep();
                RenderCurrentStep();
            }
        }

        private void OnTutorialSessionStateChanged()
        {
            AdvancePastSatisfiedSteps();
            MarkCurrentStep();
            RenderCurrentStep();
        }

        private async void OnNextClicked()
        {
            if (_payload != null && _payload.RunMode == TutorialRunMode.NormalBoardHint)
            {
                if (_payload.ProgressService != null)
                {
                    await _payload.ProgressService.MarkNormalBoardHintDismissedAsync();
                }

                CloseOverlay();
                return;
            }

            TutorialStepDefinition previousStep = CurrentStep;
            if (_payload != null && _payload.ProgressService != null && previousStep != null)
            {
                await _payload.ProgressService.MarkStepCompletedAsync(previousStep.StepIndex, previousStep.StepId);
            }

            if (_stepIndex >= _steps.Count - 1)
            {
                await CompleteTutorialAsync();
                return;
            }

            _stepIndex++;
            _isCollapsed = false;
            AdvancePastSatisfiedSteps();
            MarkCurrentStep();
            RenderCurrentStep();
        }

        private async void OnSkipClicked()
        {
            TutorialStepDefinition step = CurrentStep;
            if (_payload != null &&
                _payload.CanWriteCompletion &&
                _payload.RunMode != TutorialRunMode.Replay &&
                _payload.ProgressService != null &&
                step != null)
            {
                await _payload.ProgressService.MarkSkippedAsync(step.StepIndex, step.StepId);
            }

            await CloseAndNotifyTutorialExitedAsync(ShouldNotifyTutorialExited());
        }

        private void OnCollapseClicked()
        {
            _isCollapsed = true;
            RenderCurrentStep();
        }

        private void OnExpandClicked()
        {
            _isCollapsed = false;
            RenderCurrentStep();
        }

        public bool HandleHelpButtonClicked()
        {
            if (_isCollapsed)
            {
                OnExpandClicked();
            }

            return true;
        }

        private async System.Threading.Tasks.Task CompleteTutorialAsync()
        {
            TutorialStepDefinition step = CurrentStep;
            if (_payload != null &&
                _payload.ProgressService != null &&
                step != null &&
                CanWriteCompletedProgress())
            {
                await _payload.ProgressService.MarkCompletedAsync(step.StepIndex, step.StepId);
            }

            await CloseAndNotifyTutorialExitedAsync(ShouldNotifyTutorialExited());
        }

        private bool CanWriteCompletedProgress()
        {
            if (_payload == null || !_payload.CanWriteCompletion || _payload.RunMode == TutorialRunMode.Replay)
            {
                return false;
            }

            if (_payload.RunMode == TutorialRunMode.DebugStartAtStep)
            {
                return true;
            }

            return _tutorialBoardObjectiveSatisfied ||
                   (_payload.TutorialSessionService?.ActiveSession != null &&
                    _payload.TutorialSessionService.ActiveSession.TutorialBoardObjectiveSatisfied);
        }

        private void CloseOverlay()
        {
            if (ScreenService != null)
            {
                _ = ScreenService.CloseAsync(TutorialScreenRegistration.ScreenId);
            }
        }

        private bool ShouldNotifyTutorialExited()
        {
            return _payload != null &&
                   _payload.CanWriteCompletion &&
                   _payload.RunMode != TutorialRunMode.Replay &&
                   _payload.RunMode != TutorialRunMode.NormalBoardHint;
        }

        private async System.Threading.Tasks.Task CloseAndNotifyTutorialExitedAsync(bool notifyTutorialExited)
        {
            Root?.Context?.Logger?.LogInfo(
                "TutorialOverlayController: CloseAndNotifyTutorialExitedAsync begin. notify={0}, runMode={1}, canWriteCompletion={2}, hasCallback={3}, currentPage={4}, stepIndex={5}, stepId={6}",
                notifyTutorialExited,
                _payload != null ? _payload.RunMode.ToString() : "null",
                _payload != null && _payload.CanWriteCompletion,
                _payload?.OnTutorialExitedAsync != null,
                ScreenService?.NavigationState?.CurrentPage != null
                    ? ScreenService.NavigationState.CurrentPage.GetType().Name
                    : "null",
                _stepIndex,
                CurrentStep != null ? CurrentStep.StepId : "null");

            if (ScreenService != null)
            {
                await ScreenService.CloseAsync(TutorialScreenRegistration.ScreenId);
            }

            if (notifyTutorialExited && _payload?.OnTutorialExitedAsync != null)
            {
                Root?.Context?.Logger?.LogInfo("TutorialOverlayController: notifying tutorial exit through payload callback.");
                await _payload.OnTutorialExitedAsync();
                return;
            }

            if (notifyTutorialExited &&
                ScreenService?.NavigationState?.CurrentPage is MainPageController mainPageController)
            {
                Root?.Context?.Logger?.LogInfo("TutorialOverlayController: notifying tutorial exit through MainPageController fallback.");
                await mainPageController.HandleTutorialExitedAsync();
                return;
            }

            Root?.Context?.Logger?.LogInfo(
                "TutorialOverlayController: tutorial exit notification skipped. notify={0}, hasCallback={1}, currentPage={2}",
                notifyTutorialExited,
                _payload?.OnTutorialExitedAsync != null,
                ScreenService?.NavigationState?.CurrentPage != null
                    ? ScreenService.NavigationState.CurrentPage.GetType().Name
                    : "null");
        }

        private TutorialStepDefinition CurrentStep =>
            _stepIndex >= 0 && _stepIndex < _steps.Count ? _steps[_stepIndex] : null;

        private void AdvancePastSatisfiedSteps()
        {
            while (_stepIndex < _steps.Count - 1 && IsStepAutoSatisfied(_steps[_stepIndex]))
            {
                if (string.Equals(_steps[_stepIndex].StepId, ContinueFindStepId, StringComparison.Ordinal))
                {
                    _tutorialBoardObjectiveSatisfied = true;
                }

                _stepIndex++;
            }
        }

        private bool IsStepAutoSatisfied(TutorialStepDefinition step)
        {
            if (step == null || _runtime == null)
            {
                return false;
            }

            switch (step.StepId)
            {
                case FindFirstCatStepId:
                    return IsTutorialCatRevealed(CoreFindCatTutorialBoardDefinition.FirstCatCellIndex) ||
                           IsTutorialCatRevealed(CoreFindCatTutorialBoardDefinition.SecondCatCellIndex);
                case ContinueFindStepId:
                    CoreFindCatTutorialSession session = _payload?.TutorialSessionService?.ActiveSession;
                    bool completed = session != null && session.TutorialBoardObjectiveSatisfied;
                    if (completed)
                    {
                        _tutorialBoardObjectiveSatisfied = true;
                    }

                    return completed;
                default:
                    return false;
            }
        }

        private bool IsTutorialCatRevealed(int cellIndex)
        {
            CoreFindCatTutorialSession session = _payload?.TutorialSessionService?.ActiveSession;
            return session != null && session.IsTutorialCatRevealed(cellIndex);
        }

        private void MarkCurrentStep()
        {
            TutorialStepDefinition step = CurrentStep;
            if (_payload == null ||
                _payload.RunMode == TutorialRunMode.Replay ||
                _payload.RunMode == TutorialRunMode.NormalBoardHint ||
                _payload.ProgressService == null ||
                step == null)
            {
                return;
            }

            _ = _payload.ProgressService.MarkCurrentStepAsync(step.StepIndex, step.StepId);
        }

        private void RenderCurrentStep()
        {
            TutorialStepDefinition step = CurrentStep;
            if (step == null || _view == null)
            {
                return;
            }

            RectTransform target = _payload?.MainView != null
                ? _payload.MainView.ResolveTutorialTarget(step.TargetKey)
                : null;

            TutorialStepVisualDefinition visual = ResolveVisual(step);
            var viewModel = new TutorialOverlayVm
            {
                StepId = step.StepId,
                TargetKey = step.TargetKey,
                Title = ResolveTitle(step),
                Body = ResolveBody(step),
                NextButtonText = ResolveNextButtonText(step),
                SkipButtonText = "跳过",
                CollapseButtonText = "收起",
                CollapsedHintText = step.CollapsedHintText,
                IsCollapsed = _isCollapsed,
                CanSkip = _payload == null || (_payload.RunMode != TutorialRunMode.NormalBoardHint && step.CanSkip),
                AllowPassThroughInput = step.AllowPassThroughInput,
                TargetRect = target,
                MainImagePath = visual != null ? visual.MainImagePath : string.Empty,
                TipsIconPath = visual != null ? visual.TipsIconPath : string.Empty,
                FingerIconPath = visual != null ? visual.FingerIconPath : string.Empty,
            };

            _view.SetActions(OnNextClicked, OnSkipClicked, OnCollapseClicked);
            _view.Render(viewModel);
        }

        private string ResolveTitle(TutorialStepDefinition step)
        {
            return step.Title;
        }

        private string ResolveBody(TutorialStepDefinition step)
        {
            return step.Body;
        }

        private string ResolveNextButtonText(TutorialStepDefinition step)
        {
            if (_payload != null && _payload.RunMode == TutorialRunMode.NormalBoardHint)
            {
                return "知道了";
            }

            return _stepIndex >= _steps.Count - 1 ? "完成" : step.NextButtonText;
        }

        private TutorialStepVisualDefinition ResolveVisual(TutorialStepDefinition step)
        {
            if (_payload?.VisualConfig == null || step == null)
            {
                return null;
            }

            return _payload.VisualConfig.Find(string.IsNullOrWhiteSpace(step.VisualKey) ? step.StepId : step.VisualKey);
        }

        private int ResolveInitialStepIndex(TutorialOverlayPayload payload)
        {
            if (payload != null && payload.RunMode == TutorialRunMode.NormalBoardHint)
            {
                return Math.Max(0, CoreFindCatTutorialSteps.IndexOf(TaskBarStepId));
            }

            if (payload != null && payload.InitialStepIndex >= 0)
            {
                return CoreFindCatTutorialSteps.ClampIndex(payload.InitialStepIndex);
            }

            if (payload != null && !string.IsNullOrWhiteSpace(payload.InitialStepId))
            {
                int fromId = CoreFindCatTutorialSteps.IndexOf(payload.InitialStepId);
                if (fromId >= 0)
                {
                    return fromId;
                }
            }

            return _payload?.TutorialSessionService?.ActiveSession != null
                ? 0
                : Math.Max(0, CoreFindCatTutorialSteps.IndexOf(TaskBarStepId));
        }
    }
}
