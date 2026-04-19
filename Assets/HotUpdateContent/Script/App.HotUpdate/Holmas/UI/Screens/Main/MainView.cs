using System;
using System.Collections.Generic;
using App.HotUpdate.Holmas.UI.Binding;
using App.HotUpdate.Holmas.UI.Core;
using App.HotUpdate.Holmas.UI.Tool;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace App.HotUpdate.Holmas.UI.Screens.Main
{
    public sealed class MainView : MonoBehaviour
    {
        private static readonly string[] TaskSlotNodeNames =
        {
            "Task1",
            "Task2",
            "Task3",
            "Task5",
        };

        private readonly List<MainTaskSlotView> _taskSlotViews = new List<MainTaskSlotView>();

        private MainBindings _bindings;
        private UnityAction _currentStartAction;
        private UnityAction _currentPromotionAction;
        private Action<int> _currentTaskSlotAction;

        public void EnsureBindingSurface()
        {
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
            collector.RegisterOrReplace(MainBindings.RootPanelKey, rootRect, nodePath: MainBindings.RootNodePath);

            RectTransform overlay = GetOrCreateOverlayRoot();
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
            collector.RegisterOrReplace(
                MainBindings.StartButtonKey,
                startButton,
                MainBindings.ButtonClickEvent,
                MainBindings.StartButtonNodePath);
            collector.RegisterOrReplace(
                MainBindings.PromotionButtonKey,
                promotionButton,
                MainBindings.ButtonClickEvent,
                MainBindings.PromotionButtonNodePath);

            EnsureTaskSlotViews();
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

        public void SetTaskSlotAction(Action<int> action)
        {
            _currentTaskSlotAction = action;
        }

        public void Render(MainVm viewModel)
        {
            if (viewModel == null)
            {
                return;
            }

            if (_bindings?.LevelText != null)
            {
                TmpGlyphCoverageReporter.SetText(_bindings.LevelText, viewModel.LevelLabel);
            }

            if (_bindings?.GoldText != null)
            {
                TmpGlyphCoverageReporter.SetText(_bindings.GoldText, viewModel.GoldLabel);
            }

            if (_bindings?.SummaryText != null)
            {
                TmpGlyphCoverageReporter.SetText(_bindings.SummaryText, viewModel.Summary);
            }

            if (_bindings?.StatusText != null)
            {
                TmpGlyphCoverageReporter.SetText(_bindings.StatusText, viewModel.Status);
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

            RenderTaskSlots(viewModel.TaskItems ?? Array.Empty<MainTaskItemVm>());
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

        private void EnsureTaskSlotViews()
        {
            if (_taskSlotViews.Count > 0)
            {
                return;
            }

            for (int i = 0; i < TaskSlotNodeNames.Length; i++)
            {
                Transform slotTransform = transform.Find(TaskSlotNodeNames[i]) ?? FindFirstTransformByName(TaskSlotNodeNames[i]);
                if (slotTransform == null)
                {
                    continue;
                }

                RectTransform slotRoot = slotTransform as RectTransform;
                if (slotRoot == null)
                {
                    continue;
                }

                Image background = slotRoot.GetComponent<Image>();
                if (background == null)
                {
                    background = slotRoot.gameObject.AddComponent<Image>();
                }

                background.enabled = true;
                Button button = slotRoot.GetComponent<Button>();
                if (button == null)
                {
                    button = slotRoot.gameObject.AddComponent<Button>();
                }

                int slotIndex = i;
                button.onClick.AddListener(() => _currentTaskSlotAction?.Invoke(slotIndex));

                var view = new MainTaskSlotView
                {
                    SlotIndex = slotIndex,
                    Root = slotRoot,
                    Background = background,
                    Button = button,
                    ProgressLabel = FindFirstDescendantByName<TextMeshProUGUI>(slotRoot, "Count")
                                    ?? FindFirstDescendantByName<TMP_Text>(slotRoot, "Count")
                                    ?? GetOrCreateRuntimeText(
                                        slotRoot,
                                        "RuntimeTaskProgress",
                                        new Vector2(0.18f, 0.5f),
                                        new Vector2(0.82f, 0.5f),
                                        new Vector2(0.5f, 0.5f),
                                        new Vector2(0f, 8f),
                                        new Vector2(0f, 28f),
                                        20f,
                                        TextAlignmentOptions.Center),
                    ProgressSlider = FindFirstDescendantByName<Slider>(slotRoot, "Slider"),
                    RewardIcon = FindFirstDescendantByName<Image>(slotRoot, "RewardIcon"),
                    CatIcon = FindFirstDescendantByName<Image>(slotRoot, "CatIcon"),
                    TitleLabel = GetOrCreateRuntimeText(
                        slotRoot,
                        "RuntimeTaskTitle",
                        new Vector2(0.08f, 1f),
                        new Vector2(0.92f, 1f),
                        new Vector2(0.5f, 1f),
                        new Vector2(0f, -10f),
                        new Vector2(0f, 34f),
                        18f,
                        TextAlignmentOptions.Center),
                    RewardLabel = GetOrCreateRuntimeText(
                        slotRoot,
                        "RuntimeTaskReward",
                        new Vector2(0.08f, 0f),
                        new Vector2(0.92f, 0f),
                        new Vector2(0.5f, 0f),
                        new Vector2(0f, 14f),
                        new Vector2(0f, 34f),
                        16f,
                        TextAlignmentOptions.Center),
                };

                _taskSlotViews.Add(view);
            }
        }

        private void RenderTaskSlots(MainTaskItemVm[] items)
        {
            EnsureTaskSlotViews();
            for (int i = 0; i < _taskSlotViews.Count; i++)
            {
                MainTaskItemVm item = i < items.Length ? items[i] : null;
                RenderTaskSlot(_taskSlotViews[i], item);
            }
        }

        private void RenderTaskSlot(MainTaskSlotView slotView, MainTaskItemVm item)
        {
            if (slotView == null || slotView.Root == null)
            {
                return;
            }

            if (item == null)
            {
                ApplyTaskSlotVisual(slotView, "未使用", string.Empty, "空位", 0f, false, false, false);
                return;
            }

            ApplyTaskSlotVisual(
                slotView,
                item.Title,
                item.Progress,
                item.Reward,
                item.ProgressNormalized,
                item.IsLocked,
                item.IsClaimable,
                item.ButtonEnabled);
        }

        private static void ApplyTaskSlotVisual(
            MainTaskSlotView slotView,
            string title,
            string progress,
            string reward,
            float progressValue,
            bool isLocked,
            bool isClaimable,
            bool buttonEnabled)
        {
            if (slotView.Background != null)
            {
                slotView.Background.color = isLocked
                    ? new Color(0.32f, 0.33f, 0.37f, 0.9f)
                    : (isClaimable
                        ? new Color(0.93f, 0.58f, 0.22f, 0.95f)
                        : new Color(0.22f, 0.24f, 0.31f, 0.88f));
            }

            if (slotView.Button != null)
            {
                slotView.Button.interactable = buttonEnabled;
            }

            SetTmpText(slotView.TitleLabel, title);
            SetTmpText(slotView.ProgressLabel, progress);
            SetTmpText(slotView.RewardLabel, reward);

            if (slotView.ProgressSlider != null)
            {
                slotView.ProgressSlider.value = Mathf.Clamp01(progressValue);
                slotView.ProgressSlider.interactable = false;
            }

            if (slotView.CatIcon != null)
            {
                slotView.CatIcon.color = isLocked
                    ? new Color(1f, 1f, 1f, 0.25f)
                    : new Color(1f, 1f, 1f, 1f);
            }

            if (slotView.RewardIcon != null)
            {
                slotView.RewardIcon.color = isClaimable
                    ? new Color(1f, 1f, 1f, 1f)
                    : new Color(1f, 1f, 1f, 0.72f);
            }
        }

        private RectTransform GetOrCreateOverlayRoot()
        {
            Transform existing = transform.Find(MainBindings.RuntimeOverlayNodeName);
            GameObject overlayObject = existing != null ? existing.gameObject : new GameObject(MainBindings.RuntimeOverlayNodeName, typeof(RectTransform));
            overlayObject.transform.SetParent(transform, false);

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
            TextMeshProUGUI existing = FindDescendantComponent<TextMeshProUGUI>("BackgroundImage/Money_btn/Text (TMP)")
                ?? FindFirstDescendantByName<TextMeshProUGUI>("MoneyCount")
                ?? FindFirstDescendantByName<TextMeshProUGUI>("Text (TMP)");
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
                new Vector2(240f, 96f),
                new Color(0.28f, 0.63f, 0.89f, 0.96f));
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
            return text;
        }

        private Button GetOrCreateRuntimeButton(
            Transform parent,
            string objectName,
            string label,
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

            TextMeshProUGUI labelText = GetOrCreateRuntimeText(
                buttonObject.transform,
                objectName + "_Label",
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero,
                34f,
                TextAlignmentOptions.Center);
            TmpGlyphCoverageReporter.SetText(labelText, label);
            return button;
        }

        private void SetButtonLabel(Button button, string label)
        {
            if (button == null)
            {
                return;
            }

            TextMeshProUGUI tmp = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp != null)
            {
                string effectiveLabel = string.IsNullOrWhiteSpace(label) ? button.name : label;
                tmp.enableWordWrapping = true;
                tmp.overflowMode = TextOverflowModes.Ellipsis;
                tmp.fontSize = effectiveLabel.Length > 16 ? 24f : 34f;
                TmpGlyphCoverageReporter.SetText(tmp, effectiveLabel);
                return;
            }

            Text legacyText = button.GetComponentInChildren<Text>(true);
            if (legacyText != null)
            {
                legacyText.horizontalOverflow = HorizontalWrapMode.Wrap;
                legacyText.verticalOverflow = VerticalWrapMode.Truncate;
                legacyText.text = string.IsNullOrWhiteSpace(label) ? button.name : label;
            }
        }

        private T FindDescendantComponent<T>(string path) where T : Component
        {
            Transform target = transform.Find(path);
            return target != null ? target.GetComponent<T>() : null;
        }

        private T FindFirstDescendantByName<T>(string objectName) where T : Component
        {
            return FindFirstDescendantByName<T>(transform, objectName);
        }

        private static T FindFirstDescendantByName<T>(Transform root, string objectName) where T : Component
        {
            if (root == null)
            {
                return null;
            }

            Transform[] all = root.GetComponentsInChildren<Transform>(true);
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

        private Transform FindFirstTransformByName(string objectName)
        {
            Transform[] all = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == objectName)
                {
                    return all[i];
                }
            }

            return null;
        }

        private static void SetTmpText(TMP_Text text, string value)
        {
            if (text == null)
            {
                return;
            }

            RuntimeTmpFontResolver.EnsureFontSupportsText(text, value);
            text.text = value ?? string.Empty;
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

        private sealed class MainTaskSlotView
        {
            public int SlotIndex;
            public RectTransform Root;
            public Image Background;
            public Button Button;
            public TMP_Text TitleLabel;
            public TMP_Text ProgressLabel;
            public TMP_Text RewardLabel;
            public Slider ProgressSlider;
            public Image RewardIcon;
            public Image CatIcon;
        }
    }
}
