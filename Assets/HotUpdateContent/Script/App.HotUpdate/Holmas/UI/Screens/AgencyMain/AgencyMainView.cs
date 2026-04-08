using App.HotUpdate.Holmas.UI.Binding;
using App.HotUpdate.Holmas.UI.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace App.HotUpdate.Holmas.UI.Screens.AgencyMain
{
    public sealed class AgencyMainView : MonoBehaviour
    {
        private AgencyMainBindings _bindings;
        private UnityAction _currentPrimaryAction;

        public void EnsureFallbackLayout()
        {
            gameObject.name = AgencyMainBindings.RootNodePath;

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
            Image background = gameObject.GetComponent<Image>();
            if (background == null)
            {
                background = gameObject.AddComponent<Image>();
            }

            background.color = new Color(0.09f, 0.12f, 0.16f, 0.95f);

            RectTransform contentRect = GetOrCreateContentRoot();
            collector.RegisterOrReplace(AgencyMainBindings.RootPanelKey, rootRect, nodePath: AgencyMainBindings.RootNodePath);

            Text titleText = GetOrCreateText(contentRect, "TitleText", "AgencyMain", 44, FontStyle.Bold, TextAnchor.MiddleLeft);
            collector.RegisterOrReplace(
                AgencyMainBindings.TitleTextKey,
                titleText,
                nodePath: AgencyMainBindings.TitleTextNodePath);

            Text summaryText = GetOrCreateText(contentRect, "SummaryText", "AgencyMain summary", 28, FontStyle.Normal, TextAnchor.UpperLeft);
            summaryText.horizontalOverflow = HorizontalWrapMode.Wrap;
            summaryText.verticalOverflow = VerticalWrapMode.Overflow;
            collector.RegisterOrReplace(
                AgencyMainBindings.SummaryTextKey,
                summaryText,
                nodePath: AgencyMainBindings.SummaryTextNodePath);

            Text taskSummaryText = GetOrCreateText(contentRect, "TaskSummaryText", "Task summary", 24, FontStyle.Normal, TextAnchor.UpperLeft);
            taskSummaryText.horizontalOverflow = HorizontalWrapMode.Wrap;
            taskSummaryText.verticalOverflow = VerticalWrapMode.Overflow;
            collector.RegisterOrReplace(
                AgencyMainBindings.TaskSummaryTextKey,
                taskSummaryText,
                nodePath: AgencyMainBindings.TaskSummaryTextNodePath);

            Text boardSummaryText = GetOrCreateText(contentRect, "BoardSummaryText", "Board summary", 24, FontStyle.Normal, TextAnchor.UpperLeft);
            boardSummaryText.horizontalOverflow = HorizontalWrapMode.Wrap;
            boardSummaryText.verticalOverflow = VerticalWrapMode.Overflow;
            collector.RegisterOrReplace(
                AgencyMainBindings.BoardSummaryTextKey,
                boardSummaryText,
                nodePath: AgencyMainBindings.BoardSummaryTextNodePath);

            Text statusText = GetOrCreateText(contentRect, "StatusText", "Status", 24, FontStyle.Italic, TextAnchor.UpperLeft);
            statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
            statusText.verticalOverflow = VerticalWrapMode.Overflow;
            collector.RegisterOrReplace(
                AgencyMainBindings.StatusTextKey,
                statusText,
                nodePath: AgencyMainBindings.StatusTextNodePath);

            Button actionButton = GetOrCreateButton(contentRect, "PrimaryActionButton", "Open Level");
            collector.RegisterOrReplace(
                AgencyMainBindings.PrimaryActionButtonKey,
                actionButton,
                AgencyMainBindings.PrimaryActionButtonClickEvent,
                AgencyMainBindings.PrimaryActionButtonNodePath);
        }

        private RectTransform GetOrCreateContentRoot()
        {
            GameObject contentObject = GetOrCreateChild(transform, AgencyMainBindings.ContentNodeName);
            RectTransform contentRect = contentObject.GetComponent<RectTransform>();
            if (contentRect == null)
            {
                contentRect = contentObject.AddComponent<RectTransform>();
            }

            Stretch(contentRect);

            UiSafeAreaFitter safeAreaFitter = contentObject.GetComponent<UiSafeAreaFitter>();
            if (safeAreaFitter == null)
            {
                safeAreaFitter = contentObject.AddComponent<UiSafeAreaFitter>();
            }

            VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
            {
                layout = contentObject.AddComponent<VerticalLayoutGroup>();
            }

            layout.padding = new RectOffset(48, 48, 96, 48);
            layout.spacing = 24f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return contentRect;
        }

        public void Bind(AgencyMainBindings bindings)
        {
            _bindings = bindings ?? new AgencyMainBindings();
        }

        public void SetPrimaryAction(UnityAction action)
        {
            if (_bindings?.PrimaryActionButton == null)
            {
                _currentPrimaryAction = action;
                return;
            }

            if (_currentPrimaryAction != null)
            {
                _bindings.PrimaryActionButton.onClick.RemoveListener(_currentPrimaryAction);
            }

            _currentPrimaryAction = action;
            if (_currentPrimaryAction != null)
            {
                _bindings.PrimaryActionButton.onClick.AddListener(_currentPrimaryAction);
            }
        }

        public void Render(AgencyMainVm viewModel)
        {
            if (viewModel == null)
            {
                return;
            }

            if (_bindings?.TitleText != null)
            {
                _bindings.TitleText.text = viewModel.Title ?? string.Empty;
            }

            if (_bindings?.SummaryText != null)
            {
                _bindings.SummaryText.text = viewModel.Summary ?? string.Empty;
            }

            if (_bindings?.TaskSummaryText != null)
            {
                _bindings.TaskSummaryText.text = viewModel.TaskSummary ?? string.Empty;
            }

            if (_bindings?.BoardSummaryText != null)
            {
                _bindings.BoardSummaryText.text = viewModel.BoardSummary ?? string.Empty;
            }

            if (_bindings?.StatusText != null)
            {
                _bindings.StatusText.text = viewModel.Status ?? string.Empty;
            }

            if (_bindings?.PrimaryActionButton != null)
            {
                _bindings.PrimaryActionButton.interactable = viewModel.PrimaryActionEnabled;
                Text buttonLabel = _bindings.PrimaryActionButton.GetComponentInChildren<Text>();
                if (buttonLabel != null)
                {
                    buttonLabel.text = string.IsNullOrWhiteSpace(viewModel.PrimaryActionLabel)
                        ? "Open Level"
                        : viewModel.PrimaryActionLabel;
                }
            }
        }

        private Text GetOrCreateText(Transform parent, string objectName, string textValue, int fontSize, FontStyle fontStyle, TextAnchor anchor)
        {
            GameObject textObject = GetOrCreateChild(parent, objectName);
            RectTransform rectTransform = textObject.GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                rectTransform = textObject.AddComponent<RectTransform>();
            }

            rectTransform.sizeDelta = new Vector2(0f, fontSize * 1.8f);

            Text text = textObject.GetComponent<Text>();
            if (text == null)
            {
                text = textObject.AddComponent<Text>();
            }

            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = anchor;
            text.color = Color.white;
            text.text = textValue;
            return text;
        }

        private Button GetOrCreateButton(Transform parent, string objectName, string textValue)
        {
            GameObject buttonObject = GetOrCreateChild(parent, objectName);
            RectTransform rectTransform = buttonObject.GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                rectTransform = buttonObject.AddComponent<RectTransform>();
            }

            rectTransform.sizeDelta = new Vector2(0f, 96f);

            Image image = buttonObject.GetComponent<Image>();
            if (image == null)
            {
                image = buttonObject.AddComponent<Image>();
            }

            image.color = new Color(0.23f, 0.45f, 0.75f, 1f);

            Button button = buttonObject.GetComponent<Button>();
            if (button == null)
            {
                button = buttonObject.AddComponent<Button>();
            }

            ColorBlock colors = button.colors;
            colors.normalColor = image.color;
            colors.highlightedColor = new Color(0.28f, 0.50f, 0.82f, 1f);
            colors.pressedColor = new Color(0.15f, 0.31f, 0.58f, 1f);
            button.colors = colors;

            Text label = GetOrCreateText(buttonObject.transform, $"{objectName}_Label", textValue, 28, FontStyle.Bold, TextAnchor.MiddleCenter);
            label.transform.SetParent(buttonObject.transform, false);
            Stretch(label.rectTransform);
            return button;
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
    }
}
