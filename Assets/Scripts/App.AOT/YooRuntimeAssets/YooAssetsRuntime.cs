using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using YooAsset;
using App.Shared.Contracts;
using App.AOT.YooRuntimeAssets.PatchFlow;

namespace App.AOT.YooRuntimeAssets
{
    /// <summary>
    /// YooAssets运行时：初始化、包管理、下载器、版本对齐
    /// </summary>
    public class YooAssetsRuntime : IAssetsRuntime
    {
        private readonly IAppLogger _logger;
        private ResourcePackage _defaultPackage;
        private PatchOperationHandler _patchHandler;
#if UNITY_EDITOR
        private bool _isEditorMode;
#endif

        private const string DefaultPackageName = "DefaultPackage";
        private const string DefaultHostServer = "https://your-cdn-url.com/bundles";
        private const string FallbackHostServer = "https://your-cdn-url-backup.com/bundles";

        public YooAssetsRuntime(IAppLogger logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// 初始化YooAssets
        /// </summary>
        public Task InitializeAsync()
        {
#if UNITY_EDITOR
            try
            {
                _logger?.LogInfo("YooAssetsRuntime: 开始初始化YooAssets...");
                _isEditorMode = true;
                _logger?.LogInfo("YooAssetsRuntime: 编辑器模式 - 使用 AssetDatabase 直接加载");
                _logger?.LogInfo("YooAssetsRuntime: YooAssets初始化完成");
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger?.LogError("YooAssetsRuntime: 初始化失败: {0}", ex);
                throw;
            }
#else
            return InitializePlayerAsync();
#endif
        }

#if !UNITY_EDITOR
        private async Task InitializePlayerAsync()
        {
            try
            {
                _logger?.LogInfo("YooAssetsRuntime: 开始初始化YooAssets...");

                // 初始化YooAssets
                YooAssets.Initialize();

                // 创建默认资源包
                _defaultPackage = YooAssets.CreatePackage(DefaultPackageName);

                // 设置资源包
                YooAssets.SetDefaultPackage(_defaultPackage);

#if UNITY_WEBGL && (WEIXINMINIGAME || MINIGAME_SUBPLATFORM_WEIXIN)
                // 微信小游戏使用 YooAssets 小游戏文件系统，通过微信 USER_DATA_PATH 管理缓存。
                var webModeParameters = new WebPlayModeParameters();
                var remoteServices = CreateWeixinPackageRemoteServices(DefaultPackageName);
                string packageRoot = WechatFileSystemCreater.CreateDefaultPackageRoot(DefaultPackageName);
                webModeParameters.WebServerFileSystemParameters = WechatFileSystemCreater.CreateFileSystemParameters(packageRoot, remoteServices);
                var initOperation = _defaultPackage.InitializeAsync(webModeParameters);
#elif UNITY_WEBGL
                // WebGL 必须使用 WebPlayMode；YooAssets 不支持 WebGL 下的 OfflinePlayMode。
                var webModeParameters = new WebPlayModeParameters();
                webModeParameters.WebServerFileSystemParameters = FileSystemParameters.CreateDefaultWebServerFileSystemParameters();
                var initOperation = _defaultPackage.InitializeAsync(webModeParameters);
#elif HOLMAS_YOO_OFFLINE_PLAYMODE
                // Player smoke 使用内置包离线初始化，避免依赖 CDN。
                var offlineModeParameters = new OfflinePlayModeParameters();
                offlineModeParameters.BuildinFileSystemParameters = FileSystemParameters.CreateDefaultBuildinFileSystemParameters();
                var initOperation = _defaultPackage.InitializeAsync(offlineModeParameters);
#else
                // 运行时模式：使用主机模式（从CDN下载）
                var hostModeParameters = new HostPlayModeParameters();
                // 内置文件系统参数
                hostModeParameters.BuildinFileSystemParameters = FileSystemParameters.CreateDefaultBuildinFileSystemParameters();
                // 缓存文件系统参数（需要远程服务）
                hostModeParameters.CacheFileSystemParameters = FileSystemParameters.CreateDefaultCacheFileSystemParameters(CreateHostPackageRemoteServices(DefaultPackageName));
                var initOperation = _defaultPackage.InitializeAsync(hostModeParameters);
#endif

                if (initOperation != null)
                {
                    await initOperation.Task;
                    if (initOperation.Status != EOperationStatus.Succeed)
                    {
                        _logger?.LogError("YooAssetsRuntime: 资源包初始化失败: {0}", initOperation.Error);
                        throw new Exception(initOperation.Error);
                    }
                }

#if HOLMAS_YOO_OFFLINE_PLAYMODE || UNITY_WEBGL
                var versionOperation = _defaultPackage.RequestPackageVersionAsync();
                await versionOperation.Task;
                if (versionOperation.Status != EOperationStatus.Succeed)
                {
                    _logger?.LogError("YooAssetsRuntime: 资源包版本请求失败: {0}", versionOperation.Error);
                    throw new Exception(versionOperation.Error);
                }

                var manifestOperation = _defaultPackage.UpdatePackageManifestAsync(versionOperation.PackageVersion);
                await manifestOperation.Task;
                if (manifestOperation.Status != EOperationStatus.Succeed)
                {
                    _logger?.LogError("YooAssetsRuntime: 资源包清单更新失败: {0}", manifestOperation.Error);
                    throw new Exception(manifestOperation.Error);
                }
#endif

                // 初始化补丁处理器
                _patchHandler = new PatchOperationHandler(_defaultPackage, _logger);

                _logger?.LogInfo("YooAssetsRuntime: YooAssets初始化完成");
            }
            catch (Exception ex)
            {
                _logger?.LogError("YooAssetsRuntime: 初始化失败: {0}", ex);
                throw;
            }
        }
#endif

        private IRemoteServices CreateWeixinPackageRemoteServices(string packageName)
        {
            var settings = LoadWeixinCdnSettings();
            string mainRoot = CombineUrl(settings.CdnRoot, "StreamingAssets/yoo/" + packageName);
            string fallbackRoot = CombineUrl(settings.FallbackCdnRoot, "StreamingAssets/yoo/" + packageName);
            _logger?.LogInfo("YooAssetsRuntime: CDN package root={0}", mainRoot);
            return new RemoteServices(mainRoot, fallbackRoot);
        }

        private IRemoteServices CreateHostPackageRemoteServices(string packageName)
        {
            _logger?.LogInfo("YooAssetsRuntime: HostPlayMode package root={0}", DefaultHostServer);
            return new RemoteServices(DefaultHostServer, FallbackHostServer);
        }

        private CdnSettings LoadWeixinCdnSettings()
        {
            string cdnRoot = NormalizeCdnRoot(GetWeixinMiniGameDataCdn());
            string fallbackRoot = cdnRoot;

            ValidateHttpsCdnRoot(cdnRoot, "game.js DATA_CDN");
            return new CdnSettings(cdnRoot, fallbackRoot);
        }

        private static void ValidateHttpsCdnRoot(string url, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new InvalidOperationException(
                    $"YooAssetsRuntime: {fieldName} 不能为空。微信小游戏 YooAssets 资源必须部署到 HTTPS CDN，版本文件应可访问：{{CDN_ROOT}}/StreamingAssets/yoo/{DefaultPackageName}/{DefaultPackageName}.version");
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri) ||
                !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"YooAssetsRuntime: {fieldName} 必须是 HTTPS CDN 根地址，当前值={url}");
            }

