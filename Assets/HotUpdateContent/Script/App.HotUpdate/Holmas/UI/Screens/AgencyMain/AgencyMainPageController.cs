using System;
using System.Threading.Tasks;
using App.HotUpdate.Holmas.Application;
using App.HotUpdate.Holmas.Tasks.Services;
using App.HotUpdate.Holmas.UI.Core;
using App.HotUpdate.Holmas.UI.Generated;

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
        private bool _isFallbackLayout;
        private bool _isBusy;
        private HolmasGameplayRuntime _runtime;

        protected override void OnCreate()
        {
            _runtime = Root != null && Root.Context != null ? Root.Context.GameplayRuntime : null;
            _presenter = new AgencyMainPresenter(Root != null ? Root.Context : null);
            _view = RootObject != null ? RootObject.GetComponent<AgencyMainView>() : null;
            if (_view == null && RootObject != null)
            {
                _view = RootObject.AddComponent<AgencyMainView>();
            }

            _isFallbackLayout = !AgencyMainBindings.HasCompleteBindings(BindingResolver);
            if (_isFallbackLayout)
            {
                if (RootObject != null)
                {
                    RootObject.name = AgencyMainGeneratedBindings.PrefabName;
                }

                _view?.EnsureFallbackLayout();
            }

            if (_runtime != null)
            {
                _runtime.StateChanged += OnRuntimeStateChanged;
            }
        }

        protected override void OnBind()
        {
            _bindings = AgencyMainBindings.Resolve(BindingResolver);
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
                ? _presenter.Build(status, _isFallbackLayout || (LoadedHandle != null && LoadedHandle.IsPlaceholder))
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
                Refresh("应用上下文不可用，无法领奖。");
                return;
            }

            _isBusy = true;
            string finalStatus = null;
            Refresh($"正在领取任务槽 {slotIndex + 1} 奖励...");

            try
            {
                var result = context.ClaimTaskReward(slotIndex);
                finalStatus = result != null && result.Success
                    ? BuildClaimSuccessStatus(slotIndex, result)
                    : $"任务领奖失败：{result?.FailureReason ?? "未知错误"}";
                Refresh(finalStatus);
            }
            catch (Exception ex)
            {
                finalStatus = "任务领奖失败：" + ex.Message;
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
                case HolmasGameplayRuntimeStateChangeReason.TaskRewardClaimed:
                case HolmasGameplayRuntimeStateChangeReason.LevelCompleted:
                case HolmasGameplayRuntimeStateChangeReason.PromotionUpgraded:
                case HolmasGameplayRuntimeStateChangeReason.CurrentLevelSessionEnded:
                    Refresh(null);
                    break;
            }
        }

        private static string BuildClaimSuccessStatus(int slotIndex, HolmasTaskClaimResult result)
        {
            string refillSummary = result.RefilledTask != null
                ? $"已补新任务 {result.RefilledTask.CatId}。"
                : string.Empty;
            return string.IsNullOrWhiteSpace(refillSummary)
                ? $"任务槽 {slotIndex + 1} 已领奖，金币 +{result.Reward}。"
                : $"任务槽 {slotIndex + 1} 已领奖，金币 +{result.Reward}。{refillSummary}";
        }
    }
}
