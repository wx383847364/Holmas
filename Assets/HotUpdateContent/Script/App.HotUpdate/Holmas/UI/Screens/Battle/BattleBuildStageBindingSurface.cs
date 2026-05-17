using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace App.HotUpdate.Holmas.UI.Screens.Battle
{
    public sealed class BattleBuildStageBindingSurface : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private Image _image;
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private RectTransform _lockRect;
        [SerializeField] private RectTransform _baseStarGroup;
        [SerializeField] private RectTransform _activeStarGroup;

        public Button Button => _button;

        public Image Image => _image;

        public TextMeshProUGUI NameText => _nameText;

        public RectTransform LockRect => _lockRect;

        public RectTransform BaseStarGroup => _baseStarGroup;

        public RectTransform ActiveStarGroup => _activeStarGroup;

        public bool HasRequiredBindings =>
            _button != null &&
            _image != null &&
            _nameText != null &&
            _lockRect != null &&
            _baseStarGroup != null &&
            _activeStarGroup != null;

        #if UNITY_EDITOR
        public void AssignForEditor(
            Button button,
            Image image,
            TextMeshProUGUI nameText,
            RectTransform lockRect,
            RectTransform baseStarGroup,
            RectTransform activeStarGroup)
        {
            _button = button;
            _image = image;
            _nameText = nameText;
            _lockRect = lockRect;
            _baseStarGroup = baseStarGroup;
            _activeStarGroup = activeStarGroup;
        }
        #endif
    }
}
