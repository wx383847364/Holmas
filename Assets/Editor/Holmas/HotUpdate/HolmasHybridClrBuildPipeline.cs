using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using YooAsset;
using YooAsset.Editor;

public static class HolmasHybridClrBuildPipeline
{
    public const string PackageName = "DefaultPackage";
    public const string HotUpdateAssemblyName = "App.HotUpdate";
    public const string HotUpdateDllAssetPath = "Assets/HotUpdateContent/Res/HotUpdate/App.HotUpdate.dll.bytes";

    private const string BuildOutputRoot = "Library/HolmasHotUpdate/YooBuild";
    private const string BuildinFileRoot = "Library/HolmasHotUpdate/Buildin";
    private const string HybridClrPackageName = "com.code-philosophy.hybridclr";
    private const string HybridClrSettingsTypeName = "HybridCLR.Editor.Settings.HybridCLRSettings";
    private const string HybridClrPrebuildCommandTypeName = "HybridCLR.Editor.Commands.PrebuildCommand";
    private const string HybridClrInstallerControllerTypeName = "HybridCLR.Editor.Installer.InstallerController";

    public static readonly string[] AotMetadataAssemblyNames =
    {
        "mscorlib",
        "System",
        "System.Core",
        "UnityEngine.CoreModule",
        "UnityEngine.UI",
        "Unity.TextMeshPro",
        "App.Shared",
    };

    public static IReadOnlyList<string> RequiredAotMetadataAssetPaths =>
        AotMetadataAssemblyNames
            .Select(name => $"Assets/HotUpdateContent/Res/HotUpdate/Metadata/{name}.dll.bytes")
            .ToArray();

    [MenuItem("Holmas/HotUpdate/Configure HybridCLR Settings")]
    public static void ConfigureHybridClrSettingsMenu()
    {
        ConfigureHybridClrSettingsStrict();
        Debug.Log("[HolmasHybridClrBuildPipeline] HybridCLR settings configured.");
    }

    [MenuItem("Holmas/HotUpdate/Generate And Copy HybridCLR Assets")]
    public static void GenerateAndCopyHybridClrHotUpdateAssetsMenu()
    {
        HotUpdateBuildAssetsResult result = GenerateAndCopyHybridClrHotUpdateAssetsStrict();
        Debug.Log($"[HolmasHybridClrBuildPipeline] HybridCLR assets prepared. hotUpdate={result.HotUpdateDllAssetPath}, metadata={result.AotMetadataAssetPaths.Count}");
    }

    [MenuItem("Holmas/HotUpdate/Prepare Local Validation Assets")]
    public static void PrepareHotUpdateAssetsForLocalValidationMenu()
    {
        HotUpdateBuildAssetsResult result = PrepareHotUpdateAssetsForLocalValidation();
        Debug.Log($"[HolmasHybridClrBuildPipeline] Local validation assets prepared. source={result.Source}, metadata={result.AotMetadataAssetPaths.Count}");
    }

