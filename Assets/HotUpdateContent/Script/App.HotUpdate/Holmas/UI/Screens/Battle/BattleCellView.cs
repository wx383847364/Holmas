using System;
using App.HotUpdate.Holmas.Board;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace App.HotUpdate.Holmas.UI.Screens.Battle
{
    [RequireComponent(typeof(Image))]
    [RequireComponent(typeof(Button))]
    public sealed class BattleCellView : MonoBehaviour, IPointerClickHandler
    {
        private BoardCellState _state;
        private Action<int, bool> _onInteract;
        private Image _background;
        private Button _button;
        private TextMeshProUGUI _label;

        private void Awake()
        {
            _background = GetComponent<Image>();
            if (_background == null)
            {
                _background = gameObject.AddComponent<Image>();
            }

            _button = GetComponent<Button>();
            if (_button == null)
            {
                _button = gameObject.AddComponent<Button>();
            }

            _button.transition = Selectable.Transition.None;
            _button.targetGraphic = _background;
            _button.onClick.RemoveListener(HandlePrimaryClick);
            _button.onClick.AddListener(HandlePrimaryClick);

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
            gameObject.name = $"Cell_{state.CellIndex}";
            Refresh();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_state.IsValid)
            {
                Debug.Log($"BattleCellView: ignored pointer click on invalid cell object={name}", this);
                return;
            }

            bool isFlagAction = eventData != null && eventData.button == PointerEventData.InputButton.Right;
            if (!isFlagAction)
            {
                return;
            }

            Debug.Log(
                $"BattleCellView: right click cell={_state.CellIndex} revealed={_state.IsRevealed} flagged={_state.IsFlagged} hasCallback={_onInteract != null}",
                this);
            _onInteract?.Invoke(_state.CellIndex, true);
        }

        private void Refresh()
        {
            if (_background == null || _label == null || _button == null)
            {
                return;
            }

            if (!_state.IsValid)
            {
                _background.color = new Color(0.18f, 0.18f, 0.2f, 0.45f);
                _label.text = string.Empty;
                _background.raycastTarget = false;
                _button.interactable = false;
                return;
            }

            _background.raycastTarget = true;
            _button.interactable = true;

            if (_state.IsFlagged && !_state.IsRevealed)
            {
                _background.color = new Color(0.86f, 0.35f, 0.35f, 0.95f);
                _label.text = "F";
                _label.color = Color.white;
                return;
            }

            if (!_state.IsRevealed)
            {
                Color32 blockColor = _state.BlockColor;
                _background.color = new Color(blockColor.r / 255f, blockColor.g / 255f, blockColor.b / 255f, 1f);
                _label.text = string.Empty;
                return;
            }

            if (_state.HasCat)
            {
                _background.color = new Color(0.97f, 0.62f, 0.24f, 1f);
                _label.text = "猫";
                _label.color = Color.white;
                return;
            }

            _background.color = new Color(0.95f, 0.95f, 0.97f, 1f);
            _label.text = _state.AdjacentCatCount > 0 ? _state.AdjacentCatCount.ToString() : string.Empty;
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

        private void HandlePrimaryClick()
        {
            if (!_state.IsValid)
            {
                Debug.Log($"BattleCellView: ignored primary click on invalid cell object={name}", this);
                return;
            }

            Debug.Log(
                $"BattleCellView: left click cell={_state.CellIndex} revealed={_state.IsRevealed} flagged={_state.IsFlagged} hasCallback={_onInteract != null}",
                this);
            _onInteract?.Invoke(_state.CellIndex, false);
        }
    }
}
