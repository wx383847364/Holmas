using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using App.HotUpdate.Holmas.Application;
using App.HotUpdate.Holmas.UI.Screens.AgencyMain;
using UnityEngine;
using UnityEngine.UI;

namespace App.HotUpdate.Holmas.UI.Core
{
    /// <summary>
    /// 常驻 UI 根节点，只负责 UI 容器、输入阻断层与默认 screen 注册。
    /// </summary>
    public sealed class UiRoot : MonoBehaviour
    {
        public const string AgencyMainScreenId = "agency.main";

        private HolmasApplicationContext _context;
        private IHolmasLevelLaunchGateway _levelLaunchGateway;
        private UiScreenService _screenService;
        private RectTransform _pageLayer;
        private RectTransform _popupLayer;
        private RectTransform _sheetLayer;
        private RectTransform _overlayLayer;
        private GameObject _inputBlocker;
        private bool _built;
        private bool _bootstrapStarted;

        public HolmasApplicationContext Context => _context;

        public IHolmasLevelLaunchGateway LevelLaunchGateway => _levelLaunchGateway;

        public UiScreenService ScreenService => _screenService;

        public RectTransform PageLayer => _pageLayer;

        public RectTransform PopupLayer => _popupLayer;

        public RectTransform SheetLayer => _sheetLayer;

        public RectTransform OverlayLayer => _overlayLayer;

        public void Initialize(HolmasApplicationContext context, IHolmasLevelLaunchGateway levelLaunchGateway)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _levelLaunchGateway = levelLaunchGateway ?? throw new ArgumentNullException(nameof(levelLaunchGateway));

            if (!_built)
            {
                BuildRoot();
                CreateScreenService();
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
            scaler.matchWidthOrHeight = 0.5f;

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
            _pageLayer = CreateLayer("PageLayer");
            _popupLayer = CreateLayer("PopupLayer");
            _sheetLayer = CreateLayer("SheetLayer");
            _overlayLayer = CreateLayer("OverlayLayer");
            _inputBlocker = CreateInputBlocker();
            SetInputBlocked(false);
        }

        private void CreateScreenService()
        {
            var navigationState = new UiNavigationState();
            var prefabLoader = new UiAssetsPrefabLoader(_context != null ? _context.AssetsRuntime : null);
            _screenService = new UiScreenService(this, prefabLoader, navigationState);
        }

        private void RegisterDefaultScreens()
        {
            var definitions = new List<UiScreenDefinition>
            {
                // TODO: 正式生成 prefab/address 接入后，改成最终产物地址。
                new UiScreenDefinition(
                    AgencyMainScreenId,
                    "Generated/Holmas/Portrait/AgencyMainPanel",
                    UiScreenKind.Page,
                    typeof(AgencyMainPageController))
                {
                    Layer = "Page",
                    CachePolicy = UiCachePolicy.KeepInstance,
                    BlockInputDuringTransition = true,
                    PreloadOnBootstrap = true,
                    Exclusive = true,
                },
            };

            for (int i = 0; i < definitions.Count; i++)
            {
                _screenService.RegisterDefinition(definitions[i]);
            }
        }

        private async Task BootstrapAsync()
        {
            try
            {
                foreach (UiScreenDefinition definition in _screenService.Definitions)
                {
                    if (definition != null && definition.PreloadOnBootstrap)
                    {
                        await _screenService.PreloadAsync(definition.Id);
                    }
                }

                if (!_screenService.IsOpen(AgencyMainScreenId))
                {
                    await _screenService.OpenPageAsync(AgencyMainScreenId);
                }
            }
            catch (Exception ex)
            {
                _context?.Logger?.LogWarning("UiRoot: 启动默认页面失败。{0}", ex.Message);
            }
        }

        private RectTransform CreateLayer(string name)
        {
            var layerObject = new GameObject(name);
            layerObject.transform.SetParent(transform, false);
            RectTransform rectTransform = layerObject.AddComponent<RectTransform>();
            Stretch(rectTransform);
            return rectTransform;
        }

        private GameObject CreateInputBlocker()
        {
            var blocker = new GameObject("InputBlocker");
            blocker.transform.SetParent(transform, false);
            RectTransform rectTransform = blocker.AddComponent<RectTransform>();
            Stretch(rectTransform);

            var image = blocker.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.01f);
            image.raycastTarget = true;
            blocker.transform.SetAsLastSibling();
            return blocker;
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
