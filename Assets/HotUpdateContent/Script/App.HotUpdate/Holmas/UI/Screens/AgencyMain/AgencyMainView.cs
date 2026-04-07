using App.HotUpdate.Holmas.UI.Binding;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace App.HotUpdate.Holmas.UI.Screens.AgencyMain
{
    public sealed class AgencyMainView : MonoBehaviour
    {
        private AgencyMainBindings _bindings;
        private UnityAction _currentPrimaryAction;

        public void EnsurePlaceholderLayout()
        {
            UiReferenceCollector collector = gameObject.GetComponent<UiReferenceCollector>();
            if (collector == null)
            {
                collector = gameObject.AddComponent<UiReferenceCollector>();
            }

            if (collector.EntryCount > 0)
            {
                return;
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

            VerticalLayoutGroup layout = gameObject.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
            {
                layout = gameObject.AddComponent<VerticalLayoutGroup>();
            }

            layout.padding = new RectOffset(48, 48, 96, 48);
            layout.spacing = 24f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            collector.RegisterOrReplace(AgencyMainBindings.RootPanelKey, rootRect, nodePath: "Root");

            Text titleText = CreateText("TitleText", "AgencyMain", 44, FontStyle.Bold, TextAnchor.MiddleLeft);
            collector.RegisterOrReplace(AgencyMainBindings.TitleTextKey, titleText, nodePath: "Root/TitleText");

            Text summaryText = CreateText("SummaryText", "AgencyMain summary", 28, FontStyle.Normal, TextAnchor.UpperLeft);
            summaryText.horizontalOverflow = HorizontalWrapMode.Wrap;
            summaryText.verticalOverflow = VerticalWrapMode.Overflow;
            collector.RegisterOrReplace(AgencyMainBindings.SummaryTextKey, summaryText, nodePath: "Root/SummaryText");

            Text taskSummaryText = CreateText("TaskSummaryText", "Task summary", 24, FontStyle.Normal, TextAnchor.UpperLeft);
            taskSummaryText.horizontalOverflow = HorizontalWrapMode.Wrap;
            taskSummaryText.verticalOverflow = VerticalWrapMode.Overflow;
            collector.RegisterOrReplace(AgencyMainBindings.TaskSummaryTextKey, taskSummaryText, nodePath: "Root/TaskSummaryText");

            Text boardSummaryText = CreateText("BoardSummaryText", "Board summary", 24, FontStyle.Normal, TextAnchor.UpperLeft);
            boardSummaryText.horizontalOverflow = HorizontalWrapMode.Wrap;
            boardSummaryText.verticalOverflow = VerticalWrapMode.Overflow;
            collector.RegisterOrReplace(AgencyMainBindings.BoardSummaryTextKey, boardSummaryText, nodePath: "Root/BoardSummaryText");

            Text statusText = CreateText("StatusText", "Status", 24, FontStyle.Italic, TextAnchor.UpperLeft);
            statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
            statusText.verticalOverflow = VerticalWrapMode.Overflow;
            collector.RegisterOrReplace(AgencyMainBindings.StatusTextKey, statusText, nodePath: "Root/StatusText");

            Button actionButton = CreateButton("PrimaryActionButton", "Refresh");
            collector.RegisterOrReplace(
                AgencyMainBindings.PrimaryActionButtonKey,
                actionButton,
                AgencyMainBindings.PrimaryActionButtonClickEvent,
                "Root/PrimaryActionButton");
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
                        ? "Refresh"
                        : viewModel.PrimaryActionLabel;
                }
            }
        }

        private Text CreateText(string objectName, string textValue, int fontSize, FontStyle fontStyle, TextAnchor anchor)
        {
            var textObject = new GameObject(objectName);
            textObject.transform.SetParent(transform, false);
            var rectTransform = textObject.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(0f, fontSize * 1.8f);

            var text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = anchor;
            text.color = Color.white;
            text.text = textValue;
            return text;
        }

        private Button CreateButton(string objectName, string textValue)
        {
            var buttonObject = new GameObject(objectName);
            buttonObject.transform.SetParent(transform, false);
            var rectTransform = buttonObject.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(0f, 96f);

            var image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.23f, 0.45f, 0.75f, 1f);

            var button = buttonObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = image.color;
            colors.highlightedColor = new Color(0.28f, 0.50f, 0.82f, 1f);
            colors.pressedColor = new Color(0.15f, 0.31f, 0.58f, 1f);
            button.colors = colors;

            Text label = CreateText($"{objectName}_Label", textValue, 28, FontStyle.Bold, TextAnchor.MiddleCenter);
            label.transform.SetParent(buttonObject.transform, false);
            Stretch(label.rectTransform);
            return button;
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
