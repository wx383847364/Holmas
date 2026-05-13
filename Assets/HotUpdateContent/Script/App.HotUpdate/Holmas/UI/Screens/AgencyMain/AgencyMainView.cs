using System;
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
        private Action<int> _currentTaskClaimAction;

        #if UNITY_EDITOR
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

            Text titleText = GetOrCreateText(contentRect, "TitleText", "侦探社", 44, FontStyle.Bold, TextAnchor.MiddleLeft);
            collector.RegisterOrReplace(
                AgencyMainBindings.TitleTextKey,
                titleText,
                nodePath: AgencyMainBindings.TitleTextNodePath);

            Text summaryText = GetOrCreateText(contentRect, "SummaryText", "侦探社概览", 28, FontStyle.Normal, TextAnchor.UpperLeft);
            summaryText.horizontalOverflow = HorizontalWrapMode.Wrap;
            summaryText.verticalOverflow = VerticalWrapMode.Overflow;
            collector.RegisterOrReplace(
                AgencyMainBindings.SummaryTextKey,
                summaryText,
                nodePath: AgencyMainBindings.SummaryTextNodePath);

            Text taskSummaryText = GetOrCreateText(contentRect, "TaskSummaryText", "任务概览", 24, FontStyle.Normal, TextAnchor.UpperLeft);
            taskSummaryText.horizontalOverflow = HorizontalWrapMode.Wrap;
            taskSummaryText.verticalOverflow = VerticalWrapMode.Overflow;
            collector.RegisterOrReplace(
                AgencyMainBindings.TaskSummaryTextKey,
                taskSummaryText,
                nodePath: AgencyMainBindings.TaskSummaryTextNodePath);

            Text boardSummaryText = GetOrCreateText(contentRect, "BoardSummaryText", "棋盘概览", 24, FontStyle.Normal, TextAnchor.UpperLeft);
            boardSummaryText.horizontalOverflow = HorizontalWrapMode.Wrap;
            boardSummaryText.verticalOverflow = VerticalWrapMode.Overflow;
            collector.RegisterOrReplace(
                AgencyMainBindings.BoardSummaryTextKey,
                boardSummaryText,
                nodePath: AgencyMainBindings.BoardSummaryTextNodePath);

            Text statusText = GetOrCreateText(contentRect, "StatusText", "状态", 24, FontStyle.Italic, TextAnchor.UpperLeft);
            statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
            statusText.verticalOverflow = VerticalWrapMode.Overflow;
            collector.RegisterOrReplace(
                AgencyMainBindings.StatusTextKey,
                statusText,
                nodePath: AgencyMainBindings.StatusTextNodePath);

            Button actionButton = GetOrCreateButton(contentRect, "PrimaryActionButton", "开始找猫");
            collector.RegisterOrReplace(
                AgencyMainBindings.PrimaryActionButtonKey,
                actionButton,
                AgencyMainBindings.PrimaryActionButtonClickEvent,
                AgencyMainBindings.PrimaryActionButtonNodePath);

            EnsureTaskSection(contentRect);
        }
        #endif

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
            ApplyPreferredFonts();
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

        public void SetTaskClaimAction(Action<int> action)
        {
            _currentTaskClaimAction = action;
        }

        public void Render(AgencyMainVm viewModel)
        {
            if (viewModel == null)
            {
                return;
            }

            if (_bindings?.TitleText != null)
            {
                RuntimeTmpFontResolver.EnsureFontSupportsText(_bindings.TitleText, viewModel.Title);
                _bindings.TitleText.text = viewModel.Title ?? string.Empty;
            }

            if (_bindings?.SummaryText != null)
            {
                RuntimeTmpFontResolver.EnsureFontSupportsText(_bindings.SummaryText, viewModel.Summary);
                _bindings.SummaryText.text = viewModel.Summary ?? string.Empty;
            }

            if (_bindings?.TaskSummaryText != null)
            {
                RuntimeTmpFontResolver.EnsureFontSupportsText(_bindings.TaskSummaryText, viewModel.TaskSummary);
                _bindings.TaskSummaryText.text = viewModel.TaskSummary ?? string.Empty;
            }

            if (_bindings?.BoardSummaryText != null)
            {
                RuntimeTmpFontResolver.EnsureFontSupportsText(_bindings.BoardSummaryText, viewModel.BoardSummary);
                _bindings.BoardSummaryText.text = viewModel.BoardSummary ?? string.Empty;
            }

            if (_bindings?.StatusText != null)
            {
                RuntimeTmpFontResolver.EnsureFontSupportsText(_bindings.StatusText, viewModel.Status);
                _bindings.StatusText.text = viewModel.Status ?? string.Empty;
            }

            if (_bindings?.PrimaryActionButton != null)
            {
                _bindings.PrimaryActionButton.interactable = viewModel.PrimaryActionEnabled;
                Text buttonLabel = _bindings.PrimaryActionButton.GetComponentInChildren<Text>();
                if (buttonLabel != null)
                {
                    RuntimeTmpFontResolver.EnsureFontSupportsText(buttonLabel, viewModel.PrimaryActionLabel);
                    buttonLabel.text = string.IsNullOrWhiteSpace(viewModel.PrimaryActionLabel)
                        ? "开始找猫"
                        : viewModel.PrimaryActionLabel;
                }
            }

            RenderTaskItems(viewModel.TaskItems ?? Array.Empty<AgencyMainTaskItemVm>());
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
            RuntimeTmpFontResolver.EnsureFontSupportsText(text, textValue);
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

        private void ApplyPreferredFonts()
        {
            if (_bindings?.TitleText != null)
            {
                RuntimeTmpFontResolver.EnsureFontSupportsText(_bindings.TitleText);
            }

            if (_bindings?.SummaryText != null)
            {
                RuntimeTmpFontResolver.EnsureFontSupportsText(_bindings.SummaryText);
            }

            if (_bindings?.TaskSummaryText != null)
            {
                RuntimeTmpFontResolver.EnsureFontSupportsText(_bindings.TaskSummaryText);
            }

            if (_bindings?.BoardSummaryText != null)
            {
                RuntimeTmpFontResolver.EnsureFontSupportsText(_bindings.BoardSummaryText);
            }

            if (_bindings?.StatusText != null)
            {
                RuntimeTmpFontResolver.EnsureFontSupportsText(_bindings.StatusText);
            }

            if (_bindings?.PrimaryActionButton != null)
            {
                Text buttonLabel = _bindings.PrimaryActionButton.GetComponentInChildren<Text>(true);
                if (buttonLabel != null)
                {
                    RuntimeTmpFontResolver.EnsureFontSupportsText(buttonLabel);
                }
            }
        }

        private void RenderTaskItems(AgencyMainTaskItemVm[] taskItems)
        {
            RectTransform contentRoot = GetOrCreateContentRoot();
            RectTransform taskSection = EnsureTaskSection(contentRoot);
            RectTransform rowRoot = GetOrCreateTaskRowRoot(taskSection);
            ClearChildren(rowRoot);

            for (int i = 0; i < taskItems.Length; i++)
            {
                AgencyMainTaskItemVm item = taskItems[i];
                if (item == null)
                {
                    continue;
                }

                CreateTaskRow(rowRoot, item);
            }
        }

        private RectTransform EnsureTaskSection(RectTransform contentRoot)
        {
            GameObject sectionObject = GetOrCreateChild(contentRoot, "TaskSection");
            RectTransform sectionRect = sectionObject.GetComponent<RectTransform>();
            if (sectionRect == null)
            {
                sectionRect = sectionObject.AddComponent<RectTransform>();
            }

            LayoutElement sectionLayout = sectionObject.GetComponent<LayoutElement>();
            if (sectionLayout == null)
            {
                sectionLayout = sectionObject.AddComponent<LayoutElement>();
            }

            sectionLayout.minHeight = 480f;
            sectionLayout.preferredHeight = -1f;

            VerticalLayoutGroup sectionGroup = sectionObject.GetComponent<VerticalLayoutGroup>();
            if (sectionGroup == null)
            {
                sectionGroup = sectionObject.AddComponent<VerticalLayoutGroup>();
            }

            sectionGroup.padding = new RectOffset(0, 0, 0, 0);
            sectionGroup.spacing = 14f;
            sectionGroup.childControlWidth = true;
            sectionGroup.childControlHeight = false;
            sectionGroup.childForceExpandWidth = true;
            sectionGroup.childForceExpandHeight = false;

            Text headerText = GetOrCreateText(sectionRect, "TaskSectionTitle", "当前任务", 26, FontStyle.Bold, TextAnchor.MiddleLeft);
            headerText.horizontalOverflow = HorizontalWrapMode.Wrap;
            headerText.verticalOverflow = VerticalWrapMode.Overflow;
            headerText.color = new Color(1f, 0.95f, 0.82f, 1f);

            GetOrCreateTaskRowRoot(sectionRect);
            return sectionRect;
        }

        private RectTransform GetOrCreateTaskRowRoot(Transform parent)
        {
            GameObject rowRootObject = GetOrCreateChild(parent, "TaskRows");
            RectTransform rowRoot = rowRootObject.GetComponent<RectTransform>();
            if (rowRoot == null)
            {
                rowRoot = rowRootObject.AddComponent<RectTransform>();
            }

            VerticalLayoutGroup layout = rowRootObject.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
            {
                layout = rowRootObject.AddComponent<VerticalLayoutGroup>();
            }

            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = 12f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return rowRoot;
        }

        private void CreateTaskRow(Transform parent, AgencyMainTaskItemVm item)
        {
            GameObject rowObject = new GameObject($"TaskRow_{item.SlotIndex + 1}", typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(HorizontalLayoutGroup));
            rowObject.transform.SetParent(parent, false);

            LayoutElement layout = rowObject.GetComponent<LayoutElement>();
            layout.minHeight = 104f;
            layout.preferredHeight = 104f;

            Image background = rowObject.GetComponent<Image>();
            background.color = item.IsLocked
                ? new Color(0.18f, 0.18f, 0.20f, 0.88f)
                : new Color(0.16f, 0.23f, 0.32f, 0.92f);

            HorizontalLayoutGroup rowLayout = rowObject.GetComponent<HorizontalLayoutGroup>();
            rowLayout.padding = new RectOffset(20, 20, 14, 14);
            rowLayout.spacing = 16f;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.childControlWidth = false;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;

            RectTransform textColumn = CreateTaskTextColumn(rowObject.transform);
            Text titleText = GetOrCreateText(textColumn, "Title", item.Title, 24, FontStyle.Bold, TextAnchor.MiddleLeft);
            titleText.color = Color.white;
            Text progressText = GetOrCreateText(textColumn, "Progress", item.Progress, 20, FontStyle.Normal, TextAnchor.MiddleLeft);
            progressText.color = new Color(0.86f, 0.91f, 0.98f, 1f);
            Text rewardText = GetOrCreateText(textColumn, "Reward", item.Reward, 20, FontStyle.Normal, TextAnchor.MiddleLeft);
            rewardText.color = new Color(0.99f, 0.86f, 0.43f, 1f);

            Button claimButton = GetOrCreateButton(rowObject.transform, "ClaimButton", item.ClaimButtonLabel);
            RectTransform claimButtonRect = claimButton.GetComponent<RectTransform>();
            if (claimButtonRect != null)
            {
                claimButtonRect.sizeDelta = new Vector2(220f, 72f);
            }

            claimButton.interactable = item.ClaimButtonEnabled;
            Image claimButtonImage = claimButton.GetComponent<Image>();
            if (claimButtonImage != null)
            {
                claimButtonImage.color = item.ClaimButtonEnabled
                    ? new Color(0.93f, 0.66f, 0.20f, 1f)
                    : new Color(0.37f, 0.41f, 0.48f, 1f);
            }

            Text claimLabel = claimButton.GetComponentInChildren<Text>();
            if (claimLabel != null)
            {
                claimLabel.text = item.ClaimButtonLabel;
            }

            int slotIndex = item.SlotIndex;
            claimButton.onClick.RemoveAllListeners();
            claimButton.onClick.AddListener(() =>
            {
                if (_currentTaskClaimAction != null)
                {
                    _currentTaskClaimAction(slotIndex);
                }
            });
        }

        private RectTransform CreateTaskTextColumn(Transform parent)
        {
            GameObject columnObject = new GameObject("TextColumn", typeof(RectTransform), typeof(LayoutElement), typeof(VerticalLayoutGroup));
            columnObject.transform.SetParent(parent, false);

            LayoutElement layout = columnObject.GetComponent<LayoutElement>();
            layout.flexibleWidth = 1f;
            layout.minWidth = 0f;

            VerticalLayoutGroup group = columnObject.GetComponent<VerticalLayoutGroup>();
            group.padding = new RectOffset(0, 0, 0, 0);
            group.spacing = 6f;
            group.childControlWidth = true;
            group.childControlHeight = false;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;
            return columnObject.GetComponent<RectTransform>();
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.Destroy(parent.GetChild(i).gameObject);
            }
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
