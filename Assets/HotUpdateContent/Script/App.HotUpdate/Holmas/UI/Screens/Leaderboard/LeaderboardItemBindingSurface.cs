using App.HotUpdate.Holmas.UI.Core;
using App.HotUpdate.Holmas.UI.Tool;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace App.HotUpdate.Holmas.UI.Screens.Leaderboard
{
    public sealed class LeaderboardItemBindingSurface : MonoBehaviour
    {
        [SerializeField] private TMP_Text _rankTmpText;
        [SerializeField] private Text _rankText;
        [SerializeField] private TMP_Text _nameTmpText;
        [SerializeField] private Text _nameText;
        [SerializeField] private TMP_Text _scoreTmpText;
        [SerializeField] private Text _scoreText;
        [SerializeField] private Image _headIcon;
        [SerializeField] private Image _frameIcon;
        [SerializeField] private Image _leadIcon;

        public LeaderboardTextSlot RankText => new LeaderboardTextSlot(_rankTmpText, _rankText);

        public LeaderboardTextSlot NameText => new LeaderboardTextSlot(_nameTmpText, _nameText);

        public LeaderboardTextSlot ScoreText => new LeaderboardTextSlot(_scoreTmpText, _scoreText);

        public Image HeadIcon => _headIcon;

        public Image FrameIcon => _frameIcon;

        public Image LeadIcon => _leadIcon;

        public bool HasRankText => _rankTmpText != null || _rankText != null;

        public bool HasRequiredBindings =>
            (_nameTmpText != null || _nameText != null) &&
            (_scoreTmpText != null || _scoreText != null) &&
            _headIcon != null &&
            _frameIcon != null &&
            _leadIcon != null;

        #if UNITY_EDITOR
        public void AssignForEditor()
        {
            ResolveText("MyLeadInfo", out _rankTmpText, out _rankText);
            ResolveText("Name", out _nameTmpText, out _nameText);
            ResolveText("LeadCount", out _scoreTmpText, out _scoreText);
            _headIcon = ResolveChildComponent<Image>("HeadIcon");
            _frameIcon = ResolveChildComponent<Image>("FrameIcon");
            _leadIcon = ResolveChildComponent<Image>("LeadIcon");
        }

        private void ResolveText(string childName, out TMP_Text tmpText, out Text text)
        {
            Transform child = transform.Find(childName);
            tmpText = child != null ? child.GetComponent<TMP_Text>() : null;
            text = child != null ? child.GetComponent<Text>() : null;
        }

        private T ResolveChildComponent<T>(string childName) where T : Component
        {
            Transform child = transform.Find(childName);
            return child != null ? child.GetComponent<T>() : null;
        }
        #endif
    }

    public readonly struct LeaderboardTextSlot
    {
        private readonly TMP_Text _tmpText;
        private readonly Text _uiText;

        public LeaderboardTextSlot(TMP_Text tmpText, Text uiText)
        {
            _tmpText = tmpText;
            _uiText = uiText;
        }

        public bool HasTarget => _tmpText != null || _uiText != null;

        public void SetText(string value)
        {
            string safeValue = value ?? string.Empty;
            if (_tmpText != null)
            {
                TmpGlyphCoverageReporter.SetText(_tmpText, safeValue);
                return;
            }

            if (_uiText != null && _uiText.text != safeValue)
            {
                _uiText.text = safeValue;
            }
        }
    }
}
