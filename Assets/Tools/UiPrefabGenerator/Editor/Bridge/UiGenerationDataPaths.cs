using System.IO;
using UnityEditor;
using UnityEngine;

namespace UiPrefabGenerator.Editor.Bridge
{
    public static class UiGenerationDataPaths
    {
        public const string DataRoot = "Assets/UiPrefabGeneratorData";
        public const string TemplatesRoot = DataRoot + "/Templates";
        public const string ProjectDefaultsRoot = TemplatesRoot + "/ProjectDefaults";
        public const string PageTypesRoot = TemplatesRoot + "/PageTypes";
        public const string TasksRoot = DataRoot + "/Tasks";
        public const string CacheRoot = DataRoot + "/Cache";

        public const string RequestFileName = "request.json";
        public const string AnalysisResultFileName = "analysis_result.json";
        public const string DesignPacketFileName = "design_packet.json";
        public const string UiPrefabSpecFileName = "ui_prefab_spec.json";
        public const string ResourceMatchReportFileName = "resource_match_report.json";
        public const string AnalysisSummaryFileName = "analysis_summary.md";
        public const string ManifestFileName = "prefab_binding_manifest.json";
        public const string ExecutionResultFileName = "generation_result.json";

        public static string DefaultPortraitTemplatePath
        {
            get { return ProjectDefaultsRoot + "/holmas_portrait_wechat_default.json"; }
        }

        public static void EnsureDataFolders()
        {
            EnsureFolderExists(DataRoot);
            EnsureFolderExists(TemplatesRoot);
            EnsureFolderExists(ProjectDefaultsRoot);
            EnsureFolderExists(PageTypesRoot);
            EnsureFolderExists(TasksRoot);
            EnsureFolderExists(CacheRoot);
        }

        public static string ToAbsolutePath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
        }

        public static string ToAssetPath(string absolutePath)
        {
            string normalized = absolutePath.Replace('\\', '/');
            string projectAssetsRoot = Application.dataPath.Replace('\\', '/');
            if (!normalized.StartsWith(projectAssetsRoot))
            {
                return string.Empty;
            }

            return "Assets" + normalized.Substring(projectAssetsRoot.Length);
        }

        public static string GetTaskDirectory(string taskId)
        {
            return TasksRoot + "/" + (taskId ?? string.Empty);
        }

        public static void EnsureFolderExists(string assetFolderPath)
        {
            if (AssetDatabase.IsValidFolder(assetFolderPath))
            {
                return;
            }

            string normalized = assetFolderPath.Replace('\\', '/').TrimEnd('/');
            string parent = Path.GetDirectoryName(normalized).Replace('\\', '/');
            string folderName = Path.GetFileName(normalized);
            if (!string.IsNullOrWhiteSpace(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolderExists(parent);
            }

            if (!string.IsNullOrWhiteSpace(parent) && !AssetDatabase.IsValidFolder(normalized))
            {
                AssetDatabase.CreateFolder(parent, folderName);
            }
        }
    }
}
