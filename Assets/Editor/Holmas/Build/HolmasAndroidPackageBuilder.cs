#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Holmas.EditorTools;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Holmas.Editor.Build
{
    public sealed class HolmasAndroidPackageBuilderWindow : EditorWindow
    {
        private bool buildFullFlow = true;
        private bool buildFirstPackage = true;
        private string fullFlowOutputPath = HolmasAndroidPackageBuilder.FullFlowApkPath;
        private string firstPackageOutputPath = HolmasAndroidPackageBuilder.FirstPackageApkPath;

        [MenuItem("Holmas/Build/Android Package Builder")]
        public static void Open()
        {
            var window = GetWindow<HolmasAndroidPackageBuilderWindow>("Android Package Builder");
            window.minSize = new Vector2(480f, 280f);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Android Offline APK", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Builds local Android APKs with IL2CPP, HybridCLR, YooAssets built into StreamingAssets, and debug signing. These are not Development Builds.",
                MessageType.Info);

            EditorGUILayout.Space();
            buildFullFlow = EditorGUILayout.ToggleLeft("完整流程离线 APK（Debug 签名）", buildFullFlow);
            using (new EditorGUI.DisabledScope(!buildFullFlow))
            {
                fullFlowOutputPath = EditorGUILayout.TextField("Output APK", fullFlowOutputPath);
            }

            EditorGUILayout.Space();
            buildFirstPackage = EditorGUILayout.ToggleLeft("首包离线 APK（Debug 签名）", buildFirstPackage);
            using (new EditorGUI.DisabledScope(!buildFirstPackage))
            {
                firstPackageOutputPath = EditorGUILayout.TextField("Output APK", firstPackageOutputPath);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Fixed Settings", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Format", "APK");
            EditorGUILayout.LabelField("Signing", "Debug automatic signing");
            EditorGUILayout.LabelField("Development Build", "False");
            EditorGUILayout.LabelField("Architectures", "ARMv7 | ARM64");
            EditorGUILayout.LabelField("Play Mode", "HOLMAS_YOO_OFFLINE_PLAYMODE");
            EditorGUILayout.LabelField("Scene", HolmasAndroidPackageBuilder.BootstrapScenePath);

            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("Build Selected APKs", GUILayout.Height(34f)))
                {
                    BuildSelected();
                }
            }
        }

        private void BuildSelected()
        {
            var requests = new List<HolmasAndroidBuildRequest>();
            if (buildFullFlow)
            {
                requests.Add(HolmasAndroidBuildRequest.FullFlow(fullFlowOutputPath));
            }

            if (buildFirstPackage)
            {
                requests.Add(HolmasAndroidBuildRequest.FirstPackage(firstPackageOutputPath));
            }

            if (requests.Count == 0)
            {
                EditorUtility.DisplayDialog("Android Package Builder", "请至少选择一个 Android 包任务。", "OK");
                return;
            }

            try
            {
                HolmasAndroidBuildBatchResult result = HolmasAndroidPackageBuilder.BuildMany(requests);
                string message = result.Success
                    ? "Android APK 构建完成。"
                    : "Android APK 构建失败，请查看 Console 和 Library/holmas_android_build_result.json。";
                EditorUtility.DisplayDialog("Android Package Builder", message, "OK");
            }
            catch (Exception ex)
            {
                Debug.LogError("[HolmasAndroidPackageBuilder] Build failed: " + ex);
                EditorUtility.DisplayDialog("Android Package Builder", "Android APK 构建失败，请查看 Console 和 Library/holmas_android_build_result.json。", "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }
    }

    public static class HolmasAndroidPackageBuilder
    {
        public const string BootstrapScenePath = "Assets/Scenes/BootstrapScene.scene";
        public const string FullFlowApkPath = "Builds/Android/FullFlow/Holmas-FullFlow.apk";
        public const string FirstPackageApkPath = "Builds/Android/FirstPackage/Holmas-FirstPackage.apk";

        private const string BatchResultPath = "Library/holmas_android_build_result.json";
        private const string FullFlowResultPath = "Library/holmas_android_build_result_full_flow_offline.json";
        private const string FirstPackageResultPath = "Library/holmas_android_build_result_first_package_offline.json";
        private const string OfflineDefine = "HOLMAS_YOO_OFFLINE_PLAYMODE";
        private const string FullFlowYooBuildRoot = "Library/HolmasAndroidBuild/FullFlow/YooBuild";
        private const string FirstPackageYooBuildRoot = "Library/HolmasAndroidBuild/FirstPackage/YooBuild";
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        [MenuItem("Holmas/Build/Build Android Full Flow Offline Debug-Signed APK")]
        public static void BuildFullFlowMenu()
        {
            Build(HolmasAndroidBuildRequest.FullFlow(FullFlowApkPath));
        }

        [MenuItem("Holmas/Build/Build Android First Package Offline Debug-Signed APK")]
        public static void BuildFirstPackageMenu()
        {
            Build(HolmasAndroidBuildRequest.FirstPackage(FirstPackageApkPath));
        }

        public static HolmasAndroidBuildResult Build(HolmasAndroidBuildRequest request)
        {
            HolmasAndroidBuildResult result = BuildInternal(request);
            WriteSingleResult(result);
            WriteBatchResult(HolmasAndroidBuildBatchResult.FromSingle(result));
            if (!result.Success)
            {
                throw new BuildFailedException(result.FailureReason);
            }

            return result;
        }

        public static HolmasAndroidBuildBatchResult BuildMany(IReadOnlyList<HolmasAndroidBuildRequest> requests)
        {
            if (requests == null || requests.Count == 0)
            {
                throw new ArgumentException("At least one Android build request is required.", nameof(requests));
            }

            var batchResult = new HolmasAndroidBuildBatchResult
            {
                StartedAtUtc = DateTime.UtcNow.ToString("o"),
            };

            foreach (HolmasAndroidBuildRequest request in requests)
            {
                HolmasAndroidBuildResult result = BuildInternal(request);
                batchResult.Results.Add(result);
                WriteSingleResult(result);

                if (!result.Success)
                {
                    batchResult.Success = false;
                    batchResult.FailureReason = result.FailureReason;
                    batchResult.FinishedAtUtc = DateTime.UtcNow.ToString("o");
                    WriteBatchResult(batchResult);
                    throw new BuildFailedException(result.FailureReason);
                }
            }

            batchResult.Success = true;
            batchResult.FinishedAtUtc = DateTime.UtcNow.ToString("o");
            WriteBatchResult(batchResult);
            return batchResult;
        }

        private static HolmasAndroidBuildResult BuildInternal(HolmasAndroidBuildRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            request.Normalize();
            string startedAtUtc = DateTime.UtcNow.ToString("o");
            var result = new HolmasAndroidBuildResult
            {
                Mode = request.Mode.ToString(),
                ModeDescription = GetModeDescription(request.Mode),
                OutputDirectory = Path.GetDirectoryName(request.OutputApkPath) ?? string.Empty,
                ApkPath = request.OutputApkPath,
                StartedAtUtc = startedAtUtc,
                PlayModeDefine = OfflineDefine,
                Signing = "Debug automatic signing",
                DevelopmentBuild = false,
            };

            try
            {
                EditorUtility.DisplayProgressBar("Android Package Builder", "Exporting xlsx config...", 0.05f);
                HolmasConfigExportReport configReport = HolmasConfigBinaryExporter.ExportAll();
                if (configReport == null || !configReport.Success)
                {
                    int errorCount = configReport?.ErrorCount ?? -1;
                    throw new BuildFailedException("Holmas config export failed. errorCount=" + errorCount);
                }

                using (var scope = new AndroidBuildSettingsScope())
                {
                    EditorUtility.DisplayProgressBar("Android Package Builder", "Applying Android build settings...", 0.15f);
                    scope.Apply();
                    EnsureBootstrapSceneOnly();
                    EnsureOutputDirectory(request.OutputApkPath);
                    DeleteExistingOutput(request.OutputApkPath);

                    EditorUtility.DisplayProgressBar("Android Package Builder", "Generating HybridCLR assets...", 0.35f);
                    HotUpdateBuildAssetsResult hotUpdateAssets = HolmasHybridClrBuildPipeline.GenerateAndCopyHybridClrHotUpdateAssetsStrict();

                    EditorUtility.DisplayProgressBar("Android Package Builder", "Building YooAssets package...", 0.55f);
                    string yooPackageRoot = HolmasHybridClrBuildPipeline.BuildYooAssetsPackageToStreamingAssets(GetYooBuildRoot(request.Mode));
                    AssetDatabase.Refresh();

                    EditorUtility.DisplayProgressBar("Android Package Builder", "Building Android APK...", 0.75f);
                    BuildReport report = BuildPlayer(request.OutputApkPath);
                    FillSuccessResult(result, hotUpdateAssets, yooPackageRoot, report);
                    if (request.Mode == HolmasAndroidBuildMode.FullFlowOfflineDebugSignedApk)
                    {
                        ValidateFullFlowOutputs(result, request.OutputApkPath, yooPackageRoot);
                    }
                }
            }
            catch (Exception ex)
            {
                FillFailureResult(result, ex);
                if (ex is AndroidBuildFailedException buildFailedException)
                {
                    FillBuildSummary(result, buildFailedException.Report);
                }

                return result;
            }
            finally
            {
                result.FinishedAtUtc = DateTime.UtcNow.ToString("o");
                EditorUtility.ClearProgressBar();
            }

            Debug.Log("[HolmasAndroidPackageBuilder] Android build finished. mode=" + result.Mode + ", apk=" + Path.GetFullPath(result.ApkPath));
            return result;
        }

        private static BuildReport BuildPlayer(string apkPath)
        {
            var options = new BuildPlayerOptions
            {
                scenes = new[] { BootstrapScenePath },
                locationPathName = apkPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new AndroidBuildFailedException(report);
            }

            return report;
        }

        private static void FillSuccessResult(
            HolmasAndroidBuildResult result,
            HotUpdateBuildAssetsResult hotUpdateAssets,
            string yooPackageRoot,
            BuildReport report)
        {
            result.Success = true;
            result.FailureReason = string.Empty;
            result.YooPackageRoot = yooPackageRoot;
            result.HotUpdateDllAssetPath = hotUpdateAssets?.HotUpdateDllAssetPath ?? string.Empty;
            result.AotMetadataAssetPaths = hotUpdateAssets?.AotMetadataAssetPaths ?? new List<string>();
            result.BuildTarget = BuildTarget.Android.ToString();
            result.ActiveBuildTarget = EditorUserBuildSettings.activeBuildTarget.ToString();
            result.ScriptingBackend = ScriptingImplementation.IL2CPP.ToString();
            result.Defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(BuildTargetGroup.Android));
            result.Architectures = PlayerSettings.Android.targetArchitectures.ToString();
            FillBuildSummary(result, report);
            result.FinishedAtUtc = DateTime.UtcNow.ToString("o");
        }

        private static void FillFailureResult(HolmasAndroidBuildResult result, Exception ex)
        {
            result.Success = false;
            result.FailureReason = ex.ToString();
            result.BuildTarget = SafeGetActiveBuildTarget();
            result.ActiveBuildTarget = result.BuildTarget;
            result.ScriptingBackend = SafeGetScriptingBackend();
            result.Defines = SafeGetDefines();
            result.Architectures = SafeGetArchitectures();
            result.FinishedAtUtc = DateTime.UtcNow.ToString("o");
        }

        private static void FillBuildSummary(HolmasAndroidBuildResult result, BuildReport report)
        {
            if (report == null)
            {
                return;
            }

            result.BuildResult = report.summary.result.ToString();
            result.TotalErrors = report.summary.totalErrors;
            result.TotalWarnings = report.summary.totalWarnings;
            result.TotalSizeBytes = report.summary.totalSize.ToString();
        }

        private static void ValidateFullFlowOutputs(HolmasAndroidBuildResult result, string apkPath, string yooPackageRoot)
        {
            var validations = new List<string>();
            if (!File.Exists(apkPath))
            {
                throw new BuildFailedException("Android APK was not found after BuildPlayer. path=" + apkPath);
            }

            validations.Add("apk:ok");

            string versionPath = Path.Combine(yooPackageRoot ?? string.Empty, HolmasHybridClrBuildPipeline.PackageName + ".version");
            if (!File.Exists(versionPath))
            {
                throw new BuildFailedException("YooAssets build-in package version file was not found. path=" + versionPath);
            }

            validations.Add("yoo-version:ok");
            result.ValidationSummary = string.Join(";", validations);
        }

        private static void EnsureBootstrapSceneOnly()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(BootstrapScenePath, true)
            };
        }

        private static void EnsureOutputDirectory(string apkPath)
        {
            string directory = Path.GetDirectoryName(apkPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        private static void DeleteExistingOutput(string apkPath)
        {
            if (File.Exists(apkPath))
            {
                File.Delete(apkPath);
            }
        }

        private static string GetYooBuildRoot(HolmasAndroidBuildMode mode)
        {
            return mode == HolmasAndroidBuildMode.FirstPackageOfflineDebugSignedApk
                ? FirstPackageYooBuildRoot
                : FullFlowYooBuildRoot;
        }

        private static void WriteSingleResult(HolmasAndroidBuildResult result)
        {
            string path = GetSingleResultPath(result.Mode);
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? "Library");
            File.WriteAllText(path, JsonUtility.ToJson(result, true), Utf8NoBom);
        }

        private static void WriteBatchResult(HolmasAndroidBuildBatchResult result)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(BatchResultPath) ?? "Library");
            File.WriteAllText(BatchResultPath, JsonUtility.ToJson(result, true), Utf8NoBom);
        }

        private static string GetSingleResultPath(string mode)
        {
            return string.Equals(mode, HolmasAndroidBuildMode.FirstPackageOfflineDebugSignedApk.ToString(), StringComparison.Ordinal)
                ? FirstPackageResultPath
                : FullFlowResultPath;
        }

        private static string GetModeDescription(HolmasAndroidBuildMode mode)
        {
            return mode == HolmasAndroidBuildMode.FirstPackageOfflineDebugSignedApk
                ? "First package offline APK with debug automatic signing; YooAssets are built into StreamingAssets."
                : "Full local offline Android pipeline APK with debug automatic signing; includes config export, HybridCLR generation, YooAssets build, APK build, and result reports.";
        }

        private static string SafeGetActiveBuildTarget()
        {
            try
            {
                return EditorUserBuildSettings.activeBuildTarget.ToString();
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private static string SafeGetScriptingBackend()
        {
            try
            {
                return PlayerSettings.GetScriptingBackend(NamedBuildTarget.FromBuildTargetGroup(BuildTargetGroup.Android)).ToString();
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private static string SafeGetDefines()
        {
            try
            {
                return PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(BuildTargetGroup.Android));
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private static string SafeGetArchitectures()
        {
            try
            {
                return PlayerSettings.Android.targetArchitectures.ToString();
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private sealed class AndroidBuildSettingsScope : IDisposable
        {
            private readonly BuildTarget previousBuildTarget;
            private readonly BuildTargetGroup previousBuildTargetGroup;
            private readonly EditorBuildSettingsScene[] previousScenes;
            private readonly NamedBuildTarget androidNamedBuildTarget;
            private readonly ScriptingImplementation previousBackend;
            private readonly string previousDefines;
            private readonly AndroidArchitecture previousArchitectures;
            private readonly bool previousUseCustomKeystore;
            private readonly string previousKeystoreName;
            private readonly string previousKeystorePass;
            private readonly string previousKeyaliasName;
            private readonly string previousKeyaliasPass;
            private readonly bool previousDevelopment;
            private readonly bool previousConnectProfiler;
            private readonly bool previousDeepProfiling;
            private readonly bool previousAllowDebugging;
            private readonly bool previousBuildAppBundle;
            private readonly bool previousExportAsGoogleAndroidProject;

            private bool disposed;

            public AndroidBuildSettingsScope()
            {
                previousBuildTarget = EditorUserBuildSettings.activeBuildTarget;
                previousBuildTargetGroup = BuildPipeline.GetBuildTargetGroup(previousBuildTarget);
                previousScenes = EditorBuildSettings.scenes;
                androidNamedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(BuildTargetGroup.Android);
                previousBackend = PlayerSettings.GetScriptingBackend(androidNamedBuildTarget);
                previousDefines = PlayerSettings.GetScriptingDefineSymbols(androidNamedBuildTarget);
                previousArchitectures = PlayerSettings.Android.targetArchitectures;
                previousUseCustomKeystore = PlayerSettings.Android.useCustomKeystore;
                previousKeystoreName = PlayerSettings.Android.keystoreName;
                previousKeystorePass = PlayerSettings.Android.keystorePass;
                previousKeyaliasName = PlayerSettings.Android.keyaliasName;
                previousKeyaliasPass = PlayerSettings.Android.keyaliasPass;
                previousDevelopment = EditorUserBuildSettings.development;
                previousConnectProfiler = EditorUserBuildSettings.connectProfiler;
                previousDeepProfiling = EditorUserBuildSettings.buildWithDeepProfilingSupport;
                previousAllowDebugging = EditorUserBuildSettings.allowDebugging;
                previousBuildAppBundle = EditorUserBuildSettings.buildAppBundle;
                previousExportAsGoogleAndroidProject = EditorUserBuildSettings.exportAsGoogleAndroidProject;
            }

            public void Apply()
            {
                SwitchBuildTargetIfNeeded(BuildTarget.Android);
                PlayerSettings.SetScriptingBackend(androidNamedBuildTarget, ScriptingImplementation.IL2CPP);
                PlayerSettings.SetScriptingDefineSymbols(androidNamedBuildTarget, AddDefine(previousDefines, OfflineDefine));
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;
                PlayerSettings.Android.useCustomKeystore = false;

                EditorUserBuildSettings.development = false;
                EditorUserBuildSettings.connectProfiler = false;
                EditorUserBuildSettings.buildWithDeepProfilingSupport = false;
                EditorUserBuildSettings.allowDebugging = false;
                EditorUserBuildSettings.buildAppBundle = false;
                EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
            }

            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;

                var restoreErrors = new List<string>();
                TryRestore(() => PlayerSettings.SetScriptingBackend(androidNamedBuildTarget, previousBackend), "scripting backend", restoreErrors);
                TryRestore(() => PlayerSettings.SetScriptingDefineSymbols(androidNamedBuildTarget, previousDefines), "Android define symbols", restoreErrors);
                TryRestore(() => PlayerSettings.Android.targetArchitectures = previousArchitectures, "Android architectures", restoreErrors);
                TryRestore(() => PlayerSettings.Android.useCustomKeystore = previousUseCustomKeystore, "Android keystore mode", restoreErrors);
                TryRestore(() => PlayerSettings.Android.keystoreName = previousKeystoreName, "Android keystore name", restoreErrors);
                TryRestore(() => PlayerSettings.Android.keystorePass = previousKeystorePass, "Android keystore password", restoreErrors);
                TryRestore(() => PlayerSettings.Android.keyaliasName = previousKeyaliasName, "Android keyalias name", restoreErrors);
                TryRestore(() => PlayerSettings.Android.keyaliasPass = previousKeyaliasPass, "Android keyalias password", restoreErrors);
                TryRestore(() => EditorUserBuildSettings.development = previousDevelopment, "development build flag", restoreErrors);
                TryRestore(() => EditorUserBuildSettings.connectProfiler = previousConnectProfiler, "connect profiler flag", restoreErrors);
                TryRestore(() => EditorUserBuildSettings.buildWithDeepProfilingSupport = previousDeepProfiling, "deep profiling flag", restoreErrors);
                TryRestore(() => EditorUserBuildSettings.allowDebugging = previousAllowDebugging, "allow debugging flag", restoreErrors);
                TryRestore(() => EditorUserBuildSettings.buildAppBundle = previousBuildAppBundle, "build app bundle flag", restoreErrors);
                TryRestore(() => EditorUserBuildSettings.exportAsGoogleAndroidProject = previousExportAsGoogleAndroidProject, "export Gradle project flag", restoreErrors);
                TryRestore(() => EditorBuildSettings.scenes = previousScenes, "Editor build scenes", restoreErrors);
                TryRestore(() => SwitchBuildTargetIfNeeded(previousBuildTarget, previousBuildTargetGroup), "active build target", restoreErrors);

                if (restoreErrors.Count > 0)
                {
                    throw new InvalidOperationException("Failed to restore Android build settings:\n" + string.Join("\n", restoreErrors));
                }
            }
        }

        private static void TryRestore(Action restore, string label, List<string> restoreErrors)
        {
            try
            {
                restore();
            }
            catch (Exception ex)
            {
                string message = label + ": " + ex.Message;
                restoreErrors.Add(message);
                Debug.LogError("[HolmasAndroidPackageBuilder] Failed to restore " + message);
            }
        }

        private static void SwitchBuildTargetIfNeeded(BuildTarget buildTarget)
        {
            SwitchBuildTargetIfNeeded(buildTarget, BuildPipeline.GetBuildTargetGroup(buildTarget));
        }

        private static void SwitchBuildTargetIfNeeded(BuildTarget buildTarget, BuildTargetGroup buildTargetGroup)
        {
            if (EditorUserBuildSettings.activeBuildTarget == buildTarget)
            {
                return;
            }

            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(buildTargetGroup, buildTarget))
            {
                throw new BuildFailedException("SwitchActiveBuildTarget failed: " + buildTarget);
            }
        }

        private static string AddDefine(string original, string define)
        {
            var defines = new HashSet<string>(
                (original ?? string.Empty).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries),
                StringComparer.Ordinal);
            defines.Add(define);
            return string.Join(";", defines);
        }

        private sealed class AndroidBuildFailedException : Exception
        {
            public readonly BuildReport Report;

            public AndroidBuildFailedException(BuildReport report)
                : base($"BuildPlayer failed: result={report.summary.result}, errors={report.summary.totalErrors}")
            {
                Report = report;
            }
        }
    }

    public enum HolmasAndroidBuildMode
    {
        FullFlowOfflineDebugSignedApk,
        FirstPackageOfflineDebugSignedApk,
    }

    [Serializable]
    public sealed class HolmasAndroidBuildRequest
    {
        public HolmasAndroidBuildMode Mode;
        public string OutputApkPath;

        public static HolmasAndroidBuildRequest FullFlow(string outputApkPath)
        {
            return new HolmasAndroidBuildRequest
            {
                Mode = HolmasAndroidBuildMode.FullFlowOfflineDebugSignedApk,
                OutputApkPath = outputApkPath,
            };
        }

        public static HolmasAndroidBuildRequest FirstPackage(string outputApkPath)
        {
            return new HolmasAndroidBuildRequest
            {
                Mode = HolmasAndroidBuildMode.FirstPackageOfflineDebugSignedApk,
                OutputApkPath = outputApkPath,
            };
        }

        public void Normalize()
        {
            if (string.IsNullOrWhiteSpace(OutputApkPath))
            {
                OutputApkPath = Mode == HolmasAndroidBuildMode.FirstPackageOfflineDebugSignedApk
                    ? HolmasAndroidPackageBuilder.FirstPackageApkPath
                    : HolmasAndroidPackageBuilder.FullFlowApkPath;
            }

            if (!OutputApkPath.EndsWith(".apk", StringComparison.OrdinalIgnoreCase))
            {
                OutputApkPath = Path.Combine(OutputApkPath, Mode == HolmasAndroidBuildMode.FirstPackageOfflineDebugSignedApk
                    ? "Holmas-FirstPackage.apk"
                    : "Holmas-FullFlow.apk");
            }
        }
    }

    [Serializable]
    public sealed class HolmasAndroidBuildResult
    {
        public bool Success;
        public string Mode = string.Empty;
        public string ModeDescription = string.Empty;
        public string FailureReason = string.Empty;
        public string ApkPath = string.Empty;
        public string OutputDirectory = string.Empty;
        public string StartedAtUtc = string.Empty;
        public string FinishedAtUtc = string.Empty;
        public string BuildTarget = string.Empty;
        public string ActiveBuildTarget = string.Empty;
        public string ScriptingBackend = string.Empty;
        public string Defines = string.Empty;
        public string Architectures = string.Empty;
        public string PlayModeDefine = string.Empty;
        public string Signing = string.Empty;
        public bool DevelopmentBuild;
        public string ValidationSummary = string.Empty;
        public string YooPackageRoot = string.Empty;
        public string HotUpdateDllAssetPath = string.Empty;
        public List<string> AotMetadataAssetPaths = new List<string>();
        public string BuildResult = string.Empty;
        public int TotalErrors;
        public int TotalWarnings;
        public string TotalSizeBytes = string.Empty;
    }

    [Serializable]
    public sealed class HolmasAndroidBuildBatchResult
    {
        public bool Success;
        public string FailureReason = string.Empty;
        public string StartedAtUtc = string.Empty;
        public string FinishedAtUtc = string.Empty;
        public List<HolmasAndroidBuildResult> Results = new List<HolmasAndroidBuildResult>();

        public static HolmasAndroidBuildBatchResult FromSingle(HolmasAndroidBuildResult result)
        {
            return new HolmasAndroidBuildBatchResult
            {
                Success = result != null && result.Success,
                FailureReason = result == null || result.Success ? string.Empty : result.FailureReason,
                StartedAtUtc = result?.StartedAtUtc ?? string.Empty,
                FinishedAtUtc = result?.FinishedAtUtc ?? string.Empty,
                Results = result == null
                    ? new List<HolmasAndroidBuildResult>()
                    : new List<HolmasAndroidBuildResult> { result },
            };
        }
    }
}
#endif