            if (IsLocalOrPrivateHost(uri.Host))
            {
                throw new InvalidOperationException($"YooAssetsRuntime: {fieldName} 不能是本机或内网地址，微信小游戏正式/真机需要微信后台配置过的 HTTPS 下载域名。当前值={url}");
            }
        }

        private static bool IsLocalOrPrivateHost(string host)
        {
            if (string.IsNullOrWhiteSpace(host))
                return true;

            string normalized = host.Trim().Trim('[', ']').ToLowerInvariant();
            if (normalized == "localhost" || normalized.EndsWith(".localhost", StringComparison.Ordinal) ||
                normalized == "::1" || normalized == "0.0.0.0" || normalized.StartsWith("127.", StringComparison.Ordinal) ||
                normalized.StartsWith("10.", StringComparison.Ordinal) || normalized.StartsWith("192.168.", StringComparison.Ordinal))
            {
                return true;
            }

            return IsPrivate172Host(normalized);
        }

        private static bool IsPrivate172Host(string host)
        {
            string[] parts = host.Split('.');
            if (parts.Length != 4 || parts[0] != "172")
                return false;

            return int.TryParse(parts[1], out int secondOctet) && secondOctet >= 16 && secondOctet <= 31;
        }

        private static string NormalizeCdnRoot(string url)
        {
            return string.IsNullOrWhiteSpace(url) ? string.Empty : url.Trim().TrimEnd('/');
        }

        private static string CombineUrl(string root, string path)
        {
            return $"{root.TrimEnd('/')}/{path.TrimStart('/')}";
        }

        private sealed class CdnSettings
        {
            public readonly string CdnRoot;
            public readonly string FallbackCdnRoot;

            public CdnSettings(string cdnRoot, string fallbackCdnRoot)
            {
                CdnRoot = cdnRoot;
                FallbackCdnRoot = fallbackCdnRoot;
            }
        }

