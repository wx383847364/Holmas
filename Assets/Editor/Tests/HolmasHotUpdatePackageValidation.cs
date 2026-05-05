using System;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using YooAsset;
using YooAsset.Editor;

[InitializeOnLoad]
public static class HolmasHotUpdatePackageValidation
{
    private const string PackageName = "DefaultPackage";
    private const string StatePath = "Temp/HolmasHotUpdate/package_validation_state.json";

    private static Task _playModeValidationTask;
    private static int? _pendingBatchExitCode;

    static HolmasHotUpdatePackageValidation()
    {
        EditorApplication.update += OnEditorUpdate;
    }

    [MenuItem("Holmas/Validation/Run HotUpdate Package Validation")]
    public static void RunHotUpdatePackageValidation()
    {
        try
        {
            Debug.Log("[HolmasHotUpdatePackageValidation] Start.");
            HotUpdateBuildAssetsResult assetsResult = HolmasHybridClrBuildPipeline.PrepareHotUpdateAssetsForLocalValidation();
            string packageRoot = HolmasHybridClrBuildPipeline.BuildYooAssetsPackageForLocalValidation();
            WriteState(packageRoot, assetsResult);
            EditorApplication.isPlaying = true;
        }
        catch (Exception ex)
        {
            Debug.LogError("[HolmasHotUpdatePackageValidation] Failed: " + ex);
            RequestBatchExit(1);
        }
    }

    [Serializable]
    private sealed class PackageValidationState
    {
        public string PackageRoot;
        public string HotUpdateDllAssetPath;
        public string[] AotMetadataAssetPaths;
        public string AssetSource;
    }

    private static void OnEditorUpdate()
    {
        if (_pendingBatchExitCode.HasValue &&
            Application.isBatchMode &&
            !EditorApplication.isPlaying &&
            !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            int code = _pendingBatchExitCode.Value;
            _pendingBatchExitCode = null;
            EditorApplication.delayCall += () => EditorApplication.Exit(code);
            return;
        }

        if (!File.Exists(StatePath))
        {
            _playModeValidationTask = null;
            return;
        }

        if (!EditorApplication.isPlaying)
        {
            if (!EditorApplication.isCompiling && !EditorApplication.isUpdating && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.isPlaying = true;
            }

            return;
        }

        if (_playModeValidationTask == null)
        {
            PackageValidationState state = JsonUtility.FromJson<PackageValidationState>(File.ReadAllText(StatePath));
            _playModeValidationTask = VerifyLocalPackageAsync(state.PackageRoot);
            return;
        }

        if (!_playModeValidationTask.IsCompleted)
        {
            return;
        }

        int exitCode = 0;
        if (_playModeValidationTask.IsFaulted)
        {
            Exception exception = _playModeValidationTask.Exception != null
                ? _playModeValidationTask.Exception.GetBaseException()
                : new InvalidOperationException("HotUpdate package PlayMode validation faulted.");
            Debug.LogError("[HolmasHotUpdatePackageValidation] Failed: " + exception);
            exitCode = 1;
        }
        else if (_playModeValidationTask.IsCanceled)
        {
            Debug.LogError("[HolmasHotUpdatePackageValidation] Failed: task canceled.");
            exitCode = 1;
        }
        else
        {
            Debug.Log("Holmas hotupdate package validation passed.");
        }

        _playModeValidationTask = null;
        File.Delete(StatePath);
        RequestBatchExit(exitCode);
        if (EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;
        }
    }

