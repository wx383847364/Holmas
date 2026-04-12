using App.HotUpdate.Holmas.UI.Binding;
using App.HotUpdate.Holmas.UI.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace App.HotUpdate.Holmas.UI.Screens.Loading
{
    /// <summary>
    /// LoadingPage / LoadingOverlay 共用的视图层。
    /// 它只负责展示状态文字和进度条，不负责真正的异步任务编排。
    /// </summary>
    public sealed class LoadingView : MonoBehaviour
    {
        private LoadingBindings _bindings;
        private bool _animate;
        private float _animationTime;

        private void Update()
        {
            // 当前 loading 动画只是视觉上的 ping-pong，不代表真实加载进度。
            if (!_animate || _bindings?.LoadingBar == null)
            {
                return;
            }

            _animationTime += Time.unscaledDeltaTime * 0.9f;
            _bindings.LoadingBar.value = Mathf.PingPong(_animationTime, 0.8f) + 0.1f;
        }

        public void EnsureBindingSurface()
        {
            // 和 MainView 一样，这里会在 prefab 不完整时自动补齐运行时绑定节点。
            gameObject.name = LoadingBindings.RootNodePath;

            UiReferenceCollector collector = gameObject.GetComponent<UiReferenceCollector>();
            if (collector == null)
            {
                collector = gameObject.AddComponent<UiReferenceCollector>();
            }

            RectTransform rootRect = gameObject.GetComponent<RectTransform>();
            if (rootRect == null)
            {
                rootRect = gameObject.AddComponent<RectTransform>();
            }

            Stretch(rootRect);
            EnsureFallbackBackground();
            collector.RegisterOrReplace(LoadingBindings.RootPanelKey, rootRect, nodePath: LoadingBindings.RootNodePath);

            RectTransform overlay = GetOrCreateOverlayRoot();
            Slider slider = ResolveSlider(overlay);
            TextMeshProUGUI statusText = ResolveStatusText(overlay);

            collector.RegisterOrReplace(LoadingBindings.LoadingBarKey, slider, nodePath: LoadingBindings.LoadingBarNodePath);
            collector.RegisterOrReplace(LoadingBindings.StatusTextKey, statusText, nodePath: LoadingBindings.StatusTextNodePath);
        }

        public void Bind(LoadingBindings bindings)
        {
            _bindings = bindings ?? new LoadingBindings();
        }

        public void Render(LoadingVm viewModel)
        {
            if (viewModel == null)
            {
                return;
            }

            // 文字和进度条都完全来自 LoadingVm。
            if (_bindings?.StatusText != null)
            {
                RuntimeTmpFontResolver.EnsureFontSupportsText(_bindings.StatusText, viewModel.Status);
                _bindings.StatusText.text = viewModel.Status ?? string.Empty;
            }

            if (_bindings?.LoadingBar != null)
            {
                _bindings.LoadingBar.value = Mathf.Clamp01(viewModel.Progress);
            }

            _animate = viewModel.Animate;
            if (!_animate)
            {
                _animationTime = 0f;
            }
        }

        private void EnsureFallbackBackground()
        {
            Image background = gameObject.GetComponent<Image>();
            if (background == null)
            {
                background = gameObject.AddComponent<Image>();
            }

            background.color = new Color(0.05f, 0.07f, 0.1f, 0.78f);
        }

        private RectTransform GetOrCreateOverlayRoot()
        {
            Transform existing = transform.Find(LoadingBindings.RuntimeOverlayNodeName);
            GameObject overlayObject = existing != null ? existing.gameObject : new GameObject(LoadingBindings.RuntimeOverlayNodeName, typeof(RectTransform));
            overlayObject.transform.SetParent(transform, false);

            RectTransform overlayRect = overlayObject.GetComponent<RectTransform>();
            Stretch(overlayRect);
            return overlayRect;
        }

        private Slider ResolveSlider(Transform overlay)
        {
            // 如果 prefab 已经有正式 Slider，就复用；没有再用代码补一个最小可用版本。
            Slider existing = FindDescendantComponent<Slider>("LoadingBar") ?? FindFirstDescendantByName<Slider>("LoadingBar");
            if (existing != null)
            {
                return existing;
            }

            GameObject sliderObject = GetOrCreateChild(overlay, "LoadingBar");
            RectTransform rectTransform = sliderObject.GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                rectTransform = sliderObject.AddComponent<RectTransform>();
            }

            ConfigureRect(
                rectTransform,
                new Vector2(0.2f, 0.35f),
                new Vector2(0.8f, 0.35f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(0f, 44f));

            Slider slider = sliderObject.GetComponent<Slider>();
            if (slider == null)
            {
                slider = sliderObject.AddComponent<Slider>();
            }

            GameObject backgroundObject = GetOrCreateChild(sliderObject.transform, "Background");
            Image background = backgroundObject.GetComponent<Image>() ?? backgroundObject.AddComponent<Image>();
            background.color = new Color(1f, 1f, 1f, 0.18f);
            Stretch(background.rectTransform);

            GameObject fillAreaObject = GetOrCreateChild(sliderObject.transform, "Fill Area");
            RectTransform fillArea = fillAreaObject.GetComponent<RectTransform>() ?? fillAreaObject.AddComponent<RectTransform>();
            Stretch(fillArea);

            GameObject fillObject = GetOrCreateChild(fillAreaObject.transform, "Fill");
            Image fill = fillObject.GetComponent<Image>() ?? fillObject.AddComponent<Image>();
            fill.color = new Color(0.97f, 0.63f, 0.22f, 1f);
            Stretch(fill.rectTransform);

            slider.targetGraphic = fill;
            slider.fillRect = fill.rectTransform;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            return slider;
        }

        private TextMeshProUGUI ResolveStatusText(Transform overlay)
        {
            TextMeshProUGUI statusText = FindDescendantComponent<TextMeshProUGUI>("StatusText");
            if (statusText != null)
            {
                return statusText;
            }

            GameObject textObject = GetOrCreateChild(overlay, "StatusText");
            RectTransform rectTransform = textObject.GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                rectTransform = textObject.AddComponent<RectTransform>();
            }

            ConfigureRect(
                rectTransform,
                new Vector2(0.16f, 0.42f),
                new Vector2(0.84f, 0.42f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 42f),
                new Vector2(0f, 72f));

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            if (text == null)
            {
                text = textObject.AddComponent<TextMeshProUGUI>();
            }

            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 32f;
            text.color = Color.white;
            text.raycastTarget = false;
            RuntimeTmpFontResolver.EnsureFontSupportsText(text);
            return text;
        }

        private T FindDescendantComponent<T>(string path) where T : Component
        {
            Transform target = transform.Find(path);
            return target != null ? target.GetComponent<T>() : null;
        }

        private T FindFirstDescendantByName<T>(string objectName) where T : Component
        {
            Transform[] all = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == objectName)
                {
                    T component = all[i].GetComponent<T>();
                    if (component != null)
                    {
                        return component;
                    }
                }
            }

            return null;
        }

        private static GameObject GetOrCreateChild(Transform parent, string objectName)
        {
            Transform child = parent.Find(objectName);
            if (child != null)
            {
                return child.gameObject;
            }

            var childObject = new GameObject(objectName);
            childObject.transform.SetParent(parent, false);
            return childObject;
        }

        private static void Stretch(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.localScale = Vector3.one;
        }

        private static void ConfigureRect(
            RectTransform rectTransform,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.pivot = pivot;
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = sizeDelta;
            rectTransform.localScale = Vector3.one;
        }
    }
}
