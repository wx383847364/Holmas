using System;
using System.Threading.Tasks;
using App.HotUpdate.Holmas.Application;
using App.HotUpdate.Holmas.UI.Core;
using App.HotUpdate.Holmas.UI.Screens.Battle;
using App.HotUpdate.Holmas.UI.Screens.Loading;

namespace App.HotUpdate.Holmas.UI.Screens.Main
{
    public sealed class MainPageController : UiPageController
    {
        private MainPresenter _presenter;
        private MainView _view;
        private MainBindings _bindings;
        private bool _isBusy;

        protected override void OnCreate()
        {
            _presenter = new MainPresenter(Root != null ? Root.Context : null);
            _view = RootObject != null ? RootObject.GetComponent<MainView>() : null;
            if (_view == null && RootObject != null)
            {
                _view = RootObject.AddComponent<MainView>();
            }

            _view?.EnsureBindingSurface();
        }

        protected override void OnBind()
        {
            _bindings = MainBindings.Resolve(BindingResolver);
            _view?.Bind(_bindings);
            _view?.SetStartAction(OnStartClicked);
            _view?.SetPromotionAction(OnPromotionClicked);
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

        private async Task HandleStartAsync()
        {
            if (_isBusy)
            {
                return;
            }

            IHolmasLevelLaunchGateway gateway = Root != null ? Root.LevelLaunchGateway : null;
            if (gateway == null)
            {
                Refresh("关卡启动网关不可用。");
                return;
            }

            _isBusy = true;
            Refresh("正在准备本局棋盘...");

            try
            {
                await ScreenService.ShowOverlayAsync(LoadingScreenRegistration.TransitionOverlayScreenId, "正在准备棋盘...");
                int seed = Environment.TickCount;
                await gateway.StartLevelForCurrentPlayerAsync(seed);
                await ScreenService.OpenPageAsync(BattleScreenRegistration.ScreenId, $"关卡已启动，seed={seed}");
            }
            catch (Exception ex)
            {
                Refresh($"进入棋盘失败：{ex.Message}");
            }
            finally
            {
                await ScreenService.CloseAsync(LoadingScreenRegistration.TransitionOverlayScreenId);
                _isBusy = false;

                if (ScreenService != null &&
                    ReferenceEquals(ScreenService.NavigationState.CurrentPage, this))
                {
                    Refresh("主界面已就绪。");
                }
            }
        }

        private void Refresh(string status = null)
        {
            MainVm viewModel = _presenter != null ? _presenter.Build(status) : new MainVm();
            viewModel.StartButtonEnabled = !_isBusy && viewModel.StartButtonEnabled;
            _view?.Render(viewModel);
        }
    }
}
