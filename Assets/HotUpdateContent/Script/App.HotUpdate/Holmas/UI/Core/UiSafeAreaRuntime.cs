using App.Shared.Contracts;
using UnityEngine;

namespace App.HotUpdate.Holmas.UI.Core
{
    public static class UiSafeAreaRuntime
    {
        private static IWeChatBridge _weChatBridge;

        public static void Configure(IWeChatBridge weChatBridge)
        {
            _weChatBridge = weChatBridge;
        }

        public static bool TryGetSafeArea(out Rect safeArea, out Vector2Int screenSize)
        {
            if (_weChatBridge != null &&
                _weChatBridge.TryGetWindowInfo(out WeChatWindowInfo windowInfo) &&
                windowInfo != null)
            {
                int width = Mathf.Max(windowInfo.ScreenWidth, windowInfo.WindowWidth);
                int height = Mathf.Max(windowInfo.ScreenHeight, windowInfo.WindowHeight);
                UiSafeAreaInfo safeAreaInfo = windowInfo.SafeArea;
                if (width > 0 && height > 0 && safeAreaInfo != null)
                {
                    int left = Mathf.Max(0, safeAreaInfo.Left);
                    int right = Mathf.Max(0, safeAreaInfo.Right);
                    int top = Mathf.Max(0, safeAreaInfo.Top);
                    int bottom = Mathf.Max(0, safeAreaInfo.Bottom);
                    float rectWidth = safeAreaInfo.Width > 0
                        ? safeAreaInfo.Width
                        : Mathf.Max(1, width - left - right);
                    float rectHeight = safeAreaInfo.Height > 0
                        ? safeAreaInfo.Height
                        : Mathf.Max(1, height - top - bottom);

                    safeArea = new Rect(left, bottom, rectWidth, rectHeight);
                    screenSize = new Vector2Int(width, height);
                    return true;
                }
            }

            safeArea = default;
            screenSize = default;
            return false;
        }
    }
}
