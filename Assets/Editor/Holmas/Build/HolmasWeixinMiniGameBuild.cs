using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Holmas.Editor.Build
{
    [InitializeOnLoad]
    public static class HolmasWeixinMiniGameBuild
    {
        public const string ProfilePath = "Assets/Settings/Build Profiles/WeChat Profile.asset";
        public const string OutputPath = "Builds/WeixinMiniGame/Preview";
        public const string BootstrapScenePath = "Assets/Scenes/BootstrapScene.scene";

        private const string CdnSettingsPath = "Assets/Settings/HolmasWeixinMiniGameCdnSettings.json";
        private const string ProfileTypeName = "UnityEditor.Build.Profile.BuildProfile";
        private const string MiniGameSubplatformArg = "-minigamesubplatform weixin";
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        static HolmasWeixinMiniGameBuild()
        {
            EditorApplication.delayCall += DisableUnsupportedSlimMetadataInBuildProfiles;
        }

        [MenuItem("Holmas/Build/Build Weixin MiniGame Preview")]
        public static void BuildPreviewMenu()
        {
            BuildPreview();
        }

        public static void BuildPreview()
        {
            UnityEngine.Object profile = EnsurePreviewProfile();
            string profilePath = AssetDatabase.GetAssetPath(profile);
            WeixinCdnSettings cdnSettings = LoadWeixinCdnSettings();
            EnsureWeixinCdnSettings(cdnSettings);
            ApplyWeixinCdnSettings(profilePath, cdnSettings);
            ApplyWeixinCdnSettings("Assets/WX-WASM-SDK-V2/Editor/MiniGameConfig.asset", cdnSettings);

            EnsureBootstrapSceneOnly();
            EnsureReleaseBuildOptions();
            EnsureNoOfflineDefine();
            EnsureWeixinPlayerSettings();
            EnsureHotUpdateBuildinAssets();

            profile = ReloadProfileForBuild(profilePath);
            object result = BuildMiniGame(profile);
            if (result != null && !IsMiniGameBuildSuccess(result))
            {
                throw new BuildFailedException("Weixin MiniGame build failed: " + result);
            }

            PatchExportedMiniGameAppId(profilePath);
            PatchExportedMiniGameDataCdn(cdnSettings.CdnRoot);
            PatchExportedMiniGameProjectConfig();
            Debug.Log("[HolmasWeixinMiniGameBuild] Weixin MiniGame preview build finished. output=" + Path.GetFullPath(OutputPath));
        }

        private static UnityEngine.Object EnsurePreviewProfile()
        {
            EnsureDirectory("Assets/Settings");
            EnsureDirectory("Assets/Settings/Build Profiles");
            EnsureDirectory(OutputPath);

            UnityEngine.Object profile = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(ProfilePath);
            if (profile != null)
            {
                TryConfigureProfile(profile);
                return profile;
            }

            string existingProfilePath = FindExistingWeixinBuildProfilePath();
            if (!string.IsNullOrWhiteSpace(existingProfilePath))
            {
                profile = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(existingProfilePath);
                TryConfigureProfile(profile);
                return profile;
            }

            throw new BuildFailedException(
                "Weixin MiniGame Build Profile was not found. Create one from the official Weixin MiniGame conversion panel first, then run this menu again.");
        }

        private static void TryConfigureProfile(UnityEngine.Object profile)
        {
            if (profile == null)
            {
                return;
            }

            bool changed = false;
            changed |= TrySetMember(profile, "buildPath", OutputPath);
            changed |= TrySetMember(profile, "BuildPath", OutputPath);
            changed |= TrySetMember(profile, "outputPath", OutputPath);
            changed |= TrySetMember(profile, "OutputPath", OutputPath);
            changed |= TrySetMember(profile, "productName", PlayerSettings.productName);
            changed |= TrySetMember(profile, "ProductName", PlayerSettings.productName);
            changed |= DisableUnsupportedSlimMetadata(profile);

            if (changed)
            {
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();
            }

            Debug.LogWarning(
                "[HolmasWeixinMiniGameBuild] Please verify the generated Build Profile in Inspector: subplatform=Weixin, AppID comes from the active profile/config, build path=" +
                OutputPath + ". Reflection configured common fields where available. Slim metadata is disabled because HybridCLR does not support it.");
        }

        private static bool DisableUnsupportedSlimMetadata(UnityEngine.Object profile)
        {
            string assetPath = AssetDatabase.GetAssetPath(profile);
            return DisableUnsupportedSlimMetadata(assetPath);
        }

        private static void DisableUnsupportedSlimMetadataInBuildProfiles()
        {
            const string buildProfileDirectory = "Assets/Settings/Build Profiles";
            EnsureWeixinPlayerSettings();
            if (!Directory.Exists(buildProfileDirectory))
            {
                return;
            }

            foreach (string assetPath in Directory.GetFiles(buildProfileDirectory, "*.asset"))
            {
                if (DisableUnsupportedSlimMetadata(assetPath))
                {
                    Debug.Log("[HolmasWeixinMiniGameBuild] Disabled Weixin slim metadata for HybridCLR compatibility. profile=" + assetPath);
                }
            }
        }

        private static bool DisableUnsupportedSlimMetadata(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath) || !File.Exists(assetPath))
            {
                return false;
            }

            string text = File.ReadAllText(assetPath);
            string next = text.Replace(
                "weixinMiniGameUseSlimMetaFileFormat: 1",
                "weixinMiniGameUseSlimMetaFileFormat: 0");
            string fullOutputPath = Path.GetFullPath(OutputPath).Replace("\\", "/");
            next = ReplaceYamlScalarLine(next, "relativeDST", fullOutputPath);
            next = ReplaceYamlScalarLine(next, "DST", fullOutputPath);
            if (text == next)
            {
                return false;
            }

            File.WriteAllText(assetPath, next, Utf8NoBom);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            return true;
        }

        private static WeixinCdnSettings LoadWeixinCdnSettings()
        {
            if (!File.Exists(CdnSettingsPath))
            {
                throw new BuildFailedException(
                    "Weixin MiniGame CDN settings are missing. Create " + CdnSettingsPath +
                    " and set weixinPreviewCdnRoot to an HTTPS CDN root, for example https://cdn.example.com/Holmas/WeixinPreview");
            }

            var dto = JsonUtility.FromJson<WeixinCdnSettingsDto>(File.ReadAllText(CdnSettingsPath));
            string cdnRoot = NormalizeCdnRoot(dto?.weixinPreviewCdnRoot);
            string fallbackRoot = NormalizeCdnRoot(dto?.weixinPreviewFallbackCdnRoot);
            if (string.IsNullOrEmpty(fallbackRoot))
            {
                fallbackRoot = cdnRoot;
            }

            return new WeixinCdnSettings(cdnRoot, fallbackRoot);
        }

        private static void EnsureWeixinCdnSettings(WeixinCdnSettings settings)
        {
            ValidateHttpsCdnRoot(settings.CdnRoot, "weixinPreviewCdnRoot");
            ValidateHttpsCdnRoot(settings.FallbackCdnRoot, "weixinPreviewFallbackCdnRoot");
        }

        private static void ValidateHttpsCdnRoot(string url, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new BuildFailedException(
                    fieldName + " is empty. Weixin MiniGame YooAssets requires an HTTPS CDN root. Expected version URL: " +
                    "{CDN_ROOT}/StreamingAssets/yoo/DefaultPackage/DefaultPackage.version");
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri) ||
                !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                throw new BuildFailedException(fieldName + " must be an HTTPS CDN root, current value=" + url);
            }

            if (IsLocalOrPrivateHost(uri.Host))
            {
                throw new BuildFailedException(fieldName + " must be a public HTTPS download domain configured in the Weixin backend, current value=" + url);
            }
        }

        private static bool IsLocalOrPrivateHost(string host)
        {
            if (string.IsNullOrWhiteSpace(host))
            {
                return true;
            }

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
            {
                return false;
            }

            return int.TryParse(parts[1], out int secondOctet) && secondOctet >= 16 && secondOctet <= 31;
        }

        private static string NormalizeCdnRoot(string url)
        {
            return string.IsNullOrWhiteSpace(url) ? string.Empty : url.Trim().TrimEnd('/');
        }

        private static void ApplyWeixinCdnSettings(string assetPath, WeixinCdnSettings settings)
        {
            if (string.IsNullOrWhiteSpace(assetPath) || !File.Exists(assetPath))
            {
                return;
            }

            if (ReplaceFileText(assetPath, text => ReplaceYamlScalarLine(text, "CDN", settings.CdnRoot)))
            {
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                Debug.Log("[HolmasWeixinMiniGameBuild] Patched Weixin CDN root. asset=" + assetPath + ", CDN=" + settings.CdnRoot);
            }
        }

        private static string ReplaceYamlScalarLine(string text, string key, string value)
        {
            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = lines[i].TrimStart();
                if (!trimmed.StartsWith(key + ":", StringComparison.Ordinal))
                {
                    continue;
                }

                string indentation = lines[i].Substring(0, lines[i].Length - trimmed.Length);
                lines[i] = indentation + key + ": " + value;
            }

            return string.Join("\n", lines);
        }

        private static void PatchExportedMiniGameAppId(string profilePath)
        {
            string appId = ReadWeixinProfileAppId(profilePath);
            if (string.IsNullOrWhiteSpace(appId))
            {
                return;
            }

            string miniGameRoot = Path.Combine(OutputPath, "minigame");
            string projectConfigPath = Path.Combine(miniGameRoot, "project.config.json");
            string gameJsPath = Path.Combine(miniGameRoot, "game.js");

            if (ReplaceFileText(projectConfigPath, text => ReplaceJsonStringScalarLine(text, "appid", appId)))
            {
                Debug.Log("[HolmasWeixinMiniGameBuild] Patched exported project.config.json appid=" + appId);
            }

            if (ReplaceFileText(gameJsPath, text => ReplaceJsStringPropertyLine(text, "APPID", appId)))
            {
                Debug.Log("[HolmasWeixinMiniGameBuild] Patched exported game.js APPID=" + appId);
            }
        }

        private static void PatchExportedMiniGameDataCdn(string cdnRoot)
        {
            string gameJsPath = Path.Combine(OutputPath, "minigame", "game.js");
            if (ReplaceFileText(gameJsPath, text => ReplaceJsStringPropertyLine(text, "DATA_CDN", cdnRoot)))
            {
                Debug.Log("[HolmasWeixinMiniGameBuild] Patched exported game.js DATA_CDN=" + cdnRoot);
            }
        }

        private static void PatchExportedMiniGameProjectConfig()
        {
            string projectConfigPath = Path.Combine(OutputPath, "minigame", "project.config.json");
            if (!ReplaceFileText(projectConfigPath, PatchMiniGameProjectConfigText))
            {
                return;
            }

            Debug.Log("[HolmasWeixinMiniGameBuild] Patched exported project.config.json for stable Weixin DevTools preview.");
        }

        private static string PatchMiniGameProjectConfigText(string text)
        {
            string next = text;
            next = ReplaceJsonStringScalarLine(next, "libVersion", "3.15.2");
            next = ReplaceJsonBooleanScalarLine(next, "useIsolateContext", false);
            next = ReplaceJsonBooleanScalarLine(next, "useCompilerModule", false);
            next = ReplaceJsonBooleanScalarLine(next, "userConfirmedUseCompilerModuleSwitch", true);
            next = ReplaceJsonKeyLine(next, "currentL", "current");
            return next;
        }

        private static string ReadWeixinProfileAppId(string profilePath)
        {
            if (string.IsNullOrWhiteSpace(profilePath) || !File.Exists(profilePath))
            {
                return null;
            }

            foreach (string line in File.ReadAllLines(profilePath))
            {
                string trimmed = line.Trim();
                if (!trimmed.StartsWith("Appid:", StringComparison.Ordinal))
                {
                    continue;
                }

                return trimmed.Substring("Appid:".Length).Trim();
            }

            return null;
        }

        private static bool ReplaceFileText(string path, Func<string, string> replace)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return false;
            }

            string text = File.ReadAllText(path);
            string next = replace(text);
            if (text == next)
            {
                return false;
            }

            File.WriteAllText(path, next, Utf8NoBom);
            return true;
        }

        private static string ReplaceJsonStringScalarLine(string text, string key, string value)
        {
            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            string prefix = "\"" + key + "\"";
            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = lines[i].TrimStart();
                if (!trimmed.StartsWith(prefix, StringComparison.Ordinal))
                {
                    continue;
                }

                int colonIndex = trimmed.IndexOf(':');
                if (colonIndex < 0)
                {
                    continue;
                }

                string indentation = lines[i].Substring(0, lines[i].Length - trimmed.Length);
                string suffix = trimmed.TrimEnd().EndsWith(",", StringComparison.Ordinal) ? "," : string.Empty;
                lines[i] = indentation + prefix + ": \"" + value + "\"" + suffix;
            }

            return string.Join("\n", lines);
        }

        private static string ReplaceJsonBooleanScalarLine(string text, string key, bool value)
        {
            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            string prefix = "\"" + key + "\"";
            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = lines[i].TrimStart();
                if (!trimmed.StartsWith(prefix, StringComparison.Ordinal))
                {
                    continue;
                }

                int colonIndex = trimmed.IndexOf(':');
                if (colonIndex < 0)
                {
                    continue;
                }

                string indentation = lines[i].Substring(0, lines[i].Length - trimmed.Length);
                string suffix = trimmed.TrimEnd().EndsWith(",", StringComparison.Ordinal) ? "," : string.Empty;
                lines[i] = indentation + prefix + ": " + (value ? "true" : "false") + suffix;
            }

            return string.Join("\n", lines);
        }

        private static string ReplaceJsonKeyLine(string text, string oldKey, string newKey)
        {
            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            string oldPrefix = "\"" + oldKey + "\"";
            string newPrefix = "\"" + newKey + "\"";
            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = lines[i].TrimStart();
                if (!trimmed.StartsWith(oldPrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                string indentation = lines[i].Substring(0, lines[i].Length - trimmed.Length);
                lines[i] = indentation + newPrefix + trimmed.Substring(oldPrefix.Length);
            }

            return string.Join("\n", lines);
        }

        private static string ReplaceJsStringPropertyLine(string text, string key, string value)
        {
            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            string prefix = key + ":";
            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = lines[i].TrimStart();
                if (!trimmed.StartsWith(prefix, StringComparison.Ordinal))
                {
                    continue;
                }

                string indentation = lines[i].Substring(0, lines[i].Length - trimmed.Length);
                string suffix = trimmed.TrimEnd().EndsWith(",", StringComparison.Ordinal) ? "," : string.Empty;
                lines[i] = indentation + prefix + " '" + value + "'" + suffix;
            }

            return string.Join("\n", lines);
        }

        private static void EnsureReleaseBuildOptions()
        {
            EditorUserBuildSettings.development = false;
            EditorUserBuildSettings.connectProfiler = false;
            EditorUserBuildSettings.buildWithDeepProfilingSupport = false;
        }

        private static void EnsureWeixinPlayerSettings()
        {
            DisableSlimMetadataViaPlayerSettings();
            DisableUnsupportedSlimMetadata("ProjectSettings/ProjectSettings.asset");
        }

        private static void DisableSlimMetadataViaPlayerSettings()
        {
            Type miniGameType = typeof(PlayerSettings).GetNestedType("MiniGame", BindingFlags.Public | BindingFlags.NonPublic);
            if (miniGameType == null)
            {
                return;
            }

            PropertyInfo property = miniGameType.GetProperty("useSlimMetaFileFormat", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (property != null && property.CanWrite)
            {
                property.SetValue(null, false);
                return;
            }

            MethodInfo setter = miniGameType.GetMethod("set_useSlimMetaFileFormat", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            setter?.Invoke(null, new object[] { false });
        }

        private static string FindExistingWeixinBuildProfilePath()
        {
            const string buildProfileDirectory = "Assets/Settings/Build Profiles";
            if (!Directory.Exists(buildProfileDirectory))
            {
                return null;
            }

            return Directory.GetFiles(buildProfileDirectory, "*.asset")
                .FirstOrDefault(path =>
                {
                    string text = File.ReadAllText(path);
                    return text.Contains("m_ModuleName: WeixinMiniGame") &&
                           !text.Contains("m_BuildTarget: -2");
                });
        }

        private static void EnsureBootstrapSceneOnly()
        {
            var scene = new EditorBuildSettingsScene(BootstrapScenePath, true);
            EditorBuildSettings.scenes = new[] { scene };
        }

        private static void EnsureNoOfflineDefine()
        {
            NamedBuildTarget buildTarget = NamedBuildTarget.FromBuildTargetGroup(BuildTargetGroup.WebGL);
            string oldDefines = PlayerSettings.GetScriptingDefineSymbols(buildTarget);
            const string offlineDefine = "HOLMAS_YOO_OFFLINE_PLAYMODE";
            string nextDefines = string.Join(
                ";",
                oldDefines
                    .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(define => !string.Equals(define, offlineDefine, StringComparison.Ordinal)));

            if (!string.Equals(oldDefines, nextDefines, StringComparison.Ordinal))
            {
                PlayerSettings.SetScriptingDefineSymbols(buildTarget, nextDefines);
            }
        }

        private static void EnsureHotUpdateBuildinAssets()
        {
            HolmasHybridClrBuildPipeline.PrepareHotUpdateAssetsForLocalValidation();
            HolmasHybridClrBuildPipeline.BuildYooAssetsPackageToStreamingAssets("Library/HolmasWeixinMiniGamePreview/YooBuild");
            AssetDatabase.Refresh();
        }

        private static object BuildMiniGame(UnityEngine.Object profile)
        {
            MethodInfo buildPipelineMethod = FindBuildPipelineBuildMiniGameMethod();
            if (buildPipelineMethod != null)
            {
                return buildPipelineMethod.Invoke(null, new object[] { profile });
            }

            return BuildWithWeixinSubplatform(profile);
        }

        private static UnityEngine.Object ReloadProfileForBuild(string profilePath)
        {
            string path = string.IsNullOrWhiteSpace(profilePath) ? ProfilePath : profilePath;
            UnityEngine.Object profile = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (profile == null)
            {
                throw new BuildFailedException("Weixin MiniGame Build Profile could not be loaded before build. path=" + path);
            }

            Type buildProfileType = FindType(ProfileTypeName);
            if (buildProfileType != null && !buildProfileType.IsInstanceOfType(profile))
            {
                throw new BuildFailedException(
                    "Weixin MiniGame Build Profile asset has unexpected type. path=" + path +
                    ", actualType=" + profile.GetType().FullName);
            }

            return profile;
        }

        private static MethodInfo FindBuildPipelineBuildMiniGameMethod()
        {
            MethodInfo buildPipelineMethod = typeof(BuildPipeline)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(method =>
                    method.Name == "BuildMiniGame" &&
                    method.GetParameters().Length == 1 &&
                    method.GetParameters()[0].ParameterType.FullName == ProfileTypeName);

            if (buildPipelineMethod != null)
            {
                return buildPipelineMethod;
            }

            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly =>
                {
                    try
                    {
                        return assembly.GetTypes();
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        return ex.Types.Where(type => type != null);
                    }
                })
                .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                .FirstOrDefault(method =>
                    method.Name == "BuildMiniGame" &&
                    method.GetParameters().Length == 1 &&
                    method.GetParameters()[0].ParameterType.FullName == ProfileTypeName);
        }

        private static object BuildWithWeixinSubplatform(UnityEngine.Object profile)
        {
            Type subplatformType = FindType("WeChatWASM.WeixinSubplatformInterface");
            if (subplatformType == null)
            {
                throw new BuildFailedException(
                    "WeixinSubplatformInterface is not available. Switch the project to the Tuanjie MiniGame platform and start batchmode with " +
                    MiniGameSubplatformArg + ".");
            }

            object subplatform = Activator.CreateInstance(subplatformType);
            MethodInfo buildMethod = subplatformType
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(method =>
                {
                    if (method.Name != "Build")
                    {
                        return false;
                    }

                    ParameterInfo[] parameters = method.GetParameters();
                    return parameters.Length == 2 &&
                           parameters[0].ParameterType.FullName == ProfileTypeName &&
                           parameters[1].ParameterType == typeof(BuildOptions);
                });

            if (buildMethod == null)
            {
                throw new BuildFailedException("WeixinSubplatformInterface.Build(BuildProfile, BuildOptions) was not found.");
            }

            return buildMethod.Invoke(subplatform, new object[] { profile, BuildOptions.None });
        }

        private static bool IsMiniGameBuildSuccess(object result)
        {
            string text = result.ToString();
            return string.Equals(text, "Success", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(text, "None", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(text, "Succeeded", StringComparison.OrdinalIgnoreCase) ||
                   Convert.ToInt32(result) == 0;
        }

        private static bool TrySetMember(UnityEngine.Object target, string name, object value)
        {
            Type type = target.GetType();
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            PropertyInfo property = type.GetProperty(name, flags);
            if (property != null && property.CanWrite && IsAssignableValue(property.PropertyType, value))
            {
                property.SetValue(target, value);
                return true;
            }

            FieldInfo field = type.GetField(name, flags);
            if (field != null && IsAssignableValue(field.FieldType, value))
            {
                field.SetValue(target, value);
                return true;
            }

            return false;
        }

        private static bool IsAssignableValue(Type targetType, object value)
        {
            return value == null || targetType.IsInstanceOfType(value);
        }

        private sealed class WeixinCdnSettings
        {
            public readonly string CdnRoot;
            public readonly string FallbackCdnRoot;

            public WeixinCdnSettings(string cdnRoot, string fallbackCdnRoot)
            {
                CdnRoot = cdnRoot;
                FallbackCdnRoot = fallbackCdnRoot;
            }
        }

        [Serializable]
        private sealed class WeixinCdnSettingsDto
        {
            public string weixinPreviewCdnRoot;
            public string weixinPreviewFallbackCdnRoot;
        }

        private static Type FindType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private static void EnsureDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }
    }
}