    public static HotUpdateBuildAssetsResult PrepareHotUpdateAssetsForLocalValidation()
    {
        try
        {
            if (IsHybridClrPackageAvailable() && HasInstalledHybridClr())
            {
                return GenerateAndCopyHybridClrHotUpdateAssetsStrict();
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[HolmasHybridClrBuildPipeline] HybridCLR strict prepare failed, fallback to editor script assemblies for local validation only. " + ex.Message);
        }

        return CopyEditorScriptAssembliesForLocalValidation();
    }

    public static HotUpdateBuildAssetsResult GenerateAndCopyHybridClrHotUpdateAssetsStrict()
    {
        ConfigureHybridClrSettingsStrict();
        EnsureHybridClrReady();
        InvokeHybridClrGenerateAll();
        HotUpdateBuildAssetsResult result = CopyHybridClrGeneratedAssets(EditorUserBuildSettings.activeBuildTarget);
        AssetDatabase.Refresh();
        return result;
    }

    public static void ConfigureHybridClrSettingsStrict()
    {
        Type settingsType = FindType(HybridClrSettingsTypeName);
        if (settingsType == null)
        {
            throw new InvalidOperationException($"HybridCLR package is not available. Expected package: {HybridClrPackageName}");
        }

        object settings = settingsType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
        if (settings == null)
        {
            throw new InvalidOperationException("HybridCLRSettings.Instance is not available.");
        }

        SetField(settingsType, settings, "enable", true);
        SetField(settingsType, settings, "useGlobalIl2cpp", false);
        SetStringArrayField(settingsType, settings, "hotUpdateAssemblies", new[] { HotUpdateAssemblyName });
        SetStringArrayField(settingsType, settings, "preserveHotUpdateAssemblies", Array.Empty<string>());
        SetStringArrayField(settingsType, settings, "patchAOTAssemblies", AotMetadataAssemblyNames);
        SetStringArrayField(settingsType, settings, "externalHotUpdateAssembliyDirs", Array.Empty<string>());
        SetField(settingsType, settings, "hotUpdateDllCompileOutputRootDir", "HybridCLRData/HotUpdateDlls");
        SetField(settingsType, settings, "strippedAOTDllOutputRootDir", "HybridCLRData/AssembliesPostIl2CppStrip");
        SetField(settingsType, settings, "outputLinkFile", "HybridCLRGenerate/link.xml");
        SetField(settingsType, settings, "outputAOTGenericReferenceFile", "HybridCLRGenerate/AOTGenericReferences.cs");
        ClearArrayField(settingsType, settings, "hotUpdateAssemblyDefinitions");

        MethodInfo saveMethod = settingsType.GetMethod("Save", BindingFlags.Public | BindingFlags.Static);
        saveMethod?.Invoke(null, null);
    }

    public static void ConfigureCollectors()
    {
        AssetBundleCollectorSetting setting = AssetBundleCollectorSettingData.Setting;
        setting.ClearAll();
        setting.UniqueBundleName = true;

        var package = new AssetBundleCollectorPackage
        {
            PackageName = PackageName,
            PackageDesc = "Holmas hotupdate package",
            EnableAddressable = false,
            SupportExtensionless = true,
            LocationToLower = false,
            IncludeAssetGUID = false,
            AutoCollectShaders = true,
            IgnoreRuleName = nameof(NormalIgnoreRule),
        };

        package.Groups.Add(CreateGroup("HotUpdateConfig", "Assets/HotUpdateContent/Config"));
        package.Groups.Add(CreateGroup("HotUpdateRes", "Assets/HotUpdateContent/Res"));
        setting.Packages.Add(package);

        AssetBundleCollectorSettingData.SaveFile();
        setting.CheckPackageConfigError(PackageName);
    }

    public static string BuildYooAssetsPackageForLocalValidation()
    {
        return BuildYooAssetsPackage(BuildOutputRoot, BuildinFileRoot);
    }

    public static string BuildYooAssetsPackageToStreamingAssets(string buildOutputRoot)
    {
        string buildinFileRoot = AssetBundleBuilderHelper.GetStreamingAssetsRoot();
        return BuildYooAssetsPackage(buildOutputRoot, buildinFileRoot);
    }

    public static string BuildYooAssetsPackage(string buildOutputRoot, string buildinFileRoot)
    {
        ConfigureCollectors();
        Directory.CreateDirectory(buildOutputRoot);
        Directory.CreateDirectory(buildinFileRoot);

        var buildParameters = new BuiltinBuildParameters
        {
            BuildOutputRoot = buildOutputRoot,
            BuildinFileRoot = buildinFileRoot,
            BuildPipeline = EBuildPipeline.BuiltinBuildPipeline.ToString(),
            BuildBundleType = (int)EBuildBundleType.AssetBundle,
            BuildTarget = EditorUserBuildSettings.activeBuildTarget,
            PackageName = PackageName,
            PackageVersion = DateTime.UtcNow.ToString("yyyyMMddHHmmss"),
            PackageNote = "Holmas hotupdate package",
            ClearBuildCacheFiles = true,
            UseAssetDependencyDB = true,
            EnableSharePackRule = true,
            VerifyBuildingResult = true,
            FileNameStyle = EFileNameStyle.HashName,
            BuildinFileCopyOption = EBuildinFileCopyOption.ClearAndCopyAll,
            BuildinFileCopyParams = string.Empty,
            CompressOption = ECompressOption.LZ4,
            ReplaceAssetPathWithAddress = false,
        };

        var pipeline = new BuiltinBuildPipeline();
        BuildResult result = pipeline.Run(buildParameters, true);
        if (!result.Success)
        {
            throw new InvalidOperationException("YooAssets build failed: " + result.ErrorInfo);
        }

        return Path.GetFullPath(Path.Combine(buildinFileRoot, PackageName));
    }

    private static AssetBundleCollectorGroup CreateGroup(string groupName, string collectPath)
    {
        return new AssetBundleCollectorGroup
        {
            GroupName = groupName,
            GroupDesc = "Holmas hotupdate",
            ActiveRuleName = nameof(EnableGroup),
            Collectors =
            {
                new AssetBundleCollector
                {
                    CollectPath = collectPath,
                    CollectorType = ECollectorType.MainAssetCollector,
                    AddressRuleName = nameof(AddressDisable),
                    PackRuleName = nameof(PackDirectory),
                    FilterRuleName = nameof(CollectAll),
                }
            }
        };
    }

    private static void EnsureHybridClrReady()
    {
        if (!IsHybridClrPackageAvailable())
        {
            throw new InvalidOperationException($"HybridCLR package is not installed. Add {HybridClrPackageName} to Packages/manifest.json.");
        }

        if (!HasInstalledHybridClr())
        {
            throw new InvalidOperationException("HybridCLR has not been initialized. Run HybridCLR/Installer before strict generation or IL2CPP player smoke.");
        }
    }

    private static bool IsHybridClrPackageAvailable()
    {
        return FindType(HybridClrSettingsTypeName) != null;
    }

    private static bool HasInstalledHybridClr()
    {
        Type installerType = FindType(HybridClrInstallerControllerTypeName);
        if (installerType == null)
        {
            return false;
        }

        object installer = Activator.CreateInstance(installerType);
        MethodInfo method = installerType.GetMethod("HasInstalledHybridCLR", BindingFlags.Public | BindingFlags.Instance);
        return method != null && method.Invoke(installer, null) is bool installed && installed;
    }

    private static void InvokeHybridClrGenerateAll()
    {
        Type commandType = FindType(HybridClrPrebuildCommandTypeName);
        MethodInfo method = commandType?.GetMethod("GenerateAll", BindingFlags.Public | BindingFlags.Static);
        if (method == null)
        {
            throw new MissingMethodException(HybridClrPrebuildCommandTypeName, "GenerateAll");
        }

        method.Invoke(null, null);
    }

    private static HotUpdateBuildAssetsResult CopyHybridClrGeneratedAssets(BuildTarget target)
    {
        string targetName = target.ToString();
        string hotUpdateDll = Path.Combine("HybridCLRData/HotUpdateDlls", targetName, HotUpdateAssemblyName + ".dll");
        if (!File.Exists(hotUpdateDll))
        {
            throw new FileNotFoundException("HybridCLR generated hot update dll not found.", hotUpdateDll);
        }

        CopyFileToAsset(hotUpdateDll, HotUpdateDllAssetPath);

        var metadataPaths = new List<string>();
        string metadataRoot = Path.Combine("HybridCLRData/AssembliesPostIl2CppStrip", targetName);
        foreach (string assemblyName in AotMetadataAssemblyNames)
        {
            string source = Path.Combine(metadataRoot, assemblyName + ".dll");
            if (!File.Exists(source))
            {
                throw new FileNotFoundException("HybridCLR AOT metadata dll not found.", source);
            }

            string targetAssetPath = $"Assets/HotUpdateContent/Res/HotUpdate/Metadata/{assemblyName}.dll.bytes";
            CopyFileToAsset(source, targetAssetPath);
            metadataPaths.Add(targetAssetPath);
        }

        AssetDatabase.ImportAsset(HotUpdateDllAssetPath, ImportAssetOptions.ForceUpdate);
        foreach (string assetPath in metadataPaths)
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }

        return new HotUpdateBuildAssetsResult("HybridCLR", HotUpdateDllAssetPath, metadataPaths);
    }

