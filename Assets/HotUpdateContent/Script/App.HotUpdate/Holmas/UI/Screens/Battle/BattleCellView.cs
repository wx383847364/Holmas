using System;
using App.HotUpdate.Holmas.Board;
using App.HotUpdate.Holmas.UI.Tool;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace App.HotUpdate.Holmas.UI.Screens.Battle
{
    [RequireComponent(typeof(Image))]
    public sealed class BattleCellView : MonoBehaviour, IPointerClickHandler
    {
        private BoardCellState _state;
        private Action<int, bool> _onInteract;
        private Image _background;
        private TextMeshProUGUI _label;

        private void Awake()
        {
            _background = GetComponent<Image>();
            if (_background == null)
            {
                _background = gameObject.AddComponent<Image>();
            }

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
        }

        public void Bind(BoardCellState state, Action<int, bool> onInteract)
        {
            _state = state;
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
            if (_background == null || _label == null)
            {
                return;
            }

            if (!_state.IsValid)
            {
                _background.color = new Color(0.18f, 0.18f, 0.2f, 0.45f);
                TmpGlyphCoverageReporter.SetText(_label, string.Empty);
                _background.raycastTarget = false;
                return;
            }

            _background.raycastTarget = true;

            if (_state.IsFlagged && !_state.IsRevealed)
            {
                _background.color = new Color(0.86f, 0.35f, 0.35f, 0.95f);
                TmpGlyphCoverageReporter.SetText(_label, "F");
                _label.color = Color.white;
                return;
            }

            if (!_state.IsRevealed)
            {
                Color32 blockColor = _state.BlockColor;
                _background.color = new Color(blockColor.r / 255f, blockColor.g / 255f, blockColor.b / 255f, 1f);
                TmpGlyphCoverageReporter.SetText(_label, string.Empty);
                return;
            }

            if (_state.HasCat)
            {
                _background.color = new Color(0.97f, 0.62f, 0.24f, 1f);
                TmpGlyphCoverageReporter.SetText(_label, "猫");
                _label.color = Color.white;
                return;
            }

            _background.color = new Color(0.95f, 0.95f, 0.97f, 1f);
            TmpGlyphCoverageReporter.SetText(_label, _state.AdjacentCatCount > 0 ? _state.AdjacentCatCount.ToString() : string.Empty);
            _label.color = GetNumberColor(_state.AdjacentCatCount);
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
