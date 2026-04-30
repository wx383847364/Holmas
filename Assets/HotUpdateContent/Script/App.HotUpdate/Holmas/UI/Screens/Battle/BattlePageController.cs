using System;
using System.Threading.Tasks;
using App.HotUpdate.Holmas.Application;
using App.HotUpdate.Holmas.Board;
using App.HotUpdate.Holmas.Progression;
using App.HotUpdate.Holmas.UI.Core;
using App.Shared.Contracts;

namespace App.HotUpdate.Holmas.UI.Screens.Battle
{
    public sealed class BattlePageController : UiPageController
    {
        private BattlePresenter _presenter;
        private BattleView _view;
        private BattleBindings _bindings;
        private HolmasGameplayRuntime _runtime;

        // 领域事件订阅句柄。
        // Battle 是首轮试迁移页面：体力变化和任务奖励提示已经从旧 StateChanged 分支迁到 EventBus。
        // 保存句柄后，在 OnDestroy 中 Dispose，可以避免页面销毁后继续收到事件。
        private IEventSubscription _energyChangedSubscription;
        private IEventSubscription _taskRewardTipSubscription;
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
                // 旧 StateChanged 暂时保留，用于后续还没迁移的 Runtime reason。
                // 已迁移的 EnergyChanged / TaskRewardClaimed 不再在 OnRuntimeStateChanged 中处理，避免双刷新。
                _runtime.StateChanged += OnRuntimeStateChanged;
            }

            IEventBus eventBus = Root != null && Root.Context != null ? Root.Context.EventBus : null;
            if (eventBus != null)
            {
                // 新领域事件订阅：只关心 Battle 页面当前需要的两个低风险链路。
                // 这里不订阅通用 HolmasGameplayStateChangedEvent，避免重新引入“大而全”的 reason switch。
                _energyChangedSubscription = eventBus.SubscribeScoped<HolmasEnergyChangedEvent>(OnEnergyChanged);
                _taskRewardTipSubscription = eventBus.SubscribeScoped<HolmasTaskRewardTipChangedEvent>(OnTaskRewardTipChanged);
            }
        }

        protected override void OnBind()
        {
            _bindings = BattleBindings.Resolve(BindingResolver);
            _view?.Bind(_bindings);
            _view?.SetAssetsRuntime(Root != null && Root.Context != null ? Root.Context.AssetsRuntime : null);
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

            // Dispose 是幂等的，即使页面销毁流程重复调用也不会报错。
            _energyChangedSubscription?.Dispose();
            _energyChangedSubscription = null;
            _taskRewardTipSubscription?.Dispose();
            _taskRewardTipSubscription = null;
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
                int rewardTipVersionBeforeReveal = runtime.LastTaskRewardTipVersion;
                HolmasProgressionAdvanceResult progressionResult;
                HolmasBoardInteractionMode mode = isFlagAction
                    ? HolmasBoardInteractionMode.Find
                    : HolmasBoardInteractionMode.Walk;
                BoardRevealResult revealResult = runtime.RevealCell(cellIndex, mode, out progressionResult);
                string revealStatus = BuildRevealStatus(revealResult, progressionResult, mode);
                revealStatus = BuildStatusWithNewRewardTip(runtime, rewardTipVersionBeforeReveal, revealStatus);
                Refresh(revealStatus);
                if (revealResult != null && revealResult.IsValidAction && revealResult.Completed)
                {
                    await AdvanceToNextLevelAsync(progressionResult, revealStatus, rewardTipVersionBeforeReveal);
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

        private async Task AdvanceToNextLevelAsync(
            HolmasProgressionAdvanceResult progressionResult,
            string fallbackStatus,
            int rewardTipVersionBeforeReveal = -1)
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
                HolmasGameplayRuntime runtime = Root != null && Root.Context != null ? Root.Context.GameplayRuntime : null;
                Refresh(BuildStatusWithNewRewardTip(runtime, rewardTipVersionBeforeReveal, fallbackStatus));
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

            // EnergyChanged 和 TaskRewardClaimed 已由下面的领域事件处理。
            // 如果这里继续处理同样 reason，一次 Runtime 变化会先触发旧 StateChanged，再触发新 EventBus，
            // Battle 页面就会刷新两次，状态文案也可能被后一次刷新覆盖。
        }

        /// <summary>
        /// 体力变化事件处理。
        /// 只在当前页面仍是 BattlePageController 时刷新，避免后台页面被全局事件误刷新。
        /// </summary>
        private void OnEnergyChanged(HolmasEnergyChangedEvent eventData)
        {
            if (ScreenService == null ||
                !ReferenceEquals(ScreenService.NavigationState.CurrentPage, this))
            {
                return;
            }

            Refresh(null);
        }

        /// <summary>
        /// 任务奖励提示事件处理。
        /// 优先使用事件 DTO 里的 Tip；如果事件为空或 Tip 为空，再回退读取 Runtime 当前提示。
        /// </summary>
        private void OnTaskRewardTipChanged(HolmasTaskRewardTipChangedEvent eventData)
        {
            if (ScreenService == null ||
                !ReferenceEquals(ScreenService.NavigationState.CurrentPage, this))
            {
                return;
            }

            Refresh(eventData != null && !string.IsNullOrWhiteSpace(eventData.Tip)
                ? eventData.Tip
                : GetCurrentRewardTip(_runtime));
        }

        private static string BuildStatusWithNewRewardTip(HolmasGameplayRuntime runtime, int previousTipVersion, string fallbackStatus)
        {
            if (runtime != null &&
                previousTipVersion >= 0 &&
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
    }
}
