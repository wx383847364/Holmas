using System;
using System.Threading.Tasks;
using App.HotUpdate.Holmas.Application;
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

        protected override void OnCreate()
        {
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
        }

        protected override void OnBind()
        {
            _bindings = AgencyMainBindings.Resolve(BindingResolver);
            _view?.Bind(_bindings);
            _view?.SetPrimaryAction(OnPrimaryActionClicked);
        }

        protected override void OnOpen(object payload)
        {
            string status = payload as string;
            Refresh(status);
        }

        protected override void OnResume()
        {
            Refresh("AgencyMain resumed");
        }

        protected override void OnClose()
        {
        }

        protected override void OnDestroy()
        {
            _view?.SetPrimaryAction(null);
        }

        private void OnPrimaryActionClicked()
        {
            _ = HandlePrimaryActionAsync();
        }

        private void Refresh(string status)
        {
            AgencyMainVm viewModel = _presenter != null
                ? _presenter.Build(status, _isFallbackLayout || (LoadedHandle != null && LoadedHandle.IsPlaceholder))
                : new AgencyMainVm();
            viewModel.PrimaryActionEnabled = !_isBusy && viewModel.PrimaryActionEnabled;
            _view?.Render(viewModel);
        }

        private async Task HandlePrimaryActionAsync()
        {
            if (_isBusy)
            {
                return;
            }

            IHolmasLevelLaunchGateway levelLaunchGateway = Root != null ? Root.LevelLaunchGateway : null;
            if (levelLaunchGateway == null)
            {
                Refresh("Level launch gateway unavailable");
                return;
            }

            _isBusy = true;
            Refresh("Opening level...");

            try
            {
                int seed = Environment.TickCount;
                await levelLaunchGateway.StartLevelForCurrentPlayerAsync(seed);
                Refresh($"Level started, seed={seed}");
            }
            catch (Exception ex)
            {
                Refresh($"Open level failed: {ex.Message}");
            }
            finally
            {
                _isBusy = false;
                Refresh("AgencyMain refreshed");
            }
        }
    }
}
