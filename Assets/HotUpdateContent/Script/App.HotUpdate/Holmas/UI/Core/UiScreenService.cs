using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace App.HotUpdate.Holmas.UI.Core
{
    public sealed class UiScreenService
    {
        private readonly Dictionary<string, UiScreenDefinition> _definitions = new Dictionary<string, UiScreenDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, UiScreenRuntime> _runtimes = new Dictionary<string, UiScreenRuntime>(StringComparer.Ordinal);
        private readonly UiRoot _root;
        private readonly IUiPrefabLoader _prefabLoader;
        private readonly UiNavigationState _navigationState;

        public UiScreenService(UiRoot root, IUiPrefabLoader prefabLoader, UiNavigationState navigationState)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            _prefabLoader = prefabLoader ?? throw new ArgumentNullException(nameof(prefabLoader));
            _navigationState = navigationState ?? throw new ArgumentNullException(nameof(navigationState));
        }

        public UiNavigationState NavigationState => _navigationState;

        public IReadOnlyCollection<UiScreenDefinition> Definitions => _definitions.Values;

        public void RegisterDefinition(UiScreenDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            _definitions[definition.Id] = definition;
        }

        public async Task<UiPageController> OpenPageAsync(string screenId, object payload = null)
        {
            UiScreenRuntime runtime = await OpenScreenInternalAsync(screenId, UiScreenKind.Page, payload);
            return runtime != null ? runtime.Controller as UiPageController : null;
        }

        public async Task<UiPopupController> OpenPopupAsync(string screenId, object payload = null)
        {
            UiScreenRuntime runtime = await OpenScreenInternalAsync(screenId, UiScreenKind.Popup, payload);
            return runtime != null ? runtime.Controller as UiPopupController : null;
        }

        public async Task<UiSheetController> OpenSheetAsync(string screenId, object payload = null)
        {
            UiScreenRuntime runtime = await OpenScreenInternalAsync(screenId, UiScreenKind.Sheet, payload);
            return runtime != null ? runtime.Controller as UiSheetController : null;
        }

        public async Task<UiOverlayController> ShowOverlayAsync(string screenId, object payload = null)
        {
            UiScreenRuntime runtime = await OpenScreenInternalAsync(screenId, UiScreenKind.Overlay, payload);
            return runtime != null ? runtime.Controller as UiOverlayController : null;
        }

        public async Task BackAsync()
        {
            if (_navigationState.TopPopup != null)
            {
                await CloseTopPopupAsync();
                return;
            }

            UiPageController currentPage = _navigationState.CurrentPage;
            if (currentPage == null)
            {
                return;
            }

            await CloseAsync(currentPage.Definition.Id);
        }

        public Task CloseTopPopupAsync()
        {
            UiPopupController popup = _navigationState.TopPopup;
            return popup == null ? Task.CompletedTask : CloseAsync(popup.Definition.Id);
        }

        public Task CloseAsync(string screenId)
        {
            if (!_runtimes.TryGetValue(screenId, out UiScreenRuntime runtime))
            {
                return Task.CompletedTask;
            }

            return CloseRuntimeAsync(runtime);
        }

        public async Task PreloadAsync(string screenId)
        {
            UiScreenDefinition definition = GetDefinition(screenId);
            await GetOrCreateRuntimeAsync(definition);
        }

        public bool IsOpen(string screenId)
        {
            return _runtimes.TryGetValue(screenId, out UiScreenRuntime runtime) && runtime.IsOpen;
        }

        private async Task<UiScreenRuntime> OpenScreenInternalAsync(string screenId, UiScreenKind expectedKind, object payload)
        {
            UiScreenDefinition definition = GetDefinition(screenId);
            if (definition.Kind != expectedKind)
            {
                throw new InvalidOperationException(
                    $"UiScreenService: {screenId} 的声明类型是 {definition.Kind}，不能按 {expectedKind} 打开。");
            }

            bool shouldLockInput = definition.BlockInputDuringTransition || definition.Kind == UiScreenKind.Page;
            if (shouldLockInput)
            {
                AcquireInputLock();
            }

            try
            {
                UiScreenRuntime runtime = await GetOrCreateRuntimeAsync(definition);

                switch (expectedKind)
                {
                    case UiScreenKind.Page:
                        await OpenPageRuntimeAsync(runtime, payload);
                        break;
                    case UiScreenKind.Popup:
                        OpenPopupRuntime(runtime, payload);
                        break;
                    case UiScreenKind.Sheet:
                        await OpenSheetRuntimeAsync(runtime, payload);
                        break;
                    case UiScreenKind.Overlay:
                        OpenOverlayRuntime(runtime, payload);
                        break;
                }

                return runtime;
            }
            finally
            {
                if (shouldLockInput)
                {
                    ReleaseInputLock();
                }
            }
        }

        private async Task<UiScreenRuntime> GetOrCreateRuntimeAsync(UiScreenDefinition definition)
        {
            if (_runtimes.TryGetValue(definition.Id, out UiScreenRuntime runtime))
            {
                return runtime;
            }

            UiLoadedPrefabHandle loadedHandle = await _prefabLoader.LoadAsync(definition.AssetAddress);
            if (loadedHandle == null)
            {
                throw new InvalidOperationException($"UiScreenService: 无法加载界面 {definition.Id}。");
            }

            loadedHandle.SetParent(ResolveLayer(definition.Kind));
            loadedHandle.SetActive(false);

            UiScreenController controller = CreateController(definition);
            controller.Attach(this, _root, loadedHandle, definition);

            runtime = new UiScreenRuntime(definition, controller, loadedHandle);
            _runtimes[definition.Id] = runtime;
            return runtime;
        }

        private async Task OpenPageRuntimeAsync(UiScreenRuntime runtime, object payload)
        {
            UiPageController currentPage = _navigationState.CurrentPage;
            if (currentPage != null && !ReferenceEquals(currentPage, runtime.Controller))
            {
                currentPage.PauseInternal();
            }

            runtime.BringToFront();
            runtime.Open(payload);
            _navigationState.PushPage(runtime.Controller as UiPageController);
            await Task.CompletedTask;
        }

        private void OpenPopupRuntime(UiScreenRuntime runtime, object payload)
        {
            runtime.BringToFront();
            runtime.Open(payload);
            _navigationState.PushPopup(runtime.Controller as UiPopupController);
        }

        private async Task OpenSheetRuntimeAsync(UiScreenRuntime runtime, object payload)
        {
            string groupId = runtime.Definition.SheetGroupId;
            if (string.IsNullOrWhiteSpace(groupId))
            {
                throw new InvalidOperationException($"UiScreenService: Sheet {runtime.Definition.Id} 缺少 SheetGroupId。");
            }

            UiSheetController currentSheet = _navigationState.GetActiveSheet(groupId);
            if (currentSheet != null && !ReferenceEquals(currentSheet, runtime.Controller))
            {
                await CloseAsync(currentSheet.Definition.Id);
            }

            runtime.BringToFront();
            runtime.Open(payload);
            _navigationState.SetActiveSheet(groupId, runtime.Controller as UiSheetController);
        }

        private void OpenOverlayRuntime(UiScreenRuntime runtime, object payload)
        {
            runtime.BringToFront();
            runtime.Open(payload);
        }

        private Task CloseRuntimeAsync(UiScreenRuntime runtime)
        {
            if (runtime == null)
            {
                return Task.CompletedTask;
            }

            bool wasCurrentPage = runtime.Definition.Kind == UiScreenKind.Page
                && ReferenceEquals(_navigationState.CurrentPage, runtime.Controller);

            runtime.Close();

            switch (runtime.Definition.Kind)
            {
                case UiScreenKind.Page:
                    _navigationState.RemovePage(runtime.Controller as UiPageController);
                    break;
                case UiScreenKind.Popup:
                    _navigationState.RemovePopup(runtime.Controller as UiPopupController);
                    break;
                case UiScreenKind.Sheet:
                    _navigationState.RemoveSheet(runtime.Controller as UiSheetController);
                    break;
            }

            if (runtime.Definition.CachePolicy == UiCachePolicy.DestroyOnClose)
            {
                DestroyRuntime(runtime);
            }

            if (wasCurrentPage)
            {
                UiPageController previousPage = _navigationState.CurrentPage;
                if (previousPage != null)
                {
                    previousPage.ResumeInternal();
                }
            }

            return Task.CompletedTask;
        }

        private void DestroyRuntime(UiScreenRuntime runtime)
        {
            if (runtime == null)
            {
                return;
            }

            _runtimes.Remove(runtime.Definition.Id);
            runtime.Destroy();
            _prefabLoader.Release(runtime.LoadedHandle);
        }

        private UiScreenDefinition GetDefinition(string screenId)
        {
            if (string.IsNullOrWhiteSpace(screenId))
            {
                throw new ArgumentException("UiScreenService: screenId 不能为空。", nameof(screenId));
            }

            if (!_definitions.TryGetValue(screenId, out UiScreenDefinition definition))
            {
                throw new KeyNotFoundException($"UiScreenService: 未注册界面 {screenId}。");
            }

            return definition;
        }

        private UiScreenController CreateController(UiScreenDefinition definition)
        {
            if (!typeof(UiScreenController).IsAssignableFrom(definition.ControllerType))
            {
                throw new InvalidOperationException(
                    $"UiScreenService: {definition.ControllerType.Name} 不是 UiScreenController。");
            }

            return Activator.CreateInstance(definition.ControllerType) as UiScreenController;
        }

        private Transform ResolveLayer(UiScreenKind kind)
        {
            switch (kind)
            {
                case UiScreenKind.Page:
                    return _root.PageLayer;
                case UiScreenKind.Popup:
                    return _root.PopupLayer;
                case UiScreenKind.Sheet:
                    return _root.SheetLayer;
                case UiScreenKind.Overlay:
                    return _root.OverlayLayer;
                default:
                    return _root.transform;
            }
        }

        private void AcquireInputLock()
        {
            _navigationState.AcquireInputLock();
            _root.SetInputBlocked(_navigationState.IsInputLocked);
        }

        private void ReleaseInputLock()
        {
            _navigationState.ReleaseInputLock();
            _root.SetInputBlocked(_navigationState.IsInputLocked);
        }

        private sealed class UiScreenRuntime
        {
            public UiScreenRuntime(UiScreenDefinition definition, UiScreenController controller, UiLoadedPrefabHandle loadedHandle)
            {
                Definition = definition;
                Controller = controller;
                LoadedHandle = loadedHandle;
            }

            public UiScreenDefinition Definition { get; }

            public UiScreenController Controller { get; }

            public UiLoadedPrefabHandle LoadedHandle { get; }

            public bool IsOpen { get; private set; }

            public void BringToFront()
            {
                if (LoadedHandle?.InstanceRoot != null)
                {
                    LoadedHandle.InstanceRoot.transform.SetAsLastSibling();
                }
            }

            public void Open(object payload)
            {
                IsOpen = true;
                Controller.OpenInternal(payload);
            }

            public void Close()
            {
                IsOpen = false;
                Controller.CloseInternal();
            }

            public void Destroy()
            {
                IsOpen = false;
                Controller.DestroyInternal();
            }
        }
    }
}
