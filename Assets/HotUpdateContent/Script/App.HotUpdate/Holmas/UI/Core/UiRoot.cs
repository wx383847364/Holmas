using System;
using System.Threading.Tasks;
using App.HotUpdate.Holmas.Application;
using App.HotUpdate.Holmas.UI;
using App.HotUpdate.Holmas.UI.Screens.GmTool;
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
        private IAssetHandle _projectFontHandle;
        private IAssetHandle _fontRuntimeSettingsHandle;
        private Action _popupBackdropClickAction;
        private bool _built;
        private bool _bootstrapStarted;
        private bool _gmToggleInProgress;
        private GmGestureRecognizer _gmGestureRecognizer;

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

        private static bool IsGmDebugEnabled => UnityEngine.Application.isEditor || Debug.isDebugBuild;

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

            if (_gmGestureRecognizer == null)
            {
                _gmGestureRecognizer = new GmGestureRecognizer();
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

        public async Task ToggleGmToolAsync(GmToggleRequestSource source)
        {
            if (!IsGmDebugEnabled || _screenService == null || _gmToggleInProgress)
            {
                _context?.Logger?.LogInfo(
                    "UiRoot: 忽略 GM 切换请求。source={0}, debugEnabled={1}, screenServiceReady={2}, toggleInProgress={3}",
                    source,
                    IsGmDebugEnabled,
                    _screenService != null,
                    _gmToggleInProgress);
                return;
            }

            _gmToggleInProgress = true;
            try
            {
                bool isOpen = _screenService.IsOpen(GmToolScreenRegistration.ScreenId);
                _context?.Logger?.LogInfo(
                    "UiRoot: 开始切换 GM 工具。source={0}, currentlyOpen={1}",
                    source,
                    isOpen);

                if (isOpen)
                {
                    await _screenService.CloseAsync(GmToolScreenRegistration.ScreenId);
                    _context?.Logger?.LogInfo("UiRoot: GM 工具已关闭。source={0}", source);
                }
                else
                {
                    await _screenService.OpenPopupAsync(GmToolScreenRegistration.ScreenId, source);
                    _context?.Logger?.LogInfo("UiRoot: GM 工具已请求打开。source={0}", source);
                }
            }
            catch (Exception ex)
            {
                _context?.Logger?.LogError("UiRoot: 切换 GM 工具失败。source={0}, error={1}", source, ex);
                throw;
            }
            finally
            {
                _gmToggleInProgress = false;
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
                await PreloadProjectChineseFontAsync();

                // 只先确保 Loading 自身可用；其他启动预加载必须在 Loading 可见后执行。
                foreach (UiScreenDefinition definition in _screenService.Definitions)
                {
                    if (definition != null &&
                        definition.PreloadOnBootstrap &&
                        definition.Id == HolmasUiScreenCatalog.DefaultStartupScreenId)
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

        private async Task PreloadProjectChineseFontAsync()
        {
            if (_context == null || _context.AssetsRuntime == null || _projectFontHandle != null || _fontRuntimeSettingsHandle != null)
            {
                return;
            }

            IAssetHandle settingsHandle = await _context.AssetsRuntime.LoadAssetAsync(HolmasFontRuntimeSettings.DefaultAssetPath);
            if (settingsHandle?.AssetObject is HolmasFontRuntimeSettings settings &&
                settings.ResolvePreferredProjectFont() != null)
            {
                _fontRuntimeSettingsHandle = settingsHandle;
                RuntimeTmpFontResolver.SetRuntimeSettings(settings);
                _context.Logger?.LogInfo("UiRoot: 项目字体运行时设置已从资源加载。{0}", HolmasFontRuntimeSettings.DefaultAssetPath);
                return;
            }

            settingsHandle?.Release();

            IAssetHandle fontHandle = await _context.AssetsRuntime.LoadAssetAsync(RuntimeTmpFontResolver.ProjectChineseFontAssetPath);
            if (fontHandle?.AssetObject is Font font)
            {
                _projectFontHandle = fontHandle;
                RuntimeTmpFontResolver.SetProjectChineseFont(font);
                _context.Logger?.LogInfo("UiRoot: 项目中文字体已从热更资源加载。{0}", RuntimeTmpFontResolver.ProjectChineseFontAssetPath);
                return;
            }

            fontHandle?.Release();
            _context.Logger?.LogWarning("UiRoot: 项目中文字体热更资源加载失败。{0}", RuntimeTmpFontResolver.ProjectChineseFontAssetPath);
        }

        private void ConfigureSafeAreaRuntime()
        {
            // 安全区信息来自 AOT 的平台桥接；UI Core 本身不直接依赖具体平台实现。
            IWeChatBridge weChatBridge = _context?.ServiceContainer != null
                ? _context.ServiceContainer.Get<IWeChatBridge>()
                : null;
            UiSafeAreaRuntime.Configure(weChatBridge);
        }

        private void Update()
        {
            if (!IsGmDebugEnabled || _screenService == null || _gmGestureRecognizer == null)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.BackQuote))
            {
                _context?.Logger?.LogInfo("UiRoot: 侦测到 BackQuote，准备切换 GM 工具。");
                _ = ToggleGmToolAsync(GmToggleRequestSource.Keyboard);
            }

            HandleGestureToggleInput();
        }

        private void HandleGestureToggleInput()
        {
            float now = Time.unscaledTime;
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                switch (touch.phase)
                {
                    case TouchPhase.Began:
                        _gmGestureRecognizer.BeginStroke(now, touch.position);
                        break;
                    case TouchPhase.Moved:
                    case TouchPhase.Stationary:
                        _gmGestureRecognizer.AppendPoint(now, touch.position);
                        break;
                    case TouchPhase.Ended:
                    case TouchPhase.Canceled:
                        if (_gmGestureRecognizer.EndStroke(now, touch.position))
                        {
                            _context?.Logger?.LogInfo("UiRoot: 侦测到双五角星触摸手势，准备切换 GM 工具。");
                            _ = ToggleGmToolAsync(GmToggleRequestSource.Gesture);
                        }

                        break;
                }

                return;
            }

            Vector2 mousePosition = Input.mousePosition;
            if (Input.GetMouseButtonDown(0))
            {
                _gmGestureRecognizer.BeginStroke(now, mousePosition);
            }
            else if (Input.GetMouseButton(0))
            {
                _gmGestureRecognizer.AppendPoint(now, mousePosition);
            }
            else if (Input.GetMouseButtonUp(0) && _gmGestureRecognizer.EndStroke(now, mousePosition))
            {
                _context?.Logger?.LogInfo("UiRoot: 侦测到双五角星鼠标手势，准备切换 GM 工具。");
                _ = ToggleGmToolAsync(GmToggleRequestSource.Gesture);
            }
        }

        private void OnDestroy()
        {
            _fontRuntimeSettingsHandle?.Release();
            _fontRuntimeSettingsHandle = null;
            _projectFontHandle?.Release();
            _projectFontHandle = null;
            RuntimeTmpFontResolver.SetRuntimeSettings(null);
            RuntimeTmpFontResolver.SetProjectChineseFont(null);
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
