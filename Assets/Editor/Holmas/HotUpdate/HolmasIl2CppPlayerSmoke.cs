using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class HolmasIl2CppPlayerSmoke
{
    private const string RequestPath = "Library/holmas_il2cpp_player_smoke_request.json";
    private const string ResultPath = "Library/holmas_il2cpp_player_smoke_result.json";
    private const string OfflineDefine = "HOLMAS_YOO_OFFLINE_PLAYMODE";
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

    [MenuItem("Holmas/Validation/Build IL2CPP Player Smoke")]
    public static void RunRequestedSmoke()
    {
        SmokeRequest request = LoadRequest();
        try
        {
            BuildTarget buildTarget = ParseBuildTarget(request.BuildTargetName);
            SwitchBuildTargetIfNeeded(buildTarget);

            BuildTargetGroup buildTargetGroup = BuildPipeline.GetBuildTargetGroup(buildTarget);
            NamedBuildTarget namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup);
            ScriptingImplementation oldBackend = PlayerSettings.GetScriptingBackend(namedBuildTarget);
            string oldDefines = PlayerSettings.GetScriptingDefineSymbols(namedBuildTarget);

            try
            {
                PlayerSettings.SetScriptingBackend(namedBuildTarget, ScriptingImplementation.IL2CPP);
                PlayerSettings.SetScriptingDefineSymbols(namedBuildTarget, AddDefine(oldDefines, OfflineDefine));

                HolmasHybridClrBuildPipeline.GenerateAndCopyHybridClrHotUpdateAssetsStrict();
                string streamingRoot = HolmasHybridClrBuildPipeline.BuildYooAssetsPackageToStreamingAssets("Library/HolmasIl2CppPlayerSmoke/YooBuild");
                AssetDatabase.Refresh();

                string locationPathName = GetLocationPathName(request.OutputDirectory, buildTarget);
                Directory.CreateDirectory(Path.GetDirectoryName(locationPathName) ?? request.OutputDirectory);

                BuildPlayerOptions options = new BuildPlayerOptions
                {
                    scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
                    locationPathName = locationPathName,
                    target = buildTarget,
                    targetGroup = buildTargetGroup,
                    options = BuildOptions.Development,
                };

                BuildReport report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != BuildResult.Succeeded)
                {
                    throw new BuildFailedException($"BuildPlayer failed: result={report.summary.result}, errors={report.summary.totalErrors}");
                }

                WriteResult(new SmokeResult
                {
                    Success = true,
                    BuildTargetName = buildTarget.ToString(),
                    OutputDirectory = request.OutputDirectory,
                    PlayerPath = locationPathName,
                    StreamingAssetsPackageRoot = streamingRoot,
                });
            }
            finally
            {
                PlayerSettings.SetScriptingBackend(namedBuildTarget, oldBackend);
                PlayerSettings.SetScriptingDefineSymbols(namedBuildTarget, oldDefines);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("[HolmasIl2CppPlayerSmoke] Failed: " + ex);
            WriteResult(new SmokeResult
            {
                Success = false,
                BuildTargetName = request.BuildTargetName,
                OutputDirectory = request.OutputDirectory,
                FailureReason = ex.ToString(),
            });

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(1);
            }

            throw;
        }

        if (Application.isBatchMode)
        {
            EditorApplication.Exit(0);
        }
    }

    private static SmokeRequest LoadRequest()
    {
        if (!File.Exists(RequestPath))
        {
            return new SmokeRequest();
        }

        SmokeRequest request = JsonUtility.FromJson<SmokeRequest>(File.ReadAllText(RequestPath));
        return request ?? new SmokeRequest();
    }

    private static void WriteResult(SmokeResult result)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ResultPath) ?? "Library");
        File.WriteAllText(ResultPath, JsonUtility.ToJson(result, true), Utf8NoBom);
    }

    private static BuildTarget ParseBuildTarget(string buildTargetName)
    {
        if (!string.IsNullOrWhiteSpace(buildTargetName) &&
            Enum.TryParse(buildTargetName, true, out BuildTarget requested))
        {
            return requested;
        }

#if UNITY_EDITOR_OSX
        return BuildTarget.StandaloneOSX;
#elif UNITY_EDITOR_WIN
        return BuildTarget.StandaloneWindows64;
#else
        return BuildTarget.StandaloneLinux64;
#endif
    }

    private static void SwitchBuildTargetIfNeeded(BuildTarget buildTarget)
    {
        if (EditorUserBuildSettings.activeBuildTarget == buildTarget)
        {
            return;
        }

        BuildTargetGroup group = BuildPipeline.GetBuildTargetGroup(buildTarget);
        if (!EditorUserBuildSettings.SwitchActiveBuildTarget(group, buildTarget))
        {
            throw new BuildFailedException($"SwitchActiveBuildTarget failed: {buildTarget}");
        }
    }

    private static string GetLocationPathName(string outputDirectory, BuildTarget target)
    {
        string root = string.IsNullOrWhiteSpace(outputDirectory)
            ? Path.Combine(Path.GetTempPath(), "HolmasIl2CppPlayerSmoke", "Player")
            : outputDirectory;
        string fullRoot = Path.GetFullPath(root);

        switch (target)
        {
            case BuildTarget.StandaloneOSX:
                return Path.Combine(fullRoot, "HolmasPlayerSmoke.app");
            case BuildTarget.StandaloneWindows:
            case BuildTarget.StandaloneWindows64:
                return Path.Combine(fullRoot, "HolmasPlayerSmoke.exe");
            case BuildTarget.StandaloneLinux64:
                return Path.Combine(fullRoot, "HolmasPlayerSmoke");
            case BuildTarget.Android:
                return Path.Combine(fullRoot, "HolmasPlayerSmoke.apk");
            default:
                return fullRoot;
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

    [Serializable]
    private sealed class SmokeRequest
    {
        public string BuildTargetName = string.Empty;
        public string OutputDirectory = "";
    }

    [Serializable]
    private sealed class SmokeResult
    {
        public bool Success;
        public string BuildTargetName = string.Empty;
        public string OutputDirectory = string.Empty;
        public string PlayerPath = string.Empty;
        public string StreamingAssetsPackageRoot = string.Empty;
        public string FailureReason = string.Empty;
    }
}