    private static HotUpdateBuildAssetsResult CopyEditorScriptAssembliesForLocalValidation()
    {
        CopyFileToAsset(Path.Combine("Library/ScriptAssemblies", HotUpdateAssemblyName + ".dll"), HotUpdateDllAssetPath);

        var metadataPaths = new List<string>();
        foreach (string assemblyName in AotMetadataAssemblyNames)
        {
            string source = Path.Combine("Library/ScriptAssemblies", assemblyName + ".dll");
            if (!File.Exists(source))
            {
                continue;
            }

            string targetAssetPath = $"Assets/HotUpdateContent/Res/HotUpdate/Metadata/{assemblyName}.dll.bytes";
            CopyFileToAsset(source, targetAssetPath);
            AssetDatabase.ImportAsset(targetAssetPath, ImportAssetOptions.ForceUpdate);
            metadataPaths.Add(targetAssetPath);
        }

        AssetDatabase.ImportAsset(HotUpdateDllAssetPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.Refresh();
        return new HotUpdateBuildAssetsResult("EditorScriptAssembliesFallback", HotUpdateDllAssetPath, metadataPaths);
    }

    private static void CopyFileToAsset(string sourcePath, string targetAssetPath)
    {
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("Source file not found.", sourcePath);
        }

        string targetDirectory = Path.GetDirectoryName(targetAssetPath);
        if (!string.IsNullOrWhiteSpace(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }

        File.Copy(sourcePath, targetAssetPath, true);
    }

    private static Type FindType(string fullName)
    {
        Type type = Type.GetType(fullName + ", HybridCLR.Editor");
        if (type != null)
        {
            return type;
        }

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            type = assembly.GetType(fullName, false);
            if (type != null)
            {
                return type;
            }
        }

        return null;
    }

