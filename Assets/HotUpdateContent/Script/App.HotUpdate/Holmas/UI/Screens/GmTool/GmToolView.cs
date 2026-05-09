using App.HotUpdate.Holmas.UI.Tool;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace App.HotUpdate.Holmas.UI.Screens.GmTool
{
    public sealed class GmToolView : MonoBehaviour
    {
        private RectTransform _root;
        private RectTransform _panel;
        private RectTransform _header;
        private ScrollRect _scrollRect;
        private RectTransform _content;
        private RectTransform _quickCard;
        private RectTransform _quickActionsRow;
        private RectTransform _tutorialCard;
        private RectTransform _inputRow;
        private RectTransform _runtimeCard;
        private TextMeshProUGUI _titleText;
        private TextMeshProUGUI _statusText;
        private TextMeshProUGUI _tutorialProgressText;
        private TextMeshProUGUI _tutorialHintText;
        private TextMeshProUGUI _runtimeSummaryText;
        private TextMeshProUGUI _mainStatusText;
        private TMP_InputField _stepInput;
        private Button _closeButton;
        private Button _addEnergyButton;
        private Button _addGoldButton;
        private Button _replayHelpButton;
        private Button _startAtStepButton;
        private UnityAction _closeAction;
        private UnityAction _addEnergyAction;
        private UnityAction _addGoldAction;
        private UnityAction _replayHelpAction;
        private UnityAction _startAtStepAction;

        public void EnsureSurface()
        {
            GmToolResponsiveMetrics metrics = ComputeResponsiveMetrics();
            _root = gameObject.GetComponent<RectTransform>();
            if (_root == null)
            {
                _root = gameObject.AddComponent<RectTransform>();
            }

            Stretch(_root);

            _panel = GetOrCreateRect("Panel", transform);
            ConfigureRect(_panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, metrics.PanelSize);
            Image panelImage = GetOrCreateImage(_panel.gameObject);
            panelImage.color = new Color(0.08f, 0.1f, 0.14f, 0.96f);
            panelImage.raycastTarget = true;

            _header = GetOrCreateRect("Header", _panel);
            _titleText = GetOrCreateText(_header, "Title", "GM 工具", metrics.TitleFontSize, TextAlignmentOptions.MidlineLeft);

            _closeButton = GetOrCreateButton(_header, "CloseButton", "关闭");

            _scrollRect = GetOrCreateScrollRect(_panel);
            _content = EnsureScrollContent(_scrollRect);
            DestroyChildIfExists(_panel, "StatusText");

            _quickCard = GetOrCreateCard(_content, "QuickActionsCard");
            CreateCardTitle(_quickCard, "快捷操作");
            _quickActionsRow = GetOrCreateRow(_quickCard, "QuickActionsRow");
            DestroyChildIfExists(_quickActionsRow, "StartTutorialButton");
            _addEnergyButton = GetOrCreateButton(_quickActionsRow, "AddEnergyButton", "+5体力");
            _addGoldButton = GetOrCreateButton(_quickActionsRow, "AddGoldButton", "+100万金币");
            _replayHelpButton = GetOrCreateButton(_quickActionsRow, "ReplayHelpButton", "重看帮助");

            _tutorialCard = GetOrCreateCard(_content, "TutorialCard");
            CreateCardTitle(_tutorialCard, "教程调试");
            _tutorialProgressText = CreateCardBody(_tutorialCard, "TutorialProgressText");
            _tutorialHintText = CreateCardHint(_tutorialCard, "TutorialHintText");
            _inputRow = GetOrCreateRow(_tutorialCard, "StepInputRow");
            _stepInput = GetOrCreateInputField(_inputRow, "StepInput", "0");
            _startAtStepButton = GetOrCreateButton(_inputRow, "StartAtStepButton", "启动/跳步引导");

            _runtimeCard = GetOrCreateCard(_content, "RuntimeCard");
            CreateCardTitle(_runtimeCard, "Runtime 信息");
            _runtimeSummaryText = CreateCardBody(_runtimeCard, "RuntimeSummaryText");
            _mainStatusText = CreateCardBody(_runtimeCard, "MainStatusText");
            _statusText = CreateCardBody(_runtimeCard, "StatusText");
            _mainStatusText.enableWordWrapping = true;
            _statusText.enableWordWrapping = true;
            ApplyResponsiveLayout(metrics);
        }

        public void SetActions(
            UnityAction closeAction,
            UnityAction addEnergyAction,
            UnityAction addGoldAction,
            UnityAction replayHelpAction,
            UnityAction startAtStepAction)
        {
            ReplaceAction(_closeButton, ref _closeAction, closeAction);
            ReplaceAction(_addEnergyButton, ref _addEnergyAction, addEnergyAction);
            ReplaceAction(_addGoldButton, ref _addGoldAction, addGoldAction);
            ReplaceAction(_replayHelpButton, ref _replayHelpAction, replayHelpAction);
            ReplaceAction(_startAtStepButton, ref _startAtStepAction, startAtStepAction);
        }

        public void Render(GmToolVm viewModel)
        {
            EnsureSurface();
            GmToolResponsiveMetrics metrics = ComputeResponsiveMetrics();
            _addEnergyButton.interactable = viewModel != null && viewModel.AddEnergyEnabled;
            _addGoldButton.interactable = viewModel != null && viewModel.AddGoldEnabled;
            _replayHelpButton.interactable = viewModel != null && viewModel.ReplayHelpEnabled;
            _startAtStepButton.interactable = viewModel != null && viewModel.StartAtStepEnabled;
            TmpGlyphCoverageReporter.SetText(_statusText, viewModel != null ? viewModel.Status : string.Empty);
            TmpGlyphCoverageReporter.SetText(_tutorialProgressText, viewModel != null ? viewModel.TutorialProgressSummary : string.Empty);
            TmpGlyphCoverageReporter.SetText(_tutorialHintText, viewModel != null ? viewModel.TutorialActionHint : string.Empty);
            TmpGlyphCoverageReporter.SetText(_runtimeSummaryText, viewModel != null ? viewModel.RuntimeSummary : string.Empty);
            TmpGlyphCoverageReporter.SetText(_mainStatusText, viewModel != null ? viewModel.MainStatus : string.Empty);

            string stepText = viewModel != null && !string.IsNullOrWhiteSpace(viewModel.StepInputText)
                ? viewModel.StepInputText
                : "0";
            if (_stepInput != null &&
                (_stepInput.text == null || _stepInput.text.Length == 0 || !_stepInput.isFocused))
            {
                _stepInput.text = stepText;
            }

            ApplyResponsiveLayout(metrics);
        }

        public string GetRequestedStepText()
        {
            return _stepInput != null ? _stepInput.text : "0";
        }

        public string GetRequestedStepTextOrDefault(string fallback)
        {
            string value = GetRequestedStepText();
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static void ReplaceAction(Button button, ref UnityAction currentAction, UnityAction nextAction)
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

        private static ScrollRect GetOrCreateScrollRect(RectTransform parent)
        {
            GameObject scrollObject = GetOrCreateChild(parent, "ScrollView");
            RectTransform rect = scrollObject.GetComponent<RectTransform>() ?? scrollObject.AddComponent<RectTransform>();
            ScrollRect scrollRect = scrollObject.GetComponent<ScrollRect>() ?? scrollObject.AddComponent<ScrollRect>();
            GameObject viewportObject = GetOrCreateChild(scrollObject.transform, "Viewport");
            RectTransform viewportRect = viewportObject.GetComponent<RectTransform>() ?? viewportObject.AddComponent<RectTransform>();
            ConfigureRect(viewportRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Image viewportImage = GetOrCreateImage(viewportObject);
            viewportImage.color = new Color(0f, 0f, 0f, 0.01f);
            Mask mask = viewportObject.GetComponent<Mask>() ?? viewportObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            scrollRect.viewport = viewportRect;
            scrollRect.horizontal = false;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            return scrollRect;
        }

        private static RectTransform EnsureScrollContent(ScrollRect scrollRect)
        {
            GameObject contentObject = GetOrCreateChild(scrollRect.viewport, "Content");
            RectTransform contentRect = contentObject.GetComponent<RectTransform>() ?? contentObject.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 0f);

            VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>() ?? contentObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>() ?? contentObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scrollRect.content = contentRect;
            return contentRect;
        }

        private static RectTransform GetOrCreateCard(RectTransform parent, string objectName)
        {
            GameObject cardObject = GetOrCreateChild(parent, objectName);
            RectTransform cardRect = cardObject.GetComponent<RectTransform>() ?? cardObject.AddComponent<RectTransform>();
            Image cardImage = GetOrCreateImage(cardObject);
            cardImage.color = new Color(0.14f, 0.18f, 0.24f, 0.96f);

            VerticalLayoutGroup layout = cardObject.GetComponent<VerticalLayoutGroup>() ?? cardObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 20, 20);
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = cardObject.GetComponent<ContentSizeFitter>() ?? cardObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            LayoutElement element = cardObject.GetComponent<LayoutElement>() ?? cardObject.AddComponent<LayoutElement>();
            element.preferredHeight = 220f;
            element.minHeight = 160f;
            return cardRect;
        }

        private static void CreateCardTitle(RectTransform parent, string title)
        {
            TextMeshProUGUI titleText = GetOrCreateText(parent, "Title", title, 28f, TextAlignmentOptions.Left);
            titleText.color = new Color(0.98f, 0.9f, 0.48f, 1f);
        }

        private static TextMeshProUGUI CreateCardBody(RectTransform parent, string objectName)
        {
            TextMeshProUGUI body = GetOrCreateText(parent, objectName, string.Empty, 22f, TextAlignmentOptions.TopLeft);
            body.enableWordWrapping = true;
            LayoutElement element = body.gameObject.GetComponent<LayoutElement>() ?? body.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 140f;
            element.minHeight = 90f;
            return body;
        }

        private static TextMeshProUGUI CreateCardHint(RectTransform parent, string objectName)
        {
            TextMeshProUGUI hint = GetOrCreateText(parent, objectName, string.Empty, 20f, TextAlignmentOptions.TopLeft);
            hint.color = new Color(0.78f, 0.84f, 0.92f, 0.95f);
            LayoutElement element = hint.gameObject.GetComponent<LayoutElement>() ?? hint.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 56f;
            element.minHeight = 48f;
            return hint;
        }

        private static RectTransform GetOrCreateRow(RectTransform parent, string objectName)
        {
            GameObject rowObject = GetOrCreateChild(parent, objectName);
            RectTransform rowRect = rowObject.GetComponent<RectTransform>() ?? rowObject.AddComponent<RectTransform>();
            HorizontalLayoutGroup horizontalLayout = rowObject.GetComponent<HorizontalLayoutGroup>();
            if (horizontalLayout != null)
            {
                horizontalLayout.enabled = false;
            }

            ContentSizeFitter fitter = rowObject.GetComponent<ContentSizeFitter>();
            if (fitter != null)
            {
                fitter.enabled = false;
            }

            GridLayoutGroup grid = rowObject.GetComponent<GridLayoutGroup>() ?? rowObject.AddComponent<GridLayoutGroup>();
            grid.childAlignment = TextAnchor.UpperLeft;
            return rowRect;
        }

        private static void ApplyButtonLayout(Button button, float height, float fontSize)
        {
            RectTransform rect = button.transform as RectTransform;
            ConfigureRect(rect, Vector2.zero, Vector2.zero, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0f, height));
            LayoutElement element = button.gameObject.GetComponent<LayoutElement>() ?? button.gameObject.AddComponent<LayoutElement>();
            element.minWidth = 0f;
            element.preferredWidth = -1f;
            element.minHeight = height;
            element.preferredHeight = height;
            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
            {
                label.fontSize = fontSize;
                label.enableWordWrapping = true;
                label.overflowMode = TextOverflowModes.Ellipsis;
                label.margin = new Vector4(8f, 0f, 8f, 0f);
            }
        }

        private static void ConfigureInputLayout(TMP_InputField inputField, float height, float fontSize)
        {
            RectTransform rect = inputField.transform as RectTransform;
            ConfigureRect(rect, Vector2.zero, Vector2.zero, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0f, height));
            LayoutElement element = inputField.gameObject.GetComponent<LayoutElement>() ?? inputField.gameObject.AddComponent<LayoutElement>();
            element.minWidth = 0f;
            element.preferredWidth = -1f;
            element.minHeight = height;
            element.preferredHeight = height;
            if (inputField.textComponent != null)
            {
                inputField.textComponent.fontSize = fontSize;
            }

            if (inputField.placeholder is TextMeshProUGUI placeholder)
            {
                placeholder.fontSize = fontSize;
            }
        }

        private static Button GetOrCreateButton(RectTransform parent, string objectName, string label)
        {
            if (parent == null)
            {
                return null;
            }

            GameObject buttonObject = GetOrCreateChild(parent, objectName);
            RectTransform rect = buttonObject.GetComponent<RectTransform>() ?? buttonObject.AddComponent<RectTransform>();
            Image background = GetOrCreateImage(buttonObject);
            background.color = new Color(0.24f, 0.46f, 0.76f, 0.98f);
            Button button = buttonObject.GetComponent<Button>() ?? buttonObject.AddComponent<Button>();
            TextMeshProUGUI labelText = GetOrCreateText(rect, "Label", label, 24f, TextAlignmentOptions.Center);
            if (labelText != null)
            {
                ConfigureRect(labelText.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            }

            return button;
        }

        private static TMP_InputField GetOrCreateInputField(RectTransform parent, string objectName, string initialText)
        {
            GameObject inputObject = GetOrCreateChild(parent, objectName);
            RectTransform rect = inputObject.GetComponent<RectTransform>() ?? inputObject.AddComponent<RectTransform>();
            Image background = GetOrCreateImage(inputObject);
            background.color = new Color(0.06f, 0.08f, 0.1f, 0.94f);
            TMP_InputField input = inputObject.GetComponent<TMP_InputField>() ?? inputObject.AddComponent<TMP_InputField>();
            input.contentType = TMP_InputField.ContentType.IntegerNumber;

            TextMeshProUGUI placeholder = GetOrCreateText(rect, "Placeholder", initialText, 24f, TextAlignmentOptions.Center);
            placeholder.color = new Color(1f, 1f, 1f, 0.35f);
            ConfigureRect(placeholder.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-16f, 0f));

            TextMeshProUGUI text = GetOrCreateText(rect, "Text", initialText, 24f, TextAlignmentOptions.Center);
            ConfigureRect(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-16f, 0f));
            input.placeholder = placeholder;
            input.textComponent = text;
            if (string.IsNullOrWhiteSpace(input.text))
            {
                input.text = initialText;
            }

            return input;
        }

        private void ApplyResponsiveLayout(GmToolResponsiveMetrics metrics)
        {
            if (_panel == null)
            {
                return;
            }

            ConfigureRect(_panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, metrics.PanelSize);
            ConfigureRect(_header, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -metrics.PanelPadding), new Vector2(-metrics.PanelPadding * 2f, metrics.HeaderHeight));
            ConfigureRect(_titleText.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(-(metrics.CloseButtonWidth + metrics.SectionSpacing), 0f));
            _titleText.fontSize = metrics.TitleFontSize;
            ConfigureRect(_closeButton.transform as RectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(metrics.CloseButtonWidth, metrics.HeaderHeight));
            ApplyButtonLayout(_closeButton, metrics.HeaderHeight, metrics.ButtonFontSize);

            RectTransform scrollRectTransform = _scrollRect != null ? _scrollRect.transform as RectTransform : null;
            if (scrollRectTransform != null)
            {
                scrollRectTransform.anchorMin = Vector2.zero;
                scrollRectTransform.anchorMax = Vector2.one;
                scrollRectTransform.pivot = new Vector2(0.5f, 0.5f);
                scrollRectTransform.offsetMin = new Vector2(metrics.PanelPadding, metrics.PanelPadding);
                scrollRectTransform.offsetMax = new Vector2(-metrics.PanelPadding, -(metrics.HeaderHeight + metrics.PanelPadding * 2f));
                scrollRectTransform.localScale = Vector3.one;
            }

            ApplyContentLayout(metrics);
            ApplyCardLayout(_quickCard, metrics, metrics.QuickCardHeight);
            ApplyCardLayout(_tutorialCard, metrics, metrics.TutorialCardHeight);
            ApplyCardLayout(_runtimeCard, metrics, metrics.RuntimeCardHeight);
            ApplyGridLayout(_quickActionsRow, metrics.ActionColumns, metrics.ActionCellWidth, metrics.ButtonHeight, metrics.SectionSpacing, 3);
            ApplyGridLayout(_inputRow, metrics.InputColumns, metrics.InputCellWidth, metrics.ButtonHeight, metrics.SectionSpacing, 2);
            ApplyButtonLayout(_addEnergyButton, metrics.ButtonHeight, metrics.ButtonFontSize);
            ApplyButtonLayout(_addGoldButton, metrics.ButtonHeight, metrics.ButtonFontSize);
            ApplyButtonLayout(_replayHelpButton, metrics.ButtonHeight, metrics.ButtonFontSize);
            ConfigureInputLayout(_stepInput, metrics.ButtonHeight, metrics.BodyFontSize);
            ApplyButtonLayout(_startAtStepButton, metrics.ButtonHeight, metrics.ButtonFontSize);
            ApplyTextLayout(_tutorialProgressText, metrics.BodyFontSize, metrics.ProgressTextHeight);
            ApplyTextLayout(_tutorialHintText, metrics.HintFontSize, metrics.HintTextHeight);
            ApplyTextLayout(_runtimeSummaryText, metrics.BodyFontSize, metrics.RuntimeTextHeight);
            ApplyTextLayout(_mainStatusText, metrics.HintFontSize, metrics.MainStatusTextHeight);
            ApplyTextLayout(_statusText, metrics.HintFontSize, metrics.StatusTextHeight);
        }

        private void ApplyContentLayout(GmToolResponsiveMetrics metrics)
        {
            if (_content == null)
            {
                return;
            }

            VerticalLayoutGroup layout = _content.GetComponent<VerticalLayoutGroup>() ?? _content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = Mathf.RoundToInt(metrics.SectionSpacing);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        private static void ApplyCardLayout(RectTransform card, GmToolResponsiveMetrics metrics, float height)
        {
            if (card == null)
            {
                return;
            }

            VerticalLayoutGroup layout = card.GetComponent<VerticalLayoutGroup>() ?? card.gameObject.AddComponent<VerticalLayoutGroup>();
            int horizontalPadding = Mathf.RoundToInt(metrics.CardPadding);
            int verticalPadding = Mathf.RoundToInt(metrics.CardPadding * 0.82f);
            layout.padding = new RectOffset(horizontalPadding, horizontalPadding, verticalPadding, verticalPadding);
            layout.spacing = Mathf.RoundToInt(metrics.CardSpacing);
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            LayoutElement element = card.gameObject.GetComponent<LayoutElement>() ?? card.gameObject.AddComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
            element.flexibleWidth = 1f;
            element.flexibleHeight = 0f;
        }

        private static void ApplyGridLayout(RectTransform row, int columns, float cellWidth, float cellHeight, float spacing, int childCount)
        {
            if (row == null)
            {
                return;
            }

            GridLayoutGroup grid = row.GetComponent<GridLayoutGroup>() ?? row.gameObject.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = Mathf.Max(1, columns);
            grid.cellSize = new Vector2(cellWidth, cellHeight);
            grid.spacing = new Vector2(spacing, spacing);
            grid.childAlignment = TextAnchor.UpperLeft;

            int rowCount = Mathf.CeilToInt(childCount / (float)Mathf.Max(1, columns));
            LayoutElement element = row.gameObject.GetComponent<LayoutElement>() ?? row.gameObject.AddComponent<LayoutElement>();
            element.minHeight = rowCount * cellHeight + Mathf.Max(0, rowCount - 1) * spacing;
            element.preferredHeight = element.minHeight;
            element.minWidth = cellWidth * columns + Mathf.Max(0, columns - 1) * spacing;
            element.preferredWidth = element.minWidth;
            element.flexibleWidth = 0f;
            element.flexibleHeight = 0f;
        }

        private static void ApplyTextLayout(TextMeshProUGUI text, float fontSize, float height)
        {
            if (text == null)
            {
                return;
            }

            text.fontSize = fontSize;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Ellipsis;
            LayoutElement element = text.gameObject.GetComponent<LayoutElement>() ?? text.gameObject.AddComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
            element.flexibleWidth = 1f;
        }

        private static GmToolResponsiveMetrics ComputeResponsiveMetrics()
        {
            float pixelWidth = Mathf.Max(1f, Screen.width);
            float pixelHeight = Mathf.Max(1f, Screen.height);
            float aspect = pixelWidth / pixelHeight;
            float canvasHeight = 1920f;
            float canvasWidth = Mathf.Clamp(canvasHeight * aspect, 640f, 2560f);
            float safeMargin = canvasWidth < 820f ? 28f : 52f;
            float maxPanelWidth = Mathf.Max(420f, canvasWidth - safeMargin * 2f);
            float panelWidth = Mathf.Clamp(canvasWidth * (aspect > 0.95f ? 0.72f : 0.86f), 520f, Mathf.Min(1120f, maxPanelWidth));
            float panelHeight = Mathf.Clamp(canvasHeight * (aspect > 0.95f ? 0.78f : 0.88f), 980f, canvasHeight - 96f);
            float panelPadding = Mathf.Clamp(panelWidth * 0.045f, 24f, 40f);
            float cardPadding = Mathf.Clamp(panelWidth * 0.03f, 18f, 28f);
            float spacing = Mathf.Clamp(panelWidth * 0.018f, 12f, 20f);
            float headerHeight = Mathf.Clamp(panelHeight * 0.062f, 72f, 96f);
            float contentWidth = Mathf.Max(320f, panelWidth - panelPadding * 2f - cardPadding * 2f);
            int actionColumns = contentWidth >= 760f ? 3 : contentWidth >= 500f ? 2 : 1;
            int inputColumns = contentWidth >= 620f ? 2 : 1;
            float actionCellWidth = Mathf.Floor((contentWidth - spacing * Mathf.Max(0, actionColumns - 1)) / actionColumns);
            float inputCellWidth = Mathf.Floor((contentWidth - spacing * Mathf.Max(0, inputColumns - 1)) / inputColumns);
            float buttonHeight = Mathf.Clamp(panelHeight * 0.044f, 58f, 72f);
            float titleFontSize = Mathf.Clamp(panelWidth * 0.04f, 28f, 36f);
            float bodyFontSize = Mathf.Clamp(panelWidth * 0.025f, 18f, 23f);
            float hintFontSize = Mathf.Clamp(panelWidth * 0.022f, 16f, 21f);
            float buttonFontSize = Mathf.Clamp(panelWidth * 0.026f, 18f, 24f);
            float titleLineHeight = Mathf.Clamp(titleFontSize * 1.35f, 38f, 50f);
            float quickRows = Mathf.CeilToInt(3f / actionColumns);
            float inputRows = Mathf.CeilToInt(2f / inputColumns);
            float progressTextHeight = Mathf.Clamp(bodyFontSize * 5.8f, 112f, 142f);
            float hintTextHeight = Mathf.Clamp(hintFontSize * 3.2f, 58f, 76f);
            float runtimeTextHeight = Mathf.Clamp(bodyFontSize * 4.8f, 92f, 126f);
            float mainStatusTextHeight = Mathf.Clamp(hintFontSize * 3.1f, 56f, 82f);
            float statusTextHeight = Mathf.Clamp(hintFontSize * 3.4f, 64f, 88f);
            float quickCardHeight = cardPadding * 2f + titleLineHeight + spacing + quickRows * buttonHeight + Mathf.Max(0f, quickRows - 1f) * spacing;
            float tutorialCardHeight = cardPadding * 2f + titleLineHeight + spacing + progressTextHeight + spacing + hintTextHeight + spacing + inputRows * buttonHeight + Mathf.Max(0f, inputRows - 1f) * spacing;
            float runtimeCardHeight = cardPadding * 2f + titleLineHeight + spacing + runtimeTextHeight + spacing + mainStatusTextHeight + spacing + statusTextHeight;
            return new GmToolResponsiveMetrics
            {
                PanelSize = new Vector2(panelWidth, panelHeight),
                PanelPadding = panelPadding,
                CardPadding = cardPadding,
                SectionSpacing = spacing,
                CardSpacing = Mathf.Clamp(spacing * 0.72f, 8f, 14f),
                HeaderHeight = headerHeight,
                CloseButtonWidth = Mathf.Clamp(panelWidth * 0.17f, 112f, 160f),
                ButtonHeight = buttonHeight,
                TitleFontSize = titleFontSize,
                BodyFontSize = bodyFontSize,
                HintFontSize = hintFontSize,
                ButtonFontSize = buttonFontSize,
                ActionColumns = actionColumns,
                InputColumns = inputColumns,
                ActionCellWidth = actionCellWidth,
                InputCellWidth = inputCellWidth,
                QuickCardHeight = quickCardHeight,
                TutorialCardHeight = tutorialCardHeight,
                RuntimeCardHeight = runtimeCardHeight,
                ProgressTextHeight = progressTextHeight,
                HintTextHeight = hintTextHeight,
                RuntimeTextHeight = runtimeTextHeight,
                MainStatusTextHeight = mainStatusTextHeight,
                StatusTextHeight = statusTextHeight,
            };
        }

        private static TextMeshProUGUI GetOrCreateText(RectTransform parent, string objectName, string textValue, float fontSize, TextAlignmentOptions alignment)
        {
            if (parent == null)
            {
                return null;
            }

            GameObject textObject = GetOrCreateChild(parent, objectName);
            RectTransform rect = textObject.GetComponent<RectTransform>() ?? textObject.AddComponent<RectTransform>();
            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>() ?? textObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = alignment;
            text.enableWordWrapping = true;
            text.raycastTarget = false;
            TmpGlyphCoverageReporter.SetText(text, textValue);
            if (rect.anchorMin == Vector2.zero && rect.anchorMax == Vector2.zero && rect.sizeDelta == Vector2.zero)
            {
                ConfigureRect(rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            }

            return text;
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
                Object.Destroy(child.gameObject);
            }
            else
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }

        private static RectTransform GetOrCreateRect(string objectName, Transform parent)
        {
            GameObject childObject = GetOrCreateChild(parent, objectName);
            return childObject.GetComponent<RectTransform>() ?? childObject.AddComponent<RectTransform>();
        }

        private static GameObject GetOrCreateChild(Transform parent, string objectName)
        {
            Transform child = parent != null ? parent.Find(objectName) : null;
            if (child != null)
            {
                if (!(child is RectTransform))
                {
                    child.gameObject.name = objectName + "_LegacyTransform";
                    GameObject replacement = new GameObject(objectName, typeof(RectTransform));
                    replacement.transform.SetParent(parent, false);
                    replacement.transform.SetSiblingIndex(child.GetSiblingIndex());
                    if (UnityEngine.Application.isPlaying)
                    {
                        Object.Destroy(child.gameObject);
                    }
                    else
                    {
                        Object.DestroyImmediate(child.gameObject);
                    }

                    return replacement;
                }

                return child.gameObject;
            }

            GameObject childObject = new GameObject(objectName, typeof(RectTransform));
            childObject.transform.SetParent(parent, false);
            return childObject;
        }

        private static Image GetOrCreateImage(GameObject target)
        {
            return target.GetComponent<Image>() ?? target.AddComponent<Image>();
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

        private struct GmToolResponsiveMetrics
        {
            public Vector2 PanelSize;
            public float PanelPadding;
            public float CardPadding;
            public float SectionSpacing;
            public float CardSpacing;
            public float HeaderHeight;
            public float CloseButtonWidth;
            public float ButtonHeight;
            public float TitleFontSize;
            public float BodyFontSize;
            public float HintFontSize;
            public float ButtonFontSize;
            public int ActionColumns;
            public int InputColumns;
            public float ActionCellWidth;
            public float InputCellWidth;
            public float QuickCardHeight;
            public float TutorialCardHeight;
            public float RuntimeCardHeight;
            public float ProgressTextHeight;
            public float HintTextHeight;
            public float RuntimeTextHeight;
            public float MainStatusTextHeight;
            public float StatusTextHeight;
        }
    }
}