#if UNITY_WEBGL && (WEIXINMINIGAME || MINIGAME_SUBPLATFORM_WEIXIN) && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern string HolmasWeixinMiniGame_GetDataCdn();
#endif

        private static string GetWeixinMiniGameDataCdn()
        {
#if UNITY_WEBGL && (WEIXINMINIGAME || MINIGAME_SUBPLATFORM_WEIXIN) && !UNITY_EDITOR
            try
            {
                return HolmasWeixinMiniGame_GetDataCdn();
            }
            catch (Exception)
            {
                return string.Empty;
            }
#else
            return string.Empty;
#endif
        }

        private sealed class RemoteServices : IRemoteServices
        {
            private readonly string _mainRoot;
            private readonly string _fallbackRoot;

            public RemoteServices(string mainRoot, string fallbackRoot)
            {
                _mainRoot = mainRoot;
                _fallbackRoot = fallbackRoot;
            }

            public string GetRemoteMainURL(string fileName) => CombineUrl(_mainRoot, fileName);

            public string GetRemoteFallbackURL(string fileName) => CombineUrl(_fallbackRoot, fileName);
        }

        /// <summary>
        /// 执行补丁流程：检查版本、下载、校验
        /// </summary>
        public async Task<bool> RunPatchFlowAsync(string packageVersion = null)
        {
            if (_patchHandler == null)
            {
                _logger?.LogError("YooAssetsRuntime: 补丁处理器未初始化");
                return false;
            }

            return await _patchHandler.RunPatchFlowAsync(packageVersion);
        }

        /// <summary>
        /// 加载资源（IAssetsRuntime 接口实现）
        /// </summary>
        public async Task<IAssetHandle> LoadAssetAsync(string location)
        {
            if (string.IsNullOrEmpty(location))
                return null;

#if UNITY_EDITOR
            if (_isEditorMode)
            {
                // 编辑器模式：使用 AssetDatabase 直接加载
                UnityEngine.Object asset = null;
                foreach (var candidate in GetEditorLoadCandidates(location))
                {
                    asset = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Sprite>(candidate);
                    if (asset != null)
                        break;

                    asset = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(candidate);
                    if (asset != null)
                        break;
                }

                if (asset == null)
                {
                    _logger?.LogWarning("YooAssetsRuntime: 编辑器模式加载失败: {0}", location);
                    return null;
                }
                return new EditorAssetHandleWrapper(asset);
            }
#endif
            if (_defaultPackage == null)
                return null;

            if (IsSpriteLocation(location))
            {
                var spriteHandle = _defaultPackage.LoadAssetAsync<UnityEngine.Sprite>(location);
                await spriteHandle.Task;
                if (spriteHandle.Status == EOperationStatus.Succeed)
                {
                    return new AssetHandleWrapper(spriteHandle);
                }

                spriteHandle.Release();
            }

            var handle = _defaultPackage.LoadAssetAsync<UnityEngine.Object>(location);
            await handle.Task;
            if (handle.Status != EOperationStatus.Succeed)
            {
                _logger?.LogError("YooAssetsRuntime: 加载资源失败 {0}: {1}", location, handle.LastError);
                return null;
            }
            return new AssetHandleWrapper(handle);
        }

        /// <summary>
        /// 加载资源（内部/泛型用）
        /// </summary>
        public AssetHandle LoadAssetAsync<T>(string location) where T : UnityEngine.Object
        {
#if UNITY_EDITOR
            if (_isEditorMode)
            {
                // 编辑器模式下返回 null，应使用 LoadAssetAsync(string) 代替
                _logger?.LogWarning("YooAssetsRuntime: 编辑器模式不支持泛型 LoadAssetAsync<T>，请使用 LoadAssetAsync(string)");
                return null;
            }
#endif
            return _defaultPackage?.LoadAssetAsync<T>(location);
        }

        private sealed class AssetHandleWrapper : IAssetHandle
        {
            private readonly AssetHandle _handle;
            public UnityEngine.Object AssetObject => _handle?.AssetObject;
            public AssetHandleWrapper(AssetHandle handle) { _handle = handle; }
            public void Release() => _handle?.Release();
        }

