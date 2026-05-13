using System;
using System.Threading.Tasks;
using App.HotUpdate.Holmas.Application;
using App.HotUpdate.Holmas.UI.Core;

namespace App.HotUpdate.Holmas.UI.Screens.AgencyMain
{
    /// <summary>
    /// AgencyMain 的最小 Page 控制器。
    /// 负责 presenter/view 接线与输入转发，不承载玩法规则。
    /// </summary>
    public sealed class AgencyMainPageController : UiPageController
    {
        private AgencyMainPresenter _presenter;
        private AgencyMainView _view;
        private AgencyMainBindings _bindings;
        private bool _isBusy;
        private HolmasGameplayRuntime _runtime;

        protected override void OnCreate()
        {
            _runtime = Root != null && Root.Context != null ? Root.Context.GameplayRuntime : null;
            _presenter = new AgencyMainPresenter(Root != null ? Root.Context : null);
            _view = RootObject != null ? RootObject.GetComponent<AgencyMainView>() : null;
            if (_view == null)
            {
                throw new InvalidOperationException("AgencyMainPanel prefab 缺少 AgencyMainView，请在 prefab 静态挂载。");
            }

            if (_runtime != null)
            {
                _runtime.StateChanged += OnRuntimeStateChanged;
            }
        }

        protected override void OnBind()
        {
            _bindings = AgencyMainBindings.Resolve(BindingResolver);
            if (_bindings == null || !_bindings.HasRequiredBindings)
            {
                throw new InvalidOperationException("AgencyMainPanel 缺少完整 UiReferenceCollector 静态绑定，请先在 prefab 侧补齐 AgencyMainGeneratedBindings.Manifest 对应节点。");
            }

            _view?.Bind(_bindings);
            _view?.SetPrimaryAction(OnPrimaryActionClicked);
            _view?.SetTaskClaimAction(OnTaskClaimClicked);
        }

        protected override void OnOpen(object payload)
        {
            string status = payload as string;
            Refresh(status);
        }

        protected override void OnResume()
        {
            Refresh("已回到侦探社。");
        }

        protected override void OnClose()
        {
        }

        protected override void OnDestroy()
        {
            _view?.SetPrimaryAction(null);
            _view?.SetTaskClaimAction(null);
            if (_runtime != null)
            {
                _runtime.StateChanged -= OnRuntimeStateChanged;
            }
        }

        private void OnPrimaryActionClicked()
        {
            _ = HandlePrimaryActionAsync();
        }

        private void OnTaskClaimClicked(int slotIndex)
        {
            _ = HandleClaimTaskAsync(slotIndex);
        }

        private void Refresh(string status)
        {
            AgencyMainVm viewModel = _presenter != null
                ? _presenter.Build(status, LoadedHandle != null && LoadedHandle.IsPlaceholder)
                : new AgencyMainVm();
            viewModel.PrimaryActionEnabled = !_isBusy && viewModel.PrimaryActionEnabled;
            if (viewModel.TaskItems != null)
            {
                for (int i = 0; i < viewModel.TaskItems.Length; i++)
                {
                    if (viewModel.TaskItems[i] != null)
                    {
                        viewModel.TaskItems[i].ClaimButtonEnabled = !_isBusy && viewModel.TaskItems[i].ClaimButtonEnabled;
                    }
                }
            }
            _view?.Render(viewModel);
        }

        private async Task HandlePrimaryActionAsync()
        {
            if (_isBusy)
            {
                return;
            }

            HolmasFlowCoordinator flowCoordinator = Root != null ? Root.FlowCoordinator : null;
            if (flowCoordinator == null)
            {
                Refresh("界面流转协调器不可用。");
                return;
            }

            _isBusy = true;
            Refresh("正在准备本局棋盘...");
            string finalStatus = null;

            try
            {
                await flowCoordinator.StartBattleAsync();
            }
            catch (Exception ex)
            {
                finalStatus = "进入棋盘失败：" + ex.Message;
                Refresh(finalStatus);
            }
            finally
            {
                _isBusy = false;

                if (ScreenService != null &&
                    ReferenceEquals(ScreenService.NavigationState.CurrentPage, this))
                {
                    Refresh(finalStatus ?? "侦探社已就绪。");
                }
            }
        }

        private async Task HandleClaimTaskAsync(int slotIndex)
        {
            if (_isBusy)
            {
                return;
            }

            HolmasApplicationContext context = Root != null ? Root.Context : null;
            if (context == null)
            {
                Refresh("应用上下文不可用，无法查看任务状态。");
                return;
            }

            _isBusy = true;
            string finalStatus = null;
            Refresh($"正在查看任务槽 {slotIndex + 1} 状态...");

            try
            {
                var slot = context.GameplayRuntime != null && context.GameplayRuntime.TaskBarState != null
                    ? context.GameplayRuntime.TaskBarState.GetSlot(slotIndex)
                    : null;
                var runtimeTask = context.GameplayRuntime != null && context.GameplayRuntime.TaskBarState != null
                    ? context.GameplayRuntime.TaskBarState.GetTaskBySlot(slotIndex)
                    : null;
                if (slot != null && !slot.IsUnlocked)
                {
                    finalStatus = BuildLockedTaskStatus(slotIndex);
                }
                else
                {
                    finalStatus = BuildTaskStatus(slotIndex, slot, runtimeTask);
                }
                Refresh(finalStatus);
            }
            catch (Exception ex)
            {
                finalStatus = "任务状态查看失败：" + ex.Message;
                Refresh(finalStatus);
            }
            finally
            {
                _isBusy = false;
                if (ScreenService != null &&
                    ReferenceEquals(ScreenService.NavigationState.CurrentPage, this))
                {
                    Refresh(finalStatus);
                }

                await Task.CompletedTask;
            }
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
                case HolmasGameplayRuntimeStateChangeReason.LevelCompleted:
                case HolmasGameplayRuntimeStateChangeReason.PromotionUpgraded:
                case HolmasGameplayRuntimeStateChangeReason.DebugGoldChanged:
                case HolmasGameplayRuntimeStateChangeReason.CurrentLevelSessionEnded:
                    Refresh(null);
                    break;
                case HolmasGameplayRuntimeStateChangeReason.TaskRewardClaimed:
                    Refresh(_runtime != null && !string.IsNullOrWhiteSpace(_runtime.LastTaskRewardTip)
                        ? _runtime.LastTaskRewardTip
                        : null);
                    break;
            }
        }

        private static string BuildTaskStatus(int slotIndex, App.Shared.Holmas.RuntimeData.TaskSlotState slot, App.HotUpdate.Holmas.Tasks.Runtime.HolmasTaskRuntimeInstance runtimeTask)
        {
            if (slot == null || !slot.IsUnlocked)
            {
                return $"任务槽 {slotIndex + 1} 尚未解锁。";
            }

            if (runtimeTask == null || runtimeTask.Task == null)
            {
                return $"任务槽 {slotIndex + 1} 当前为空；当前等级暂无可补任务。";
            }

            string suffix = slot.PendingRelockAfterTaskCompletion
                ? "广告槽已到期，完成当前任务后会自动领奖并重新锁定。"
                : "完成后会自动领奖并补新任务。";
            return $"任务槽 {slotIndex + 1} 进度 {runtimeTask.Task.CurrentCount}/{runtimeTask.Task.TargetCount}，{suffix}";
        }

        private static string BuildLockedTaskStatus(int slotIndex)
        {
            return $"任务槽 {slotIndex + 1} 尚未解锁；请通过任务奖励或广告入口解锁。";
        }
    }
}
