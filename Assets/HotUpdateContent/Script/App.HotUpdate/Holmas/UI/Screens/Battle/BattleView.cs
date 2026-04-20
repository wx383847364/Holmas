using System;
using App.HotUpdate.Holmas.UI.Binding;
using App.HotUpdate.Holmas.UI.Core;
using App.HotUpdate.Holmas.UI.Screens.FindCat;
using App.HotUpdate.Holmas.UI.Tool;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace App.HotUpdate.Holmas.UI.Screens.Battle
{
    public sealed class BattleView : MonoBehaviour
    {
        private BattleBindings _bindings;
        private FindCatBoardView _boardView;
        private UnityAction _currentBackAction;
        private Action<int, bool> _currentCellAction;
        private HolmasCatSpriteLoader _catSpriteLoader;

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

        public void SetAssetsRuntime(App.Shared.Contracts.IAssetsRuntime assetsRuntime)
        {
            if (_catSpriteLoader == null)
            {
                _catSpriteLoader = new HolmasCatSpriteLoader(assetsRuntime);
            }
            else
            {
                _catSpriteLoader.SetAssetsRuntime(assetsRuntime);
            }

            if (_boardView != null)
            {
                _boardView.SetCatSpriteLoader(_catSpriteLoader);
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

            _boardView = boardObject.GetComponent<FindCatBoardView>() ?? boardObject.AddComponent<FindCatBoardView>();
            _boardView.SetCatSpriteLoader(_catSpriteLoader);
            return rectTransform;
        }

        private void RenderBoard(BattleVm viewModel)
        {
            if (_bindings?.BoardContainer == null)
            {
                return;
            }

            FindCatBoardView boardView = _boardView ?? _bindings.BoardContainer.GetComponent<FindCatBoardView>() ?? _bindings.BoardContainer.gameObject.AddComponent<FindCatBoardView>();
            _boardView = boardView;
            boardView.SetCatSpriteLoader(_catSpriteLoader);
            boardView.Render(viewModel.Rows, viewModel.Cols, viewModel.Cells, viewModel.CatVisuals, _currentCellAction);
        }

        private void OnDestroy()
        {
            _catSpriteLoader?.Dispose();
            _catSpriteLoader = null;
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
