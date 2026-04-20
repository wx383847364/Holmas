using System;
using System.Collections.Generic;
using App.HotUpdate.Holmas.Board;
using App.HotUpdate.Holmas.UI.Binding;
using App.HotUpdate.Holmas.UI.Tool;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace App.HotUpdate.Holmas.UI.Screens.Battle
{
    public sealed class BattleView : MonoBehaviour
    {
        private readonly List<BattleCellView> _cells = new List<BattleCellView>();
        private BattleBindings _bindings;
        private UnityAction _currentBackAction;
        private UnityAction _currentAddEnergyAction;
        private Action<int, bool> _currentCellAction;

        public void EnsureBindingSurface()
        {
            gameObject.name = BattleBindings.RootNodePath;

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
            collector.RegisterOrReplace(BattleBindings.RootPanelKey, rootRect, nodePath: BattleBindings.RootNodePath);

            RectTransform overlay = GetOrCreateOverlayRoot();
            Button backButton = ResolveBackButton(overlay);
            TextMeshProUGUI levelText = ResolveLevelText(overlay);
            TextMeshProUGUI goldText = ResolveGoldText(overlay);
            TextMeshProUGUI energyText = ResolveEnergyText(overlay);
            TextMeshProUGUI summaryText = GetOrCreateRuntimeText(
                overlay,
                "SummaryText",
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -112f),
                new Vector2(-260f, 112f),
                24f,
                TextAlignmentOptions.Center);
            TextMeshProUGUI statusText = GetOrCreateRuntimeText(
                overlay,
                "StatusText",
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 60f),
                new Vector2(-200f, 100f),
                26f,
                TextAlignmentOptions.Center);
            Button addEnergyButton = GetOrCreateRuntimeButton(
                overlay,
                "AddEnergyButton",
                "+5体力",
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-64f, -150f),
                new Vector2(180f, 64f),
                new Color(0.95f, 0.58f, 0.16f, 0.95f));
            RectTransform boardContainer = GetOrCreateBoardContainer(overlay);

            collector.RegisterOrReplace(
                BattleBindings.BackButtonKey,
                backButton,
                BattleBindings.ButtonClickEvent,
                BattleBindings.BackButtonNodePath);
            collector.RegisterOrReplace(BattleBindings.LevelTextKey, levelText, nodePath: BattleBindings.LevelTextNodePath);
            collector.RegisterOrReplace(BattleBindings.GoldTextKey, goldText, nodePath: BattleBindings.GoldTextNodePath);
            collector.RegisterOrReplace(BattleBindings.EnergyTextKey, energyText, nodePath: BattleBindings.EnergyTextNodePath);
            collector.RegisterOrReplace(BattleBindings.SummaryTextKey, summaryText, nodePath: BattleBindings.SummaryTextNodePath);
            collector.RegisterOrReplace(BattleBindings.StatusTextKey, statusText, nodePath: BattleBindings.StatusTextNodePath);
            collector.RegisterOrReplace(
                BattleBindings.AddEnergyButtonKey,
                addEnergyButton,
                BattleBindings.ButtonClickEvent,
                BattleBindings.AddEnergyButtonNodePath);
            collector.RegisterOrReplace(BattleBindings.BoardContainerKey, boardContainer, nodePath: BattleBindings.BoardContainerNodePath);
        }

        public void Bind(BattleBindings bindings)
        {
            _bindings = bindings ?? new BattleBindings();
        }

        public void SetBackAction(UnityAction action)
        {
            if (_bindings?.BackButton == null)
            {
                _currentBackAction = action;
                return;
            }

            if (_currentBackAction != null)
            {
                _bindings.BackButton.onClick.RemoveListener(_currentBackAction);
            }

            _currentBackAction = action;
            if (_currentBackAction != null)
            {
                _bindings.BackButton.onClick.AddListener(_currentBackAction);
            }
        }

        public void SetCellAction(Action<int, bool> action)
        {
            _currentCellAction = action;
        }

        public void SetAddEnergyAction(UnityAction action)
        {
            if (_bindings?.AddEnergyButton == null)
            {
                _currentAddEnergyAction = action;
                return;
            }

            if (_currentAddEnergyAction != null)
            {
                _bindings.AddEnergyButton.onClick.RemoveListener(_currentAddEnergyAction);
            }

            _currentAddEnergyAction = action;
            if (_currentAddEnergyAction != null)
            {
                _bindings.AddEnergyButton.onClick.AddListener(_currentAddEnergyAction);
            }
        }

        public void Render(BattleVm viewModel)
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

            if (_bindings?.EnergyText != null)
            {
                TmpGlyphCoverageReporter.SetText(_bindings.EnergyText, viewModel.EnergyLabel);
            }

            if (_bindings?.SummaryText != null)
            {
                TmpGlyphCoverageReporter.SetText(_bindings.SummaryText, viewModel.Summary);
            }

            if (_bindings?.StatusText != null)
            {
                TmpGlyphCoverageReporter.SetText(_bindings.StatusText, viewModel.Status);
            }

            if (_bindings?.AddEnergyButton != null)
            {
                _bindings.AddEnergyButton.interactable = viewModel.AddEnergyButtonEnabled;
                SetButtonLabel(_bindings.AddEnergyButton, viewModel.AddEnergyButtonLabel);
            }

            RenderBoard(viewModel);
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

            background.color = new Color(0.11f, 0.15f, 0.19f, 0.96f);
        }

        private RectTransform GetOrCreateOverlayRoot()
        {
            Transform existing = transform.Find(BattleBindings.RuntimeOverlayNodeName);
            GameObject overlayObject = existing != null ? existing.gameObject : new GameObject(BattleBindings.RuntimeOverlayNodeName, typeof(RectTransform));
            overlayObject.transform.SetParent(transform, false);

            RectTransform overlayRect = overlayObject.GetComponent<RectTransform>();
            Stretch(overlayRect);
            return overlayRect;
        }

        private RectTransform GetOrCreateBoardContainer(Transform parent)
        {
            GameObject boardObject = GetOrCreateChild(parent, "BoardContainer");
            RectTransform rectTransform = boardObject.GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                rectTransform = boardObject.AddComponent<RectTransform>();
            }

            ConfigureRect(
                rectTransform,
                new Vector2(0.08f, 0.17f),
                new Vector2(0.92f, 0.75f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero);

            Image background = boardObject.GetComponent<Image>();
            if (background == null)
            {
                background = boardObject.AddComponent<Image>();
            }

            background.color = new Color(0f, 0f, 0f, 0.18f);

            GridLayoutGroup layout = boardObject.GetComponent<GridLayoutGroup>();
            if (layout == null)
            {
                layout = boardObject.AddComponent<GridLayoutGroup>();
            }

            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.spacing = new Vector2(4f, 4f);
            layout.childAlignment = TextAnchor.MiddleCenter;
            return rectTransform;
        }

        private void RenderBoard(BattleVm viewModel)
        {
            if (_bindings?.BoardContainer == null)
            {
                return;
            }

            GridLayoutGroup layout = _bindings.BoardContainer.GetComponent<GridLayoutGroup>();
            int rows = Mathf.Max(0, viewModel.Rows);
            int cols = Mathf.Max(0, viewModel.Cols);

            if (rows <= 0 || cols <= 0 || viewModel.Cells == null)
            {
                for (int i = 0; i < _cells.Count; i++)
                {
                    _cells[i].gameObject.SetActive(false);
                }
                return;
            }

            layout.constraintCount = cols;
            float width = _bindings.BoardContainer.rect.width;
            float height = _bindings.BoardContainer.rect.height;
            if (width <= 0f)
            {
                width = 720f;
            }

            if (height <= 0f)
            {
                height = 960f;
            }

            float spacingX = layout.spacing.x;
            float spacingY = layout.spacing.y;
            float cellWidth = (width - (cols - 1) * spacingX) / cols;
            float cellHeight = (height - (rows - 1) * spacingY) / rows;
            float cellSize = Mathf.Max(18f, Mathf.Min(cellWidth, cellHeight));
            layout.cellSize = new Vector2(cellSize, cellSize);

            SetCellCount(viewModel.Cells.Count);
            for (int i = 0; i < _cells.Count; i++)
            {
                if (i < viewModel.Cells.Count)
                {
                    _cells[i].gameObject.SetActive(true);
                    _cells[i].Bind(viewModel.Cells[i], _currentCellAction);
                }
                else
                {
                    _cells[i].gameObject.SetActive(false);
                }
            }
        }

        private void SetCellCount(int requiredCount)
        {
            while (_cells.Count < requiredCount)
            {
                GameObject cellObject = new GameObject($"Cell_{_cells.Count}", typeof(RectTransform), typeof(Image), typeof(BattleCellView));
                cellObject.transform.SetParent(_bindings.BoardContainer, false);
                BattleCellView cellView = cellObject.GetComponent<BattleCellView>();
                _cells.Add(cellView);
            }
        }

        private Button ResolveBackButton(Transform overlay)
        {
            Button existing = FindDescendantComponent<Button>("Back_btn") ?? FindFirstDescendantByName<Button>("Back_btn");
            if (existing != null)
            {
                return existing;
            }

            return GetOrCreateRuntimeButton(
                overlay,
                "BackButton",
                "返回",
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(48f, -52f),
                new Vector2(180f, 84f),
                new Color(0.22f, 0.24f, 0.31f, 0.9f));
        }

        private TextMeshProUGUI ResolveLevelText(Transform overlay)
        {
            TextMeshProUGUI existing = FindDescendantComponent<TextMeshProUGUI>("Headicon_btn/Level") ?? FindFirstDescendantByName<TextMeshProUGUI>("Level");
            return existing ?? GetOrCreateRuntimeText(
                overlay,
                "LevelText",
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(250f, -54f),
                new Vector2(180f, 72f),
                28f,
                TextAlignmentOptions.Left);
        }

        private TextMeshProUGUI ResolveGoldText(Transform overlay)
        {
            TextMeshProUGUI existing = FindDescendantComponent<TextMeshProUGUI>("Money_btn/Text (TMP)") ?? FindFirstDescendantByName<TextMeshProUGUI>("Text (TMP)");
            return existing ?? GetOrCreateRuntimeText(
                overlay,
                "GoldText",
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-48f, -54f),
                new Vector2(220f, 72f),
                28f,
                TextAlignmentOptions.Right);
        }

        private TextMeshProUGUI ResolveEnergyText(Transform overlay)
        {
            TextMeshProUGUI existing = FindDescendantComponent<TextMeshProUGUI>("Energy_btn/Text (TMP)")
                ?? FindFirstDescendantByName<TextMeshProUGUI>("EnergyCount");
            if (existing != null)
            {
                existing.raycastTarget = false;
                return existing;
            }

            return GetOrCreateRuntimeText(
                overlay,
                "EnergyText",
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-64f, -96f),
                new Vector2(220f, 64f),
                28f,
                TextAlignmentOptions.Right);
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
                30f,
                TextAlignmentOptions.Center);
            TmpGlyphCoverageReporter.SetText(labelText, label);
            return button;
        }

        private static void SetButtonLabel(Button button, string label)
        {
            if (button == null)
            {
                return;
            }

            TextMeshProUGUI tmp = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp != null)
            {
                TmpGlyphCoverageReporter.SetText(tmp, string.IsNullOrWhiteSpace(label) ? button.name : label);
                return;
            }

            Text legacy = button.GetComponentInChildren<Text>(true);
            if (legacy != null)
            {
                legacy.text = string.IsNullOrWhiteSpace(label) ? button.name : label;
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
