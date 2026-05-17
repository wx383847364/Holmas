using System;
using System.Threading.Tasks;
using App.Shared.Contracts;
using UnityEngine;

#if MINIGAME_SUBPLATFORM_WEIXIN && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace App.AOT.Platform.WeChat
{
    public static class WeixinMiniGameJsBridge
    {
        private const string CallbackObjectName = "HolmasWeixinMiniGameCallbackProxy";
        private static IAppLogger _logger;

#if MINIGAME_SUBPLATFORM_WEIXIN && !UNITY_EDITOR
        private static WeixinMiniGameCallbackProxy _callbackProxy;

        [DllImport("__Internal")]
        private static extern int HolmasWeixinMiniGame_IsAvailable();

        [DllImport("__Internal")]
        private static extern void HolmasWeixinMiniGame_Login(string callbackObjectName, string successMethodName, string failMethodName);

        [DllImport("__Internal")]
        private static extern string HolmasWeixinMiniGame_GetWindowInfoJson();
#endif

        public static bool IsAvailable
        {
            get
            {
#if MINIGAME_SUBPLATFORM_WEIXIN && !UNITY_EDITOR
                try
                {
                    return HolmasWeixinMiniGame_IsAvailable() == 1;
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning("WeixinMiniGameJsBridge: JSBridge availability check failed. {0}", ex);
                    return false;
                }
#else
                return false;
#endif
            }
        }

        public static void Initialize(IAppLogger logger)
        {
            _logger = logger;
#if MINIGAME_SUBPLATFORM_WEIXIN && !UNITY_EDITOR
            EnsureCallbackProxy();
#endif
        }

        public static void Shutdown()
        {
#if MINIGAME_SUBPLATFORM_WEIXIN && !UNITY_EDITOR
            if (_callbackProxy != null)
            {
                UnityEngine.Object.Destroy(_callbackProxy.gameObject);
                _callbackProxy = null;
            }
#endif
            _logger = null;
        }

        public static Task<string> LoginAsync()
        {
#if MINIGAME_SUBPLATFORM_WEIXIN && !UNITY_EDITOR
            if (!IsAvailable)
            {
                return Task.FromResult(string.Empty);
            }

            WeixinMiniGameCallbackProxy proxy = EnsureCallbackProxy();
            return proxy.LoginAsync(() =>
                HolmasWeixinMiniGame_Login(
                    CallbackObjectName,
                    nameof(WeixinMiniGameCallbackProxy.OnLoginSuccess),
                    nameof(WeixinMiniGameCallbackProxy.OnLoginFail)));
#else
            return Task.FromResult(string.Empty);
#endif
        }

        public static bool TryGetWindowInfo(out WeChatWindowInfo windowInfo)
        {
            windowInfo = null;

#if MINIGAME_SUBPLATFORM_WEIXIN && !UNITY_EDITOR
            if (!IsAvailable)
            {
                return false;
            }

            try
            {
                string json = HolmasWeixinMiniGame_GetWindowInfoJson();
                if (string.IsNullOrWhiteSpace(json))
                {
                    return false;
                }

                var dto = JsonUtility.FromJson<WeixinWindowInfoDto>(json);
                windowInfo = dto != null ? dto.ToWindowInfo() : null;
                return windowInfo != null && windowInfo.IsAvailable;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning("WeixinMiniGameJsBridge: window info read failed. {0}", ex);
                return false;
            }
#else
            return false;
#endif
        }

#if MINIGAME_SUBPLATFORM_WEIXIN && !UNITY_EDITOR
        private static WeixinMiniGameCallbackProxy EnsureCallbackProxy()
        {
            if (_callbackProxy != null)
            {
                return _callbackProxy;
            }

            GameObject existing = GameObject.Find(CallbackObjectName);
            GameObject callbackObject = existing != null ? existing : new GameObject(CallbackObjectName);
            UnityEngine.Object.DontDestroyOnLoad(callbackObject);
            _callbackProxy = callbackObject.GetComponent<WeixinMiniGameCallbackProxy>();
            if (_callbackProxy == null)
            {
                _callbackProxy = callbackObject.AddComponent<WeixinMiniGameCallbackProxy>();
            }

            _callbackProxy.Configure(_logger);
            return _callbackProxy;
        }
#endif

        [Serializable]
        private sealed class WeixinWindowInfoDto
        {
            public float screenWidth;
            public float screenHeight;
            public float windowWidth;
            public float windowHeight;
            public float pixelRatio = 1f;
            public float statusBarHeight;
            public WeixinSafeAreaDto safeArea;

            public WeChatWindowInfo ToWindowInfo()
            {
                float ratio = pixelRatio > 0f ? pixelRatio : 1f;
                int screenWidthPixels = ToPixels(screenWidth, ratio, Screen.width);
                int screenHeightPixels = ToPixels(screenHeight, ratio, Screen.height);
                int windowWidthPixels = ToPixels(windowWidth > 0f ? windowWidth : screenWidth, ratio, Screen.width);
                int windowHeightPixels = ToPixels(windowHeight > 0f ? windowHeight : screenHeight, ratio, Screen.height);

                UiSafeAreaInfo safeAreaInfo = safeArea != null
                    ? safeArea.ToSafeAreaInfo(screenWidth, screenHeight, ratio, screenWidthPixels, screenHeightPixels)
                    : BuildUnitySafeArea(screenWidthPixels, screenHeightPixels);

                return new WeChatWindowInfo
                {
                    ScreenWidth = screenWidthPixels,
                    ScreenHeight = screenHeightPixels,
                    WindowWidth = windowWidthPixels,
                    WindowHeight = windowHeightPixels,
                    PixelRatio = ratio,
                    StatusBarHeight = ToPixels(statusBarHeight, ratio, 0),
                    IsFallback = safeArea == null,
                    IsAvailable = screenWidthPixels > 0 && screenHeightPixels > 0,
                    SafeArea = safeAreaInfo,
                };
            }

            private static UiSafeAreaInfo BuildUnitySafeArea(int screenWidthPixels, int screenHeightPixels)
            {
                Rect unitySafeArea = Screen.safeArea;
                int left = Mathf.RoundToInt(unitySafeArea.xMin);
                int bottom = Mathf.RoundToInt(unitySafeArea.yMin);
                int safeWidth = Mathf.RoundToInt(unitySafeArea.width);
                int safeHeight = Mathf.RoundToInt(unitySafeArea.height);
                return new UiSafeAreaInfo
                {
                    Left = left,
                    Right = Mathf.Max(0, screenWidthPixels - left - safeWidth),
                    Top = Mathf.Max(0, screenHeightPixels - bottom - safeHeight),
                    Bottom = bottom,
                    Width = safeWidth,
                    Height = safeHeight,
                };
            }
        }

        [Serializable]
        private sealed class WeixinSafeAreaDto
        {
            public float left;
            public float right;
            public float top;
            public float bottom;
            public float width;
            public float height;

            public UiSafeAreaInfo ToSafeAreaInfo(float screenWidth, float screenHeight, float pixelRatio, int screenWidthPixels, int screenHeightPixels)
            {
                int leftPixels = ToPixels(left, pixelRatio, 0);
                int rightMarginPixels = ToPixels(Mathf.Max(0f, screenWidth - right), pixelRatio, 0);
                int topPixels = ToPixels(top, pixelRatio, 0);
                int bottomMarginPixels = ToPixels(Mathf.Max(0f, screenHeight - bottom), pixelRatio, 0);
                int widthPixels = ToPixels(width, pixelRatio, Mathf.Max(1, screenWidthPixels - leftPixels - rightMarginPixels));
                int heightPixels = ToPixels(height, pixelRatio, Mathf.Max(1, screenHeightPixels - topPixels - bottomMarginPixels));

                return new UiSafeAreaInfo
                {
                    Left = leftPixels,
                    Right = rightMarginPixels,
                    Top = topPixels,
                    Bottom = bottomMarginPixels,
                    Width = widthPixels,
                    Height = heightPixels,
                };
            }
        }

        private static int ToPixels(float value, float ratio, int fallback)
        {
            if (value <= 0f)
            {
                return fallback;
            }

            return Mathf.Max(1, Mathf.RoundToInt(value * Mathf.Max(0.01f, ratio)));
        }
    }

#if MINIGAME_SUBPLATFORM_WEIXIN && !UNITY_EDITOR
    public sealed class WeixinMiniGameCallbackProxy : MonoBehaviour
    {
        private IAppLogger _logger;
        private TaskCompletionSource<string> _loginCompletion;

        public void Configure(IAppLogger logger)
        {
            _logger = logger;
        }

        public Task<string> LoginAsync(Action invokeLogin)
        {
            if (_loginCompletion != null && !_loginCompletion.Task.IsCompleted)
            {
                _loginCompletion.TrySetCanceled();
            }

            _loginCompletion = new TaskCompletionSource<string>();

            try
            {
                invokeLogin();
            }
            catch (Exception ex)
            {
                _loginCompletion.TrySetException(ex);
            }

            return _loginCompletion.Task;
        }

        public void OnLoginSuccess(string code)
        {
            _logger?.LogInfo("WeixinMiniGameCallbackProxy: wx.login success.");
            _loginCompletion?.TrySetResult(code ?? string.Empty);
        }

        public void OnLoginFail(string error)
        {
            _logger?.LogWarning("WeixinMiniGameCallbackProxy: wx.login failed. {0}", error);
            _loginCompletion?.TrySetResult(string.Empty);
        }
    }
#endif
}