    private static void WriteState(string packageRoot, HotUpdateBuildAssetsResult assetsResult)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(StatePath) ?? "Temp/HolmasHotUpdate");
        var state = new PackageValidationState
        {
            PackageRoot = packageRoot,
            HotUpdateDllAssetPath = assetsResult.HotUpdateDllAssetPath,
            AotMetadataAssetPaths = assetsResult.AotMetadataAssetPaths.ToArray(),
            AssetSource = assetsResult.Source,
        };
        File.WriteAllText(StatePath, JsonUtility.ToJson(state, true));
    }

    private static async Task VerifyLocalPackageAsync(string packageRoot)
    {
        PackageValidationState state = JsonUtility.FromJson<PackageValidationState>(File.ReadAllText(StatePath));
        YooAssets.Destroy();
        YooAssets.Initialize();
        ResourcePackage package = YooAssets.CreatePackage(PackageName);
        YooAssets.SetDefaultPackage(package);

        var parameters = new OfflinePlayModeParameters
        {
            BuildinFileSystemParameters = FileSystemParameters.CreateDefaultBuildinFileSystemParameters(null, packageRoot),
        };

        InitializationOperation initOperation = package.InitializeAsync(parameters);
        await initOperation.Task;
        if (initOperation.Status != EOperationStatus.Succeed)
        {
            throw new InvalidOperationException("YooAssets package init failed: " + initOperation.Error);
        }

        var versionOperation = package.RequestPackageVersionAsync();
        await versionOperation.Task;
        if (versionOperation.Status != EOperationStatus.Succeed)
        {
            throw new InvalidOperationException("YooAssets package version failed: " + versionOperation.Error);
        }

        var manifestOperation = package.UpdatePackageManifestAsync(versionOperation.PackageVersion);
        await manifestOperation.Task;
        if (manifestOperation.Status != EOperationStatus.Succeed)
        {
            throw new InvalidOperationException("YooAssets package manifest failed: " + manifestOperation.Error);
        }

        await AssertAssetAsync<TextAsset>(package, "Assets/HotUpdateContent/Config/holmas_core_config.bytes");
        await AssertAssetAsync<TextAsset>(package, "Assets/HotUpdateContent/Config/holmas_cat_meta.bytes");
        await AssertAssetAsync<ScriptableObject>(package, "Assets/HotUpdateContent/Res/Map/11-8-8.asset");
        await AssertAssetAsync<GameObject>(package, "Assets/HotUpdateContent/Res/Perfabs/UI/MainPanel.prefab");
        await AssertAssetAsync<GameObject>(package, "Assets/HotUpdateContent/Res/Perfabs/Generated/Holmas/Portrait/AgencyMainPanel.prefab");
        await AssertAssetAsync<Font>(package, "Assets/Res/Font/NotoSansSC.ttf");
        await AssertAssetAsync<ScriptableObject>(package, "Assets/Res/Font/HolmasFontRuntimeSettings.asset");
        await AssertAssetAsync<Texture2D>(package, "Assets/HotUpdateContent/Res/Icons/cat_01.png");
        await AssertAssetAsync<TextAsset>(package, state.HotUpdateDllAssetPath);
        foreach (string metadataAssetPath in state.AotMetadataAssetPaths ?? Array.Empty<string>())
        {
            await AssertAssetAsync<TextAsset>(package, metadataAssetPath);
        }

        Debug.Log("[HolmasHotUpdatePackageValidation] YooAssets local package load checks passed. assetSource=" + state.AssetSource);
        YooAssets.Destroy();
    }

    private static async Task AssertAssetAsync<T>(ResourcePackage package, string location) where T : UnityEngine.Object
    {
        AssetHandle handle = package.LoadAssetAsync<T>(location);
        await handle.Task;
        try
        {
            if (handle.Status != EOperationStatus.Succeed || handle.AssetObject == null)
            {
                throw new InvalidOperationException($"YooAssets load failed: {location}, error={handle.LastError}");
            }

            Debug.Log("[HolmasHotUpdatePackageValidation] Loaded " + location);
        }
        finally
        {
            handle.Release();
        }
    }

    private static void RequestBatchExit(int code)
    {
        if (!Application.isBatchMode)
        {
            return;
        }

        _pendingBatchExitCode = code;
        if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += () => EditorApplication.Exit(code);
        }
    }
}
