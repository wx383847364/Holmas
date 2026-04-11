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
        private bool _isProcessing;
        private bool _isLeaving;

        protected override void OnCreate()
        {
            _presenter = new BattlePresenter(Root != null ? Root.Context : null);
            _view = RootObject != null ? RootObject.GetComponent<BattleView>() : null;
            if (_view == null && RootObject != null)
            {
                _view = RootObject.AddComponent<BattleView>();
            }

            _view?.EnsureBindingSurface();
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
            Refresh(payload as string);
        }

        protected override void OnResume()
        {
            Refresh("已回到当前棋盘。");
        }

        protected override void OnDestroy()
        {
            _view?.SetBackAction(null);
            _view?.SetCellAction(null);
        }

        private void OnBackClicked()
        {
            if (_isLeaving || _isProcessing)
            {
                return;
            }

            _ = HandleBackAsync();
        }

        private void OnCellClicked(int cellIndex, bool isFlagAction)
        {
            _ = HandleCellInteractionAsync(cellIndex, isFlagAction);
        }

        private async Task HandleBackAsync()
        {
            HolmasFlowCoordinator flowCoordinator = Root != null ? Root.FlowCoordinator : null;
            if (flowCoordinator == null)
            {
                Refresh("界面流转协调器不可用。");
                return;
            }

            _isLeaving = true;
            try
            {
                await flowCoordinator.ExitBattleToMainAsync();
            }
            catch (Exception ex)
            {
                Refresh("返回主界面失败：" + ex.Message);
            }
            finally
            {
                _isLeaving = false;
            }
        }

        private async Task HandleCellInteractionAsync(int cellIndex, bool isFlagAction)
        {
            if (_isProcessing || _isLeaving)
            {
                return;
            }

            HolmasGameplayRuntime runtime = Root != null && Root.Context != null ? Root.Context.GameplayRuntime : null;
            if (runtime == null)
            {
                Refresh("玩法运行时不可用。");
                return;
            }

            if (runtime.CurrentBoardRuntime != null && runtime.CurrentBoardRuntime.Completed)
            {
                Refresh("本局已结算，请返回主界面。");
                return;
            }

            _isProcessing = true;
            try
            {
                if (isFlagAction)
                {
                    BoardRevealResult flagResult = runtime.ToggleFlag(cellIndex);
                    Refresh(BuildFlagStatus(flagResult));
                }
                else
                {
                    HolmasProgressionAdvanceResult progressionResult;
                    BoardRevealResult revealResult = runtime.RevealCell(cellIndex, out progressionResult);
                    Refresh(BuildRevealStatus(revealResult, progressionResult));
                }
            }
            catch (Exception ex)
            {
                Refresh("棋盘操作失败：" + ex.Message);
            }
            finally
            {
                _isProcessing = false;
                await Task.CompletedTask;
            }
        }

        private void Refresh(string status = null)
        {
            BattleVm viewModel = _presenter != null ? _presenter.Build(status) : new BattleVm();
            _view?.Render(viewModel);
        }

        private static string BuildFlagStatus(BoardRevealResult result)
        {
            if (result == null)
            {
                return "插旗结果为空。";
            }

            if (!result.IsValidAction)
            {
                return "该格当前不能插旗。";
            }

            return $"已切换格子 {result.CellIndex} 的旗标。";
        }

        private static string BuildRevealStatus(BoardRevealResult result, HolmasProgressionAdvanceResult progression)
        {
            if (result == null)
            {
                return "翻格结果为空。";
            }

            if (!result.IsValidAction)
            {
                return "该格当前不能翻开。";
            }

            if (result.Completed)
            {
                int progressed = progression != null ? progression.ProgressedTaskIds.Count : 0;
                int completed = progression != null ? progression.CompletedTaskIds.Count : 0;
                return $"本局完成，推进任务 {progressed} 条，新完成 {completed} 条。";
            }

            if (result.FoundCat)
            {
                return $"找到了一只猫，格子 {result.CellIndex}。";
            }

            return result.ChangedCellIndices.Count > 1
                ? $"已展开 {result.ChangedCellIndices.Count} 个格子。"
                : $"已翻开格子 {result.CellIndex}。";
        }
    }
}
