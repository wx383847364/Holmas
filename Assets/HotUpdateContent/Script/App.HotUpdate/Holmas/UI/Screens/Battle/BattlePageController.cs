using System;
using System.Threading.Tasks;
using App.HotUpdate.Holmas.Application;
using App.HotUpdate.Holmas.UI.Core;
using App.HotUpdate.Holmas.UI.Screens.Main;

namespace App.HotUpdate.Holmas.UI.Screens.Battle
{
    public sealed class BattlePageController : UiPageController
    {
        private BattlePresenter _presenter;
        private BattleView _view;
        private BattleBindings _bindings;
        private int _selectedStageId;
        private bool _isProcessing;

        protected override void OnCreate()
        {
            _presenter = new BattlePresenter(Root != null ? Root.Context : null);
            _view = RootObject != null ? RootObject.GetComponent<BattleView>() : null;
            if (_view == null)
            {
                throw new InvalidOperationException("BattlePanel prefab 缺少 BattleView，请在 prefab 静态挂载。");
            }

            _selectedStageId = Math.Max(1, Root?.Context?.CurrentAgencyStageId ?? 1);
        }

        protected override void OnBind()
        {
            _bindings = BattleBindings.Resolve(BindingResolver);
            if (_bindings == null || !_bindings.HasRequiredBindings)
            {
                throw new InvalidOperationException("BattlePanel 缺少完整 UiReferenceCollector 静态绑定，请先在 prefab 侧补齐 BattleGeneratedBindings.Manifest 对应节点。");
            }

            _view?.Bind(_bindings);
            _view?.SetAssetsRuntime(Root != null && Root.Context != null ? Root.Context.AssetsRuntime : null);
            _view?.SetBackAction(OnBackClicked);
            _view?.SetBuildAction(null);
            _view?.SetStageAction(OnStageClicked);
            _view?.SetPromotionSlotAction(OnPromotionSlotClicked);
        }

        protected override void OnOpen(object payload)
        {
            _selectedStageId = Math.Max(1, Root?.Context?.CurrentAgencyStageId ?? 1);
            Refresh(payload as string);
        }

        protected override void OnResume()
        {
            Refresh("已回到城市宣传地图。");
        }

        protected override void OnDestroy()
        {
            _view?.SetBackAction(null);
            _view?.SetBuildAction(null);
            _view?.SetStageAction(null);
            _view?.SetPromotionSlotAction(null);
        }

        private void OnBackClicked()
        {
            _ = HandleBackAsync();
        }

        private void OnBuildClicked()
        {
            _ = HandleBuildAsync();
        }

        private void OnStageClicked(int stageSlotIndex)
        {
            BattleVm viewModel = _presenter != null ? _presenter.Build(_selectedStageId) : null;
            if (viewModel == null ||
                viewModel.Stages == null ||
                stageSlotIndex < 0 ||
                stageSlotIndex >= viewModel.Stages.Length ||
                viewModel.Stages[stageSlotIndex] == null ||
                !viewModel.Stages[stageSlotIndex].Visible)
            {
                return;
            }

            BattleStageVm stage = viewModel.Stages[stageSlotIndex];
            _selectedStageId = stage.AgencyStageId;
            Refresh(BuildStageStatus(stage));
        }

        private void OnPromotionSlotClicked(int promotionSlotIndex)
        {
            BattleVm viewModel = _presenter != null ? _presenter.Build(_selectedStageId) : null;
            if (viewModel == null ||
                viewModel.PromotionSlots == null ||
                promotionSlotIndex < 0 ||
                promotionSlotIndex >= viewModel.PromotionSlots.Length ||
                viewModel.PromotionSlots[promotionSlotIndex] == null ||
                !viewModel.PromotionSlots[promotionSlotIndex].Visible)
            {
                return;
            }

            BattlePromotionSlotVm promotionSlot = viewModel.PromotionSlots[promotionSlotIndex];
            if (!promotionSlot.Unlocked)
            {
                Refresh("城市尚未解锁。");
                return;
            }

            if (!promotionSlot.Current)
            {
                Refresh("已完成城市只能回看，不能继续建设。");
                return;
            }

            _ = HandlePromotionAsync(promotionSlotIndex, promotionSlot.PromotionId);
        }