#if UNITY_EDITOR
        /// <summary>
        /// 编辑器模式下的资源句柄包装器
        /// </summary>
        private sealed class EditorAssetHandleWrapper : IAssetHandle
        {
            private readonly UnityEngine.Object _asset;
            public UnityEngine.Object AssetObject => _asset;
            public EditorAssetHandleWrapper(UnityEngine.Object asset) { _asset = asset; }
            public void Release() { /* 编辑器模式下无需释放 */ }
        }
#endif

        /// <summary>
        /// 获取资源包
        /// </summary>
        public ResourcePackage GetPackage(string packageName = null)
        {
            if (string.IsNullOrEmpty(packageName))
            {
                return _defaultPackage;
            }
            return YooAssets.GetPackage(packageName);
        }

        /// <summary>
        /// 关闭
        /// </summary>
        public void Shutdown()
        {
            YooAssets.Destroy();
            _logger?.LogInfo("YooAssetsRuntime: 已关闭");
        }

#if UNITY_EDITOR
        private static IEnumerable<string> GetEditorLoadCandidates(string location)
        {
            var candidates = new List<string>();
            string normalized = location.Replace('\\', '/').Trim();
            string[] extensions = { string.Empty, ".prefab", ".asset", ".mat", ".png", ".jpg", ".bytes" };

            void AddWithExtensions(string basePath)
            {
                if (string.IsNullOrWhiteSpace(basePath))
                    return;

                foreach (var ext in extensions)
                {
                    string candidate = basePath.EndsWith(ext, StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(ext)
                        ? basePath
                        : basePath + ext;
                    candidates.Add(candidate);
                }
            }

            AddWithExtensions(normalized);

            string fileName = Path.GetFileName(normalized);
            if (!string.IsNullOrEmpty(fileName))
            {
                AddWithExtensions($"Assets/HotUpdateContent/Res/Map/{fileName}");
                AddWithExtensions($"Assets/HotUpdateContent/Res/{fileName}");
            }

            string resolvedHotUpdatePath = ResolveHotUpdateTerrainPath(normalized);
            if (!string.IsNullOrEmpty(resolvedHotUpdatePath))
            {
                AddWithExtensions(resolvedHotUpdatePath);
            }

            return candidates
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private static string ResolveHotUpdateTerrainPath(string location)
        {
            if (string.IsNullOrWhiteSpace(location))
                return string.Empty;

            string normalized = location.Replace('\\', '/').Trim();
            if (normalized.Contains("://", StringComparison.Ordinal))
                return string.Empty;

            if (normalized.StartsWith("Assets/HotUpdateContent/Res/Map/", StringComparison.OrdinalIgnoreCase))
                return normalized;

            string fileName = Path.GetFileName(normalized);
            if (string.IsNullOrEmpty(fileName))
                return string.Empty;

            return $"Assets/HotUpdateContent/Res/Map/{fileName}";
        }
#endif

        private static bool IsSpriteLocation(string location)
        {
            if (string.IsNullOrWhiteSpace(location))
                return false;

            string normalized = location.Trim();
            return normalized.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                   normalized.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                   normalized.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase);
        }
    }
}
