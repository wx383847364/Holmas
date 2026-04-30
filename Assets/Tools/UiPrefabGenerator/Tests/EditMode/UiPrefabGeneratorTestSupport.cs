using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UiPrefabGenerator.Tests.EditMode
{
    internal static class UiPrefabGeneratorTestSupport
    {
        private static readonly string[] PreservedFolderPaths =
        {
            "Assets/HotUpdateContent/Res/Perfabs/Generated",
            "Assets/HotUpdateContent/Res/Perfabs/Generated/Holmas",
            "Assets/HotUpdateContent/Res/Perfabs/Generated/Holmas/Portrait",
        };

        private static readonly string[] PreservedAssetPaths =
        {
            "Assets/HotUpdateContent/Res/Perfabs/Generated.meta",
            "Assets/HotUpdateContent/Res/Perfabs/Generated/Holmas.meta",
            "Assets/HotUpdateContent/Res/Perfabs/Generated/Holmas/Portrait.meta",
            "Assets/HotUpdateContent/Res/Perfabs/Generated/Holmas/Portrait/AgencyMainPanel.prefab",
            "Assets/HotUpdateContent/Res/Perfabs/Generated/Holmas/Portrait/AgencyMainPanel.prefab.meta",
        };

        private static readonly Dictionary<string, string> PreservedAssetContents = new Dictionary<string, string>();
        private static bool _preservedAssetSnapshotCaptured;

        public static void PreserveTrackedGeneratedAssets()
        {
            CapturePreservedAssetsIfNeeded();
        }

        public static void CleanupGeneratedDraftRoot()
        {
            PreserveTrackedGeneratedAssets();
            DeleteAssetIfExists("Assets/HotUpdateContent/Res/Perfabs/Generated/Holmas/AgencyMainPanel.prefab");
            DeleteAssetIfExists("Assets/HotUpdateContent/Res/Perfabs/Generated/Holmas/Portrait/AgencyMainPanel.prefab");
            DeleteAssetIfExists("Assets/HotUpdateContent/Res/Perfabs/Generated/Holmas/Portrait");
            DeleteAssetIfExists("Assets/HotUpdateContent/Res/Perfabs/Generated/Holmas");
            DeleteAssetIfExists("Assets/HotUpdateContent/Res/Perfabs/Generated");
            AssetDatabase.Refresh();
            RestorePreservedAssets();
            AssetDatabase.Refresh();
        }

        public static void DeleteAssetIfExistsForTests(string assetPath)
        {
            DeleteAssetIfExists(assetPath);
            AssetDatabase.Refresh();
        }

        public static string ToAbsolutePath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
        }

        private static void CapturePreservedAssetsIfNeeded()
        {
            if (_preservedAssetSnapshotCaptured)
            {
                return;
            }

            PreservedAssetContents.Clear();
            for (int i = 0; i < PreservedAssetPaths.Length; i++)
            {
                string assetPath = PreservedAssetPaths[i];
                string absolutePath = ToAbsolutePath(assetPath);
                if (File.Exists(absolutePath))
                {
                    PreservedAssetContents[assetPath] = File.ReadAllText(absolutePath);
                }
            }

            _preservedAssetSnapshotCaptured = true;
        }

        private static void RestorePreservedAssets()
        {
            if (!_preservedAssetSnapshotCaptured)
            {
                return;
            }

            for (int i = 0; i < PreservedFolderPaths.Length; i++)
            {
                Directory.CreateDirectory(ToAbsolutePath(PreservedFolderPaths[i]));
            }

            for (int i = 0; i < PreservedAssetPaths.Length; i++)
            {
                string assetPath = PreservedAssetPaths[i];
                if (!PreservedAssetContents.TryGetValue(assetPath, out string content))
                {
                    continue;
                }

                string absolutePath = ToAbsolutePath(assetPath);
                string directory = Path.GetDirectoryName(absolutePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(absolutePath, content);
            }
        }

        private static void DeleteAssetIfExists(string assetPath)
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(assetPath) != null || AssetDatabase.IsValidFolder(assetPath))
            {
                AssetDatabase.DeleteAsset(assetPath);
            }
        }
    }
}
