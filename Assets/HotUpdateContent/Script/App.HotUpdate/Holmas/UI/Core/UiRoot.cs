using System;
using System.Threading.Tasks;
using App.HotUpdate.Holmas.Application;
using App.HotUpdate.Holmas.UI;
using App.Shared.Contracts;
using UnityEngine;
using UnityEngine.UI;

namespace App.HotUpdate.Holmas.UI.Core
{
    /// <summary>
    /// 常驻 UI 根节点，只负责 UI 容器、输入阻断层与默认 screen 注册。
    /// </summary>
    public sealed class UiRoot : MonoBehaviour
    {
        private HolmasApplicationContext _context;
        private IHolmasLevelLaunchGateway _levelLaunchGateway;
        private UiScreenService _screenService;
        private IBattleWorldHost _battleWorldHost;
        private HolmasFlowCoordinator _flowCoordinator;
        private RectTransform _unsafeBackgroundLayer;
        private RectTransform _safeAreaRoot;
        private RectTransform _pageLayer;
        private RectTransform _popupLayer;
        private RectTransform _sheetLayer;
        private RectTransform _overlayLayer;
        private RectTransform _debugLayer;
        private GameObject _inputBlocker;
        private GameObject _popupBackdrop;
        private Button _popupBackdropButton;
        private Action _popupBackdropClickAction;
        private bool _built;
        private bool _bootstrapStarted;

        public HolmasApplicationContext Context => _context;

        public IHolmasLevelLaunchGateway LevelLaunchGateway => _levelLaunchGateway;

        public UiScreenService ScreenService => _screenService;

        public HolmasFlowCoordinator FlowCoordinator => _flowCoordinator;

        public RectTransform UnsafeBackgroundLayer => _unsafeBackgroundLayer;

        public RectTransform SafeAreaRoot => _safeAreaRoot;

        public RectTransform PageLayer => _pageLayer;

        public RectTransform PopupLayer => _popupLayer;

        public RectTransform SheetLayer => _sheetLayer;

        public RectTransform OverlayLayer => _overlayLayer;

        public RectTransform DebugLayer => _debugLayer;

        public void Initialize(HolmasApplicationContext context, IHolmasLevelLaunchGateway levelLaunchGateway)
        {
            // UiRoot 只做一次真实搭建；之后再次初始化只更新上下文引用，不重复造层级。
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _levelLaunchGateway = levelLaunchGateway ?? throw new ArgumentNullException(nameof(levelLaunchGateway));
            ConfigureSafeAreaRuntime();

            if (!_built)
            {
                BuildRoot();
                CreateScreenService();
                CreateFlowCoordinator();
                RegisterDefaultScreens();
                _built = true;
            }

            if (!_bootstrapStarted)
            {
                _bootstrapStarted = true;
                _ = BootstrapAsync();
            }
        }

        public void SetInputBlocked(bool isBlocked)
        {
            if (_inputBlocker != null)
            {
                _inputBlocker.SetActive(isBlocked);
            }
        }

        private void BuildRoot()
        {
            // UiRoot 自己就是最外层 Canvas。下面再拆 page / popup / overlay 等固定层级。
            Canvas canvas = gameObject.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
            }

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 3000;

            CanvasScaler scaler = gameObject.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = gameObject.AddComponent<CanvasScaler>();
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            if (gameObject.GetComponent<GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }

            RectTransform rootRect = gameObject.GetComponent<RectTransform>();
            if (rootRect == null)
            {
                rootRect = gameObject.AddComponent<RectTransform>();
            }

            Stretch(rootRect);
            // UnsafeBackgroundLayer 用来放不受安全区约束的全屏背景；
            // 其余 page / sheet / overlay 默认挂在安全区内部。
            _unsafeBackgroundLayer = CreateLayer("UnsafeBackgroundLayer", rootRect);
            _safeAreaRoot = CreateLayer("SafeAreaRoot", rootRect);
            if (_safeAreaRoot.gameObject.GetComponent<UiSafeAreaFitter>() == null)
            {
                _safeAreaRoot.gameObject.AddComponent<UiSafeAreaFitter>();
            }

            _pageLayer = CreateLayer("PageLayer", _safeAreaRoot);
            _popupBackdrop = CreatePopupBackdrop(rootRect);
            _popupLayer = CreateLayer("PopupLayer", rootRect);
            if (_popupLayer.gameObject.GetComponent<UiSafeAreaFitter>() == null)
            {
                _popupLayer.gameObject.AddComponent<UiSafeAreaFitter>();
            }
            _sheetLayer = CreateLayer("SheetLayer", _safeAreaRoot);
            _overlayLayer = CreateLayer("OverlayLayer", _safeAreaRoot);
            _debugLayer = CreateLayer("DebugLayer", rootRect);
            _inputBlocker = CreateInputBlocker(rootRect);
            BringPopupModalLayersToFront();
            SetInputBlocked(false);
        }

