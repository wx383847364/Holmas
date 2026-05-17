using System;
using System.IO;
using System.Linq;
using System.Reflection;
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

        private const string OfflineDefine = "HOLMAS_YOO_OFFLINE_PLAYMODE";
        private const string ProfileTypeName = "UnityEditor.Build.Profile.BuildProfile";
        private const string MiniGameSubplatformArg = "-minigamesubplatform weixin";

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

            EnsureBootstrapSceneOnly();
            EnsureReleaseBuildOptions();
            EnsureOfflineDefine();
            EnsureWeixinPlayerSettings();
            EnsureHotUpdateBuildinAssets();

            object result = BuildMiniGame(profile);
            if (result != null && !IsMiniGameBuildSuccess(result))
            {
                throw new BuildFailedException("Weixin MiniGame build failed: " + result);
            }

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
                "[HolmasWeixinMiniGameBuild] Please verify the generated Build Profile in Inspector: subplatform=Weixin, AppID=test AppID, build path=" +
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

            File.WriteAllText(assetPath, next);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            return true;
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

        private static void EnsureOfflineDefine()
        {
            NamedBuildTarget buildTarget = NamedBuildTarget.FromBuildTargetGroup(BuildTargetGroup.WebGL);
            string oldDefines = PlayerSettings.GetScriptingDefineSymbols(buildTarget);
            if (oldDefines.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Contains(OfflineDefine))
            {
                return;
            }

            string nextDefines = string.IsNullOrWhiteSpace(oldDefines)
                ? OfflineDefine
                : oldDefines + ";" + OfflineDefine;
            PlayerSettings.SetScriptingDefineSymbols(buildTarget, nextDefines);
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
