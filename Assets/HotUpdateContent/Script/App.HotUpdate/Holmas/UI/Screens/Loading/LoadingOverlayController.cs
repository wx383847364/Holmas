using App.HotUpdate.Holmas.UI.Core;

namespace App.HotUpdate.Holmas.UI.Screens.Loading
{
    public sealed class LoadingOverlayController : UiOverlayController
    {
        private LoadingView _view;
        private LoadingBindings _bindings;

        protected override void OnCreate()
        {
            _view = RootObject != null ? RootObject.GetComponent<LoadingView>() : null;
            if (_view == null && RootObject != null)
            {
                _view = RootObject.AddComponent<LoadingView>();
            }

            _view?.EnsureBindingSurface();
        }

        protected override void OnBind()
        {
            _bindings = LoadingBindings.Resolve(BindingResolver);
            _view?.Bind(_bindings);
        }

        protected override void OnOpen(object payload)
        {
            LoadingVm viewModel = payload as LoadingVm ?? new LoadingVm
            {
                Status = payload as string ?? "Loading..."
            };
            _view?.Render(viewModel);
        }

        protected override void OnClose()
        {
            _view?.Render(new LoadingVm
            {
                Status = string.Empty,
                Progress = 0f,
                Animate = false,
            });
        }
    }
}
