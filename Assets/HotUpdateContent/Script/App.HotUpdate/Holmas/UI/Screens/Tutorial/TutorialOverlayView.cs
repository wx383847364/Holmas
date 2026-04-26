using App.HotUpdate.Holmas.UI.Tool;
using App.Shared.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace App.HotUpdate.Holmas.UI.Screens.Tutorial
{
    public sealed class TutorialOverlayView : MonoBehaviour
    {
        private RectTransform _root;
        private RectTransform _dimMask;
        private RectTransform _highlight;
        private RectTransform _card;
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _body;
        private Image _mainImage;
        private Image _tipsIcon;
        private Image _fingerIcon;
        private Button _nextButton;
        private Button _skipButton;
        private Button _collapseButton;
        private UnityAction _nextAction;
        private UnityAction _skipAction;
        private UnityAction _collapseAction;
        private TutorialVisualSpriteLoader _spriteLoader;

        public void EnsureSurface()
        {
            _root = gameObject.GetComponent<RectTransform>();
            if (_root == null)
            {
                _root = gameObject.AddComponent<RectTransform>();
            }

            Stretch(_root);

            _dimMask = GetOrCreateRect("DimMask");
            Stretch(_dimMask);
            Image dimMaskImage = GetOrCreateImage(_dimMask.gameObject);
            dimMaskImage.color = new Color(0f, 0f, 0f, 0.5f);
            dimMaskImage.raycastTarget = false;

            _highlight = GetOrCreateRect("Highlight");
            Image highlightImage = GetOrCreateImage(_highlight.gameObject);
            highlightImage.color = new Color(1f, 0.88f, 0.25f, 0.52f);
            highlightImage.raycastTarget = false;

            _card = GetOrCreateRect("TutorialCard");
            ConfigureRect(
                _card,
                new Vector2(0.06f, 0f),
                new Vector2(0.94f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 42f),
                new Vector2(0f, 300f));
            Image cardImage = GetOrCreateImage(_card.gameObject);
            cardImage.color = new Color(0.08f, 0.1f, 0.12f, 0.9f);
            cardImage.raycastTarget = false;

            _mainImage = GetOrCreateVisualImage(_card, "MainImage", new Vector2(0.05f, 0.36f), new Vector2(0.24f, 0.91f));
            _tipsIcon = GetOrCreateVisualImage(_card, "TipsIcon", new Vector2(0.26f, 0.75f), new Vector2(0.34f, 0.93f));
            _fingerIcon = GetOrCreateVisualImage(_root, "FingerIcon", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            ConfigureRect(_fingerIcon.transform as RectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), Vector2.zero, new Vector2(64f, 64f));
            _title = GetOrCreateText(_card, "Title", new Vector2(0.35f, 0.68f), new Vector2(0.95f, 0.95f), 34f, TextAlignmentOptions.TopLeft);
            _body = GetOrCreateText(_card, "Body", new Vector2(0.35f, 0.28f), new Vector2(0.95f, 0.7f), 26f, TextAlignmentOptions.TopLeft);
            _skipButton = GetOrCreateButton(_card, "SkipButton", new Vector2(0.05f, 0.06f), new Vector2(0.25f, 0.25f), "跳过");
            _collapseButton = GetOrCreateButton(_card, "CollapseButton", new Vector2(0.28f, 0.06f), new Vector2(0.48f, 0.25f), "收起");
            _nextButton = GetOrCreateButton(_card, "NextButton", new Vector2(0.67f, 0.06f), new Vector2(0.95f, 0.25f), "下一步");
            DestroyChildIfExists(_root, "CollapsedButton");
            ApplyLayerOrder();
        }

        public void SetAssetsRuntime(IAssetsRuntime assetsRuntime)
        {
            if (_spriteLoader == null)
            {
                _spriteLoader = new TutorialVisualSpriteLoader(assetsRuntime);
                return;
            }

            _spriteLoader.SetAssetsRuntime(assetsRuntime);
        }

        public void SetActions(UnityAction nextAction, UnityAction skipAction, UnityAction collapseAction)
        {
            ReplaceButtonAction(_nextButton, ref _nextAction, nextAction);
            ReplaceButtonAction(_skipButton, ref _skipAction, skipAction);
            ReplaceButtonAction(_collapseButton, ref _collapseAction, collapseAction);
        }

        public void Render(TutorialOverlayVm viewModel)
        {
            if (viewModel == null)
            {
                return;
            }

            EnsureSurface();

            bool isCollapsed = viewModel.IsCollapsed;
            if (_dimMask != null)
            {
                _dimMask.gameObject.SetActive(!isCollapsed);
            }

            if (_card != null)
            {
                _card.gameObject.SetActive(!isCollapsed);
            }

            if (isCollapsed)
            {
                if (_highlight != null)
                {
                    _highlight.gameObject.SetActive(false);
                }

                if (_fingerIcon != null)
                {
                    _fingerIcon.gameObject.SetActive(false);
                }
                return;
            }

            TmpGlyphCoverageReporter.SetText(_title, viewModel.Title);
            TmpGlyphCoverageReporter.SetText(_body, viewModel.Body);
            SetButtonLabel(_nextButton, viewModel.NextButtonText);
            SetButtonLabel(_skipButton, viewModel.SkipButtonText);
            SetButtonLabel(_collapseButton, viewModel.CollapseButtonText);
            if (_skipButton != null)
            {
                _skipButton.gameObject.SetActive(viewModel.CanSkip);
            }

            UpdateHighlight(viewModel.TargetRect);
            UpdateCardPlacement(viewModel.TargetRect);
            UpdateVisuals(viewModel);
        }

        private void UpdateHighlight(RectTransform target)
        {
            if (_highlight == null)
            {
                return;
            }

            if (target == null || _root == null)
            {
                _highlight.gameObject.SetActive(false);
                return;
            }

            if (!TryGetRootLocalBounds(target, out Rect bounds))
            {
                _highlight.gameObject.SetActive(false);
                return;
            }

            _highlight.gameObject.SetActive(true);
            _highlight.anchorMin = new Vector2(0.5f, 0.5f);
            _highlight.anchorMax = new Vector2(0.5f, 0.5f);
            _highlight.pivot = new Vector2(0.5f, 0.5f);
            _highlight.anchoredPosition = bounds.center;
            _highlight.sizeDelta = bounds.size + new Vector2(24f, 24f);
            if (_fingerIcon != null)
            {
                _fingerIcon.gameObject.SetActive(true);
                RectTransform fingerRect = _fingerIcon.transform as RectTransform;
                if (fingerRect != null)
                {
                    fingerRect.anchorMin = new Vector2(0.5f, 0.5f);
                    fingerRect.anchorMax = new Vector2(0.5f, 0.5f);
                    fingerRect.pivot = new Vector2(0f, 1f);
                    fingerRect.anchoredPosition = bounds.center + new Vector2(_highlight.sizeDelta.x * 0.45f, -_highlight.sizeDelta.y * 0.45f);
                }
            }

            ApplyLayerOrder();
        }

        private void UpdateCardPlacement(RectTransform target)
        {
            if (_card == null || _root == null)
            {
                return;
            }

            if (target == null || !TryGetRootLocalBounds(target, out Rect targetBounds))
            {
                ApplyDefaultCardLayout();
                return;
            }

            Rect rootRect = _root.rect;
            if (rootRect.width <= 0f || rootRect.height <= 0f)
            {
                ApplyDefaultCardLayout();
                return;
            }

            float cardHeight = Mathf.Clamp(rootRect.height * 0.3f, 260f, 320f);
            float safeMargin = Mathf.Clamp(rootRect.height * 0.04f, 24f, 56f);
            float gap = Mathf.Clamp(rootRect.height * 0.035f, 24f, 48f);
            float minBottom = rootRect.yMin + safeMargin;
            float maxBottom = rootRect.yMax - safeMargin - cardHeight;
            bool preferAbove = targetBounds.center.y < rootRect.center.y;
            float aboveBottom = targetBounds.yMax + gap;
            float belowBottom = targetBounds.yMin - gap - cardHeight;
            float desiredBottom = preferAbove ? aboveBottom : belowBottom;
            if (desiredBottom < minBottom || desiredBottom > maxBottom)
            {
                float alternateBottom = preferAbove ? belowBottom : aboveBottom;
                if (alternateBottom >= minBottom && alternateBottom <= maxBottom)
                {
                    desiredBottom = alternateBottom;
                }
            }

            if (maxBottom < minBottom)
            {
                desiredBottom = minBottom;
            }
            else
            {
                desiredBottom = Mathf.Clamp(desiredBottom, minBottom, maxBottom);
            }

            ConfigureCardLayout(desiredBottom - rootRect.yMin, cardHeight);
        }

        private void ApplyDefaultCardLayout()
        {
            ConfigureCardLayout(42f, 300f);
        }

        private void ConfigureCardLayout(float anchoredBottom, float height)
        {
            ConfigureRect(
                _card,
                new Vector2(0.06f, 0f),
                new Vector2(0.94f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, anchoredBottom),
                new Vector2(0f, height));
        }

        private bool TryGetRootLocalBounds(RectTransform target, out Rect bounds)
        {
            bounds = new Rect();
            if (target == null || _root == null)
            {
                return false;
            }

            bool hasBounds = false;
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            AccumulateRectBounds(target, ref hasBounds, ref min, ref max);
            if (!hasBounds)
            {
                RectTransform[] descendants = target.GetComponentsInChildren<RectTransform>(true);
                for (int i = 0; i < descendants.Length; i++)
                {
                    RectTransform descendant = descendants[i];
                    if (descendant == null || ReferenceEquals(descendant, target))
                    {
                        continue;
                    }

                    AccumulateRectBounds(descendant, ref hasBounds, ref min, ref max);
                }
            }

            if (!hasBounds)
            {
                Vector2 center = _root.InverseTransformPoint(target.position);
                Vector2 fallbackHalfSize = new Vector2(90f, 42f);
                min = center - fallbackHalfSize;
                max = center + fallbackHalfSize;
                hasBounds = true;
            }

            if (!hasBounds ||
                IsInvalid(min.x) ||
                IsInvalid(min.y) ||
                IsInvalid(max.x) ||
                IsInvalid(max.y))
            {
                return false;
            }

            bounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return bounds.width > 0f && bounds.height > 0f;
        }

        private void AccumulateRectBounds(RectTransform rectTransform, ref bool hasBounds, ref Vector2 min, ref Vector2 max)
        {
            if (rectTransform == null || rectTransform.rect.width <= 0.1f || rectTransform.rect.height <= 0.1f)
            {
                return;
            }

            Vector3[] corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            for (int i = 0; i < corners.Length; i++)
            {
                Vector2 localPoint = _root.InverseTransformPoint(corners[i]);
                min = Vector2.Min(min, localPoint);
                max = Vector2.Max(max, localPoint);
            }

            hasBounds = true;
        }

        private static bool IsInvalid(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value);
        }

        private void UpdateVisuals(TutorialOverlayVm viewModel)
        {
            if (_spriteLoader == null)
            {
                _spriteLoader = new TutorialVisualSpriteLoader(null);
            }

            _spriteLoader.Bind(_mainImage, viewModel.MainImagePath, new Color(0.2f, 0.32f, 0.42f, 0.95f));
            _spriteLoader.Bind(_tipsIcon, viewModel.TipsIconPath, new Color(0.96f, 0.72f, 0.2f, 0.95f));
            _spriteLoader.Bind(_fingerIcon, viewModel.FingerIconPath, new Color(1f, 0.92f, 0.4f, 0.92f));
        }

        private void ApplyLayerOrder()
        {
            if (_dimMask != null)
            {
                _dimMask.SetAsFirstSibling();
            }

            if (_highlight != null)
            {
                int highlightIndex = _dimMask != null ? _dimMask.GetSiblingIndex() + 1 : 0;
                _highlight.SetSiblingIndex(highlightIndex);
            }

            if (_fingerIcon != null)
            {
                _fingerIcon.transform.SetAsLastSibling();
            }

            if (_card != null)
            {
                _card.SetAsLastSibling();
            }
        }

        private RectTransform GetOrCreateRect(string objectName)
        {
            Transform child = transform.Find(objectName);
            GameObject childObject = child != null ? child.gameObject : new GameObject(objectName, typeof(RectTransform));
            childObject.transform.SetParent(transform, false);
            RectTransform rect = childObject.GetComponent<RectTransform>();
            if (rect == null)
            {
                rect = childObject.AddComponent<RectTransform>();
            }
            return rect;
        }

        private static TextMeshProUGUI GetOrCreateText(RectTransform parent, string objectName, Vector2 anchorMin, Vector2 anchorMax, float fontSize, TextAlignmentOptions alignment)
        {
            if (parent == null)
            {
                return null;
            }

            GameObject textObject = GetOrCreateChild(parent, objectName);
            RectTransform rect = textObject.GetComponent<RectTransform>() ?? textObject.AddComponent<RectTransform>();
            ConfigureRect(rect, anchorMin, anchorMax, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>() ?? textObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = alignment;
            text.enableWordWrapping = true;
            text.raycastTarget = false;
            return text;
        }

        private static Button GetOrCreateButton(RectTransform parent, string objectName, Vector2 anchorMin, Vector2 anchorMax, string label)
        {
            if (parent == null)
            {
                return null;
            }

            GameObject buttonObject = GetOrCreateChild(parent, objectName);
            RectTransform rect = buttonObject.GetComponent<RectTransform>() ?? buttonObject.AddComponent<RectTransform>();
            ConfigureRect(rect, anchorMin, anchorMax, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            GetOrCreateImage(buttonObject).color = new Color(0.18f, 0.44f, 0.72f, 0.98f);
            Button button = buttonObject.GetComponent<Button>() ?? buttonObject.AddComponent<Button>();
            TextMeshProUGUI labelText = GetOrCreateText(rect, "Label", Vector2.zero, Vector2.one, 24f, TextAlignmentOptions.Center);
            if (labelText != null)
            {
                TmpGlyphCoverageReporter.SetText(labelText, label);
            }

            return button;
        }

        private static Image GetOrCreateVisualImage(RectTransform parent, string objectName, Vector2 anchorMin, Vector2 anchorMax)
        {
            GameObject imageObject = GetOrCreateChild(parent, objectName);
            RectTransform rect = imageObject.GetComponent<RectTransform>() ?? imageObject.AddComponent<RectTransform>();
            ConfigureRect(rect, anchorMin, anchorMax, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Image image = GetOrCreateImage(imageObject);
            image.raycastTarget = false;
            image.preserveAspect = true;
            return image;
        }

        private static GameObject GetOrCreateChild(RectTransform parent, string objectName)
        {
            if (parent == null)
            {
                return null;
            }

            Transform child = parent.Find(objectName);
            if (child != null && !(child is RectTransform))
            {
                child.gameObject.name = objectName + "_LegacyTransform";
                GameObject replacement = new GameObject(objectName, typeof(RectTransform));
                replacement.transform.SetParent(parent, false);
                replacement.transform.SetSiblingIndex(child.GetSiblingIndex());
                if (UnityEngine.Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(child.gameObject);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                }

                return replacement;
            }

            GameObject childObject = child != null ? child.gameObject : new GameObject(objectName, typeof(RectTransform));
            childObject.transform.SetParent(parent, false);
            return childObject;
        }

        private static void DestroyChildIfExists(RectTransform parent, string objectName)
        {
            Transform child = parent != null ? parent.Find(objectName) : null;
            if (child == null)
            {
                return;
            }

            if (UnityEngine.Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }

        private static Image GetOrCreateImage(GameObject target)
        {
            Image image = target.GetComponent<Image>();
            if (image == null)
            {
                image = target.AddComponent<Image>();
            }
            return image;
        }

        private static void ReplaceButtonAction(Button button, ref UnityAction currentAction, UnityAction nextAction)
        {
            if (button == null)
            {
                currentAction = nextAction;
                return;
            }

            if (currentAction != null)
            {
                button.onClick.RemoveListener(currentAction);
            }

            currentAction = nextAction;
            if (currentAction != null)
            {
                button.onClick.AddListener(currentAction);
            }
        }

        private static void SetButtonLabel(Button button, string label)
        {
            if (button == null)
            {
                return;
            }

            TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (text == null)
            {
                return;
            }

            string effectiveLabel = string.IsNullOrWhiteSpace(label) ? button.name : label;
            text.fontSize = effectiveLabel.Length > 8 ? 21f : 24f;
            TmpGlyphCoverageReporter.SetText(text, effectiveLabel);
        }

        private static void Stretch(RectTransform rectTransform)
        {
            ConfigureRect(rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        private static void ConfigureRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            rect.localScale = Vector3.one;
        }

        private void OnDestroy()
        {
            _spriteLoader?.Dispose();
            _spriteLoader = null;
        }
    }
}