    private static void SetStringArrayField(Type settingsType, object settings, string fieldName, IEnumerable<string> values)
    {
        FieldInfo field = settingsType.GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
        if (field == null)
        {
            throw new MissingFieldException(settingsType.FullName, fieldName);
        }

        field.SetValue(settings, values.ToArray());
    }

    private static void ClearArrayField(Type settingsType, object settings, string fieldName)
    {
        FieldInfo field = settingsType.GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
        if (field == null || !field.FieldType.IsArray)
        {
            return;
        }

        field.SetValue(settings, Array.CreateInstance(field.FieldType.GetElementType(), 0));
    }

    private static void SetField(Type settingsType, object settings, string fieldName, object value)
    {
        FieldInfo field = settingsType.GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
        if (field == null)
        {
            throw new MissingFieldException(settingsType.FullName, fieldName);
        }

        field.SetValue(settings, value);
    }
}

[Serializable]
public sealed class HotUpdateBuildAssetsResult
{
    public string Source;
    public string HotUpdateDllAssetPath;
    public List<string> AotMetadataAssetPaths = new List<string>();

    public HotUpdateBuildAssetsResult(string source, string hotUpdateDllAssetPath, IEnumerable<string> aotMetadataAssetPaths)
    {
        Source = source;
        HotUpdateDllAssetPath = hotUpdateDllAssetPath;
        AotMetadataAssetPaths = aotMetadataAssetPaths?.ToList() ?? new List<string>();
    }
}