        private async Task HandleBuildAsync()
        {
            BattleVm viewModel = _presenter != null ? _presenter.Build(_selectedStageId) : null;
            int promotionSlotIndex = -1;
            if (viewModel?.PromotionSlots != null)
            {
                for (int i = 0; i < viewModel.PromotionSlots.Length; i++)
                {
                    if (viewModel.PromotionSlots[i] != null && viewModel.PromotionSlots[i].CanBuild)
                    {
                        promotionSlotIndex = i;
                        break;
                    }
                }
            }

            string promotionId = promotionSlotIndex >= 0
                ? viewModel.PromotionSlots[promotionSlotIndex].PromotionId
                : string.Empty;
            await HandlePromotionAsync(promotionSlotIndex, promotionId);
        }

        private async Task HandlePromotionAsync(int promotionSlotIndex, string promotionId)
        {
            if (_isProcessing)
            {
                return;
            }

            HolmasApplicationContext context = Root != null ? Root.Context : null;
            if (context == null)
            {
                Refresh("应用上下文不可用，无法建设城市宣传。");
                return;
            }

            if (_presenter != null && promotionSlotIndex >= 0)
            {
                promotionId = _presenter.GetPromotionIdForSlot(promotionSlotIndex);
            }

            if (string.IsNullOrWhiteSpace(promotionId))
            {
                Refresh(_selectedStageId == context.CurrentAgencyStageId
                    ? "当前城市阶段已完成。"
                    : "只有当前推进阶段可以建设。");
                return;
            }

            _isProcessing = true;
            try
            {
                var result = context.TryUpgradePromotion(promotionId);
                if (result != null && result.Success)
                {
                    Refresh(result.StageAdvanced
                        ? $"宣传 {promotionId} 升到 Lv {result.NewLevel}，已解锁下一城市阶段。"
                        : $"宣传 {promotionId} 升到 Lv {result.NewLevel}，金币 -{result.GoldSpent}。");
                    return;
                }

                Refresh($"宣传建设失败：{result?.FailureReason ?? "未知错误"}");
            }
            catch (Exception ex)
            {
                Refresh($"宣传建设失败：{ex.Message}");
            }
            finally
            {
                _isProcessing = false;
                await Task.CompletedTask;
            }
        }

        private static string BuildStageStatus(BattleStageVm stage)
        {
            if (stage == null)
            {
                return string.Empty;
            }

            string stageName = string.IsNullOrWhiteSpace(stage.StageName)
                ? $"Stage {stage.AgencyStageId}"
                : stage.StageName;
            string progress = string.IsNullOrWhiteSpace(stage.ProgressLabel)
                ? $"{stage.ProgressCurrent}/{stage.ProgressCap}"
                : stage.ProgressLabel;
            if (!stage.Unlocked)
            {
                return $"城市尚未解锁。{stageName}：宣传 {progress}";
            }

            return $"{stageName}：宣传 {progress}";
        }

        private async Task HandleBackAsync()
        {
            if (_isProcessing)
            {
                return;
            }

            _isProcessing = true;
            try
            {
                if (ScreenService == null)
                {
                    return;
                }

                if (ScreenService.IsOpen(BattleScreenRegistration.ScreenId))
                {
                    await ScreenService.CloseAsync(BattleScreenRegistration.ScreenId);
                }

                await ScreenService.OpenPageAsync(MainScreenRegistration.ScreenId, "已返回侦探社。");
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
            BattleVm viewModel = _presenter != null ? _presenter.Build(_selectedStageId, status) : new BattleVm();
            _selectedStageId = viewModel.SelectedStageId;
            _view?.Render(viewModel);
        }
    }
}
