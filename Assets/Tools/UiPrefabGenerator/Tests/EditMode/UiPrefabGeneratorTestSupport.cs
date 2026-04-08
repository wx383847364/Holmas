using System.IO;
using UnityEditor;
using UnityEngine;

namespace UiPrefabGenerator.Tests.EditMode
{
    internal static class UiPrefabGeneratorTestSupport
    {
        public static void CleanupGeneratedDraftRoot()
        {
            DeleteAssetIfExists("Assets/Res/Perfabs/Generated/Holmas/AgencyMainPanel.prefab");
            DeleteAssetIfExists("Assets/Res/Perfabs/Generated/Holmas/Portrait/AgencyMainPanel.prefab");
            DeleteAssetIfExists("Assets/Res/Perfabs/Generated/Holmas/Portrait");
            DeleteAssetIfExists("Assets/Res/Perfabs/Generated/Holmas");
            DeleteAssetIfExists("Assets/Res/Perfabs/Generated");
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

        private static void DeleteAssetIfExists(string assetPath)
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(assetPath) != null || AssetDatabase.IsValidFolder(assetPath))
            {
                AssetDatabase.DeleteAsset(assetPath);
            }
        }
    }
}