        private void CreateScreenService()
        {
            // ScreenService 是“界面定义注册 + 运行时实例缓存 + 导航状态”的统一入口。
            var navigationState = new UiNavigationState();
            var prefabLoader = new UiAssetsPrefabLoader(_context != null ? _context.AssetsRuntime : null);
            _screenService = new UiScreenService(this, prefabLoader, navigationState);
        }

        private void CreateFlowCoordinator()
        {
            if (_battleWorldHost == null)
            {
                _battleWorldHost = new HolmasBattleWorldHost();
            }

            if (_flowCoordinator == null)
            {
                _flowCoordinator = new HolmasFlowCoordinator(this, _battleWorldHost);
            }
        }

        private void RegisterDefaultScreens()
        {
            // 真正有哪些 page / popup / overlay 可打开，由 catalog 统一注册。
            HolmasUiScreenCatalog.RegisterAll(_screenService);
        }

        private async Task BootstrapAsync()
        {
            try
            {
                // 先预热声明为 PreloadOnBootstrap 的界面，再进入正式启动流程。
                foreach (UiScreenDefinition definition in _screenService.Definitions)
                {
                    if (definition != null && definition.PreloadOnBootstrap)
                    {
                        await _screenService.PreloadAsync(definition.Id);
                    }
                }

                if (_flowCoordinator != null)
                {
                    await _flowCoordinator.EnterStartupAsync();
                }
                else
                {
                    string startupScreenId = HolmasUiScreenCatalog.DefaultStartupScreenId;
                    if (!_screenService.IsOpen(startupScreenId))
                    {
                        await _screenService.OpenPageAsync(startupScreenId);
                    }
                }
            }
            catch (Exception ex)
            {
                _context?.Logger?.LogWarning("UiRoot: 启动默认页面失败。{0}", ex.Message);
            }
        }

        private void ConfigureSafeAreaRuntime()
        {
            // 安全区信息来自 AOT 的平台桥接；UI Core 本身不直接依赖具体平台实现。
            IWeChatBridge weChatBridge = _context?.ServiceContainer != null
                ? _context.ServiceContainer.Get<IWeChatBridge>()
                : null;
            UiSafeAreaRuntime.Configure(weChatBridge);
        }

        private RectTransform CreateLayer(string name, Transform parent)
        {
            // 每层都是一个全屏 RectTransform，具体布局交给各自页面 prefab / view 处理。
            var layerObject = new GameObject(name);
            layerObject.transform.SetParent(parent, false);
            RectTransform rectTransform = layerObject.AddComponent<RectTransform>();
            Stretch(rectTransform);
            return rectTransform;
        }

        private GameObject CreateInputBlocker(Transform parent)
        {
            var blocker = new GameObject("InputBlocker");
            blocker.transform.SetParent(parent, false);
            RectTransform rectTransform = blocker.AddComponent<RectTransform>();
            Stretch(rectTransform);

            var image = blocker.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.01f);
            image.raycastTarget = true;
            blocker.transform.SetAsLastSibling();
            return blocker;
        }

        public void ConfigurePopupBackdrop(bool isVisible, bool clickOutsideToClose, Action onClick)
        {
            // Popup 背板是否响应点击关闭，由具体 popup 定义决定。
            _popupBackdropClickAction = clickOutsideToClose ? onClick : null;
            if (_popupBackdropButton != null)
            {
                _popupBackdropButton.interactable = clickOutsideToClose;
            }

            if (_popupBackdrop != null)
            {
                _popupBackdrop.SetActive(isVisible);
                BringPopupModalLayersToFront();
            }
        }

        private GameObject CreatePopupBackdrop(Transform parent)
        {
            var backdrop = new GameObject("PopupBackdrop");
            backdrop.transform.SetParent(parent, false);
            RectTransform rectTransform = backdrop.AddComponent<RectTransform>();
            Stretch(rectTransform);

            var image = backdrop.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.45f);
            image.raycastTarget = true;

            _popupBackdropButton = backdrop.AddComponent<Button>();
            _popupBackdropButton.transition = Selectable.Transition.None;
            _popupBackdropButton.onClick.AddListener(HandlePopupBackdropClicked);
            backdrop.SetActive(false);
            return backdrop;
        }

        private void HandlePopupBackdropClicked()
        {
            _popupBackdropClickAction?.Invoke();
        }

        private void BringPopupModalLayersToFront()
        {
            if (_popupBackdrop != null)
            {
                _popupBackdrop.transform.SetAsLastSibling();
            }

            if (_popupLayer != null)
            {
                _popupLayer.transform.SetAsLastSibling();
            }

            if (_inputBlocker != null)
            {
                _inputBlocker.transform.SetAsLastSibling();
            }
        }

        private static void Stretch(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.localScale = Vector3.one;
        }
    }
}
