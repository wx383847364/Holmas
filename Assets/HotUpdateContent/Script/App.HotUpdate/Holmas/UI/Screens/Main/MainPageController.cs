using System;
using System.Threading.Tasks;
using App.HotUpdate.Holmas.Application;
using App.HotUpdate.Holmas.UI.Core;
using App.Shared.Holmas.RuntimeData;

namespace App.HotUpdate.Holmas.UI.Screens.Main
{
    public sealed class MainPageController : UiPageController
    {
        private MainPresenter _presenter;
        private MainView _view;
        private MainBindings _bindings;
        private bool _isBusy;
        private HolmasGameplayRuntime _runtime;

        protected override void OnCreate()
        {
            _runtime = Root != null && Root.Context != null ? Root.Context.GameplayRuntime : null;
            _presenter = new MainPresenter(Root != null ? Root.Context : null);
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
            _view?.SetStartAction(OnStartClicked);
            _view?.SetPromotionAction(OnPromotionClicked);
            _view?.SetTaskSlotAction(OnTaskSlotClicked);
        }

        protected override void OnOpen(object payload)
        {
            Refresh(payload as string);
        }

        protected override void OnResume()
        {
            Refresh("已返回主界面。");
        }

        protected override void OnDestroy()
        {
            _view?.SetStartAction(null);
            _view?.SetPromotionAction(null);
            _view?.SetTaskSlotAction(null);
            if (_runtime != null)
            {
                _runtime.StateChanged -= OnRuntimeStateChanged;
            }
        }

        private void OnStartClicked()
        {
            _ = HandleStartAsync();
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

        private void OnTaskSlotClicked(int slotIndex)
        {
            _ = HandleTaskSlotAsync(slotIndex);
        }

        private async Task HandleStartAsync()
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
                finalStatus = $"进入棋盘失败：{ex.Message}";
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
                if (!slot.IsUnlocked)
                {
                    finalStatus = $"任务槽 {slotIndex + 1} 尚未解锁。";
                }
                else if (runtimeTask != null && runtimeTask.CanClaimReward)
                {
                    var claimResult = context.ClaimTaskReward(slotIndex);
                    finalStatus = claimResult != null && claimResult.Success
                        ? $"任务槽 {slotIndex + 1} 已领奖，金币 +{claimResult.Reward}。"
                        : $"任务领奖失败：{claimResult?.FailureReason ?? "未知错误"}";
                }
                else
                {
                    var refillResult = context.RefillAvailableTasks();
                    int generatedCount = refillResult != null ? refillResult.GeneratedTasks.Count : 0;
                    finalStatus = generatedCount > 0
                        ? $"任务栏已补 {generatedCount} 条新任务。"
                        : $"任务槽 {slotIndex + 1} 当前没有可领取奖励。";
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

        private void Refresh(string status = null)
        {
            MainVm viewModel = _presenter != null ? _presenter.Build(status) : new MainVm();
            viewModel.StartButtonEnabled = !_isBusy && viewModel.StartButtonEnabled;
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
                case HolmasGameplayRuntimeStateChangeReason.TaskRewardClaimed:
                case HolmasGameplayRuntimeStateChangeReason.PromotionUpgraded:
                case HolmasGameplayRuntimeStateChangeReason.EnergyChanged:
                case HolmasGameplayRuntimeStateChangeReason.LevelStarted:
                case HolmasGameplayRuntimeStateChangeReason.LevelCompleted:
                case HolmasGameplayRuntimeStateChangeReason.CurrentLevelSessionEnded:
                    Refresh(null);
                    break;
            }
        }
    }
}
