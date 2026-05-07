using System;
using App.HotUpdate.Holmas.Board;
using App.HotUpdate.Holmas.UI.Core;
using App.HotUpdate.Holmas.UI.Tool;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace App.HotUpdate.Holmas.UI.Screens.FindCat
{
    [RequireComponent(typeof(Image))]
    public sealed class FindCatCellView : MonoBehaviour, IPointerClickHandler
    {
        private BoardCellState _state;
        private Action<int, bool> _onInteract;
        private Image _background;
        private Outline _outline;
        private TextMeshProUGUI _label;
        private Image _catIcon;
        private HolmasCatVisualVm _catVisual;
        private HolmasCatSpriteLoader _catSpriteLoader;

        private void Awake()
        {
            _background = GetComponent<Image>();
            if (_background == null)
            {
                _background = gameObject.AddComponent<Image>();
            }

            _outline = GetComponent<Outline>();
            if (_outline == null)
            {
                _outline = gameObject.AddComponent<Outline>();
            }

            _outline.effectDistance = new Vector2(2f, -2f);
            _outline.useGraphicAlpha = false;

            GameObject labelObject = transform.Find("Label") != null
                ? transform.Find("Label").gameObject
                : new GameObject("Label", typeof(RectTransform));
            labelObject.transform.SetParent(transform, false);

            RectTransform rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            _label = labelObject.GetComponent<TextMeshProUGUI>();
            if (_label == null)
            {
                _label = labelObject.AddComponent<TextMeshProUGUI>();
            }

            _label.alignment = TextAlignmentOptions.Center;
            _label.raycastTarget = false;
            _label.fontSize = 28f;
            _label.enableAutoSizing = true;
            _label.fontSizeMin = 14f;
            _label.fontSizeMax = 28f;

            GameObject iconObject = transform.Find("CatIcon") != null
                ? transform.Find("CatIcon").gameObject
                : new GameObject("CatIcon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(transform, false);

            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.18f, 0.18f);
            iconRect.anchorMax = new Vector2(0.82f, 0.82f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;

            _catIcon = iconObject.GetComponent<Image>();
            _catIcon.enabled = false;
            _catIcon.raycastTarget = false;
            _catIcon.preserveAspect = true;
        }

        public void Bind(
            BoardCellState state,
            HolmasCatVisualVm catVisual,
            HolmasCatSpriteLoader catSpriteLoader,
            Action<int, bool> onInteract)
        {
            _state = state;
            _catVisual = catVisual;
            _catSpriteLoader = catSpriteLoader;
            _onInteract = onInteract;
            Refresh();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_state.IsValid)
            {
                return;
            }

            bool isFlagAction = eventData != null && eventData.button == PointerEventData.InputButton.Right;
            _onInteract?.Invoke(_state.CellIndex, isFlagAction);
        }

        private void Refresh()
        {
            if (_background == null || _label == null || _catIcon == null)
            {
                return;
            }

            _catIcon.enabled = false;
            _catSpriteLoader?.Clear(_catIcon);

            if (!_state.IsValid)
            {
                _background.color = new Color(0.11f, 0.13f, 0.16f, 0.32f);
                TmpGlyphCoverageReporter.SetText(_label, string.Empty);
                _background.raycastTarget = false;
                SetOutline(false, Color.clear);
                return;
            }

            _background.raycastTarget = true;

            if (_state.IsFlagged && !_state.IsRevealed)
            {
                _background.color = new Color(0.86f, 0.35f, 0.35f, 0.95f);
                TmpGlyphCoverageReporter.SetText(_label, "F");
                _label.color = Color.white;
                SetOutline(true, new Color(1f, 0.86f, 0.38f, 1f));
                return;
            }

            if (!_state.IsRevealed)
            {
                Color32 blockColor = _state.BlockColor;
                _background.color = Color.Lerp(
                    new Color(blockColor.r / 255f, blockColor.g / 255f, blockColor.b / 255f, 1f),
                    new Color(0.18f, 0.42f, 0.74f, 1f),
                    0.32f);
                TmpGlyphCoverageReporter.SetText(_label, "?");
                _label.color = new Color(0.9f, 0.96f, 1f, 1f);
                SetOutline(true, new Color(0.67f, 0.86f, 1f, 1f));
                return;
            }

            if (_state.HasCat)
            {
                _background.color = new Color(0.97f, 0.62f, 0.24f, 1f);
                TmpGlyphCoverageReporter.SetText(_label, string.Empty);
                _catIcon.enabled = true;
                _catSpriteLoader?.Bind(_catIcon, _catVisual ?? HolmasCatVisualVm.CreateFallback(_state.CatId));
                SetOutline(true, new Color(1f, 0.8f, 0.38f, 1f));
                return;
            }

            _background.color = new Color(0.95f, 0.95f, 0.97f, 1f);
            TmpGlyphCoverageReporter.SetText(_label, _state.AdjacentCatCount > 0 ? _state.AdjacentCatCount.ToString() : string.Empty);
            _label.color = GetNumberColor(_state.AdjacentCatCount);
            SetOutline(false, Color.clear);
        }

        private void SetOutline(bool enabled, Color effectColor)
        {
            if (_outline == null)
            {
                return;
            }

            _outline.enabled = enabled;
            _outline.effectColor = effectColor;
        }

        private static Color GetNumberColor(int count)
        {
            switch (count)
            {
                case 1: return new Color(0.18f, 0.35f, 0.92f);
                case 2: return new Color(0.12f, 0.56f, 0.18f);
                case 3: return new Color(0.84f, 0.15f, 0.18f);
                case 4: return new Color(0.2f, 0.2f, 0.5f);
                case 5: return new Color(0.53f, 0.2f, 0.12f);
                case 6: return new Color(0.13f, 0.5f, 0.5f);
                case 7: return Color.black;
                case 8: return Color.gray;
                default: return new Color(0.24f, 0.24f, 0.24f);
            }
        }
    }
}
