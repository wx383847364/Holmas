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
        private Button _collapsedButton;
        private UnityAction _nextAction;
        private UnityAction _skipAction;
        private UnityAction _collapseAction;
        private UnityAction _expandAction;
        private TutorialVisualSpriteLoader _spriteLoader;

        public void EnsureSurface()
        {
            _root = gameObject.GetComponent<RectTransform>();
            if (_root == null)
            {
                _root = gameObject.AddComponent<RectTransform>();
            }

            Stretch(_root);

            _highlight = GetOrCreateRect("Highlight");
            Image highlightImage = GetOrCreateImage(_highlight.gameObject);
            highlightImage.color = new Color(1f, 0.88f, 0.25f, 0.32f);
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
            cardImage.raycastTarget = true;

            _mainImage = GetOrCreateVisualImage(_card, "MainImage", new Vector2(0.05f, 0.36f), new Vector2(0.24f, 0.91f));
            _tipsIcon = GetOrCreateVisualImage(_card, "TipsIcon", new Vector2(0.26f, 0.75f), new Vector2(0.34f, 0.93f));
            _fingerIcon = GetOrCreateVisualImage(_root, "FingerIcon", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            ConfigureRect(_fingerIcon.transform as RectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), Vector2.zero, new Vector2(64f, 64f));
            _title = GetOrCreateText(_card, "Title", new Vector2(0.35f, 0.68f), new Vector2(0.95f, 0.95f), 34f, TextAlignmentOptions.TopLeft);
            _body = GetOrCreateText(_card, "Body", new Vector2(0.35f, 0.28f), new Vector2(0.95f, 0.7f), 26f, TextAlignmentOptions.TopLeft);
            _skipButton = GetOrCreateButton(_card, "SkipButton", new Vector2(0.05f, 0.06f), new Vector2(0.25f, 0.25f), "跳过");
            _collapseButton = GetOrCreateButton(_card, "CollapseButton", new Vector2(0.28f, 0.06f), new Vector2(0.48f, 0.25f), "收起");
            _nextButton = GetOrCreateButton(_card, "NextButton", new Vector2(0.67f, 0.06f), new Vector2(0.95f, 0.25f), "下一步");

            _collapsedButton = GetOrCreateButton(
                _root,
                "CollapsedButton",
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                "引导");
            ConfigureRect(
                _collapsedButton.transform as RectTransform,
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(-36f, 36f),
                new Vector2(150f, 64f));
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

        public void SetActions(UnityAction nextAction, UnityAction skipAction, UnityAction collapseAction, UnityAction expandAction)
        {
            ReplaceButtonAction(_nextButton, ref _nextAction, nextAction);
            ReplaceButtonAction(_skipButton, ref _skipAction, skipAction);
            ReplaceButtonAction(_collapseButton, ref _collapseAction, collapseAction);
            ReplaceButtonAction(_collapsedButton, ref _expandAction, expandAction);
        }

        public void Render(TutorialOverlayVm viewModel)
        {
            if (viewModel == null)
            {
                return;
            }

            EnsureSurface();

            bool isCollapsed = viewModel.IsCollapsed;
            if (_card != null)
            {
                _card.gameObject.SetActive(!isCollapsed);
            }

            if (_collapsedButton != null)
            {
                _collapsedButton.gameObject.SetActive(isCollapsed);
                SetButtonLabel(_collapsedButton, viewModel.CollapsedHintText);
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

            Vector3[] corners = new Vector3[4];
            target.GetWorldCorners(corners);
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (int i = 0; i < corners.Length; i++)
            {
                Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null, corners[i]);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screenPoint, null, out Vector2 localPoint);
                min = Vector2.Min(min, localPoint);
                max = Vector2.Max(max, localPoint);
            }

            _highlight.gameObject.SetActive(true);
            _highlight.anchorMin = new Vector2(0.5f, 0.5f);
            _highlight.anchorMax = new Vector2(0.5f, 0.5f);
            _highlight.pivot = new Vector2(0.5f, 0.5f);
            _highlight.anchoredPosition = (min + max) * 0.5f;
            _highlight.sizeDelta = (max - min) + new Vector2(24f, 24f);
            _highlight.SetAsFirstSibling();
            if (_fingerIcon != null)
            {
                _fingerIcon.gameObject.SetActive(true);
                RectTransform fingerRect = _fingerIcon.transform as RectTransform;
                if (fingerRect != null)
                {
                    fingerRect.anchorMin = new Vector2(0.5f, 0.5f);
                    fingerRect.anchorMax = new Vector2(0.5f, 0.5f);
                    fingerRect.pivot = new Vector2(0f, 1f);
                    fingerRect.anchoredPosition = _highlight.anchoredPosition + new Vector2(_highlight.sizeDelta.x * 0.45f, -_highlight.sizeDelta.y * 0.45f);
                }
            }
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
            GameObject buttonObject = GetOrCreateChild(parent, objectName);
            RectTransform rect = buttonObject.GetComponent<RectTransform>() ?? buttonObject.AddComponent<RectTransform>();
            ConfigureRect(rect, anchorMin, anchorMax, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            GetOrCreateImage(buttonObject).color = new Color(0.18f, 0.44f, 0.72f, 0.98f);
            Button button = buttonObject.GetComponent<Button>() ?? buttonObject.AddComponent<Button>();
            TextMeshProUGUI labelText = GetOrCreateText(rect, "Label", Vector2.zero, Vector2.one, 24f, TextAlignmentOptions.Center);
            TmpGlyphCoverageReporter.SetText(labelText, label);
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
            Transform child = parent != null ? parent.Find(objectName) : null;
            GameObject childObject = child != null ? child.gameObject : new GameObject(objectName);
            if (parent != null)
            {
                childObject.transform.SetParent(parent, false);
            }
            return childObject;
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
