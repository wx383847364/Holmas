using System;
using System.Collections.Generic;

namespace App.HotUpdate.Holmas.UI.Core
{
    public sealed class UiNavigationState
    {
        private readonly List<UiPageController> _pageStack = new List<UiPageController>();
        private readonly List<UiPopupController> _popupStack = new List<UiPopupController>();
        private readonly Dictionary<string, UiSheetController> _activeSheets = new Dictionary<string, UiSheetController>(StringComparer.Ordinal);
        private UiOverlayController _currentOverlay;

        public IReadOnlyList<UiPageController> PageStack => _pageStack;

        public IReadOnlyList<UiPopupController> PopupStack => _popupStack;

        public IReadOnlyDictionary<string, UiSheetController> ActiveSheets => _activeSheets;

        public bool IsInputLocked => InputLockCount > 0;

        public int InputLockCount { get; private set; }

        public UiPageController CurrentPage => _pageStack.Count > 0 ? _pageStack[_pageStack.Count - 1] : null;

        public UiPopupController TopPopup => _popupStack.Count > 0 ? _popupStack[_popupStack.Count - 1] : null;

        public UiOverlayController CurrentOverlay => _currentOverlay;

        public void PushPage(UiPageController page)
        {
            if (page == null)
            {
                return;
            }

            _pageStack.Remove(page);
            _pageStack.Add(page);
        }

        public void RemovePage(UiPageController page)
        {
            if (page == null)
            {
                return;
            }

            _pageStack.Remove(page);
        }

        public void PushPopup(UiPopupController popup)
        {
            if (popup == null)
            {
                return;
            }

            _popupStack.Remove(popup);
            _popupStack.Add(popup);
        }

        public void RemovePopup(UiPopupController popup)
        {
            if (popup == null)
            {
                return;
            }

            _popupStack.Remove(popup);
        }

        public void SetActiveSheet(string groupId, UiSheetController sheet)
        {
            if (string.IsNullOrWhiteSpace(groupId))
            {
                return;
            }

            if (sheet == null)
            {
                _activeSheets.Remove(groupId);
                return;
            }

            _activeSheets[groupId] = sheet;
        }

        public UiSheetController GetActiveSheet(string groupId)
        {
            if (string.IsNullOrWhiteSpace(groupId))
            {
                return null;
            }

            _activeSheets.TryGetValue(groupId, out UiSheetController sheet);
            return sheet;
        }

        public void RemoveSheet(UiSheetController sheet)
        {
            if (sheet == null)
            {
                return;
            }

            string targetKey = null;
            foreach (KeyValuePair<string, UiSheetController> pair in _activeSheets)
            {
                if (ReferenceEquals(pair.Value, sheet))
                {
                    targetKey = pair.Key;
                    break;
                }
            }

            if (!string.IsNullOrWhiteSpace(targetKey))
            {
                _activeSheets.Remove(targetKey);
            }
        }

        public void SetCurrentOverlay(UiOverlayController overlay)
        {
            _currentOverlay = overlay;
        }

        public void RemoveOverlay(UiOverlayController overlay)
        {
            if (ReferenceEquals(_currentOverlay, overlay))
            {
                _currentOverlay = null;
            }
        }

        public void AcquireInputLock()
        {
            InputLockCount++;
        }

        public void ReleaseInputLock()
        {
            InputLockCount = Math.Max(0, InputLockCount - 1);
        }
    }
}
