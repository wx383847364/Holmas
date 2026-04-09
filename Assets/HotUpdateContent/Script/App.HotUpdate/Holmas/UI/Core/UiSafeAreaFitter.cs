using UnityEngine;

namespace App.HotUpdate.Holmas.UI.Core
{
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public sealed class UiSafeAreaFitter : MonoBehaviour
    {
        public enum FitMode
        {
            Full = 0,
            Horizontal = 1,
            LeftOnly = 2
        }

        [SerializeField]
        private FitMode fitMode = FitMode.Horizontal;

        [SerializeField]
        private bool updateEveryFrameInEditor;

        private RectTransform _rectTransform;
        private Rect _lastSafeArea;
        private Vector2Int _lastScreenSize;
        private FitMode _lastFitMode;

        private void OnEnable()
        {
            ApplyIfNeeded(force: true);
        }

        private void Update()
        {
            if (!UnityEngine.Application.isPlaying && !updateEveryFrameInEditor)
            {
                return;
            }

            ApplyIfNeeded(force: false);
        }

        private void OnRectTransformDimensionsChange()
        {
            ApplyIfNeeded(force: false);
        }

        private void OnValidate()
        {
            ApplyIfNeeded(force: true);
        }

        private void ApplyIfNeeded(bool force)
        {
            if (!TryGetRectTransform(out RectTransform rectTransform))
            {
                return;
            }

            Rect safeArea;
            Vector2Int screenSize;
            if (!UiSafeAreaRuntime.TryGetSafeArea(out safeArea, out screenSize))
            {
                safeArea = Screen.safeArea;
                screenSize = new Vector2Int(Mathf.Max(Screen.width, 1), Mathf.Max(Screen.height, 1));
            }

            if (!force &&
                safeArea == _lastSafeArea &&
                screenSize == _lastScreenSize &&
                fitMode == _lastFitMode)
            {
                return;
            }

            Vector2 anchorMin = Vector2.zero;
            Vector2 anchorMax = Vector2.one;

            float minX = Mathf.Clamp01(safeArea.xMin / screenSize.x);
            float maxX = Mathf.Clamp01(safeArea.xMax / screenSize.x);
            float minY = Mathf.Clamp01(safeArea.yMin / screenSize.y);
            float maxY = Mathf.Clamp01(safeArea.yMax / screenSize.y);

            switch (fitMode)
            {
                case FitMode.Full:
                    anchorMin = new Vector2(minX, minY);
                    anchorMax = new Vector2(maxX, maxY);
                    break;
                case FitMode.LeftOnly:
                    anchorMin = new Vector2(minX, 0f);
                    anchorMax = Vector2.one;
                    break;
                default:
                    anchorMin = new Vector2(minX, 0f);
                    anchorMax = new Vector2(maxX, 1f);
                    break;
            }

            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.localScale = Vector3.one;

            _lastSafeArea = safeArea;
            _lastScreenSize = screenSize;
            _lastFitMode = fitMode;
        }

        private bool TryGetRectTransform(out RectTransform rectTransform)
        {
            if (_rectTransform == null)
            {
                _rectTransform = GetComponent<RectTransform>();
            }

            rectTransform = _rectTransform;
            return rectTransform != null;
        }
    }
}
