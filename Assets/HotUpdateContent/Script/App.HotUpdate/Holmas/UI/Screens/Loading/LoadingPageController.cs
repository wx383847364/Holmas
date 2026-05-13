using App.HotUpdate.Holmas.UI.Core;

namespace App.HotUpdate.Holmas.UI.Screens.Loading
{
    public sealed class LoadingPageController : UiPageController
    {
        private LoadingView _view;
        private LoadingBindings _bindings;

        protected override void OnCreate()
        {
            _view = RootObject != null ? RootObject.GetComponent<LoadingView>() : null;
            if (_view == null)
            {
                throw new System.InvalidOperationException("LoadingPanel prefab 缺少 LoadingView，请在 prefab 静态挂载。");
            }
        }

        protected override void OnBind()
        {
            _bindings = LoadingBindings.Resolve(BindingResolver);
            if (_bindings == null || !_bindings.HasRequiredBindings)
            {
                throw new System.InvalidOperationException("LoadingPanel 缺少完整 UiReferenceCollector 静态绑定，请先在 prefab 侧补齐 LoadingGeneratedBindings.Manifest 对应节点。");
            }

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
