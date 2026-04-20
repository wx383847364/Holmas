using System;
using System.Threading.Tasks;
using App.HotUpdate.Holmas.Application;
using App.HotUpdate.Holmas.Board;
using App.HotUpdate.Holmas.Progression;
using App.HotUpdate.Holmas.UI.Core;

namespace App.HotUpdate.Holmas.UI.Screens.Battle
{
    public sealed class BattlePageController : UiPageController
    {
        private BattlePresenter _presenter;
        private BattleView _view;
        private BattleBindings _bindings;
        private HolmasGameplayRuntime _runtime;
        private bool _isProcessing;

        protected override void OnCreate()
        {
            _runtime = Root != null && Root.Context != null ? Root.Context.GameplayRuntime : null;
            _presenter = new BattlePresenter(Root != null ? Root.Context : null);
            _view = RootObject != null ? RootObject.GetComponent<BattleView>() : null;
            if (_view == null && RootObject != null)
            {
                _view = RootObject.AddComponent<BattleView>();
            }

            _view?.EnsureBindingSurface();
            if (_runtime != null)
            {
                _runtime.StateChanged += OnRuntimeStateChanged;
            }
        }

        protected override void OnBind()
        {
            _bindings = BattleBindings.Resolve(BindingResolver);
            _view?.Bind(_bindings);
            _view?.SetBackAction(OnBackClicked);
            _view?.SetCellAction(OnCellClicked);
        }

        protected override void OnOpen(object payload)
        {
            string status = payload as string;
            Refresh(status);
            _ = AdvanceAlreadyCompletedBoardAsync(status);
        }

        protected override void OnResume()
        {
            const string status = "已回到当前棋盘。";
            Refresh(status);
            _ = AdvanceAlreadyCompletedBoardAsync(status);
        }

        protected override void OnDestroy()
        {
            _view?.SetBackAction(null);
            _view?.SetCellAction(null);
            if (_runtime != null)
            {
                _runtime.StateChanged -= OnRuntimeStateChanged;
            }
        }

        private void OnBackClicked()
        {
            _ = HandleBackAsync();
        }

        private void OnCellClicked(int cellIndex, bool isFlagAction)
        {
            _ = HandleCellInteractionAsync(cellIndex, isFlagAction);
        }

        private async Task HandleCellInteractionAsync(int cellIndex, bool isFlagAction)
        {
            if (_isProcessing)
            {
                return;
            }

            HolmasGameplayRuntime runtime = Root != null && Root.Context != null ? Root.Context.GameplayRuntime : null;
            if (runtime == null)
            {
                Refresh("玩法运行时不可用。");
                return;
            }

            _isProcessing = true;
            try
            {
                HolmasProgressionAdvanceResult progressionResult;
                HolmasBoardInteractionMode mode = isFlagAction
                    ? HolmasBoardInteractionMode.Find
                    : HolmasBoardInteractionMode.Walk;
                BoardRevealResult revealResult = runtime.RevealCell(cellIndex, mode, out progressionResult);
                string revealStatus = BuildRevealStatus(revealResult, progressionResult, mode);
                Refresh(revealStatus);
                if (revealResult != null && revealResult.IsValidAction && revealResult.Completed)
                {
                    await AdvanceToNextLevelAsync(progressionResult, revealStatus);
                }
            }
            catch (Exception ex)
            {
                Refresh($"棋盘操作失败：{ex.Message}");
            }
            finally
            {
                _isProcessing = false;
                await Task.CompletedTask;
            }
        }

        private async Task AdvanceToNextLevelAsync(HolmasProgressionAdvanceResult progressionResult, string fallbackStatus)
        {
            HolmasFlowCoordinator flowCoordinator = Root != null ? Root.FlowCoordinator : null;
            if (flowCoordinator == null)
            {
                Refresh(fallbackStatus);
                return;
            }

            try
            {
                await flowCoordinator.AdvanceToNextBattleAsync(progressionResult);
            }
            catch (Exception ex)
            {
                Refresh($"本局完成，但进入下一关失败：{ex.Message}");
            }
        }

        private async Task AdvanceAlreadyCompletedBoardAsync(string fallbackStatus)
        {
            if (_isProcessing)
            {
                return;
            }

            HolmasGameplayRuntime runtime = Root != null && Root.Context != null ? Root.Context.GameplayRuntime : null;
            if (runtime == null || runtime.CurrentBoardRuntime == null || !runtime.CurrentBoardRuntime.Completed)
            {
                return;
            }

            _isProcessing = true;
            try
            {
                HolmasProgressionAdvanceResult progressionResult = runtime.ApplyCurrentLevelCompletion();
                await AdvanceToNextLevelAsync(progressionResult, fallbackStatus);
            }
            catch (Exception ex)
            {
                Refresh($"本局完成，但进入下一关失败：{ex.Message}");
            }
            finally
            {
                _isProcessing = false;
            }
        }

        private async Task HandleBackAsync()
        {
            if (_isProcessing)
            {
                return;
            }

            HolmasFlowCoordinator flowCoordinator = Root != null ? Root.FlowCoordinator : null;
            if (flowCoordinator == null)
            {
                await ScreenService.BackAsync();
                return;
            }

            _isProcessing = true;
            try
            {
                await flowCoordinator.ExitBattleToMainAsync();
            }
            catch (Exception ex)
            {
                Refresh($"返回侦探社失败：{ex.Message}");
            }
            finally
            {
                _isProcessing = false;
            }
        }

        private void Refresh(string status = null)
        {
            BattleVm viewModel = _presenter != null ? _presenter.Build(status) : new BattleVm();
            _view?.Render(viewModel);
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

        private void OnRuntimeStateChanged(HolmasGameplayRuntimeStateChangeReason reason)
        {
            if (ScreenService == null ||
                !ReferenceEquals(ScreenService.NavigationState.CurrentPage, this))
            {
                return;
            }

            if (reason == HolmasGameplayRuntimeStateChangeReason.EnergyChanged)
            {
                Refresh(null);
            }
        }
    }
}
