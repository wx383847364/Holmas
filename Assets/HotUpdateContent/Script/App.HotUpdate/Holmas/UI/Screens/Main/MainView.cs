using App.HotUpdate.Holmas.UI.Binding;
using App.HotUpdate.Holmas.UI.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace App.HotUpdate.Holmas.UI.Screens.Main
{
    /// <summary>
    /// MainPage 的 Unity 视图层。
    /// 负责搭建/补齐节点、绑定按钮事件、把 MainVm 渲染到屏幕上。
    /// </summary>
    public sealed class MainView : MonoBehaviour
    {
        private MainBindings _bindings;
        private UnityAction _currentStartAction;
        private UnityAction _currentPromotionAction;

        public void EnsureBindingSurface()
        {
            // 这一步的目标是“无论 prefab 完整与否，都补齐运行时绑定面”。
            gameObject.name = MainBindings.RootNodePath;

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
            // MainPanel 根节点是全屏的，但真正视觉内容通常挂在 BackgroundImage 下。
            // 这里把它拉成全屏内容宿主，避免主界面只剩中间一小块。
            RectTransform contentRoot = ResolveContentRoot(rootRect);
            collector.RegisterOrReplace(MainBindings.RootPanelKey, rootRect, nodePath: MainBindings.RootNodePath);

            // 运行时补的文案和按钮统一挂在 RuntimeOverlay 下，尽量不破坏 prefab 原有层级。
            RectTransform overlay = GetOrCreateOverlayRoot(contentRoot);
            TextMeshProUGUI levelText = ResolveLevelText(overlay);
            TextMeshProUGUI goldText = ResolveGoldText(overlay);
            TextMeshProUGUI summaryText = GetOrCreateRuntimeText(
                overlay,
                "SummaryText",
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -280f),
                new Vector2(-120f, 120f),
                34f,
                TextAlignmentOptions.TopLeft);
            TextMeshProUGUI statusText = GetOrCreateRuntimeText(
                overlay,
                "StatusText",
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 220f),
                new Vector2(-120f, 160f),
                28f,
                TextAlignmentOptions.TopLeft);
            Button startButton = GetOrCreateRuntimeButton(
                overlay,
                "StartButton",
                "开始找猫",
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 96f),
                new Vector2(320f, 108f),
                new Color(0.96f, 0.57f, 0.16f, 0.96f));
            Button promotionButton = ResolvePromotionButton(overlay);

            collector.RegisterOrReplace(MainBindings.LevelTextKey, levelText, nodePath: MainBindings.LevelTextNodePath);
            collector.RegisterOrReplace(MainBindings.GoldTextKey, goldText, nodePath: MainBindings.GoldTextNodePath);
            collector.RegisterOrReplace(MainBindings.SummaryTextKey, summaryText, nodePath: MainBindings.SummaryTextNodePath);
            collector.RegisterOrReplace(MainBindings.StatusTextKey, statusText, nodePath: MainBindings.StatusTextNodePath);
            collector.RegisterOrReplace(MainBindings.StartButtonKey, startButton, MainBindings.ButtonClickEvent, MainBindings.StartButtonNodePath);
            collector.RegisterOrReplace(MainBindings.PromotionButtonKey, promotionButton, MainBindings.ButtonClickEvent, MainBindings.PromotionButtonNodePath);
        }

        public void Bind(MainBindings bindings)
        {
            _bindings = bindings ?? new MainBindings();
        }

        public void SetStartAction(UnityAction action)
        {
            if (_bindings?.StartButton == null)
            {
                _currentStartAction = action;
                return;
            }

            if (_currentStartAction != null)
            {
                _bindings.StartButton.onClick.RemoveListener(_currentStartAction);
            }

            _currentStartAction = action;
            if (_currentStartAction != null)
            {
                _bindings.StartButton.onClick.AddListener(_currentStartAction);
            }
        }

        public void SetPromotionAction(UnityAction action)
        {
            if (_bindings?.PromotionButton == null)
            {
                _currentPromotionAction = action;
                return;
            }

            if (_currentPromotionAction != null)
            {
                _bindings.PromotionButton.onClick.RemoveListener(_currentPromotionAction);
            }

            _currentPromotionAction = action;
            if (_currentPromotionAction != null)
            {
                _bindings.PromotionButton.onClick.AddListener(_currentPromotionAction);
            }
        }

        public void Render(MainVm viewModel)
        {
            if (viewModel == null)
            {
                return;
            }

            // View 只做“把 ViewModel 写进控件”，不在这里生成业务状态。
            if (_bindings?.LevelText != null)
            {
                _bindings.LevelText.text = viewModel.LevelLabel ?? string.Empty;
            }

            if (_bindings?.GoldText != null)
            {
                _bindings.GoldText.text = viewModel.GoldLabel ?? string.Empty;
            }

            if (_bindings?.SummaryText != null)
            {
                RuntimeTmpFontResolver.EnsureFontSupportsText(_bindings.SummaryText, viewModel.Summary);
                _bindings.SummaryText.text = viewModel.Summary ?? string.Empty;
            }

            if (_bindings?.StatusText != null)
            {
                RuntimeTmpFontResolver.EnsureFontSupportsText(_bindings.StatusText, viewModel.Status);
                _bindings.StatusText.text = viewModel.Status ?? string.Empty;
            }

            if (_bindings?.StartButton != null)
            {
                _bindings.StartButton.interactable = viewModel.StartButtonEnabled;
                SetButtonLabel(_bindings.StartButton, viewModel.StartButtonLabel);
            }

            if (_bindings?.PromotionButton != null)
            {
                _bindings.PromotionButton.interactable = viewModel.PromotionButtonEnabled;
                SetButtonLabel(_bindings.PromotionButton, viewModel.PromotionButtonLabel);
            }
        }

        private void EnsureFallbackBackground()
        {
            if (transform.Find("BackgroundImage") != null)
            {
                return;
            }

            Image background = gameObject.GetComponent<Image>();
            if (background == null)
            {
                background = gameObject.AddComponent<Image>();
            }

            background.color = new Color(0.12f, 0.17f, 0.22f, 0.95f);
        }

        private RectTransform ResolveContentRoot(RectTransform rootRect)
        {
            RectTransform contentRoot = transform.Find("BackgroundImage") as RectTransform;
            if (contentRoot == null)
            {
                return rootRect;
            }

            // 老 prefab 里 BackgroundImage 是固定尺寸面板；这里把它修正成真正的全屏内容根。
            Stretch(contentRoot);
            Image background = contentRoot.GetComponent<Image>();
            if (background != null)
            {
                background.preserveAspect = false;
            }

            return contentRoot;
        }

        private RectTransform GetOrCreateOverlayRoot(RectTransform parent)
        {
            Transform existing = transform.Find(MainBindings.RuntimeOverlayNodeName);
            GameObject overlayObject = existing != null ? existing.gameObject : new GameObject(MainBindings.RuntimeOverlayNodeName, typeof(RectTransform));
            overlayObject.transform.SetParent(parent, false);

            RectTransform overlayRect = overlayObject.GetComponent<RectTransform>();
            Stretch(overlayRect);
            return overlayRect;
        }

        private TextMeshProUGUI ResolveLevelText(Transform overlay)
        {
            TextMeshProUGUI existing = FindDescendantComponent<TextMeshProUGUI>("BackgroundImage/Headicon_btn/Level")
                ?? FindFirstDescendantByName<TextMeshProUGUI>("Level");
            if (existing != null)
            {
                existing.alignment = TextAlignmentOptions.Center;
                existing.raycastTarget = false;
                return existing;
            }

            return GetOrCreateRuntimeText(
                overlay,
                "LevelText",
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(64f, -72f),
                new Vector2(180f, 72f),
                30f,
                TextAlignmentOptions.Left);
        }

        private TextMeshProUGUI ResolveGoldText(Transform overlay)
        {
            TextMeshProUGUI existing = FindDescendantComponent<TextMeshProUGUI>("BackgroundImage/Money_btn/Text (TMP)");
            if (existing != null)
            {
                existing.raycastTarget = false;
                return existing;
            }

            return GetOrCreateRuntimeText(
                overlay,
                "GoldText",
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-64f, -72f),
                new Vector2(240f, 72f),
                30f,
                TextAlignmentOptions.Right);
        }

        private Button ResolvePromotionButton(Transform overlay)
        {
            Button existing = FindDescendantComponent<Button>("BackgroundImage/Publicity_btn")
                ?? FindFirstDescendantByName<Button>("Publicity_btn");
            if (existing != null)
            {
                return existing;
            }

            return GetOrCreateRuntimeButton(
                overlay,
                "PromotionButton",
                "宣传升级",
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(-60f, 96f),
                new Vector2(280f, 96f),
                new Color(0.27f, 0.58f, 0.82f, 0.96f));
        }

        private TextMeshProUGUI GetOrCreateRuntimeText(
            Transform parent,
            string objectName,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta,
            float fontSize,
            TextAlignmentOptions alignment)
        {
            GameObject textObject = GetOrCreateChild(parent, objectName);
            RectTransform rectTransform = textObject.GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                rectTransform = textObject.AddComponent<RectTransform>();
            }

            ConfigureRect(rectTransform, anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta);

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            if (text == null)
            {
                text = textObject.AddComponent<TextMeshProUGUI>();
            }

            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = alignment;
            text.enableWordWrapping = true;
            text.raycastTarget = false;
            // 运行时创建的 TMP 文本默认会走项目默认字体，这里补中文字体兜底。
            RuntimeTmpFontResolver.EnsureFontSupportsText(text);
            return text;
        }

        private Button GetOrCreateRuntimeButton(
            Transform parent,
            string objectName,
            string labelText,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta,
            Color color)
        {
            GameObject buttonObject = GetOrCreateChild(parent, objectName);
            RectTransform rectTransform = buttonObject.GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                rectTransform = buttonObject.AddComponent<RectTransform>();
            }

            ConfigureRect(rectTransform, anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta);

            Image image = buttonObject.GetComponent<Image>();
            if (image == null)
            {
                image = buttonObject.AddComponent<Image>();
            }

            image.color = color;

            Button button = buttonObject.GetComponent<Button>();
            if (button == null)
            {
                button = buttonObject.AddComponent<Button>();
            }

            TextMeshProUGUI label = GetOrCreateRuntimeText(
                buttonObject.transform,
                "Label",
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero,
                34f,
                TextAlignmentOptions.Center);
            label.text = labelText;
            return button;
        }

        private static void SetButtonLabel(Button button, string labelText)
        {
            if (button == null)
            {
                return;
            }

            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
            {
                RuntimeTmpFontResolver.EnsureFontSupportsText(label, labelText);
                label.text = string.IsNullOrWhiteSpace(labelText) ? string.Empty : labelText;
            }
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
